using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class AsyncPasswordGuiTests
{
    public sealed class TestApp
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    private static async Task<PasswordDialog> OpenDialog(Window owner)
    {
        for (int i = 0; i < 100; i++)
        {
            var dialog = owner.OwnedWindows.OfType<PasswordDialog>().FirstOrDefault();
            if (dialog is not null) return dialog;
            await Task.Delay(10);
        }
        throw new AssertionException("Password dialog did not open.");
    }

    [TestCase(false)] [TestCase(true)]
    public async Task ProductiveSecureSaveUsesDialogAndStopsBookOnCancellation(bool cancel)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
        await session.Dispatch(async () =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "S05B2-GUI-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var dictionary = VariableDictionary.Instance(); var previous = dictionary.GetVariables().ToArray();
            var owner = new Window(); owner.Show();
            using var host = new PasswordDialogHost(() => owner);
            var status = new StatusViewModel(); var variables = new VariableViewModel();
            using var editor = new CryptoScriptEditViewModel(status, variables, new InfoViewModel(),
                new MainViewModel(), null!, new HistoryService());
            var runner = new CryptoScriptRunner();
            try
            {
                dictionary.Clear();
                string destination = Path.Combine(directory, "secure.json");
                dictionary.Add(new PathVariableDeclaration { Id = "path", Type = new CryptoTypePath(), Value = destination });
                dictionary.Add(new StringVariableDeclaration { Id = "data", Value = "GUI-CANARY" });
                var lines = new List<string>();
                var execution = editor.ExecuteScriptBookAsync(new[] { "SecureSave(path,data,#SECUREKDF:PBKDF2)", "Print(data)", "Info(SecureSave)" },
                    lines.Add, passwordHandler: host.RequestAsync);
                var dialog = await OpenDialog(owner);
                Assert.That(editor.IsExecuting,Is.True);
                bool responsive = false; await Dispatcher.UIThread.InvokeAsync(() => responsive = true);
                Assert.That(responsive,Is.True);
                if (cancel) dialog.Close();
                else { dialog.PasswordInput.Text = "canary"; dialog.ConfirmationInput.Text = "canary"; dialog.Confirm(); }
                await execution;
                Assert.That(editor.LastScriptBookSucceeded,Is.EqualTo(!cancel));
                Assert.That(lines,Has.Count.EqualTo(cancel ? 1 : 3));
                Assert.That(File.Exists(destination),Is.EqualTo(!cancel));
                Assert.That(dialog.PasswordInput.Text,Is.Empty);
                Assert.That(dialog.ConfirmationInput.Text,Is.Empty);
                if (!cancel) Assert.That(File.ReadAllText(destination),Does.Not.Contain("GUI-CANARY"));
                return true;
            }
            finally
            {
                owner.Close(); dictionary.Clear(); foreach (var v in previous) dictionary.Add(v);
                Directory.Delete(directory,true);
            }
        }, CancellationToken.None);
    }

    [TestCase("success")] [TestCase("wrong")] [TestCase("cancel")]
    public async Task ProductiveSecureLoadDialogUpdatesVariablesAndStopsBookOnFailure(string outcome)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
        await session.Dispatch(async () =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "S06-GUI-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            var dictionary = VariableDictionary.Instance(); var previous = dictionary.GetVariables().ToArray();
            var owner = new Window(); owner.Show(); using var host = new PasswordDialogHost(() => owner);
            var status = new StatusViewModel(); var variables = new VariableViewModel();
            using var editor = new CryptoScriptEditViewModel(status, variables, new InfoViewModel(), new MainViewModel(), null!, new HistoryService());
            Task? activeExecution = null;
            try
            {
                dictionary.Clear();
                var path = new PathVariableDeclaration {Id="path",Type=new CryptoTypePath(),Value=Path.Combine(directory,"secure.json")};
                dictionary.Add(path); dictionary.Add(new StringVariableDeclaration {Id="data",Value="GUI-CANARY"});
                var save = new CryptoScriptRunner(); save.PasswordRequested += (_,r) => Task.FromResult(PasswordResponse.Supplied(r.RequestId,
                    PasswordSecret.TakeOwnership("canary".ToCharArray()),PasswordSecret.TakeOwnership("canary".ToCharArray())));
                await Task.Run(() => save.ExecuteAsync(CryptoScript.Model.ParserBuilder.StringBuild("SecureSave(path,data,#SECUREKDF:PBKDF2)").program()));
                dictionary.Clear(); dictionary.Add(path);
                var runner = new CryptoScriptRunner();
                var lines = new List<string>();
                var execution = editor.ExecuteScriptBookAsync(new[] {"SecureLoad(path)","Print(data)","Info(SecureLoad)"}, lines.Add, passwordHandler: host.RequestAsync);
                activeExecution = execution;
                var dialog = await OpenDialog(owner);
                Assert.That(dialog.ConfirmationInput.Parent,Is.Null);
                bool responsive=false; await Dispatcher.UIThread.InvokeAsync(()=>responsive=true); Assert.That(responsive,Is.True);
                Assert.That(execution.IsCompleted,Is.False);
                if (outcome=="cancel") dialog.Close(); else {dialog.PasswordInput.Text=outcome=="wrong" ? "wrong" : "canary"; dialog.Confirm();}
                await execution;
                Assert.That(editor.LastScriptBookSucceeded,Is.EqualTo(outcome=="success"));
                Assert.That(lines,Has.Count.EqualTo(outcome=="success" ? 3 : 1));
                Assert.That(dictionary.Contains("data"),Is.EqualTo(outcome=="success"));
                if (outcome=="success") Assert.That(variables.DataVariables.Any(v=>v.Identifier=="data"),Is.True);
                if (outcome=="wrong")
                {
                    Assert.That(status.StatusString,Does.Contain("semantic error"));
                    Assert.That(status.Diagnostics.Single().Text,Does.Contain("SecureAuthenticationFailed"));
                }
                if (outcome=="cancel") Assert.That(status.StatusString,Does.Contain("cancelled by user"));
                Assert.That(dialog.PasswordInput.Text,Is.Empty);
                return true;
            }
            finally
            {
                owner.Close();
                if (activeExecution is not null) await activeExecution;
                dictionary.Clear(); foreach(var v in previous) dictionary.Add(v); Directory.Delete(directory,true);
            }
        }, CancellationToken.None);
    }

    [TestCase(PasswordPurpose.SecureSave)] [TestCase(PasswordPurpose.SecureLoad)]
    public async Task WorkerRequestOpensResponsiveUiDialogAndClearsInputs(PasswordPurpose purpose)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
        await session.Dispatch(async () =>
        {
            var owner = new Window(); owner.Show();
            using var host = new PasswordDialogHost(() => owner);
            var runner = new CryptoScriptRunner();
            runner.PasswordRequested += host.RequestAsync;
            PasswordSecret? supplied = null;
            var execution = Task.Run(() => runner.ExecuteAsync(new[] { new AsyncInfrastructureStatement(async context =>
            { using var response = await context.RequestPasswordAsync(purpose); supplied = response.Password; }) }));
            try
            {
                var dialog = await OpenDialog(owner);
                Assert.That(Dispatcher.UIThread.CheckAccess(), Is.True);
                bool heartbeat = false;
                await Dispatcher.UIThread.InvokeAsync(() => heartbeat = true);
                Assert.That(heartbeat, Is.True); Assert.That(execution.IsCompleted, Is.False);
                dialog.PasswordInput.Text = "canary";
                if (purpose == PasswordPurpose.SecureSave)
                {
                    dialog.Confirm();
                    Assert.That(dialog.IsVisible, Is.True);
                    Assert.That(dialog.Validation.Text, Is.EqualTo("Passwords do not match."));
                    dialog.ConfirmationInput.Text = "canary";
                }
                dialog.Confirm();
                await execution;
                Assert.That(dialog.PasswordInput.Text, Is.Empty);
                Assert.That(dialog.ConfirmationInput.Text, Is.Empty);
                Assert.Throws<ObjectDisposedException>(() => _ = supplied!.Characters);
                return true;
            }
            finally { owner.Close(); runner.PasswordRequested -= host.RequestAsync; }
        }, CancellationToken.None);
    }

    [TestCase("close")] [TestCase("token")] [TestCase("owner")]
    public async Task DialogCloseCancellationAndOwnerLifetimeAbort(string kind)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
        await session.Dispatch(async () =>
        {
            var owner = new Window(); owner.Show();
            using var host = new PasswordDialogHost(() => owner);
            using var cancellation = new CancellationTokenSource();
            var runner = new CryptoScriptRunner(); runner.PasswordRequested += host.RequestAsync;
            var execution = Task.Run(() => runner.ExecuteAsync(new[] { new AsyncInfrastructureStatement(async context =>
            { using var response = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad); }) }, cancellation.Token));
            try
            {
                var dialog = await OpenDialog(owner);
                dialog.PasswordInput.Text = "canary";
                if (kind == "token") cancellation.Cancel();
                else if (kind == "owner") owner.Close();
                else dialog.Close();
                try { await execution; Assert.Fail("Expected cancellation"); }
                catch (SecurePasswordCancelledException) { Assert.That(kind, Is.Not.EqualTo("token")); }
                catch (OperationCanceledException) { Assert.That(kind, Is.EqualTo("token")); }
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.That(dialog.PasswordInput.Text, Is.Empty);
                return true;
            }
            finally { owner.Close(); runner.PasswordRequested -= host.RequestAsync; }
        }, CancellationToken.None);
    }

    [Test]
    public async Task ScriptBookStopsAtUserCancellationAndNeverStoresPassword()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
        await session.Dispatch(async () =>
        {
            var history = new HistoryService();
            var status = new StatusViewModel();
            using var editor = new CryptoScriptEditViewModel(status, new VariableViewModel(), new InfoViewModel(),
                new MainViewModel(), null!, history);
            var runner = new CryptoScriptRunner();
            PasswordRequestHandler password = (_, request) => Task.FromResult(PasswordResponse.UserCancelled(request.RequestId));
            var lines = new List<string>();
            await editor.ExecuteScriptBookAsync(new[] { "Print(0x(01))", "Print(0x(02))" }, lines.Add,
                passwordHandler: password, testStatements: new[] { new AsyncInfrastructureStatement(async context =>
                { using var response = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad); }) });
            Assert.That(lines, Has.Count.EqualTo(1));
            Assert.That(editor.LastScriptBookSucceeded, Is.False);
            Assert.That(status.StatusString, Does.Contain("cancelled by user"));
            Assert.That(editor.PrintMessage, Is.Empty);
            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task PendingRequestRejectsBookClearAndVariableCommands()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
        await session.Dispatch(async () =>
        {
            var variables = new VariableViewModel();
            using var editor = new CryptoScriptEditViewModel(new StatusViewModel(), variables, new InfoViewModel(),
                new MainViewModel(), null!, new HistoryService());
            var requested = new TaskCompletionSource<PasswordRequestEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            var response = new TaskCompletionSource<PasswordResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runner = new CryptoScriptRunner();
            PasswordRequestHandler password = (_, request) => { requested.SetResult(request); return response.Task; };
            var execution = editor.ExecuteLineAsync("Print(0x(01))", passwordHandler: password,
                testStatements: new[] { new AsyncInfrastructureStatement(async context =>
                { using var answer = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad); }) });
            var request = await requested.Task;
            try
            {
                Assert.ThrowsAsync<ExecutionBusyException>(async () => await editor.OpenScriptBookAsync("must-not-be-read", clear: true));
                Assert.DoesNotThrow(() => variables.ModifyValueCommand.Execute(null!));
                Assert.DoesNotThrow(() => variables.ModifyVariableCommand.Execute(null!));
                // Also cover collision admission without the editor's busy predicate.
                var otherVariables = new VariableViewModel();
                Assert.DoesNotThrow(() => otherVariables.ModifyValueCommand.Execute(null!));
                Assert.That(editor.IsExecuting, Is.True);
            }
            finally { response.TrySetResult(PasswordResponse.UserCancelled(request.RequestId)); await execution; }
            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task AsyncEditorPreservesOutputOrderAndDetachedVariableEditing()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
        await session.Dispatch(async () =>
        {
            var previous = VariableDictionary.Instance().GetVariables().ToArray();
            var variables = new VariableViewModel();
            using var editor = new CryptoScriptEditViewModel(new StatusViewModel(), variables, new InfoViewModel(),
                new MainViewModel(), null!, new HistoryService());
            try
            {
                await editor.ParseLineAsync("VAR asyncValue=0x(01) Print(asyncValue) Info(keymap) Print(0x(02))");
                Assert.That(editor.PrintMessage, Is.EqualTo("out: 0x(01)" + Environment.NewLine + "out: 0x(02)"));
                variables.SelectedVariable = variables.DataVariables.Single(v => v.Identifier == "asyncValue");
                variables.SelectedVariable.Value = "0x(03)";
                variables.ModifyValueCommand.Execute(variables.SelectedVariable);
                Assert.That(VariableDictionary.Instance().Get("asyncValue").Value, Is.EqualTo("0x(03)"));
                Assert.That(editor.IsExecuting, Is.False);
                return true;
            }
            finally { VariableDictionary.Instance().Clear(); foreach (var v in previous) VariableDictionary.Instance().Add(v); }
        }, CancellationToken.None);
    }
}
