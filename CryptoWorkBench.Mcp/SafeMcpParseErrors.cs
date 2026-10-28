using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CryptoWorkBench.Mcp;

/// <summary>Attach to the MCP endpoint; install the middleware after UseRouting.</summary>
public sealed class McpParseErrorProtectionMetadata;

/// <summary>Suppresses input-derived parser details in SDK HTTP 400 responses for marked endpoints.
/// Install after UseRouting so protection follows endpoint selection, including route aliases.
/// Successful SSE/JSON writes are forwarded immediately; requests are never parsed here.</summary>
public static class SafeMcpParseErrors
{
    public static IApplicationBuilder UseSafeMcpParseErrors(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<McpParseErrorProtectionMetadata>() is null)
        { await next(context); return; }
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
        finally { context.Response.Body = original; }
    });

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
