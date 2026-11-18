using A2A;
using A2A.AspNetCore;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

const string AgentName = "orchestrator-agent";
const string A2APath = "/a2a/orchestrator";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKeyedSingleton<AIAgent>(AgentName, (services, _) =>
    new ChatClientAgent(
        new TransportProofChatClient(),
        instructions:
            "You are the PMCR-O Orchestrator transport proof. " +
            "Do not claim orchestration execution. Return the deterministic provider disposition.",
        name: AgentName,
        description: "PMCR-O global lifecycle owner. CI transport proof uses a non-executing deterministic provider.",
        services: services));

builder.AddA2AServer(AgentName);

var app = builder.Build();

var publicBaseUrl =
    builder.Configuration["A2A_PUBLIC_BASE_URL"]?.TrimEnd('/')
    ?? "http://127.0.0.1:5091";

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "PASS",
    agent = AgentName,
    transport = "A2A",
    provider = "transport-proof",
    execution = "NOT_INVOKED"
}));

app.MapA2AHttpJson(AgentName, A2APath);
app.MapA2AJsonRpc(AgentName, A2APath);

app.MapWellKnownAgentCard(new AgentCard
{
    Name = "PMCR-O Orchestrator Agent",
    Description = "Owns global PMCR-O lifecycle routing. This host exposes the A2A service boundary.",
    Version = "1.0.0",
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

internal sealed class TransportProofChatClient : IChatClient
{
    private const string Disposition =
        "NOT_INVOKED: A2A transport reached orchestrator-agent; " +
        "the real PMCR-O Orchestrator reasoning/provider is not connected in this CI proof.";

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, Disposition)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, Disposition);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
