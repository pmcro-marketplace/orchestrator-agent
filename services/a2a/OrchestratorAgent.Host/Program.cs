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
            "Preserve PMCR-O correlation exactly and never claim role reasoning/provider execution.",
        name: AgentName,
        description:
            "PMCR-O global lifecycle owner. Proves A2A dispatch and correlation preservation while " +
            "every role reasoning/provider remains NOT_INVOKED.",
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
    dispatch_chain = dispatchConfigured ? "CONFIGURED" : "NOT_CONFIGURED",
    correlation = "SUPPORTED"
}));

app.MapA2AHttpJson(AgentName, A2APath);
app.MapA2AJsonRpc(AgentName, A2APath);

app.MapWellKnownAgentCard(new AgentCard
{
    Name = "PMCR-O Orchestrator Agent",
    Description =
        "Owns global PMCR-O lifecycle routing. The proof host may dispatch the A2A role chain " +
        "and preserve a PMCR-O correlation envelope, but it does not execute role reasoning/providers.",
    Version = "1.2.0",
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
    private const string CorrelationPrefix = "PMCRO_CORRELATION ";

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
        var text = await RunDispatchAsync(messages, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var text = await RunDispatchAsync(messages, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    private async Task<string> RunDispatchAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var correlation = messages
            .Select(message => message.Text)
            .LastOrDefault(text => text.StartsWith(CorrelationPrefix, StringComparison.Ordinal));

        var targets = new List<(string Role, Uri Url)>();
        foreach (var (role, key) in Downstream)
        {
            var raw = configuration[key];
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                return correlation is null
                    ? StandaloneDisposition
                    : StandaloneDisposition + " " + correlation;
            }
            targets.Add((role, uri));
        }

        var receipts = new List<string>();
        foreach (var (role, url) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolver = new A2ACardResolver(url);
            AIAgent remoteAgent = await resolver.GetAIAgentAsync();

            var downstreamRequest = correlation
                ?? $"PMCR-O A2A dispatch proof from orchestrator-agent to {role}-agent. " +
                   "Role reasoning/provider execution must remain NOT_INVOKED.";

            AgentResponse response = await remoteAgent.RunAsync(downstreamRequest);

            if (!response.Text.StartsWith("NOT_INVOKED:", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Downstream {role}-agent claimed unexpected execution: {response.Text}");
            }

            if (correlation is not null &&
                !response.Text.Contains(correlation, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Downstream {role}-agent dropped or changed the PMCR-O correlation envelope.");
            }

            receipts.Add($"{role}=NOT_INVOKED");
        }

        var correlationMarker = correlation is null
            ? string.Empty
            : "PMCRO_CORRELATION_CHAIN=PASS; ";

        var correlationEcho = correlation is null
            ? string.Empty
            : " " + correlation;

        return
            "NOT_INVOKED: ORCHESTRATOR_REASONING_PROVIDER; " +
            "A2A_DISPATCH_CHAIN=PASS; " +
            correlationMarker +
            string.Join(";", receipts) + "; " +
            "maker_actuators=NOT_CONNECTED;checker_mutation=DISALLOWED;reflector_dispatch=false" +
            correlationEcho;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
