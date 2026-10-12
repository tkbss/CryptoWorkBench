using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class SemanticDiagnosticExecutionTests
{
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

    private static FunctionCallExpressionNode Call(string name, params FunctionCallArgumentNode[] args) =>
        new(name, name + "(raw text)", args);

    private static FunctionCallStatementNode Compare() => new(Call("Compare",
        new LiteralArgumentNode("0x(AB)"), new LiteralArgumentNode("0x(AB)")));

    private static CryptoScriptProgram Execute(CryptoScriptRunner runner, string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var context = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
        return runner.Execute(context);
    }

    private static SemanticError Warning() => new()
    {
        Severity = SemanticDiagnosticSeverity.Warning,
        DiagnosticCode = SemanticDiagnosticCode.InvalidParameter,
        Message = "Controlled transport diagnostic"
    };

    [Test]
    public void DiagnosticsStartEmptyAndSuccessfulExecutionDoesNotInventWarnings()
    {
        var runner = new CryptoScriptRunner();
        Assert.That(runner.Diagnostics, Is.Empty);
        Assert.That(new CryptoScriptProgram().Diagnostics, Is.Empty);
        var program = runner.Execute(new[] { Compare() });
        Assert.That(program.Diagnostics, Is.Empty);
        Assert.That(runner.Diagnostics, Is.Empty);
        Assert.That(runner.SemanticErrors, Is.Empty);
        Assert.That(((FunctionCall)program.Statements[0]).Location!.OperationId, Is.EqualTo(1));
    }

    [Test]
    public void ExecutionsHaveIndependentSnapshotsAndContexts()
    {
        var runner = new CryptoScriptRunner((_, sink) => sink.Report(Warning()));
        var first = runner.Execute(new[] { Compare() });
        var firstDiagnostic = first.Diagnostics.Single();
        var firstSnapshot = runner.Diagnostics;
        var second = runner.Execute(new[] { Compare() });
        Assert.That(second.Diagnostics.Single(), Is.Not.SameAs(firstDiagnostic));
        Assert.That(second.Diagnostics[0].Location!.ExecutionId,
            Is.Not.EqualTo(firstDiagnostic.Location!.ExecutionId));
        Assert.That(second.Diagnostics[0].Location!.OperationId, Is.EqualTo(1));
        Assert.That(first.Diagnostics.Single(), Is.SameAs(firstDiagnostic));
        Assert.That(firstSnapshot.Single(), Is.SameAs(firstDiagnostic));
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(second.Diagnostics[0]));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SemanticError>)first.Diagnostics).Clear());
        firstDiagnostic.Message = "Updated diagnostic";
        Assert.That(first.Diagnostics[0].Message, Is.EqualTo("Updated diagnostic"));
    }

    [Test]
    public void DifferentRunnersNeverShareDiagnostics()
    {
        var first = new CryptoScriptRunner((_, sink) => sink.Report(Warning()));
        var second = new CryptoScriptRunner();
        first.Execute(new[] { Compare() });
        second.Execute(new[] { Compare() });
        Assert.That(first.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(second.Diagnostics, Is.Empty);
    }

    [Test]
    public void WarningDoesNotBlockResultsOrFollowingStatementsAndIsDeduplicated()
    {
        var warning = Warning();
        int count = 0;
        var runner = new CryptoScriptRunner((_, sink) =>
        {
            if (++count != 1) return;
            sink.Report(warning);
            sink.Report(warning);
        });
        var originalErrors = runner.SemanticErrors;
        var program = Execute(runner,
            "VAR first=Compare(0x(AB),0x(AB)) VAR second=Compare(0x(CD),0x(CD))");
        Assert.That(program.Statements, Has.Count.EqualTo(2));
        Assert.That(VariableDictionary.Instance().Get("second").Value, Is.EqualTo("Values are equal"));
        Assert.That(program.Diagnostics.Single(), Is.SameAs(warning));
        Assert.That(warning.Location!.StatementIndex, Is.EqualTo(1));
        Assert.That(warning.Location.OperationId, Is.EqualTo(1));
        Assert.That(warning.Location.ParentOperationId, Is.Null);
        Assert.That(warning.Location.FunctionName, Is.EqualTo("Compare"));
        Assert.That(runner.SemanticErrors, Is.SameAs(originalErrors).And.Empty);
    }

    [Test]
    public void NestedContextsAreAllocatedBeforeArgumentsAndPreserveSiblingParents()
    {
        var calls = new List<FunctionCall>();
        var runner = new CryptoScriptRunner((call, sink) =>
        {
            calls.Add(call);
            sink.Report(Warning());
        });
        var outer = Call("Compare",
            new NestedCallArgumentNode(Compare().Call), new NestedCallArgumentNode(Compare().Call));
        var program = runner.Execute(new StatementNode?[] { null, new FunctionCallStatementNode(outer) });
        Assert.That(calls.Select(call => call.Location!.OperationId), Is.EqualTo(new[] { 2, 3, 1 }));
        Assert.That(calls.Select(call => call.Location!.ParentOperationId),
            Is.EqualTo(new int?[] { 1, 1, null }));
        Assert.That(program.Diagnostics.Select(error => error.Location!.StatementIndex), Is.All.EqualTo(2));
        Assert.That(program.Diagnostics.Select(error => error.Location!.ExecutionId).Distinct().Count(), Is.EqualTo(1));
        Assert.That(program.Diagnostics.Select(error => error.Location!.OperationId), Is.EqualTo(new[] { 2, 3, 1 }));
        Assert.That(calls[2].Invocation!.Arguments.Select(arg => arg.Kind),
            Is.All.EqualTo(ResolvedCallArgumentKind.NestedFunctionCall));
        Assert.That(calls[2].Invocation!.Arguments.Select(arg => arg.SourceVariable), Is.All.Null);
    }

    [Test]
    public void DirectMechanismIsAssociatedWithoutCopyingArgumentValues()
    {
        var runner = new CryptoScriptRunner((_, sink) => sink.Report(Warning()));
        var program = Execute(runner, "PARAM p=Parameters(AES-ECB)");
        var location = program.Diagnostics.Single().Location!;
        Assert.That(location.Mechanism, Is.EqualTo("AES-ECB"));
        Assert.That(location.FunctionName, Is.EqualTo("Parameters"));
        Assert.That(location.Identifier, Is.Null);
    }

    [Test]
    public void ExistingErrorListEntriesAreExcludedAndFailuresAccumulateOnlyInLegacyList()
    {
        var oldError = new SemanticError { Message = "Already present" };
        var list = new List<SemanticError> { oldError };
        var runner = new CryptoScriptRunner { SemanticErrors = list };
        var first = Assert.Throws<SemanticErrorException>(() =>
            runner.Execute(new[] { new FunctionCallStatementNode(Call("Unknown")) }))!;
        var firstSnapshot = runner.Diagnostics;
        Assert.That(firstSnapshot.Single(), Is.SameAs(first.SemanticError));
        Assert.That(oldError.Location, Is.Null);
        Assert.That(oldError.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.Unclassified));
        var second = Assert.Throws<SemanticErrorException>(() =>
            runner.Execute(new[] { new FunctionCallStatementNode(Call("OtherUnknown")) }))!;
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(second.SemanticError));
        Assert.That(firstSnapshot.Single(), Is.SameAs(first.SemanticError));
        Assert.That(runner.SemanticErrors, Is.SameAs(list));
        Assert.That(list, Has.Count.EqualTo(3));
        Assert.That(list[0], Is.SameAs(oldError));
        Assert.That(list[1], Is.SameAs(first.SemanticError));
        Assert.That(list[2], Is.SameAs(second.SemanticError));
        runner.Execute(Array.Empty<StatementNode?>());
        Assert.That(runner.Diagnostics, Is.Empty);
        Assert.That(list, Has.Count.EqualTo(3));
    }

    [Test]
    public void NestedLegacyErrorsRemainDistinctOrderedAndIdenticalToListObjects()
    {
        var inner = Call("Compare", new VariableArgumentNode("missing"));
        var outer = Call("Print", new NestedCallArgumentNode(inner));
        var runner = new CryptoScriptRunner();
        var thrown = Assert.Throws<SemanticErrorException>(() =>
            runner.Execute(new[] { new FunctionCallStatementNode(outer) }))!;
        Assert.That(runner.SemanticErrors.Select(error => error.Type),
            Is.EqualTo(new[] { "Variable", "FunctionCall", "FunctionCall" }));
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(3));
        for (int i = 0; i < 3; i++)
            Assert.That(runner.Diagnostics[i], Is.SameAs(runner.SemanticErrors[i]));
        Assert.That(runner.Diagnostics.Distinct(ReferenceEqualityComparer.Instance).Count(), Is.EqualTo(3));
        Assert.That(thrown.SemanticError, Is.SameAs(runner.Diagnostics[2]));
        Assert.That(runner.Diagnostics.Select(error => error.Location!.OperationId), Is.EqualTo(new[] { 2, 2, 1 }));
        Assert.That(runner.Diagnostics.Select(error => error.Location!.ParentOperationId),
            Is.EqualTo(new int?[] { 1, 1, null }));
        Assert.That(runner.Diagnostics[0].DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.UnknownVariable));
    }

    [Test]
    public void SensitiveNestedErrorIsNotDuplicatedOrUnredacted()
    {
        var inner = Call("Wrap", new LiteralArgumentNode("0x(00112233445566778899AABBCCDDEEFF)"),
            new LiteralArgumentNode("0x(9876)"));
        var runner = new CryptoScriptRunner();
        var thrown = Assert.Throws<SemanticErrorException>(() => runner.Execute(new[]
        {
            new FunctionCallStatementNode(Call("Print", new NestedCallArgumentNode(inner)))
        }))!;
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(thrown.SemanticError));
        Assert.That(runner.SemanticErrors.Single(), Is.SameAs(thrown.SemanticError));
        Assert.That(thrown.SemanticError!.FunctionCall, Is.EqualTo("Wrap(<redacted>)"));
        Assert.That(thrown.SemanticError.Message, Does.Not.Contain("9876"));
        Assert.That(thrown.SemanticError.Location!.OperationId, Is.EqualTo(2));
        Assert.That(thrown.SemanticError.Location.ParentOperationId, Is.EqualTo(1));
        Assert.That(thrown.SemanticError.Location.Identifier, Is.Null);
    }

    [Test]
    public void DeclarationErrorsHaveStatementContextWithoutInventingAnOperation()
    {
        var runner = new CryptoScriptRunner();
        var thrown = Assert.Throws<SemanticErrorException>(() => Execute(runner,
            "VAR first=0x(AB) KEY wrong=Compare(0x(AB),0x(AB))"))!;
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(thrown.SemanticError));
        Assert.That(thrown.SemanticError!.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.InvalidDeclaration));
        Assert.That(thrown.SemanticError.Location!.StatementIndex, Is.EqualTo(2));
        Assert.That(thrown.SemanticError.Location.OperationId, Is.Null);
    }

    [Test]
    public void WarningAndErrorsRemainAccessibleAfterAbort()
    {
        var warning = Warning();
        var runner = new CryptoScriptRunner((_, sink) => sink.Report(warning));
        var thrown = Assert.Throws<SemanticErrorException>(() => runner.Execute(new[]
        {
            Compare(), new FunctionCallStatementNode(Call("Unknown")), Compare()
        }))!;
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(2));
        Assert.That(runner.Diagnostics[0], Is.SameAs(warning));
        Assert.That(runner.Diagnostics[1], Is.SameAs(thrown.SemanticError));
        Assert.That(thrown.SemanticError!.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.UnknownFunction));
        Assert.That(thrown.SemanticError.Location!.StatementIndex, Is.EqualTo(2));
        Assert.That(runner.SemanticErrors.Single(), Is.SameAs(thrown.SemanticError));
    }

    [Test]
    public void StatementsRemainLazyAndEnumerationStopsImmediatelyAfterError()
    {
        int completed = 0;
        bool disposed = false;
        var runner = new CryptoScriptRunner((_, sink) => { completed++; sink.Report(Warning()); });
        IEnumerable<StatementNode?> Statements()
        {
            try
            {
                Assert.That(completed, Is.Zero);
                yield return Compare();
                Assert.That(completed, Is.EqualTo(1));
                yield return new FunctionCallStatementNode(Call("Unknown"));
                Assert.Fail("Must not enumerate after the blocking error");
            }
            finally { disposed = true; }
        }
        Assert.Throws<SemanticErrorException>(() => runner.Execute(Statements()));
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(disposed, Is.True);
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(2));
    }

    [Test]
    public void EnumerationRuntimeExceptionIsNotConvertedToWarning()
    {
        var expected = new InvalidOperationException("Controlled enumeration failure");
        var runner = new CryptoScriptRunner((_, sink) => sink.Report(Warning()));
        IEnumerable<StatementNode?> Statements()
        {
            yield return Compare();
            throw expected;
        }
        Assert.That(Assert.Throws<InvalidOperationException>(() => runner.Execute(Statements())), Is.SameAs(expected));
        Assert.That(runner.Diagnostics.Single().Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
        Assert.That(runner.SemanticErrors, Is.Empty);
        IEnumerable<StatementNode?> FailsBeforeFirstStatement()
        {
            foreach (var node in Array.Empty<StatementNode?>())
                yield return node;
            throw expected;
        }
        Assert.Throws<InvalidOperationException>(() => runner.Execute(FailsBeforeFirstStatement()));
        Assert.That(runner.Diagnostics, Is.Empty);
    }

    [Test]
    public void ErrorListBindingRemainsTheOneCapturedAtExecutionStart()
    {
        var replacement = new List<SemanticError>();
        CryptoScriptRunner runner = null!;
        runner = new CryptoScriptRunner((_, _) => runner.SemanticErrors = replacement);
        var original = runner.SemanticErrors;
        Assert.Throws<SemanticErrorException>(() => runner.Execute(new[]
        {
            Compare(), new FunctionCallStatementNode(Call("Unknown"))
        }));
        Assert.That(runner.SemanticErrors, Is.SameAs(replacement).And.Empty);
        Assert.That(original.Single(), Is.SameAs(runner.Diagnostics.Single()));
    }

    [Test]
    public void PublicEvaluatorOverloadsRemainUsableWithoutDiagnosticContext()
    {
        var errors = new List<SemanticError>();
        var result = (FunctionCall)StatementEvaluator.Evaluate(Compare(), errors)!;
        Assert.That(result.Location, Is.Null);
        var thrown = Assert.Throws<SemanticErrorException>(() =>
            FunctionCallEvaluator.EvaluateFunctionCall(Call("Unknown"), errors))!;
        Assert.That(errors.Single(), Is.SameAs(thrown.SemanticError));
        Assert.That(thrown.SemanticError!.Location, Is.Null);
        Assert.That(thrown.SemanticError.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.Unclassified));
    }

    [Test]
    public void UnknownFunctionStopsBeforeNestedArgumentEvaluation()
    {
        var runner = new CryptoScriptRunner((_, _) => Assert.Fail("Arguments must not execute"));
        Assert.Throws<SemanticErrorException>(() => runner.Execute(new[]
        {
            new FunctionCallStatementNode(Call("Unknown", new NestedCallArgumentNode(Compare().Call)))
        }));
        Assert.That(runner.Diagnostics.Single().Location!.OperationId, Is.EqualTo(1));
        Assert.That(runner.Diagnostics[0].DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.UnknownFunction));
    }

    [Test]
    public void NestedFunctionContractErrorKeepsItsIdentityAndBlockingSemantics()
    {
        var runner = new CryptoScriptRunner();
        var thrown = Assert.Throws<SemanticErrorException>(() => Execute(runner,
            "KEY k=GenerateKey(AES-CBC,0x(000102030405060708090A0B0C0D0E0F)) " +
            "PARAM p=#MECH:AES-CBC #PAD:NONE " +
            "Print(Encrypt(p,k,0x(00112233445566778899AABBCCDDEEFF)))"))!;
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(thrown.SemanticError));
        Assert.That(runner.SemanticErrors.Single(), Is.SameAs(thrown.SemanticError));
        Assert.That(thrown.SemanticError!.ErrorCode, Is.Not.Null);
        Assert.That(thrown.SemanticError.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.FunctionContractViolation));
        Assert.That(thrown.SemanticError.Location!.StatementIndex, Is.EqualTo(3));
        Assert.That(thrown.SemanticError.Location.OperationId, Is.EqualTo(3));
        Assert.That(thrown.SemanticError.Location.ParentOperationId, Is.EqualTo(2));
    }

    [Test]
    public void OperationRuntimeFailureRemainsBlockingAfterReportingWarning()
    {
        int calls = 0;
        var warning = Warning();
        var runner = new CryptoScriptRunner((_, sink) =>
        {
            calls++;
            sink.Report(warning);
            throw new InvalidOperationException("Controlled operation failure");
        });
        var thrown = Assert.Throws<SemanticErrorException>(() => runner.Execute(new[] { Compare(), Compare() }))!;
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(runner.Diagnostics, Has.Count.EqualTo(2));
        Assert.That(runner.Diagnostics[0], Is.SameAs(warning));
        Assert.That(runner.Diagnostics[1], Is.SameAs(thrown.SemanticError));
        Assert.That(thrown.SemanticError!.Severity, Is.EqualTo(SemanticDiagnosticSeverity.Error));
        Assert.That(thrown.SemanticError.Message, Is.EqualTo("Controlled operation failure"));
    }

    [Test]
    public void EmvStillRejectsNestedAtcWithoutLosingArgumentKind()
    {
        FunctionCall? derive = null;
        var runner = new CryptoScriptRunner((call, _) =>
        {
            if (call.Name == "Derive") derive = call;
        });
        var thrown = Assert.Throws<SemanticErrorException>(() => Execute(runner,
            "KEY dk=GenerateKey(AES-CBC,0x(000102030405060708090A0B0C0D0E0F)) " +
            "PARAM dp=Parameters(#MECH:AES-CBC,#IV:0x(00000000000000000000000000000000),#PAD:PKCS-7) " +
            "KEY master=GenerateKey(AES-ECB,0x(2EF6E07ECBA86BCF3C3CFF7BBEBE6F38)) " +
            "PARAM p=Parameters(KDF-EMV-AC-SESSION) " +
            "KEY output=Derive(p,master,Decrypt(dp,dk,0x(A72846C84C7F9B9BB4104ADFBBCAFFF5)))"))!;
        Assert.That(derive!.Invocation!.Arguments[2].Kind, Is.EqualTo(ResolvedCallArgumentKind.NestedFunctionCall));
        Assert.That(derive.Invocation.Arguments[2].SourceVariable, Is.Null);
        Assert.That(thrown.SemanticError!.Message,
            Does.Contain("hexadecimal literal or a VAR containing hexadecimal binary data"));
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(thrown.SemanticError));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmvDerivationRetainsSourceKeyIdentityAndAtcArgumentKinds(bool variableAtc)
    {
        FunctionCall? derive = null;
        var runner = new CryptoScriptRunner((call, _) =>
        {
            if (call.Name == "Derive") derive = call;
        });
        string atc = variableAtc ? "atc" : "0x(0001)";
        var program = Execute(runner,
            "KEY master=GenerateKey(AES-ECB,0x(2EF6E07ECBA86BCF3C3CFF7BBEBE6F38)) " +
            "PARAM p=Parameters(KDF-EMV-AC-SESSION) " +
            (variableAtc ? "VAR atc=0x(0001) " : "") + $"KEY output=Derive(p,master,{atc})");
        Assert.That(derive, Is.Not.Null);
        Assert.That(derive!.Invocation!.Arguments[1].SourceVariable,
            Is.SameAs(VariableDictionary.Instance().Get("master")));
        Assert.That(derive.Invocation.Arguments[1].Kind, Is.EqualTo(ResolvedCallArgumentKind.Variable));
        Assert.That(derive.Invocation.Arguments[2].Kind, Is.EqualTo(variableAtc
            ? ResolvedCallArgumentKind.Variable : ResolvedCallArgumentKind.HexLiteral));
        Assert.That(VariableDictionary.Instance().Get("output").Value,
            Is.EqualTo("0x(89F7B697A028A93345BE7A409665B9A4)").IgnoreCase);
        Assert.That(program.Diagnostics, Is.Empty);
        Assert.That(runner.SemanticErrors, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    [Category("DiagnosticIsolationRegression")]
    public void ReentrantPrintFailureExcludesForeignErrorsAndRestoresOuterContext(bool abortOuter)
    {
        var runner = new CryptoScriptRunner((_, sink) => sink.Report(Warning()));
        var existing = new SemanticError { Message = "Already present" };
        var legacy = runner.SemanticErrors;
        legacy.Add(existing);
        IReadOnlyList<SemanticError>? innerSnapshot = null;
        IReadOnlyList<SemanticError>? restoredSnapshot = null;
        SemanticError? innerError = null;
        void OnPrint(string _)
        {
            IEnumerable<StatementNode?> InnerStatements()
            {
                try
                {
                    yield return Compare();
                    yield return new FunctionCallStatementNode(Call("Unknown"));
                }
                finally { innerSnapshot = runner.Diagnostics; }
            }
            try { runner.Execute(InnerStatements()); }
            catch (SemanticErrorException exception) { innerError = exception.SemanticError; }
            restoredSnapshot = runner.Diagnostics;
        }

        CryptoScriptProgram? outerProgram = null;
        SemanticErrorException? outerException = null;
        OutputOperations.PrintEvent += OnPrint;
        try
        {
            var statements = new[]
            {
                new FunctionCallStatementNode(Call("Print", new LiteralArgumentNode("\"outer\""))),
                abortOuter ? new FunctionCallStatementNode(Call("OuterUnknown")) : Compare()
            };
            if (abortOuter)
                outerException = Assert.Throws<SemanticErrorException>(() => runner.Execute(statements));
            else
                outerProgram = runner.Execute(statements);
        }
        finally { OutputOperations.PrintEvent -= OnPrint; }

        Assert.That(innerSnapshot, Has.Count.EqualTo(2));
        Assert.That(innerSnapshot![1], Is.SameAs(innerError));
        Assert.That(innerSnapshot[0].Location!.ExecutionId, Is.EqualTo(innerError!.Location!.ExecutionId));
        Assert.That(restoredSnapshot, Has.Count.EqualTo(1));
        var outerId = restoredSnapshot![0].Location!.ExecutionId;
        Assert.That(outerId, Is.Not.EqualTo(innerError.Location.ExecutionId));
        var outerSnapshot = runner.Diagnostics;
        Assert.That(outerSnapshot, Has.Count.EqualTo(2));
        Assert.That(outerSnapshot[0], Is.SameAs(restoredSnapshot[0]));
        Assert.That(outerSnapshot.Select(error => error.Location!.ExecutionId), Is.All.EqualTo(outerId));
        Assert.That(outerSnapshot.Any(error => ReferenceEquals(error, innerError)), Is.False);
        Assert.That(runner.SemanticErrors, Is.SameAs(legacy));
        Assert.That(legacy, Has.Count.EqualTo(abortOuter ? 3 : 2));
        Assert.That(legacy[0], Is.SameAs(existing));
        Assert.That(legacy[1], Is.SameAs(innerError));
        Assert.That(existing.Location, Is.Null);
        if (abortOuter)
        {
            Assert.That(outerSnapshot[1], Is.SameAs(outerException!.SemanticError));
            Assert.That(legacy[2], Is.SameAs(outerException.SemanticError));
        }
        else
        {
            Assert.That(outerProgram!.Statements, Has.Count.EqualTo(2));
            Assert.That(outerProgram.Diagnostics[0], Is.SameAs(outerSnapshot[0]));
            Assert.That(outerProgram.Diagnostics[1], Is.SameAs(outerSnapshot[1]));
        }
    }

    [Test]
    [Category("DiagnosticIsolationRegression")]
    public void ThreeReentrantExecutionsKeepOwnWarningsAndRestoreEachParent()
    {
        CryptoScriptRunner runner = null!;
        var activeContextsMatch = new List<bool>();
        runner = new CryptoScriptRunner((call, sink) =>
        {
            sink.Report(Warning());
            activeContextsMatch.Add(runner.Diagnostics.Single().Location!.ExecutionId == call.Location!.ExecutionId);
        });
        int depth = 0;
        var children = new List<CryptoScriptProgram>();
        var restorations = new List<(SemanticError Expected, SemanticError Actual)>();
        void OnPrint(string _)
        {
            var parentWarning = runner.Diagnostics.Single();
            depth++;
            try
            {
                var statement = depth == 1
                    ? new FunctionCallStatementNode(Call("Print", new LiteralArgumentNode("\"middle\"")))
                    : Compare();
                children.Add(runner.Execute(new[] { statement }));
                restorations.Add((parentWarning, runner.Diagnostics.Single()));
            }
            finally { depth--; }
        }

        CryptoScriptProgram outer;
        OutputOperations.PrintEvent += OnPrint;
        try
        {
            outer = runner.Execute(new[]
            {
                new FunctionCallStatementNode(Call("Print", new LiteralArgumentNode("\"outer\"")))
            });
        }
        finally { OutputOperations.PrintEvent -= OnPrint; }

        Assert.That(activeContextsMatch, Has.Count.EqualTo(3));
        Assert.That(activeContextsMatch, Is.All.True);
        Assert.That(children, Has.Count.EqualTo(2));
        foreach (var child in children)
            Assert.That(child.Diagnostics, Has.Count.EqualTo(1));
        foreach (var pair in restorations)
            Assert.That(pair.Actual, Is.SameAs(pair.Expected));
        var contexts = children.Select(child => child.Diagnostics.Single().Location!.ExecutionId)
            .Append(outer.Diagnostics.Single().Location!.ExecutionId);
        Assert.That(contexts.Distinct().Count(), Is.EqualTo(3));
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(outer.Diagnostics.Single()));
        Assert.That(runner.SemanticErrors, Is.Empty);
        var later = runner.Execute(new[] { Compare() });
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(later.Diagnostics.Single()));
        Assert.That(later.Diagnostics[0].Location!.ExecutionId,
            Is.Not.EqualTo(outer.Diagnostics[0].Location!.ExecutionId));
        Assert.That(outer.Diagnostics, Has.Count.EqualTo(1));
    }

    [Test]
    [Category("DiagnosticIsolationRegression")]
    public void ExplicitWarningReuseUpdatesOldSnapshotsButKeepsFirstLocationWithinEachExecution()
    {
        var warning = Warning();
        var runner = new CryptoScriptRunner((_, sink) => sink.Report(warning));
        var first = runner.Execute(new StatementNode?[] { null, Compare(), Compare() });
        var originalLocation = warning.Location!;
        Assert.That(first.Diagnostics.Single(), Is.SameAs(warning));
        Assert.That(originalLocation.StatementIndex, Is.EqualTo(2));
        Assert.That(originalLocation.OperationId, Is.EqualTo(1));

        var second = runner.Execute(new[] { Compare(), Compare() });
        Assert.That(second.Diagnostics.Single(), Is.SameAs(warning));
        Assert.That(warning.Location!.ExecutionId, Is.Not.EqualTo(originalLocation.ExecutionId));
        Assert.That(warning.Location.StatementIndex, Is.EqualTo(1));
        Assert.That(warning.Location.OperationId, Is.EqualTo(1));
        Assert.That(first.Diagnostics.Single(), Is.SameAs(warning));
        Assert.That(first.Diagnostics[0].Location, Is.SameAs(second.Diagnostics[0].Location));
        Assert.That(originalLocation.StatementIndex, Is.EqualTo(2));
        Assert.That(runner.SemanticErrors, Is.Empty);
    }

    [Test]
    [Category("DiagnosticIsolationRegression")]
    public void ForeignContractExceptionIsNotRelabelledOrAddedByEitherEvaluatorBoundary()
    {
        var foreignLocation = new DiagnosticLocation(Guid.NewGuid(), 7, 8, FunctionName: "Encrypt");
        var foreignError = new SemanticError
        {
            Type = "FunctionContract",
            ErrorCode = FunctionContractError.MissingRequiredParameter,
            Location = foreignLocation
        };
        var expected = new SemanticErrorException { SemanticError = foreignError };
        var warning = Warning();
        CryptoScriptRunner runner = null!;
        runner = new CryptoScriptRunner((_, sink) =>
        {
            sink.Report(warning);
            runner.SemanticErrors.Add(foreignError);
            throw expected;
        });
        var legacy = runner.SemanticErrors;
        var thrown = Assert.Throws<SemanticErrorException>(() => runner.Execute(new[] { Compare() }));
        Assert.That(thrown, Is.SameAs(expected));
        Assert.That(thrown!.SemanticError, Is.SameAs(foreignError));
        Assert.That(foreignError.Location, Is.SameAs(foreignLocation));
        Assert.That(foreignError.DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.Unclassified));
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(warning));
        Assert.That(runner.SemanticErrors, Is.SameAs(legacy));
        Assert.That(legacy.Single(), Is.SameAs(foreignError));
    }

    [TestCase(false)]
    [TestCase(true)]
    [Category("DiagnosticIsolationRegression")]
    public void ReentrantEnumerationExceptionRestoresOuterContextEvenBeforeFirstStatement(bool afterStatement)
    {
        var runner = new CryptoScriptRunner((_, sink) => sink.Report(Warning()));
        var expected = new InvalidOperationException("Controlled inner enumeration failure");
        Exception? observed = null;
        SemanticError? restored = null;
        void OnPrint(string _)
        {
            IEnumerable<StatementNode?> InnerStatements()
            {
                if (afterStatement) yield return Compare();
                throw expected;
            }
            try { runner.Execute(InnerStatements()); }
            catch (InvalidOperationException exception) { observed = exception; }
            restored = runner.Diagnostics.Single();
        }

        CryptoScriptProgram outer;
        OutputOperations.PrintEvent += OnPrint;
        try
        {
            outer = runner.Execute(new[]
            {
                new FunctionCallStatementNode(Call("Print", new LiteralArgumentNode("\"outer\"")))
            });
        }
        finally { OutputOperations.PrintEvent -= OnPrint; }
        Assert.That(observed, Is.SameAs(expected));
        Assert.That(outer.Diagnostics.Single(), Is.SameAs(restored));
        Assert.That(runner.Diagnostics.Single(), Is.SameAs(restored));
        Assert.That(runner.SemanticErrors, Is.Empty);
    }
}
