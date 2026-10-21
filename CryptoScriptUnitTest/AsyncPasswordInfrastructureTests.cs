using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class AsyncPasswordInfrastructureTests
{
    private static AsyncInfrastructureStatement Request(PasswordPurpose purpose = PasswordPurpose.SecureLoad,
        Action<PasswordResponse>? received = null) => new(async context =>
    {
        using var response = await context.RequestPasswordAsync(purpose);
        received?.Invoke(response);
    });
    private static PasswordResponse Supply(PasswordRequestEventArgs request, char[]? password = null, char[]? confirmation = null) =>
        PasswordResponse.Supplied(request.RequestId, PasswordSecret.TakeOwnership(password ?? "canary".ToCharArray()),
            confirmation is null ? null : PasswordSecret.TakeOwnership(confirmation));
    private static Task<CryptoScriptProgram> Run(CryptoScriptRunner runner, PasswordPurpose purpose = PasswordPurpose.SecureLoad,
        CancellationToken token = default) => runner.ExecuteAsync(new[] { Request(purpose) }, token);

    [Test]
    public async Task NormalStatementsPreserveResultsLocationsAndEarlierEffects()
    {
        var dictionary = VariableDictionary.Instance();
        var before = dictionary.GetVariables().ToArray();
        try
        {
            dictionary.Clear();
            var sync = new CryptoScriptRunner();
            var first = sync.Execute(ParserBuilder.StringBuild("VAR a=Compare(0x(AB),0x(AB))").program());
            string expected = dictionary.Get("a").Value;
            dictionary.Clear();
            var runner = new CryptoScriptRunner();
            var second = await runner.ExecuteAsync(ParserBuilder.StringBuild("VAR a=Compare(0x(AB),0x(AB))").program());
            Assert.That(dictionary.Get("a").Value, Is.EqualTo(expected));
            Assert.That(second.Statements.Count, Is.EqualTo(first.Statements.Count));
            var warningRunner = new CryptoScriptRunner((_, sink) => sink.Report(new SemanticError { Message = "warning" }));
            await warningRunner.ExecuteAsync(ParserBuilder.StringBuild("Compare(0x(AB),0x(AB))").program());
            Assert.That(warningRunner.Diagnostics.Single().Location!.StatementIndex, Is.EqualTo(1));
            Assert.That(warningRunner.Diagnostics.Single().Location!.OperationId, Is.EqualTo(1));
            var failing = ParserBuilder.StringBuild("VAR b=0x(01) Unknown()").program();
            Assert.ThrowsAsync<SemanticErrorException>(async () => await runner.ExecuteAsync(failing));
            Assert.That(dictionary.Contains("b"), Is.True);
            Assert.That(runner.Diagnostics.Single().Location!.StatementIndex, Is.EqualTo(2));
            await runner.ExecuteAsync(Array.Empty<StatementNode?>());
            Assert.That(runner.Diagnostics, Is.Empty);
        }
        finally { dictionary.Clear(); foreach (var variable in before) dictionary.Add(variable); }
    }

    [TestCase(false)] [TestCase(true)]
    public async Task ImmediateOrDelayedResponsesHaveCorrectIdentityAndDispose(bool delayed)
    {
        var runner = new CryptoScriptRunner();
        char[] secret = "canary".ToCharArray();
        char[] confirmation = "canary".ToCharArray();
        var ids = new List<Guid>();
        runner.PasswordRequested += async (sender, request) =>
        {
            Assert.That(sender, Is.SameAs(runner));
            Assert.That(request.RunnerId, Is.EqualTo(runner.RunnerId));
            Assert.That(request.ExecutionId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(request.ConfirmationRequired, Is.True);
            ids.Add(request.RequestId);
            if (delayed) await Task.Yield();
            return Supply(request, secret, confirmation);
        };
        await Run(runner, PasswordPurpose.SecureSave);
        Assert.That(secret, Is.All.EqualTo('\0'));
        Assert.That(confirmation, Is.All.EqualTo('\0'));
        Assert.That(ids, Has.Count.EqualTo(1));
    }

    [TestCase(0, PasswordProtocolError.PasswordHostUnavailable)]
    [TestCase(2, PasswordProtocolError.PasswordHostAmbiguous)]
    public void SubscriberCountFailsBeforeInvokingAnyone(int count, PasswordProtocolError expected)
    {
        var runner = new CryptoScriptRunner();
        int invoked = 0;
        for (int i = 0; i < count; i++) runner.PasswordRequested += (_, request) => { invoked++; return Task.FromResult(Supply(request)); };
        var error = Assert.ThrowsAsync<PasswordProtocolException>(async () => await Run(runner));
        Assert.That(error!.Code, Is.EqualTo(expected)); Assert.That(invoked, Is.Zero);
        Assert.That(runner.Diagnostics.Single().Message, Is.EqualTo(error.Message));
    }

    [TestCase("nullTask")] [TestCase("nullResponse")] [TestCase("foreignId")]
    [TestCase("confirmationOnLoad")] [TestCase("disposed")]
    public void InvalidResponsesAreRejectedAndOwnedBuffersErased(string kind)
    {
        var runner = new CryptoScriptRunner();
        char[] secret = "canary".ToCharArray();
        bool created = false;
        runner.PasswordRequested += (_, request) =>
        {
            if (kind == "nullTask") return null!;
            if (kind == "nullResponse") return Task.FromResult<PasswordResponse>(null!);
            var response = PasswordResponse.Supplied(kind == "foreignId" ? Guid.NewGuid() : request.RequestId,
                PasswordSecret.TakeOwnership(secret), kind == "confirmationOnLoad" ? PasswordSecret.TakeOwnership("canary".ToCharArray()) : null);
            created = true;
            if (kind == "disposed") response.Dispose();
            return Task.FromResult(response);
        };
        Assert.That(Assert.ThrowsAsync<PasswordProtocolException>(async () => await Run(runner))!.Code,
            Is.EqualTo(PasswordProtocolError.PasswordHostContractViolation));
        if (created) Assert.That(secret, Is.All.EqualTo('\0'));
    }

    [TestCase(false)] [TestCase(true)]
    public void HostExceptionsAreSanitizedBeforeAndAfterTaskReturn(bool asynchronous)
    {
        var runner = new CryptoScriptRunner();
        runner.PasswordRequested += (_, _) => asynchronous
            ? Task.FromException<PasswordResponse>(new Exception("PASSWORD-CANARY"))
            : throw new Exception("PASSWORD-CANARY");
        var error = Assert.ThrowsAsync<PasswordProtocolException>(async () => await Run(runner))!;
        Assert.That(error.Code, Is.EqualTo(PasswordProtocolError.PasswordHostFailure));
        Assert.That(error.InnerException, Is.Null);
        Assert.That(error.ToString(), Does.Not.Contain("PASSWORD-CANARY"));
        Assert.That(runner.Diagnostics.Single().Message, Does.Not.Contain("PASSWORD-CANARY"));
    }

    [TestCase(null, PasswordProtocolError.PasswordHostContractViolation)]
    [TestCase("different", PasswordProtocolError.PasswordConfirmationMismatch)]
    public void SaveConfirmationIsCoreValidated(string? confirmation, PasswordProtocolError code)
    {
        var runner = new CryptoScriptRunner();
        runner.PasswordRequested += (_, request) => Task.FromResult(Supply(request, confirmation: confirmation?.ToCharArray()));
        Assert.That(Assert.ThrowsAsync<PasswordProtocolException>(async () => await Run(runner, PasswordPurpose.SecureSave))!.Code, Is.EqualTo(code));
    }

    [TestCase("")] [TestCase("surrogate")] [TestCase("oversize")]
    public void PasswordEncodingAndBoundsAreCoreValidated(string value)
    {
        var runner = new CryptoScriptRunner();
        runner.PasswordRequested += (_, request) => Task.FromResult(Supply(request,
            (value == "oversize" ? new string('x', 1025) : value == "surrogate" ? "\ud800" : value).ToCharArray()));
        Assert.That(Assert.ThrowsAsync<PasswordProtocolException>(async () => await Run(runner))!.Code,
            Is.EqualTo(PasswordProtocolError.InvalidPassword));
    }

    [Test]
    public void OwnershipCannotBeTransferredTwiceOrAliasedForConfirmation()
    {
        char[] buffer = "canary".ToCharArray();
        using var secret = PasswordSecret.TakeOwnership(buffer);
        using var alias = PasswordSecret.TakeOwnership(buffer);
        Assert.Throws<PasswordProtocolException>(() => PasswordResponse.Supplied(Guid.NewGuid(), secret, alias));
        using var response = PasswordResponse.Supplied(Guid.NewGuid(), secret);
        Assert.Throws<PasswordProtocolException>(() => PasswordResponse.Supplied(Guid.NewGuid(), secret));
        Assert.That(System.Text.Json.JsonSerializer.Serialize(response), Does.Not.Contain("canary").And.Not.Contain("Characters"));
        Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(secret), Is.EqualTo("{}"));
        response.Dispose(); response.Dispose();
        Assert.That(buffer, Is.All.EqualTo('\0'));
        Assert.Throws<ObjectDisposedException>(() => _ = secret.Characters);
    }

    [Test]
    public void UserCancellationIsNotSuccessAndStopsFollowingStatements()
    {
        var runner = new CryptoScriptRunner(); bool continued = false;
        runner.PasswordRequested += (_, request) => Task.FromResult(PasswordResponse.UserCancelled(request.RequestId));
        Assert.ThrowsAsync<SecurePasswordCancelledException>(async () => await runner.ExecuteAsync(new StatementNode[]
        { Request(), new AsyncInfrastructureStatement(_ => { continued = true; return Task.CompletedTask; }) }));
        Assert.That(continued, Is.False); Assert.That(InterpreterExecution.IsBusy, Is.False);
    }

    [TestCase(false)] [TestCase(true)]
    public async Task CancellationObservesLateResponseOrFaultAndQuarantinesRunner(bool fault)
    {
        var runner = new CryptoScriptRunner();
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource<PasswordResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        PasswordRequestEventArgs? request = null;
        PasswordRequestHandler handler = (_, args) => { request = args; return pending.Task; };
        runner.PasswordRequested += handler;
        var execution = Run(runner, token: cancellation.Token);
        Assert.That(request, Is.Not.Null);
        cancellation.Cancel();
        try { await execution; Assert.Fail("Must cancel"); } catch (OperationCanceledException) { }
        Assert.That(execution.IsCanceled, Is.True);
        Assert.That(Assert.ThrowsAsync<PasswordProtocolException>(async () => await Run(runner))!.Code,
            Is.EqualTo(PasswordProtocolError.PasswordRequestBusy));
        char[] secret = "canary".ToCharArray();
        if (fault) pending.SetException(new Exception("LATE-CANARY"));
        else pending.SetResult(Supply(request!, secret));
        // Bounded condition polling, not timing-based correctness of cancellation.
        runner.PasswordRequested -= handler;
        runner.PasswordRequested += (_, args) => Task.FromResult(Supply(args));
        for (int attempt = 0; ; attempt++)
        {
            try { await Run(runner); break; }
            catch (PasswordProtocolException e) when (e.Code == PasswordProtocolError.PasswordRequestBusy && attempt < 100)
            { await Task.Delay(10); }
        }
        if (!fault) Assert.That(secret, Is.All.EqualTo('\0'));
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    }

    [Test]
    public async Task ReentrancyParallelRunnersAndParsingFailFastDuringRequest()
    {
        var runner = new CryptoScriptRunner();
        var pending = new TaskCompletionSource<PasswordResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        PasswordRequestEventArgs? request = null;
        runner.PasswordRequested += async (_, args) =>
        {
            request = args;
            Assert.Throws<ExecutionBusyException>(() => runner.Execute(Array.Empty<StatementNode?>()));
            Assert.ThrowsAsync<ExecutionBusyException>(async () => await runner.ExecuteAsync(Array.Empty<StatementNode?>()));
            Assert.Throws<ExecutionBusyException>(() => VariableDictionary.Instance().Clear());
            return await pending.Task;
        };
        var execution = Run(runner);
        Assert.ThrowsAsync<ExecutionBusyException>(async () => await new CryptoScriptRunner().ExecuteAsync(Array.Empty<StatementNode?>()));
        Assert.Throws<ExecutionBusyException>(() => ParserBuilder.StringBuild("VAR a=0x(01)"));
        pending.SetResult(Supply(request!)); await execution;
        await runner.ExecuteAsync(Array.Empty<StatementNode?>());
    }

    [Test]
    public async Task IteratorCleanupRetainsStateAccessOnCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        bool cleaned = false;
        IEnumerable<StatementNode?> Statements()
        {
            try
            {
                yield return new AsyncInfrastructureStatement(_ =>
                { cancellation.Cancel(); return Task.CompletedTask; });
                yield return null;
            }
            finally { _ = VariableDictionary.Instance().GetVariables(); cleaned = true; }
        }
        var execution = new CryptoScriptRunner().ExecuteAsync(Statements(), cancellation.Token);
        try { await execution; Assert.Fail("Must cancel"); } catch (OperationCanceledException) { }
        Assert.That(execution.IsCanceled, Is.True);
        Assert.That(cleaned, Is.True);
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    }

    [Test]
    public async Task InheritedExecutionContextDoesNotAuthorizeConcurrentStateAccess()
    {
        await InterpreterExecution.RunAsync(async () =>
        {
            // The async callback itself must not hold a thread-bound state lease:
            // its continuation may run before the synchronous prefix has unwound.
            Assert.Throws<ExecutionBusyException>(() => VariableDictionary.Instance().Clear());
            await Task.Run(() =>
            {
                Assert.Throws<ExecutionBusyException>(() => VariableDictionary.Instance().Clear());
                Assert.Throws<ExecutionBusyException>(() => ParserBuilder.StringBuild("VAR a=0x(01)"));
            });
            return InterpreterExecution.AccessState(() => VariableDictionary.Instance().GetVariables().Count());
        });
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    }

    [Test]
    public async Task CompletedFinalOperationIsNotRetroactivelyCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var runner = new CryptoScriptRunner();
        await runner.ExecuteAsync(new[] { new AsyncInfrastructureStatement(_ =>
        { cancellation.Cancel(); return Task.CompletedTask; }) }, cancellation.Token);
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    }
}
