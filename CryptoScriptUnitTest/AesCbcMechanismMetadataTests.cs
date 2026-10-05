using System.Collections.Frozen;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class AesCbcMechanismMetadataTests
{
    private const string Key = "0x(00112233445566778899AABBCCDDEEFF)";
    private const string Iv = "0x(000102030405060708090A0B0C0D0E0F)";

    private VariableDeclaration[] previousVariables = null!;
    private SyntaxError[] previousSyntaxErrors = null!;
    private bool previousSyntaxErrorOccurred;
    private bool previousLexerErrorOccurred;
    private string previousErrorMessage = null!;

    [SetUp]
    public void SetUp()
    {
        previousVariables = VariableDictionary.Instance().GetVariables().ToArray();
        previousSyntaxErrors = SyntaxErrorList.Instance().ToArray();
        previousSyntaxErrorOccurred = SyntaxErrorListner.SyntaxErrorOccured;
        previousLexerErrorOccurred = LexerErrorListener.LexerErrorOccured;
        previousErrorMessage = SyntaxErrorListner.ErrorMessage.ToString();

        VariableDictionary.Instance().Clear();
        SyntaxErrorList.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.ErrorMessage.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        VariableDictionary.Instance().Clear();
        foreach (VariableDeclaration variable in previousVariables)
        {
            VariableDictionary.Instance().Add(variable);
        }

        SyntaxErrorList.Instance().Clear();
        SyntaxErrorList.Instance().AddRange(previousSyntaxErrors);
        SyntaxErrorListner.SyntaxErrorOccured = previousSyntaxErrorOccurred;
        LexerErrorListener.LexerErrorOccured = previousLexerErrorOccurred;
        SyntaxErrorListner.ErrorMessage.Clear();
        SyntaxErrorListner.ErrorMessage.Append(previousErrorMessage);
    }

    [Test]
    public void MetadataExistsOnlyForTheFourExistingAesCbcFunctions()
    {
        Assert.That(MechanismRegistry.TryGet("AES-CBC", out MechanismRegistryEntry? aesCbc), Is.True);
        Assert.That(aesCbc!.FunctionMetadata.Keys, Is.EquivalentTo(new[]
        {
            CryptoScriptFunction.Parameters,
            CryptoScriptFunction.GenerateKey,
            CryptoScriptFunction.Encrypt,
            CryptoScriptFunction.Decrypt
        }));

        foreach (MechanismRegistryEntry other in MechanismRegistry.Entries.Where(
                     entry => entry.CanonicalName != "AES-CBC"))
        {
            Assert.That(other.FunctionMetadata, Is.Empty, other.CanonicalName);
        }
    }

    [Test]
    public void MetadataCapturesRequiredArgumentsDefaultsRangesAndCombinations()
    {
        MechanismRegistry.TryGet("AES-CBC", out MechanismRegistryEntry? entry);
        IReadOnlyDictionary<CryptoScriptFunction, MechanismFunctionMetadata> functions =
            entry!.FunctionMetadata;

        MechanismParameterMetadata[] parameterCreation =
            functions[CryptoScriptFunction.Parameters].Parameters.ToArray();
        MechanismParameterMetadata[] keyGeneration =
            functions[CryptoScriptFunction.GenerateKey].Parameters.ToArray();
        MechanismParameterMetadata[] encryption =
            functions[CryptoScriptFunction.Encrypt].Parameters.ToArray();
        MechanismParameterMetadata[] decryption =
            functions[CryptoScriptFunction.Decrypt].Parameters.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(parameterCreation.Select(parameter => parameter.Name),
                Is.EqualTo(new[] { "mechanism", "#IV", "#PAD" }));
            Assert.That(parameterCreation.Single(parameter => parameter.Name == "mechanism").IsRequired, Is.True);
            Assert.That(parameterCreation.Single(parameter => parameter.Name == "#IV").DefaultKind,
                Is.EqualTo(MechanismParameterDefaultKind.Generated));
            Assert.That(parameterCreation.Single(parameter => parameter.Name == "#PAD").DefaultValue,
                Is.EqualTo("PKCS-7"));
            Assert.That(parameterCreation.Single(parameter => parameter.Name == "#PAD").ValueConstraint,
                Does.Contain("ISO-10126"));
            Assert.That(keyGeneration.Single(parameter => parameter.Name == "keySizeOrValue").ResultingDataTypes,
                Is.EquivalentTo(new[] { MechanismParameterDataType.Integer, MechanismParameterDataType.HexString }));
            Assert.That(keyGeneration.Single(parameter => parameter.Name == "keySizeOrValue").ValueConstraint,
                Does.Contain("128").And.Contain("192").And.Contain("256"));
            Assert.That(encryption.Where(parameter => parameter.Kind == MechanismParameterKind.NamedParameter)
                    .Select(parameter => parameter.Name),
                Is.EqualTo(new[] { "#MECH", "#IV", "#PAD" }));
            Assert.That(encryption.Single(parameter => parameter.Name == "#IV").ValueConstraint,
                Does.Contain("16 bytes"));
            Assert.That(encryption.Single(parameter => parameter.Name == "#PAD").ValueConstraint,
                Does.Contain("ISO-10126").And.Not.Contain("grammar cannot express"));
            Assert.That(decryption.Single(parameter => parameter.Name == "#PAD").ValueConstraint,
                Does.Contain("ISO-10126").And.Not.Contain("grammar cannot express"));
            Assert.That(encryption.Single(parameter => parameter.Name == "data").ValueConstraint,
                Does.Contain("#PAD:NONE").And.Contain("non-zero multiple of 16 bytes"));
            Assert.That(decryption.Single(parameter => parameter.Name == "data").ValueConstraint,
                Does.Contain("#PAD:NONE").And.Contain("non-zero multiple of 16 bytes"));
        });
    }

    [Test]
    public void AllMetadataCollectionsAndPropertiesAreImmutable()
    {
        MechanismRegistry.TryGet("AES-CBC", out MechanismRegistryEntry? entry);
        Assert.That(entry!.FunctionMetadata, Is.InstanceOf<FrozenDictionary<CryptoScriptFunction, MechanismFunctionMetadata>>());

        var functions = (IDictionary<CryptoScriptFunction, MechanismFunctionMetadata>)entry.FunctionMetadata;
        MechanismFunctionMetadata encryption = entry.FunctionMetadata[CryptoScriptFunction.Encrypt];
        var parameters = (IList<MechanismParameterMetadata>)encryption.Parameters;
        MechanismParameterMetadata key = parameters.Single(parameter => parameter.Name == "key");
        var types = (ISet<MechanismParameterDataType>)key.ResultingDataTypes;
        MechanismParameterMetadata iv = parameters.Single(parameter => parameter.Name == "#IV");
        var inputForms = (ISet<MechanismParameterInputForm>)iv.AcceptedInputForms;

        Assert.Multiple(() =>
        {
            Assert.Throws<NotSupportedException>(() => functions.Add(
                CryptoScriptFunction.Mac,
                new MechanismFunctionMetadata(CryptoScriptFunction.Mac, Array.Empty<MechanismParameterMetadata>())));
            Assert.Throws<NotSupportedException>(() => parameters.Add(key));
            Assert.Throws<NotSupportedException>(() => types.Add(MechanismParameterDataType.Integer));
            Assert.Throws<NotSupportedException>(() => inputForms.Add(MechanismParameterInputForm.HexLiteral));
            Assert.That(typeof(MechanismParameterMetadata).GetProperties().Select(property => property.SetMethod),
                Is.All.Null);
        });
    }

    [Test]
    public void MetadataSeparatesIvInputFormsFromItsConvertedValue()
    {
        MechanismRegistry.TryGet("AES-CBC", out MechanismRegistryEntry? entry);
        MechanismParameterMetadata[] ivMetadata = entry!.FunctionMetadata.Values
            .SelectMany(metadata => metadata.Parameters)
            .Where(parameter => parameter.Name == "#IV")
            .ToArray();

        Assert.That(ivMetadata, Has.Length.EqualTo(3));
        Assert.Multiple(() =>
        {
            foreach (MechanismParameterMetadata iv in ivMetadata)
            {
                Assert.That(iv.AcceptedInputForms, Is.EquivalentTo(new[]
                {
                    MechanismParameterInputForm.HexLiteral,
                    MechanismParameterInputForm.Base64Literal,
                    MechanismParameterInputForm.VariableReference
                }));
                Assert.That(iv.ResultingDataTypes,
                    Is.EquivalentTo(new[] { MechanismParameterDataType.BinaryData }));
                Assert.That(iv.ValueConstraint,
                    Does.Contain("Hexadecimal or Base64").And.Contain("exactly 16 bytes"));
            }
        });
    }

    [Test]
    public void MetadataStoresAdditionalKnownParametersButOnlyDeclaresConsumedParameters()
    {
        MechanismRegistry.TryGet("AES-CBC", out MechanismRegistryEntry? entry);
        MechanismFunctionMetadata parameters =
            entry!.FunctionMetadata[CryptoScriptFunction.Parameters];
        MechanismFunctionMetadata encryption =
            entry.FunctionMetadata[CryptoScriptFunction.Encrypt];
        MechanismFunctionMetadata decryption =
            entry.FunctionMetadata[CryptoScriptFunction.Decrypt];

        Assert.Multiple(() =>
        {
            Assert.That(parameters.AdditionalNamedParameterHandling,
                Is.EqualTo(AdditionalNamedParameterHandling.StoreGloballyKnown));
            Assert.That(parameters.Parameters.Select(parameter => parameter.Name),
                Does.Not.Contain("#NONCE"));
            Assert.That(encryption.AdditionalNamedParameterHandling,
                Is.EqualTo(AdditionalNamedParameterHandling.IgnoreStored));
            Assert.That(decryption.AdditionalNamedParameterHandling,
                Is.EqualTo(AdditionalNamedParameterHandling.IgnoreStored));
            Assert.That(encryption.Parameters
                    .Where(parameter => parameter.Kind == MechanismParameterKind.NamedParameter)
                    .Select(parameter => parameter.Name),
                Is.EqualTo(new[] { "#MECH", "#IV", "#PAD" }));
            Assert.That(decryption.Parameters
                    .Where(parameter => parameter.Kind == MechanismParameterKind.NamedParameter)
                    .Select(parameter => parameter.Name),
                Is.EqualTo(new[] { "#MECH", "#IV", "#PAD" }));
        });
    }

    [Test]
    public void RuntimeAcceptsHexAndBase64IvLiteralsAndBinaryVariables()
    {
        const string ivAsHex = "0x(30313233343536373839414243444546)";
        const string ivAsBase64 = "b64(MDEyMzQ1Njc4OUFCQ0RFRg==)";
        const string plaintext = "0x(00112233445566778899AABBCCDDEEFF)";
        string script =
            $"VAR hexIv={ivAsHex} " +
            $"VAR base64Iv={ivAsBase64} " +
            $"KEY k=GenerateKey(AES-CBC,{Key}) " +
            $"PARAM hexLiteral=Parameters(AES-CBC,#IV:{ivAsHex},#PAD:NONE) " +
            $"PARAM base64Literal=Parameters(AES-CBC,#IV:{ivAsBase64},#PAD:NONE) " +
            "PARAM hexReference=Parameters(AES-CBC,#IV:hexIv,#PAD:NONE) " +
            "PARAM base64Reference=Parameters(AES-CBC,#IV:base64Iv,#PAD:NONE) " +
            $"VAR hexLiteralCipher=Encrypt(hexLiteral,k,{plaintext}) " +
            $"VAR base64LiteralCipher=Encrypt(base64Literal,k,{plaintext}) " +
            $"VAR hexReferenceCipher=Encrypt(hexReference,k,{plaintext}) " +
            $"VAR base64ReferenceCipher=Encrypt(base64Reference,k,{plaintext})";

        CryptoScriptProgram result = new CryptoScriptRunner().Execute(
            ParserBuilder.StringBuild(script).program());
        var parameters = result.Statements.OfType<ParameterVariableDeclaration>().ToArray();
        var ciphertexts = result.Statements.OfType<StringVariableDeclaration>()
            .Where(variable => variable.Id.EndsWith("Cipher", StringComparison.Ordinal))
            .Select(variable => variable.Value)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(parameters.Single(parameter => parameter.Id == "hexLiteral").GetParameter("IV"),
                Is.EqualTo(ivAsHex));
            Assert.That(parameters.Single(parameter => parameter.Id == "base64Literal").GetParameter("IV"),
                Is.EqualTo(ivAsBase64));
            Assert.That(parameters.Single(parameter => parameter.Id == "hexReference").GetParameter("IV"),
                Is.EqualTo(ivAsHex));
            Assert.That(parameters.Single(parameter => parameter.Id == "base64Reference").GetParameter("IV"),
                Is.EqualTo(ivAsBase64));
            Assert.That(ciphertexts, Has.Length.EqualTo(4));
            Assert.That(ciphertexts, Is.All.EqualTo(ciphertexts[0]));
        });
    }

    [TestCase("\"0123456789ABCDEF\"")]
    [TestCase("textIv")]
    public void ParametersRejectsStringLiteralAndTextVariableIv(string iv)
    {
        string declaration = iv == "textIv" ? "VAR textIv=\"0123456789ABCDEF\" " : string.Empty;

        SemanticErrorException exception = AssertSemanticError(
            $"{declaration}PARAM p=Parameters(AES-CBC,#IV:{iv},#PAD:NONE)");

        Assert.That(exception.SemanticError!.Message,
            Does.Contain("AES-CBC #IV").And.Contain("hexadecimal or Base64"));
    }

    [Test]
    public void ParametersReportsInvalidBase64EncodingAsSemanticError()
    {
        SemanticErrorException exception = AssertSemanticError(
            "PARAM p=Parameters(AES-CBC,#IV:b64(AAAA=),#PAD:NONE)");

        Assert.That(exception.SemanticError!.Message,
            Does.Contain("AES-CBC #IV").And.Contain("invalid Base64 encoding"));
    }

    [Test]
    public void ParametersReportsInvalidHexadecimalEncodingAsSemanticError()
    {
        SemanticErrorException exception = AssertSemanticError(
            "PARAM p=Parameters(AES-CBC,#IV:0x(0),#PAD:NONE)");

        Assert.That(exception.SemanticError!.Message,
            Does.Contain("AES-CBC #IV").And.Contain("invalid hexadecimal encoding"));
    }

    [TestCase("0x(000102030405060708090A0B0C0D0E)", 15)]
    [TestCase("0x(000102030405060708090A0B0C0D0E0F10)", 17)]
    [TestCase("b64(MDEyMzQ1Njc4OUFCQ0RF)", 15)]
    [TestCase("b64(MDEyMzQ1Njc4OUFCQ0RFRjA=)", 17)]
    public void ParametersReportsDecodedIvLengthAsSemanticError(string iv, int byteLength)
    {
        SemanticErrorException exception = AssertSemanticError(
            $"PARAM p=Parameters(AES-CBC,#IV:{iv},#PAD:NONE)");

        Assert.That(exception.SemanticError!.Message,
            Does.Contain("AES-CBC #IV").And.Contain("exactly 16 bytes")
                .And.Contain($"actual length is {byteLength} bytes"));
    }

    [Test]
    public void DirectParameterDeclarationAcceptsBinaryVariableAtEncryptionBoundary()
    {
        const string plaintext = "0x(00112233445566778899AABBCCDDEEFF)";
        string script =
            $"VAR iv={Iv} " +
            $"KEY k=GenerateKey(AES-CBC,{Key}) " +
            "PARAM p=#MECH:AES-CBC #IV:iv #PAD:NONE " +
            $"VAR encrypted=Encrypt(p,k,{plaintext})";

        CryptoScriptProgram result = Execute(script);
        StringVariableDeclaration encrypted = result.Statements
            .OfType<StringVariableDeclaration>()
            .Single(variable => variable.Id == "encrypted");

        Assert.That(FormatConversions.ToByteArray(encrypted.Value, FormatConversions.HEX),
            Has.Length.EqualTo(16));
    }

    [TestCase("\"0123456789ABCDEF\"")]
    [TestCase("textIv")]
    public void DirectParameterDeclarationRejectsTextAtEncryptionBoundary(string iv)
    {
        string declaration = iv == "textIv" ? "VAR textIv=\"0123456789ABCDEF\" " : string.Empty;
        string script =
            $"{declaration}KEY k=GenerateKey(AES-CBC,{Key}) " +
            $"PARAM p=#MECH:AES-CBC #IV:{iv} #PAD:NONE " +
            "VAR encrypted=Encrypt(p,k,0x(00112233445566778899AABBCCDDEEFF))";

        SemanticErrorException exception = AssertSemanticError(script);

        Assert.That(exception.SemanticError!.Message,
            Does.Contain("AES-CBC #IV").And.Contain("hexadecimal or Base64"));
    }

    [Test]
    public void DirectParameterDeclarationRejectsTextAtDecryptionBoundary()
    {
        string script =
            $"KEY k=GenerateKey(AES-CBC,{Key}) " +
            "PARAM p=#MECH:AES-CBC #IV:\"0123456789ABCDEF\" #PAD:NONE " +
            "VAR decrypted=Decrypt(p,k,0x(00112233445566778899AABBCCDDEEFF))";

        SemanticErrorException exception = AssertSemanticError(script);

        Assert.That(exception.SemanticError!.Message,
            Does.Contain("AES-CBC #IV").And.Contain("hexadecimal or Base64"));
    }

    [Test]
    public void ReconstructedParameterSetUsesValidatedIvBytes()
    {
        const string base64Iv = "b64(AAECAwQFBgcICQoLDA0ODw==)";
        const string plaintext = "0x(00112233445566778899AABBCCDDEEFF)";
        var algorithm = AlgorithmFactory.Create("AES-CBC");
        ParameterVariableDeclaration parameters = algorithm.GenerateParameters(
            "AES-CBC", new[] { $"#IV:{base64Iv}", "#PAD:NONE" });
        KeyVariableDeclaration key = algorithm.GenerateKey("AES-CBC", Key);

        StringVariableDeclaration encrypted = algorithm.Encrypt(
            new[] { parameters.Value, key.Value, plaintext });
        StringVariableDeclaration decrypted = algorithm.Decrypt(
            new[] { parameters.Value, key.Value, encrypted.Value });

        Assert.That(decrypted.Value, Is.EqualTo(plaintext).IgnoreCase);
    }

    [Test]
    public void GeneratedDefaultIvPassesEncryptionAndDecryptionValidation()
    {
        const string plaintext = "0x(00112233445566778899AABBCCDDEEFF)";
        var algorithm = AlgorithmFactory.Create("AES-CBC");
        ParameterVariableDeclaration parameters = algorithm.GenerateParameters("AES-CBC");
        KeyVariableDeclaration key = algorithm.GenerateKey("AES-CBC", Key);

        StringVariableDeclaration encrypted = algorithm.Encrypt(
            new[] { parameters.Value, key.Value, plaintext });
        StringVariableDeclaration decrypted = algorithm.Decrypt(
            new[] { parameters.Value, key.Value, encrypted.Value });

        Assert.That(decrypted.Value, Is.EqualTo(plaintext).IgnoreCase);
    }

    [Test]
    public void EncryptAcceptsBase64IvIndependently()
    {
        const string base64Iv = "b64(AAECAwQFBgcICQoLDA0ODw==)";
        const string plaintext = "0x(00112233445566778899AABBCCDDEEFF)";
        var algorithm = AlgorithmFactory.Create("AES-CBC");
        ParameterVariableDeclaration hexParameters = algorithm.GenerateParameters(
            "AES-CBC", new[] { $"#IV:{Iv}", "#PAD:NONE" });
        ParameterVariableDeclaration base64Parameters = algorithm.GenerateParameters(
            "AES-CBC", new[] { $"#IV:{base64Iv}", "#PAD:NONE" });
        KeyVariableDeclaration key = algorithm.GenerateKey("AES-CBC", Key);

        StringVariableDeclaration encryptedWithBase64 = algorithm.Encrypt(
            new[] { base64Parameters.Value, key.Value, plaintext });
        StringVariableDeclaration encryptedWithHex = algorithm.Encrypt(
            new[] { hexParameters.Value, key.Value, plaintext });

        Assert.That(encryptedWithBase64.Value, Is.EqualTo(encryptedWithHex.Value));
    }

    [Test]
    public void DecryptAcceptsBase64IvIndependently()
    {
        const string base64Iv = "b64(AAECAwQFBgcICQoLDA0ODw==)";
        const string plaintext = "0x(00112233445566778899AABBCCDDEEFF)";
        var algorithm = AlgorithmFactory.Create("AES-CBC");
        ParameterVariableDeclaration hexParameters = algorithm.GenerateParameters(
            "AES-CBC", new[] { $"#IV:{Iv}", "#PAD:NONE" });
        ParameterVariableDeclaration base64Parameters = algorithm.GenerateParameters(
            "AES-CBC", new[] { $"#IV:{base64Iv}", "#PAD:NONE" });
        KeyVariableDeclaration key = algorithm.GenerateKey("AES-CBC", Key);
        StringVariableDeclaration encryptedWithHex = algorithm.Encrypt(
            new[] { hexParameters.Value, key.Value, plaintext });

        StringVariableDeclaration decryptedWithBase64 = algorithm.Decrypt(
            new[] { base64Parameters.Value, key.Value, encryptedWithHex.Value });

        Assert.That(decryptedWithBase64.Value, Is.EqualTo(plaintext).IgnoreCase);
    }

    [Test]
    public void DuplicateIvParametersRemainLastWriteWinsBeforeValidation()
    {
        var algorithm = AlgorithmFactory.Create("AES-CBC");

        ParameterVariableDeclaration parameters = algorithm.GenerateParameters("AES-CBC", new[]
        {
            "#IV:\"not binary\"", $"#IV:{Iv}", "#PAD:NONE"
        });

        Assert.That(parameters.GetParameter("IV"), Is.EqualTo(Iv));

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            algorithm.GenerateParameters("AES-CBC", new[]
            {
                $"#IV:{Iv}", "#IV:\"not binary\"", "#PAD:NONE"
            }))!;
        Assert.That(exception.Message, Does.Contain("hexadecimal or Base64"));
    }

    [TestCase("64")]
    [TestCase("129")]
    [TestCase("0x(0011)")]
    public void RuntimeRejectsInvalidGeneratedOrImportedKeySizes(string sizeOrValue)
    {
        var algorithm = AlgorithmFactory.Create("AES-CBC");
        Assert.Throws<ArgumentException>(() => algorithm.GenerateKey("AES-CBC", sizeOrValue));
    }

    [Test]
    public void ScriptParametersStoreNonceButEncryptionAndDecryptionIgnoreIt()
    {
        const string plaintext = "0x(00112233445566778899AABBCCDDEEFF)";
        string script =
            $"KEY k=GenerateKey(AES-CBC,{Key}) " +
            $"PARAM baseline=Parameters(AES-CBC,#IV:{Iv},#PAD:NONE) " +
            $"PARAM withNonce=Parameters(AES-CBC,#IV:{Iv},#PAD:NONE,#NONCE:0x(01)) " +
            $"VAR baselineCiphertext=Encrypt(baseline,k,{plaintext}) " +
            $"VAR nonceCiphertext=Encrypt(withNonce,k,{plaintext}) " +
            "VAR baselineCleartext=Decrypt(baseline,k,baselineCiphertext) " +
            "VAR nonceCleartext=Decrypt(withNonce,k,baselineCiphertext)";

        CryptoScriptProgram result = Execute(script);
        ParameterVariableDeclaration withNonce = result.Statements
            .OfType<ParameterVariableDeclaration>()
            .Single(parameter => parameter.Id == "withNonce");
        StringVariableDeclaration baselineCiphertext = result.Statements
            .OfType<StringVariableDeclaration>()
            .Single(variable => variable.Id == "baselineCiphertext");
        StringVariableDeclaration nonceCiphertext = result.Statements
            .OfType<StringVariableDeclaration>()
            .Single(variable => variable.Id == "nonceCiphertext");
        StringVariableDeclaration baselineCleartext = result.Statements
            .OfType<StringVariableDeclaration>()
            .Single(variable => variable.Id == "baselineCleartext");
        StringVariableDeclaration nonceCleartext = result.Statements
            .OfType<StringVariableDeclaration>()
            .Single(variable => variable.Id == "nonceCleartext");

        Assert.Multiple(() =>
        {
            Assert.That(withNonce.GetParameter("NONCE"), Is.EqualTo("0x(01)"));
            Assert.That(nonceCiphertext.Value, Is.EqualTo(baselineCiphertext.Value));
            Assert.That(baselineCleartext.Value, Is.EqualTo(plaintext).IgnoreCase);
            Assert.That(nonceCleartext.Value, Is.EqualTo(baselineCleartext.Value));
        });
    }

    [Test]
    public void RuntimeRejectsTrulyUnknownParameterName()
    {
        var algorithm = AlgorithmFactory.Create("AES-CBC");
        var exception = Assert.Throws<ArgumentException>(() => algorithm.GenerateParameters(
            "AES-CBC", new[] { "#UNKNOWN:1" }));

        Assert.That(exception!.Message, Does.Contain("Unknown parameter name '#UNKNOWN'."));
    }

    [Test]
    public void GrammarAcceptsAnArbitraryIdentifierAsPaddingButRejectsAnUnknownParameterName()
    {
        SyntaxErrorListner.SyntaxErrorOccured = false;
        ParserBuilder.StringBuild("PARAM p=Parameters(AES-CBC,#PAD:INVALID)").program();
        bool paddingParsed = !SyntaxErrorListner.SyntaxErrorOccured;

        SyntaxErrorListner.SyntaxErrorOccured = false;
        ParserBuilder.StringBuild("PARAM p=Parameters(AES-CBC,#UNKNOWN:1)").program();
        bool unknownNameParsed = !SyntaxErrorListner.SyntaxErrorOccured;

        Assert.Multiple(() =>
        {
            Assert.That(paddingParsed, Is.True);
            Assert.That(unknownNameParsed, Is.False);
        });
    }

    [Test]
    public void RuntimeRejectsInvalidPaddingAndNoneWithUnalignedInput()
    {
        var algorithm = AlgorithmFactory.Create("AES-CBC");
        KeyVariableDeclaration key = algorithm.GenerateKey("AES-CBC", Key);

        ParameterVariableDeclaration badPadding = algorithm.GenerateParameters("AES-CBC", new[]
            { $"#IV:{Iv}", "#PAD:INVALID" });
        ParameterVariableDeclaration noPadding = algorithm.GenerateParameters("AES-CBC", new[]
            { $"#IV:{Iv}", "#PAD:NONE" });

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => algorithm.Encrypt(new[]
                { badPadding.Value, key.Value, "0x(00112233445566778899AABBCCDDEEFF)" }));
            Assert.Throws<ArgumentException>(() => algorithm.Encrypt(new[]
                { noPadding.Value, key.Value, "0x(00)" }));
        });
    }

    private static CryptoScriptProgram Execute(string script) =>
        new CryptoScriptRunner().Execute(ParserBuilder.StringBuild(script).program());

    private static SemanticErrorException AssertSemanticError(string script) =>
        Assert.Throws<SemanticErrorException>(() => Execute(script))!;
}
