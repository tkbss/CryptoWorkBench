using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Runtime.CompilerServices;
using CryptoWorkBench.Automation.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CryptoWorkBench.Mcp;

/// <summary>Attach to the MCP endpoint; install the middleware after UseRouting.</summary>
public sealed class McpParseErrorProtectionMetadata;

/// <summary>Suppresses input-derived parser details in SDK HTTP 400 responses for marked endpoints.
/// Install after UseRouting so protection follows endpoint selection, including route aliases.
/// Validates bounded POST bodies before SDK routing. Successful SSE/JSON writes are forwarded immediately.</summary>
public static class SafeMcpParseErrors
{
    // Only the validated POST owns this single-use receipt. It carries no credential.
    private sealed class ValidatedPost(RequestId? id, JsonNode? parameters, CancellationToken aborted)
    {
        private int consumed;
        internal void Invalidate() => Interlocked.Exchange(ref consumed, 1);
        internal bool Consume(JsonRpcRequest message, string name) =>
            id.HasValue && message.Method == "tools/call" && message.Id.Equals(id.Value) &&
            (string?)parameters?["name"] == name && JsonNode.DeepEquals(parameters, message.Params) &&
            // Claim once, then sample the original POST token. A prior abort always wins,
            // even if cancellation callbacks are delayed. No callback registration is needed.
            Interlocked.Exchange(ref consumed, 1) == 0 && !aborted.IsCancellationRequested;
    }

    // In stateless HTTP the SDK creates fresh server options for each POST. Bind the
    // receipt to that server identity, without flowing authority through ExecutionContext.
    private sealed record SessionBinding(IReadOnlyList<McpServerTool> RegisteredTools, ValidatedPost Post);
    private static readonly ConditionalWeakTable<McpServerOptions, SessionBinding> Calls = new();

    internal static bool BindValidatedPost(HttpContext http, McpServerOptions options, IReadOnlyList<McpServerTool> catalog)
    {
        if (http.Features.Get<ValidatedPost>() is not { } receipt) return false;
        Calls.Add(options, new(catalog, receipt));
        return true;
    }

    internal static bool ConsumeValidatedPost(RequestContext<CallToolRequestParams> request, McpServerTool tool, string name) =>
        Calls.TryGetValue(request.Server.ServerOptions, out var binding) && binding.RegisteredTools.Any(registered => ReferenceEquals(registered, tool)) &&
        binding.Post.Consume(request.JsonRpcRequest, name);

    public static IApplicationBuilder UseSafeMcpParseErrors(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<McpParseErrorProtectionMetadata>() is null)
        { await next(context); return; }
        var input = context.Request.Body;
        using var replay = new MemoryStream();
        if (HttpMethods.IsPost(context.Request.Method))
        {
            if (!await ValidateInputAsync(context, replay)) return;
            replay.Position = 0;
            context.Request.Body = replay;
        }
        var receipt = context.Features.Get<ValidatedPost>();
        var original = context.Response.Body;
        using var capture = new ErrorCaptureStream(context, original);
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == 400) context.Response.ContentLength = null;
            return Task.CompletedTask;
        });
        context.Response.Body = capture;
        try
        {
            await next(context);
            if (context.Response.StatusCode == 400)
            {
                var bytes = capture.ErrorBytes;
                if (capture.Overflow || IsParseError(bytes))
                    bytes = Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid JSON-RPC request."}}""");
                await original.WriteAsync(bytes, context.RequestAborted);
            }
        }
        finally
        {
            // A retained SDK server or tool must not outlive the POST’s validation receipt.
            receipt?.Invalidate();
            context.Features.Set<ValidatedPost>(null);
            context.Response.Body = original;
            context.Request.Body = input;
        }
    });

    private static async Task<bool> ValidateInputAsync(HttpContext context, MemoryStream replay)
    {
        const int limit = AutomationContracts.MaxHttpBodyBytes;
        var request = context.Request;
        var ct = context.RequestAborted;
        if (request.ContentLength > limit)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return false;
        }

        var reader = request.BodyReader;
        while (true)
        {
            var read = await reader.ReadAsync(ct);
            var buffer = read.Buffer;
            try
            {
                if (buffer.Length > limit - replay.Length)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return false;
                }
                foreach (var segment in buffer) replay.Write(segment.Span);
            }
            finally { reader.AdvanceTo(buffer.End); }
            if (read.IsCompleted) break;
        }

        const string invalid = """{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid JSON-RPC request."}}""";
        string response = invalid;
        int status = StatusCodes.Status400BadRequest;
        try
        {
            using var doc = JsonDocument.Parse(replay.GetBuffer().AsMemory(0, (int)replay.Length));
            var root = doc.RootElement;
            var error = CheckNames(root, Section.Envelope);
            bool toolCall = TryIdentifyToolCall(root, out var id, out var parameters);
            if (error == 0)
            {
                context.Features.Set(toolCall ? new ValidatedPost(id, JsonNode.Parse(parameters.GetRawText()), ct)
                    : new ValidatedPost(null, null, ct));
                return true;
            }
            if (error == -32602 && toolCall)
            {
                response = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = id.Id, error = new { code = error, message = "Invalid v2 arguments." } });
                status = StatusCodes.Status200OK;
            }
        }
        catch (JsonException) { }
        catch (InvalidOperationException) { } // Invalid escaped Unicode in a property name.
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(response, ct);
        return false;
    }

    private static bool TryIdentifyToolCall(JsonElement root, out RequestId id, out JsonElement parameters)
    {
        id = default;
        parameters = default;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("jsonrpc", out var version) || !IsString(version, "2.0") ||
            !root.TryGetProperty("method", out var method) || !IsString(method, "tools/call") ||
            !root.TryGetProperty("id", out var value) ||
            !root.TryGetProperty("params", out parameters) || parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(name.GetString()) ||
            parameters.TryGetProperty("_meta", out var meta) && meta.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Any(field => field.Name is not ("jsonrpc" or "method" or "id" or "params"))) return false;
        if (value.ValueKind == JsonValueKind.String) id = new RequestId(value.GetString()!);
        else if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number)) id = new RequestId(number);
        else return false;
        return true;
    }

    private static bool IsString(JsonElement value, string text) =>
        value.ValueKind == JsonValueKind.String && value.ValueEquals(text);

    private enum Section { Envelope, Params, Arguments, Other }

    private static int CheckNames(JsonElement value, Section section)
    {
        int error = 0;
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in value.EnumerateObject())
            {
                if (!names.Add(field.Name)) return section == Section.Arguments ? -32602 : -32600;
                var child = section switch
                {
                    Section.Envelope when field.NameEquals("params") => Section.Params,
                    Section.Params when field.NameEquals("arguments") => Section.Arguments,
                    Section.Arguments => Section.Arguments,
                    _ => Section.Other
                };
                var nested = CheckNames(field.Value, child);
                if (nested == -32600) return nested;
                if (nested != 0) error = nested;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
            {
                var nested = CheckNames(item, section == Section.Arguments ? section : Section.Other);
                if (nested == -32600) return nested;
                if (nested != 0) error = nested;
            }
        return error;
    }

    private static bool IsParseError(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var code)
                && code.TryGetInt32(out var number) && number is -32600 or -32700;
        }
        catch (JsonException) { return true; }
    }
    private sealed class ErrorCaptureStream(HttpContext context, Stream inner) : Stream
    {
        private readonly MemoryStream errors = new();
        public byte[] ErrorBytes => errors.ToArray();
        public bool Overflow { get; private set; }
        private bool Capture(ReadOnlySpan<byte> data)
        {
            if (context.Response.StatusCode != 400) return false;
            if (errors.Length + data.Length <= 16384) errors.Write(data); else Overflow = true;
            return true;
        }
        public override void Write(byte[] buffer, int offset, int count)
        { if (!Capture(buffer.AsSpan(offset, count))) inner.Write(buffer, offset, count); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        { if (!Capture(buffer.Span)) await inner.WriteAsync(buffer, ct); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();
        public override void Flush() { if (context.Response.StatusCode != 400) inner.Flush(); }
        public override Task FlushAsync(CancellationToken ct) => context.Response.StatusCode == 400 ? Task.CompletedTask : inner.FlushAsync(ct);
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) errors.Dispose(); base.Dispose(disposing); }
    }
}
