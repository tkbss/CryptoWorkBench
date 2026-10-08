using Antlr4.Runtime;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.PINBLOCK;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class PinBlockArchitectureTests
{
    private static readonly string[] Mechanisms =
    {
        "WRAP-AES-PINBLOCK-4",
        "WRAP-DES3-PINBLOCK-0",
        "WRAP-DES3-PINBLOCK-1",
        "WRAP-DES3-PINBLOCK-2",
        "WRAP-DES3-PINBLOCK-3"
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TearDown]
    public void TearDown() => VariableDictionary.Instance().Clear();

    [Test]
    public void RegistryContainsAllFiveMechanismsWithOnlyWrapAndUnwrap()
    {
        foreach (string mechanism in Mechanisms)
        {
            Assert.That(MechanismRegistry.TryGet(mechanism, out MechanismRegistryEntry? entry),
                Is.True, mechanism);
            Assert.Multiple(() =>
            {
                Assert.That(entry!.CanonicalName, Is.EqualTo(mechanism));
                Assert.That(entry.SupportedFunctions, Is.EquivalentTo(new[]
                {
                    CryptoScriptFunction.Wrap,
                    CryptoScriptFunction.Unwrap
                }));
                Assert.That(entry.FunctionMetadata.Keys, Is.EquivalentTo(entry.SupportedFunctions));
            });
        }
    }

    [Test]
    public void FactoryDispatchesToTheExpectedCipherFamily()
    {
        foreach (string mechanism in Mechanisms)
        {
            PinBlockAlgorithm algorithm = AssertFactoryResult(mechanism);
            PinBlockCipherFamily expected = mechanism.StartsWith("WRAP-AES-", StringComparison.Ordinal)
                ? PinBlockCipherFamily.Aes
                : PinBlockCipherFamily.Des3;

            Assert.Multiple(() =>
            {
                Assert.That(algorithm.MechanismName, Is.EqualTo(mechanism));
                Assert.That(algorithm.CipherFamily, Is.EqualTo(expected));
            });
        }
    }

    [Test]
    public void FunctionMetadataContainsThreePositionalArgumentsAndClosedNamedContract()
    {
        foreach (string mechanism in Mechanisms)
        {
            MechanismRegistry.TryGet(mechanism, out MechanismRegistryEntry? entry);
            foreach (CryptoScriptFunction function in entry!.SupportedFunctions)
            {
                MechanismFunctionMetadata metadata = entry.FunctionMetadata[function];
                MechanismParameterMetadata[] positional = metadata.Parameters
                    .Where(parameter => parameter.Kind == MechanismParameterKind.PositionalArgument)
                    .ToArray();
                MechanismParameterMetadata[] named = metadata.Parameters
                    .Where(parameter => parameter.Kind == MechanismParameterKind.NamedParameter)
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(positional, Has.Length.EqualTo(3), $"{mechanism} {function}");
                    Assert.That(positional.All(parameter => parameter.IsRequired), Is.True);
                    Assert.That(named.Single(parameter => parameter.Name == "#MECH").IsRequired, Is.True);
                    Assert.That(metadata.AdditionalNamedParameterHandling,
                        Is.EqualTo(AdditionalNamedParameterHandling.None));
                });
            }
        }
    }

    [TestCase("WRAP-DES3-PINBLOCK-0")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    [TestCase("WRAP-AES-PINBLOCK-4")]
    public void FormatsZeroThreeAndFourRequirePanForWrapAndUnwrap(string mechanism)
    {
        foreach (CryptoScriptFunction function in new[]
                 {
                     CryptoScriptFunction.Wrap,
                     CryptoScriptFunction.Unwrap
                 })
        {
            FunctionContractException error = Assert.Throws<FunctionContractException>(() =>
                MechanismParameterContractValidator.Validate(
                    mechanism, function, Parameters(mechanism)))!;

            Assert.Multiple(() =>
            {
                Assert.That(error.Error, Is.EqualTo(FunctionContractError.MissingRequiredParameter));
                Assert.That(error.ParameterName, Is.EqualTo("#PAN"));
            });
        }
    }

    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-2")]
    public void FormatsOneAndTwoDoNotDeclarePan(string mechanism)
    {
        MechanismRegistry.TryGet(mechanism, out MechanismRegistryEntry? entry);

        foreach (MechanismFunctionMetadata metadata in entry!.FunctionMetadata.Values)
        {
            Assert.That(metadata.Parameters.Select(parameter => parameter.Name),
                Does.Not.Contain("#PAN"));
            Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
                mechanism, metadata.Function, Parameters(mechanism)));
        }
    }

    [TestCase("WRAP-DES3-PINBLOCK-1", "#TRANSACTION")]
    [TestCase("WRAP-DES3-PINBLOCK-3", "#FILL")]
    [TestCase("WRAP-AES-PINBLOCK-4", "#RANDOM")]
    public void VariableFieldsAreOptionalInOutForWrapAndOutputForUnwrap(
        string mechanism,
        string parameterName)
    {
        MechanismRegistry.TryGet(mechanism, out MechanismRegistryEntry? entry);
        MechanismParameterMetadata wrap = Named(entry!, CryptoScriptFunction.Wrap, parameterName);
        MechanismParameterMetadata unwrap = Named(entry!, CryptoScriptFunction.Unwrap, parameterName);

        Assert.Multiple(() =>
        {
            Assert.That(wrap.Direction, Is.EqualTo(MechanismParameterDirection.InOut));
            Assert.That(wrap.IsRequired, Is.False);
            Assert.That(wrap.DefaultKind, Is.EqualTo(MechanismParameterDefaultKind.Generated));
            Assert.That(unwrap.Direction, Is.EqualTo(MechanismParameterDirection.Output));
            Assert.That(unwrap.IsRequired, Is.False);
            Assert.That(unwrap.DefaultKind, Is.EqualTo(MechanismParameterDefaultKind.None));
        });

        ParameterVariableDeclaration required = mechanism.EndsWith("-1", StringComparison.Ordinal)
            ? Parameters(mechanism)
            : Parameters(mechanism, ("#PAN", "0x(1234567890123456)"));
        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            mechanism, CryptoScriptFunction.Wrap, required));
        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            mechanism, CryptoScriptFunction.Unwrap, required));

        required.SetParameter(parameterName, "0x(ABCDEF)");
        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            mechanism, CryptoScriptFunction.Wrap, required));
        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            mechanism, CryptoScriptFunction.Unwrap, required));
    }

    [Test]
    public void ClosedContractsDistinguishUnknownAndForbiddenAdditionalParameters()
    {
        ParameterVariableDeclaration unknown = Parameters("WRAP-DES3-PINBLOCK-2");
        unknown.GetParameters()["#UNKNOWN"] = "secret";
        ParameterVariableDeclaration forbidden = Parameters(
            "WRAP-DES3-PINBLOCK-2", ("#PAN", "0x(1234567890123456)"));

        foreach (CryptoScriptFunction function in new[]
                 {
                     CryptoScriptFunction.Wrap,
                     CryptoScriptFunction.Unwrap
                 })
        {
            FunctionContractException unknownError = Assert.Throws<FunctionContractException>(() =>
                MechanismParameterContractValidator.Validate(
                    "WRAP-DES3-PINBLOCK-2", function, unknown))!;
            FunctionContractException forbiddenError = Assert.Throws<FunctionContractException>(() =>
                MechanismParameterContractValidator.Validate(
                    "WRAP-DES3-PINBLOCK-2", function, forbidden))!;

            Assert.Multiple(() =>
            {
                Assert.That(unknownError.Error, Is.EqualTo(FunctionContractError.UnknownParameter));
                Assert.That(forbiddenError.Error,
                    Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
            });
        }
    }

    [Test]
    public void LexerAndParserRecognizeCanonicalMechanismsAndParameters()
    {
        string[] parameterNames = { "#PAN", "#TRANSACTION", "#FILL", "#RANDOM" };
        Assert.That(AntlrLanguageMetadata.GetMechanisms(), Is.SupersetOf(Mechanisms));
        Assert.That(AntlrLanguageMetadata.GetParameters(), Is.SupersetOf(parameterNames));

        foreach (string mechanism in Mechanisms)
        {
            var lexer = new CryptoScriptLexer(new AntlrInputStream(mechanism));
            IToken token = lexer.NextToken();
            Assert.Multiple(() =>
            {
                Assert.That(token.Type, Is.EqualTo(CryptoScriptLexer.MECHANISM), mechanism);
                Assert.That(token.Text, Is.EqualTo(mechanism));
            });

            ParserBuilder.StringBuild($"PARAM p=#MECH:{mechanism}").program();
            Assert.Multiple(() =>
            {
                Assert.That(LexerErrorListener.LexerErrorOccured, Is.False, mechanism);
                Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False, mechanism);
            });
        }

        string[] declarations =
        {
            "PARAM p0=#MECH:WRAP-DES3-PINBLOCK-0 #PAN:0x(1234567890123456)",
            "PARAM p1=#MECH:WRAP-DES3-PINBLOCK-1 #TRANSACTION:0x(1234567890)",
            "PARAM p3=#MECH:WRAP-DES3-PINBLOCK-3 #PAN:0x(1234567890123456) #FILL:0x(ABCDEF)",
            "PARAM p4=#MECH:WRAP-AES-PINBLOCK-4 #PAN:0x(1234567890123456) #RANDOM:0x(0011223344556677)"
        };
        foreach (string declaration in declarations)
        {
            ParserBuilder.StringBuild(declaration).program();
            Assert.Multiple(() =>
            {
                Assert.That(LexerErrorListener.LexerErrorOccured, Is.False, declaration);
                Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False, declaration);
            });
        }
    }

    [Test]
    public void OperationsFailExplicitlyWithoutPerformingPinBlockCryptography()
    {
        foreach (string mechanism in Mechanisms)
        {
            ParameterVariableDeclaration parameters = mechanism.EndsWith("-0", StringComparison.Ordinal) ||
                                                      mechanism.EndsWith("-3", StringComparison.Ordinal) ||
                                                      mechanism.EndsWith("-4", StringComparison.Ordinal)
                ? Parameters(mechanism, ("#PAN", "0x(1234567890123456)"))
                : Parameters(mechanism);
            var arguments = new AlgorithmCallArguments(new[]
            {
                new AlgorithmCallArgument(parameters.Value, parameters),
                new AlgorithmCallArgument("0x(00112233445566778899AABBCCDDEEFF)"),
                new AlgorithmCallArgument("0x(1234)")
            });
            PinBlockAlgorithm algorithm = AssertFactoryResult(mechanism);

            NotImplementedException wrap = Assert.Throws<NotImplementedException>(() =>
                algorithm.Wrap(arguments))!;
            NotImplementedException unwrap = Assert.Throws<NotImplementedException>(() =>
                algorithm.Unwrap(arguments))!;

            Assert.Multiple(() =>
            {
                Assert.That(wrap.Message, Is.EqualTo($"{mechanism} Wrap is not implemented."));
                Assert.That(unwrap.Message, Is.EqualTo($"{mechanism} Unwrap is not implemented."));
            });
        }
    }

    [Test]
    public void LegacyStringArrayOverloadsReachTheMechanismSpecificStub()
    {
        foreach (string mechanism in Mechanisms)
        {
            ParameterVariableDeclaration parameters = mechanism.EndsWith("-0", StringComparison.Ordinal) ||
                                                      mechanism.EndsWith("-3", StringComparison.Ordinal) ||
                                                      mechanism.EndsWith("-4", StringComparison.Ordinal)
                ? Parameters(mechanism, ("#PAN", "0x(1234567890123456)"))
                : Parameters(mechanism);
            string[] arguments =
            {
                parameters.Value,
                "0x(00112233445566778899AABBCCDDEEFF)",
                "0x(1234)"
            };
            CryptoAlgorithm algorithm = AlgorithmFactory.Create(mechanism);

            Assert.That(algorithm, Is.TypeOf<PinBlockAlgorithm>());
            NotImplementedException wrap = Assert.Throws<NotImplementedException>(() =>
                algorithm.Wrap(arguments))!;
            NotImplementedException unwrap = Assert.Throws<NotImplementedException>(() =>
                algorithm.Unwrap(arguments))!;

            Assert.Multiple(() =>
            {
                Assert.That(wrap.Message, Is.EqualTo($"{mechanism} Wrap is not implemented."));
                Assert.That(unwrap.Message, Is.EqualTo($"{mechanism} Unwrap is not implemented."));
            });
        }
    }

    [Test]
    public void ScriptsReachThePinBlockStubThroughThePublicExecutionPath()
    {
        string[] mechanisms =
        {
            "WRAP-DES3-PINBLOCK-0",
            "WRAP-AES-PINBLOCK-4"
        };
        foreach (string mechanism in mechanisms)
        {
            foreach (string function in new[] { "Wrap", "Unwrap" })
            {
                VariableDictionary.Instance().Clear();
                SyntaxErrorListner.SyntaxErrorOccured = false;
                LexerErrorListener.LexerErrorOccured = false;
                string script =
                    $"PARAM p=#MECH:{mechanism} #PAN:0x(1234567890123456) " +
                    "KEY k=0x(00112233445566778899AABBCCDDEEFF) " +
                    "VAR input=0x(1234) " +
                    $"VAR result={function}(p,k,input)";
                CryptoScriptParser parser = ParserBuilder.StringBuild(script);
                CryptoScriptParser.ProgramContext context = parser.program();
                var runner = new CryptoScriptRunner();

                SemanticErrorException error = Assert.Throws<SemanticErrorException>(() =>
                    runner.Execute(context))!;

                Assert.Multiple(() =>
                {
                    Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
                    Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
                    Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
                    Assert.That(runner.SemanticErrors, Has.Count.EqualTo(1));
                    Assert.That(runner.SemanticErrors[0], Is.SameAs(error.SemanticError));
                    Assert.That(error.SemanticError!.Type, Is.EqualTo("FunctionCall"));
                    Assert.That(error.SemanticError.FunctionName, Is.EqualTo(function));
                    Assert.That(error.SemanticError.Message,
                        Is.EqualTo($"{mechanism} {function} is not implemented."));
                });
            }
        }
    }

    [Test]
    public void RuntimeDispatchFailsExplicitlyAfterContractValidation()
    {
        var operations = new CryptoOperations();
        foreach (string mechanism in Mechanisms)
        {
            ParameterVariableDeclaration parameters = mechanism.EndsWith("-0", StringComparison.Ordinal) ||
                                                      mechanism.EndsWith("-3", StringComparison.Ordinal) ||
                                                      mechanism.EndsWith("-4", StringComparison.Ordinal)
                ? Parameters(mechanism, ("#PAN", "0x(1234567890123456)"))
                : Parameters(mechanism);
            var invocation = new OperationInvocation(new[]
            {
                new ResolvedCallArgument(
                    parameters.Value, ResolvedCallArgumentKind.Variable, parameters),
                new ResolvedCallArgument(
                    "0x(00112233445566778899AABBCCDDEEFF)", ResolvedCallArgumentKind.Expression),
                new ResolvedCallArgument("0x(1234)", ResolvedCallArgumentKind.Expression)
            });

            Assert.Throws<NotImplementedException>(() => operations.Wrap(invocation));
            Assert.Throws<NotImplementedException>(() => operations.Unwrap(invocation));
        }
    }

    [Test]
    public void ExistingTr31DispatchAndContractsRemainUnchanged()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AlgorithmFactory.Create("WRAP-AES-TR31"), Is.TypeOf<WrapAESTR31>());
            Assert.That(AlgorithmFactory.Create("WRAP-DES3-TR31"), Is.TypeOf<WrapDES3TR31>());
            Assert.That(MechanismRegistry.TryGet("WRAP-AES-TR31", out MechanismRegistryEntry? aes),
                Is.True);
            Assert.That(aes!.SupportedFunctions, Is.EquivalentTo(new[]
            {
                CryptoScriptFunction.Wrap,
                CryptoScriptFunction.Unwrap,
                CryptoScriptFunction.BlockHeader
            }));
            Assert.That(MechanismRegistry.TryGet("WRAP-DES3-TR31", out MechanismRegistryEntry? des3),
                Is.True);
            Assert.That(des3!.SupportedFunctions, Is.EquivalentTo(new[]
            {
                CryptoScriptFunction.Wrap,
                CryptoScriptFunction.Unwrap
            }));
        });
    }

    private static PinBlockAlgorithm AssertFactoryResult(string mechanism)
    {
        CryptoAlgorithm algorithm = AlgorithmFactory.Create(mechanism);
        Assert.That(algorithm, Is.TypeOf<PinBlockAlgorithm>());
        return (PinBlockAlgorithm)algorithm;
    }

    private static MechanismParameterMetadata Named(
        MechanismRegistryEntry entry,
        CryptoScriptFunction function,
        string name) =>
        entry.FunctionMetadata[function].Parameters.Single(parameter =>
            parameter.Kind == MechanismParameterKind.NamedParameter && parameter.Name == name);

    private static ParameterVariableDeclaration Parameters(
        string mechanism,
        params (string Name, string Value)[] values)
    {
        var parameters = new ParameterVariableDeclaration { Mechanism = mechanism };
        foreach ((string name, string value) in values)
            parameters.SetParameter(name, value);
        return parameters;
    }
}
