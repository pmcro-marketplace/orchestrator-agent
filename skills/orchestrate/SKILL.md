---
name: orchestrate
description: Own one established high-level goal across bounded PMCR-O cycles and route the next bounded intent. USE FOR selecting and routing the next bounded PMCR-O intent. DO NOT USE FOR planning, making, checking, or reflecting; use the corresponding PMCR-O role agent.
---
# orchestrate

## Purpose
Maintain the high-level goal, select one current bounded intent, and route the PMCR-O cycle while preserving role boundaries.

## When to Use
Use when this PMCR-O role is the narrowest resolved capability for the current bounded cycle.

## When Not to Use
Do not perform another role's work, broaden scope silently, duplicate an existing validated capability, or claim execution without evidence.

## Inputs
A bounded request, resolved target identity, authorization context, current cycle state, and applicable evidence.

## Deliverables
One concrete role result or a BLOCKED/NOT_INVOKED disposition with evidence.

## Workflow
1. Preserve the established high-level goal.
2. Select the smallest useful current intent.
3. Resolve the next PMCR-O role capability.
4. Delegate planning/making/checking/reflection rather than performing those roles.
5. Decide continue, clarify, or stop from evidence.

## Validation
A PASS requires actual evidence. An emitted invocation, route, plan, or declared path is not proof of execution.

## Pitfalls
Emission is not invocation. The installed Hook Agent may surround this role but does not replace its business function.

## Resources
See `references/contract.md`, `assets/request.json`, and `scripts/validate.py`.
