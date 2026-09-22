using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScriptUnitTest
{
    public class KeyDeclarationTests
    {
        [Test]
        public void NewKeyHasEmptyDerivationMechanism()
        {
            var key = new KeyVariableDeclaration();

            Assert.That(key.DerivationMechanism, Is.Empty);
        }

        [Test]
        public void SerializationPreservesDerivationMechanism()
        {
            var key = new KeyVariableDeclaration { DerivationMechanism = "KDF-HKDF" };

            string json = key.Serialize();
            var restored = KeyVariableDeclaration.Deserialize(json);

            ClassicAssert.IsTrue(json.Contains("\"DerivationMechanism\":\"KDF-HKDF\""));
            Assert.That(restored.DerivationMechanism, Is.EqualTo("KDF-HKDF"));
        }

        [Test]
        public void LegacyJsonDefaultsToEmptyDerivationMechanism()
        {
            const string json = "{\"Mechanism\":\"AES-CBC\",\"KeySize\":\"128\",\"KeyValue\":\"0x(00000000000000000000000000000000)\",\"KeyAttributes\":[],\"Id\":\"legacy\",\"Type\":null,\"Value\":\"0x(00000000000000000000000000000000)\",\"ValueFormat\":\"HEX_STRING\"}";

            var restored = KeyVariableDeclaration.Deserialize(json);

            Assert.That(restored.DerivationMechanism, Is.Empty);
            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Unknown)));
            Assert.That(restored.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(restored.Mechanism, Is.EqualTo("AES-CBC"));
            Assert.That(restored.KeySize, Is.EqualTo("128"));
        }

        [Test]
        public void SerializationPreservesStronglyTypedKeyMetadata()
        {
            var key = new KeyVariableDeclaration
            {
                KeyType = KeyType.Private(KeyAlgorithm.Rsa),
                KeySizeInBits = new KeySize(2048),
                KeySize = "2048",
                Mechanism = "legacy-mechanism"
            };

            var restored = KeyVariableDeclaration.Deserialize(key.Serialize());

            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Private(KeyAlgorithm.Rsa)));
            Assert.That(restored.KeySizeInBits, Is.EqualTo(new KeySize(2048)));
            Assert.That(restored.KeySize, Is.EqualTo("2048"));
            Assert.That(restored.Mechanism, Is.EqualTo("legacy-mechanism"));
        }

        [Test]
        public void GeneratePredefinedAES256KBPKTest()
        {
            string input = "KEY kbpk=GenerateKey(AES-CBC,0x(EF0BA217D99A6D7033227079B3C3F5B16E31E828659AE1A6B5A757C2D8D20133))";
            CryptoScriptRunner prog = new CryptoScriptRunner();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Execute(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            ClassicAssert.IsTrue(statement is KeyVariableDeclaration);
        }
        [Test]
        public void GeneratePredefinedAESKBPKAndIKTest()
        {
            string input = "KEY kbpk=GenerateKey(AES-CBC,0x(EF0BA217D99A6D7033227079B3C3F5B16E31E828659AE1A6B5A757C2D8D20133)) " +
                            "KEY ik=GenerateKey(AES-CBC,0x(A714752E27B680B646CB110D6EB31C5C))";
            CryptoScriptRunner prog = new CryptoScriptRunner();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Execute(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 2);
            ClassicAssert.IsTrue(statement is KeyVariableDeclaration);
        }
        [Test]
        public void GenerateKeyRandomAES128_Test()
        {
            string input = "KEY key2=GenerateKey(AES-CBC,128)";
            CryptoScriptRunner prog = new CryptoScriptRunner();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Execute(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 1);
            ClassicAssert.IsTrue(statement is KeyVariableDeclaration);
            var variable = statement as KeyVariableDeclaration;
            ClassicAssert.IsTrue(variable.Id == "key2");
            ClassicAssert.IsTrue(variable.Mechanism == "AES-CBC");
            ClassicAssert.IsTrue(variable.DerivationMechanism == string.Empty);
            ClassicAssert.IsTrue(variable.KeySize == "128");
            var key = FormatConversions.HexStringToByteArray(variable.Value);
            ClassicAssert.IsTrue(key.Length == 16);
            ClassicAssert.IsTrue(VariableDictionary.Instance().Contains(variable.Id));
        }
        [Test]
        public void GenerateKeyWitVARDataTest() 
        {
            string input = "VAR data=0x(A714752E27B680B646CB110D6EB31C5C) " +
                           "KEY k=GenerateKey(AES-CBC,data)";
            CryptoScriptRunner prog = new CryptoScriptRunner();
            CryptoScriptParser parser = ParserBuilder.StringBuild(input);
            CryptoScriptParser.ProgramContext context = parser.program();
            var res = prog.Execute(context);
            var statement = res.Statements.FirstOrDefault();
            ClassicAssert.IsNotNull(statement);
            ClassicAssert.IsTrue(res.Statements.Count == 2);
            ClassicAssert.IsTrue(statement is VariableDeclaration);
        }
    }
}
