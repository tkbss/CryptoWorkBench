using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
            Assert.That(restored.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(restored.KeySize, Is.EqualTo("128"));
        }

        [Test]
        public void SerializationPreservesStronglyTypedKeyMetadata()
        {
            var key = new KeyVariableDeclaration
            {
                KeyType = KeyType.Private(KeyAlgorithm.Rsa),
                KeySizeInBits = new KeySize(2048),
                KeySize = "2048"
            };

            var restored = KeyVariableDeclaration.Deserialize(key.Serialize());

            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Private(KeyAlgorithm.Rsa)));
            Assert.That(restored.KeySizeInBits, Is.EqualTo(new KeySize(2048)));
            Assert.That(restored.KeySize, Is.EqualTo("2048"));
        }

        [TestCase("AES-CBC", KeyAlgorithm.Aes)]
        [TestCase("aes-gcm", KeyAlgorithm.Aes)]
        [TestCase("DES3-ECB", KeyAlgorithm.Tdea)]
        [TestCase("des3-cmac", KeyAlgorithm.Tdea)]
        [TestCase("HMAC-SHA256", KeyAlgorithm.Hmac)]
        [TestCase("hmac-sha3-512", KeyAlgorithm.Hmac)]
        [TestCase("", KeyAlgorithm.Unknown)]
        [TestCase("UNKNOWN-MECHANISM", KeyAlgorithm.Unknown)]
        [TestCase("WRAP-AES-TR31", KeyAlgorithm.Unknown)]
        [TestCase("WRAP-DES3-TR31", KeyAlgorithm.Unknown)]
        public void LegacyJsonInfersKeyTypeFromMechanism(string mechanism, KeyAlgorithm expected)
        {
            string json = JsonConvert.SerializeObject(new { Mechanism = mechanism });

            var restored = KeyVariableDeclaration.Deserialize(json);

            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(expected)));
        }

        [TestCase("null")]
        [TestCase("123")]
        [TestCase("true")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void NonStringLegacyMechanismDefaultsToUnknown(string mechanismToken)
        {
            KeyVariableDeclaration restored = KeyVariableDeclaration.Deserialize(
                $"{{\"Mechanism\":{mechanismToken}}}");

            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Unknown)));
        }

        [Test]
        public void ExplicitUnknownKeyTypeIsNotReclassifiedFromLegacyMechanism()
        {
            const string json = "{\"Mechanism\":\"AES-CBC\",\"KeyType\":{\"Algorithm\":0,\"MaterialKind\":0}}";

            var restored = KeyVariableDeclaration.Deserialize(json);

            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Unknown)));
        }

        [Test]
        public void ExplicitKeyTypeWinsOverNonStringLegacyMechanism()
        {
            const string json = "{\"Mechanism\":{\"value\":\"DES3-CBC\"},\"KeyType\":{\"Algorithm\":1,\"MaterialKind\":0}}";

            var restored = KeyVariableDeclaration.Deserialize(json);

            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
        }

        [TestCase("AES-CBC", "D0000D0TB00E0000", KeyAlgorithm.Aes)]
        [TestCase("DES3-CBC", "D0000D0AB00E0000", KeyAlgorithm.Tdea)]
        [TestCase("HMAC-SHA256", "D0000D0AB00E0000", KeyAlgorithm.Hmac)]
        [TestCase("HMAC-SHA256", "D0000D0TB00E0000", KeyAlgorithm.Hmac)]
        [TestCase("WRAP-AES-TR31", "D0000D0AB00E0000", KeyAlgorithm.Unknown)]
        [TestCase("WRAP-AES-TR31", "D0000D0TB00E0000", KeyAlgorithm.Unknown)]
        [TestCase("WRAP-AES-TR31", "D0000D0HB00E0000", KeyAlgorithm.Unknown)]
        [TestCase("WRAP-DES3-TR31", "D0000D0AB00E0000", KeyAlgorithm.Unknown)]
        [TestCase("WRAP-DES3-TR31", "D0000D0TB00E0000", KeyAlgorithm.Unknown)]
        [TestCase("WRAP-DES3-TR31", "D0000D0HB00E0000", KeyAlgorithm.Unknown)]
        [TestCase("WRAP-AES-TR31", "short", KeyAlgorithm.Unknown)]
        [TestCase("WRAP-AES-TR31", "", KeyAlgorithm.Unknown)]
        public void LegacyJsonIgnoresStoredHeaderWhenInferringKeyType(
            string mechanism, string header, KeyAlgorithm expected)
        {
            string json = JsonConvert.SerializeObject(new
            {
                Mechanism = mechanism,
                KeyAttributes = new[] { new { ID = "HDR", Data = header } }
            });

            var restored = KeyVariableDeclaration.Deserialize(json);

            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(expected)));
            Assert.That(restored.KeyAttributes.Single().Data, Is.EqualTo(header));
        }

        [Test]
        public void MultipleOrConflictingStoredHeadersDoNotAffectLegacyInference()
        {
            const string json = "{\"Mechanism\":\"AES-CBC\",\"KeyAttributes\":[" +
                                "{\"ID\":\"HDR\",\"Data\":\"short\"}," +
                                "{\"ID\":\"HDR\",\"Data\":\"D0000D0TB00E0000\"}," +
                                "{\"ID\":\"HDR\",\"Data\":\"D0000D0HB00E0000\"}]}";

            var restored = KeyVariableDeclaration.Deserialize(json);

            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Aes)));
            Assert.That(restored.KeyAttributes, Has.Count.EqualTo(3));
        }

        [Test]
        public void MigratedLegacyKeyRoundtripOmitsMechanismAndPreservesCurrentState()
        {
            const string json = "{\"Mechanism\":\"DES3-CBC\",\"DerivationMechanism\":\"legacy-kdf\",\"KeySize\":\"128\",\"KeyAttributes\":[],\"LegacyExtra\":\"ignored\"}";
            var migrated = KeyVariableDeclaration.Deserialize(json);
            string currentJson = migrated.Serialize();

            var restored = KeyVariableDeclaration.Deserialize(currentJson);

            Assert.That(JObject.Parse(currentJson).Property("Mechanism", StringComparison.OrdinalIgnoreCase), Is.Null);
            Assert.That(restored.KeyType, Is.EqualTo(KeyType.Secret(KeyAlgorithm.Tdea)));
            Assert.That(restored.DerivationMechanism, Is.EqualTo("legacy-kdf"));
            Assert.That(restored.KeySizeInBits, Is.EqualTo(new KeySize(128)));
            Assert.That(restored.KeySize, Is.EqualTo("128"));
            Assert.That(restored.KeyAttributes, Is.Empty);
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
            ClassicAssert.IsTrue(variable.KeyType == KeyType.Secret(KeyAlgorithm.Aes));
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
