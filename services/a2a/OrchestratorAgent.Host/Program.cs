using A2A;
using A2A.AspNetCore;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

const string AgentName = "orchestrator-agent";
const string A2APath = "/a2a/orchestrator";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKeyedSingleton<AIAgent>(AgentName, (services, _) =>
    new ChatClientAgent(
        new DispatchChainProofChatClient(services.GetRequiredService<IConfiguration>()),
        instructions:
            "You are the PMCR-O Orchestrator transport proof. " +
            "When downstream role services are configured, dispatch the bounded transport chain in PMCR-O order. " +
            "Never claim role reasoning/provider execution.",
        name: AgentName,
        description:
            "PMCR-O global lifecycle owner. Can prove A2A dispatch to Planner, Maker, Checker and Reflector " +
            "while every role reasoning/provider remains NOT_INVOKED.",
        services: services));

builder.AddA2AServer(AgentName);

var app = builder.Build();

var publicBaseUrl =
    builder.Configuration["A2A_PUBLIC_BASE_URL"]?.TrimEnd('/')
    ?? "http://127.0.0.1:5091";

var downstreamKeys = new[]
{
    "A2A_DOWNSTREAM_PLANNER",
    "A2A_DOWNSTREAM_MAKER",
    "A2A_DOWNSTREAM_CHECKER",
    "A2A_DOWNSTREAM_REFLECTOR"
};

var dispatchConfigured = downstreamKeys.All(
    key => Uri.TryCreate(builder.Configuration[key], UriKind.Absolute, out _));

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "PASS",
    agent = AgentName,
    transport = "A2A",
    provider = "transport-proof",
    execution = "NOT_INVOKED",
    reasoning_provider = "NOT_INVOKED",
    dispatch_chain = dispatchConfigured ? "CONFIGURED" : "NOT_CONFIGURED"
}));

app.MapA2AHttpJson(AgentName, A2APath);
app.MapA2AJsonRpc(AgentName, A2APath);

app.MapWellKnownAgentCard(new AgentCard
{
    Name = "PMCR-O Orchestrator Agent",
    Description =
        "Owns global PMCR-O lifecycle routing. The proof host may dispatch the A2A role chain, " +
        "but does not execute role reasoning/providers.",
    Version = "1.1.0",
    DefaultInputModes = ["text"],
    DefaultOutputModes = ["text"],
    SupportedInterfaces =
    [
        new AgentInterface
        {
            Url = publicBaseUrl + A2APath,
            ProtocolBinding = ProtocolBindingNames.HttpJson,
            ProtocolVersion = "1.0",
        }
    ]
});

app.Run();

internal sealed class DispatchChainProofChatClient(IConfiguration configuration) : IChatClient
{
    private static readonly (string Role, string Key)[] Downstream =
    [
        ("planner", "A2A_DOWNSTREAM_PLANNER"),
        ("maker", "A2A_DOWNSTREAM_MAKER"),
        ("checker", "A2A_DOWNSTREAM_CHECKER"),
        ("reflector", "A2A_DOWNSTREAM_REFLECTOR")
    ];

    private const string StandaloneDisposition =
        "NOT_INVOKED: A2A transport reached orchestrator-agent; " +
        "the real PMCR-O Orchestrator reasoning/provider is not connected; " +
        "A2A_DISPATCH_CHAIN=NOT_CONFIGURED.";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var text = await RunDispatchAsync(cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var text = await RunDispatchAsync(cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    private async Task<string> RunDispatchAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targets = new List<(string Role, Uri Url)>();
        foreach (var (role, key) in Downstream)
        {
            var raw = configuration[key];
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                return StandaloneDisposition;
            }
            targets.Add((role, uri));
        }

        var receipts = new List<string>();
        foreach (var (role, url) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolver = new A2ACardResolver(url);
            AIAgent remoteAgent = await resolver.GetAIAgentAsync();
            AgentResponse response = await remoteAgent.RunAsync(
                $"PMCR-O A2A dispatch proof from orchestrator-agent to {role}-agent. " +
                "Role reasoning/provider execution must remain NOT_INVOKED.");

            if (!response.Text.StartsWith("NOT_INVOKED:", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Downstream {role}-agent claimed unexpected execution: {response.Text}");
            }

            receipts.Add($"{role}=NOT_INVOKED");
        }

        return
            "NOT_INVOKED: ORCHESTRATOR_REASONING_PROVIDER; " +
            "A2A_DISPATCH_CHAIN=PASS; " +
            string.Join(";", receipts) + "; " +
            "maker_actuators=NOT_CONNECTED;checker_mutation=DISALLOWED;reflector_dispatch=false";
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
