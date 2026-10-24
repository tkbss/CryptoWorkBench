using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.FileEncryption;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class F05FileTests
{
    private string directory = null!;
    private static readonly string[] Mechanisms = ["AES-CBC", "AES-ECB", "AES-CTR", "AES-GCM", "AES-CCM", "DES3-CBC", "DES3-ECB"];
    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "F05-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsMacOS() && directory.StartsWith("/var/")) directory = "/private" + directory;
        Directory.CreateDirectory(directory);
    }
    [TearDown] public void Cleanup() => Directory.Delete(directory, true);
    private PathVariableDeclaration Source(byte[] data)
    {
        string path = Path.Combine(directory, "input"); File.WriteAllBytes(path, data);
        return new() { Value = path, Type = new CryptoTypePath() };
    }
    private static AlgorithmCallArguments Args(ParameterVariableDeclaration p, KeyVariableDeclaration k, PathVariableDeclaration path) =>
        new([new(p.Value, p), new(k.Value, k), new(path.Value, path)]);
    private static ParameterVariableDeclaration Parameters(string m, string padding, bool automatic)
    {
        var p = m.StartsWith("DES3") ? new DES3().GenerateParameters(m) : new AES().GenerateParameters(m);
        if (m.EndsWith("CBC") || m.EndsWith("ECB")) p.SetParameter("PAD", padding);
        if (!automatic)
        {
            string name = m.EndsWith("CBC") ? "IV" : "NONCE";
            if (!m.EndsWith("ECB")) p.RecordExplicitParameter(name);
            if (m == "AES-CTR") p.RecordExplicitParameter("COUNTER");
        }
        return p;
    }

    [TestCase("AES-ECB")] [TestCase("AES-GCM")]
    public void ScriptRejectsExplicitPaddingEvenWhenLegacyFactoryNormalizesIt(string mechanism)
    {
        var previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
        try
        {
            var source = Source(new byte[16]); source.Id = "source";
            VariableDictionary.Instance().Add(source);
            string script = $"KEY k = GenerateKey({mechanism}, 128)\nPARAM p = Parameters({mechanism}, #PAD:PKCS-7)\nPATH result = Encrypt(p,k,source)";
            var parser = new CryptoScriptParser(new Antlr4.Runtime.CommonTokenStream(
                new CryptoScriptLexer(new Antlr4.Runtime.AntlrInputStream(script))));
            Assert.Catch(() => new CryptoScriptRunner().Execute(parser.program()));
            Assert.That(VariableDictionary.Instance().Get("result"), Is.Null);
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }
        finally
        {
            VariableDictionary.Instance().Clear();
            foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
        }
    }
    public static IEnumerable<TestCaseData> Matrix()
    {
        foreach (string m in Mechanisms)
        foreach (int size in m.StartsWith("DES3") ? new[] { 16, 24 } : new[] { 16, 24, 32 })
        foreach (string pad in m.EndsWith("CBC") || m.StartsWith("DES3") ? PaddingRegistry.Entries.Select(p => p.CanonicalName) : new[] { "NONE" })
        foreach (bool automatic in new[] { true, false })
            yield return new TestCaseData(m, size, pad, automatic);
    }

    [TestCaseSource(nameof(Matrix))]
    public void FileMatrixAndRepeatedParameters(string m, int size, string pad, bool automatic)
    {
        var p = Parameters(m, pad, automatic);
        var key = F05ReferenceTests.Key(Enumerable.Range(0, size).Select(i => (byte)i).ToArray());
        string original = p.Value;
        int block = m.StartsWith("DES3") ? 8 : 16;
        foreach (int length in new[] { 0, 1, block - 1, block, block + 1, block * 3, 65535, 65536, 65537 })
        {
            byte[] data = Enumerable.Range(0, length).Select(i => (byte)(i * 29)).ToArray();
            if (length > 0) data[^1] = 0;
            var path = Source(data);
            bool invalid = pad == "NONE" && (m.EndsWith("CBC") || m.EndsWith("ECB")) &&
                (length % block != 0 || length == 0 && m != "AES-ECB");
            if (invalid)
            {
                Assert.Throws<ArgumentException>(() => FileEncryptCoordinator.Encrypt(Args(p, key, path), path));
                continue;
            }
            var encrypted = FileEncryptCoordinator.Encrypt(Args(p, key, path), path);
            var decrypted = FileDecryptCoordinator.Decrypt(Args(p, key, encrypted), encrypted);
            Assert.That(File.ReadAllBytes(decrypted.Value), Is.EqualTo(data));
            Assert.That(p.Value, Is.EqualTo(original));
            File.Delete(encrypted.Value); File.Delete(decrypted.Value);
        }
        Assert.That(Directory.GetFiles(directory, ".cwe-*.tmp"), Is.Empty);
    }

    [TestCase("AES-CBC")] [TestCase("AES-CTR")] [TestCase("AES-GCM")] [TestCase("AES-CCM")] [TestCase("DES3-CBC")]
    public void FreshAutomaticValuesAndExplicitConflicts(string m)
    {
        var p = Parameters(m, "PKCS-7", true);
        var k = F05ReferenceTests.Key(Enumerable.Range(0, 16).Select(i => (byte)i).ToArray());
        var path = Source([0, 1, 255, 0]);
        var first = FileEncryptCoordinator.Encrypt(Args(p, k, path), path);
        var second = FileEncryptCoordinator.Encrypt(Args(p, k, path), path);
        Assert.That(File.ReadAllBytes(first.Value), Is.Not.EqualTo(File.ReadAllBytes(second.Value)));
        string name = m.EndsWith("CBC") ? "IV" : "NONCE";
        p.SetParameter(name, "0x(00)"); p.RecordExplicitParameter(name);
        Assert.Throws<ArgumentException>(() => FileDecryptCoordinator.Decrypt(Args(p, k, first), first));
        Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(3));
    }

    [TestCase("AES-GCM", "tag")] [TestCase("AES-CCM", "tag")]
    [TestCase("AES-GCM", "cipher")] [TestCase("AES-CCM", "cipher")]
    [TestCase("AES-GCM", "header")] [TestCase("AES-CCM", "header")]
    [TestCase("AES-GCM", "key")] [TestCase("AES-CCM", "key")]
    [TestCase("AES-GCM", "nonce")] [TestCase("AES-CCM", "nonce")]
    public void AeadNeverPublishesUnauthenticatedPlaintext(string m, string damage)
    {
        var p = Parameters(m, "NONE", true); var k = F05ReferenceTests.Key(new byte[16]);
        var source = Source(new byte[100003]);
        var encrypted = FileEncryptCoordinator.Encrypt(Args(p, k, source), source);
        byte[] file = File.ReadAllBytes(encrypted.Value);
        if (damage == "key") k = F05ReferenceTests.Key(Enumerable.Repeat((byte)1, 16).ToArray());
        else if (damage == "tag") file[^1] ^= 1;
        else if (damage == "cipher") file[^17] ^= 1;
        else
        {
            int offset = Encoding.UTF8.GetString(file, 0, 16 + (int)BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(12)))
                .IndexOf(damage == "nonce" ? "\"nonce\":\"" : "\"associatedData\":\"", StringComparison.Ordinal);
            offset += damage == "nonce" ? 9 : 18;
            file[offset] = file[offset] == (byte)'A' ? (byte)'B' : (byte)'A';
        }
        File.WriteAllBytes(encrypted.Value, file);
        Assert.Catch(() => FileDecryptCoordinator.Decrypt(Args(p, k, encrypted), encrypted));
        Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(2));
    }

    [TestCase("AES-GCM")] [TestCase("AES-CCM")]
    public void RawFrameIsAuthenticatedWithoutReserialization(string m)
    {
        var p = Parameters(m, "NONE", true);
        var h = FileCipherParameters.Create(p, 3);
        byte[] normal = EncryptedFileCodec.Encode(h);
        byte[] json = Encoding.UTF8.GetBytes(" \n" + Encoding.UTF8.GetString(normal, 16, normal.Length - 16));
        byte[] frame = new byte[16 + json.Length]; normal.AsSpan(0, 16).CopyTo(frame);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(12), (uint)json.Length); json.CopyTo(frame, 16);
        byte[] plain = [1, 2, 3], cipher = new byte[3], tag = new byte[16], key = new byte[16];
        if (m == "AES-GCM")
        {
            using var reference = new AesGcm(key, 16); reference.Encrypt(Convert.FromBase64String(h.Nonce!), plain, cipher, tag, frame);
        }
        else
        {
            if (!AesCcm.IsSupported) Assert.Ignore("Platform has no independent system CCM provider; Python fixtures still run.");
            using var reference = new AesCcm(key); reference.Encrypt(Convert.FromBase64String(h.Nonce!), plain, cipher, tag, frame);
        }
        using var source = new MemoryStream(frame.Concat(cipher).Concat(tag).ToArray());
        var (parsed, raw) = EncryptedFileCodec.Read(source);
        using var result = new MemoryStream();
        FileCipherStream.Process(source, result, F05ReferenceTests.Key(key), parsed, raw, false, default);
        Assert.That(result.ToArray(), Is.EqualTo(plain));
    }

    [TestCase("AES-GCM")] [TestCase("AES-CCM")]
    public void MultiMibMatchesSystemProvider(string m)
    {
        if (m == "AES-CCM" && !AesCcm.IsSupported) Assert.Ignore("No system CCM; independent fixtures still run.");
        byte[] data = new byte[2 * 1024 * 1024 + 7]; RandomNumberGenerator.Fill(data);
        byte[] key = new byte[32], expected = new byte[data.Length], tag = new byte[16];
        var h = FileCipherParameters.Create(Parameters(m, "NONE", true), data.Length);
        byte[] frame = EncryptedFileCodec.Encode(h), nonce = Convert.FromBase64String(h.Nonce!);
        if (m == "AES-GCM") { using var c = new AesGcm(key, 16); c.Encrypt(nonce, data, expected, tag, frame); }
        else { using var c = new AesCcm(key); c.Encrypt(nonce, data, expected, tag, frame); }
        using var result = new MemoryStream();
        FileCipherStream.Process(new MemoryStream(data), result, F05ReferenceTests.Key(key), h, frame, true, default);
        Assert.That(result.ToArray(), Is.EqualTo(expected.Concat(tag).ToArray()));
    }

    [TestCase("AES-CTR", 33)] [TestCase("AES-CCM", 16777216)] [TestCase("AES-GCM", 68719476705)]
    public void LimitsFailBeforeReading(string m, long length)
    {
        var p = Parameters(m, "NONE", true);
        if (m == "AES-CTR") p.SetParameter("COUNTER", "0x(FFFFFFFE)");
        var h = FileCipherParameters.Create(p, length);
        using var source = new SyntheticStream(length);
        Assert.Throws<ArgumentException>(() => FileCipherStream.Process(source, Stream.Null,
            F05ReferenceTests.Key(new byte[16]), h, EncryptedFileCodec.Encode(h), true, default));
        Assert.That(source.Position, Is.Zero);
    }

    [TestCase("AES-GCM")] [TestCase("AES-CCM")] [TestCase("AES-CTR")]
    public void LargeStreamingHasBoundedAllocations(string m)
    {
        const long length = 64L * 1024 * 1024;
        var p = Parameters(m, "NONE", true);
        if (m == "AES-CCM") { p.SetParameter("NONCE", "0x(000102030405060708090A)"); p.RecordExplicitParameter("NONCE"); }
        var h = FileCipherParameters.Create(p, length); byte[] frame = EncryptedFileCodec.Encode(h);
        var key = F05ReferenceTests.Key(new byte[16]);
        FileCipherStream.Process(new SyntheticStream(16), Stream.Null, key, h, frame, true, default);
        long before = GC.GetAllocatedBytesForCurrentThread();
        FileCipherStream.Process(new SyntheticStream(length), Stream.Null, key, h, frame, true, default);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"{m}: 64 MiB, {allocated} managed bytes allocated.");
        Assert.That(allocated, Is.LessThan(2 * 1024 * 1024));
    }

    private sealed class SyntheticStream(long length) : Stream
    {
        public override long Length => length;
        public override long Position { get; set; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = (int)Math.Min(count, length - Position); buffer.AsSpan(offset, n).Clear(); Position += n; return n;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
