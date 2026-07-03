> **Status:** Draft
>
> **Version:** 0.1
>
> **Last Updated:** 2026-06-29
>
> **Applies To:** Deadbelt Operations Platform (DOP)
>
> **Audience:** Contributors, Architects, Maintainers
>
> **Related Documents:**
>
> * README.md
> * PROJECT_CHARTER.md
> * VISION.md
> * ROADMAP.md
> * architecture/overview.md
> * architecture/application-lifecycle.md
> * architecture/solution-structure.md
> * architecture/technology-stack.md
> * architecture/plugin-system.md

# Domain Model

## Overview

The Deadbelt Operations Platform (DOP) domain model defines the core business concepts used throughout the platform.

DOP is designed around managing complete operational environments rather than individual servers. This allows the platform to support local servers, hosted servers, multi-server communities, extensions, providers, automation, monitoring, and future intelligent operations without redesigning the core model.

---

# Core Concepts

## Workspace

A **Workspace** represents the highest-level organizational boundary in DOP.

A Workspace may represent:

* A game server community
* A development lab
* A hosting customer
* A group of related environments
* A future organization or tenant boundary

Example:

```text
Workspace: Deadbelt Community
```

A Workspace contains one or more Environments.

---

## Environment

An Environment represents a managed game server environment within a Workspace.

A Workspace is the top-level organizational boundary in DOP. An Environment is the next major operational boundary inside a Workspace. It is where future game server configuration, provider settings, deployment state, jobs, backups, monitoring, and desired-state definitions will be organized.

An Environment is not just a single server. It represents the intended operational state for a managed game server environment.

Examples of future environments may include:

- Production DayZ Server
- Test DayZ Server
- Development Minecraft Server
- Staging Rust Server
- Custom Modded Environment

### Initial Environment Model

The initial Environment domain model includes:

- Environment ID
- Workspace path reference
- Environment name
- Environment description
- Game type
- Environment path
- Created UTC timestamp
- Environment version
- Environment status

### Environment ID

Each Environment has a unique identifier represented by `EnvironmentId`.

The ID is used to distinguish environments even if names change later.

### Game Type

The initial `GameType` values are:

- Unknown
- DayZ
- Minecraft
- Rust
- ArmaReforger
- Custom

This list is intentionally small and can be expanded as additional game ecosystems are supported.

The `Unknown` game type is reserved for unset, invalid, or fallback states and should not be exposed as a normal user selection in the Create Environment workflow.

### Environment Status

The initial `EnvironmentStatus` values are:

- Unknown
- Draft
- Active
- Disabled
- Archived

These statuses describe the lifecycle state of the Environment from the platform’s perspective.

### Environment Persistence

Environment persistence is handled through the infrastructure layer.

Each Environment is stored under the active Workspace folder in an `environments` directory.

Initial file layout:

    <WorkspaceFolder>
      environments
        <EnvironmentName>
          environment.json

Example:

    C:\Deadbelt\Workspaces\TestWorkspace
      environments
        production-dayz
          environment.json

The environment folder name is generated from the Environment name using a safe folder naming process. For example:

    Production DayZ

becomes:

    production-dayz

### Environment Metadata File

Each Environment is persisted as an `environment.json` metadata file.

Initial example:

    {
      "id": "00000000-0000-0000-0000-000000000000",
      "name": "Production DayZ",
      "description": "",
      "gameType": "DayZ",
      "environmentPath": "C:\\Deadbelt\\Workspaces\\TestWorkspace\\environments\\production-dayz",
      "createdUtc": "2026-07-02T00:00:00Z",
      "version": "0.1",
      "status": "Draft"
    }

The initial metadata file captures the Environment identity and lifecycle state. Future versions may expand this file or introduce additional files for desired state, provider configuration, mods, deployment state, jobs, backups, and monitoring.

### Environment Naming and Duplicate Prevention

Environment names are normalized into safe folder names before persistence.

Examples:

    Production DayZ
    production-dayz
    Production-DayZ
    Production    DayZ

all resolve to:

    production-dayz

Duplicate safe folder names are not allowed within the same Workspace.

If an Environment already exists at the generated path:

    <WorkspaceFolder>\environments\<safe-environment-name>\environment.json

then the Create Environment workflow fails with a clear validation message:

    An environment with this name already exists in the current workspace.

Duplicate validation is handled through the Application and Infrastructure layers. The Desktop UI does not perform direct filesystem duplicate checks.

### Environment Creation Service

Environment creation is handled through the Application layer.

The initial creation flow is:

    CreateEnvironmentRequest
        ↓
    EnvironmentService
        ↓
    Environment domain model
        ↓
    IEnvironmentStore
        ↓
    JsonEnvironmentStore
        ↓
    environment.json

The Desktop UI should not create `environment.json` directly. UI workflows should call the Application layer through `IEnvironmentService`.

### Create Environment UI Workflow

The desktop application includes an initial Create Environment workflow.

When a Workspace is active, the user can navigate to the Environments section and create a new Environment from the desktop UI.

The workflow is:

    Active Workspace
        ↓
    Environments Section
        ↓
    Create Environment Dialog
        ↓
    IEnvironmentService
        ↓
    EnvironmentService
        ↓
    IEnvironmentStore
        ↓
    JsonEnvironmentStore
        ↓
    environment.json

The Desktop UI does not create `environment.json` directly. It collects user input and calls the Application layer through `IEnvironmentService`.

### Create Environment Dialog

The initial dialog captures:

- Environment name
- Game type
- Optional description

The Environment name and Game type are required.

The initial supported game types are:

- DayZ
- Minecraft
- Rust
- ArmaReforger
- Custom

The `Unknown` game type is excluded from the dialog because it is reserved for unset, invalid, or fallback states.

### Loading Existing Environments

When an existing Workspace is opened, DOP loads persisted Environments from disk.

The expected folder layout is:

    <WorkspaceFolder>
      environments
        production-dayz
          environment.json
        test-server
          environment.json

The loading workflow is:

    Open Workspace
        ↓
    WorkspaceService loads workspace.json
        ↓
    MainWindowViewModel sets the active Workspace
        ↓
    IEnvironmentService loads Environments by Workspace path
        ↓
    JsonEnvironmentStore scans the environments folder
        ↓
    Valid environment.json files are rehydrated into Environment domain models
        ↓
    The Environments section displays the loaded Environments

The Desktop UI does not read `environment.json` directly. It requests Environment data through the Application layer using `IEnvironmentService`.

### Environment Loading Behavior

The initial loading implementation supports:

- Loading existing Environments when a Workspace is opened
- Returning an empty list when the `environments` folder does not exist
- Returning an empty list when the `environments` folder is empty
- Skipping folders that do not contain `environment.json`
- Skipping malformed or invalid Environment metadata without crashing the application
- Displaying loaded Environments in the Environments section

This completes the initial Environment persistence loop:

    Create Environment
        ↓
    Write environment.json
        ↓
    Close application
        ↓
    Reopen application
        ↓
    Open Workspace
        ↓
    Load existing Environments
        ↓
    Display Environments in UI

### Environment Display

Environments are displayed in the Environments section of the active Workspace shell.

The initial display includes:

- Environment name
- Description
- Game type
- Status
- Environment path

When a Workspace is opened, persisted Environments are loaded from disk and displayed in the Environments section.

When a new Environment is created, it is added to the current UI session and persisted to disk.

### Environment Detail View

The desktop application now includes an initial read-only Environment detail view.

When a Workspace contains one or more Environments, the Environments section displays a selectable Environment list and a detail panel for the selected Environment.

The initial detail view displays:

- Environment name
- Description
- Game type
- Status
- Environment ID
- Environment path
- Created UTC timestamp
- Version

The detail view is currently read-only.

The selection workflow is:

    Open Workspace
        ↓
    Load existing Environments
        ↓
    Navigate to Environments
        ↓
    Select Environment
        ↓
    Display Environment metadata in detail panel

When a Workspace is opened and Environments are loaded, the first Environment is selected automatically.

When a new Environment is created, the newly created Environment is selected automatically and displayed in the detail panel.

Future issues may expand this detail view to include editing, archiving, provider configuration, game-specific configuration, deployment state, jobs, backups, monitoring, and desired-state comparison.

### Current Environment Capability Scope

The current Environment implementation supports:

- Environment domain model
- Environment ID generation
- Game type tracking
- Environment status tracking
- Environment metadata persistence
- Safe environment folder name generation
- Duplicate Environment name prevention
- Creating an Environment while a Workspace is active
- Writing `environment.json`
- Loading existing Environments when opening a Workspace
- Displaying Environments in the desktop UI
- Showing an empty state when no Environments exist
- Selecting an Environment in the desktop UI
- Viewing read-only Environment metadata in the detail panel

The following are still out of scope:

- Editing Environments
- Deleting Environments
- Environment dashboard integration
- Provider configuration
- Game-specific configuration
- Mod management
- Deployment
- Job execution
- Deployment state
- Job history
- Desired-state comparison
- Repairing malformed Environment metadata

### Relationship to Desired State

The Environment model is an early foundation for DOP’s desired-state architecture.

Future versions of the Environment model will describe what the environment should contain, including providers, configuration, packages, mods, jobs, schedules, backups, and monitoring rules.

The long-term goal is for DOP to compare desired environment state against actual state and determine what actions are needed to bring the environment into compliance.

---

## Server

A **Server** represents a managed game server instance.

A Server may be:

* Local
* Remote
* Self-hosted
* Hosted by a provider
* Physical
* Virtual
* Containerized
* Cloud-hosted

A Server belongs to an Environment.

A Server does not define the entire operational state by itself. It is one component within an Environment.

---

## Provider

A **Provider** represents an external system or platform that DOP interacts with.

Examples:

* Local Windows host
* Linux host
* SteamCMD
* Hosting provider
* RCON provider
* Backup provider
* Notification provider
* Monitoring provider
* Community service provider

Providers allow DOP to orchestrate systems it does not own.

---

## Extension

An **Extension** adds optional platform functionality.

Extensions may provide:

* Hosting integrations
* Notification systems
* Backup targets
* Monitoring tools
* Game-specific support
* Automation modules
* AI-assisted features

Extensions should integrate through documented interfaces and should not require changes to the core platform.

---

## Configuration

A **Configuration** represents settings that define how an Environment or Server should behave.

Configuration may include:

* Server name
* Ports
* IP bindings
* Runtime settings
* Gameplay settings
* File paths
* Launch parameters
* Provider settings
* Extension settings

DOP should treat configuration as desired state whenever possible.

---

## Package

A **Package** represents a reusable group of related items that can be applied to one or more Environments.

Packages may contain:

* Mods
* Configuration fragments
* Mission files
* Economy settings
* Event definitions
* Extension settings

Packages allow repeatable deployment across Development, Testing, and Production environments.

---

## Mod

A **Mod** represents a game modification known to DOP.

A Mod may include:

* Name
* Source
* Version
* Workshop ID
* Local path
* Dependencies
* Load order
* Client/server classification

Mods may be managed directly or through Packages.

---

## Deployment

A **Deployment** represents the process of applying desired state to an Environment.

A Deployment may include:

* Configuration changes
* Mod updates
* Server updates
* Package changes
* File synchronization
* Backup creation
* Validation
* Restart operations
* Rollback metadata

Deployments should be auditable and repeatable.

---

## Job

A **Job** represents a unit of work executed by DOP.

Examples:

* Start server
* Stop server
* Restart server
* Update mods
* Create backup
* Validate configuration
* Deploy environment
* Run health check

Jobs may be manually triggered, scheduled, or event-driven.

---

## Schedule

A **Schedule** defines when Jobs should run.

Examples:

* Restart every 4 hours
* Backup every 6 hours
* Check for updates daily
* Run health checks every 5 minutes
* Send restart warnings before maintenance

Schedules belong to an Environment or Workspace.

---

## Backup

A **Backup** represents a restorable snapshot of part or all of an Environment.

Backups may include:

* Configuration
* Profiles
* Mission files
* Economy files
* Logs
* Databases
* Mod manifests
* Deployment metadata

Backup storage may be local or provider-based.

---

## Secret

A **Secret** represents sensitive information.

Examples:

* API keys
* RCON passwords
* Steam credentials
* Hosting provider tokens
* Encryption keys
* Webhook URLs

Secrets must never be stored in plain text unless explicitly allowed by a secure local development mode.

---

## Monitoring Signal

A **Monitoring Signal** represents operational data collected from an Environment or Server.

Examples:

* Server online/offline status
* Player count
* CPU usage
* Memory usage
* Restart count
* Failed jobs
* Mod update status
* Backup status
* Health check result

Monitoring Signals may be used for dashboards, alerts, automation, and future intelligent recommendations.

---

# Conceptual Hierarchy

```text
Workspace
│
├── Environment
│   │
│   ├── Server
│   ├── Configuration
│   ├── Packages
│   │   └── Mods
│   ├── Providers
│   ├── Extensions
│   ├── Deployments
│   ├── Jobs
│   ├── Schedules
│   ├── Backups
│   ├── Secrets
│   └── Monitoring Signals
│
└── Environment
```

---

# Desired State Model

DOP should prefer a desired-state model.

Instead of only asking:

```text
What is currently running?
```

DOP should also ask:

```text
What should this environment look like?
```

The platform can then compare desired state against actual state and determine whether changes are required.

This enables:

* Environment drift detection
* Deployment previews
* Safer updates
* Repeatable configuration
* Rollback support
* Environment promotion

---

# Environment Promotion

DOP should support promoting changes between Environments.

Example:

```text
Development
    ↓
Testing
    ↓
Production
```

Promotion may include:

* Configuration changes
* Package changes
* Mod updates
* Mission updates
* Economy updates
* Extension settings

Promotion should include validation and backup creation before changes are applied.

---

# Provider Boundary

DOP should orchestrate providers, not own them.

For example, DOP may interact with a hosting provider API, but the hosting provider remains responsible for its own infrastructure.

This boundary is important for:

* Legal clarity
* Maintainability
* Provider neutrality
* Extension development
* Security

---

# Initial Domain Priorities

The first implementation should focus on these domain objects:

1. Workspace
2. Environment
3. Server
4. Configuration
5. Provider
6. Extension
7. Mod
8. Package
9. Deployment
10. Secret

Other concepts may be introduced as the platform matures.

---

# Design Principle

The domain model should remain as platform-agnostic as possible.

Game-specific concepts should live in providers, extensions, or implementation layers unless they are truly universal to game server operations.

The core domain should describe operations, environments, providers, automation, and deployment rather than any single game.
