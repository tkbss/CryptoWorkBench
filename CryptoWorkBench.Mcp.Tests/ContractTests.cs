using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoWorkBench.Automation.Abstractions;
using Json.Schema;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

[TestFixture]
public class ContractTests
{
    private static readonly Guid Id = Guid.Parse("12345678-1234-1234-1234-123456789abc");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-22T12:00:00Z");
    private static readonly Diagnostic ErrorDiagnostic = new(DiagnosticKind.Semantic, DiagnosticSeverity.Error,
        "Check", "Check failed.", null, null, 1, null, null, null, null, null);
    public static IEnumerable<object> Samples()
    {
        yield return new EditorEdit(Id, Id, "0", EditorEditMode.Insert, "abc", 0);
        yield return new EditorExecution(Id, Id, Id, "0", "0");
        yield return new TextExecution(Id, Id, "0", "abc", TextExecutionMode.Submission);
        yield return new WorkbenchStatus(Lifecycle.Ready, false, false, WorkbenchExecutionState.Idle, null, false, "999999999999999999999999", null, []);
        yield return new EditorSnapshot(Id, "1", "abc", 3, false, DirtyState.Unknown);
        yield return new EditorMutation(Id, "0", "1", 3, DirtyState.Dirty);
        yield return FakeAutomationService.Completed(Id);
        yield return new DiagnosticSnapshot(Id, true, [ErrorDiagnostic], false);
        yield return ErrorDiagnostic;
        yield return new Output(1, OutputKind.Print, null, true, false);
        yield return FakeAutomationService.Variables;
        yield return new VariableInfo("key", "KEY", true, "AES", "symmetric", 128, "AES-CBC", false);
        yield return new CancelReceipt(Id, false, ExecutionState.Completed);
        yield return AutomationErrors.Create(AutomationErrorCode.Busy);
        yield return Reply<ExecutionResult>.Success(Id, FakeAutomationService.Completed(Id));
        yield return Reply<WorkbenchStatus>.Failure(Id, AutomationErrors.Create(AutomationErrorCode.Forbidden));
    }
    private static string Serialize(object value)
    {
        AutomationJson.Validate(value);
        return JsonSerializer.Serialize(value, value.GetType(), AutomationJson.Options);
    }
    private static object Deserialize(string json, Type type) => typeof(AutomationJson).GetMethod(nameof(AutomationJson.Deserialize))!.MakeGenericMethod(type).Invoke(null, [json])!;
    private static JsonElement Schema(Type type) => (JsonElement)typeof(AutomationSchemas).GetMethod(nameof(AutomationSchemas.For))!.MakeGenericMethod(type).Invoke(null, null)!;
    private static bool ValidSchema(Type type, JsonNode node) => JsonSchema.FromText(Schema(type).GetRawText()).Evaluate(JsonSerializer.SerializeToElement(node)).IsValid;

    [TestCaseSource(nameof(Samples))]
    public void RoundtripAndSchema(object sample)
    {
        var json = Serialize(sample);
        var copy = Deserialize(json, sample.GetType());
        Assert.That(Serialize(copy), Is.EqualTo(json));
        Assert.That(ValidSchema(sample.GetType(), JsonNode.Parse(json)!), Is.True, Schema(sample.GetType()).GetRawText());
    }
    [TestCaseSource(nameof(Samples))]
    public void RequiredNullableAndUnknownFields(object sample)
    {
        var type = sample.GetType();
        var source = JsonNode.Parse(Serialize(sample))!.AsObject();
        var required = Schema(type).GetProperty("required").EnumerateArray().Select(x => x.GetString()!).ToArray();
        foreach (var name in required)
        {
            var missing = source.DeepClone().AsObject(); missing.Remove(name);
            Assert.Throws<TargetInvocationException>(() => Deserialize(missing.ToJsonString(), type), name);
            Assert.That(ValidSchema(type, missing), Is.False, name);
        }
        foreach (var property in type.GetProperties())
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            var nullable = Nullable.GetUnderlyingType(property.PropertyType) is not null ||
                new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;
            var changed = source.DeepClone().AsObject(); changed[name] = null;
            if (!nullable)
            {
                Assert.Throws<TargetInvocationException>(() => Deserialize(changed.ToJsonString(), type), name);
                Assert.That(ValidSchema(type, changed), Is.False, name);
            }
        }
        source["secret-marker"] = "secret-value";
        Assert.Throws<TargetInvocationException>(() => Deserialize(source.ToJsonString(), type));
        Assert.That(ValidSchema(type, source), Is.False);
    }
    [Test]
    public void EnumTextsAreExactAndNumericValuesRejected()
    {
        var expected = new Dictionary<Type, string[]>
        {
            [typeof(Lifecycle)] = ["starting","ready","stopping","stopped","faulted"],
            [typeof(WorkbenchExecutionState)] = ["idle","awaitingConfirmation","running","waitingForUser","cancellationRequested"],
            [typeof(ExecutionState)] = ["awaitingConfirmation","running","waitingForUser","cancellationRequested","completed","syntaxError","aborted","cancelled","timedOut","failed","rejected"],
            [typeof(CancellationReason)] = ["explicit","timeout","shutdown","mcpDisabled","user"],
            [typeof(ExecutionSource)] = ["editor","text","guiLine","scriptBookFile"],
            [typeof(DirtyState)] = ["clean","dirty","unknown"], [typeof(EditorEditMode)] = ["insert","append","replace"],
            [typeof(OutputPolicy)] = ["metadataOnly","includeText"], [typeof(TextExecutionMode)] = ["submission","scriptBook"],
            [typeof(Effects)] = ["none","possible"], [typeof(DiagnosticKind)] = ["syntax","semantic","host"],
            [typeof(DiagnosticSeverity)] = ["warning","error"], [typeof(OutputKind)] = ["print","info"]
        };
        foreach (var pair in expected)
        {
            Assert.That(Enum.GetValues(pair.Key).Cast<object>().Select(x => JsonSerializer.Serialize(x, pair.Key, AutomationJson.Options).Trim('"')), Is.EqualTo(pair.Value));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("0", pair.Key, AutomationJson.Options));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("\"INVALID\"", pair.Key, AutomationJson.Options));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(JsonSerializer.Serialize(pair.Value[0].ToUpperInvariant()), pair.Key, AutomationJson.Options));
        }
    }
    [TestCase("")][TestCase("-1")][TestCase("1.0")][TestCase("1e9")][TestCase(" 1")][TestCase("١")]
    public void InvalidRevision(string revision) => Assert.Throws<JsonException>(() => AutomationJson.Serialize(new EditorEdit(Id, Id, revision, EditorEditMode.Append, "")));
    [Test]
    public void UuidUtcAndOptionalInputs()
    {
        var request = new EditorExecution(Id, Id, Id, "0", "0");
        var json = AutomationJson.Serialize(request);
        Assert.That(json, Does.Contain(Id.ToString("D")));
        Assert.That(json, Does.Not.Contain("timeoutMs"));
        foreach (var key in new[] { "timeoutMs", "outputPolicy" })
        {
            var node = JsonNode.Parse(json)!.AsObject(); node[key] = null;
            Assert.Throws<JsonException>(() => AutomationJson.Deserialize<EditorExecution>(node.ToJsonString()));
        }
        Assert.Throws<JsonException>(() => AutomationJson.Deserialize<EditorExecution>(json.Replace(Id.ToString("D"), Id.ToString("N"))));
        var snapshot = FakeAutomationService.Variables with { CapturedAt = Now };
        var timeJson = AutomationJson.Serialize(snapshot);
        Assert.That(timeJson, Does.Contain("Z\""));
        Assert.Throws<JsonException>(() => AutomationJson.Deserialize<VariableSnapshot>(timeJson.Replace("Z\"", "+02:00\"")));
        Assert.Throws<JsonException>(() => AutomationJson.Deserialize<VariableSnapshot>(timeJson.Replace("Z\"", "\"")));
        Assert.Throws<JsonException>(() => AutomationJson.Deserialize<EditorExecution>(json.Replace("\"expectedRevision\":\"0\"", "\"expectedRevision\":0")));
        Assert.Throws<JsonException>(() => AutomationJson.Deserialize<EditorExecution>(json.Replace("\"expectedRevision\":\"0\"", "\"expectedRevision\":\"0\",\"expectedRevision\":\"1\"")));
    }
    [Test]
    public void TextTimeoutCollectionAndOutputLimits()
    {
        var edit = new EditorEdit(Id, Id, "0", EditorEditMode.Append, new string('x', AutomationContracts.MaxTextLength));
        Assert.DoesNotThrow(() => AutomationJson.Serialize(edit));
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(edit with { Text = edit.Text + "x" }));
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(edit with { Offset = 0 }));
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(edit with { Mode = EditorEditMode.Insert }));
        foreach (var timeout in new[] { 999, 120001 }) Assert.Throws<JsonException>(() => AutomationJson.Serialize(new EditorExecution(Id, Id, Id, "0", "0", timeout)));
        foreach (var timeout in new[] { 1000, 120000 }) Assert.DoesNotThrow(() => AutomationJson.Serialize(new EditorExecution(Id, Id, Id, "0", "0", timeout)));
        var snapshot = new DiagnosticSnapshot(Id, false, Enumerable.Repeat(ErrorDiagnostic, 1000).ToImmutableArray(), false);
        Assert.DoesNotThrow(() => AutomationJson.Serialize(snapshot));
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(snapshot with { Items = snapshot.Items.Add(ErrorDiagnostic) }));
        var result = FakeAutomationService.Completed(Id) with { Outputs = [new(1, OutputKind.Print, new string('é', 32768), false, false)] };
        Assert.DoesNotThrow(() => AutomationJson.Serialize(result));
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(result with { Outputs = result.Outputs.Add(new(2, OutputKind.Info, "x", false, false)) }));
    }
    [TestCase("a\r\nb", 2, false)][TestCase("a\r\nb", 3, true)][TestCase("a😀b", 2, false)][TestCase("a😀b", 3, true)]
    [TestCase("abc", -1, false)][TestCase("abc", 4, false)][TestCase("abc", 3, true)]
    public void Utf16Offsets(string text, int offset, bool expected) => Assert.That(AutomationContracts.IsValidInsertionOffset(text, offset), Is.EqualTo(expected));
    [Test]
    public void SnapshotsAreDetachedAndContainNoSecretValueSlots()
    {
        var source = new[] { ErrorDiagnostic };
        var snapshot = new DiagnosticSnapshot(Id, true, source.ToImmutableArray(), false);
        source[0] = ErrorDiagnostic with { Message = "changed" };
        Assert.That(snapshot.Items[0].Message, Is.EqualTo("Check failed."));
        Assert.That(typeof(VariableInfo).GetProperties().Select(p => p.Name), Is.EquivalentTo(new[] { "Identifier","Type","IsKey","Algorithm","MaterialKind","KeySizeBits","Mechanism","ValueIncluded" }));
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(new VariableInfo("key", "KEY", true, null, null, null, null, true)));
        var assemblies = typeof(IWorkbenchAutomationService).Assembly.GetReferencedAssemblies().Concat(typeof(StatusTransport).Assembly.GetReferencedAssemblies());
        Assert.That(assemblies.Select(a => a.Name), Has.None.Contains("Avalonia").And.None.Contains("Prism").And.None.EqualTo("CryptoScript"));
        Assert.That(typeof(IWorkbenchAutomationService).GetMethods(), Has.Length.EqualTo(9));
    }
    [Test]
    public void ErrorEnvelopeAndSemanticErrorsRemainSeparate()
    {
        foreach (var code in Enum.GetValues<AutomationErrorCode>())
        {
            var reply = Reply<ExecutionResult>.Failure(Id, AutomationErrors.Create(code, Id));
            Assert.That(AutomationToolResults.Create(reply).IsError, Is.True);
            Assert.That(ValidSchema(reply.GetType(), JsonNode.Parse(AutomationJson.Serialize(reply))!), Is.True);
        }
        var completed = FakeAutomationService.Completed(Id) with { HasErrors = true, Effects = Effects.Possible, Diagnostics = [ErrorDiagnostic] };
        var success = Reply<ExecutionResult>.Success(Id, completed);
        Assert.That(AutomationToolResults.Create(success, executionCall: true).IsError, Is.False);
        foreach (var state in Enum.GetValues<ExecutionState>())
        {
            var reply = success with { Data = completed with { State = state } };
            Assert.That(AutomationToolResults.Create(reply).IsError, Is.False);
            Assert.That(AutomationToolResults.Create(reply, true).IsError, Is.EqualTo(AutomationContracts.IsTerminal(state) && state != ExecutionState.Completed));
        }
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(success with { Error = AutomationErrors.Create(AutomationErrorCode.Busy) }));
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(success with { Data = null }));
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(AutomationErrors.Create(AutomationErrorCode.InternalError) with { Message = "secret" }));
        Assert.Throws<JsonException>(() => AutomationJson.Serialize(success with { SchemaVersion = "2" }));
    }
    [Test]
    public async Task CancellationBeforeAdmissionAndExplicitCancellation()
    {
        var service = new FakeAutomationService();
        var request = new TextExecution(service.InstanceId, Id, "0", "", TextExecutionMode.Submission);
        Assert.Throws<OperationCanceledException>(() => service.ExecuteTextAsync(FakeAutomationService.Caller, request, new CancellationToken(true)));
        Assert.That(service.Admitted.Task.IsCompleted, Is.False);
        var job = service.ExecuteTextAsync(FakeAutomationService.Caller, request, CancellationToken.None);
        await service.Admitted.Task;
        await service.CancelExecutionAsync(FakeAutomationService.Caller, Id, CancellationToken.None);
        var result = await job;
        Assert.That(result.Data!.State, Is.EqualTo(ExecutionState.Cancelled));
        Assert.That(result.Data.CancellationReason, Is.EqualTo(CancellationReason.Explicit));
    }
    [Test]
    public async Task CancellingResponseWaitDoesNotSetExecutionCancellationReason()
    {
        var service = new FakeAutomationService();
        using var requestToken = new CancellationTokenSource();
        var wait = service.ExecuteTextAsync(FakeAutomationService.Caller,
            new(service.InstanceId, Id, "0", "", TextExecutionMode.Submission), requestToken.Token);
        await service.Admitted.Task;
        var deadline = service.Result!.DeadlineAt;
        requestToken.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await wait);
        Assert.That(service.Result.State, Is.EqualTo(ExecutionState.Running));
        Assert.That(service.Result.CancellationReason, Is.Null);
        Assert.That(service.Result.DeadlineAt, Is.EqualTo(deadline));
        service.FinishExecution.TrySetResult();
        await service.JobFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(service.Result.State, Is.EqualTo(ExecutionState.Completed));
        Assert.That(service.Result.DeadlineAt, Is.EqualTo(deadline));
    }
}
