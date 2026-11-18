using A2A;
using Microsoft.Agents.AI;

var expectChain = args.Any(arg => arg == "--chain");
var baseUrl = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal))
    ?? "http://127.0.0.1:5091";

var resolver = new A2ACardResolver(new Uri(baseUrl));
AIAgent remoteAgent = await resolver.GetAIAgentAsync();

AgentResponse response = await remoteAgent.RunAsync(
    expectChain
        ? "PMCR-O A2A dispatch-chain proof. Dispatch transport only; do not execute role reasoning/providers."
        : "PMCR-O A2A transport proof. Do not execute orchestration.");

if (!response.Text.StartsWith("NOT_INVOKED:", StringComparison.Ordinal))
{
    Console.Error.WriteLine("FAIL unexpected A2A response: " + response.Text);
    return 1;
}

if (expectChain)
{
    var required = new[]
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

    foreach (var marker in required)
    {
        if (!response.Text.Contains(marker, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"FAIL missing dispatch marker: {marker}");
            Console.Error.WriteLine(response.Text);
            return 2;
        }
    }

    Console.WriteLine("PASS Orchestrator-owned A2A dispatch chain");
}
else
{
    Console.WriteLine("PASS A2A discovery");
    Console.WriteLine("PASS A2A message transport");
}

Console.WriteLine(response.Text);
return 0;
