using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoCbcNistTests
    {
        private static readonly object[] CbcNist128PaddingCases =
        {
            new object[] { "ISO-7816"     },
            new object[] { "PKCS-7"       },
            new object[] { "ISO-9797-M1"  },
            new object[] { "ISO-9797-M2"  },
            new object[] { "ISO-9797-M3"  },
            new object[] { "ANSI-X923"    },
            new object[] { "TLS-CBC"    },
        };

        private static readonly object[] CbcNistVectors =
        {
            new object[]
            {
                128,
                "0x(2B7E151628AED2A6ABF7158809CF4F3C)",
                "0x(000102030405060708090A0B0C0D0E0F)",
                "0x(6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E5130C81C46A35CE411E5FBC1191A0A52EFF69F2445DF4F9B17AD2B417BE66C3710)",
                "0x(7649ABAC8119B246CEE98E9B12E9197D5086CB9B507219EE95DB113A917678B273BED6B8E3C1743B7116E69E222295163FF1CAA1681FAC09120ECA307586E1A7)"
            },
            new object[]
            {
                192,
                "0x(8E73B0F7DA0E6452C810F32B809079E562F8EAD2522C6B7B)",
                "0x(000102030405060708090A0B0C0D0E0F)",
                "0x(6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E5130C81C46A35CE411E5FBC1191A0A52EFF69F2445DF4F9B17AD2B417BE66C3710)",
                "0x(4F021DB243BC633D7178183A9FA071E8B4D9ADA9AD7DEDF4E5E738763F69145A571B242012FB7AE07FA9BAAC3DF102E008B0E27988598881D920A9E64F5615CD)"
            },
            new object[]
            {
                256,
                "0x(603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4)",
                "0x(000102030405060708090A0B0C0D0E0F)",
                "0x(6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E5130C81C46A35CE411E5FBC1191A0A52EFF69F2445DF4F9B17AD2B417BE66C3710)",
                "0x(F58C4C04D6E5F1BA779EABFB5F7BFBD69CFC4E967EDB808D679F777BC6702C7D39F23369A9D9BACFA530E26304231461B2EB05E2C39BE9FCDA6C19078C6A9D1B)"
            },
        };
        
        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
        }
        [TestCaseSource(nameof(CbcNistVectors))]
        public void AES_CBC_NIST_Encrypt_Matches_Expected_Ciphertext(
            int keyBits,
            string keyHex,
            string ivHex,
            string plaintextHex,
            string expectedCipherHex)
        {
            
            var prog = new AntlrToProgram();            
            var input =
                $"KEY k=GenerateKey(AES-CBC,{keyHex}) " +
                $"PARAM p=Parameters(#MECH:AES-CBC,#IV:{ivHex},#PAD:NONE) " +
                $"VAR c=Encrypt(p,k,{plaintextHex})";

            // Act
            var parser = ParserBuilder.StringBuild(input);
            var context = parser.program();
            SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse("Syntax errors occurred during parsing.");
            var res = prog.Visit(context);

            // Assert (gleiches Muster wie in deinem AEAD-Template, nur mit FluentAssertions)
            var keyVar = res.Statements[0].Should().BeOfType<KeyVariableDeclaration>().Subject;
            VariableDictionary.Instance().Contains(keyVar.Id).Should().BeTrue();

            var pVar = res.Statements[1].Should().BeOfType<ParameterVariableDeclaration>().Subject;
            VariableDictionary.Instance().Contains(pVar.Id).Should().BeTrue();

            var cVar = res.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject;
            VariableDictionary.Instance().Contains(cVar.Id).Should().BeTrue();

            // NIST-Vergleich (Ciphertext muss exakt matchen)
            cVar.Value.Should().BeEquivalentTo(expectedCipherHex,options => options.IgnoringCase());
        }

        [TestCaseSource(nameof(CbcNistVectors))]
        public void AES_CBC_NIST_Decrypt_Roundtrip_Returns_Original_Plaintext(
            int keyBits,
            string keyHex,
            string ivHex,
            string plaintextHex,
            string expectedCipherHex)
        {
            var prog = new AntlrToProgram();
            var input =
                $"KEY k=GenerateKey(AES-CBC,{keyHex}) " +
                $"PARAM p=Parameters(#MECH:AES-CBC,#IV:{ivHex},#PAD:NONE) " +
                $"VAR c=Encrypt(p,k,{plaintextHex}) " +
                $"VAR pt=Decrypt(p,k,c)";

            // Act
            var parser = ParserBuilder.StringBuild(input);
            var context = parser.program();
            var res = prog.Visit(context);

            // Assert
            var ptVar = res.Statements[3].Should().BeOfType<StringVariableDeclaration>().Subject;
            ptVar.Value.Should().BeEquivalentTo(plaintextHex,options => options.IgnoringCase());
        }
        [TestCaseSource(nameof(CbcNist128PaddingCases))]
        public void AES_CBC_NIST128_Padding_Adds_One_Block_And_Roundtrip_Works(string pad)
        {
            // NIST 128 Vektoren (aus vorhandener Klasse)
            var keyHex = "0x(2B7E151628AED2A6ABF7158809CF4F3C)";
            var ivHex = "0x(000102030405060708090A0B0C0D0E0F)";
            var plaintextHex =
                "0x(6BC1BEE22E409F96E93D7E117393172A" +
                "AE2D8A571E03AC9C9EB76FAC45AF8E51" +
                "30C81C46A35CE411E5FBC1191A0A52EF" +
                "F69F2445DF4F9B17AD2B417BE66C3710)";

            var nistCipherHex =
                "7649ABAC8119B246CEE98E9B12E9197D" +
                "5086CB9B507219EE95DB113A917678B2" +
                "73BED6B8E3C1743B7116E69E22229516" +
                "3FF1CAA1681FAC09120ECA307586E1A7";

            // ------------------------------------------------------------
            // Durchgang 1: exakt NIST-Plaintext (4 Blöcke)
            // ------------------------------------------------------------
            VariableDictionary.Instance().Clear();
            var prog1 = new AntlrToProgram();

            var input1 =
                $"KEY k=GenerateKey(AES-CBC,{keyHex}) " +
                $"PARAM p=Parameters(#MECH:AES-CBC,#IV:{ivHex},#PAD:{pad}) " +
                $"VAR c=Encrypt(p,k,{plaintextHex}) " +
                $"VAR pt=Decrypt(p,k,c)";

            var parser1 = ParserBuilder.StringBuild(input1);
            var ctx1 = parser1.program();
            SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
            var res1 = prog1.Visit(ctx1);

            var cVar1 = res1.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject;
            var ptVar1 = res1.Statements[3].Should().BeOfType<StringVariableDeclaration>().Subject;
            
            var cVarByte=FormatConversions.HexStringToByteArray(cVar1.Value);
            var ptVarByte = FormatConversions.HexStringToByteArray(ptVar1.Value);
            var nistCipherByte = FormatConversions.HexStringToByteArray(nistCipherHex);
            var nistPlainTxtByte = FormatConversions.HexStringToByteArray(plaintextHex);

            if (pad == "ISO-9797-M1")
            {
                cVarByte.Length.Should().Be(nistCipherByte.Length);

            }
            else
                // Ciphertext = 5 Blöcke = 80 Bytes = 160 Hex-Zeichen (+ 0x())
                cVar1.Value.Length.Should().Be(("0x()".Length + 160));

            cVarByte.Take(nistCipherByte.Length).Should().Equal(nistCipherByte);            
            ptVarByte.Take(nistPlainTxtByte.Length).Should().Equal(nistPlainTxtByte);

            // ------------------------------------------------------------
            // Durchgang 2: NIST-Plaintext + 0xAB
            // ------------------------------------------------------------
            VariableDictionary.Instance().Clear();
            var prog2 = new AntlrToProgram();
            var plaintextPlusAb =
                plaintextHex.TrimEnd(')') + "AB)";
            var input2 =
                $"KEY k=GenerateKey(AES-CBC,{keyHex}) " +
                $"PARAM p=Parameters(#MECH:AES-CBC,#IV:{ivHex},#PAD:{pad}) " +
                $"VAR c=Encrypt(p,k,{plaintextPlusAb}) " +
                $"VAR pt=Decrypt(p,k,c)";

            var parser2 = ParserBuilder.StringBuild(input2);
            var ctx2 = parser2.program();
            SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
            var res2 = prog2.Visit(ctx2);

            var cVar2 = res2.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject;
            var ptVar2 = res2.Statements[3].Should().BeOfType<StringVariableDeclaration>().Subject;
            cVarByte = FormatConversions.HexStringToByteArray(cVar2.Value);
            ptVarByte = FormatConversions.HexStringToByteArray(ptVar2.Value);
            
            cVar2.Value.Length.Should().Be(("0x()".Length + 160));
            cVarByte.Take(nistCipherByte.Length).Should().Equal(nistCipherByte);
            var ptxPlusAb=FormatConversions.HexStringToByteArray(plaintextPlusAb);
            if(pad == "ISO-9797-M1")
                ptVarByte.Take(ptxPlusAb.Length).Should().Equal(ptxPlusAb);
            else
                ptVarByte.Take(ptVarByte.Length).Should().Equal(ptxPlusAb);
        }

    }
}

