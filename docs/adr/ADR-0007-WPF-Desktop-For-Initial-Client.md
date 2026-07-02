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
> - docs/architecture/technology-stack.md
> - docs/architecture/solution-structure.md
> - docs/architecture/application-lifecycle.md

# ADR-0007: WPF Desktop for Initial Client

## Status

Accepted

## Context

Deadbelt Operations Platform needs an initial user interface that allows operators to manage workspaces, environments, providers, jobs, and future operational workflows.

The project is starting with a Windows-first MVP. Many early target users are expected to run game server tooling, editors, SteamCMD workflows, configuration tools, and administrative utilities from Windows workstations.

The initial UI needs to support:

- Desktop-first workflows
- Local file access
- Workspace creation and opening
- Future environment management
- Future provider and job dashboards
- A professional operations-console layout
- MVVM structure
- Dependency injection
- Long-term maintainability

## Decision

DOP will use WPF as the initial desktop client technology.

The WPF desktop application will be the first primary client for the platform. It will follow MVVM principles and remain a thin presentation layer over the Application and Domain layers.

The desktop client must not contain core business logic directly. Business workflows should flow through the Application layer.

The intended dependency direction is:

    Desktop
        ↓
    Application
        ↓
    Domain

Infrastructure and Providers are registered through dependency injection and used through abstractions.

## Alternatives Considered

### Web UI First

Rejected for the MVP because it would require additional hosting, browser, API, authentication, and deployment decisions before the core local workspace workflows are proven.

### CLI First

Rejected as the primary initial client because DOP is intended to become an operations platform with visual workspace, environment, job, and provider management. A CLI may be added later, but it is not the best first user experience.

### Cross-Platform UI First

Deferred because cross-platform UI would increase early complexity. DOP should preserve cross-platform architectural options, but the first client will prioritize a strong Windows desktop experience.

### WinUI

Considered, but WPF was selected because it is mature, stable, well-understood, and appropriate for a Windows-first operations console.

## Consequences

### Positive

- Enables fast Windows desktop development.
- Supports MVVM cleanly.
- Integrates well with .NET hosting, dependency injection, and logging.
- Supports local file workflows needed for workspace creation and opening.
- Provides a stable foundation for a professional operations-console UI.

### Negative

- Initial desktop client is Windows-specific.
- Future cross-platform support will require additional clients or UI strategy.
- WPF resource handling requires careful conventions for images, icons, and theme assets.

## Implementation Notes

The initial desktop shell includes:

- Generic Host startup
- Dependency injection
- Logging
- MVVM foundation
- Main window shell
- Workspace creation workflow
- Open workspace workflow
- Workspace dashboard shell
- Workspace navigation shell
- Deadbelt branding assets

The WPF client should remain one client of the platform, not the platform itself.