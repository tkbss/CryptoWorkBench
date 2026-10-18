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
        OutputOperations.PrintEvent -= Handler(editor, "OnPrintEvent");
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

    private bool ExecuteLine(string script, CryptoScriptRunner? runner = null) =>
        (bool)typeof(CryptoScriptEditViewModel).GetMethod("ExecuteLine", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(editor, new object?[] { script, null, runner })!;

    private List<string> ExecuteBook(string[] lines, Func<CryptoScriptRunner>? factory = null)
    {
        // Model OpenScriptBook's reset; ExecuteScriptBook itself must not reset the presentation.
        status.ClearDiagnostics();
        var appended = new List<string>();
        typeof(CryptoScriptEditViewModel).GetMethod("ExecuteScriptBook", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(editor, new object?[] { lines, (Action<string>)appended.Add, factory });
        return appended;
    }

    private static SemanticError Warning(string message) => new()
    {
        Severity = SemanticDiagnosticSeverity.Warning, Message = message
    };

    [Test]
    public void CheckErrorsContinueAndKeyPrintReachesExistingGuiOutput()
    {
        editor.ParseLine("KEY missing=Check(0x(AA)) VAR continued=0x(01)");
        Assert.That(status.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(status.Diagnostics[0].Text, Does.Contain("KeyCheckValueNotFound"));
        Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Contain("continued"));
        Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Not.Contain("missing"));
        editor.ParseLine("KEY key=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F)) Print(key)");
        Assert.That(editor.PrintMessage, Does.Contain("KCV: 0x(c6a13b37878f5b826f4f8162a1c8d879)"));
        Assert.That(editor.PrintMessage, Does.Contain("KCVC: 0x(be7ed6ae787dd5544c5561c7dcd06abb)"));
        Assert.That(editor.PrintMessage, Does.Contain("KCVK: 0x(0a940bb5416ef045f1c39458c653ea5a)"));
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
        await session.Dispatch(() =>
        {
            var textEditor = new TextEditor();
            editor.TextEditor = textEditor;
            // Exercise the actual view input handler without unrelated XAML/navigation setup.
            var view = (CryptoScriptEditView)RuntimeHelpers.GetUninitializedObject(typeof(CryptoScriptEditView));
            typeof(CryptoScriptEditView).GetField("_textEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(view, textEditor);
            typeof(CryptoScriptEditView).GetField("_viewModel", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(view, editor);
            var entering = typeof(CryptoScriptEditView).GetMethod("textEditor_TextArea_TextEntering", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Enter(string script)
            {
                textEditor.Document.Insert(textEditor.Document.TextLength, script);
                textEditor.TextArea.Caret.Offset = textEditor.Document.TextLength;
                entering.Invoke(view, new object[] { textEditor.TextArea, new TextInputEventArgs { Text = newline } });
                textEditor.Document.Insert(textEditor.Document.TextLength, newline);
            }

            string path = Path.Combine(Path.GetTempPath(), "g01-m1-" + Guid.NewGuid().ToString("N") + ".sbk");
            try
            {
                Enter("KEY k=GenerateKey(AES-ECB,0x(000102030405060708090A0B0C0D0E0F))");
                for (int i = 0; i < printCount; i++) Enter("Print(k)");
                Enter("Print(0x(AB))");
                Enter("VAR after=0x(CAFE)");
                editor.SaveScriptBook(path);
                string saved = File.ReadAllText(path);
                // SaveScriptBook normalizes to LF. Also exercise externally supplied CRLF books.
                if (newline == "\r\n") File.WriteAllText(path, saved.Replace("\n", "\r\n"));
                VariableDictionary.Instance().Clear();
                textEditor.Document.Text = string.Empty;
                editor.OpenScriptBook(path);

                Assert.That(editor.LastScriptBookSucceeded, Is.True, "Saved KEY output must not be parsed as script.");
                Assert.That(status.Diagnostics, Is.Empty);
                Assert.That(status.StatusString, Does.Not.Contain("SYNTAX ERROR"));
                Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
                Assert.That(VariableDictionary.Instance().Get("after").Value, Is.EqualTo("0x(CAFE)"));
                string[] lines = File.ReadAllLines(path);
                foreach (string label in new[] { "Identifier", "Type", "Value", "ValueFormat", "KeyValue", "KeySize", "KeyType", "KeySizeInBits", "KeyUsage", "KeyPolicy", "DerivationMechanism", "KeyAttributes", "KCV", "KCVC", "KCVK" })
                    Assert.That(lines.Count(line => line.StartsWith("out: " + label + ":", StringComparison.Ordinal)), Is.EqualTo(printCount));
                foreach (string fullValue in new[] { "Value: 0x(000102030405060708090A0B0C0D0E0F)", "KCV: 0x(c6a13b37878f5b826f4f8162a1c8d879)", "KCVC: 0x(be7ed6ae787dd5544c5561c7dcd06abb)", "KCVK: 0x(0a940bb5416ef045f1c39458c653ea5a)" })
                    Assert.That(lines.Count(line => line == "out: " + fullValue), Is.EqualTo(printCount));
                Assert.That(lines.Count(line => line == "out: 0x(AB)"), Is.EqualTo(1));
                Assert.That(lines, Is.All.Not.Empty);
                Assert.That(textEditor.Document.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(lines));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }, CancellationToken.None);
    }

    [Test]
    public void SuccessfulInteractiveExecutionClearsOldDiagnosticsAndUpdatesVariables()
    {
        status.AppendDiagnostics(new[] { Warning("old") });
        editor.ParseLine("VAR result=0x(AB)");
        Assert.That(status.Diagnostics, Is.Empty);
        Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Contain("result"));
        editor.ParseLine("VAR next=0x(CD)");
        Assert.That(status.Diagnostics, Is.Empty);
        Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Contain("next"));
    }

    [TestCase(1)]
    [TestCase(2)]
    public void SuccessfulWarningsRemainOrderedAndDoNotSetErrorFlag(int count)
    {
        var reported = Enumerable.Range(1, count).Select(i => Warning("warning " + i)).ToArray();
        var runner = ControlledRunner((_, sink) => { foreach (var warning in reported) sink.Report(warning); });
        Assert.That(ExecuteLine("VAR result=Compare(0x(AB),0x(AB))", runner), Is.True);
        Assert.That(status.Diagnostics.Select(item => item.Text),
            Is.EqualTo(reported.Select(SemanticDiagnosticFormatter.Format)));
        Assert.That(variables.DataVariables.Select(item => item.Identifier), Does.Contain("result"));
        Assert.That(typeof(CryptoScriptEditViewModel).GetField("_syntaxErrorOccured", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(editor), Is.False);
        editor.ParseLine("VAR next=0x(CD)");
        Assert.That(status.Diagnostics, Is.Empty);
    }

    [Test]
    public void WarningBeforeErrorUsesRunnerSnapshotWithoutDuplicateOrVariableRefresh()
    {
        var warning = Warning("warning before failure");
        var error = new SemanticError { Message = "controlled failure", Type = "FunctionContract",
            ErrorCode = FunctionContractError.UnsupportedFunction };
        var runner = ControlledRunner((_, sink) =>
        {
            sink.Report(warning);
            sink.Report(error);
            throw new SemanticErrorException { SemanticError = error };
        });
        Assert.That(ExecuteLine("VAR before=0x(AB) Compare(0x(AB),0x(AB)) VAR after=0x(CD)", runner), Is.False);
        Assert.That(status.Diagnostics.Select(item => item.Text),
            Is.EqualTo(runner.Diagnostics.Select(SemanticDiagnosticFormatter.Format)));
        Assert.That(status.Diagnostics, Has.Count.EqualTo(2));
        Assert.That(variables.DataVariables, Is.Empty);
        Assert.That(VariableDictionary.Instance().GetVariables().Select(item => item.Id), Does.Contain("before"));
        Assert.That(VariableDictionary.Instance().GetVariables().Select(item => item.Id), Does.Not.Contain("after"));
        Assert.That(status.StatusString, Does.Not.Contain(error.Message));
    }

    [Test]
    public void SyntaxErrorClearsOldWarningsAndDoesNotExecute()
    {
        status.AppendDiagnostics(new[] { Warning("old") });
        editor.ParseLine("VAR =");
        Assert.That(status.Diagnostics, Is.Empty);
        Assert.That(status.StatusString, Does.Contain("SYNTAX ERROR"));
        Assert.That(variables.DataVariables, Is.Empty);
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    [Test]
    public void VariableRefreshFailureDoesNotAppendSuccessfulDiagnosticsAgain()
    {
        var warning = Warning("successful script warning");
        var runner = ControlledRunner((_, sink) => sink.Report(warning));
        int refreshAttempts = 0;
        variables.DataVariables.CollectionChanged += (_, _) =>
        {
            ++refreshAttempts;
            throw new InvalidOperationException("controlled variable refresh failure");
        };

        Assert.That(ExecuteLine("VAR result=Compare(0x(AB),0x(AB))", runner), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(refreshAttempts, Is.EqualTo(1));
            Assert.That(status.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(status.Diagnostics[0].Text, Is.EqualTo(SemanticDiagnosticFormatter.Format(warning)));
            Assert.That(status.StatusString, Is.EqualTo("Semantic controlled variable refresh failure"));
            Assert.That(typeof(CryptoScriptEditViewModel).GetField("_syntaxErrorOccured", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(editor), Is.True);
            Assert.That(VariableDictionary.Instance().GetVariables().Select(item => item.Id), Does.Contain("result"));
        });
    }

    [Test]
    public void MissingScriptBookClearsOldDiagnosticsBeforePropagatingFileError()
    {
        status.AppendDiagnostics(new[] { Warning("old") });
        int resets = 0;
        ((System.Collections.Specialized.INotifyCollectionChanged)status.Diagnostics).CollectionChanged += (_, args) =>
        {
            if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) ++resets;
        };
        var missingFile = Path.Combine(Path.GetTempPath(), "missing-scriptbook-" + Guid.NewGuid().ToString("N") + ".sbk");

        Assert.Throws<FileNotFoundException>(() => editor.OpenScriptBook(missingFile));
        Assert.That(status.Diagnostics, Is.Empty);
        Assert.That(resets, Is.EqualTo(1));
        Assert.That(editor.LastScriptBookSucceeded, Is.False);
    }

    [Test]
    public void ScriptBookProcessingDoesNotResetItsAlreadyStartedPresentation()
    {
        status.AppendDiagnostics(new[] { Warning("already collected") });
        var prior = status.Diagnostics[0];
        int resets = 0;
        ((System.Collections.Specialized.INotifyCollectionChanged)status.Diagnostics).CollectionChanged += (_, args) =>
        {
            if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) ++resets;
        };
        typeof(CryptoScriptEditViewModel).GetMethod("ExecuteScriptBook", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(editor, new object?[] { new[] { "Compare(0x(AB),0x(AB))" }, (Action<string>)(_ => { }), null });

        Assert.That(resets, Is.Zero);
        Assert.That(status.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(status.Diagnostics[0], Is.SameAs(prior));
        Assert.That(editor.LastScriptBookSucceeded, Is.True);
    }

    [Test]
    public void ScriptBookAppendsWarningsKeepsRealBookLinesAndSkipsOutputLines()
    {
        status.AppendDiagnostics(new[] { Warning("old") });
        int runs = 0;
        var lines = new[] { "Compare(0x(AB),0x(AB))", "out: ignored", "Compare(0x(CD),0x(CD))" };
        var appended = ExecuteBook(lines, () => ControlledRunner((_, sink) => sink.Report(Warning("run " + ++runs))));
        Assert.That(appended, Is.EqualTo(lines));
        Assert.That(editor.LastScriptBookSucceeded, Is.True);
        Assert.That(runs, Is.EqualTo(2));
        Assert.That(status.Diagnostics.Select(item => item.ScriptBookLine), Is.EqualTo(new int?[] { 1, 3 }));
        Assert.That(status.Diagnostics[0].Text, Does.Contain("Statement=1"));
        Assert.That(status.Diagnostics[1].Text, Does.Contain("Statement=1"));
        Assert.That(status.Diagnostics[0].Text, Does.Contain("run 1"));
        Assert.That(status.Diagnostics[1].Text, Does.Contain("run 2"));
    }

    [Test]
    public void ScriptBookStopsAfterErrorAndSidebarDoesNotOverwriteAbortedStatus()
    {
        var lines = new[] { "Compare(0x(AB),0x(AB))", "Print(missing)", "VAR after=0x(CD)" };
        var appended = ExecuteBook(lines, () => ControlledRunner((call, sink) =>
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
        ExecuteBook(new[] { "VAR success=0x(AB)" });
        UpdateSidebarStatus();
        Assert.That(status.StatusString, Is.EqualTo("Script book loaded and all lines successfull parsed"));
        Assert.That(status.Diagnostics, Is.Empty);
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
    public void NestedOperationsKeepTheirContextsInPresentation()
    {
        var reported = new List<SemanticError>();
        var runner = ControlledRunner((_, sink) =>
        {
            var warning = Warning("nested");
            reported.Add(warning);
            sink.Report(warning);
        });
        Assert.That(ExecuteLine("Print(Compare(0x(AB),0x(AB)))", runner), Is.True);
        Assert.That(status.Diagnostics.Select(item => item.Text),
            Is.EqualTo(reported.Select(SemanticDiagnosticFormatter.Format)));
        Assert.That(reported, Has.Count.EqualTo(2));
        // BeforeOperation runs when the call executes: arguments execute first, IDs are still outer-first.
        Assert.That(reported[0].Location!.FunctionName, Is.EqualTo("Compare"));
        Assert.That(reported[0].Location!.OperationId, Is.EqualTo(2));
        Assert.That(reported[0].Location!.ParentOperationId, Is.EqualTo(1));
        Assert.That(reported[1].Location!.FunctionName, Is.EqualTo("Print"));
        Assert.That(reported[1].Location!.OperationId, Is.EqualTo(1));
    }

    [Test]
    public void PrintOutputIsUnchanged()
    {
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            editor.ParseLine("Print(0x(AB))");
            Assert.That(output.ToString(), Is.EqualTo(editor.PrintMessage + Environment.NewLine));
            Assert.That(editor.PrintMessage, Does.StartWith("out: "));
            Assert.That(status.Diagnostics, Is.Empty);
        }
        finally { Console.SetOut(original); }
    }

    [Test]
    public void KeyMapEmptyResultPreservesForeignDiagnostics()
    {
        // Detach the editor's info handler: this test concerns KeyMap, not region navigation.
        OutputOperations.InfoEvent -= Handler(editor, "OnInfoEvent");
        status.AppendDiagnostics(new[] { Warning("interactive warning") });
        var prior = status.Diagnostics[0];
        var keyMap = new KeyMapViewModel(status);
        try
        {
            Assert.That(status.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(status.Diagnostics[0], Is.SameAs(prior));
            var warning = Warning("keymap warning");
            typeof(KeyMapViewModel).GetMethod("LoadInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyMap, new object[] { status, ControlledRunner((_, sink) => sink.Report(warning)) });
            Assert.That(status.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(status.Diagnostics[1].Text, Is.EqualTo(SemanticDiagnosticFormatter.Format(warning)));
        }
        finally { OutputOperations.InfoEvent -= Handler(keyMap, "OnInfoEvent"); }
    }
}
