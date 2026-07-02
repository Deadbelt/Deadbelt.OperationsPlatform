> **Status:** Accepted
>
> **Version:** 0.1
>
> **Last Updated:** 2026-07-01
>
> **Applies To:** Deadbelt Operations Platform (DOP)
>
> **Audience:** Contributors, Architects, Maintainers
>
> **Related Documents:**
> - docs/architecture/overview.md
> - docs/architecture/domain-model.md
> - docs/architecture/design-principles.md
> - docs/architecture/data-flow.md

# ADR-0008: Desired-State Architecture

## Status

Accepted

## Context

Game server operations often involve many disconnected pieces:

- Game server files
- Configuration files
- Mods
- Workshop content
- Provider settings
- Startup parameters
- Scheduled jobs
- Backups
- Monitoring checks
- Manual operational steps

Traditional game server tools often focus on direct action against a server: start, stop, update, edit, or deploy. This can be useful, but it does not provide a complete model of what the environment is supposed to look like.

DOP is intended to be more than a server manager. It is intended to be an operations platform that can describe, validate, compare, and eventually apply intended operational state.

## Decision

DOP will use a desired-state architecture.

Workspaces and environments will represent intended operational state. Future platform capabilities should compare desired state to actual state, detect drift, validate configuration, and apply changes through providers and jobs.

The long-term model is:

    Desired State
        ↓
    Validation
        ↓
    Plan
        ↓
    Execution
        ↓
    Result
        ↓
    Drift Detection

For the early MVP, this begins with simple workspace metadata and placeholder dashboard state. As the platform grows, desired state will expand to include environments, providers, packages, mods, deployments, schedules, backups, and jobs.

## Design Implications

DOP should prefer declarative models over isolated actions.

For example, instead of only saying:

    Install this mod.

DOP should eventually understand:

    This environment should contain these mods, these versions, these configuration files, these providers, and these operational policies.

Then the platform can determine what actions are required.

## Alternatives Considered

### Direct Action Model

Rejected as the primary architecture because it limits DOP to being a task runner or server manager.

### Server-Centric Model

Rejected because DOP needs to manage complete environments, not just individual servers.

### Provider-Centric Model

Rejected because providers are implementation details. The environment and desired state should remain the primary platform concepts.

## Consequences

### Positive

- Supports drift detection.
- Supports validation before execution.
- Supports repeatable deployments.
- Supports environment promotion.
- Supports future automation and planning.
- Creates a stronger foundation than one-off operational actions.

### Negative

- Requires more up-front modeling.
- Some workflows may take longer to implement.
- The platform must clearly separate desired state, actual state, and execution results.

## Early Implementation Notes

Current early implementation includes:

- Workspace model
- Workspace metadata file
- Workspace creation
- Workspace opening
- Workspace dashboard shell
- Workspace navigation shell

These are foundational steps toward the desired-state model. Future issues will extend this into environments, providers, jobs, deployments, and drift detection.