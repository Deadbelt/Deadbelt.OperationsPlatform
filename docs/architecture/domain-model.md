> **Status:** Draft
>
> **Version:** 0.1
>
> **Last Updated:** 2026-07-18
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

### Recent Workspaces

Recent Workspaces are local application convenience data used by the desktop shell.

Recent Workspace tracking allows DOP to remember Workspaces that were recently created or opened so users can reopen them without browsing to the folder each time.

Recent Workspace data is not part of Workspace domain metadata.

Recent Workspace data is stored outside the Workspace folder.

Initial stored data includes:

- Workspace name
- Workspace path
- Last opened UTC timestamp

Recent Workspaces are currently stored locally in the user profile at:

    %AppData%\Deadbelt\OperationsPlatform\settings.json

Recent Workspace tracking does not modify `workspace.json`.

The initial Recent Workspace workflow supports:

- Recording created Workspaces
- Recording opened Workspaces
- Persisting recent Workspace history locally
- Displaying recent Workspaces on the no-workspace landing screen
- Displaying recent Workspaces on the Workspace Overview page
- Opening a selected recent Workspace
- Showing which recent Workspace is currently active
- Disabling Open Selected when the selected recent Workspace is already active

Future workflows may support pinning, removing, sorting, validation, health checks, or automatic startup restore.

Recent Workspace entries can be removed from local recent history.

Removing a Recent Workspace entry:

- Removes the entry from the local recent Workspace list
- Updates `%AppData%\Deadbelt\OperationsPlatform\settings.json`
- Does not delete the Workspace folder
- Does not modify `workspace.json`
- Does not close the currently active Workspace

Recent Workspace removal is a local convenience cleanup workflow only.

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

### Environment Status Badge Display

The desktop UI displays Environment status values as badge-style labels.

Status badges appear in:

- Environment list items
- Environment detail panel

The badge display is UI-only. The actual Environment status remains stored as metadata in `environment.json`.

Initial status values include:

- Draft
- Active
- Disabled
- Archived
- Unknown

Archived Environments continue to use the archived UI state in addition to the status badge. This means Archived Environments may appear muted, show archived-state messaging, and display the `Archived` badge.

Status badges do not change Environment lifecycle behavior. Status transitions are still controlled through Application-layer workflows such as archive and restore.

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

### Environment Identity, Slug, and Rename Behavior

DOP separates Environment identity, storage identity, and display name.

The model is:

    Environment ID = permanent identity
    Environment folder / slug = stable storage identity
    Environment name = editable display name

The `EnvironmentId` is the permanent identifier for an Environment.

The Environment folder path is generated when the Environment is first created. This folder path acts as the stable storage location for Environment metadata and future Environment-owned files.

The Environment name is user-facing metadata. It can be edited without changing the Environment ID or the Environment folder path.

When an Environment is created:

- DOP generates an Environment ID
- DOP generates a safe folder name from the initial Environment name
- DOP creates the Environment folder
- DOP stores the Environment path in `environment.json`

When an Environment is edited:

- The Environment name may change
- The Environment description may change
- The Environment game type may change
- The Environment ID does not change
- The Environment path does not change
- The Environment folder is not renamed

Example:

    Initial Environment name:
    Production DayZ

    Generated folder:
    production-dayz

    Later display name:
    Main DayZ Server

    Folder remains:
    production-dayz

This behavior is intentional.

Renaming or moving Environment folders is out of scope for the initial Environment lifecycle. A future folder rename or migration workflow would need to safely handle metadata updates, provider configuration, game files, job history, backups, deployment state, logs, and any other files stored under the Environment folder.

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

### Environment List Filtering

The desktop shell supports filtering the visible Environment list by Environment status.

Initial filter options include:

- All
- Draft
- Active
- Disabled
- Archived

Environment filtering is a UI-only workflow.

Filtering does not modify Environment metadata, does not change `environment.json`, and does not affect the stored Environment lifecycle state.

The full Environment list remains loaded in memory, while the desktop UI displays the filtered visible list based on the selected status filter.

Archived Environments remain stored, loadable, and restorable even when hidden by the current filter.

### Environment Search

The desktop shell supports searching Environments by text.

Initial search matching includes:

- Environment name
- Description
- Game type
- Status
- Environment path

Environment search is a UI-only workflow.

Search does not modify Environment metadata, does not change `environment.json`, and does not affect the stored Environment lifecycle state.

Search is applied together with the active Environment status filter.

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

### Editing Environment Metadata

The desktop application now includes an initial workflow for editing basic Environment metadata.

Editable fields:

- Environment name
- Description
- Game type

Immutable fields:

- Environment ID
- Environment path
- Created UTC timestamp
- Version

The edit workflow is:

    Open Workspace
        ↓
    Load existing Environments
        ↓
    Navigate to Environments
        ↓
    Select Environment
        ↓
    Click Edit
        ↓
    Update metadata
        ↓
    Save
        ↓
    IEnvironmentService updates the Environment
        ↓
    JsonEnvironmentStore updates environment.json
        ↓
    UI refreshes selected Environment

The Desktop UI does not write `environment.json` directly. Metadata updates are routed through the Application layer using `IEnvironmentService`.

The initial edit workflow keeps the existing Environment folder path unchanged, even if the Environment display name changes.

This follows the Environment identity model:

    Environment ID = permanent identity
    Environment folder / slug = stable storage identity
    Environment name = editable display name

Folder rename behavior is intentionally out of scope for the initial edit workflow.

Duplicate Environment name validation is enforced during edits. If the updated Environment name would conflict with another Environment in the same Workspace, the update is blocked with a clear validation message.

### Archiving Environments

The desktop application now includes an initial Environment archive workflow.

Archiving marks an Environment as archived without deleting, moving, or renaming any files.

The archive workflow is:

    Open Workspace
        ↓
    Load existing Environments
        ↓
    Navigate to Environments
        ↓
    Select Environment
        ↓
    Click Archive
        ↓
    Confirm archive action
        ↓
    IEnvironmentService archives the Environment
        ↓
    JsonEnvironmentStore updates environment.json
        ↓
    UI refreshes selected Environment status

When an Environment is archived, its status is updated to:

    Archived

The existing `environment.json` file is updated in place.

Archiving does not:

- Delete the Environment folder
- Delete `environment.json`
- Delete provider files
- Delete game files
- Rename the Environment folder
- Move the Environment folder
- Remove the Environment from the Workspace

Archived Environments remain loadable when the Workspace is reopened.

Permanent deletion, restore from archive, filtering archived Environments, provider shutdown, job cleanup, and deployment cleanup are future workflows.

### Archived Environment UI State

Archived Environments remain visible and loadable in the desktop UI.

When an Environment status is `Archived`:

- The Environment remains in the Environments list
- The Environment still appears in the detail panel
- The Environment list item is visually muted
- The detail panel displays an archived-state message
- The Archive action is disabled
- The user is not prompted to archive an already archived Environment

Archived state is metadata-only. It does not delete, move, rename, or remove Environment files.

### Restoring Archived Environments

The desktop application includes an initial workflow for restoring Archived Environments.

Restoring changes an Environment status from:

    Archived

to:

    Draft

The restore workflow is:

    Open Workspace
        ↓
    Load existing Environments
        ↓
    Navigate to Environments
        ↓
    Select Archived Environment
        ↓
    Click Restore
        ↓
    Confirm restore action
        ↓
    IEnvironmentService restores the Environment
        ↓
    JsonEnvironmentStore updates environment.json
        ↓
    UI refreshes selected Environment status

Restore is metadata-only.

Restoring does not:

- Delete files
- Move files
- Rename folders
- Restore provider state
- Restore deployment state
- Restore job state
- Restore backups

The existing `environment.json` file is updated in place.

The initial restore workflow always restores an Archived Environment to `Draft`. Future workflows may support restoring to a previous status.

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
- Editing basic Environment metadata
- Updating Environment name
- Updating Environment description
- Updating Environment game type
- Persisting edited metadata to `environment.json`
- Archiving Environments
- Updating Environment status to `Archived`
- Persisting archived status to `environment.json`
- Loading archived Environments when reopening a Workspace
- Stable Environment folder path behavior after creation
- Editable Environment display names without folder rename
- Visually distinguishing Archived Environments in the desktop UI
- Disabling Archive action for already archived Environments
- Displaying archived-state messaging in the detail panel
- Restoring Archived Environments
- Updating Environment status from `Archived` to `Draft`
- Persisting restored status to `environment.json`
- Loading restored Environments when reopening a Workspace
- Displaying Environment status as a badge in the desktop UI
- Showing status badges in the Environment list
- Showing status badges in the Environment detail panel
- Filtering the visible Environment list by status
- Searching Environments by name, description, game type, status, and path
- Applying status filters and search text together
- Showing an empty state when no Environments match the current filter or search

The following are still out of scope:

- Permanently deleting Environments
- Provider shutdown during archive
- Deployment changes during restore
- Backup restore
- Deployment cleanup
- Job cleanup
- Backup cleanup
- Renaming Environment folders
- Environment dashboard integration
- Provider configuration
- Environment-to-Provider association
- Game-specific configuration
- Mod management
- Deployment
- Job execution
- Deployment state
- Job history
- Desired-state comparison
- Repairing malformed Environment metadata
- Saved filters
- Advanced search
- User-configurable status colors
- Advanced status transition workflows

### Relationship to Desired State

The Environment model is an early foundation for DOP’s desired-state architecture.

Future versions of the Environment model will describe what the environment should contain, including providers, configuration, packages, mods, jobs, schedules, backups, and monitoring rules.

The long-term goal is for DOP to compare desired environment state against actual state and determine what actions are needed to bring the environment into compliance.

---

## Provider

A **Provider** represents a Workspace-level external system, host, platform, or service that DOP can interact with.

Providers are how DOP models systems it does not own but may need to communicate with, validate, monitor, or orchestrate.

Examples:

* Local Windows host
* Local Linux host
* SteamCMD
* Hosting provider
* RCON provider
* Backup provider
* Notification provider
* Monitoring provider
* Community service provider
* Custom provider

Providers allow DOP to orchestrate systems it does not own while keeping provider-specific behavior outside the core Environment model.

### Initial Provider Model

The initial Provider model includes:

- Provider ID
- Workspace path reference
- Provider name
- Provider type
- Provider path
- Provider status
- Created UTC timestamp
- Provider version

The Provider model now supports creation and JSON metadata persistence through the Application and Infrastructure layers.

This issue does not add Provider UI, loading workflows, editing, health checks, secrets, execution, or Environment association.

### Provider ID

Each Provider has a unique identifier represented by `ProviderId`.

The ID is used to distinguish Providers even if names change later.

### Provider Type

The initial `ProviderType` values are:

- Unknown
- LocalWindows
- LocalLinux
- SteamCmd
- Rcon
- HostingProvider
- BackupProvider
- MonitoringProvider
- NotificationProvider
- Custom

This list is intentionally broad enough to support local infrastructure, hosted infrastructure, game server tooling, monitoring, backups, and future integrations.

The `Unknown` provider type is reserved for unset, invalid, or fallback states and should not be exposed as a normal user selection in future Provider creation workflows.

### Provider Status

The initial `ProviderStatus` values are:

- Unknown
- Draft
- Configured
- Disabled
- Error
- Archived

These statuses describe the lifecycle state of the Provider from the platform’s perspective.

Initial status meanings:

- `Draft` means the Provider has been defined but is not ready for operational use.
- `Configured` means the Provider has enough configuration to be used by future workflows.
- `Disabled` means the Provider should not be used for active operations.
- `Error` means DOP has detected a Provider-level issue in a future validation or health workflow.
- `Archived` means the Provider has been retired without deleting historical metadata.
- `Unknown` is reserved for unset, invalid, or fallback states.

### Provider Persistence

Provider persistence is handled through the infrastructure layer.

Each Provider is stored under the active Workspace folder in a `providers` directory.

Initial file layout:

    <WorkspaceFolder>
      providers
        <ProviderName>
          provider.json

Example:

    C:\Deadbelt\Workspaces\TestWorkspace
      providers
        local-windows
          provider.json

The Provider folder name is generated from the Provider name using a safe folder naming process. For example:

    Local Windows

becomes:

    local-windows

### Provider Metadata File

Each Provider is persisted as a `provider.json` metadata file.

Initial example:

    {
      "id": "00000000-0000-0000-0000-000000000000",
      "workspacePath": "C:\\Deadbelt\\Workspaces\\TestWorkspace",
      "name": "Local Windows",
      "providerType": "LocalWindows",
      "providerPath": "C:\\Deadbelt\\Workspaces\\TestWorkspace\\providers\\local-windows",
      "createdUtc": "2026-07-18T00:00:00Z",
      "version": "0.1",
      "status": "Draft"
    }

The initial metadata file captures Provider identity and lifecycle state only.

Future versions may expand Provider metadata or introduce additional files for provider configuration, connection settings, health state, capability discovery, Environment associations, secrets references, and operational history.

### Provider Naming and Duplicate Prevention

Provider names are normalized into safe folder names before persistence.

Examples:

    Local Windows
    local-windows
    Local-Windows
    Local    Windows

all resolve to:

    local-windows

Duplicate safe folder names are not allowed within the same Workspace.

If a Provider already exists at the generated path:

    <WorkspaceFolder>\providers\<safe-provider-name>\provider.json

then the Create Provider workflow fails with a clear validation message:

    A provider with this name already exists in the current workspace.

Duplicate validation is handled through the Application and Infrastructure layers. Future Desktop UI workflows should not perform direct filesystem duplicate checks.

### Provider Creation Service

Provider creation is handled through the Application layer.

The initial creation flow is:

    CreateProviderRequest
        ↓
    ProviderService
        ↓
    Provider domain model
        ↓
    IProviderStore
        ↓
    JsonProviderStore
        ↓
    provider.json

The Desktop UI should not create `provider.json` directly. Future UI workflows should call the Application layer through `IProviderService`.

The initial Create Provider workflow validates:

- Workspace path is provided
- Workspace path exists
- Provider name is provided
- Provider type is not `Unknown`
- Duplicate Provider safe folder names are blocked

### Provider Boundary

DOP should orchestrate Providers, not own them.

For example, DOP may communicate with a hosting provider API, but the hosting provider remains responsible for its own infrastructure.

The Provider boundary is important for:

- Provider neutrality
- Security
- Maintainability
- Extension development
- Legal clarity
- Clear operational responsibility

### Provider Relationship to Workspaces and Environments

The initial Provider model is scoped to a Workspace.

A Provider belongs to a Workspace and may later be associated with one or more Environments.

This allows DOP to define Providers once and reuse them across future Environment workflows.

The initial Provider creation and persistence workflow does not create an Environment-to-Provider association yet.

Future workflows may support:

- Assigning a Provider to an Environment
- Showing Provider status on an Environment
- Using Provider health in Environment status rollups
- Running Provider-specific validation
- Running Provider-specific operations
- Surfacing Provider outage or status links

### Current Provider Capability Scope

The current Provider implementation supports:

- Provider domain model
- Provider ID generation
- Provider name tracking
- Provider type tracking
- Provider status tracking
- Workspace path reference
- Provider path tracking
- Created UTC timestamp
- Provider version
- Provider creation request/result model
- Provider service abstraction
- Provider store abstraction
- Provider creation service
- JSON-backed Provider metadata persistence
- Safe Provider folder name generation
- Duplicate Provider safe-name prevention
- Writing `provider.json`
- Dependency injection registration for Provider services

The following are still out of scope:

- Provider loading
- Provider UI
- Editing Providers
- Archiving Providers
- Restoring Providers
- Deleting Providers
- Environment-to-Provider association
- Provider configuration forms
- Provider secrets
- Provider validation
- Provider execution
- Provider health checks
- Provider connectivity monitoring
- Provider or ISP outage awareness
- Provider status or outage URL handling
- SteamCMD integration
- RCON integration
- Hosting provider API integration
- Agent integration

### Relationship to Provider Health and Outage Awareness

Future Provider workflows may monitor whether DOP can reach a Provider.

If an Environment is down or unreachable and the associated Provider cannot be reached, DOP should eventually help the user distinguish between an Environment issue, Provider issue, ISP issue, or local connectivity issue.

Future Provider health workflows may also surface Provider or ISP outage/status links when available.

This is intentionally out of scope for the initial Provider creation and persistence workflow.

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
├── Environments
│   └── Environment
│       │
│       ├── Server
│       ├── Configuration
│       ├── Packages
│       │   └── Mods
│       ├── Provider Associations
│       ├── Extensions
│       ├── Deployments
│       ├── Jobs
│       ├── Schedules
│       ├── Backups
│       ├── Secrets
│       └── Monitoring Signals
│
├── Providers
└── Extensions
```

Providers are modeled at the Workspace level first. Future Environment-to-Provider association workflows will connect Environments to one or more Providers.

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

DOP should orchestrate Providers, not own them.

For example, DOP may interact with a hosting provider API, but the hosting provider remains responsible for its own infrastructure.

This boundary is important for:

* Legal clarity
* Maintainability
* Provider neutrality
* Extension development
* Security
* Clear operational responsibility

Provider-specific behavior should live in Provider implementations, extensions, or infrastructure layers rather than being hard-coded into the core Environment model.

---

# Initial Domain Priorities

The initial and near-term implementation should focus on these domain objects:

1. Workspace
2. Environment
3. Provider
4. Server
5. Configuration
6. Extension
7. Mod
8. Package
9. Deployment
10. Secret

Workspace and Environment are already the active foundation. Provider is the next active domain object so DOP can begin modeling external systems, hosts, services, and future integrations.

Other concepts may be introduced as the platform matures.

---

# Design Principle

The domain model should remain as platform-agnostic as possible.

Game-specific concepts should live in providers, extensions, or implementation layers unless they are truly universal to game server operations.

The core domain should describe operations, environments, providers, automation, and deployment rather than any single game.
