using CryptoScript.CryptoAlgorithm.KDF;

namespace CryptoScriptUnitTest;

public class HkdfModeTests
{
    public sealed record Rfc5869Vector(
        string Name,
        string Hash,
        string Ikm,
        string? Salt,
        string Info,
        int OutputLength,
        string Prk,
        string Okm);

    private static readonly Rfc5869Vector[] Vectors =
    {
        new("RFC 5869 A.1", "HASH-SHA256",
            Repeat("0B", 22),
            "000102030405060708090A0B0C",
            "F0F1F2F3F4F5F6F7F8F9", 42,
            "077709362C2E32DF0DDC3F0DC47BBA6390B6C73BB50F9C3122EC844AD7C2B3E5",
            "3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865"),
        new("RFC 5869 A.2", "HASH-SHA256",
            Sequence(0x00, 0x4F),
            Sequence(0x60, 0xAF),
            Sequence(0xB0, 0xFF), 82,
            "06A6B88C5853361A06104C9CEB35B45CEF760014904671014A193F40C15FC244",
            "B11E398DC80327A1C8E7F78C596A49344F012EDA2D4EFAD8A050CC4C19AFA97C59045A99CAC7827271CB41C65E590E09DA3275600C2F09B8367793A9ACA3DB71CC30C58179EC3E87C14C01D5C1F3434F1D87"),
        new("RFC 5869 A.3", "HASH-SHA256",
            Repeat("0B", 22),
            null,
            string.Empty, 42,
            "19EF24A32C717B167F33A91D6F648BDF96596776AFDB6377AC434C1C293CCB04",
            "8DA4E775A563C18F715F802A063C5A31B8A11F5C5EE1879EC3454E5F3C738D2D9D201395FAA4B61A96C8"),
        new("RFC 5869 A.4", "HASH-SHA1",
            Repeat("0B", 11),
            "000102030405060708090A0B0C",
            "F0F1F2F3F4F5F6F7F8F9", 42,
            "9B6C18C432A7BF8F0E71C8EB88F4B30BAA2BA243",
            "085A01EA1B10F36933068B56EFA5AD81A4F14B822F5B091568A9CDD4F155FDA2C22E422478D305F3F896")
    };

    [TestCaseSource(nameof(Vectors))]
    public void ExtractMatchesRfc5869Prk(Rfc5869Vector vector)
    {
        byte[]? salt = vector.Salt == null ? null : Convert.FromHexString(vector.Salt);

        byte[] prk = HKDFMode.Extract(vector.Hash, salt, Convert.FromHexString(vector.Ikm));

        Assert.That(Convert.ToHexString(prk), Is.EqualTo(vector.Prk), vector.Name);
    }

    [TestCaseSource(nameof(Vectors))]
    public void ExpandMatchesRfc5869Okm(Rfc5869Vector vector)
    {
        byte[] okm = HKDFMode.Expand(vector.Hash, Convert.FromHexString(vector.Prk),
            Convert.FromHexString(vector.Info), vector.OutputLength);

        Assert.That(Convert.ToHexString(okm), Is.EqualTo(vector.Okm), vector.Name);
    }

    [Test]
    public void ExpandDefensivelyRejectsMoreThan255Blocks()
    {
        Assert.Throws<ArgumentException>(() => HKDFMode.Expand(
            "HASH-SHA256", new byte[32], Array.Empty<byte>(), 255 * 32 + 1));
    }

    [Test]
    public void ExpandDefensivelyRejectsPrkShorterThanHashLen()
    {
        var exception = Assert.Throws<ArgumentException>(() => HKDFMode.Expand(
            "HASH-SHA256", new byte[31], Array.Empty<byte>(), 32));

        Assert.That(exception!.Message, Does.Contain("PRK must be at least HashLen bytes"));
    }

    [Test]
    public void ExpandAcceptsPrkEqualToHashLen()
    {
        byte[] output = HKDFMode.Expand(
            "HASH-SHA256", new byte[32], Array.Empty<byte>(), 32);

        Assert.That(output, Has.Length.EqualTo(32));
    }

    [TestCase(15)]
    [TestCase(17)]
    [TestCase(32)]
    public void ExpandEp2Sha256RejectsKeysThatAreNotExactly16Bytes(int keyLength)
    {
        var exception = Assert.Throws<ArgumentException>(() => HKDFMode.ExpandEp2Sha256(
            new byte[keyLength], Array.Empty<byte>(), 32));

        Assert.That(exception!.Message, Does.Contain("exactly 16 bytes"));
    }

    [Test]
    public void ExpandEp2Sha256MatchesEp2Section814ReferenceVector()
    {
        const string key = "0123456789ABCDEF23456789ABCDEF01";
        const string info = "5413330089020011";
        const string expected = "AEA780CDFC3CDA67C0FD0D70D509C9B4C1DD1F40F0C05D922BFD3BC8A01E2E6E";

        byte[] output = HKDFMode.ExpandEp2Sha256(
            Convert.FromHexString(key), Convert.FromHexString(info), 32);

        Assert.That(Convert.ToHexString(output), Is.EqualTo(expected));
    }

    [TestCase(0, "positive")]
    [TestCase(255 * 32 + 1, "255 times HashLen")]
    public void ExpandEp2Sha256RejectsInvalidOutputLength(int outputLength, string expectedMessage)
    {
        var exception = Assert.Throws<ArgumentException>(() => HKDFMode.ExpandEp2Sha256(
            new byte[16], Array.Empty<byte>(), outputLength));

        Assert.That(exception!.Message, Does.Contain(expectedMessage));
    }

    private static string Repeat(string value, int count) => string.Concat(Enumerable.Repeat(value, count));

    private static string Sequence(int first, int last) =>
        string.Concat(Enumerable.Range(first, last - first + 1).Select(value => value.ToString("X2")));
}
