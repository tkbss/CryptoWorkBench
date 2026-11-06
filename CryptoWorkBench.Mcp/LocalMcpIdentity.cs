using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CryptoWorkBench.Mcp;

/// <summary>One session CA and leaf. Only the public CA is exported to a locked trust file.</summary>
internal sealed class LocalMcpIdentity : IDisposable
{
    private readonly FileStream? trustFile;
    private readonly string? directory;
    private int disposed;
    internal Task PublicCleanup { get; private set; } = Task.CompletedTask;
    internal bool PublicCleanupPending { get; private set; }
    internal X509Certificate2 Certificate { get; }
    internal DateTimeOffset ExpiresAt { get; }
    internal string Fingerprint => Convert.ToHexString(SHA256.HashData(Certificate.RawData));
    internal string TrustBundlePath { get; } = "";

    internal LocalMcpIdentity(TimeSpan lifetime, Action<X509Certificate2, string>? beforePublicExport = null)
    {
        ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        using var authorityKey = RSA.Create(2048);
        var authorityRequest = new CertificateRequest("CN=CryptoWorkBench session " + Guid.NewGuid(),
            authorityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        authorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var authority = authorityRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAt.AddMinutes(1));
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=CryptoWorkBench", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var signed = request.Create(authority, DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAt, RandomNumberGenerator.GetBytes(16));
        using var created = signed.CopyWithPrivateKey(key);
        // Schannel needs a temporary user key container. Default import owns and deletes
        // that container on certificate disposal; never use PersistKeySet or Exportable.
        byte[] pfx = created.Export(X509ContentType.Pfx);
        try { Certificate = X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.UserKeySet); }
        finally { CryptographicOperations.ZeroMemory(pfx); }
        try
        {
            // Ownership begins immediately after import, including directory creation.
            directory = Directory.CreateTempSubdirectory("cryptoworkbench-ca-").FullName;
            TrustBundlePath = Path.Combine(directory, "session-ca.pem");
            // Keep Windows sharing protection for the entire activation. The file is public,
            // but changing the trusted CA during pairing would change the authenticated peer.
            trustFile = new FileStream(TrustBundlePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            try
            {
                beforePublicExport?.Invoke(Certificate, TrustBundlePath);
                trustFile.Write(Encoding.ASCII.GetBytes(authority.ExportCertificatePem())); trustFile.Flush(true);
            }
            catch { trustFile.Dispose(); throw; }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        // Private key release is critical and must not be hidden as a housekeeping fault.
        Certificate.Dispose();
        trustFile?.Dispose();
        PublicCleanup = CleanupPublicFilesAsync();
    }

    private async Task CleanupPublicFilesAsync()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (TrustBundlePath.Length != 0) File.Delete(TrustBundlePath);
                // Never recursively remove a directory or follow unrelated files.
                if (directory != null) Directory.Delete(directory);
                PublicCleanupPending = false;
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                PublicCleanupPending = true;
                if (attempt < 2) await Task.Delay(100 * (attempt + 1)).ConfigureAwait(false);
            }
        }
        System.Diagnostics.Trace.TraceWarning("Workbench public session certificate cleanup deferred; private identity released.");
    }
}
