# AspireForm MCP Server — Plan 3.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `AspireForm 0.4.0` adding an `aspireform mcp` verb that exposes AspireForm's verbs as MCP tools (14 low-level + 3 macros) over stdio and HTTP transports — letting agents chat-construct an Aspire app without shelling out to the CLI.

**Architecture:** A thin in-house JSON-RPC 2.0 layer (per spec §2.6 fallback — chosen as primary to remove the `ModelContextProtocol` SDK API-stability risk in spec §10.1). One transport-agnostic `McpServer` dispatches `initialize`, `tools/list`, and `tools/call`. Two transports — `StdioTransport` (newline-delimited JSON over stdin/stdout) and `HttpTransport` (BCL `HttpListener` POST/response on localhost). Each tool is an `IToolHandler` that calls the same internal services (`ConfigLoader`, `PluginManager`, `Planner`, `Executor`, `StateStore`, `AspireCli`) the CLI commands use — no shell-out.

**Tech Stack:** .NET 10, Spectre.Console.Cli 0.55.0 (existing), `System.Text.Json` (BCL), `System.Net.HttpListener` (BCL — no new package deps), xUnit v3 3.2.2 on MTP, AwesomeAssertions 9.4.0.

**Solo-dev workflow:** Work in-place on `main`, no feature branch (per saved feedback memory).

---

## File map

**New (production):**

- `src/AspireForm/Mcp/IToolHandler.cs` — tool contract
- `src/AspireForm/Mcp/ToolResult.cs` — result/content records
- `src/AspireForm/Mcp/ToolRegistry.cs` — name → handler map
- `src/AspireForm/Mcp/McpServer.cs` — transport-agnostic dispatcher + JSON-RPC envelope handling
- `src/AspireForm/Mcp/JsonRpc.cs` — request/response/error records + parser
- `src/AspireForm/Mcp/ITransport.cs` — read/write/close contract for transports
- `src/AspireForm/Mcp/StdioTransport.cs` — newline-delimited JSON over stdin/stdout
- `src/AspireForm/Mcp/HttpTransport.cs` — `HttpListener` POST `/mcp/messages` endpoint
- `src/AspireForm/Mcp/ToolBase.cs` — shared `Resolve(projectDir)` + exception → `ToolResult` mapper
- `src/AspireForm/Mcp/Tools/ConfigTool.cs`
- `src/AspireForm/Mcp/Tools/PlanTool.cs`
- `src/AspireForm/Mcp/Tools/ApplyTool.cs`
- `src/AspireForm/Mcp/Tools/NewTool.cs`
- `src/AspireForm/Mcp/Tools/AddTool.cs`
- `src/AspireForm/Mcp/Tools/DestroyTool.cs`
- `src/AspireForm/Mcp/Tools/ImportTool.cs`
- `src/AspireForm/Mcp/Tools/StateListTool.cs`
- `src/AspireForm/Mcp/Tools/StateShowTool.cs`
- `src/AspireForm/Mcp/Tools/DoctorTool.cs`
- `src/AspireForm/Mcp/Tools/PluginListTool.cs`
- `src/AspireForm/Mcp/Tools/PluginInstallTool.cs`
- `src/AspireForm/Mcp/Tools/PluginUpdateTool.cs`
- `src/AspireForm/Mcp/Tools/PluginRemoveTool.cs`
- `src/AspireForm/Mcp/Tools/Macros/ScaffoldAspireAppWithDataTool.cs`
- `src/AspireForm/Mcp/Tools/Macros/AddCacheLayerTool.cs`
- `src/AspireForm/Mcp/Tools/Macros/AddAuthenticationTool.cs`
- `src/AspireForm/Cli/McpCommand.cs` — Spectre verb wiring

**Modified:**

- `src/AspireForm/AspireForm.csproj` — bump `0.3.2` → `0.4.0`
- `src/AspireForm/Program.cs` — register `mcp` verb
- `README.md` — add "Use with an agent" section + Claude Code / Claude Desktop config snippet
- `CHANGELOG.md` — `[0.4.0]` entry

**New (tests):**

- `tests/AspireForm.Tests/Mcp/McpServerTests.cs`
- `tests/AspireForm.Tests/Mcp/StdioTransportTests.cs`
- `tests/AspireForm.Tests/Mcp/HttpTransportTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/ConfigToolTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/PlanToolTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/ApplyToolTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/NewToolTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/AddToolTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/DestroyToolTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/ImportToolTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/StateToolsTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/DoctorToolTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/PluginToolsTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs`

---

## Task 1: Bump version and create Mcp namespace skeleton

**Files:**
- Modify: `src/AspireForm/AspireForm.csproj`

- [ ] **Step 1: Bump `<Version>0.3.2</Version>` to `<Version>0.4.0</Version>`** in `src/AspireForm/AspireForm.csproj`.

- [ ] **Step 2: Build to confirm no regression**

```bash
dotnet build
```

Expected: build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add src/AspireForm/AspireForm.csproj
git -c commit.gpgsign=false commit -m "chore: bump AspireForm to 0.4.0 for MCP server"
```

---

## Task 2: JSON-RPC envelope types

**Files:**
- Create: `src/AspireForm/Mcp/JsonRpc.cs`
- Create: `tests/AspireForm.Tests/Mcp/JsonRpcTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AspireForm.Tests/Mcp/JsonRpcTests.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class JsonRpcTests
{
    [Fact]
    public void Parse_request_extracts_id_method_params()
    {
        var json = """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"x"}}""";
        var req = JsonRpc.ParseRequest(json);
        req.Id.Should().Be(JsonNode.Parse("7"));
        req.Method.Should().Be("tools/call");
        req.Params!["name"]!.GetValue<string>().Should().Be("x");
    }

    [Fact]
    public void Success_response_serialises_with_result()
    {
        var resp = JsonRpc.Success(JsonNode.Parse("3"), new JsonObject { ["ok"] = true });
        var json = resp.ToJsonString();
        json.Should().Contain("\"jsonrpc\":\"2.0\"")
            .And.Contain("\"id\":3")
            .And.Contain("\"result\":{\"ok\":true}");
    }

    [Fact]
    public void Error_response_serialises_with_code_and_message()
    {
        var resp = JsonRpc.Error(JsonNode.Parse("4"), -32601, "method not found");
        var json = resp.ToJsonString();
        json.Should().Contain("\"id\":4")
            .And.Contain("\"code\":-32601")
            .And.Contain("\"message\":\"method not found\"");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet build
```

Expected: FAIL with "type or namespace 'JsonRpc' could not be found".

- [ ] **Step 3: Write the JSON-RPC primitives**

Create `src/AspireForm/Mcp/JsonRpc.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp;

/// <summary>JSON-RPC 2.0 envelope helpers. AspireForm's MCP server speaks the minimum JSON-RPC needed for the MCP tools subset (initialize, tools/list, tools/call).</summary>
public static class JsonRpc
{
    /// <summary>Standard JSON-RPC parse-error code.</summary>
    public const int ParseError = -32700;

    /// <summary>Standard JSON-RPC method-not-found code.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>Standard JSON-RPC invalid-params code.</summary>
    public const int InvalidParams = -32602;

    /// <summary>Standard JSON-RPC internal-error code.</summary>
    public const int InternalError = -32603;

    /// <summary>Server-defined error code for known AspireForm exceptions.</summary>
    public const int AspireFormServerError = -32001;

    /// <summary>A parsed JSON-RPC 2.0 request.</summary>
    public sealed record Request(JsonNode? Id, string Method, JsonObject? Params);

    /// <summary>Parses a single JSON-RPC request frame.</summary>
    /// <param name="json">UTF-8 JSON text containing one JSON-RPC request object.</param>
    /// <returns>The parsed request.</returns>
    /// <exception cref="JsonException">Thrown when the input is not a valid JSON-RPC request.</exception>
    public static Request ParseRequest(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject
            ?? throw new JsonException("JSON-RPC request must be an object.");
        var method = node["method"]?.GetValue<string>()
            ?? throw new JsonException("JSON-RPC request is missing 'method'.");
        return new Request(
            Id: node["id"]?.DeepClone(),
            Method: method,
            Params: node["params"] as JsonObject);
    }

    /// <summary>Builds a successful JSON-RPC response.</summary>
    /// <param name="id">The request id to echo back. May be null for notifications, but notifications get no response.</param>
    /// <param name="result">The result payload.</param>
    /// <returns>A JsonObject ready to serialise to the transport.</returns>
    public static JsonObject Success(JsonNode? id, JsonNode? result) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result,
    };

    /// <summary>Builds a JSON-RPC error response.</summary>
    /// <param name="id">The request id to echo back.</param>
    /// <param name="code">Error code.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="data">Optional error data payload.</param>
    /// <returns>A JsonObject ready to serialise to the transport.</returns>
    public static JsonObject Error(JsonNode? id, int code, string message, JsonNode? data = null)
    {
        var err = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        };
        if (data is not null)
        {
            err["data"] = data;
        }

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = err,
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.JsonRpcTests*"
```

Expected: 3/3 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Mcp/JsonRpc.cs tests/AspireForm.Tests/Mcp/JsonRpcTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add JSON-RPC envelope primitives"
```

---

## Task 3: Tool contract and registry

**Files:**
- Create: `src/AspireForm/Mcp/IToolHandler.cs`
- Create: `src/AspireForm/Mcp/ToolResult.cs`
- Create: `src/AspireForm/Mcp/ToolRegistry.cs`
- Create: `tests/AspireForm.Tests/Mcp/ToolRegistryTests.cs`

- [ ] **Step 1: Define result records**

Create `src/AspireForm/Mcp/ToolResult.cs`:

```csharp
namespace AspireForm.Mcp;

/// <summary>A single content chunk in a tool result. Mirrors the MCP "content" element.</summary>
/// <param name="Type">Content type. AspireForm tools always emit "text".</param>
/// <param name="Text">The text payload.</param>
public sealed record ToolContent(string Type, string Text);

/// <summary>Result of executing an MCP tool. <see cref="IsError"/> indicates a tool-level (recoverable) failure, distinct from a transport-level JSON-RPC error.</summary>
/// <param name="IsError">When true, the agent sees a tool-level failure but the JSON-RPC call still succeeded.</param>
/// <param name="Content">One or more content chunks describing the result or failure.</param>
public sealed record ToolResult(bool IsError, IReadOnlyList<ToolContent> Content)
{
    /// <summary>Convenience factory for a single-text success result.</summary>
    public static ToolResult Ok(string text) => new(false, [new ToolContent("text", text)]);

    /// <summary>Convenience factory for a single-text tool-level error result.</summary>
    public static ToolResult Fail(string text) => new(true, [new ToolContent("text", text)]);
}
```

- [ ] **Step 2: Define the tool handler contract**

Create `src/AspireForm/Mcp/IToolHandler.cs`:

```csharp
using System.Text.Json.Nodes;

namespace AspireForm.Mcp;

/// <summary>The contract every MCP tool implements. The MCP server dispatches <c>tools/call</c> requests to a registered handler matched by <see cref="Name"/>.</summary>
public interface IToolHandler
{
    /// <summary>The tool name surfaced to the agent (e.g. <c>aspireform_plan</c>).</summary>
    string Name { get; }

    /// <summary>Human-readable description surfaced to the agent.</summary>
    string Description { get; }

    /// <summary>JSON Schema describing the tool's input arguments. Returned by <c>tools/list</c>.</summary>
    JsonObject InputSchema { get; }

    /// <summary>Executes the tool with the supplied arguments.</summary>
    /// <param name="args">Arguments matching <see cref="InputSchema"/>; never null (use an empty object when there are no args).</param>
    /// <param name="ct">Cancellation token from the transport.</param>
    /// <returns>The structured tool result.</returns>
    Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct);
}
```

- [ ] **Step 3: Define the registry**

Create `src/AspireForm/Mcp/ToolRegistry.cs`:

```csharp
using System.Text.Json.Nodes;

namespace AspireForm.Mcp;

/// <summary>Name-indexed collection of registered MCP tool handlers.</summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IToolHandler> _handlers = new(StringComparer.Ordinal);

    /// <summary>Registers a tool handler. Throws if a handler with the same name is already registered.</summary>
    /// <param name="handler">The handler to register.</param>
    /// <exception cref="InvalidOperationException">A handler with the same name is already registered.</exception>
    public void Register(IToolHandler handler)
    {
        if (!_handlers.TryAdd(handler.Name, handler))
        {
            throw new InvalidOperationException($"Tool '{handler.Name}' is already registered.");
        }
    }

    /// <summary>True when a handler with the supplied name is registered.</summary>
    public bool Contains(string name) => _handlers.ContainsKey(name);

    /// <summary>Retrieves the handler for <paramref name="name"/>, or null when not registered.</summary>
    public IToolHandler? Get(string name) =>
        _handlers.TryGetValue(name, out var h) ? h : null;

    /// <summary>All registered handlers in registration order.</summary>
    public IReadOnlyCollection<IToolHandler> All => _handlers.Values;

    /// <summary>Builds the JSON payload returned from MCP <c>tools/list</c>.</summary>
    public JsonObject ListToolsPayload()
    {
        var tools = new JsonArray();
        foreach (var h in _handlers.Values)
        {
            tools.Add(new JsonObject
            {
                ["name"] = h.Name,
                ["description"] = h.Description,
                ["inputSchema"] = h.InputSchema.DeepClone(),
            });
        }

        return new JsonObject { ["tools"] = tools };
    }
}
```

- [ ] **Step 4: Write the tests**

Create `tests/AspireForm.Tests/Mcp/ToolRegistryTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class ToolRegistryTests
{
    private sealed class FakeTool(string name) : IToolHandler
    {
        public string Name => name;
        public string Description => $"fake {name}";
        public JsonObject InputSchema => new() { ["type"] = "object" };
        public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
            Task.FromResult(ToolResult.Ok($"called {name}"));
    }

    [Fact]
    public void Register_then_Get_returns_handler()
    {
        var r = new ToolRegistry();
        var t = new FakeTool("aspireform_test");
        r.Register(t);
        r.Get("aspireform_test").Should().BeSameAs(t);
        r.Contains("aspireform_test").Should().BeTrue();
    }

    [Fact]
    public void Register_duplicate_throws()
    {
        var r = new ToolRegistry();
        r.Register(new FakeTool("dup"));
        var act = () => r.Register(new FakeTool("dup"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Get_missing_returns_null()
    {
        var r = new ToolRegistry();
        r.Get("missing").Should().BeNull();
    }

    [Fact]
    public void ListToolsPayload_emits_each_handler_metadata()
    {
        var r = new ToolRegistry();
        r.Register(new FakeTool("a"));
        r.Register(new FakeTool("b"));
        var payload = r.ListToolsPayload();
        var tools = payload["tools"] as JsonArray;
        tools.Should().NotBeNull();
        tools!.Count.Should().Be(2);
        tools[0]!["name"]!.GetValue<string>().Should().Be("a");
        tools[1]!["name"]!.GetValue<string>().Should().Be("b");
        tools[0]!["description"]!.GetValue<string>().Should().Be("fake a");
        (tools[0]!["inputSchema"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("object");
    }
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.ToolRegistryTests*"
```

Expected: 4/4 PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Mcp/ToolResult.cs src/AspireForm/Mcp/IToolHandler.cs src/AspireForm/Mcp/ToolRegistry.cs tests/AspireForm.Tests/Mcp/ToolRegistryTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add IToolHandler contract + ToolRegistry"
```

---

## Task 4: McpServer dispatcher

**Files:**
- Create: `src/AspireForm/Mcp/McpServer.cs`
- Create: `tests/AspireForm.Tests/Mcp/McpServerTests.cs`

- [ ] **Step 1: Implement the dispatcher**

Create `src/AspireForm/Mcp/McpServer.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp;

/// <summary>Transport-agnostic MCP server. Handles the subset of JSON-RPC methods needed for an MCP tools-only server: <c>initialize</c>, <c>tools/list</c>, and <c>tools/call</c>.</summary>
public sealed class McpServer
{
    private readonly ToolRegistry _registry;

    /// <summary>Creates a server bound to the supplied registry.</summary>
    public McpServer(ToolRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>Dispatches a single JSON-RPC request frame and returns the JSON-RPC response frame as a serialised string, or null for a notification (no id).</summary>
    /// <param name="requestJson">UTF-8 JSON text containing one JSON-RPC request object.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The serialised JSON-RPC response, or null when the request was a notification.</returns>
    public async Task<string?> DispatchAsync(string requestJson, CancellationToken ct)
    {
        JsonRpc.Request req;
        try
        {
            req = JsonRpc.ParseRequest(requestJson);
        }
        catch (JsonException ex)
        {
            return JsonRpc.Error(null, JsonRpc.ParseError, ex.Message).ToJsonString();
        }

        if (req.Id is null)
        {
            // Notification — fire-and-forget. We don't currently use notifications.
            return null;
        }

        JsonNode? result;
        try
        {
            result = req.Method switch
            {
                "initialize" => InitializeResult(),
                "tools/list" => _registry.ListToolsPayload(),
                "tools/call" => await CallToolAsync(req.Params, ct),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            return JsonRpc.Error(req.Id, JsonRpc.InternalError, ex.Message,
                new JsonObject { ["type"] = ex.GetType().Name }).ToJsonString();
        }

        if (result is null)
        {
            return JsonRpc.Error(req.Id, JsonRpc.MethodNotFound, $"Method not found: {req.Method}").ToJsonString();
        }

        return JsonRpc.Success(req.Id, result).ToJsonString();
    }

    private static JsonObject InitializeResult() => new()
    {
        ["protocolVersion"] = "2024-11-05",
        ["serverInfo"] = new JsonObject
        {
            ["name"] = "AspireForm",
            ["version"] = typeof(McpServer).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        },
        ["capabilities"] = new JsonObject
        {
            ["tools"] = new JsonObject(),
        },
    };

    private async Task<JsonNode> CallToolAsync(JsonObject? @params, CancellationToken ct)
    {
        var name = @params?["name"]?.GetValue<string>()
            ?? throw new InvalidOperationException("tools/call requires 'name'.");
        var args = @params?["arguments"] as JsonObject ?? [];
        var handler = _registry.Get(name)
            ?? throw new InvalidOperationException($"Unknown tool: {name}");

        var result = await handler.ExecuteAsync(args, ct);

        var content = new JsonArray();
        foreach (var c in result.Content)
        {
            content.Add(new JsonObject { ["type"] = c.Type, ["text"] = c.Text });
        }

        return new JsonObject
        {
            ["content"] = content,
            ["isError"] = result.IsError,
        };
    }
}
```

- [ ] **Step 2: Write the tests**

Create `tests/AspireForm.Tests/Mcp/McpServerTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class McpServerTests
{
    private sealed class EchoTool : IToolHandler
    {
        public string Name => "echo";
        public string Description => "echoes input";
        public JsonObject InputSchema => new() { ["type"] = "object" };
        public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
            Task.FromResult(ToolResult.Ok(args["text"]?.GetValue<string>() ?? ""));
    }

    private static McpServer NewServer(params IToolHandler[] handlers)
    {
        var r = new ToolRegistry();
        foreach (var h in handlers) r.Register(h);
        return new McpServer(r);
    }

    [Fact]
    public async Task Initialize_returns_protocol_version_and_capabilities()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize"}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        node!["result"]!["protocolVersion"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["result"]!["capabilities"]!["tools"].Should().NotBeNull();
    }

    [Fact]
    public async Task ToolsList_returns_registered_handlers()
    {
        var server = NewServer(new EchoTool());
        var resp = await server.DispatchAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        var tools = node!["result"]!["tools"] as JsonArray;
        tools!.Count.Should().Be(1);
        tools[0]!["name"]!.GetValue<string>().Should().Be("echo");
    }

    [Fact]
    public async Task ToolsCall_dispatches_to_handler_and_returns_content()
    {
        var server = NewServer(new EchoTool());
        var resp = await server.DispatchAsync(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hi"}}}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        var content = node!["result"]!["content"] as JsonArray;
        content![0]!["type"]!.GetValue<string>().Should().Be("text");
        content[0]!["text"]!.GetValue<string>().Should().Be("hi");
        node["result"]!["isError"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ToolsCall_unknown_tool_returns_internal_error()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync(
            """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"missing"}}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        node!["error"]!["code"]!.GetValue<int>().Should().Be(JsonRpc.InternalError);
        node["error"]!["message"]!.GetValue<string>().Should().Contain("missing");
    }

    [Fact]
    public async Task Unknown_method_returns_method_not_found()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync("""{"jsonrpc":"2.0","id":5,"method":"made/up"}""", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        node!["error"]!["code"]!.GetValue<int>().Should().Be(JsonRpc.MethodNotFound);
    }

    [Fact]
    public async Task Parse_error_returns_parse_error_response_with_null_id()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync("not json", default);
        var node = JsonNode.Parse(resp!) as JsonObject;
        node!["error"]!["code"]!.GetValue<int>().Should().Be(JsonRpc.ParseError);
        node["id"]!.GetValueKind().Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Notification_with_no_id_returns_null()
    {
        var server = NewServer();
        var resp = await server.DispatchAsync("""{"jsonrpc":"2.0","method":"tools/list"}""", default);
        resp.Should().BeNull();
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.McpServerTests*"
```

Expected: 7/7 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/McpServer.cs tests/AspireForm.Tests/Mcp/McpServerTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add McpServer JSON-RPC dispatcher (initialize / tools/list / tools/call)"
```

---

## Task 5: Transport contract + StdioTransport

**Files:**
- Create: `src/AspireForm/Mcp/ITransport.cs`
- Create: `src/AspireForm/Mcp/StdioTransport.cs`
- Create: `tests/AspireForm.Tests/Mcp/StdioTransportTests.cs`

- [ ] **Step 1: Define the transport contract**

Create `src/AspireForm/Mcp/ITransport.cs`:

```csharp
namespace AspireForm.Mcp;

/// <summary>A transport that hands the server one JSON-RPC frame at a time and writes responses back.</summary>
public interface ITransport
{
    /// <summary>Runs the transport read/dispatch loop until the input is closed or <paramref name="ct"/> fires.</summary>
    /// <param name="server">The server that dispatches each request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RunAsync(McpServer server, CancellationToken ct);
}
```

- [ ] **Step 2: Implement StdioTransport**

Create `src/AspireForm/Mcp/StdioTransport.cs`:

```csharp
namespace AspireForm.Mcp;

/// <summary>JSON-RPC transport over stdin/stdout, framed as newline-delimited JSON (one request per line, one response per line).</summary>
public sealed class StdioTransport : ITransport
{
    private readonly TextReader _input;
    private readonly TextWriter _output;

    /// <summary>Creates a stdio transport bound to the process stdin/stdout.</summary>
    public StdioTransport() : this(Console.In, Console.Out) { }

    /// <summary>Creates a stdio transport bound to the supplied reader/writer (used by tests).</summary>
    public StdioTransport(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
    }

    /// <inheritdoc />
    public async Task RunAsync(McpServer server, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _input.ReadLineAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (line is null)
            {
                return; // EOF — the client disconnected.
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var response = await server.DispatchAsync(line, ct);
            if (response is not null)
            {
                await _output.WriteLineAsync(response.AsMemory(), ct);
                await _output.FlushAsync(ct);
            }
        }
    }
}
```

- [ ] **Step 3: Write the test**

Create `tests/AspireForm.Tests/Mcp/StdioTransportTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class StdioTransportTests
{
    private sealed class EchoTool : IToolHandler
    {
        public string Name => "echo";
        public string Description => "echo";
        public JsonObject InputSchema => new() { ["type"] = "object" };
        public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
            Task.FromResult(ToolResult.Ok(args["text"]?.GetValue<string>() ?? ""));
    }

    [Fact]
    public async Task Reads_newline_delimited_requests_and_writes_responses()
    {
        var registry = new ToolRegistry();
        registry.Register(new EchoTool());
        var server = new McpServer(registry);

        var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""" + "\n" +
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hello"}}}""" + "\n");
        var output = new StringWriter();
        var transport = new StdioTransport(input, output);

        await transport.RunAsync(server, default);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(2);
        var first = JsonNode.Parse(lines[0]) as JsonObject;
        first!["result"]!["tools"]!.AsArray().Count.Should().Be(1);
        var second = JsonNode.Parse(lines[1]) as JsonObject;
        second!["result"]!["content"]![0]!["text"]!.GetValue<string>().Should().Be("hello");
    }

    [Fact]
    public async Task Stops_at_EOF()
    {
        var server = new McpServer(new ToolRegistry());
        var input = new StringReader("");
        var output = new StringWriter();
        var transport = new StdioTransport(input, output);

        await transport.RunAsync(server, default);

        output.ToString().Should().BeEmpty();
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.StdioTransportTests*"
```

Expected: 2/2 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Mcp/ITransport.cs src/AspireForm/Mcp/StdioTransport.cs tests/AspireForm.Tests/Mcp/StdioTransportTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add StdioTransport (newline-delimited JSON over stdin/stdout)"
```

---

## Task 6: HttpTransport (localhost POST endpoint)

**Files:**
- Create: `src/AspireForm/Mcp/HttpTransport.cs`
- Create: `tests/AspireForm.Tests/Mcp/HttpTransportTests.cs`

- [ ] **Step 1: Implement HttpTransport**

Create `src/AspireForm/Mcp/HttpTransport.cs`:

```csharp
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

                _ = HandleAsync(context, server, ct);
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
```

- [ ] **Step 2: Write the integration test**

Create `tests/AspireForm.Tests/Mcp/HttpTransportTests.cs`:

```csharp
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class HttpTransportTests
{
    private sealed class EchoTool : IToolHandler
    {
        public string Name => "echo";
        public string Description => "echo";
        public JsonObject InputSchema => new() { ["type"] = "object" };
        public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
            Task.FromResult(ToolResult.Ok(args["text"]?.GetValue<string>() ?? ""));
    }

    private static int FindFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task POST_mcp_messages_round_trips_a_tools_call()
    {
        var port = FindFreeTcpPort();
        var registry = new ToolRegistry();
        registry.Register(new EchoTool());
        var server = new McpServer(registry);
        var transport = new HttpTransport(port);

        using var cts = new CancellationTokenSource();
        var serverTask = transport.RunAsync(server, cts.Token);

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            var body = """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hi"}}}""";
            var resp = await http.PostAsync("/mcp/messages",
                new StringContent(body, Encoding.UTF8, "application/json"));

            resp.IsSuccessStatusCode.Should().BeTrue();
            var text = await resp.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(text) as JsonObject;
            node!["result"]!["content"]![0]!["text"]!.GetValue<string>().Should().Be("hi");
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task GET_root_returns_404()
    {
        var port = FindFreeTcpPort();
        var server = new McpServer(new ToolRegistry());
        var transport = new HttpTransport(port);

        using var cts = new CancellationTokenSource();
        var serverTask = transport.RunAsync(server, cts.Token);

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            var resp = await http.GetAsync("/");
            ((int)resp.StatusCode).Should().Be(404);
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.HttpTransportTests*"
```

Expected: 2/2 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/HttpTransport.cs tests/AspireForm.Tests/Mcp/HttpTransportTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add HttpTransport (localhost POST /mcp/messages)"
```

---

## Task 7: ToolBase shared helpers

**Files:**
- Create: `src/AspireForm/Mcp/ToolBase.cs`

- [ ] **Step 1: Implement the helpers**

Create `src/AspireForm/Mcp/ToolBase.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Mcp;

/// <summary>Shared helpers for tool handlers: project-dir resolution, input-schema building, and exception → ToolResult mapping.</summary>
internal static class ToolBase
{
    /// <summary>Resolves the project directory from the tool args, falling back to the supplied default.</summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="defaultDir">Default project dir, supplied by the server (usually the value from <c>--project-dir</c>).</param>
    /// <returns>An absolute, normalised path.</returns>
    public static string ResolveProjectDir(JsonObject args, string defaultDir)
    {
        var supplied = args["projectDir"]?.GetValue<string>();
        var dir = string.IsNullOrWhiteSpace(supplied) ? defaultDir : supplied;
        return Path.GetFullPath(dir);
    }

    /// <summary>Catches the common AspireForm exception set and converts to a tool-level failure result.</summary>
    /// <param name="action">The body to execute.</param>
    /// <returns>The result, or a tool-level failure result when a known exception is thrown.</returns>
    public static async Task<ToolResult> CatchKnownAsync(Func<Task<ToolResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ConfigValidationException ex) { return ToolResult.Fail($"Configuration error: {ex.Message}"); }
        catch (DependencyCycleException ex)   { return ToolResult.Fail($"Plan error: {ex.Message}"); }
        catch (ProviderNotFoundException ex)  { return ToolResult.Fail($"Plan error: {ex.Message}"); }
        catch (PluginContractException ex)    { return ToolResult.Fail($"Plugin error: {ex.Message}"); }
        catch (StateException ex)             { return ToolResult.Fail($"State error: {ex.Message}"); }
    }

    /// <summary>Builds a JSON-Schema "object" schema with the supplied properties and required keys.</summary>
    /// <param name="properties">Property name → schema object map.</param>
    /// <param name="required">Optional list of required property names.</param>
    public static JsonObject ObjectSchema(IDictionary<string, JsonObject> properties, params string[] required)
    {
        var props = new JsonObject();
        foreach (var (k, v) in properties)
        {
            props[k] = v.DeepClone();
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
        };

        if (required.Length > 0)
        {
            schema["required"] = new JsonArray(required.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray());
        }

        return schema;
    }

    /// <summary>JSON-Schema primitive: string with optional description.</summary>
    public static JsonObject Str(string? description = null) =>
        description is null ? new JsonObject { ["type"] = "string" } :
                              new JsonObject { ["type"] = "string", ["description"] = description };

    /// <summary>JSON-Schema primitive: boolean with optional description.</summary>
    public static JsonObject Bool(string? description = null) =>
        description is null ? new JsonObject { ["type"] = "boolean" } :
                              new JsonObject { ["type"] = "boolean", ["description"] = description };

    /// <summary>JSON-Schema primitive: array of strings.</summary>
    public static JsonObject StrArray(string? description = null) =>
        description is null ? new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } } :
                              new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = description };
}
```

- [ ] **Step 2: Build**

```bash
dotnet build
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/AspireForm/Mcp/ToolBase.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add ToolBase helpers (project-dir, exception mapping, schema builders)"
```

---

## Task 8: ConfigTool

**Files:**
- Create: `src/AspireForm/Mcp/Tools/ConfigTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/ConfigToolTests.cs`

- [ ] **Step 1: Implement**

Create `src/AspireForm/Mcp/Tools/ConfigTool.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Configuration;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: prints the fully merged + interpolated AspireForm configuration as indented JSON.</summary>
public sealed class ConfigTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (used when args omit <c>projectDir</c>).</summary>
    public ConfigTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_config";

    /// <inheritdoc />
    public string Description => "Print the fully merged and interpolated desired-state configuration as JSON.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["env"] = ToolBase.Str("Environment whose override file is layered over the base."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(() =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var env = args["env"]?.GetValue<string>();
            var loaded = new ConfigLoader().Load(projectDir, env);
            var json = loaded.Resolved.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return Task.FromResult(ToolResult.Ok(json));
        });
}
```

- [ ] **Step 2: Write the tests**

Create `tests/AspireForm.Tests/Mcp/Tools/ConfigToolTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class ConfigToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new ConfigTool(".");
        tool.Name.Should().Be("aspireform_config");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Returns_merged_config_as_indented_json()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");

        try
        {
            var tool = new ConfigTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("\"project\": \"Demo\"");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Returns_tool_level_error_for_missing_config()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-config-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new ConfigTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("Configuration error");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.ConfigToolTests*"
```

Expected: 3/3 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/Tools/ConfigTool.cs tests/AspireForm.Tests/Mcp/Tools/ConfigToolTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add ConfigTool"
```

---

## Task 9: PlanTool

**Files:**
- Create: `src/AspireForm/Mcp/Tools/PlanTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/PlanToolTests.cs`

- [ ] **Step 1: Implement**

Create `src/AspireForm/Mcp/Tools/PlanTool.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: renders the reconciliation diff between desired and current state.</summary>
public sealed class PlanTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PlanTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plan";

    /// <inheritdoc />
    public string Description => "Show the reconciliation diff between desired and current state (unified diffs).";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["env"] = ToolBase.Str("Environment whose override file is layered over the base."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var env = args["env"]?.GetValue<string>();
            var loaded = new ConfigLoader().Load(projectDir, env);
            var state = new StateStore().Load(projectDir);
            var registry = await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, ct);
            var plan = new Planner(registry).Plan(loaded.Model, state, projectDir);
            return ToolResult.Ok(PlanRenderer.Render(plan));
        });
}
```

- [ ] **Step 2: Write the tests**

Create `tests/AspireForm.Tests/Mcp/Tools/PlanToolTests.cs`:

```csharp
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class PlanToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new PlanTool(".");
        tool.Name.Should().Be("aspireform_plan");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Empty_config_returns_no_changes_plan()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-plan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        try
        {
            var tool = new PlanTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().NotBeNullOrEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Missing_config_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-plan-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new PlanTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.PlanToolTests*"
```

Expected: 3/3 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/Tools/PlanTool.cs tests/AspireForm.Tests/Mcp/Tools/PlanToolTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add PlanTool"
```

---

## Task 10: ApplyTool

**Files:**
- Create: `src/AspireForm/Mcp/Tools/ApplyTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/ApplyToolTests.cs`

- [ ] **Step 1: Implement**

Create `src/AspireForm/Mcp/Tools/ApplyTool.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: executes the plan. Always auto-approves (no interactive prompt over MCP).</summary>
public sealed class ApplyTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public ApplyTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_apply";

    /// <inheritdoc />
    public string Description => "Execute the plan. Auto-approves (no interactive prompt over MCP).";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["env"] = ToolBase.Str("Environment whose override file is layered over the base."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
        ["forceDrift"] = ToolBase.Bool("Apply even when drift has been detected on tracked files."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var env = args["env"]?.GetValue<string>();
            var forceDrift = args["forceDrift"]?.GetValue<bool>() ?? false;

            var loaded = new ConfigLoader().Load(projectDir, env);
            var stateStore = new StateStore();
            var prevState = stateStore.Load(projectDir);
            var registry = await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, ct);
            var plan = new Planner(registry).Plan(loaded.Model, prevState, projectDir);

            var sb = new StringBuilder();
            sb.Append(PlanRenderer.Render(plan));

            if (!plan.HasChanges)
            {
                sb.AppendLine("No changes.");
                return ToolResult.Ok(sb.ToString());
            }

            var executor = new Executor(new AspireCli(), stateStore);
            var result = await executor.ApplyAsync(plan, loaded.Model, prevState, projectDir,
                new ExecuteOptions { AutoApprove = true, ForceDrift = forceDrift }, ct);

            if (!result.Success)
            {
                return ToolResult.Fail(sb + Environment.NewLine + $"Apply failed: {result.FailureMessage}");
            }

            sb.AppendLine($"Applied {result.BlocksApplied} block(s).");
            return ToolResult.Ok(sb.ToString());
        });
}
```

- [ ] **Step 2: Write the tests**

Create `tests/AspireForm.Tests/Mcp/Tools/ApplyToolTests.cs`:

```csharp
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class ApplyToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new ApplyTool(".");
        tool.Name.Should().Be("aspireform_apply");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Apply_on_empty_config_reports_no_changes()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-apply-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        try
        {
            var tool = new ApplyTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("No changes");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.ApplyToolTests*"
```

Expected: 2/2 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/Tools/ApplyTool.cs tests/AspireForm.Tests/Mcp/Tools/ApplyToolTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add ApplyTool"
```

---

## Task 11: NewTool

**Files:**
- Create: `src/AspireForm/Mcp/Tools/NewTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/NewToolTests.cs`

- [ ] **Step 1: Implement**

Create `src/AspireForm/Mcp/Tools/NewTool.cs`:

```csharp
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: scaffolds a new Aspire AppHost project + starter <c>aspireform.yaml</c>. Mirrors the CLI <c>new</c> verb.</summary>
public sealed class NewTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (used as the default output root).</summary>
    public NewTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_new";

    /// <inheritdoc />
    public string Description => "Scaffold a new Aspire AppHost project and starter aspireform.yaml in <output>/<name>.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("The project name (required)."),
        ["output"] = ToolBase.Str("Output directory (defaults to the server's --project-dir)."),
    }, "name");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Fail("aspireform_new requires 'name'.");
        }

        var outputDir = args["output"]?.GetValue<string>() ?? _defaultProjectDir;
        var projectRoot = Path.GetFullPath(Path.Combine(outputDir, name));
        var appHostName = $"{name}.AppHost";

        if (Directory.Exists(projectRoot))
        {
            return ToolResult.Fail($"Refusing to scaffold into existing directory '{projectRoot}'.");
        }

        Directory.CreateDirectory(projectRoot);

        var (exitCode, stderr) = await RunDotnetNewAsync(appHostName, projectRoot, ct);
        if (exitCode != 0)
        {
            return ToolResult.Fail($"dotnet new aspire-apphost failed (exit {exitCode}): {stderr}");
        }

        WriteStarterYaml(projectRoot, name, appHostName);

        var summary =
            $"Created {projectRoot}{Environment.NewLine}" +
            $"  - {appHostName}/ (Aspire AppHost project){Environment.NewLine}" +
            $"  - aspireform.yaml (starter)";
        return ToolResult.Ok(summary);
    }

    private static async Task<(int ExitCode, string StandardError)> RunDotnetNewAsync(
        string appHostName, string workingDirectory, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("new");
        startInfo.ArgumentList.Add("aspire-apphost");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(appHostName);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        await Task.WhenAll(stderrTask, stdoutTask);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await stderrTask);
    }

    private static void WriteStarterYaml(string projectRoot, string projectName, string appHostName)
    {
        var content = $$"""
            aspireform:
              version: 1
              project: {{projectName}}
              apphost: {{appHostName}}
            resources: {}
            modules: {}
            """;
        File.WriteAllText(Path.Combine(projectRoot, "aspireform.yaml"), content);
    }
}
```

- [ ] **Step 2: Write the test**

Create `tests/AspireForm.Tests/Mcp/Tools/NewToolTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class NewToolTests
{
    [Fact]
    public void Name_description_and_required_input()
    {
        var tool = new NewTool(".");
        tool.Name.Should().Be("aspireform_new");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("name");
    }

    [Fact]
    public async Task Missing_name_returns_tool_level_error()
    {
        var tool = new NewTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'name'");
    }

    [Fact]
    public async Task Existing_directory_returns_tool_level_error()
    {
        var outDir = Path.Combine(Path.GetTempPath(), $"af-mcp-new-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo"));
        try
        {
            var tool = new NewTool(outDir);
            var result = await tool.ExecuteAsync(new JsonObject { ["name"] = "Demo" }, default);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("Refusing to scaffold");
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.NewToolTests*"
```

Expected: 3/3 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/Tools/NewTool.cs tests/AspireForm.Tests/Mcp/Tools/NewToolTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add NewTool"
```

---

## Task 12: AddTool

**Files:**
- Create: `src/AspireForm/Mcp/Tools/AddTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/AddToolTests.cs`

- [ ] **Step 1: Implement**

Create `src/AspireForm/Mcp/Tools/AddTool.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using YamlDotNet.Serialization;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: appends a Resource (default) or Module block to the AspireForm config file.</summary>
public sealed class AddTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public AddTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_add";

    /// <inheritdoc />
    public string Description => "Append a Resource (default) or Module block to the AspireForm config file.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["type"] = ToolBase.Str("Provider type (e.g. 'sqlserver', 'ef-data')."),
        ["name"] = ToolBase.Str("Block name (defaults to the provider type)."),
        ["module"] = ToolBase.Bool("Treat this block as a Module (default is Resource)."),
        ["dependsOn"] = ToolBase.StrArray("Block names this module depends on."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "type");

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(() =>
        {
            var type = args["type"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(type))
            {
                return Task.FromResult(ToolResult.Fail("aspireform_add requires 'type'."));
            }

            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var module = args["module"]?.GetValue<bool>() ?? false;
            var name = args["name"]?.GetValue<string>() ?? type;
            var dependsOn = (args["dependsOn"] as JsonArray)?
                .Select(n => n?.GetValue<string>())
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s!)
                .ToArray() ?? [];

            var configPath = FindConfigPath(projectDir);
            var format = ConfigFormatDetector.FromPath(configPath)
                ?? throw new ConfigValidationException($"Unrecognized configuration file: '{configPath}'.");
            IConfigParser parser = format == ConfigFormat.Yaml ? new YamlConfigParser() : new JsoncConfigParser();
            var dom = parser.Parse(File.ReadAllText(configPath));

            var section = module ? "modules" : "resources";
            if (dom[section] is not JsonObject blocks)
            {
                blocks = [];
                dom[section] = blocks;
            }

            if (blocks.ContainsKey(name))
            {
                return Task.FromResult(ToolResult.Fail($"Block '{name}' already exists in {section}."));
            }

            var newBlock = new JsonObject { ["type"] = type };
            if (module && dependsOn.Length > 0)
            {
                newBlock["dependsOn"] = new JsonArray(dependsOn.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray());
            }
            blocks[name] = newBlock;

            File.WriteAllText(configPath, Serialise(dom, format));
            return Task.FromResult(ToolResult.Ok(
                $"Added {section[..^1]} '{name}' ({type}) to {Path.GetFileName(configPath)}."));
        });

    private static string FindConfigPath(string projectDir)
    {
        string[] candidates = ["aspireform.yaml", "aspireform.yml", "aspireform.jsonc", "aspireform.json"];
        foreach (var n in candidates)
        {
            var path = Path.Combine(projectDir, n);
            if (File.Exists(path))
            {
                return path;
            }
        }
        throw new ConfigValidationException($"No AspireForm configuration file found in '{projectDir}'.");
    }

    private static string Serialise(JsonObject dom, ConfigFormat format) => format switch
    {
        ConfigFormat.Jsonc => dom.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n",
        ConfigFormat.Yaml => new SerializerBuilder().Build().Serialize(DomToPlain(dom)),
        _ => throw new InvalidOperationException(),
    };

    private static object? DomToPlain(JsonNode? node) => node switch
    {
        JsonObject obj => obj.ToDictionary(kvp => kvp.Key, kvp => DomToPlain(kvp.Value)),
        JsonArray arr => arr.Select(DomToPlain).ToList(),
        JsonValue v when v.TryGetValue(out string? s) => s,
        JsonValue v when v.TryGetValue(out bool b) => b,
        JsonValue v when v.TryGetValue(out long l) => l,
        JsonValue v when v.TryGetValue(out double d) => d,
        null => null,
        _ => node.ToString(),
    };
}
```

- [ ] **Step 2: Write the tests**

Create `tests/AspireForm.Tests/Mcp/Tools/AddToolTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class AddToolTests
{
    private static string MakeTempProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-add-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        return dir;
    }

    [Fact]
    public void Name_description_and_required_input()
    {
        var tool = new AddTool(".");
        tool.Name.Should().Be("aspireform_add");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("type");
    }

    [Fact]
    public async Task Adds_resource_block()
    {
        var dir = MakeTempProject();
        try
        {
            var tool = new AddTool(dir);
            var result = await tool.ExecuteAsync(new JsonObject { ["type"] = "sqlserver" }, default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("Added resource 'sqlserver'");
            File.ReadAllText(Path.Combine(dir, "aspireform.yaml")).Should().Contain("sqlserver");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Adds_module_block_with_dependsOn()
    {
        var dir = MakeTempProject();
        try
        {
            var tool = new AddTool(dir);
            var args = new JsonObject
            {
                ["type"] = "ef-data",
                ["name"] = "data",
                ["module"] = true,
                ["dependsOn"] = new JsonArray("sql"),
            };
            var result = await tool.ExecuteAsync(args, default);
            result.IsError.Should().BeFalse();
            var yaml = File.ReadAllText(Path.Combine(dir, "aspireform.yaml"));
            yaml.Should().Contain("data:").And.Contain("ef-data").And.Contain("sql");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Duplicate_block_returns_tool_level_error()
    {
        var dir = MakeTempProject();
        try
        {
            var tool = new AddTool(dir);
            await tool.ExecuteAsync(new JsonObject { ["type"] = "redis" }, default);
            var second = await tool.ExecuteAsync(new JsonObject { ["type"] = "redis" }, default);
            second.IsError.Should().BeTrue();
            second.Content[0].Text.Should().Contain("already exists");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.AddToolTests*"
```

Expected: 4/4 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/Tools/AddTool.cs tests/AspireForm.Tests/Mcp/Tools/AddToolTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add AddTool"
```

---

## Task 13: DestroyTool

**Files:**
- Create: `src/AspireForm/Mcp/Tools/DestroyTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/DestroyToolTests.cs`

- [ ] **Step 1: Implement (mirrors `DestroyCommand.cs` — pseudo-model + standard `Planner.Plan`)**

Create `src/AspireForm/Mcp/Tools/DestroyTool.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: destroys one block (when <c>block</c> is supplied) or all blocks (when omitted). Mirrors <c>DestroyCommand</c> — builds a pseudo-model with the targets removed from desired state so the standard planner emits Delete actions.</summary>
public sealed class DestroyTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public DestroyTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_destroy";

    /// <inheritdoc />
    public string Description => "Destroy one block (when 'block' is supplied) or all blocks.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["block"] = ToolBase.Str("Block name to destroy; omit to destroy all tracked blocks."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
        ["allowModuleDestroy"] = ToolBase.Bool("Permit destroying Module blocks (which are destroy-protected by default)."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var block = args["block"]?.GetValue<string>();
            var allowModuleDestroy = args["allowModuleDestroy"]?.GetValue<bool>() ?? false;

            var loaded = new ConfigLoader().Load(projectDir, env: null);
            var stateStore = new StateStore();
            var prevState = stateStore.Load(projectDir);

            // Decide which blocks to destroy.
            var targets = string.IsNullOrEmpty(block)
                ? prevState.Blocks.Keys.ToList()
                : [block];

            foreach (var name in targets)
            {
                if (!prevState.Blocks.TryGetValue(name, out var blockState))
                {
                    return ToolResult.Fail($"Block '{name}' is not tracked in state.");
                }
                if (!allowModuleDestroy
                    && string.Equals(blockState.Kind, "module", StringComparison.OrdinalIgnoreCase))
                {
                    return ToolResult.Fail(
                        $"Refusing to destroy module block '{name}': pass allowModuleDestroy=true to override.");
                }
            }

            var pseudoModel = BuildPseudoModelExcluding(loaded.Model, targets);
            var registry = await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, ct);
            var plan = new Planner(registry).Plan(pseudoModel, prevState, projectDir);

            if (!plan.Blocks.Any(b => b.Kind == BlockActionKind.Delete))
            {
                return ToolResult.Ok("Nothing to destroy.");
            }

            var executor = new Executor(new AspireCli(), stateStore);
            var result = await executor.ApplyAsync(plan, pseudoModel, prevState, projectDir,
                new ExecuteOptions { AutoApprove = true, ForceDrift = true }, ct);

            return result.Success
                ? ToolResult.Ok($"Destroyed {targets.Count} block(s).")
                : ToolResult.Fail($"Destroy failed: {result.FailureMessage}");
        });

    private static ProjectModel BuildPseudoModelExcluding(ProjectModel original, IReadOnlyList<string> exclude)
    {
        var ex = exclude.ToHashSet(StringComparer.Ordinal);
        return new ProjectModel
        {
            AspireForm = original.AspireForm,
            Resources = original.Resources.Where(r => !ex.Contains(r.Key)).ToDictionary(),
            Modules = original.Modules.Where(m => !ex.Contains(m.Key)).ToDictionary(),
            Profiles = original.Profiles,
        };
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build
```

Expected: build succeeds.

- [ ] **Step 3: Write the tests**

Create `tests/AspireForm.Tests/Mcp/Tools/DestroyToolTests.cs`:

```csharp
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class DestroyToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new DestroyTool(".");
        tool.Name.Should().Be("aspireform_destroy");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Empty_state_destroy_all_reports_nothing_to_destroy()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-destroy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        try
        {
            var tool = new DestroyTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("Nothing to destroy");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Unknown_block_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-destroy-unk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aspireform.yaml"),
            "aspireform:\n  version: 1\n  project: Demo\n  apphost: Demo.AppHost\nresources: {}\nmodules: {}\n");
        try
        {
            var tool = new DestroyTool(dir);
            var result = await tool.ExecuteAsync(new System.Text.Json.Nodes.JsonObject { ["block"] = "missing" }, default);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("not tracked");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.DestroyToolTests*"
```

Expected: 3/3 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Mcp/Tools/DestroyTool.cs tests/AspireForm.Tests/Mcp/Tools/DestroyToolTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add DestroyTool"
```

---

## Task 14: ImportTool

**Files:**
- Create: `src/AspireForm/Mcp/Tools/ImportTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/ImportToolTests.cs`

- [ ] **Step 1: Implement (mirrors `ImportCommand.cs` — provider.Plan + BlockState write)**

Create `src/AspireForm/Mcp/Tools/ImportTool.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: adopts an existing block into AspireForm state without executing. The block must already be declared in the AspireForm config file.</summary>
public sealed class ImportTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public ImportTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_import";

    /// <inheritdoc />
    public string Description => "Adopt an existing block (declared in the config file) into AspireForm state without executing.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["block"] = ToolBase.Str("Block name to import (required). Must already be declared in the config file."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "block");

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var blockName = args["block"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return ToolResult.Fail("aspireform_import requires 'block'.");
            }

            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, env: null);

            BlockKind blockKind;
            string blockType;
            JsonObject inputs;

            if (loaded.Model.Resources.TryGetValue(blockName, out var r))
            {
                blockKind = BlockKind.Resource;
                blockType = r.Type;
                inputs = r.Inputs;
            }
            else if (loaded.Model.Modules.TryGetValue(blockName, out var m))
            {
                blockKind = BlockKind.Module;
                blockType = m.Type;
                inputs = m.Inputs;
            }
            else
            {
                return ToolResult.Fail($"Block '{blockName}' is not declared in the config file.");
            }

            var registry = await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, ct);
            var provider = registry.Get(blockType);
            var providerCtx = new PlanContext(
                BlockName: blockName,
                Inputs: inputs,
                AppHostDirectory: loaded.Model.AspireForm.AppHost,
                ProjectName: loaded.Model.AspireForm.Project);
            var providerPlan = provider.Plan(providerCtx);

            var stateStore = new StateStore();
            var state = stateStore.Load(projectDir);

            var files = new Dictionary<string, FileState>(StringComparer.Ordinal);
            foreach (var planned in providerPlan.FileActions)
            {
                var absolute = Path.IsPathRooted(planned.Path)
                    ? planned.Path
                    : Path.GetFullPath(Path.Combine(projectDir, planned.Path));
                var checksum = File.Exists(absolute) ? DriftDetector.ComputeChecksum(absolute) : string.Empty;
                files[PathUtilities.ToRepoRelative(absolute, projectDir)] = new FileState
                {
                    OwnershipMode = planned.OwnershipMode.ToString().ToLowerInvariant(),
                    Checksum = checksum,
                };
            }

            state.Blocks[blockName] = new BlockState
            {
                Type = blockType,
                Kind = blockKind == BlockKind.Module ? "module" : "resource",
                Files = files,
                Inputs = inputs,
            };

            stateStore.Save(projectDir, state);
            return ToolResult.Ok($"Imported '{blockName}' ({blockType}, {files.Count} file(s)).");
        });
}
```

- [ ] **Step 2: Build**

```bash
dotnet build
```

Expected: build succeeds.

- [ ] **Step 3: Write the tests**

Create `tests/AspireForm.Tests/Mcp/Tools/ImportToolTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class ImportToolTests
{
    [Fact]
    public void Name_description_and_required_input()
    {
        var tool = new ImportTool(".");
        tool.Name.Should().Be("aspireform_import");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("block");
    }

    [Fact]
    public async Task Missing_block_returns_tool_level_error()
    {
        var tool = new ImportTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'block'");
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.ImportToolTests*"
```

Expected: 2/2 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Mcp/Tools/ImportTool.cs tests/AspireForm.Tests/Mcp/Tools/ImportToolTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add ImportTool"
```

---

## Task 15: State tools (list + show)

**Files:**
- Create: `src/AspireForm/Mcp/Tools/StateListTool.cs`
- Create: `src/AspireForm/Mcp/Tools/StateShowTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/StateToolsTests.cs`

- [ ] **Step 1: Implement StateListTool**

Create `src/AspireForm/Mcp/Tools/StateListTool.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: lists all tracked blocks as a simple table.</summary>
public sealed class StateListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public StateListTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_state_list";

    /// <inheritdoc />
    public string Description => "List all tracked blocks (name, type, kind).";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(() =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var state = new StateStore().Load(projectDir);
            if (state.Blocks.Count == 0)
            {
                return Task.FromResult(ToolResult.Ok("No tracked blocks."));
            }

            var sb = new StringBuilder();
            sb.AppendLine("Block        Kind      Type          Files");
            sb.AppendLine("-----        ----      ----          -----");
            foreach (var (name, block) in state.Blocks.OrderBy(b => b.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"{Pad(name, 12)} {Pad(block.Kind, 9)} {Pad(block.Type, 13)} {block.Files.Count}");
            }
            return Task.FromResult(ToolResult.Ok(sb.ToString()));

            static string Pad(string s, int width) => s.PadRight(width)[..Math.Max(width, s.Length)];
        });
}
```

- [ ] **Step 2: Implement StateShowTool**

Create `src/AspireForm/Mcp/Tools/StateShowTool.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: dumps one tracked block as indented JSON.</summary>
public sealed class StateShowTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Creates the tool with a default project directory.</summary>
    public StateShowTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_state_show";

    /// <inheritdoc />
    public string Description => "Show one tracked block's state as indented JSON.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["block"] = ToolBase.Str("Block name to show (required)."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "block");

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(() =>
        {
            var blockName = args["block"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return Task.FromResult(ToolResult.Fail("aspireform_state_show requires 'block'."));
            }

            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var state = new StateStore().Load(projectDir);
            if (!state.Blocks.TryGetValue(blockName, out var block))
            {
                return Task.FromResult(ToolResult.Fail($"Block '{blockName}' is not tracked in state."));
            }

            return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(block, PrettyOptions)));
        });
}
```

- [ ] **Step 3: Write the tests**

Create `tests/AspireForm.Tests/Mcp/Tools/StateToolsTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class StateToolsTests
{
    [Fact]
    public void StateListTool_metadata()
    {
        var tool = new StateListTool(".");
        tool.Name.Should().Be("aspireform_state_list");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void StateShowTool_metadata_requires_block()
    {
        var tool = new StateShowTool(".");
        tool.Name.Should().Be("aspireform_state_show");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("block");
    }

    [Fact]
    public async Task StateListTool_returns_no_blocks_message_for_empty_state()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new StateListTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("No tracked blocks");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task StateShowTool_unknown_block_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-state-show-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new StateShowTool(dir);
            var result = await tool.ExecuteAsync(new JsonObject { ["block"] = "missing" }, default);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("is not tracked");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.StateToolsTests*"
```

Expected: 4/4 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Mcp/Tools/StateListTool.cs src/AspireForm/Mcp/Tools/StateShowTool.cs tests/AspireForm.Tests/Mcp/Tools/StateToolsTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add StateListTool + StateShowTool"
```

---

## Task 16: DoctorTool

**Files:**
- Create: `src/AspireForm/Mcp/Tools/DoctorTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/DoctorToolTests.cs`

- [ ] **Step 1: Implement**

Create `src/AspireForm/Mcp/Tools/DoctorTool.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Aspire;
using AspireForm.Diagnostics;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: runs the prerequisite checker and returns the report.</summary>
public sealed class DoctorTool : IToolHandler
{
    /// <inheritdoc />
    public string Name => "aspireform_doctor";

    /// <inheritdoc />
    public string Description => "Check that AspireForm's prerequisites are installed.";

    /// <inheritdoc />
    public JsonObject InputSchema => new() { ["type"] = "object", ["properties"] = new JsonObject() };

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var report = await new PrerequisiteChecker(new AspireCli()).RunAsync(ct);
        var sb = new StringBuilder();
        foreach (var check in report.Checks)
        {
            var status = check.Ok ? "OK    " : "FAILED";
            sb.AppendLine($"[{status}] {check.Name}: {check.Detail}");
        }
        foreach (var failed in report.Checks.Where(c => !c.Ok && c.Remedy is not null))
        {
            sb.AppendLine($"  -> {failed.Name}: {failed.Remedy}");
        }
        return report.AllPassed ? ToolResult.Ok(sb.ToString()) : ToolResult.Fail(sb.ToString());
    }
}
```

- [ ] **Step 2: Write the test**

Create `tests/AspireForm.Tests/Mcp/Tools/DoctorToolTests.cs`:

```csharp
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class DoctorToolTests
{
    [Fact]
    public void Name_and_description_are_set()
    {
        var tool = new DoctorTool();
        tool.Name.Should().Be("aspireform_doctor");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Returns_at_least_one_check_in_the_report()
    {
        var tool = new DoctorTool();
        var result = await tool.ExecuteAsync([], default);
        // Whether IsError depends on the environment — we only assert the text is non-empty.
        result.Content[0].Text.Should().NotBeNullOrWhiteSpace();
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.DoctorToolTests*"
```

Expected: 2/2 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/Tools/DoctorTool.cs tests/AspireForm.Tests/Mcp/Tools/DoctorToolTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add DoctorTool"
```

---

## Task 17: Plugin tools (list / install / update / remove)

**Files:**
- Create: `src/AspireForm/Mcp/Tools/PluginListTool.cs`
- Create: `src/AspireForm/Mcp/Tools/PluginInstallTool.cs`
- Create: `src/AspireForm/Mcp/Tools/PluginUpdateTool.cs`
- Create: `src/AspireForm/Mcp/Tools/PluginRemoveTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/PluginToolsTests.cs`

- [ ] **Step 1: Implement PluginListTool**

Create `src/AspireForm/Mcp/Tools/PluginListTool.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: lists installed plugins from the lockfile.</summary>
public sealed class PluginListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PluginListTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plugin_list";

    /// <inheritdoc />
    public string Description => "List installed plugins from .aspireform/plugins.lock.yaml.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var lockfile = PluginLockfile.Load(projectDir);
        if (lockfile.Plugins.Count == 0)
        {
            return Task.FromResult(ToolResult.Ok("No plugins installed."));
        }

        var nameW = Math.Max(4, lockfile.Plugins.Max(p => p.Name.Length));
        var packageW = Math.Max(7, lockfile.Plugins.Max(p => p.Package.Length));
        var versionW = Math.Max(7, lockfile.Plugins.Max(p => p.Version.Length));

        var sb = new StringBuilder();
        sb.AppendLine($"{"Name".PadRight(nameW)} {"Package".PadRight(packageW)} Version");
        sb.AppendLine($"{"----".PadRight(nameW)} {"-------".PadRight(packageW)} {"-------".PadRight(versionW)}");
        foreach (var p in lockfile.Plugins)
        {
            sb.AppendLine($"{p.Name.PadRight(nameW)} {p.Package.PadRight(packageW)} {p.Version}");
        }

        return Task.FromResult(ToolResult.Ok(sb.ToString()));
    }
}
```

- [ ] **Step 2: Implement PluginInstallTool**

Create `src/AspireForm/Mcp/Tools/PluginInstallTool.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: installs a plugin by name (or <c>name@version</c>) and records it in the lockfile.</summary>
public sealed class PluginInstallTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PluginInstallTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plugin_install";

    /// <inheritdoc />
    public string Description => "Install a plugin by name (or 'name@version') and record it in the lockfile.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Plugin name or package id (e.g. 'Redis' or 'AspireForm.Plugin.Redis@0.1.0')."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "name");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Fail("aspireform_plugin_install requires 'name'.");
        }

        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var (packageId, version) = AspireForm.Cli.PluginInstallCommand.ParseNameAndVersion(name);

        var restorer = new PluginRestorer();
        PluginRestoreResult result;
        try
        {
            result = await restorer.RestoreAsync(packageId, version, projectDir, ct);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Plugin install error: {ex.Message}");
        }

        if (!result.Success)
        {
            return ToolResult.Fail($"Plugin install error: {result.ErrorMessage}");
        }

        var resolvedVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        var displayName = packageId.StartsWith("AspireForm.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? packageId["AspireForm.Plugin.".Length..]
            : packageId;

        var lockfile = PluginLockfile.Load(projectDir);
        lockfile.Plugins.RemoveAll(p => string.Equals(p.Package, packageId, StringComparison.OrdinalIgnoreCase));
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = displayName,
            Package = packageId,
            Version = resolvedVersion,
        });
        lockfile.Plugins.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        PluginLockfile.Save(projectDir, lockfile);

        return ToolResult.Ok($"Installed {displayName} ({packageId} {resolvedVersion}).");
    }
}
```

> If `PluginInstallCommand.ParseNameAndVersion` is currently `internal`, the implementer must either keep it accessible to the Mcp namespace (already same assembly) or extract it to a public helper.

- [ ] **Step 3: Implement PluginUpdateTool**

Create `src/AspireForm/Mcp/Tools/PluginUpdateTool.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: updates an installed plugin to the latest version.</summary>
public sealed class PluginUpdateTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PluginUpdateTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plugin_update";

    /// <inheritdoc />
    public string Description => "Update an installed plugin to the latest version.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Plugin name or package id."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "name");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Fail("aspireform_plugin_update requires 'name'.");
        }

        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var lockfile = PluginLockfile.Load(projectDir);

        // Mirror PluginUpdateCommand: look up by display Name (the lockfile field), not by Package.
        var entry = lockfile.Plugins.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return ToolResult.Fail($"Plugin '{name}' is not installed.");
        }

        var oldVersion = entry.Version;
        var restorer = new PluginRestorer();
        PluginRestoreResult result;
        try
        {
            result = await restorer.RestoreAsync(entry.Package, "*", projectDir, ct);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Plugin update error: {ex.Message}");
        }

        if (!result.Success)
        {
            return ToolResult.Fail($"Plugin update error: {result.ErrorMessage}");
        }

        var newVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        entry.Version = newVersion;
        PluginLockfile.Save(projectDir, lockfile);

        return ToolResult.Ok(
            string.Equals(oldVersion, newVersion, StringComparison.Ordinal)
                ? $"{entry.Name} already at {newVersion}."
                : $"Updated {entry.Name}: {oldVersion} -> {newVersion}.");
    }
}
```

- [ ] **Step 4: Implement PluginRemoveTool**

Create `src/AspireForm/Mcp/Tools/PluginRemoveTool.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: removes a plugin from the lockfile.</summary>
public sealed class PluginRemoveTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PluginRemoveTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plugin_remove";

    /// <inheritdoc />
    public string Description => "Remove a plugin from the lockfile.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Plugin name or package id."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "name");

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(ToolResult.Fail("aspireform_plugin_remove requires 'name'."));
        }

        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var lockfile = PluginLockfile.Load(projectDir);

        // Mirror PluginRemoveCommand: look up by display Name (the lockfile field), not Package.
        var removed = lockfile.Plugins.RemoveAll(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return Task.FromResult(ToolResult.Fail($"Plugin '{name}' is not installed."));
        }

        PluginLockfile.Save(projectDir, lockfile);
        return Task.FromResult(ToolResult.Ok(
            $"Removed plugin '{name}' from the lockfile. Already-loaded plugins remain active until next run."));
    }
}
```

- [ ] **Step 5: Write the tests**

Create `tests/AspireForm.Tests/Mcp/Tools/PluginToolsTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools;

public sealed class PluginToolsTests
{
    [Fact]
    public void All_four_tools_have_aspireform_plugin_prefix()
    {
        new PluginListTool(".").Name.Should().Be("aspireform_plugin_list");
        new PluginInstallTool(".").Name.Should().Be("aspireform_plugin_install");
        new PluginUpdateTool(".").Name.Should().Be("aspireform_plugin_update");
        new PluginRemoveTool(".").Name.Should().Be("aspireform_plugin_remove");
    }

    [Fact]
    public async Task PluginListTool_empty_lockfile_returns_no_plugins_message()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-plug-list-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new PluginListTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeFalse();
            result.Content[0].Text.Should().Contain("No plugins installed");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task PluginInstallTool_missing_name_returns_tool_level_error()
    {
        var tool = new PluginInstallTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'name'");
    }

    [Fact]
    public async Task PluginUpdateTool_missing_name_returns_tool_level_error()
    {
        var tool = new PluginUpdateTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'name'");
    }

    [Fact]
    public async Task PluginRemoveTool_unknown_plugin_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-plug-rm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new PluginRemoveTool(dir);
            var result = await tool.ExecuteAsync(new JsonObject { ["name"] = "DoesNotExist" }, default);
            result.IsError.Should().BeTrue();
            result.Content[0].Text.Should().Contain("not installed");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 6: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.PluginToolsTests*"
```

Expected: 5/5 PASS.

- [ ] **Step 7: Commit**

```bash
git add src/AspireForm/Mcp/Tools/PluginListTool.cs src/AspireForm/Mcp/Tools/PluginInstallTool.cs src/AspireForm/Mcp/Tools/PluginUpdateTool.cs src/AspireForm/Mcp/Tools/PluginRemoveTool.cs tests/AspireForm.Tests/Mcp/Tools/PluginToolsTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add plugin tools (list / install / update / remove)"
```

---

## Task 18: Macro — scaffold_aspire_app_with_data

**Files:**
- Create: `src/AspireForm/Mcp/Tools/Macros/ScaffoldAspireAppWithDataTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs`

- [ ] **Step 1: Implement**

Create `src/AspireForm/Mcp/Tools/Macros/ScaffoldAspireAppWithDataTool.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp.Tools.Macros;

/// <summary>MCP macro: scaffolds a new project, adds a SQL Server Resource and an ef-data Module, then plans and applies.</summary>
public sealed class ScaffoldAspireAppWithDataTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the macro with a default project directory.</summary>
    public ScaffoldAspireAppWithDataTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "scaffold_aspire_app_with_data";

    /// <inheritdoc />
    public string Description =>
        "End-to-end recipe: create a new Aspire app, add a SQL Server Resource and an ef-data Module, then plan and apply.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Project name (required)."),
        ["output"] = ToolBase.Str("Output directory (defaults to the server's --project-dir)."),
        ["databaseName"] = ToolBase.Str("Database block name (defaults to 'appdb')."),
    }, "name");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Fail("scaffold_aspire_app_with_data requires 'name'.");
        }

        var output = args["output"]?.GetValue<string>() ?? _defaultProjectDir;
        var dbName = args["databaseName"]?.GetValue<string>() ?? "appdb";
        var projectRoot = Path.GetFullPath(Path.Combine(output, name));

        var summary = new StringBuilder();
        summary.AppendLine($"Macro: scaffold_aspire_app_with_data(name={name}, databaseName={dbName})");

        var newResult = await new NewTool(_defaultProjectDir).ExecuteAsync(
            new JsonObject { ["name"] = name, ["output"] = output }, ct);
        summary.AppendLine($"  [1/5] new       : {Summarise(newResult)}");
        if (newResult.IsError) return ToolResult.Fail(summary.ToString());

        var addSqlResult = await new AddTool(projectRoot).ExecuteAsync(
            new JsonObject { ["type"] = "sqlserver", ["name"] = dbName }, ct);
        summary.AppendLine($"  [2/5] add sql   : {Summarise(addSqlResult)}");
        if (addSqlResult.IsError) return ToolResult.Fail(summary.ToString());

        var addEfResult = await new AddTool(projectRoot).ExecuteAsync(
            new JsonObject
            {
                ["type"] = "ef-data",
                ["name"] = "data",
                ["module"] = true,
                ["dependsOn"] = new JsonArray(dbName),
            }, ct);
        summary.AppendLine($"  [3/5] add ef-data: {Summarise(addEfResult)}");
        if (addEfResult.IsError) return ToolResult.Fail(summary.ToString());

        var planResult = await new PlanTool(projectRoot).ExecuteAsync([], ct);
        summary.AppendLine($"  [4/5] plan      : {(planResult.IsError ? "FAIL" : "ok")}");
        if (planResult.IsError) return ToolResult.Fail(summary + Environment.NewLine + planResult.Content[0].Text);

        var applyResult = await new ApplyTool(projectRoot).ExecuteAsync([], ct);
        summary.AppendLine($"  [5/5] apply     : {(applyResult.IsError ? "FAIL" : "ok")}");
        summary.AppendLine();
        summary.AppendLine("Plan output:");
        summary.AppendLine(planResult.Content[0].Text);
        summary.AppendLine("Apply output:");
        summary.AppendLine(applyResult.Content[0].Text);

        return applyResult.IsError ? ToolResult.Fail(summary.ToString()) : ToolResult.Ok(summary.ToString());
    }

    private static string Summarise(ToolResult r) =>
        r.Content.Count > 0 ? r.Content[0].Text.Split('\n', 2)[0] : (r.IsError ? "FAIL" : "ok");
}
```

- [ ] **Step 2: Write tests (will be extended by Tasks 19 and 20)**

Create `tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools.Macros;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Macros;

public sealed class MacroToolsTests
{
    [Fact]
    public void ScaffoldAspireAppWithDataTool_metadata()
    {
        var tool = new ScaffoldAspireAppWithDataTool(".");
        tool.Name.Should().Be("scaffold_aspire_app_with_data");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("name");
    }

    [Fact]
    public async Task ScaffoldAspireAppWithDataTool_missing_name_returns_tool_level_error()
    {
        var tool = new ScaffoldAspireAppWithDataTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'name'");
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.Macros.MacroToolsTests*"
```

Expected: 2/2 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/Tools/Macros/ScaffoldAspireAppWithDataTool.cs tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add scaffold_aspire_app_with_data macro tool"
```

---

## Task 19: Macro — add_cache_layer

**Files:**
- Create: `src/AspireForm/Mcp/Tools/Macros/AddCacheLayerTool.cs`
- Modify: `tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs`

- [ ] **Step 1: Implement**

Create `src/AspireForm/Mcp/Tools/Macros/AddCacheLayerTool.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp.Tools.Macros;

/// <summary>MCP macro: adds a Redis Resource to an existing AspireForm project, plans, and applies.</summary>
public sealed class AddCacheLayerTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the macro with a default project directory.</summary>
    public AddCacheLayerTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "add_cache_layer";

    /// <inheritdoc />
    public string Description =>
        "Adds a Redis Resource to an existing AspireForm project, then plans and applies.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectDir"] = ToolBase.Str("Project directory containing aspireform.yaml."),
        ["name"] = ToolBase.Str("Cache block name (default 'cache')."),
    });

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var name = args["name"]?.GetValue<string>() ?? "cache";

        var summary = new StringBuilder();
        summary.AppendLine($"Macro: add_cache_layer(name={name})");

        var addResult = await new AddTool(projectDir).ExecuteAsync(
            new JsonObject { ["type"] = "redis", ["name"] = name }, ct);
        summary.AppendLine($"  [1/3] add redis : {Summarise(addResult)}");
        if (addResult.IsError) return ToolResult.Fail(summary.ToString());

        var planResult = await new PlanTool(projectDir).ExecuteAsync([], ct);
        summary.AppendLine($"  [2/3] plan      : {(planResult.IsError ? "FAIL" : "ok")}");
        if (planResult.IsError) return ToolResult.Fail(summary + Environment.NewLine + planResult.Content[0].Text);

        var applyResult = await new ApplyTool(projectDir).ExecuteAsync([], ct);
        summary.AppendLine($"  [3/3] apply     : {(applyResult.IsError ? "FAIL" : "ok")}");
        summary.AppendLine();
        summary.AppendLine("Plan output:");
        summary.AppendLine(planResult.Content[0].Text);
        summary.AppendLine("Apply output:");
        summary.AppendLine(applyResult.Content[0].Text);

        return applyResult.IsError ? ToolResult.Fail(summary.ToString()) : ToolResult.Ok(summary.ToString());
    }

    private static string Summarise(ToolResult r) =>
        r.Content.Count > 0 ? r.Content[0].Text.Split('\n', 2)[0] : (r.IsError ? "FAIL" : "ok");
}
```

- [ ] **Step 2: Extend MacroToolsTests.cs**

Append the following inside the class in `tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs`:

```csharp
    [Fact]
    public void AddCacheLayerTool_metadata()
    {
        var tool = new AddCacheLayerTool(".");
        tool.Name.Should().Be("add_cache_layer");
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AddCacheLayerTool_missing_config_returns_tool_level_error()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-mcp-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var tool = new AddCacheLayerTool(dir);
            var result = await tool.ExecuteAsync([], default);
            result.IsError.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.Macros.MacroToolsTests*"
```

Expected: 4/4 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/Tools/Macros/AddCacheLayerTool.cs tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add add_cache_layer macro tool"
```

---

## Task 20: Macro — add_authentication

**Files:**
- Create: `src/AspireForm/Mcp/Tools/Macros/AddAuthenticationTool.cs`
- Modify: `tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs`

- [ ] **Step 1: Implement**

Create `src/AspireForm/Mcp/Tools/Macros/AddAuthenticationTool.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools.Macros;

/// <summary>MCP macro: adds an authentication variant (apikey / magiclink / entra) to an existing project. Auto-installs the relevant plugin if missing, then adds + plans + applies.</summary>
public sealed class AddAuthenticationTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the macro with a default project directory.</summary>
    public AddAuthenticationTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "add_authentication";

    /// <inheritdoc />
    public string Description =>
        "Adds an authentication variant (apikey/magiclink/entra) to an AspireForm project. " +
        "Auto-installs the matching plugin if missing.";

    /// <inheritdoc />
    public JsonObject InputSchema
    {
        get
        {
            var variant = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray("apikey", "magiclink", "entra"),
                ["description"] = "Auth variant.",
            };
            return ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
            {
                ["projectDir"] = ToolBase.Str("Project directory containing aspireform.yaml."),
                ["name"] = ToolBase.Str("Auth block name (default 'auth')."),
                ["variant"] = variant,
                ["inputs"] = new JsonObject { ["type"] = "object", ["description"] = "Variant-specific inputs to inject under the block." },
            }, "variant");
        }
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var variant = args["variant"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(variant))
        {
            return ToolResult.Fail("add_authentication requires 'variant'.");
        }
        if (variant is not ("apikey" or "magiclink" or "entra"))
        {
            return ToolResult.Fail($"Unknown variant '{variant}'. Allowed: apikey, magiclink, entra.");
        }

        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var name = args["name"]?.GetValue<string>() ?? "auth";
        var blockType = $"auth-{variant}";
        var pluginName = variant switch
        {
            "apikey" => "Auth.ApiKey",
            "magiclink" => "Auth.MagicLink",
            "entra" => "Auth.Entra",
            _ => throw new InvalidOperationException(),
        };
        var packageId = $"AspireForm.Plugin.{pluginName}";

        var summary = new StringBuilder();
        summary.AppendLine($"Macro: add_authentication(variant={variant}, name={name})");

        // Step 1: install plugin if not already in lockfile.
        var lockfile = PluginLockfile.Load(projectDir);
        if (!lockfile.Plugins.Any(p => string.Equals(p.Package, packageId, StringComparison.OrdinalIgnoreCase)))
        {
            var installResult = await new Tools.PluginInstallTool(projectDir).ExecuteAsync(
                new JsonObject { ["name"] = pluginName }, ct);
            summary.AppendLine($"  [1/4] install   : {Summarise(installResult)}");
            if (installResult.IsError) return ToolResult.Fail(summary.ToString());
        }
        else
        {
            summary.AppendLine($"  [1/4] install   : already installed");
        }

        // Step 2: add the block.
        var addResult = await new AddTool(projectDir).ExecuteAsync(
            new JsonObject { ["type"] = blockType, ["name"] = name }, ct);
        summary.AppendLine($"  [2/4] add       : {Summarise(addResult)}");
        if (addResult.IsError) return ToolResult.Fail(summary.ToString());

        // Step 3: plan.
        var planResult = await new PlanTool(projectDir).ExecuteAsync([], ct);
        summary.AppendLine($"  [3/4] plan      : {(planResult.IsError ? "FAIL" : "ok")}");
        if (planResult.IsError) return ToolResult.Fail(summary + Environment.NewLine + planResult.Content[0].Text);

        // Step 4: apply.
        var applyResult = await new ApplyTool(projectDir).ExecuteAsync([], ct);
        summary.AppendLine($"  [4/4] apply     : {(applyResult.IsError ? "FAIL" : "ok")}");
        summary.AppendLine();
        summary.AppendLine("Plan output:");
        summary.AppendLine(planResult.Content[0].Text);
        summary.AppendLine("Apply output:");
        summary.AppendLine(applyResult.Content[0].Text);

        return applyResult.IsError ? ToolResult.Fail(summary.ToString()) : ToolResult.Ok(summary.ToString());
    }

    private static string Summarise(ToolResult r) =>
        r.Content.Count > 0 ? r.Content[0].Text.Split('\n', 2)[0] : (r.IsError ? "FAIL" : "ok");
}
```

- [ ] **Step 2: Extend MacroToolsTests.cs**

Append the following inside the class in `tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs`:

```csharp
    [Fact]
    public void AddAuthenticationTool_metadata_requires_variant()
    {
        var tool = new AddAuthenticationTool(".");
        tool.Name.Should().Be("add_authentication");
        tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Contain("variant");
    }

    [Fact]
    public async Task AddAuthenticationTool_unknown_variant_returns_tool_level_error()
    {
        var tool = new AddAuthenticationTool(".");
        var result = await tool.ExecuteAsync(new JsonObject { ["variant"] = "saml" }, default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("Unknown variant 'saml'");
    }

    [Fact]
    public async Task AddAuthenticationTool_missing_variant_returns_tool_level_error()
    {
        var tool = new AddAuthenticationTool(".");
        var result = await tool.ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'variant'");
    }
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.Tools.Macros.MacroToolsTests*"
```

Expected: 7/7 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Mcp/Tools/Macros/AddAuthenticationTool.cs tests/AspireForm.Tests/Mcp/Tools/Macros/MacroToolsTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add add_authentication macro tool"
```

---

## Task 21: McpCommand — wire the CLI verb and register all tools

**Files:**
- Create: `src/AspireForm/Cli/McpCommand.cs`
- Modify: `src/AspireForm/Program.cs`

- [ ] **Step 1: Implement the McpCommand**

Create `src/AspireForm/Cli/McpCommand.cs`:

```csharp
using System.ComponentModel;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools;
using AspireForm.Mcp.Tools.Macros;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>mcp</c> command: starts an MCP server exposing AspireForm's verbs as tools. Defaults to stdio; pass <c>--http --port N</c> for HTTP.</summary>
public sealed class McpCommand : AsyncCommand<McpCommand.Settings>
{
    /// <summary>Options for <c>mcp</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Default project directory used by tool handlers when their args omit <c>projectDir</c>.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("Default project directory for tool calls that omit 'projectDir'.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Use HTTP transport instead of stdio.</summary>
        [CommandOption("--http")]
        [Description("Use HTTP transport (localhost only) instead of stdio.")]
        public bool Http { get; init; }

        /// <summary>Port for the HTTP transport. Ignored unless <c>--http</c> is supplied.</summary>
        [CommandOption("--port <PORT>")]
        [Description("Port for the HTTP transport (default 5050).")]
        public int Port { get; init; } = 5050;
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectDir = Path.GetFullPath(settings.ProjectDir);
        var registry = BuildRegistry(projectDir);
        var server = new McpServer(registry);
        ITransport transport = settings.Http
            ? new HttpTransport(settings.Port)
            : new StdioTransport();

        try
        {
            await transport.RunAsync(server, cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }

    /// <summary>Builds the registry of all 14 low-level tools and 3 macros, all bound to <paramref name="projectDir"/> as their default.</summary>
    public static ToolRegistry BuildRegistry(string projectDir)
    {
        var r = new ToolRegistry();

        // Low-level (14).
        r.Register(new ConfigTool(projectDir));
        r.Register(new PlanTool(projectDir));
        r.Register(new ApplyTool(projectDir));
        r.Register(new NewTool(projectDir));
        r.Register(new AddTool(projectDir));
        r.Register(new DestroyTool(projectDir));
        r.Register(new ImportTool(projectDir));
        r.Register(new StateListTool(projectDir));
        r.Register(new StateShowTool(projectDir));
        r.Register(new DoctorTool());
        r.Register(new PluginListTool(projectDir));
        r.Register(new PluginInstallTool(projectDir));
        r.Register(new PluginUpdateTool(projectDir));
        r.Register(new PluginRemoveTool(projectDir));

        // Macros (3).
        r.Register(new ScaffoldAspireAppWithDataTool(projectDir));
        r.Register(new AddCacheLayerTool(projectDir));
        r.Register(new AddAuthenticationTool(projectDir));

        return r;
    }
}
```

- [ ] **Step 2: Register the verb in Program.cs**

Edit `src/AspireForm/Program.cs` and add (after the existing `config.AddCommand<DoctorCommand>("doctor")...` line and before `config.AddBranch("state", ...)`):

```csharp
    config.AddCommand<McpCommand>("mcp")
        .WithDescription("Start an MCP server exposing AspireForm's verbs (stdio by default; --http for localhost HTTP).");
```

- [ ] **Step 3: Build and write a registration smoke test**

Create `tests/AspireForm.Tests/Mcp/McpCommandRegistrationTests.cs`:

```csharp
using AspireForm.Cli;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class McpCommandRegistrationTests
{
    [Fact]
    public void BuildRegistry_registers_14_low_level_tools_plus_3_macros()
    {
        var r = McpCommand.BuildRegistry(".");
        r.All.Count.Should().Be(17);

        // Low-level tools: 14 distinct names.
        string[] expectedLowLevel =
        [
            "aspireform_new", "aspireform_add", "aspireform_config", "aspireform_plan",
            "aspireform_apply", "aspireform_destroy", "aspireform_import",
            "aspireform_state_list", "aspireform_state_show", "aspireform_doctor",
            "aspireform_plugin_list", "aspireform_plugin_install",
            "aspireform_plugin_update", "aspireform_plugin_remove",
        ];
        foreach (var n in expectedLowLevel)
        {
            r.Contains(n).Should().BeTrue(because: $"low-level tool '{n}' must be registered");
        }

        // Macros: 3 distinct names.
        string[] expectedMacros =
        [
            "scaffold_aspire_app_with_data", "add_cache_layer", "add_authentication",
        ];
        foreach (var n in expectedMacros)
        {
            r.Contains(n).Should().BeTrue(because: $"macro tool '{n}' must be registered");
        }
    }
}
```

- [ ] **Step 4: Run**

```bash
dotnet build
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.McpCommandRegistrationTests*"
```

Expected: build succeeds; 1/1 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Cli/McpCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Mcp/McpCommandRegistrationTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): wire McpCommand and register 17 tools"
```

---

## Task 22: End-to-end smoke — run aspireform mcp over stdio in-process

**Files:**
- Create: `tests/AspireForm.Tests/Mcp/EndToEndTests.cs`

- [ ] **Step 1: Write the integration test**

Create `tests/AspireForm.Tests/Mcp/EndToEndTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Cli;
using AspireForm.Mcp;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class EndToEndTests
{
    [Fact]
    public async Task Initialize_then_tools_list_then_doctor_tools_call_round_trips_via_stdio()
    {
        var registry = McpCommand.BuildRegistry(".");
        var server = new McpServer(registry);

        var inputLines = new[]
        {
            """{"jsonrpc":"2.0","id":1,"method":"initialize"}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""",
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"aspireform_doctor","arguments":{}}}""",
        };
        var input = new StringReader(string.Join('\n', inputLines) + "\n");
        var output = new StringWriter();
        var transport = new StdioTransport(input, output);

        await transport.RunAsync(server, default);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.Should().Be(3);

        var initResp = JsonNode.Parse(lines[0]) as JsonObject;
        initResp!["result"]!["serverInfo"]!["name"]!.GetValue<string>().Should().Be("AspireForm");

        var listResp = JsonNode.Parse(lines[1]) as JsonObject;
        listResp!["result"]!["tools"]!.AsArray().Count.Should().Be(17);

        var callResp = JsonNode.Parse(lines[2]) as JsonObject;
        callResp!["result"]!["content"]![0]!["text"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }
}
```

- [ ] **Step 2: Run**

```bash
dotnet run --project tests/AspireForm.Tests --filter-method "AspireForm.Tests.Mcp.EndToEndTests*"
```

Expected: 1/1 PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/AspireForm.Tests/Mcp/EndToEndTests.cs
git -c commit.gpgsign=false commit -m "test(mcp): add stdio end-to-end smoke (initialize / tools/list / tools/call)"
```

---

## Task 23: Documentation + CHANGELOG + final test gate

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Add README "Use with an agent" section**

Locate the most appropriate place in `README.md` (typically near the end of the "Usage" or "Commands" section). Add:

````markdown
## Use with an agent (MCP server)

AspireForm includes an MCP server that exposes its verbs as tools, so AI agents can chat-construct an Aspire app:

```bash
aspireform mcp                         # stdio (default — for Claude Desktop / Claude Code / Aspire CLI)
aspireform mcp --http --port 5050      # localhost HTTP transport
aspireform mcp --project-dir ./myapp   # set the default projectDir for tool calls
```

### Claude Code config snippet

Add to `~/.claude/mcp.json` (or the project-scoped equivalent):

```json
{
  "mcpServers": {
    "aspireform": {
      "command": "dnx",
      "args": ["AspireForm", "mcp"]
    }
  }
}
```

### Tool surface

**14 low-level tools** mirror the CLI verbs:
`aspireform_new`, `aspireform_add`, `aspireform_config`, `aspireform_plan`, `aspireform_apply`, `aspireform_destroy`, `aspireform_import`, `aspireform_state_list`, `aspireform_state_show`, `aspireform_doctor`, `aspireform_plugin_list`, `aspireform_plugin_install`, `aspireform_plugin_update`, `aspireform_plugin_remove`.

**3 curated macros** orchestrate common recipes:
`scaffold_aspire_app_with_data`, `add_cache_layer`, `add_authentication`.

> **Security:** the HTTP transport binds localhost only and has no authentication in this version. Do not expose it on a public interface.
````

- [ ] **Step 2: Add CHANGELOG.md entry**

Add a new section at the top of `CHANGELOG.md`:

```markdown
## [0.4.0] - 2026-05-25

### Added
- `aspireform mcp` verb: starts an MCP (Model Context Protocol) server exposing AspireForm's verbs as tools to AI agents.
  - Two transports: stdio (default) and HTTP on localhost (`--http --port N`).
  - 14 low-level tools (one per CLI verb): `aspireform_{new, add, config, plan, apply, destroy, import, state_list, state_show, doctor, plugin_list, plugin_install, plugin_update, plugin_remove}`.
  - 3 curated macros: `scaffold_aspire_app_with_data`, `add_cache_layer`, `add_authentication`.
  - In-process: tool handlers call the same internal services the CLI commands use — no shell-out.
- README "Use with an agent" section with Claude Code config snippet.
```

- [ ] **Step 3: Run the full test suite**

```bash
dotnet build
dotnet run --project tests/AspireForm.Tests
```

Expected: all tests pass (the existing 186 + the new MCP tests added in Tasks 2–22).

- [ ] **Step 4: Commit**

```bash
git add README.md CHANGELOG.md
git -c commit.gpgsign=false commit -m "docs: add MCP server section to README + 0.4.0 CHANGELOG"
```

- [ ] **Step 5: Verify the package builds for release**

```bash
dotnet pack src/AspireForm/AspireForm.csproj -o ./artifacts
```

Expected: `./artifacts/AspireForm.0.4.0.nupkg` produced.

- [ ] **Step 6: Tag the release (when sign-off is given)**

When the user gives the go-ahead to ship:

```bash
git tag -a v0.4.0 -m "AspireForm 0.4.0 — MCP server"
git push origin main
git push origin v0.4.0
```

The existing `release.yml` workflow (the `publish` job triggered by `v*` tags) publishes the package to NuGet.

---

## Definition of done

- `aspireform mcp` runs an MCP server over stdio (default) and HTTP (`--http --port N`).
- 14 low-level verb tools and 3 curated macro tools registered and behave end-to-end.
- Test count up by at least 40 (registry + JSON-RPC + server dispatcher + 2 transports + 14 low-level tool tests + 3 macro tool tests + e2e smoke + registration smoke); MTP suite green.
- `AspireForm 0.4.0` package builds cleanly.
- README has the "Use with an agent" section + Claude Code config snippet.
- `CHANGELOG.md` has a `[0.4.0]` entry.
- Ready to ship via `v0.4.0` tag through the existing release workflow.
