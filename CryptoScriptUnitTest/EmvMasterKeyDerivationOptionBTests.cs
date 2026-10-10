using CryptoScript.CryptoAlgorithm.KDF;
using CryptoScript.Variables;
using FluentAssertions;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

public class EmvMasterKeyDerivationOptionBTests
{
    private const string IssuerKey = "0123456789ABCDEFFEDCBA9876543210";

    // V1/V2: published PyEMV comparison values, independently corroborated in AP4.1.
    // V3 and the session keys are independent characterization values, NOT normative vectors.
    [TestCase("99012345678901234", "45", "985EC4FD3EDF6162E31AF1C7D0543416", "985FC5FD3EDF6162E21AF1C6D0543416", "43A79915202AD2C8504F42A91438B152")]
    [TestCase("12345678901234567", "01", "AD406D7F6D7570916D75E5DCAB8CF737", "AC406C7E6D7471906C74E5DCAB8DF636", "5CD81C0194FA97A27DD15DC79EDC5CBC")]
    [TestCase("123456789012345678", "00", "C43446BAC7C71C267A3768EF4F8A5DAB", "C53546BAC6C61C277A3668EF4E8B5DAA", "7024D3A822B63E3E60B3311BBF8E90B0")]
    public void FixedKnownAnswersParityAndSessionKeysMatch(string pan, string psn, string expected, string rawHex, string session)
    {
        byte[] result = Derive(pan, psn);
        Convert.ToHexString(result).Should().Be(expected);
        result.Should().HaveCount(16);
        byte[] raw = Convert.FromHexString(rawHex);
        for (int index = 0; index < result.Length; index++)
        {
            (result[index] & 0xFE).Should().Be(raw[index] & 0xFE);
            (BitOperations.PopCount((uint)result[index]) % 2).Should().Be(1);
        }
        Convert.ToHexString(EmvAcSessionKeyDerivation.Derive(KeyAlgorithm.Tdea, result, new byte[] { 0, 1 }))
            .Should().Be(session);
    }

    [TestCase("99012345678901234", "45", "09901234567890123445", "DAC7DDA66BD8DD3C67928930798739903B45E5BD", "7668367928930798")]
    [TestCase("12345678901234567", "01", "01234567890123456701", "42018CCED92658095AF334207976CA56F2278DD6", "4201892658095334")]
    [TestCase("123456789012345678", "00", "12345678901234567800", "49F3DAA9A2D4D5181BA4CF98289DEC300425571A", "4939245181498289")]
    public void ActualPackedInputHashAndDecimalizationMatchFixedValues(string pan, string psn, string packed, string sha1, string f1)
    {
        byte[] input = InvokeHelper("CreateHashInput", pan, psn);
        Convert.ToHexString(input).Should().Be(packed);
        byte[] hash = InvokeHelper("ComputeHash", input);
        Convert.ToHexString(hash).Should().Be(sha1);
        // Separate platform SHA-1 primitive checks the binary input, not a textual encoding.
        SHA1.HashData(input).Should().Equal(hash);
        Convert.ToHexString(InvokeHelper("Decimalize", hash)).Should().Be(f1);
        byte[] block = Convert.FromHexString(f1);
        EmvTdeaMasterKeyDiversification.CreateComplement(block)
            .Should().Equal(block.Select(value => (byte)(value ^ 0xFF)));
    }

    [TestCase("1230ABCD567842D4B179F2CA345D6789A17B64BB", "1230567842417923")]
    [TestCase("1B3CABCDD6E8FAD4B1CDF2CAD4FDC78FA17B6EBB", "1368412478176120")]
    [TestCase("ABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCD", "0123450123450123")]
    public void DecimalizationUsesTwoOrderedPasses(string hashHex, string expected)
    {
        byte[] hash = Convert.FromHexString(hashHex);
        Convert.ToHexString(InvokeHelper("Decimalize", hash)).Should().Be(expected);
        Convert.ToHexString(hash).Should().Be(hashHex);
    }

    [TestCase("1")]
    [TestCase("123456789012345")]
    [TestCase("1234567890123456")]
    [TestCase("12345678901234567")]
    [TestCase("123456789012345678")]
    [TestCase("1234567890123456789")]
    public void PanBoundariesAreDeterministicAndSelectByPanLength(string pan)
    {
        byte[] result = Derive(pan, "00");
        result.Should().HaveCount(16).And.Equal(Derive(pan, "00"));
        result.All(value => BitOperations.PopCount((uint)value) % 2 == 1).Should().BeTrue();
        byte[] optionA = EmvMasterKeyDerivationOptionA.Derive(Key(), pan, "00");
        if (pan.Length <= 16)
            result.Should().Equal(optionA);
        else
            result.Should().NotEqual(optionA);
    }

    [TestCase("1", "00", "F77F048F6D1F3E9D9BE53D6BC7328086")]
    [TestCase("00123456789012", "01", "7C2CF1495458DC3B62913BA87AF7F44A")]
    [TestCase("12345678901234", "99", "574CD5CB9EFD51A19BD63E76F4927F83")]
    [TestCase("1234567890123456", "00", "154F349D8585CB7F6B0798E9839B10C1")]
    public void ShortPanMatchesExistingFixedOptionAAnswers(string pan, string psn, string expected) =>
        Convert.ToHexString(Derive(pan, psn)).Should().Be(expected);

    [Test]
    public void LeadingZeroChangesBranchAndRemainsInPackedInput()
    {
        const string pan = "01234567890123456";
        Convert.ToHexString(InvokeHelper("CreateHashInput", pan, "00")).Should().Be("00123456789012345600");
        Derive(pan, "00").Should().NotEqual(Derive(pan[1..], "00"));
        Derive(pan, "00").Should().NotEqual(EmvMasterKeyDerivationOptionA.Derive(Key(), pan, "00"));
    }

    [TestCase("1")]
    [TestCase("1234567890123456")]
    [TestCase("12345678901234567")]
    [TestCase("1234567890123456789")]
    public void NullPsnDefaultsToZero(string pan) => Derive(pan, null).Should().Equal(Derive(pan, "00"));

    [TestCase(0)]
    [TestCase(8)]
    [TestCase(15)]
    [TestCase(17)]
    [TestCase(24)]
    [TestCase(32)]
    public void RejectsInvalidKeyLengths(int length)
    {
        byte[] key = Enumerable.Range(0, length).Select(index => (byte)(0xA5 ^ index)).ToArray();
        byte[] original = (byte[])key.Clone();
        Action action = () => EmvMasterKeyDerivationOptionB.Derive(key, "12345678901234567", "01");
        action.Should().Throw<ArgumentException>().WithParameterName("issuerMasterKey");
        key.Should().Equal(original);
    }

    [Test]
    public void RejectsNullKey()
    {
        Action action = () => EmvMasterKeyDerivationOptionB.Derive(null!, "1", null);
        action.Should().Throw<ArgumentNullException>().WithParameterName("issuerMasterKey");
    }

    [TestCase("01010101010101010101010101010101")]
    [TestCase("0123456789ABCDEF0123456789ABCDEF")]
    [TestCase("0123456789ABCDEF0022446688AACCEE")]
    public void ReusesExistingWeakKeyPolicy(string hex)
    {
        byte[] key = Convert.FromHexString(hex);
        Action action = () => EmvMasterKeyDerivationOptionB.Derive(key, "99012345678901234", "45");
        ArgumentException exception = action.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().NotContain(hex).And.NotContain("99012345678901234").And.NotContain("45");
        Convert.ToHexString(key).Should().Be(hex);
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
    public void RejectsInvalidPanWithoutChangingKey(string? pan)
    {
        byte[] key = Key();
        Action action = () => EmvMasterKeyDerivationOptionB.Derive(key, pan!, "45");
        ArgumentException exception = action.Should().Throw<ArgumentException>().WithParameterName("pan").Which;
        AssertSanitized(exception, pan, "45");
        Convert.ToHexString(key).Should().Be(IssuerKey);
    }

    [TestCase("")]
    [TestCase("1")]
    [TestCase("001")]
    [TestCase("A1")]
    [TestCase(" 1")]
    [TestCase("1 ")]
    [TestCase("１２")]
    [TestCase("١٢")]
    public void RejectsInvalidPsnWithoutChangingKey(string psn)
    {
        byte[] key = Key();
        Action action = () => EmvMasterKeyDerivationOptionB.Derive(key, "99012345678901234", psn);
        ArgumentException exception = action.Should().Throw<ArgumentException>().WithParameterName("psn").Which;
        AssertSanitized(exception, "99012345678901234", psn);
        Convert.ToHexString(key).Should().Be(IssuerKey);
    }

    [Test]
    public void InputsAndReturnedBuffersAreIndependentAndParityEquivalentKeysRemainAccepted()
    {
        byte[] key = Key();
        for (int index = 0; index < key.Length; index++)
            key[index] ^= 1;
        byte[] original = (byte[])key.Clone();
        byte[] result = EmvMasterKeyDerivationOptionB.Derive(key, "99012345678901234", "45");
        byte[] second = EmvMasterKeyDerivationOptionB.Derive(key, "99012345678901234", "45");
        Convert.ToHexString(result).Should().Be("985EC4FD3EDF6162E31AF1C7D0543416");
        result.Should().NotBeSameAs(key).And.NotBeSameAs(second);
        result[0] ^= 255;
        Convert.ToHexString(second).Should().Be("985EC4FD3EDF6162E31AF1C7D0543416");
        key.Should().Equal(original);
    }

    [Test]
    public void SharedCoreBorrowsInputsWithoutChangingOrClearingThem()
    {
        byte[] key = Key();
        byte[] block = Convert.FromHexString("7668367928930798");
        Convert.ToHexString(EmvTdeaMasterKeyDiversification.Derive(key, block))
            .Should().Be("985EC4FD3EDF6162E31AF1C7D0543416");
        Convert.ToHexString(key).Should().Be(IssuerKey);
        Convert.ToHexString(block).Should().Be("7668367928930798");
    }

    [Test]
    public void SharedCorePropagatesPrimitiveFailureAndPreservesBorrowedBuffers()
    {
        // Deliberately violate the internal validated-key precondition to exercise provider failure.
        byte[] key = Convert.FromHexString("0123456789ABCDEF0123456789ABCDEF");
        byte[] original = (byte[])key.Clone();
        byte[] block = Convert.FromHexString("7668367928930798");
        Action action = () => EmvTdeaMasterKeyDiversification.Derive(key, block);
        action.Should().Throw<CryptographicException>();
        key.Should().Equal(original);
        Convert.ToHexString(block).Should().Be("7668367928930798");
        // Private finally-owned buffers cannot be observed without introducing a test-only hook.
        // Their zeroization, including a partially allocated result, is audited structurally.
    }

    [TestCase(0)]
    [TestCase(7)]
    [TestCase(9)]
    public void SharedCoreRejectsIncorrectBlockLength(int length)
    {
        Action action = () => EmvTdeaMasterKeyDiversification.Derive(Key(), new byte[length]);
        action.Should().Throw<ArgumentException>().WithParameterName("diversificationBlock");
    }

    [TestCase(0)]
    [TestCase(24)]
    public void SharedCoreRejectsIncorrectKeyLength(int length)
    {
        Action action = () => EmvTdeaMasterKeyDiversification.Derive(new byte[length], new byte[8]);
        action.Should().Throw<ArgumentException>().WithParameterName("validatedImkSnapshot");
    }

    private static byte[] Key() => Convert.FromHexString(IssuerKey);
    private static byte[] Derive(string pan, string? psn) => EmvMasterKeyDerivationOptionB.Derive(Key(), pan, psn);
    private static byte[] InvokeHelper(string name, params object[] arguments) =>
        (byte[])typeof(EmvMasterKeyDerivationOptionB)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;

    private static void AssertSanitized(Exception exception, string? pan, string? psn)
    {
        exception.Message.Should().NotContain(IssuerKey);
        if (!string.IsNullOrEmpty(pan))
            exception.Message.Should().NotContain(pan);
        if (!string.IsNullOrEmpty(psn))
            exception.Message.Should().NotContain(psn);
    }
}
