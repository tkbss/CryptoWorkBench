using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class SymmetricAlgoHmacTests
{
    private const string RfcCase1Key = "0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B";

    public sealed record HmacVector(
        string Mechanism,
        string Key,
        string Data,
        string Expected,
        int ExpectedBytes,
        string Source);

    private static readonly HmacVector[] KnownAnswerVectors =
    {
        // RFC 2202, test case 1.
        new("HMAC-SHA1",
            RfcCase1Key,
            "\"Hi There\"",
            "B617318655057264E28BC0B6FB378C8EF146BE00", 20, "RFC 2202 test case 1"),

        // RFC 4231, test case 1.
        new("HMAC-SHA224",
            RfcCase1Key,
            "\"Hi There\"",
            "896FB1128ABBDF196832107CD49DF33F47B4B1169912BA4F53684B22", 28, "RFC 4231 test case 1"),
        new("HMAC-SHA256",
            RfcCase1Key,
            "\"Hi There\"",
            "B0344C61D8DB38535CA8AFCEAF0BF12B881DC200C9833DA726E9376C2E32CFF7", 32, "RFC 4231 test case 1"),
        new("HMAC-SHA384",
            RfcCase1Key,
            "\"Hi There\"",
            "AFD03944D84895626B0825F4AB46907F15F9DADBE4101EC682AA034C7CEBC59CFAEA9EA9076EDE7F4AF152E8B2FA9CB6", 48, "RFC 4231 test case 1"),
        new("HMAC-SHA512",
            RfcCase1Key,
            "\"Hi There\"",
            "87AA7CDEA5EF619D4FF0B4241A1D6CB02379F4E2CE4EC2787AD0B30545E17CDEDAA833B7D6B8A702038B274EAEA3F4E4BE9D914EEB61F1702E696C203A126854", 64, "RFC 4231 test case 1"),

        // NIST SP 800-224 ipd, Appendix B, Table 4. The publication provides the
        // indicated tag prefix; the complete tags below were independently
        // cross-checked with OpenSSL 3.6.1 using the published key and message.
        new("HMAC-SHA512-224",
            "6036DB046AAC5778CEF2E795A9787347310907D711D0A2BF1D15B1BFA5EB",
            "0x(4407F708FB4EB39882E7FA552474C595)",
            "F5AA41547F04B336AD6862F64D1F508F660439536B290C03CC527B00", 28, "NIST SP 800-224 ipd Table 4, tcid 301"),
        new("HMAC-SHA512-256",
            "D3F8BBE410DC40EA2BA2176BD99E0905C8F8EDE67FA40A33897F1CE38CBA34C3AD4D5207",
            "0x(7AFE75E5D204235A462BB282C648278C)",
            "23C7CFBE4921B9A4D862B01B6F86273E24DD52304E5C81C3CD1FF6458ED63087", 32, "NIST SP 800-224 ipd Table 4, tcid 76"),
        new("HMAC-SHA3-224",
            "F8A7ED5562A7646A22B4DBB14D3AD891CA677877DAE378602F09CE479D3B11E81A",
            "0x(7627B19CB55594587EDAD2FF0C22D292)",
            "1AF28609D217BF6DFB1184A15FDE9423DA4C1BC1F047FD9D9DCB8A65", 28, "NIST SP 800-224 ipd Table 4, tcid 1"),
        new("HMAC-SHA3-256",
            "5F712D90E610531AA24E2C5CB59B2B7F0E1D229809B10F46201E48D493EB6784EC",
            "0x(6D95CE1DECC2212AF7B33A90D6297E02)",
            "ED29D0D3923524AE417F0B30DFF8A4128DC202AEA7F6AAC724A5661417AFD1B5", 32, "NIST SP 800-224 ipd Table 4, tcid 526"),
        new("HMAC-SHA3-384",
            "63E7020D5E017AA8F86618BA4A4ED4BE03298E92BA8EF97C7396D26061B12D5D638C3E53FF1B8052B5E217A927EB7D9B80CEDAC1CEB227A13A0229DF542F8B0F1040A5C8E9558CDDEB",
            "0x(C4222888AFAB77E7C9206D2894714E9A)",
            "0B546DF3EF91E1DA09E5E7EFC7258CA2DA57CBE6AF00B571CF234C8A352364CB88509F0865ECF50CABA90442182991BE", 48, "NIST SP 800-224 ipd Table 4, tcid 601"),
        new("HMAC-SHA3-512",
            "A471B46143C47722A4317F79C3605F5606210066F7607F37BFC05AB48AD624ECDDAA5F2BCE0F5D68CB900A94041A388C",
            "0x(676498A915CC5B773275034A972B552A)",
            "CF38AA4B510886A34FB3B67F50F8FED59DE58564C19866D3B6D961E98FF11BB252A597C87510C2ADD394E98D7871BE663E6C3310D7B44408C5D63DFB46DF3654", 64, "NIST SP 800-224 ipd Table 4, tcid 76")
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TestCaseSource(nameof(KnownAnswerVectors))]
    public void Mac_MatchesPublishedKnownAnswerVector(HmacVector vector)
    {
        CryptoScriptProgram result = Execute(
            $"PARAM p=Parameters({vector.Mechanism}) " +
            $"KEY k=GenerateKey({vector.Mechanism},0x({vector.Key})) " +
            $"VAR mac=Mac(p,k,{vector.Data})");

        var mac = result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject;
        mac.Value.Should().BeEquivalentTo($"0x({vector.Expected})", options => options.IgnoringCase(), vector.Source);
        mac.Type.Should().BeOfType<CryptoTypeVar>();
        mac.ValueFormat.Should().Be(FormatConversions.HEX);
        FormatConversions.HexStringToByteArray(mac.Value).Should().HaveCount(vector.ExpectedBytes);
        VariableDictionary.Instance().Get("mac").Should().BeSameAs(mac);
    }

    [Test]
    public void Mac_HandlesKeyLongerThanSha256BlockSize()
    {
        // RFC 4231, test case 6: a 131-byte key must be hashed before use.
        string key = string.Concat(Enumerable.Repeat("AA", 131));
        const string expected = "60E431591EE0B67F0D8A26AACBF5B77F8E0BC6213728C5140546040F0EE37F54";

        CryptoScriptProgram result = Execute(
            $"PARAM p=Parameters(HMAC-SHA256) " +
            $"KEY k=GenerateKey(HMAC-SHA256,0x({key})) " +
            "VAR mac=Mac(p,k,\"Test Using Larger Than Block-Size Key - Hash Key First\")");

        ((StringVariableDeclaration)result.Statements[2]).Value
            .Should().BeEquivalentTo($"0x({expected})", options => options.IgnoringCase());
    }

    [Test]
    public void Mac_AcceptsBase64Data()
    {
        CryptoScriptProgram result = Execute(
            "PARAM p=Parameters(HMAC-SHA256) " +
            $"KEY k=GenerateKey(HMAC-SHA256,0x({RfcCase1Key})) " +
            "VAR mac=Mac(p,k,b64(SGkgVGhlcmU=))");

        ((StringVariableDeclaration)result.Statements[2]).Value.Should().BeEquivalentTo(
            "0x(B0344C61D8DB38535CA8AFCEAF0BF12B881DC200C9833DA726E9376C2E32CFF7)",
            options => options.IgnoringCase());
    }

    [Test]
    public void Mac_AcceptsDataFromVar()
    {
        CryptoScriptProgram result = Execute(
            "PARAM p=Parameters(HMAC-SHA1) " +
            $"KEY k=GenerateKey(HMAC-SHA1,0x({RfcCase1Key})) " +
            "VAR data=0x(4869205468657265) " +
            "VAR mac=Mac(p,k,data)");

        ((StringVariableDeclaration)result.Statements[3]).Value.Should().BeEquivalentTo(
            "0x(B617318655057264E28BC0B6FB378C8EF146BE00)",
            options => options.IgnoringCase());
    }

    [Test]
    public void Mac_WithGeneratedKey_ReturnsFullMac()
    {
        CryptoScriptProgram result = Execute(
            "PARAM p=Parameters(HMAC-SHA384) " +
            "KEY k=GenerateKey(HMAC-SHA384,136) " +
            "VAR mac=Mac(p,k,\"data\")");

        var mac = (StringVariableDeclaration)result.Statements[2];
        FormatConversions.HexStringToByteArray(mac.Value).Should().HaveCount(48);
    }

    [Test]
    public void Mac_RejectsUnsupportedDataFormat()
    {
        Action act = () => Execute(
            "PARAM p=Parameters(HMAC-SHA256) " +
            "KEY k=GenerateKey(HMAC-SHA256,256) " +
            "VAR mac=Mac(p,k,123)");

        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains("wrong data argument"));
    }

    private static CryptoScriptProgram Execute(string input)
    {
        var parser = ParserBuilder.StringBuild(input);
        var context = parser.program();
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return new CryptoScriptRunner().Execute(context);
    }
}
