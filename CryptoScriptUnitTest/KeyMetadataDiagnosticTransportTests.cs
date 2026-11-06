using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class KeyMetadataDiagnosticTransportTests
{
    private const string Data = "0x(00112233445566778899AABBCCDDEEFF)";
    private const string Prefix = "KEY k=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F)) " +
        "PARAM p=Parameters(AES-ECB,#PAD:NONE) ";
    private VariableDeclaration[] previous = null!;

    [SetUp]
    public void SetUp()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }

    [TearDown]
    public void TearDown()
    {
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous)
            VariableDictionary.Instance().Add(variable);
    }

    private static CryptoScriptProgram Execute(CryptoScriptRunner runner, string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        return runner.Execute(context);
    }

    // OperationFactory selects this probe on the real dispatch path. The callback only
    // observes the invocation; the selected probe delegates to real AES after inspection.
    private static CryptoScriptRunner Runner(Action<AlgorithmCallArguments>? inspect = null)
    {
        var probe = new ProbeAlgorithm(arguments =>
        {
            inspect?.Invoke(arguments);
            var key = (KeyVariableDeclaration)arguments.Arguments[1].SourceVariable!;
            KeyMetadataValidator.Validate(key, KeyAlgorithm.Tdea, KeyUsage.MacGenerate,
                "Encrypt", "AES-ECB", arguments.DiagnosticSink);
        }, AlgorithmFactory.Create("AES-ECB"));
        return new CryptoScriptRunner((call, sink) =>
        {
            Assert.That(call.Invocation!.DiagnosticSink, Is.SameAs(sink));
        }, _ => probe);
    }

    [Test]
    public void ActualScriptPathCarriesSinkAndOriginalKeyToAlgorithmThenReturnsRealCryptoResult()
    {
        AlgorithmCallArguments? observed = null;
        var runner = Runner(args => observed = args);
        var program = Execute(runner, Prefix + $"VAR result=Encrypt(p,k,{Data})");
        Assert.That(observed, Is.Not.Null);
        Assert.That(observed!.DiagnosticSink, Is.Not.Null);
        Assert.That(observed.Arguments[0].SourceVariable, Is.SameAs(VariableDictionary.Instance().Get("p")));
        Assert.That(observed.Arguments[1].SourceVariable, Is.SameAs(VariableDictionary.Instance().Get("k")));
        Assert.That(observed.Arguments[1].Kind, Is.EqualTo(ResolvedCallArgumentKind.Variable));
        Assert.That(observed.Arguments[1].HasReconstructedSourceVariable, Is.False);
        Assert.That(observed.Arguments[2].Kind, Is.EqualTo(ResolvedCallArgumentKind.HexLiteral));
        Assert.That(program.Diagnostics, Has.Count.EqualTo(1));
        var diagnostic = program.Diagnostics[0];
        Assert.That(diagnostic.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyTypeConflict));
        Assert.That(diagnostic.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
        Assert.That(diagnostic.Location, Is.EqualTo(new DiagnosticLocation(
            diagnostic.Location!.ExecutionId, 3, 3, null, "Encrypt", "AES-ECB", "k")));
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(diagnostic));
        Assert.That(runner.SemanticErrors, Is.Empty);
        Assert.That(VariableDictionary.Instance().Get("result").Value,
            Is.EqualTo("0x(69C4E0D86A7B0430D8CDB78070B4C55A)").IgnoreCase);
    }

    [Test]
    public void NestedAlgorithmsKeepDifferentScopedSinksAndOuterFirstOperationIds()
    {
        var observed = new List<AlgorithmCallArguments>();
        var runner = Runner(observed.Add);
        var program = Execute(runner, Prefix + $"VAR result=Encrypt(p,k,Encrypt(p,k,{Data}))");
        Assert.That(observed, Has.Count.EqualTo(2));
        Assert.That(observed[0].DiagnosticSink, Is.Not.SameAs(observed[1].DiagnosticSink));
        Assert.That(observed[1].Arguments[2].Kind, Is.EqualTo(ResolvedCallArgumentKind.NestedFunctionCall));
        Assert.That(observed[1].Arguments[2].SourceVariable, Is.Null);
        Assert.That(program.Diagnostics.Select(d => d.Location!.OperationId), Is.EqualTo(new[] { 4, 3 }));
        Assert.That(program.Diagnostics.Select(d => d.Location!.ParentOperationId), Is.EqualTo(new int?[] { 3, null }));
        Assert.That(program.Diagnostics.Select(d => d.Location!.StatementIndex), Is.All.EqualTo(3));
        Assert.That(program.Diagnostics.Select(d => d.Location!.ExecutionId).Distinct().Count(), Is.EqualTo(1));
        Assert.That(runner.SemanticErrors, Is.Empty);
    }

    [Test]
    public void RunnersAndRepeatedExecutionsDoNotShareDiagnostics()
    {
        var firstRunner = Runner();
        var secondRunner = Runner();
        Execute(firstRunner, Prefix);
        var first = Execute(firstRunner, $"Encrypt(p,k,{Data})");
        var second = Execute(secondRunner, $"Encrypt(p,k,{Data})");
        var third = Execute(firstRunner, $"Encrypt(p,k,{Data})");
        Assert.That(new[] { first, second, third }.Select(p => p.Diagnostics.Single().Location!.ExecutionId)
            .Distinct().Count(), Is.EqualTo(3));
        Assert.That(first.Diagnostics.Single(), Is.Not.SameAs(third.Diagnostics.Single()));
        Assert.That(firstRunner.Diagnostics.Single(), Is.SameAs(third.Diagnostics.Single()));
        Assert.That(secondRunner.Diagnostics.Single(), Is.SameAs(second.Diagnostics.Single()));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DirectCSharpInvocationWithOptionalSinkPreservesSourceAndArgumentKind(bool withSink)
    {
        var key = new KeyVariableDeclaration { Id = "direct", KeyType = KeyType.Secret(KeyAlgorithm.Aes) };
        var parameters = new ParameterVariableDeclaration { Mechanism = "AES-ECB" };
        var sink = new SemanticDiagnosticSink();
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameters.Value, ResolvedCallArgumentKind.Variable, parameters),
            new ResolvedCallArgument(key.Value, ResolvedCallArgumentKind.Variable, key),
            new ResolvedCallArgument(Data, ResolvedCallArgumentKind.HexLiteral)
        }, withSink ? sink : null);
        AlgorithmCallArguments? seen = null;
        var algorithm = new ProbeAlgorithm(args =>
        {
            seen = args;
            KeyMetadataValidator.Validate(key, KeyAlgorithm.Tdea, KeyUsage.Encrypt,
                "Encrypt", "AES-ECB", args.DiagnosticSink);
        });
        Assert.That(new CryptoOperations(_ => algorithm).Encrypt(invocation), Is.Not.Null);
        Assert.That(seen!.DiagnosticSink, Is.SameAs(invocation.DiagnosticSink));
        Assert.That(seen.Arguments[1].SourceVariable, Is.SameAs(key));
        Assert.That(seen.Arguments[1].HasReconstructedSourceVariable, Is.False);
        Assert.That(sink.GetSnapshot().Count, Is.EqualTo(withSink ? 1 : 0));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WithSourceVariablePreservesSinkLegacyFlagAndUntouchedArgumentIdentity(bool legacy)
    {
        var sink = new SemanticDiagnosticSink();
        var key = new KeyVariableDeclaration();
        var originalKeyArgument = new AlgorithmCallArgument(key.Value, key, ResolvedCallArgumentKind.Variable);
        var input = new AlgorithmCallArguments(new[]
        {
            new AlgorithmCallArgument("#MECH:AES-ECB", kind: ResolvedCallArgumentKind.NestedFunctionCall),
            originalKeyArgument
        }, legacy, sink);
        var parameters = new ParameterVariableDeclaration { Mechanism = "AES-ECB" };
        var updated = input.WithSourceVariable(0, parameters);
        Assert.That(updated.DiagnosticSink, Is.SameAs(sink));
        Assert.That(updated.IsLegacyDeriveCall, Is.EqualTo(legacy));
        Assert.That(updated.Arguments[0].SourceVariable, Is.SameAs(parameters));
        Assert.That(updated.Arguments[0].Kind, Is.EqualTo(ResolvedCallArgumentKind.NestedFunctionCall));
        Assert.That(updated.Arguments[0].HasReconstructedSourceVariable, Is.True);
        Assert.That(updated.Arguments[1], Is.SameAs(originalKeyArgument));
        Assert.That(updated.Arguments[1].HasReconstructedSourceVariable, Is.False);
        Assert.That(input.Arguments[0].SourceVariable, Is.Null);
        Assert.That(input.Arguments[0].HasReconstructedSourceVariable, Is.False);
        Assert.Throws<ArgumentNullException>(() => input.WithSourceVariable(0, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => input.WithSourceVariable(-1, parameters));
        Assert.Throws<ArgumentOutOfRangeException>(() => input.WithSourceVariable(2, parameters));
    }

    [Test]
    public void WithSourceVariableRetainsAlreadyReconstructedNeighbor()
    {
        var sink = new SemanticDiagnosticSink();
        var neighbor = new AlgorithmCallArgument("key", new KeyVariableDeclaration(),
            ResolvedCallArgumentKind.Variable) { HasReconstructedSourceVariable = true };
        var input = new AlgorithmCallArguments(new[] { new AlgorithmCallArgument("#MECH:AES-ECB"), neighbor }, sink);
        var updated = input.WithSourceVariable(0, new ParameterVariableDeclaration());
        Assert.That(updated.Arguments[0].HasReconstructedSourceVariable, Is.True);
        Assert.That(updated.Arguments[1], Is.SameAs(neighbor));
        Assert.That(updated.Arguments[1].HasReconstructedSourceVariable, Is.True);
        Assert.That(input.Arguments[1].HasReconstructedSourceVariable, Is.True);
        Assert.That(updated.DiagnosticSink, Is.SameAs(sink));
    }

    [Test]
    public void DispatchReconstructionKeepsSinkAndMarksOnlyReconstructedParameter()
    {
        var sink = new SemanticDiagnosticSink();
        var key = new KeyVariableDeclaration();
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument("#MECH:AES-ECB#PAD:NONE", ResolvedCallArgumentKind.NestedFunctionCall),
            new ResolvedCallArgument(key.Value, ResolvedCallArgumentKind.Variable, key),
            new ResolvedCallArgument(Data, ResolvedCallArgumentKind.HexLiteral)
        }, sink);
        new CryptoOperations(_ => new ProbeAlgorithm(args =>
        {
            Assert.That(args.DiagnosticSink, Is.SameAs(sink));
            Assert.That(args.Arguments[0].SourceVariable, Is.TypeOf<ParameterVariableDeclaration>());
            Assert.That(args.Arguments[0].HasReconstructedSourceVariable, Is.True);
            Assert.That(args.Arguments[0].Kind, Is.EqualTo(ResolvedCallArgumentKind.NestedFunctionCall));
            Assert.That(args.Arguments[1].SourceVariable, Is.SameAs(key));
            Assert.That(args.Arguments[1].HasReconstructedSourceVariable, Is.False);
        })).Encrypt(invocation);
    }

    [Test]
    public void DirectAlgorithmAcceptsSinkWithoutGeneratingAutomaticWarnings()
    {
        Execute(new CryptoScriptRunner(), Prefix);
        var sink = new SemanticDiagnosticSink();
        var key = VariableDictionary.Instance().Get("k");
        var parameters = VariableDictionary.Instance().Get("p");
        var args = new AlgorithmCallArguments(new[]
        {
            new AlgorithmCallArgument(parameters.Value, parameters, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(key.Value, key, ResolvedCallArgumentKind.Variable),
            new AlgorithmCallArgument(Data, kind: ResolvedCallArgumentKind.HexLiteral)
        }, sink);
        var algorithm = AlgorithmFactory.Create("AES-ECB");
        Assert.That(algorithm.Encrypt(args).Value, Is.EqualTo("0x(69C4E0D86A7B0430D8CDB78070B4C55A)").IgnoreCase);
        Assert.That(algorithm.Encrypt(new AlgorithmCallArguments(args.Arguments)).Value,
            Is.EqualTo(algorithm.Encrypt(args).Value));
        Assert.That(sink.GetSnapshot(), Is.Empty);
        ((KeyVariableDeclaration)key).Usage = KeyUsagePolicy.Restricted(KeyUsage.Decrypt);
        Assert.That(algorithm.Encrypt(args).Value, Is.EqualTo("0x(69C4E0D86A7B0430D8CDB78070B4C55A)").IgnoreCase);
        Assert.That(sink.GetSnapshot().Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyUsageConflict));
    }

    [Test]
    public void LegacyAdaptersAndNormalRunnerStillHaveNoNewWarnings()
    {
        var runner = new CryptoScriptRunner();
        var program = Execute(runner, Prefix + $"VAR result=Encrypt(p,k,{Data})");
        Assert.That(program.Diagnostics, Is.Empty);
        Assert.That(runner.Diagnostics, Is.Empty);
        Assert.That(runner.SemanticErrors, Is.Empty);
        var algorithmArgs = AlgorithmCallArguments.FromValues(new[] { "a", "b" });
        Assert.That(algorithmArgs.DiagnosticSink, Is.Null);
        Assert.That(algorithmArgs.Values, Is.EqualTo(new[] { "a", "b" }));
        var legacy = LegacyDeriveArgumentAdapter.Create(new[] { "a" });
        Assert.That(legacy.DiagnosticSink, Is.Null);
        Assert.That(legacy.IsLegacyDeriveCall, Is.True);
        Assert.That(new OperationInvocation(Array.Empty<ResolvedCallArgument>()).DiagnosticSink, Is.Null);
        Assert.That(new CryptoOperations().Encrypt(new[]
            { VariableDictionary.Instance().Get("p").Value, VariableDictionary.Instance().Get("k").Value, Data }).Value,
            Is.EqualTo(VariableDictionary.Instance().Get("result").Value));
    }

    [Test]
    public void ScopedSinkDoesNotAcceptProducerSuppliedOperationIdentity()
    {
        var runner = new CryptoScriptRunner((call, sink) => sink.Report(new SemanticError
        {
            Severity = SemanticDiagnosticSeverity.Warning,
            DiagnosticCode = SemanticDiagnosticCode.KeyTypeConflict,
            Location = new DiagnosticLocation(Guid.NewGuid(), 99, 99, 98, "wrong", "AES-ECB", "k")
        }));
        var program = Execute(runner, "Compare(0x(AB),0x(AB))");
        var location = program.Diagnostics.Single().Location!;
        Assert.That(location.StatementIndex, Is.EqualTo(1));
        Assert.That(location.OperationId, Is.EqualTo(1));
        Assert.That(location.ParentOperationId, Is.Null);
        Assert.That(location.FunctionName, Is.EqualTo("Compare"));
        Assert.That(location.Mechanism, Is.EqualTo("AES-ECB"));
        Assert.That(location.Identifier, Is.EqualTo("k"));
    }

    [TestCase("Encrypt", "AES-ECB")]
    [TestCase("Decrypt", "AES-ECB")]
    [TestCase("Mac", "AES-CMAC")]
    [TestCase("Derive", "KDF-HKDF")]
    [TestCase("Wrap", "WRAP-AES-TR31")]
    [TestCase("Unwrap", "WRAP-AES-TR31")]
    public void AllSixFunctionsTransportSinkThroughActualNestedDispatch(string function, string mechanism)
    {
        var parameters = new ParameterVariableDeclaration { Id = "p", Mechanism = mechanism };
        var key = new KeyVariableDeclaration { Id = "k", KeyType = KeyType.Secret(KeyAlgorithm.Aes) };
        VariableDictionary.Instance().Add(parameters);
        VariableDictionary.Instance().Add(key);
        ISemanticDiagnosticSink? operationSink = null;
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning,
            DiagnosticCode = SemanticDiagnosticCode.KeyTypeConflict, Message = "dispatch warning" };
        int factoryCalls = 0;
        var probe = new ProbeAlgorithm(args =>
        {
            Assert.That(args.DiagnosticSink, Is.Not.Null.And.SameAs(operationSink));
            Assert.That(args.Arguments[0].SourceVariable, Is.SameAs(parameters));
            Assert.That(args.Arguments[1].SourceVariable, Is.SameAs(key));
            Assert.That(args.Arguments.Select(a => a.HasReconstructedSourceVariable), Is.All.False);
            Assert.That(args.Arguments.Select(a => a.Kind), Is.EqualTo(new[]
                { ResolvedCallArgumentKind.Variable, ResolvedCallArgumentKind.Variable, ResolvedCallArgumentKind.HexLiteral }));
            args.DiagnosticSink!.Report(warning);
        });
        var runner = new CryptoScriptRunner((call, sink) =>
        {
            if (call.Name == function)
                operationSink = sink;
        }, selected =>
        {
            Assert.That(selected, Is.EqualTo(mechanism));
            factoryCalls++;
            return probe;
        });
        var program = Execute(runner, $"Print({function}(p,k,{Data}))");
        Assert.That(factoryCalls, Is.EqualTo(1));
        Assert.That(probe.Calls, Is.EqualTo(new[] { function }));
        Assert.That(program.Diagnostics.Single(), Is.SameAs(warning));
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(warning));
        Assert.That(warning.Location, Is.EqualTo(new DiagnosticLocation(
            warning.Location!.ExecutionId, 1, 2, 1, function)));
        Assert.That(runner.SemanticErrors, Is.Empty);
    }

    [TestCase("Encrypt", "AES-ECB")]
    [TestCase("Decrypt", "AES-ECB")]
    [TestCase("Mac", "AES-CMAC")]
    [TestCase("Derive", "KDF-HKDF")]
    [TestCase("Wrap", "WRAP-AES-TR31")]
    [TestCase("Unwrap", "WRAP-AES-TR31")]
    public void AllSixFunctionsSupportActualDirectDispatchWithoutSink(string function, string mechanism)
    {
        var parameters = new ParameterVariableDeclaration { Mechanism = mechanism };
        var key = new KeyVariableDeclaration { KeyType = KeyType.Secret(KeyAlgorithm.Aes) };
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameters.Value, ResolvedCallArgumentKind.Variable, parameters),
            new ResolvedCallArgument(key.Value, ResolvedCallArgumentKind.Variable, key),
            new ResolvedCallArgument(Data, ResolvedCallArgumentKind.HexLiteral)
        });
        int calls = 0;
        var probe = new ProbeAlgorithm(args =>
        {
            calls++;
            Assert.That(args.DiagnosticSink, Is.Null);
            Assert.That(args.Arguments[0].SourceVariable, Is.SameAs(parameters));
            Assert.That(args.Arguments[1].SourceVariable, Is.SameAs(key));
            Assert.That(KeyMetadataValidator.Validate(key, KeyAlgorithm.Tdea, KeyUsage.Encrypt,
                function, mechanism, args.DiagnosticSink), Has.Count.EqualTo(1));
        });
        var operation = OperationFactory.CreateInvocationOperation(function, _ => probe);
        Assert.That(operation(invocation), Is.Not.Null);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(probe.Calls, Is.EqualTo(new[] { function }));
        Assert.That(new CryptoScriptRunner().Diagnostics, Is.Empty);
        Assert.That(new SemanticDiagnosticSink().GetSnapshot(), Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActualAlgorithmWarningThenSemanticExceptionPreservesExceptionIdentity(bool reportWarning)
    {
        Execute(new CryptoScriptRunner(), Prefix);
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning, Message = "before failure" };
        var error = new SemanticError { Type = "FunctionContract", FunctionName = "Encrypt",
            ErrorCode = FunctionContractError.UnsupportedFunction, Message = "controlled contract failure" };
        var expected = new SemanticErrorException { SemanticError = error };
        var probe = new ProbeAlgorithm(args =>
        {
            if (reportWarning) args.DiagnosticSink!.Report(warning);
            throw expected;
        });
        var runner = new CryptoScriptRunner(null, _ => probe);
        var thrown = Assert.Throws<SemanticErrorException>(() => Execute(runner,
            $"Encrypt(p,k,{Data}) VAR after=0x(AB)"));
        Assert.That(thrown, Is.SameAs(expected));
        Assert.That(thrown!.SemanticError, Is.SameAs(error));
        Assert.That(thrown.StackTrace, Does.Contain(nameof(ProbeAlgorithm.Encrypt)));
        Assert.That(probe.Calls, Is.EqualTo(new[] { "Encrypt" }));
        Assert.That(VariableDictionary.Instance().Contains("after"), Is.False);
        var diagnostics = runner.Diagnostics;
        Assert.That(diagnostics.Count, Is.EqualTo(reportWarning ? 2 : 1));
        if (reportWarning)
        {
            Assert.That(diagnostics[0], Is.SameAs(warning));
            Assert.That(warning.Location!.OperationId, Is.EqualTo(1));
        }
        Assert.That(diagnostics[^1], Is.SameAs(error));
        Assert.That(runner.SemanticErrors, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActualAlgorithmWarningDoesNotChangeRegularExceptionWrapping(bool reportWarning)
    {
        Execute(new CryptoScriptRunner(), Prefix);
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning, Message = "before failure" };
        var expected = new InvalidOperationException("controlled algorithm failure");
        var probe = new ProbeAlgorithm(args =>
        {
            if (reportWarning) args.DiagnosticSink!.Report(warning);
            throw expected;
        });
        var runner = new CryptoScriptRunner(null, _ => probe);
        var thrown = Assert.Throws<SemanticErrorException>(() => Execute(runner, $"Encrypt(p,k,{Data})"));
        // Regular algorithm failures still use the pre-existing evaluator conversion.
        Assert.That(thrown!.SemanticError!.Message, Is.EqualTo(expected.Message));
        Assert.That(thrown.SemanticError.Type, Is.EqualTo("FunctionCall"));
        Assert.That(runner.SemanticErrors.Single(), Is.SameAs(thrown.SemanticError));
        Assert.That(runner.Diagnostics.Count, Is.EqualTo(reportWarning ? 2 : 1));
        if (reportWarning) Assert.That(runner.Diagnostics[0], Is.SameAs(warning));
        Assert.That(runner.Diagnostics[^1], Is.SameAs(thrown.SemanticError));
    }

    [Test]
    public void ReusedWarningGetsNewMechanismAndDoesNotImportOldIdentifier()
    {
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning,
            Location = new DiagnosticLocation(Mechanism: "conflicting", Identifier: "old-key") };
        var companion = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning, Message = "companion" };
        bool firstExecution = true;
        var runner = new CryptoScriptRunner((call, sink) =>
        {
            if (call.Name == "Parameters")
            {
                sink.Report(warning);
                if (firstExecution) sink.Report(companion);
            }
        });
        var first = Execute(runner, "Parameters(AES-ECB)");
        firstExecution = false;
        var oldLocation = warning.Location!;
        var snapshot = first.Diagnostics;
        Assert.That(oldLocation.Mechanism, Is.EqualTo("AES-ECB"));
        Assert.That(oldLocation.Identifier, Is.EqualTo("old-key"));
        var second = Execute(runner, "Compare(0x(AB),0x(AB)) Parameters(DES3-ECB) Parameters(AES-ECB)");
        Assert.That(second.Diagnostics.Single(), Is.SameAs(warning));
        Assert.That(snapshot, Has.Count.EqualTo(2));
        Assert.That(snapshot[0], Is.SameAs(warning));
        Assert.That(snapshot[1], Is.SameAs(companion));
        Assert.That(companion.Location!.ExecutionId, Is.EqualTo(oldLocation.ExecutionId));
        Assert.That(warning.Location!.ExecutionId, Is.Not.EqualTo(oldLocation.ExecutionId));
        Assert.That(warning.Location.StatementIndex, Is.EqualTo(2));
        Assert.That(warning.Location.OperationId, Is.EqualTo(2));
        Assert.That(warning.Location.ParentOperationId, Is.Null);
        Assert.That(warning.Location.FunctionName, Is.EqualTo("Parameters"));
        Assert.That(warning.Location.Mechanism, Is.EqualTo("DES3-ECB"));
        Assert.That(warning.Location.Identifier, Is.Null);
        Assert.That(oldLocation.Mechanism, Is.EqualTo("AES-ECB"));
        Assert.That(oldLocation.Identifier, Is.EqualTo("old-key"));
        Assert.Throws<NotSupportedException>(() => ((IList<SemanticError>)snapshot).Clear());
    }

    [Test]
    public void ReusedWarningDoesNotImportAssignedMetadataWhenNewOperationLacksMechanism()
    {
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning,
            Location = new DiagnosticLocation(Identifier: "old-key") };
        var runner = new CryptoScriptRunner((_, sink) => sink.Report(warning));
        Execute(runner, "Parameters(AES-ECB)");
        var oldLocation = warning.Location!;
        Assert.That(oldLocation.Mechanism, Is.EqualTo("AES-ECB"));
        Assert.That(oldLocation.Identifier, Is.EqualTo("old-key"));
        var next = Execute(runner, "Compare(0x(AB),0x(AB))");
        Assert.That(next.Diagnostics.Single(), Is.SameAs(warning));
        Assert.That(warning.Location!.ExecutionId, Is.Not.EqualTo(oldLocation.ExecutionId));
        Assert.That(warning.Location.FunctionName, Is.EqualTo("Compare"));
        Assert.That(warning.Location.Mechanism, Is.Null);
        Assert.That(warning.Location.Identifier, Is.Null);
    }

    [Test]
    public void FreshExplicitLocationIsHonoredButCannotOverrideOperationContext()
    {
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning };
        var runner = new CryptoScriptRunner((call, sink) =>
        {
            warning.Location = new DiagnosticLocation(Guid.NewGuid(), 99, 99, 98,
                "wrong", "fresh-mechanism", "fresh-key");
            sink.Report(warning);
        });
        Execute(runner, "Parameters(AES-ECB)");
        Assert.That(warning.Location!.Mechanism, Is.EqualTo("AES-ECB"));
        Assert.That(warning.Location.Identifier, Is.EqualTo("fresh-key"));
        var oldId = warning.Location.ExecutionId;
        Execute(runner, "Compare(0x(AB),0x(AB))");
        Assert.That(warning.Location.ExecutionId, Is.Not.EqualTo(oldId));
        Assert.That(warning.Location.StatementIndex, Is.EqualTo(1));
        Assert.That(warning.Location.OperationId, Is.EqualTo(1));
        Assert.That(warning.Location.ParentOperationId, Is.Null);
        Assert.That(warning.Location.FunctionName, Is.EqualTo("Compare"));
        Assert.That(warning.Location.Mechanism, Is.EqualTo("fresh-mechanism"));
        Assert.That(warning.Location.Identifier, Is.EqualTo("fresh-key"));
    }

    [Test]
    public void FreshValueEqualLocationIsNotMistakenForPreviousExecutionAssignment()
    {
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning,
            Location = new DiagnosticLocation(Identifier: "explicit-key") };
        var runner = new CryptoScriptRunner((_, sink) => sink.Report(warning));
        Execute(runner, "Parameters(AES-ECB)");
        var oldLocation = warning.Location!;
        warning.Location = oldLocation with { };
        Assert.That(warning.Location, Is.EqualTo(oldLocation).And.Not.SameAs(oldLocation));
        Execute(runner, "Compare(0x(AB),0x(AB))");
        Assert.That(warning.Location!.ExecutionId, Is.Not.EqualTo(oldLocation.ExecutionId));
        Assert.That(warning.Location.Mechanism, Is.EqualTo("AES-ECB"));
        Assert.That(warning.Location.Identifier, Is.EqualTo("explicit-key"));
    }

    [Test]
    public void CurrentOperationIdentifierOverridesFreshDiagnosticIdentifierAndFirstReportWins()
    {
        var execution = new SemanticDiagnosticExecution();
        var warning = new SemanticError { Severity = SemanticDiagnosticSeverity.Warning,
            Location = new DiagnosticLocation(Mechanism: "other", Identifier: "producer-key") };
        var current = new DiagnosticLocation(execution.ExecutionId, 1, 1, null,
            "Encrypt", "AES-ECB", "operation-key");
        execution.CreateSink(current).Report(warning);
        var firstLocation = warning.Location;
        warning.Location = new DiagnosticLocation(Identifier: "changed");
        execution.CreateSink(current with { StatementIndex = 2, OperationId = 2 }).Report(warning);
        Assert.That(execution.GetSnapshot().Single(), Is.SameAs(warning));
        Assert.That(warning.Location, Is.SameAs(firstLocation));
        Assert.That(warning.Location, Is.EqualTo(current));
    }

    private sealed class ProbeAlgorithm(Action<AlgorithmCallArguments> inspect,
        CryptoScript.CryptoAlgorithm.CryptoAlgorithm? inner = null) : CryptoScript.CryptoAlgorithm.CryptoAlgorithm
    {
        public List<string> Calls { get; } = new();

        private void Inspect(string function, AlgorithmCallArguments args)
        {
            Calls.Add(function);
            inspect(args);
        }

        private static StringVariableDeclaration Result() => new()
            { Value = Data, Type = new CryptoTypeVar(), ValueFormat = FormatConversions.HEX };

        private static KeyVariableDeclaration KeyResult() => new()
            { Value = Data, Type = new CryptoTypeKey(), ValueFormat = FormatConversions.HEX };

        public override StringVariableDeclaration Encrypt(AlgorithmCallArguments parameters)
        {
            Inspect("Encrypt", parameters);
            return inner?.Encrypt(parameters) ?? Result();
        }

        public override StringVariableDeclaration Decrypt(AlgorithmCallArguments parameters)
        {
            Inspect("Decrypt", parameters);
            return inner?.Decrypt(parameters) ?? Result();
        }

        public override StringVariableDeclaration Mac(AlgorithmCallArguments parameters)
        {
            Inspect("Mac", parameters);
            return inner?.Mac(parameters) ?? Result();
        }

        public override KeyVariableDeclaration Derive(AlgorithmCallArguments parameters)
        {
            Inspect("Derive", parameters);
            return inner?.Derive(parameters) ?? KeyResult();
        }

        public override StringVariableDeclaration Wrap(AlgorithmCallArguments parameters)
        {
            Inspect("Wrap", parameters);
            return inner?.Wrap(parameters) ?? Result();
        }

        public override VariableDeclaration Unwrap(AlgorithmCallArguments parameters)
        {
            Inspect("Unwrap", parameters);
            return inner?.Unwrap(parameters) ?? KeyResult();
        }
    }
}
