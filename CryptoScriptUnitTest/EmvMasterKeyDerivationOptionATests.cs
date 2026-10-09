using CryptoScript.CryptoAlgorithm.KDF;
using FluentAssertions;
using Org.BouncyCastle.Crypto.Parameters;
using System.Numerics;
using System.Reflection;

namespace CryptoScriptUnitTest;

public class EmvMasterKeyDerivationOptionATests
{
    private const string IssuerKey = "0123456789ABCDEFFEDCBA9876543210";

    // V1: PyEMV 1.3.0 published example; V2: PyEMV kd.py example (EMV 4.3 reference).
    // V3: independent characterization, NOT a published normative vector.
    // Blocks/raw ciphertext were independently checked with .NET and Bouncy Castle in AP3.1.
    private const string V1Raw = "67F8292358083F5EA6AB7FDA58D43A6B";
    private const string V2Raw = "72AD54698CEE2935B0969956E2C718F0";
    private const string V3Raw = "FE880EC1B2FBF6ED32A880C9D6179561";

    // L1: buffers in finally are audited structurally. Provider fault injection / observation
    // of private zeroized buffers is not covered; no crypto abstraction is added just for testing.
    // L2: independent DesEdeEngine calculation: PAN 99012345678901234, PSN 45,
    // diversification 1234567890123445; different, non-parity-equivalent IMK.
    [Test]
    public void IndependentSecondIssuerKeyKnownAnswerMatches()
    {
        byte[] key = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
        byte[] result = EmvMasterKeyDerivationOptionA.Derive(key, "99012345678901234", "45");
        Convert.ToHexString(result).Should().Be("7FABA85B199461BFA83DBAF461010E2F");
    }

    [TestCase("99012345678901234", "45", "67F8292358083E5EA7AB7FDA58D53B6B")]
    [TestCase("12345678901234567", "01", "73AD54688CEF2934B0979857E3C719F1")]
    [TestCase("123456789012", "00", "FE890EC1B3FBF7EC32A880C8D6169461")]
    public void FixedReferenceVectorsMatch(string pan, string psn, string expected)
    {
        byte[] result = Derive(pan, psn);
        Convert.ToHexString(result).Should().Be(expected);
        result.Should().HaveCount(16);
        result.All(HasOddParity).Should().BeTrue();
    }

    // Additional fixed characterization values calculated before implementation with DesEdeEngine.
    [TestCase("1", "00", "F77F048F6D1F3E9D9BE53D6BC7328086")]
    [TestCase("00123456789012", "01", "7C2CF1495458DC3B62913BA87AF7F44A")]
    [TestCase("12345678901234", "99", "574CD5CB9EFD51A19BD63E76F4927F83")]
    [TestCase("1234567890123456", "00", "154F349D8585CB7F6B0798E9839B10C1")]
    [TestCase("1234567890123456789", "99", "8654DAEF9DC24F0262CD7CEC2304CBDF")]
    public void PanBoundariesAndLeadingZerosMatchFixedCharacterizations(string pan, string psn, string expected) =>
        Convert.ToHexString(Derive(pan, psn)).Should().Be(expected);

    [TestCase("99012345678901234", "45", "1234567890123445", "EDCBA9876FEDCBBA")]
    [TestCase("12345678901234567", "01", "4567890123456701", "BA9876FEDCBA98FE")]
    [TestCase("123456789012", "00", "0012345678901200", "FFEDCBA9876FEDFF")]
    [TestCase("1", "00", "0000000000000100", "FFFFFFFFFFFFFEFF")]
    [TestCase("12345678901234", "99", "1234567890123499", "EDCBA9876FEDCB66")]
    public void ActualInputConstructionMatchesFixedBlocks(string pan, string psn, string first, string second)
    {
        // Inspect private construction helpers without widening the production API.
        byte[] f1 = InvokeBlockHelper("CreateDiversificationBlock", pan, psn);
        byte[] f2 = InvokeBlockHelper("CreateComplement", f1);
        f1.Should().HaveCount(8);
        f2.Should().HaveCount(8);
        Convert.ToHexString(f1).Should().Be(first);
        Convert.ToHexString(f2).Should().Be(second);
        for (int index = 0; index < 8; index++)
            (f1[index] ^ f2[index]).Should().Be(255);
    }

    [TestCase("99012345678901234", "45", V1Raw)]
    [TestCase("12345678901234567", "01", V2Raw)]
    [TestCase("123456789012", "00", V3Raw)]
    public void ParityChangesOnlyBitZeroAndIsIdempotent(string pan, string psn, string rawHex)
    {
        byte[] raw = Convert.FromHexString(rawHex);
        byte[] result = Derive(pan, psn);
        for (int index = 0; index < result.Length; index++)
        {
            (result[index] & 0xFE).Should().Be(raw[index] & 0xFE);
            HasOddParity(result[index]).Should().BeTrue();
        }
        byte[] corrected = (byte[])raw.Clone();
        DesParameters.SetOddParity(corrected);
        corrected.Should().Equal(result);
        DesParameters.SetOddParity(corrected);
        corrected.Should().Equal(result);
    }

    [TestCase("1")]
    [TestCase("123456789012")]
    [TestCase("1234567890123456789")]
    public void MissingPsnEqualsExplicitZero(string pan) => Derive(pan, null).Should().Equal(Derive(pan, "00"));

    [TestCase(0)]
    [TestCase(8)]
    [TestCase(15)]
    [TestCase(17)]
    [TestCase(24)]
    [TestCase(32)]
    public void RejectsInvalidIssuerKeyLengths(int length)
    {
        Action action = () => EmvMasterKeyDerivationOptionA.Derive(new byte[length], "1", "00");
        action.Should().Throw<ArgumentException>().WithParameterName("issuerMasterKey");
    }

    [Test]
    public void RejectsNullIssuerKey()
    {
        Action action = () => EmvMasterKeyDerivationOptionA.Derive(null!, "1", "00");
        action.Should().Throw<ArgumentNullException>().WithParameterName("issuerMasterKey");
    }

    [TestCase("01010101010101010101010101010101")]
    [TestCase("0123456789ABCDEF0123456789ABCDEF")]
    [TestCase("0123456789ABCDEF0022446688AACCEE")]
    public void RejectsExistingWeakOrDegenerateKeyCasesWithoutChangingInput(string hex)
    {
        byte[] key = Convert.FromHexString(hex);
        byte[] original = (byte[])key.Clone();
        Action action = () => EmvMasterKeyDerivationOptionA.Derive(key, "12345678901234567", "45");
        ArgumentException exception = action.Should().Throw<ArgumentException>().Which;
        exception.ToString().Should().NotContain(hex).And.NotContain("12345678901234567");
        key.Should().Equal(original);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("12345678901234567890")]
    [TestCase("123A5678")]
    [TestCase("123 5678")]
    [TestCase("123-5678")]
    [TestCase("123.5678")]
    [TestCase("１２３４")]
    [TestCase("١٢٣٤")]
    [TestCase("1234\n")]
    public void RejectsInvalidPan(string? pan)
    {
        byte[] key = Convert.FromHexString(IssuerKey);
        byte[] original = (byte[])key.Clone();
        Action action = () => EmvMasterKeyDerivationOptionA.Derive(key, pan!, "45");
        ArgumentException exception = action.Should().Throw<ArgumentException>().WithParameterName("pan").Which;
        AssertSanitized(exception, pan, "45");
        key.Should().Equal(original);
    }

    [TestCase("")]
    [TestCase("1")]
    [TestCase("001")]
    [TestCase("A1")]
    [TestCase(" 1")]
    [TestCase("1 ")]
    [TestCase("１２")]
    [TestCase("١٢")]
    public void RejectsInvalidPsn(string psn)
    {
        Action action = () => Derive("99012345678901234", psn);
        ArgumentException exception = action.Should().Throw<ArgumentException>().WithParameterName("psn").Which;
        AssertSanitized(exception, "99012345678901234", psn);
    }

    [Test]
    public void IssuerKeyWithNonOddParityIsNotChangedOrRejected()
    {
        byte[] key = Convert.FromHexString(IssuerKey);
        for (int index = 0; index < key.Length; index++)
            key[index] ^= 1;
        byte[] original = (byte[])key.Clone();
        byte[] result = EmvMasterKeyDerivationOptionA.Derive(key, "99012345678901234", "45");
        Convert.ToHexString(result).Should().Be("67F8292358083E5EA7AB7FDA58D53B6B");
        key.Should().Equal(original);
    }

    [Test]
    public void ReturnedBuffersAreIndependentOfInputAndOfEachOther()
    {
        byte[] key = Convert.FromHexString(IssuerKey);
        byte[] result = EmvMasterKeyDerivationOptionA.Derive(key, "99012345678901234", "45");
        byte[] second = EmvMasterKeyDerivationOptionA.Derive(key, "99012345678901234", "45");
        result.Should().NotBeSameAs(key).And.NotBeSameAs(second);
        result[0] ^= 0xFF;
        Convert.ToHexString(second).Should().Be("67F8292358083E5EA7AB7FDA58D53B6B");
        Convert.ToHexString(key).Should().Be(IssuerKey);
    }

    private static byte[] Derive(string pan, string? psn) =>
        EmvMasterKeyDerivationOptionA.Derive(Convert.FromHexString(IssuerKey), pan, psn);

    private static byte[] InvokeBlockHelper(string name, params object[] arguments) =>
        (byte[])typeof(EmvMasterKeyDerivationOptionA)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;

    private static bool HasOddParity(byte value) => BitOperations.PopCount((uint)value) % 2 == 1;

    private static void AssertSanitized(Exception exception, string? pan, string? psn)
    {
        string message = exception.Message;
        message.Should().NotContain(IssuerKey);
        if (!string.IsNullOrEmpty(pan))
            message.Should().NotContain(pan);
        if (!string.IsNullOrEmpty(psn))
            message.Should().NotContain(psn);
    }
}
