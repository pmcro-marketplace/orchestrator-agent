# Orchestrator A2A Service Host

This directory contains the first concrete A2A service boundary for PMCR-O.

## What this proof establishes

- The host uses the current Agent Framework A2A SDK v1 hosting pattern.
- `orchestrator-agent` is registered as an independent A2A service.
- HTTP+JSON and JSON-RPC bindings are mapped at `/a2a/orchestrator`.
- A well-known agent card advertises the HTTP+JSON interface.
- The CI probe resolves the card through `A2ACardResolver` and sends a real A2A message.

## What this proof does not establish

The CI provider is deliberately deterministic and non-executing. It returns:

`NOT_INVOKED: ... the real PMCR-O Orchestrator reasoning/provider is not connected ...`

That prevents transport validation from being confused with native Orchestrator execution.

Production wiring must replace `TransportProofChatClient` with the validated MAF/model provider and durable session storage before the service is considered production-runtime ready.

## Boundaries

- A2A: agent-to-agent transport.
- MCP: tools, resources, prompts and actuator access.
- PMCR-O: lifecycle/governance.
- Trail: persistent execution evidence.

Only Orchestrator Agent may advance or dispatch the global PMCR-O lifecycle.
