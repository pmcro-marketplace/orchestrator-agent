using A2A;
using Microsoft.Agents.AI;

const string Correlation =
    "PMCRO_CORRELATION {\"workspace_id\":\"workspace-proof-001\",\"trail_id\":\"trail-proof-001\",\"cycle_id\":\"cycle-proof-001\",\"a2a_context_id\":\"context-proof-001\"}";

var expectChain = args.Any(arg => arg == "--chain");
var expectCorrelation = args.Any(arg => arg == "--correlation");

var baseUrl = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal))
    ?? "http://127.0.0.1:5091";

var resolver = new A2ACardResolver(new Uri(baseUrl));
AIAgent remoteAgent = await resolver.GetAIAgentAsync();

string request =
    expectCorrelation ? Correlation :
    expectChain
        ? "PMCR-O A2A dispatch-chain proof. Dispatch transport only; do not execute role reasoning/providers."
        : "PMCR-O A2A transport proof. Do not execute orchestration.";

AgentResponse response = await remoteAgent.RunAsync(request);

if (!response.Text.StartsWith("NOT_INVOKED:", StringComparison.Ordinal))
{
    Console.Error.WriteLine("FAIL unexpected A2A response: " + response.Text);
    return 1;
}

if (expectChain)
{
    var required = new List<string>
    {
        "A2A_DISPATCH_CHAIN=PASS",
        "planner=NOT_INVOKED",
        "maker=NOT_INVOKED",
        "checker=NOT_INVOKED",
        "reflector=NOT_INVOKED",
        "maker_actuators=NOT_CONNECTED",
        "checker_mutation=DISALLOWED",
        "reflector_dispatch=false"
    };

    if (expectCorrelation)
    {
        required.Add("PMCRO_CORRELATION_CHAIN=PASS");
        required.Add(Correlation);
    }

    foreach (var marker in required)
    {
        if (!response.Text.Contains(marker, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"FAIL missing dispatch marker: {marker}");
            Console.Error.WriteLine(response.Text);
            return 2;
        }
    }

    Console.WriteLine(
        expectCorrelation
            ? "PASS Orchestrator-owned A2A dispatch chain with exact PMCR-O correlation preservation"
            : "PASS Orchestrator-owned A2A dispatch chain");
}
else
{
    if (expectCorrelation && !response.Text.Contains(Correlation, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("FAIL standalone correlation envelope was not preserved.");
        Console.Error.WriteLine(response.Text);
        return 3;
    }

    Console.WriteLine("PASS A2A discovery");
    Console.WriteLine("PASS A2A message transport");
}

Console.WriteLine(response.Text);
return 0;
