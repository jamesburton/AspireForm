using System.Net;
using System.Text;

namespace AspireForm.Mcp;

/// <summary>JSON-RPC transport over HTTP. Accepts POST requests at <c>/mcp/messages</c> on localhost. Each request body is one JSON-RPC frame; the response body is the JSON-RPC response.</summary>
public sealed class HttpTransport : ITransport
{
    private readonly int _port;

    /// <summary>Creates an HTTP transport bound to the given localhost port.</summary>
    public HttpTransport(int port)
    {
        _port = port;
    }

    /// <summary>The port the transport will bind. Useful when the caller passed 0 and wants to know the assigned port — but <see cref="HttpListener"/> doesn't support port 0 so callers must supply a real port.</summary>
    public int Port => _port;

    /// <inheritdoc />
    public async Task RunAsync(McpServer server, CancellationToken ct)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{_port}/");
        listener.Start();

        using var _ = ct.Register(listener.Stop);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    return; // listener.Stop was called.
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                /* fire-and-forget; exceptions caught inside HandleAsync */
                var __ = HandleAsync(context, server, ct);
            }
        }
        finally
        {
            if (listener.IsListening) listener.Stop();
        }
    }

    private static async Task HandleAsync(HttpListenerContext context, McpServer server, CancellationToken ct)
    {
        try
        {
            if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/mcp/messages")
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(ct);
            var response = await server.DispatchAsync(body, ct);

            context.Response.ContentType = "application/json";
            if (response is null)
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(response);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, ct);
            context.Response.Close();
        }
        catch (Exception)
        {
            try { context.Response.StatusCode = 500; context.Response.Close(); }
            catch { /* nothing we can do once the response is wedged */ }
        }
    }
}
