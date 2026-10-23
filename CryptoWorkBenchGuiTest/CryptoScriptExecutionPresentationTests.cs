using System.Reflection;
using System.Runtime.CompilerServices;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;
using NUnit.Framework;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using AvaloniaEdit;
using CryptoWorkBenchAvalonia;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class CryptoScriptExecutionPresentationTests
{
    private StatusViewModel status = null!;
    private VariableViewModel variables = null!;
    private CryptoScriptEditViewModel editor = null!;
    private VariableDeclaration[] previous = null!;
    private bool previousSyntaxFlag;
    private System.Text.StringBuilder previousSyntaxMessage = null!;
    private SyntaxError[] previousSyntaxErrors = null!;

    [SetUp]
    public void SetUp()
    {
        previous = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
        previousSyntaxFlag = SyntaxErrorListner.SyntaxErrorOccured;
        previousSyntaxMessage = SyntaxErrorListner.ErrorMessage;
        previousSyntaxErrors = SyntaxErrorList.Instance().ToArray();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        SyntaxErrorListner.ErrorMessage = new();
        status = new();
        variables = new();
        editor = new(status, variables, new InfoViewModel(), new MainViewModel(), null!, new HistoryService());
    }

    [TearDown]
    public void TearDown()
    {
        editor.Dispose();
        OutputOperations.InfoEvent -= Handler(editor, "OnInfoEvent");
        VariableDictionary.Instance().Clear();
        foreach (var variable in previous) VariableDictionary.Instance().Add(variable);
        SyntaxErrorListner.SyntaxErrorOccured = previousSyntaxFlag;
        SyntaxErrorListner.ErrorMessage = previousSyntaxMessage;
        SyntaxErrorList.Instance().Clear();
        SyntaxErrorList.Instance().AddRange(previousSyntaxErrors);
    }

    internal static Action<string> Handler(object target, string name) =>
        (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), target,
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!);

    internal static CryptoScriptRunner ControlledRunner(Action<FunctionCall, ISemanticDiagnosticSink> action) =>
        (CryptoScriptRunner)Activator.CreateInstance(typeof(CryptoScriptRunner),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { action }, null)!;

    private Task<bool> ExecuteLine(string script, CryptoScriptRunner? runner = null) =>
        editor.ExecuteLineAsync(script, runner: runner);

    private async Task<List<string>> ExecuteBook(string[] lines, Func<CryptoScriptRunner>? factory = null)
    {
        status.ClearDiagnostics();
        var appended = new List<string>();
        await editor.ExecuteScriptBookAsync(lines, appended.Add, runnerFactory: factory);
        return appended;
    }
    private static SemanticError Warning(string message) => new()
    {
        Severity = SemanticDiagnosticSeverity.Warning,
        Message = message
    };

    [Test]
    public async Task CheckErrorsContinueAndKeyPrintReachesExistingGuiOutput()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            await editor.ParseLineAsync("KEY missing=Check(0x(AA)) VAR continued=0x(01)");
            Assert.That(status.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(status.Diagnostics[0].Text, Does.Contain("KeyCheckValueNotFound"));
            Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Contain("continued"));
            Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Not.Contain("missing"));
            await editor.ParseLineAsync("KEY key=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F)) Print(key)");
            Assert.That(editor.PrintMessage, Does.Contain("KCV: 0x(c6a13b37878f5b826f4f8162a1c8d879)"));
            Assert.That(editor.PrintMessage, Does.Contain("KCVC: 0x(be7ed6ae787dd5544c5561c7dcd06abb)"));
            Assert.That(editor.PrintMessage, Does.Contain("KCVK: 0x(0a940bb5416ef045f1c39458c653ea5a)"));

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task SecretPrintUsesExistingGuiEventAndScriptBookOutputPaths()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            await ExecuteBook([
                "KEY k=GenerateKey(AES-ECB,128)",
            "PARAM p=Parameters(SECRET_SHAMIR,#PARTS:3,#MINPARTS:2,#SHAREFORMAT:\"Y-X\")",
            "SECRET s=Share(p,k)", "KEY r=Restore(s)", "Print(s)"
            ]);
            Assert.That(status.Diagnostics, Is.Empty);
            Assert.That(editor.LastScriptBookSucceeded, Is.True);
            Assert.That(editor.PrintMessage, Does.Contain("ShareFormat: Y-X").And.Contain("AvailableShares: 3"));
            Assert.That(variables.DataVariables.Any(v => v.Identifier == "s" && v.Type == "SECRET"), Is.True);
            var set = ((SecretVariableDeclaration)VariableDictionary.Instance().Get("s")).SecretSet;
            foreach (var share in set.Shares)
                Assert.That(editor.PrintMessage, Does.Contain(Convert.ToHexString(share.Value)));
            Assert.That(editor.PrintMessage, Does.Contain("out: SECRET importedSecret = Secret("));
            await ExecuteBook(editor.PrintMessage.Split(Environment.NewLine).Append("KEY replay=Restore(s)").ToArray());
            Assert.That(editor.LastScriptBookSucceeded, Is.True);
            Assert.That(VariableDictionary.Instance().Contains("replay"), Is.True);
            Assert.That(VariableDictionary.Instance().Get("r").Value,
                Is.EqualTo(VariableDictionary.Instance().Get("k").Value).IgnoreCase);

            return true;
        }, CancellationToken.None);
    }

    [TestCase("0x(01AABBCC) 0x(02DDEEFF)")]
    [TestCase("0x(AABBCC01) 0x(DDEEFF02)")]
    [TestCase("1,0x(AABBCC),2 0x(DDEEFF)")]
    [TestCase("0x(01AABBCC),0x(02DDEEFFGG)")]
    [TestCase("0x(01AABBCC),0x(02DDEEFF@)")]
    [TestCase("0x(01AABBCC),0x(02DDEEFF")]
    public async Task SecretImportSyntaxErrorsReachGuiWithoutShareMaterial(string arguments)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            await editor.ParseLineAsync("SECRET s=Secret(p," + arguments + ")");
            Assert.That(status.StatusString, Does.Contain("Secret import").And.Contain("line 1").And.Contain("column"));
            Assert.That(status.StatusString, Does.Not.Contain("AABBCC").And.Not.Contain("DDEEFF"));
            Assert.That(status.Diagnostics, Is.Empty); // No semantic evaluation after lexical/syntax failure.
            Assert.That(VariableDictionary.Instance().Contains("s"), Is.False);

            return true;
        }, CancellationToken.None);
    }

    [TestCase("VAR v=\"bad\\q SECRET s=Secret(p,0x(01AABBCC) 0x(02DDEEFF))")]
    [TestCase("VAR v=\"bad\\z Print(Secret(p,\n0x(01AABBCC) 0x(02DDEEFF)))")]
    public async Task LexerRecoverySecretErrorsAreRedactedInGui(string script)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            await editor.ParseLineAsync(script);
            Assert.That(status.StatusString, Does.Contain("Secret import"));
            Assert.That(status.StatusString, Does.Not.Contain("AABBCC").And.Not.Contain("DDEEFF"));
            foreach (var error in SyntaxErrorList.Instance())
                Assert.That(error.Symbol + error.Message, Does.Not.Contain("AABBCC").And.Not.Contain("DDEEFF"));
            Assert.That(SyntaxErrorListner.ErrorMessage.ToString(), Does.Not.Contain("AABBCC").And.Not.Contain("DDEEFF"));

            return true;
        }, CancellationToken.None);
    }

    [TestCase("\n", 0)]
    [TestCase("\n", 21)]
    [TestCase("\r\n", 0)]
    [TestCase("\r\n", 21)]
    public async Task LaterLexerFailureCannotCopyEarlierSharesIntoGuiDiagnostics(string newline, int column)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            await editor.ParseLineAsync("PARAM p=Parameters(SECRET_SHAMIR,#PARTS:2,#MINPARTS:2)");
            await editor.ParseLineAsync("SECRET s=Secret(p,0x(01AABBCC),0x(02DDEEFF))" + newline + new string(' ', column) + "@");
            var error = SyntaxErrorList.Instance().OfType<LexerError>().Single();
            Assert.That(error.Symbol, Is.EqualTo("@"));
            Assert.That(error.Line, Is.EqualTo("2"));
            Assert.That(error.Column, Is.EqualTo(column.ToString()));
            Assert.That(status.StatusString + SyntaxErrorListner.ErrorMessage,
                Does.Not.Contain("AABBCC").And.Not.Contain("DDEEFF"));

            return true;
        }, CancellationToken.None);
    }

    public sealed class ScriptBookTestApp
    {
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [TestCase("\n", 1)]
    [TestCase("\r\n", 1)]
    [TestCase("\n", 2)]
    [TestCase("\r\n", 2)]
    public async Task KeyPrintSurvivesEditorSaveAndScriptBookReload(string newline, int printCount)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            var textEditor = new TextEditor();
            editor.TextEditor = textEditor;
            // Exercise the actual view input handler without unrelated XAML/navigation setup.
            var view = (CryptoScriptEditView)RuntimeHelpers.GetUninitializedObject(typeof(CryptoScriptEditView));
            typeof(CryptoScriptEditView).GetField("_textEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(view, textEditor);
            typeof(CryptoScriptEditView).GetField("_viewModel", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(view, editor);
            var entering = typeof(CryptoScriptEditView).GetMethod("textEditor_TextArea_TextEntering", BindingFlags.Instance | BindingFlags.NonPublic)!;
            async Task Enter(string script)
            {
                textEditor.Document.Insert(textEditor.Document.TextLength, script);
                textEditor.TextArea.Caret.Offset = textEditor.Document.TextLength;
                entering.Invoke(view, new object[] { textEditor.TextArea, new TextInputEventArgs { Text = newline } });
                textEditor.Document.Insert(textEditor.Document.TextLength, newline);
                await view.PendingExecution;
            }

            string path = Path.Combine(Path.GetTempPath(), "g01-m1-" + Guid.NewGuid().ToString("N") + ".sbk");
            try
            {
                await Enter("KEY k=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F))");
                for (int i = 0; i < printCount; i++) await Enter("Print(k)");
                await Enter("Print(0x(AB))");
                await Enter("VAR after=0x(CAFE)");
                editor.SaveScriptBook(path);
                string saved = File.ReadAllText(path);
                // SaveScriptBook normalizes to LF. Also exercise externally supplied CRLF books.
                if (newline == "\r\n") File.WriteAllText(path, saved.Replace("\n", "\r\n"));
                VariableDictionary.Instance().Clear();
                textEditor.Document.Text = string.Empty;
                await editor.OpenScriptBookAsync(path);

                Assert.That(editor.LastScriptBookSucceeded, Is.True, "Saved KEY output must not be parsed as script.");
                Assert.That(status.Diagnostics, Is.Empty);
                Assert.That(status.StatusString, Does.Not.Contain("SYNTAX ERROR"));
                Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
                Assert.That(VariableDictionary.Instance().Get("after").Value, Is.EqualTo("0x(CAFE)"));
                string[] lines = File.ReadAllLines(path);
                Assert.That(lines.Any(line => line.StartsWith("out: KeyValue:", StringComparison.Ordinal)), Is.False);
                foreach (string label in new[] { "Identifier", "Type", "Value", "ValueFormat", "KeySize", "KeyType", "KeySizeInBits", "KeyUsage", "KeyPolicy", "DerivationMechanism", "KeyAttributes", "KCV", "KCVC", "KCVK" })
                    Assert.That(lines.Count(line => line.StartsWith("out: " + label + ":", StringComparison.Ordinal)), Is.EqualTo(printCount));
                foreach (string fullValue in new[] { "Value: 0x(000102030405060708090A0B0C0D0E0F)", "KCV: 0x(c6a13b37878f5b826f4f8162a1c8d879)", "KCVC: 0x(be7ed6ae787dd5544c5561c7dcd06abb)", "KCVK: 0x(0a940bb5416ef045f1c39458c653ea5a)" })
                    Assert.That(lines.Count(line => line == "out: " + fullValue), Is.EqualTo(printCount));
                Assert.That(lines.Count(line => line == "out: 0x(AB)"), Is.EqualTo(1));
                Assert.That(lines, Is.All.Not.Empty);
                Assert.That(textEditor.Document.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(lines));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task SuccessfulInteractiveExecutionClearsOldDiagnosticsAndUpdatesVariables()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            status.AppendDiagnostics(new[] { Warning("old") });
            await editor.ParseLineAsync("VAR result=0x(AB)");
            Assert.That(status.Diagnostics, Is.Empty);
            Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Contain("result"));
            await editor.ParseLineAsync("VAR next=0x(CD)");
            Assert.That(status.Diagnostics, Is.Empty);
            Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Contain("next"));

            return true;
        }, CancellationToken.None);
    }

    [TestCase(1)]
    [TestCase(2)]
    public async Task SuccessfulWarningsRemainOrderedAndDoNotSetErrorFlag(int count)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            var reported = Enumerable.Range(1, count).Select(i => Warning("warning " + i)).ToArray();
            var runner = ControlledRunner((_, sink) => { foreach (var warning in reported) sink.Report(warning); });
            Assert.That(await ExecuteLine("VAR result=Compare(0x(AB),0x(AB))", runner), Is.True);
            Assert.That(status.Diagnostics.Select(item => item.Text),
                Is.EqualTo(reported.Select(SemanticDiagnosticFormatter.Format)));
            Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Contain("result"));
            Assert.That(typeof(CryptoScriptEditViewModel).GetField("_syntaxErrorOccured", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(editor), Is.False);
            await editor.ParseLineAsync("VAR next=0x(CD)");
            Assert.That(status.Diagnostics, Is.Empty);

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task WarningBeforeErrorUsesRunnerSnapshotWithoutDuplicateAndRefreshesPartialState()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            var warning = Warning("warning before failure");
            var error = new SemanticError
            {
                Message = "controlled failure",
                Type = "FunctionContract",
                ErrorCode = FunctionContractError.UnsupportedFunction
            };
            var runner = ControlledRunner((_, sink) =>
            {
                sink.Report(warning);
                sink.Report(error);
                throw new SemanticErrorException { SemanticError = error };
            });
            Assert.That(await ExecuteLine("VAR before=0x(AB) Compare(0x(AB),0x(AB)) VAR after=0x(CD)", runner), Is.False);
            Assert.That(status.Diagnostics.Select(item => item.Text),
                Is.EqualTo(runner.Diagnostics.Select(SemanticDiagnosticFormatter.Format)));
            Assert.That(status.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Contain("before"));
            Assert.That(VariableDictionary.Instance().GetVariables().Select(item => item.Id), Does.Contain("before"));
            Assert.That(VariableDictionary.Instance().GetVariables().Select(item => item.Id), Does.Not.Contain("after"));
            Assert.That(status.StatusString, Does.Not.Contain(error.Message));

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task SyntaxErrorClearsOldWarningsAndDoesNotExecute()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            status.AppendDiagnostics(new[] { Warning("old") });
            await editor.ParseLineAsync("VAR =");
            Assert.That(status.Diagnostics, Is.Empty);
            Assert.That(status.StatusString, Does.Contain("SYNTAX ERROR"));
            Assert.That(variables.DataVariables, Is.Empty);
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task VariableRefreshFailureDoesNotAppendSuccessfulDiagnosticsAgain()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            var warning = Warning("successful script warning");
            var runner = ControlledRunner((_, sink) => sink.Report(warning));
            int refreshAttempts = 0;
            variables.DataVariables.CollectionChanged += (_, _) =>
            {
                ++refreshAttempts;
                throw new InvalidOperationException("controlled variable refresh failure");
            };

            Assert.That(await ExecuteLine("VAR result=Compare(0x(AB),0x(AB))", runner), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(refreshAttempts, Is.EqualTo(1));
                Assert.That(status.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(status.Diagnostics[0].Text, Is.EqualTo(SemanticDiagnosticFormatter.Format(warning)));
                Assert.That(status.StatusString, Is.EqualTo("Variable presentation failed."));
                Assert.That(typeof(CryptoScriptEditViewModel).GetField("_syntaxErrorOccured", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(editor), Is.True);
                Assert.That(VariableDictionary.Instance().GetVariables().Select(item => item.Id), Does.Contain("result"));
            });

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task MissingScriptBookClearsOldDiagnosticsBeforePropagatingFileError()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            status.AppendDiagnostics(new[] { Warning("old") });
            int resets = 0;
            ((System.Collections.Specialized.INotifyCollectionChanged)status.Diagnostics).CollectionChanged += (_, args) =>
            {
                if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) ++resets;
            };
            var missingFile = Path.Combine(Path.GetTempPath(), "missing-scriptbook-" + Guid.NewGuid().ToString("N") + ".sbk");

            await Assert.ThatAsync(() => editor.OpenScriptBookAsync(missingFile), Throws.TypeOf<FileNotFoundException>());
            Assert.That(status.Diagnostics, Is.Empty);
            Assert.That(resets, Is.EqualTo(1));
            Assert.That(editor.LastScriptBookSucceeded, Is.False);

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task ScriptBookProcessingDoesNotResetItsAlreadyStartedPresentation()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            status.AppendDiagnostics(new[] { Warning("already collected") });
            var prior = status.Diagnostics[0];
            int resets = 0;
            ((System.Collections.Specialized.INotifyCollectionChanged)status.Diagnostics).CollectionChanged += (_, args) =>
            {
                if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) ++resets;
            };
            await editor.ExecuteScriptBookAsync(new[] { "Compare(0x(AB),0x(AB))" }, _ => { });

            Assert.That(resets, Is.Zero);
            Assert.That(status.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(status.Diagnostics[0], Is.SameAs(prior));
            Assert.That(editor.LastScriptBookSucceeded, Is.True);

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task ScriptBookAppendsWarningsKeepsRealBookLinesAndSkipsOutputLines()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            status.AppendDiagnostics(new[] { Warning("old") });
            int runs = 0;
            var lines = new[] { "Compare(0x(AB),0x(AB))", "out: ignored", "Compare(0x(CD),0x(CD))" };
            var appended = await ExecuteBook(lines, () => ControlledRunner((_, sink) => sink.Report(Warning("run " + ++runs))));
            Assert.That(appended, Is.EqualTo(lines));
            Assert.That(editor.LastScriptBookSucceeded, Is.True);
            Assert.That(runs, Is.EqualTo(2));
            Assert.That(status.Diagnostics.Select(item => item.ScriptBookLine), Is.EqualTo(new int?[] { 1, 3 }));
            Assert.That(status.Diagnostics[0].Text, Does.Contain("Statement=1"));
            Assert.That(status.Diagnostics[1].Text, Does.Contain("Statement=1"));
            Assert.That(status.Diagnostics[0].Text, Does.Contain("run 1"));
            Assert.That(status.Diagnostics[1].Text, Does.Contain("run 2"));

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task ScriptBookStopsAfterErrorAndSidebarDoesNotOverwriteAbortedStatus()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            var lines = new[] { "Compare(0x(AB),0x(AB))", "Print(missing)", "VAR after=0x(CD)" };
            var appended = await ExecuteBook(lines, () => ControlledRunner((call, sink) =>
            {
                if (call.Location?.FunctionName == "Compare") sink.Report(Warning("first warning"));
            }));
            Assert.That(appended, Is.EqualTo(lines.Take(2)));
            Assert.That(editor.LastScriptBookSucceeded, Is.False);
            Assert.That(status.Diagnostics[0].Severity, Is.EqualTo(SemanticDiagnosticSeverity.Warning));
            Assert.That(status.Diagnostics[1].Severity, Is.EqualTo(SemanticDiagnosticSeverity.Error));
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
            var originalStatus = status.StatusString;
            UpdateSidebarStatus();
            Assert.That(status.StatusString, Is.EqualTo(originalStatus));
            await ExecuteBook(new[] { "VAR success=0x(AB)" });
            UpdateSidebarStatus();
            Assert.That(status.StatusString, Is.EqualTo("Script book loaded and all lines successfull parsed"));
            Assert.That(status.Diagnostics, Is.Empty);

            return true;
        }, CancellationToken.None);
    }

    private void UpdateSidebarStatus()
    {
        // Test only the completion decision, without constructing a native WindowIcon or file picker.
        var sidebar = (SidebarViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SidebarViewModel));
        typeof(SidebarViewModel).GetField("_cseVm", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(sidebar, editor);
        typeof(SidebarViewModel).GetMethod("SetScriptBookCompletionStatus", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(sidebar, null);
    }

    [Test]
    public async Task NestedOperationsKeepTheirContextsInPresentation()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            var reported = new List<SemanticError>();
            var runner = ControlledRunner((_, sink) =>
            {
                var warning = Warning("nested");
                reported.Add(warning);
                sink.Report(warning);
            });
            Assert.That(await ExecuteLine("Print(Compare(0x(AB),0x(AB)))", runner), Is.True);
            Assert.That(status.Diagnostics.Select(item => item.Text),
                Is.EqualTo(reported.Select(SemanticDiagnosticFormatter.Format)));
            Assert.That(reported, Has.Count.EqualTo(2));
            // BeforeOperation runs when the call executes: arguments execute first, IDs are still outer-first.
            Assert.That(reported[0].Location!.FunctionName, Is.EqualTo("Compare"));
            Assert.That(reported[0].Location!.OperationId, Is.EqualTo(2));
            Assert.That(reported[0].Location!.ParentOperationId, Is.EqualTo(1));
            Assert.That(reported[1].Location!.FunctionName, Is.EqualTo("Print"));
            Assert.That(reported[1].Location!.OperationId, Is.EqualTo(1));

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task PrintOutputIsUnchanged()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            var original = Console.Out;
            using var output = new StringWriter();
            try
            {
                Console.SetOut(output);
                await editor.ParseLineAsync("Print(0x(AB))");
                Assert.That(output.ToString(), Is.EqualTo(editor.PrintMessage + Environment.NewLine));
                Assert.That(editor.PrintMessage, Does.StartWith("out: "));
                Assert.That(status.Diagnostics, Is.Empty);
            }
            finally { Console.SetOut(original); }

            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task KeyMapEmptyResultPreservesForeignDiagnostics()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ScriptBookTestApp));
        await session.Dispatch(async () =>
        {
            // Detach the editor's info handler: this test concerns KeyMap, not region navigation.
            OutputOperations.InfoEvent -= Handler(editor, "OnInfoEvent");
            status.AppendDiagnostics(new[] { Warning("interactive warning") });
            var prior = status.Diagnostics[0];
            var keyMap = new KeyMapViewModel(status);
            await keyMap.Initialization;
            try
            {
                Assert.That(status.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(status.Diagnostics[0], Is.SameAs(prior));
                var warning = Warning("keymap warning");
                await keyMap.LoadInfoAsync(status, ControlledRunner((_, sink) => sink.Report(warning)));
                Assert.That(status.Diagnostics, Has.Count.EqualTo(2));
                Assert.That(status.Diagnostics[1].Text, Is.EqualTo(SemanticDiagnosticFormatter.Format(warning)));
            }
            finally { OutputOperations.InfoEvent -= Handler(keyMap, "OnInfoEvent"); }

            return true;
        }, CancellationToken.None);
    }
}
