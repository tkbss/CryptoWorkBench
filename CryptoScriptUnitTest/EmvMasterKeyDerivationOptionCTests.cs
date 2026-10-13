using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.Variables;
using FluentAssertions;
using System.Numerics;
using System.Reflection;

namespace CryptoScriptUnitTest;

public class EmvMasterKeyDerivationOptionCTests
{
    private const string Aes128Imk = "2EF6E07ECBA86BCF3C3CFF7BBEBE6F38";
    private const string Aes192Imk = "7481B0525D05D393D6DCBADD333C6BC514087C6BF5FA10C4";
    private const string Aes256Imk = "14D63F23982740AC65B482BAF5913092D8132BAA4143A24D3CF437232711A507";
    private const string Pan = "99012345678901234";
    private const string Psn = "45";
    private const string Y = "00000000000009901234567890123445";
    private const string YComplement = "FFFFFFFFFFFFF66FEDCBA9876FEDCBBA";
    private const string Icc128 = "35C006583E2B417207AE5B166C0EA6EF";
    private const string Icc192 = "62566BB70C70FC5C90E42F516AA7C4C5C791E8450BFE7018";
    private const string Icc256 = "8FF3C2A25D2AA18F0DE61855EE142340ADB1A69B33939F5DF93E03AF9DE93357";

    // AP5.1 characterization values, NOT official EMVCo conformance vectors.
    // Independently rechecked on 2026-10-13 using Python cryptography 50.0.2 / OpenSSL 4.0.3,
    // without invoking CryptoScript: packed (PAN || PSN).zfill(32), AES-ECB without padding,
    // full 16-byte XOR-FF complement, and leftmost IMK-length bytes of Z1 || Z2.
    // Boundary values below use Aes128Imk and the explicit PAN/PSN in each TestCase.
    // Session characterizations use the fixed ICC keys and ATC 0001 in A1.3.1.
    [TestCase(Aes128Imk, Icc128, 16)]
    [TestCase(Aes192Imk, Icc192, 24)]
    [TestCase(Aes256Imk, Icc256, 32)]
    public void FixedCharacterizationValuesMatch(string imk, string expected, int length)
    {
        byte[] result = Derive(imk, Pan, Psn);
        Convert.ToHexString(result).Should().Be(expected);
        result.Should().HaveCount(length);
    }

    [TestCase("1", "00", "00000000000000000000000000000100", "0C0A4EE137B64B36D85E13A8EDBDF100")]
    [TestCase("123456789012345", "00", "00000000000000012345678901234500", "3EC1431AA67BFBC7D39623B1A94AB007")]
    [TestCase("1234567890123456", "00", "00000000000000123456789012345600", "D16D4CA4ACEC451576CC36F3A9EF354E")]
    [TestCase("12345678901234567", "00", "00000000000001234567890123456700", "04F3C48D63035391BB55B26F2615AA0B")]
    [TestCase("123456789012345678", "00", "00000000000012345678901234567800", "E1EECAAD96EDB144E3A9F72CC7045B28")]
    [TestCase("1234567890123456789", "00", "00000000000123456789012345678900", "F755500B70BF36ED0ED01ADB4A0005EC")]
    [TestCase("00123456789012", "01", "00000000000000000012345678901201", "0490252CF520C76E5AEA5EE593364612")]
    [TestCase(Pan, Psn, Y, Icc128)]
    public void PanBoundariesAndPackedDecimalMatchFixedValues(string pan, string psn, string block, string expected)
    {
        byte[] actualBlock = InvokeHelper("CreateDiversificationBlock", pan, psn);
        actualBlock.Should().HaveCount(16);
        Convert.ToHexString(actualBlock).Should().Be(block);
        Convert.ToHexString(Derive(Aes128Imk, pan, psn)).Should().Be(expected);
    }

    [Test]
    public void ComplementInvertsEveryBitIncludingTheLeftPadding()
    {
        byte[] first = InvokeHelper("CreateDiversificationBlock", Pan, Psn);
        byte[] original = (byte[])first.Clone();
        byte[] complement = InvokeHelper("CreateComplement", first);
        Convert.ToHexString(first).Should().Be(Y);
        Convert.ToHexString(complement).Should().Be(YComplement);
        complement.Should().HaveCount(16).And.NotBeSameAs(first);
        for (int index = 0; index < first.Length; index++)
            (first[index] ^ complement[index]).Should().Be(255);
        first.Should().Equal(original);
    }

    [Test]
    public void Aes192UsesFirstBlockThenOnlyTheFirstEightBytesOfSecondBlock()
    {
        byte[] result = Derive(Aes192Imk, Pan, Psn);
        result.Should().HaveCount(24);
        Convert.ToHexString(result.AsSpan(0, 16)).Should().Be("62566BB70C70FC5C90E42F516AA7C4C5");
        Convert.ToHexString(result.AsSpan(16)).Should().Be("C791E8450BFE7018");
    }

    [Test]
    public void Aes256ConcatenatesBothCompleteBlocksInOrder()
    {
        byte[] result = Derive(Aes256Imk, Pan, Psn);
        result.Should().HaveCount(32);
        Convert.ToHexString(result.AsSpan(0, 16)).Should().Be("8FF3C2A25D2AA18F0DE61855EE142340");
        Convert.ToHexString(result.AsSpan(16)).Should().Be("ADB1A69B33939F5DF93E03AF9DE93357");
    }

    [TestCase("00", "CDDADAB206289E67A9BE8C323C30AE9C")]
    [TestCase("01", "7A3CCA8FE4801CF7A4C4F93D5C14CA81")]
    [TestCase("45", Icc128)]
    [TestCase("99", "BE8EEACA6001F25ACA29CC456F491F0B")]
    public void ExplicitPsnMatchesFixedValues(string psn, string expected) =>
        Convert.ToHexString(Derive(Aes128Imk, Pan, psn)).Should().Be(expected);

    [TestCase(Aes128Imk)]
    [TestCase(Aes192Imk)]
    [TestCase(Aes256Imk)]
    public void MissingPsnEqualsExplicitZero(string imk) =>
        Derive(imk, Pan, null).Should().Equal(Derive(imk, Pan, "00"));

    [TestCase(Aes128Imk, Icc128)]
    [TestCase(Aes192Imk, Icc192)]
    [TestCase(Aes256Imk, Icc256)]
    public void NoDesParityCorrectionIsApplied(string imk, string expected)
    {
        byte[] result = Derive(imk, Pan, Psn);
        Convert.ToHexString(result).Should().Be(expected);
        result.Any(value => BitOperations.PopCount((uint)value) % 2 == 0).Should().BeTrue();
    }

    [TestCase(Aes128Imk, Icc128, "1E955EB21AFFA73F01DBED4D41BD25F4")]
    [TestCase(Aes192Imk, Icc192, "3006A6041278FCD5AB0DFC5D679777E8F34EF83E90188E85")]
    [TestCase(Aes256Imk, Icc256, "8BAB29FA43EB767878BD3EA707193E5C1628F21558791FFF78FE275AB845A1D6")]
    public void IccMasterKeyToSessionKeyMatchesFixedCharacterization(string imk, string expectedIcc, string expectedSession)
    {
        byte[] icc = Derive(imk, Pan, Psn);
        Convert.ToHexString(icc).Should().Be(expectedIcc);
        byte[] original = (byte[])icc.Clone();
        byte[] atc = Convert.FromHexString("0001");
        byte[] session = EmvAcSessionKeyDerivation.Derive(KeyAlgorithm.Aes, icc, atc);
        Convert.ToHexString(session).Should().Be(expectedSession);
        session.Should().HaveCount(icc.Length);
        icc.Should().Equal(original);
        Convert.ToHexString(atc).Should().Be("0001");
    }

    [TestCase(Aes128Imk, Icc128)]
    [TestCase(Aes192Imk, Icc192)]
    [TestCase(Aes256Imk, Icc256)]
    public void InputsRemainUnchangedAndReturnedBuffersHaveIndependentOwnership(string imk, string expected)
    {
        byte[] key = Convert.FromHexString(imk);
        byte[] original = (byte[])key.Clone();
        byte[] first = EmvMasterKeyDerivationOptionC.Derive(key, Pan, Psn);
        byte[] second = EmvMasterKeyDerivationOptionC.Derive(key, Pan, Psn);
        first.Should().Equal(second).And.NotBeSameAs(second).And.NotBeSameAs(key);
        key.Should().Equal(original);
        first[0] ^= 0xFF;
        key.AsSpan().Clear();
        Convert.ToHexString(second).Should().Be(expected);
    }

    [TestCase(0)]
    [TestCase(15)]
    [TestCase(17)]
    [TestCase(23)]
    [TestCase(25)]
    [TestCase(31)]
    [TestCase(33)]
    public void RejectsInvalidIssuerKeyLengthsWithoutMutatingInputs(int length)
    {
        byte[] key = Enumerable.Repeat((byte)0xAA, length).ToArray();
        byte[] original = (byte[])key.Clone();
        Action action = () => EmvMasterKeyDerivationOptionC.Derive(key, Pan, Psn);
        ArgumentException exception = action.Should().Throw<ArgumentException>().WithParameterName("key").Which;
        exception.Message.Should().Contain("AES key must contain 16, 24 or 32 bytes");
        AssertSanitized(exception, Convert.ToHexString(key), Pan, Psn);
        key.Should().Equal(original);
    }

    [Test]
    public void RejectsNullIssuerKey()
    {
        Action action = () => EmvMasterKeyDerivationOptionC.Derive(null!, Pan, Psn);
        action.Should().Throw<ArgumentNullException>().WithParameterName("issuerMasterKey");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("12345678901234567890")]
    [TestCase("123A5678")]
    [TestCase("123 5678")]
    [TestCase(" 1234")]
    [TestCase("1234 ")]
    [TestCase("123-5678")]
    [TestCase("123.5678")]
    [TestCase("１２３４")]
    [TestCase("١٢٣٤")]
    [TestCase("1234\n")]
    [TestCase("0x(1234)")]
    public void RejectsInvalidPanWithoutNormalizing(string? pan)
    {
        byte[] key = Convert.FromHexString(Aes128Imk);
        byte[] original = (byte[])key.Clone();
        Action action = () => EmvMasterKeyDerivationOptionC.Derive(key, pan!, Psn);
        ArgumentException exception = action.Should().Throw<ArgumentException>().WithParameterName("pan").Which;
        AssertSanitized(exception, Aes128Imk, pan, Psn);
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
    [TestCase("1\n")]
    public void RejectsInvalidPsnWithoutNormalizing(string psn)
    {
        byte[] key = Convert.FromHexString(Aes128Imk);
        byte[] original = (byte[])key.Clone();
        Action action = () => EmvMasterKeyDerivationOptionC.Derive(key, Pan, psn);
        ArgumentException exception = action.Should().Throw<ArgumentException>().WithParameterName("psn").Which;
        AssertSanitized(exception, Aes128Imk, Pan, psn);
        key.Should().Equal(original);
    }

    [TestCase(16)]
    [TestCase(24)]
    [TestCase(32)]
    public void DoesNotIntroduceAnAesWeakKeyPolicy(int length) =>
        EmvMasterKeyDerivationOptionC.Derive(new byte[length], "1", null).Should().HaveCount(length);

    [Test]
    public void CleanupHelperZeroizesItsBufferInPlaceAndAcceptsNull()
    {
        MethodInfo clear = typeof(EmvMasterKeyDerivationOptionC)
            .GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Static)!;
        byte[] buffer = Convert.FromHexString(Aes256Imk);
        clear.Invoke(null, new object?[] { buffer });
        buffer.Should().Equal(new byte[32]);
        clear.Invoke(null, new object?[] { null });
    }

    // Snapshot-before-validation, the single AES-128 call, and finally ownership are
    // audited structurally. Concurrent input mutation and provider exceptions are not
    // injected: no crypto abstraction or test hook is introduced solely for this purpose.
    private static byte[] Derive(string imk, string pan, string? psn) =>
        EmvMasterKeyDerivationOptionC.Derive(Convert.FromHexString(imk), pan, psn);

    private static byte[] InvokeHelper(string name, params object[] arguments) =>
        (byte[])typeof(EmvMasterKeyDerivationOptionC)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;

    private static void AssertSanitized(Exception exception, params string?[] sensitiveValues)
    {
        foreach (string? value in sensitiveValues)
            if (!string.IsNullOrEmpty(value))
                exception.ToString().Should().NotContain(value);
    }
}
