using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CryptoScript.Model;

public enum PasswordPurpose { SecureSave, SecureLoad }
public enum PasswordResponseKind { Supplied, UserCancelled }
public enum PasswordProtocolError
{
    PasswordHostUnavailable, PasswordHostAmbiguous, PasswordHostContractViolation,
    PasswordHostFailure, PasswordConfirmationMismatch, InvalidPassword, PasswordRequestBusy
}

public sealed class PasswordProtocolException : Exception
{
    public PasswordProtocolError Code { get; }
    internal PasswordProtocolException(PasswordProtocolError code)
        : base("Password request failed: " + code + ".") => Code = code;
}

public sealed class SecurePasswordCancelledException : Exception
{
    public SecurePasswordCancelledException() : base("Secure operation cancelled by user.") { }
}

public delegate Task<PasswordResponse> PasswordRequestHandler(object sender, PasswordRequestEventArgs request);

public sealed class PasswordRequestEventArgs : EventArgs
{
    internal PasswordRequestEventArgs(Guid runnerId, Guid executionId, PasswordPurpose purpose, CancellationToken token)
    {
        RunnerId = runnerId; ExecutionId = executionId; RequestId = Guid.NewGuid();
        Purpose = purpose; CancellationToken = token;
    }
    public Guid RunnerId { get; }
    public Guid ExecutionId { get; }
    public Guid RequestId { get; }
    public PasswordPurpose Purpose { get; }
    public bool ConfirmationRequired => Purpose == PasswordPurpose.SecureSave;
    public CancellationToken CancellationToken { get; }
}

/// <summary>Exclusively owned mutable buffer. Borrowed views must not outlive ownership.
/// This cannot erase copies made by a GUI, the caller, GC or a serializer.</summary>
public sealed class PasswordSecret : IDisposable
{
    private char[]? characters;
    private bool transferred;
    internal static readonly object OwnershipGate = new();
    private PasswordSecret(char[] characters) => this.characters = characters;
    public static PasswordSecret TakeOwnership(char[] characters)
    {
        ArgumentNullException.ThrowIfNull(characters);
        return new PasswordSecret(characters);
    }
    [System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public ReadOnlyMemory<char> Characters
    {
        get { lock (OwnershipGate) return characters ?? throw new ObjectDisposedException(nameof(PasswordSecret)); }
    }
    internal char[] Buffer => characters ?? throw new PasswordProtocolException(PasswordProtocolError.PasswordHostContractViolation);
    internal bool CanTransfer => characters is not null && !transferred;
    internal void Transfer() => transferred = true;
    public void Dispose()
    {
        lock (OwnershipGate)
        {
            if (characters is null) return;
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan()));
            characters = null;
        }
    }
    public override string ToString() => nameof(PasswordSecret);
}

public sealed class PasswordResponse : IDisposable
{
    private int consumed;
    private PasswordResponse(Guid id, PasswordResponseKind kind, PasswordSecret? password, PasswordSecret? confirmation)
    { RequestId = id; Kind = kind; Password = password; Confirmation = confirmation; }
    public Guid RequestId { get; }
    public PasswordResponseKind Kind { get; }
    [System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public PasswordSecret? Password { get; }
    [System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public PasswordSecret? Confirmation { get; }
    public static PasswordResponse Supplied(Guid requestId, PasswordSecret password, PasswordSecret? confirmation = null)
    {
        lock (PasswordSecret.OwnershipGate)
        {
            if (password is null || !password.CanTransfer || confirmation is not null &&
                (!confirmation.CanTransfer || ReferenceEquals(password.Buffer, confirmation.Buffer)))
                throw new PasswordProtocolException(PasswordProtocolError.PasswordHostContractViolation);
            var response = new PasswordResponse(requestId, PasswordResponseKind.Supplied, password, confirmation);
            password.Transfer(); confirmation?.Transfer();
            return response;
        }
    }
    public static PasswordResponse UserCancelled(Guid requestId) => new(requestId, PasswordResponseKind.UserCancelled, null, null);
    internal void Consume()
    {
        if (Interlocked.Exchange(ref consumed, 1) != 0)
            throw new PasswordProtocolException(PasswordProtocolError.PasswordHostContractViolation);
    }
    public void Dispose() { Interlocked.Exchange(ref consumed, 1); Password?.Dispose(); Confirmation?.Dispose(); }
    public override string ToString() => nameof(PasswordResponse);
}

internal static class PasswordValidation
{
    internal static void Validate(PasswordResponse response, PasswordRequestEventArgs request)
    {
        response.Consume();
        if (response.RequestId != request.RequestId)
            throw new PasswordProtocolException(PasswordProtocolError.PasswordHostContractViolation);
        if (response.Kind == PasswordResponseKind.UserCancelled) throw new SecurePasswordCancelledException();
        if (response.Kind != PasswordResponseKind.Supplied || response.Password is null ||
            request.ConfirmationRequired != (response.Confirmation is not null))
            throw new PasswordProtocolException(PasswordProtocolError.PasswordHostContractViolation);
        byte[]? password = null, confirmation = null;
        try
        {
            password = Encode(response.Password);
            if (response.Confirmation is not null)
            {
                confirmation = Encode(response.Confirmation);
                if (!CryptographicOperations.FixedTimeEquals(password, confirmation))
                    throw new PasswordProtocolException(PasswordProtocolError.PasswordConfirmationMismatch);
            }
        }
        finally
        {
            if (password is not null) CryptographicOperations.ZeroMemory(password);
            if (confirmation is not null) CryptographicOperations.ZeroMemory(confirmation);
        }
    }
    internal static byte[] Encode(PasswordSecret secret)
    {
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            var chars = secret.Characters.Span;
            int size = utf8.GetByteCount(chars);
            if (size is < 1 or > 1024) throw new PasswordProtocolException(PasswordProtocolError.InvalidPassword);
            var bytes = new byte[size];
            utf8.GetBytes(chars, bytes);
            return bytes;
        }
        catch (EncoderFallbackException) { throw new PasswordProtocolException(PasswordProtocolError.InvalidPassword); }
        catch (ObjectDisposedException) { throw new PasswordProtocolException(PasswordProtocolError.PasswordHostContractViolation); }
    }
}
