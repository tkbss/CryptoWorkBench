using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.CryptoAlgorithm.EMV;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;
using System.Reflection;

namespace CryptoScriptUnitTest;

public class EmvApplicationCryptogramTests
{
    private const string Data = "000102030405060708090A0B0C0D0E0F";
    private const string TdeaKey = "5B10C70AFEE94975A345C69888048FF9";
    private const string AesKey = "89F7B697A028A93345BE7A409665B9A4";

    // Fixed independent characterization values from AP6.1, not official EMVCo vectors.
    // 2026-10-13: Python cryptography 50.0.2 / OpenSSL 4.0.3, no CryptoScript calls.
    // AES CMAC used OpenSSL CMAC. Retail used DES-CBC(KL), D(KR), E(KL), M2 padding.
    // M1: ECB(key, ARQC xor (ARC||zeroes)), AES additionally appends eight zeroes.
    // M2: CMAC/Retail over ARQC||00000000||proprietary; left four bytes.
    // Boundary messages are byte sequences 00..(length-1). All expectations are constants.
    public sealed record Vector(KeyAlgorithm Algorithm, string Key, string Ac, string M1,
        string M1Zero, string M1FFFF, string[] M2, string[] Boundaries, string FullMac);

    private static readonly Vector[] All =
    [
        new(KeyAlgorithm.Tdea, TdeaKey, "613D21599AAC13F5", "7FE9710A3DAD5725",
            "A90B02E8837D48F1", "518B08617D839761",
            ["81884C5C", "4A9B02A7", "EF3EBD5C", "33EEDB59"],
            ["AEDA4B66978B4C72", "F1BA48D207B5C2DB", "D459F38B284C3036", "27F78775E1E5B513"],
            "613D21599AAC13F5"),
        new(KeyAlgorithm.Aes, AesKey, "D3A78E1517C22F70", "FC16C28C668DDC7C",
            "DD032CF5613B516D", "D424AAC5DAA672A9",
            ["B7E6B69E", "50B632DC", "20A190D6", "164124C1"],
            ["57FFD62EA0AF1B51", "5FA4F267AB2F9CE3", "D3A78E1517C22F70", "E599B225947D8612"],
            "D3A78E1517C22F7040A3556A23B8A2A7"),
        new(KeyAlgorithm.Aes, "78429FD2061D24B1F8830B2C91D3ED95ED9D4B069A7C2707",
            "497A0F0552EDFE73", "6CCDF434997B63E2", "4A493CCB6F4A96B7", "8E3069C4BCA33A94",
            ["0DC0A0B0", "5AD9357A", "E48DC100", "4C1DD78E"],
            ["B74D5EB2F7687646", "F44A9DE72113653C", "497A0F0552EDFE73", "6553A5EA86578B8D"],
            "497A0F0552EDFE73CE9F76BDDCA382F8"),
        new(KeyAlgorithm.Aes, "A99C5840A0CBFBF093DEA740FFFFF5A241BFE9472968CC6E0B9EC76BA280CE0F",
            "3AFC4EF4D46F8F27", "8DF1FFC8B7D2EB25", "11482FD5A201EBB0", "D964C738A75C3737",
            ["0E95C016", "78E32620", "660705F9", "0E2CE01B"],
            ["049E69A272E2FAE7", "3879A74B4481BE6E", "3AFC4EF4D46F8F27", "1CEDB47FEB58A7B4"],
            "3AFC4EF4D46F8F2756E6FC242D292846")
    ];

    public static IEnumerable<TestCaseData> Vectors => All.Select(v => new TestCaseData(v)
        .SetArgDisplayNames($"{v.Algorithm}-{v.Key.Length * 4}"));
    public static IEnumerable<TestCaseData> AesVectors => All.Where(v => v.Algorithm == KeyAlgorithm.Aes)
        .Select(v => new TestCaseData(v).SetArgDisplayNames($"AES-{v.Key.Length * 4}"));

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCaseSource(nameof(Vectors))]
    public void AcMatchesFixedVector(Vector v) =>
        AssertHex(EmvApplicationCryptogram.ComputeAc(v.Algorithm, Hex(v.Key), Hex(Data)), v.Ac, 8);

    [TestCaseSource(nameof(Vectors))]
    public void EmptyAndBlockBoundariesMatchFixedVectors(Vector v)
    {
        int block = v.Algorithm == KeyAlgorithm.Tdea ? 8 : 16;
        int[] lengths = [0, block - 1, block, block + 1];
        for (int i = 0; i < lengths.Length; i++)
            AssertHex(EmvApplicationCryptogram.ComputeAc(v.Algorithm, Hex(v.Key),
                Enumerable.Range(0, lengths[i]).Select(x => (byte)x).ToArray()), v.Boundaries[i], 8);
    }

    [TestCaseSource(nameof(Vectors))]
    public void VerifyAcceptsVectorAndRejectsEverySingleByteDifference(Vector v)
    {
        byte[] expected = Hex(v.Ac);
        EmvApplicationCryptogram.VerifyAc(v.Algorithm, Hex(v.Key), Hex(Data), expected).Should().BeTrue();
        for (int i = 0; i < 8; i++)
        {
            byte[] key = Hex(v.Key);
            byte[] transactionData = Hex(Data);
            byte[] changed = (byte[])expected.Clone();
            changed[i] ^= 0x80;
            byte[] keyBefore = (byte[])key.Clone();
            byte[] dataBefore = (byte[])transactionData.Clone();
            byte[] changedBefore = (byte[])changed.Clone();

            EmvApplicationCryptogram.VerifyAc(v.Algorithm, key, transactionData, changed).Should().BeFalse();
            key.Should().Equal(keyBefore);
            transactionData.Should().Equal(dataBefore);
            changed.Should().Equal(changedBefore);
            changed[i].Should().Be((byte)(expected[i] ^ 0x80));
        }
        expected.Should().Equal(Hex(v.Ac));
    }

    [TestCaseSource(nameof(Vectors))]
    public void Method1MatchesAllBinaryArcVectors(Vector v)
    {
        AssertHex(EmvApplicationCryptogram.ComputeArpcMethod1(v.Algorithm, Hex(v.Key), Hex(v.Ac), Hex("3030")), v.M1, 8);
        AssertHex(EmvApplicationCryptogram.ComputeArpcMethod1(v.Algorithm, Hex(v.Key), Hex(v.Ac), Hex("0000")), v.M1Zero, 8);
        AssertHex(EmvApplicationCryptogram.ComputeArpcMethod1(v.Algorithm, Hex(v.Key), Hex(v.Ac), Hex("FFFF")), v.M1FFFF, 8);
    }

    [TestCaseSource(nameof(Vectors))]
    public void Method2AndIadMatchAllFixedVectors(Vector v)
    {
        string[] proprietary = ["", "01", "01020304", "0102030405060708"];
        for (int i = 0; i < proprietary.Length; i++)
        {
            byte[] result = EmvApplicationCryptogram.ComputeArpcMethod2(v.Algorithm, Hex(v.Key),
                Hex(v.Ac), Hex("00000000"), Hex(proprietary[i]));
            AssertHex(result, v.M2[i], 4);
            AssertHex(EmvApplicationCryptogram.BuildIssuerAuthenticationData(result, Hex("00000000"),
                Hex(proprietary[i])), v.M2[i] + "00000000" + proprietary[i], 8 + proprietary[i].Length / 2);
        }
        AssertHex(EmvApplicationCryptogram.ComputeArpcMethod2(v.Algorithm, Hex(v.Key), Hex(v.Ac),
            Hex("00000000"), null), v.M2[0], 4);
    }

    [TestCaseSource(nameof(Vectors))]
    public void GenericAdapterAndLanguagePathMatchFixedFullMac(Vector v)
    {
        byte[] key = Hex(v.Key), data = Hex(Data);
        byte[] result = v.Algorithm == KeyAlgorithm.Tdea
            ? DES3_RETAIL.ComputeMac(key, data, "ISO-9797-M2", 8)
            : AES_CMAC.ComputeMac(key, data);
        AssertHex(result, v.FullMac, v.Algorithm == KeyAlgorithm.Tdea ? 8 : 16);
        string mechanism = v.Algorithm == KeyAlgorithm.Tdea ? "DES3-RETAIL" : "AES-CMAC";
        var program = Execute($"KEY k=GenerateKey({mechanism},0x({v.Key})) " +
            $"PARAM p=Parameters({mechanism}) VAR m=Mac(p,k,0x({Data}))");
        ((StringVariableDeclaration)program.Statements[2]).Value.Should().BeEquivalentTo("0x(" + v.FullMac + ")");
        key.Should().Equal(Hex(v.Key));
        data.Should().Equal(Hex(Data));
    }

    [TestCaseSource(nameof(AesVectors))]
    public void AesGenericMacLengthAndPaddingRemainIgnored(Vector v)
    {
        var program = Execute($"KEY k=GenerateKey(AES-CMAC,0x({v.Key})) " +
            $"PARAM p=Parameters(AES-CMAC,#PAD:PKCS-7,#MACLEN:4) VAR m=Mac(p,k,0x({Data}))");
        ((StringVariableDeclaration)program.Statements[2]).Value.Should().BeEquivalentTo("0x(" + v.FullMac + ")");
    }

    // Additional OpenSSL calculations with asymmetrical ARC/CSU bytes (same fixture keys).
    [TestCase(0, "755A6F063A8C6281", "A7006D39")]
    [TestCase(1, "E31780AAB17D0341", "F7491A20")]
    [TestCase(2, "7AD3CB3CA396CB3F", "99507C0F")]
    [TestCase(3, "53CA2C8905A1F62A", "781BA35B")]
    public void NonzeroAsymmetricalArcAndCsuMatchFixedByteOrder(int index, string m1, string m2)
    {
        Vector v = All[index];
        AssertHex(EmvApplicationCryptogram.ComputeArpcMethod1(v.Algorithm, Hex(v.Key), Hex(v.Ac), Hex("1234")), m1, 8);
        AssertHex(EmvApplicationCryptogram.ComputeArpcMethod2(v.Algorithm, Hex(v.Key), Hex(v.Ac),
            Hex("12345678"), Hex("01020304")), m2, 4);
    }

    [TestCase(1, "57FFD62EA0AF1B51B7C21A2BF4DBD7A9,5FA4F267AB2F9CE3DBB3D3ED85300065,D3A78E1517C22F7040A3556A23B8A2A7,E599B225947D86120ABA38F4457975A8")]
    [TestCase(2, "B74D5EB2F768764652255FF0BA8CE068,F44A9DE72113653C24483C1EDBC1A9C4,497A0F0552EDFE73CE9F76BDDCA382F8,6553A5EA86578B8D0F7C840EB2AB4613")]
    [TestCase(3, "049E69A272E2FAE7D147264282596198,3879A74B4481BE6E73D905B4C0B6E25A,3AFC4EF4D46F8F2756E6FC242D292846,1CEDB47FEB58A7B4DCD8DE19E55F6427")]
    public void FullAesAdapterAndLanguageBoundaryValuesMatchOpenSsl(int index, string fixedValues)
    {
        Vector v = All[index];
        int[] lengths = [0, 15, 16, 17];
        string[] expected = fixedValues.Split(',');
        for (int i = 0; i < lengths.Length; i++)
        {
            byte[] data = Enumerable.Range(0, lengths[i]).Select(x => (byte)x).ToArray();
            AssertHex(AES_CMAC.ComputeMac(Hex(v.Key), data), expected[i], 16);
            string expression = data.Length == 0 ? "\"\"" : $"0x({Convert.ToHexString(data)})";
            var program = Execute($"KEY k=GenerateKey(AES-CMAC,0x({v.Key})) PARAM p=Parameters(AES-CMAC) VAR m=Mac(p,k,{expression})");
            ((StringVariableDeclaration)program.Statements[2]).Value.Should().BeEquivalentTo("0x(" + expected[i] + ")");
        }
    }

    [TestCaseSource(nameof(Vectors))]
    public void AllOperationsLeaveNonzeroCallerInputsUnchanged(Vector v)
    {
        byte[][] inputs = [Hex(v.Key), Hex(Data), Hex(v.Ac), Hex("3030"), Hex("12345678"), Hex("0102030405060708")];
        byte[][] before = inputs.Select(b => (byte[])b.Clone()).ToArray();
        _ = EmvApplicationCryptogram.ComputeAc(v.Algorithm, inputs[0], inputs[1]);
        _ = EmvApplicationCryptogram.VerifyAc(v.Algorithm, inputs[0], inputs[1], inputs[2]);
        _ = EmvApplicationCryptogram.ComputeArpcMethod1(v.Algorithm, inputs[0], inputs[2], inputs[3]);
        byte[] arpc = EmvApplicationCryptogram.ComputeArpcMethod2(v.Algorithm, inputs[0], inputs[2], inputs[4], inputs[5]);
        byte[] arpcBefore = (byte[])arpc.Clone();
        _ = EmvApplicationCryptogram.BuildIssuerAuthenticationData(arpc, inputs[4], inputs[5]);
        arpc.Should().Equal(arpcBefore);
        for (int i = 0; i < inputs.Length; i++) inputs[i].Should().Equal(before[i]);
    }

    [TestCase(0)] [TestCase(7)] [TestCase(9)]
    public void RejectsInvalidArqcAndExpectedLengths(int length)
    {
        Action verify = () => EmvApplicationCryptogram.VerifyAc(KeyAlgorithm.Aes, Hex(AesKey), Hex(Data), new byte[length]);
        verify.Should().Throw<ArgumentException>().WithParameterName("expectedCryptogram");
        Action m1 = () => EmvApplicationCryptogram.ComputeArpcMethod1(KeyAlgorithm.Aes, Hex(AesKey), new byte[length], Hex("3030"));
        m1.Should().Throw<ArgumentException>().WithParameterName("arqc");
        Action m2 = () => EmvApplicationCryptogram.ComputeArpcMethod2(KeyAlgorithm.Aes, Hex(AesKey), new byte[length], Hex("12345678"), null);
        m2.Should().Throw<ArgumentException>().WithParameterName("arqc");
    }

    [TestCase(0)] [TestCase(1)] [TestCase(3)]
    public void RejectsInvalidArc(int length) =>
        FluentActions.Invoking(() => EmvApplicationCryptogram.ComputeArpcMethod1(KeyAlgorithm.Aes,
            Hex(AesKey), Hex(All[1].Ac), new byte[length])).Should().Throw<ArgumentException>().WithParameterName("arc");

    [TestCase(0)] [TestCase(3)] [TestCase(5)]
    public void RejectsInvalidCsu(int length)
    {
        FluentActions.Invoking(() => EmvApplicationCryptogram.ComputeArpcMethod2(KeyAlgorithm.Aes,
            Hex(AesKey), Hex(All[1].Ac), new byte[length], null)).Should().Throw<ArgumentException>().WithParameterName("csu");
        FluentActions.Invoking(() => EmvApplicationCryptogram.BuildIssuerAuthenticationData(Hex("01020304"),
            new byte[length], null)).Should().Throw<ArgumentException>().WithParameterName("csu");
    }

    [TestCase(0)] [TestCase(3)] [TestCase(5)] [TestCase(8)]
    public void RejectsInvalidIadArpc(int length) =>
        FluentActions.Invoking(() => EmvApplicationCryptogram.BuildIssuerAuthenticationData(new byte[length],
            Hex("12345678"), null)).Should().Throw<ArgumentException>().WithParameterName("arpc");

    [TestCase(9)] [TestCase(16)]
    public void RejectsOversizedProprietaryData(int length)
    {
        FluentActions.Invoking(() => EmvApplicationCryptogram.ComputeArpcMethod2(KeyAlgorithm.Tdea,
            Hex(TdeaKey), Hex(All[0].Ac), Hex("12345678"), new byte[length])).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => EmvApplicationCryptogram.BuildIssuerAuthenticationData(Hex("01020304"),
            Hex("12345678"), new byte[length])).Should().Throw<ArgumentException>();
    }

    [TestCase(0)] [TestCase(15)] [TestCase(17)] [TestCase(23)]
    [TestCase(25)] [TestCase(31)] [TestCase(33)]
    public void RejectsInvalidAesKeys(int length)
    {
        CheckInvalidKey(KeyAlgorithm.Aes, new byte[length]);
        FluentActions.Invoking(() => AES_CMAC.ComputeMac(new byte[length], Hex(Data))).Should().Throw<ArgumentException>();
    }

    [TestCase(0)] [TestCase(8)] [TestCase(15)] [TestCase(17)] [TestCase(24)] [TestCase(32)]
    public void RejectsInvalidEmvTdeaKeys(int length) => CheckInvalidKey(KeyAlgorithm.Tdea, new byte[length]);

    [TestCase(KeyAlgorithm.Unknown)] [TestCase(KeyAlgorithm.Hmac)]
    [TestCase(KeyAlgorithm.Rsa)] [TestCase(KeyAlgorithm.Ec)] [TestCase((KeyAlgorithm)int.MaxValue)]
    public void RejectsOtherAlgorithms(KeyAlgorithm algorithm) => CheckInvalidKey(algorithm, Hex(AesKey));

    [Test]
    public void WeakKeyFailuresPreserveInputsAndDoNotExposeThem()
    {
        byte[] key = Hex("01010101010101010101010101010101"), data = Hex(Data);
        CheckInvalidKey(KeyAlgorithm.Tdea, key);
        var ex = FluentActions.Invoking(() => DES3_RETAIL.ComputeMac(key, data, "ISO-9797-M2", 8))
            .Should().Throw<ArgumentException>().Which;
        ex.ToString().Should().NotContain(Convert.ToHexString(key)).And.NotContain(Data);
        key.Should().Equal(Hex("01010101010101010101010101010101"));
        data.Should().Equal(Hex(Data));
    }

    [Test]
    public void NullInputsAreRejectedAtEveryBoundary()
    {
        byte[] key = Hex(AesKey), arqc = Hex(All[1].Ac), csu = Hex("12345678"), arc = Hex("3030");
        Action[] calls =
        [
            () => EmvApplicationCryptogram.ComputeAc(KeyAlgorithm.Aes, null!, []),
            () => EmvApplicationCryptogram.ComputeAc(KeyAlgorithm.Aes, key, null!),
            () => EmvApplicationCryptogram.VerifyAc(KeyAlgorithm.Aes, null!, [], arqc),
            () => EmvApplicationCryptogram.VerifyAc(KeyAlgorithm.Aes, key, null!, arqc),
            () => EmvApplicationCryptogram.VerifyAc(KeyAlgorithm.Aes, key, [], null!),
            () => EmvApplicationCryptogram.ComputeArpcMethod1(KeyAlgorithm.Aes, null!, arqc, arc),
            () => EmvApplicationCryptogram.ComputeArpcMethod1(KeyAlgorithm.Aes, key, null!, arc),
            () => EmvApplicationCryptogram.ComputeArpcMethod1(KeyAlgorithm.Aes, key, arqc, null!),
            () => EmvApplicationCryptogram.ComputeArpcMethod2(KeyAlgorithm.Aes, null!, arqc, csu, null),
            () => EmvApplicationCryptogram.ComputeArpcMethod2(KeyAlgorithm.Aes, key, null!, csu, null),
            () => EmvApplicationCryptogram.ComputeArpcMethod2(KeyAlgorithm.Aes, key, arqc, null!, null),
            () => EmvApplicationCryptogram.BuildIssuerAuthenticationData(null!, csu, null),
            () => EmvApplicationCryptogram.BuildIssuerAuthenticationData(Hex("01020304"), null!, null),
            () => AES_CMAC.ComputeMac(null!, []), () => AES_CMAC.ComputeMac(key, null!),
            () => DES3_RETAIL.ComputeMac(null!, [], "ISO-9797-M2", 8),
            () => DES3_RETAIL.ComputeMac(Hex(TdeaKey), null!, "ISO-9797-M2", 8),
            () => DES3_RETAIL.ComputeMac(Hex(TdeaKey), [], null!, 8)
        ];
        foreach (Action call in calls) call.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void SnapshotOwnerIsIndependentAndZeroesBuffersOnExceptionUnwind()
    {
        // No concurrency timing assertion: directly inspect the private owner lifetime.
        Type ownerType = typeof(EmvApplicationCryptogram).GetNestedType("InputSnapshots", BindingFlags.NonPublic)!;
        byte[] original = Hex("123456789ABCDEF0");
        object owner = ownerType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
            .Invoke([new byte[][] { original }]);
        byte[] copy = (byte[])ownerType.GetProperty("Item", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(owner, [0])!;
        original[0] = 0xFF;
        copy.Should().Equal(Hex("123456789ABCDEF0"));
        try
        {
            using ((IDisposable)owner) throw new InvalidOperationException("test unwind");
        }
        catch (InvalidOperationException) { }
        copy.Should().OnlyContain(b => b == 0);
        original.Should().Equal(Hex("FF3456789ABCDEF0"));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
    public void IadSupportsEveryProprietaryLengthAndPreservesNonzeroOrder(int length)
    {
        byte[] data = Enumerable.Range(1, length).Select(x => (byte)x).ToArray();
        AssertHex(EmvApplicationCryptogram.BuildIssuerAuthenticationData(Hex("01020304"), Hex("AABBCCDD"), data),
            "01020304AABBCCDD" + Convert.ToHexString(data), 8 + length);
        AssertHex(EmvApplicationCryptogram.BuildIssuerAuthenticationData(Hex("01020304"), Hex("AABBCCDD"), null),
            "01020304AABBCCDD", 8);
    }

    // Independently computed OpenSSL fixed generic Retail values, including K3 for 24 bytes.
    [TestCase(16, "ISO-9797-M1", "", "08D7B4FB629D0885")]
    [TestCase(16, "ISO-9797-M1", "0011223344", "12AB8B3F39AAB727")]
    [TestCase(16, "ISO-9797-M1", "0011223344556677", "31A7364CAC91CA39")]
    [TestCase(16, "ISO-9797-M1", "001122334455667788", "FDD94ED3E2D1456A")]
    [TestCase(16, "ISO-9797-M2", "", "F1FBCF2A56D19BA7")]
    [TestCase(16, "ISO-9797-M2", "0011223344", "2E44DF94582763D9")]
    [TestCase(16, "ISO-9797-M2", "0011223344556677", "0B8F413E201CFD90")]
    [TestCase(16, "ISO-9797-M2", "001122334455667788", "D0357EE6A7098F0F")]
    [TestCase(24, "ISO-9797-M1", "", "3FD539E3ABEB8B5B")]
    [TestCase(24, "ISO-9797-M1", "0011223344", "FDDD0C5CB746EAB2")]
    [TestCase(24, "ISO-9797-M1", "0011223344556677", "41153ED6AC30654B")]
    [TestCase(24, "ISO-9797-M1", "001122334455667788", "89A919B449293977")]
    [TestCase(24, "ISO-9797-M2", "", "0191EB44920FFE18")]
    [TestCase(24, "ISO-9797-M2", "0011223344", "660287E214A7AA0C")]
    [TestCase(24, "ISO-9797-M2", "0011223344556677", "9D27466A132F3A88")]
    [TestCase(24, "ISO-9797-M2", "001122334455667788", "67064D96ACCBA79D")]
    public void RetailAdapterPreservesGenericPaddingKeysLengthsAndLanguageResults(int keyLength,
        string padding, string data, string full)
    {
        string keyHex = "0123456789ABCDEFFEDCBA9876543210" + (keyLength == 24 ? "89ABCDEF01234567" : "");
        byte[] key = Hex(keyHex), message = Hex(data);
        for (int length = 4; length <= 8; length++)
        {
            string expected = full[..(length * 2)]; // Fixed independently computed full MAC, not a core call.
            AssertHex(DES3_RETAIL.ComputeMac(key, message, padding, length), expected, length);
            string expression = data.Length == 0 ? "\"\"" : $"0x({data})";
            var program = Execute($"KEY k=GenerateKey(DES3-RETAIL,0x({keyHex})) " +
                $"PARAM p=Parameters(DES3-RETAIL,#PAD:{padding},#MACLEN:{length}) VAR m=Mac(p,k,{expression})");
            ((StringVariableDeclaration)program.Statements[2]).Value.Should().BeEquivalentTo("0x(" + expected + ")");
        }
        key.Should().Equal(Hex(keyHex));
        message.Should().Equal(Hex(data));
    }

    [TestCase("NONE")] [TestCase("ISO-7816")] [TestCase("ISO-9797-M3")] [TestCase("PKCS-7")]
    public void RetailAdapterRejectsPreviouslyUnsupportedPadding(string padding) =>
        FluentActions.Invoking(() => DES3_RETAIL.ComputeMac(Hex(TdeaKey), Hex(Data), padding, 8))
            .Should().Throw<ArgumentException>().WithMessage("DES3-RETAIL supports only ISO-9797-M1 or ISO-9797-M2 padding.");

    [TestCase(3)] [TestCase(9)]
    public void RetailAdapterRejectsInvalidMacLength(int length) =>
        FluentActions.Invoking(() => DES3_RETAIL.ComputeMac(Hex(TdeaKey), Hex(Data), "ISO-9797-M2", length))
            .Should().Throw<ArgumentException>().WithMessage("DES3-RETAIL MAC length must be between 4 and 8 bytes.");

    [TestCase(0)] [TestCase(8)] [TestCase(15)] [TestCase(17)] [TestCase(23)] [TestCase(25)] [TestCase(32)]
    public void RetailAdapterRejectsInvalidGenericKeyLength(int length) =>
        FluentActions.Invoking(() => DES3_RETAIL.ComputeMac(new byte[length], Hex(Data), "ISO-9797-M2", 8))
            .Should().Throw<ArgumentException>().WithMessage("DES3 key must contain exactly 16 or 24 bytes.");

    private static void CheckInvalidKey(KeyAlgorithm algorithm, byte[] key)
    {
        Action[] calls =
        [
            () => EmvApplicationCryptogram.ComputeAc(algorithm, key, Hex(Data)),
            () => EmvApplicationCryptogram.VerifyAc(algorithm, key, Hex(Data), Hex("0102030405060708")),
            () => EmvApplicationCryptogram.ComputeArpcMethod1(algorithm, key, Hex("0102030405060708"), Hex("3030")),
            () => EmvApplicationCryptogram.ComputeArpcMethod2(algorithm, key, Hex("0102030405060708"), Hex("12345678"), null)
        ];
        foreach (Action call in calls) call.Should().Throw<ArgumentException>();
    }

    private static byte[] Hex(string value) => Convert.FromHexString(value);
    private static void AssertHex(byte[] result, string expected, int length)
    {
        result.Should().HaveCount(length);
        Convert.ToHexString(result).Should().Be(expected);
    }

    private static CryptoScriptProgram Execute(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
