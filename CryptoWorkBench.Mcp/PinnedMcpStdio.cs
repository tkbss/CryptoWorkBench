using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CryptoWorkBench.Mcp;

/// <summary>Transport-only SDK bridge. All authority and business state remain in the Workbench.</summary>
public static class PinnedMcpStdio
{
    public static async Task<int> RunAsync()
    {
        try
        {
            string pin = Environment.GetEnvironmentVariable("CRYPTOWORKBENCH_MCP_CERT_SHA256") ?? "";
            string token = Environment.GetEnvironmentVariable("CRYPTOWORKBENCH_MCP_TOKEN") ?? "";
            var endpoint = new Uri(Environment.GetEnvironmentVariable("CRYPTOWORKBENCH_MCP_ENDPOINT") ?? LocalMcpHost.Endpoint);
            // No child processes are started; do not retain the inherited credential in the environment.
            Environment.SetEnvironmentVariable("CRYPTOWORKBENCH_MCP_TOKEN", null);
            await RunAsync(endpoint, pin, token, CancellationToken.None);
            return 0;
        }
        catch
        {
            await Console.Error.WriteLineAsync("Workbench connection unavailable. Verify pairing and start a new client. Do not automatically repeat mutations.");
            return 1;
        }
    }

    internal static HttpClient CreateHttpClient(Uri endpoint, string fingerprint, string token)
    {
        if (endpoint.Scheme != "https" || endpoint.Host != "127.0.0.1" || endpoint.AbsolutePath != "/mcp" ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0 ||
            fingerprint.Length != 64 || token.Length != 64) throw new ArgumentException("Invalid local pairing.");
        byte[] pin = Convert.FromHexString(fingerprint);
        CryptographicOperations.ZeroMemory(Convert.FromHexString(token));
        var handler = new SocketsHttpHandler
        {
            UseProxy = false, AllowAutoRedirect = false, UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            // No pooled connection or negotiated HTTP/2 replay after an ambiguous write.
            PooledConnectionLifetime = TimeSpan.Zero,
            SslOptions = new()
            {
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    certificate is System.Security.Cryptography.X509Certificates.X509Certificate2 peer &&
                    DateTime.UtcNow >= peer.NotBefore.ToUniversalTime() && DateTime.UtcNow < peer.NotAfter.ToUniversalTime() &&
                    CryptographicOperations.FixedTimeEquals(SHA256.HashData(peer.RawData), pin)
            }
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(150), DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        client.DefaultRequestHeaders.ConnectionClose = true;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    internal static async Task RunAsync(Uri endpoint, string pin, string token, CancellationToken ct)
    {
        using var http = CreateHttpClient(endpoint, pin, token);
        await using var transport = new HttpClientTransport(new()
        {
            Endpoint = endpoint, TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(10), EnableStandaloneGetStream = false,
            MaxReconnectionAttempts = 0
        }, http);
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);
        var catalog = (await client.ListToolsAsync(cancellationToken: ct)).Select(tool => tool.ProtocolTool).ToArray();
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
        long sequence = 0;
        builder.Services.AddMcpServer().WithStdioServerTransport()
            .WithMessageFilters(filters => filters.AddOutgoingFilter(next => async (context, cancellation) =>
            {
                if (context.JsonRpcMessage is JsonRpcError error)
                {
                    error.Error.Message = error.Error.Code == -32601 ? "Method not found." : "Invalid or failed Workbench request.";
                    error.Error.Data = null;
                }
                await next(context, cancellation);
            }))
            .WithListToolsHandler((_, _) => ValueTask.FromResult(new ListToolsResult { Tools = catalog }))
            .WithCallToolHandler(async (request, cancellation) =>
            {
                if (request.Params == null || !catalog.Any(tool => tool.Name == request.Params.Name))
                    throw new McpProtocolException("Unknown tool.", McpErrorCode.InvalidParams);
                try
                {
                    // Exactly one raw SDK request. Do not use the higher-level MRTR continuation /
                    // elicitation retry helper. No reconnect, reinitialization or mutation replay here.
                    var response = await client.SendRequestAsync(new JsonRpcRequest
                    {
                        Id = new RequestId(Interlocked.Increment(ref sequence)), Method = "tools/call",
                        Params = JsonSerializer.SerializeToNode(request.Params, McpJsonUtilities.DefaultOptions)
                    }, cancellation);
                    return response.Result!.Deserialize<CallToolResult>(McpJsonUtilities.DefaultOptions)!;
                }
                catch
                {
                    throw new McpProtocolException("Workbench request failed; outcome may be unknown. Do not automatically repeat mutations.", McpErrorCode.InternalError);
                }
            });
        using var server = builder.Build();
        await server.RunAsync(ct);
    }
}
