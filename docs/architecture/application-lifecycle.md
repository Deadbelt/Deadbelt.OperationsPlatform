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
> * architecture/domain-model.md
> * architecture/solution-structure.md
> * architecture/plugin-system.md
> * architecture/technology-stack.md

# Application Lifecycle

## Overview

The Deadbelt Operations Platform (DOP) follows a predictable startup and shutdown lifecycle.

A well-defined lifecycle ensures that providers, extensions, configuration, secrets, and user interfaces are initialized in a consistent and reliable manner.

Each stage should complete successfully before progressing to the next. If a critical stage fails, the application should stop safely and provide meaningful diagnostics.

---

# Startup Sequence

```text
User Launches DOP
        │
        ▼
Application Bootstrap
        │
        ▼
Load Configuration
        │
        ▼
Initialize Logging
        │
        ▼
Initialize Dependency Injection
        │
        ▼
Load Secret Providers
        │
        ▼
Discover Extensions
        │
        ▼
Validate Extensions
        │
        ▼
Load Providers
        │
        ▼
Initialize Workspace
        │
        ▼
Load Environments
        │
        ▼
Build UI
        │
        ▼
Ready
```

---

## Stage 1 – Bootstrap

Responsibilities:

* Verify runtime requirements
* Verify application version
* Create required folders
* Load bootstrap configuration
* Display splash screen (future)

No provider or extension code should execute during this stage.

---

## Stage 2 – Configuration

Responsibilities:

* Load platform configuration
* Load user settings
* Validate configuration
* Apply defaults where necessary

Configuration should be considered immutable after startup unless explicitly modified.

---

## Stage 3 – Logging

Responsibilities:

* Initialize logging providers
* Configure log levels
* Create log directories
* Register structured logging

Logging should be available before extension loading begins.

---

## Stage 4 – Dependency Injection

Responsibilities:

* Register platform services
* Register infrastructure services
* Register application services
* Build service provider

No provider should manually instantiate dependencies.

---

## Stage 5 – Secret Providers

Responsibilities:

* Initialize secret storage
* Validate secret provider availability
* Load required credentials

Secrets should remain encrypted whenever possible.

---

## Stage 6 – Extension Discovery

Responsibilities:

* Scan extension directories
* Read extension manifests
* Identify compatible extensions
* Detect duplicate IDs

No extension code should execute during discovery.

---

## Stage 7 – Extension Validation

Responsibilities:

* Verify compatibility
* Verify dependencies
* Verify permissions
* Validate manifests

Invalid extensions should be skipped with appropriate logging.

---

## Stage 8 – Provider Registration

Responsibilities:

* Register providers
* Validate provider configuration
* Initialize provider connections
* Verify provider availability

Providers should register capabilities rather than directly modifying platform behavior.

---

## Stage 9 – Workspace Initialization

Responsibilities:

* Load available workspaces
* Validate workspace metadata
* Restore previous session if applicable

No environment-specific actions occur until a workspace is selected.

---

## Stage 10 – Environment Loading

Responsibilities:

* Load environment definitions
* Validate desired state
* Restore cached status
* Build operational model

The Environment becomes the primary unit of management after this stage.

---

## Stage 11 – User Interface

Responsibilities:

* Build navigation
* Load dashboard
* Display environment status
* Enable user interaction

The UI should communicate exclusively through Application services.

---

## Environment Detail Selection Lifecycle

The desktop shell supports an initial read-only Environment detail workflow.

When a Workspace is opened, DOP loads persisted Environments from disk through the Application layer. Loaded Environments are displayed in the Environments section.

The selection lifecycle is:

    Workspace opened
        ↓
    Environments loaded
        ↓
    Environments collection populated
        ↓
    First Environment selected automatically
        ↓
    Detail panel displays selected Environment metadata

When a user selects a different Environment from the list, the selected Environment state is updated in the desktop ViewModel and the detail panel refreshes.

When a new Environment is created, the newly created Environment is selected automatically and shown in the detail panel.

The detail panel is currently read-only. Editing, archiving, deleting, provider configuration, deployment state, jobs, and desired-state comparison remain future workflows.

---

## Environment Metadata Edit Lifecycle

The desktop shell supports an initial Environment metadata edit workflow.

The edit lifecycle is:

    Environment selected
        ↓
    User clicks Edit
        ↓
    Edit Environment dialog opens with current metadata
        ↓
    User updates editable fields
        ↓
    Desktop ViewModel sends update request to IEnvironmentService
        ↓
    EnvironmentService validates the update
        ↓
    JsonEnvironmentStore updates environment.json
        ↓
    Desktop UI refreshes selected Environment metadata

Editable fields are limited to:

- Environment name
- Description
- Game type

The following fields remain immutable during the initial edit workflow:

- Environment ID
- Environment path
- Created UTC timestamp
- Version

The Environment folder path is not renamed when the Environment display name changes.

The edit workflow enforces duplicate Environment name validation through the Application layer. The Desktop UI does not write `environment.json` directly.

---

## Environment Archive Lifecycle

The desktop shell supports an initial Environment archive workflow.

The archive lifecycle is:

    Environment selected
        ↓
    User clicks Archive
        ↓
    Confirmation prompt appears
        ↓
    User confirms archive action
        ↓
    Desktop ViewModel sends archive request to IEnvironmentService
        ↓
    EnvironmentService validates the request
        ↓
    Environment status changes to Archived
        ↓
    JsonEnvironmentStore updates environment.json
        ↓
    Desktop UI refreshes selected Environment status

Archiving updates Environment metadata only.

The archive workflow does not delete files, move folders, rename folders, stop providers, clean up jobs, remove backups, or remove the Environment from the Workspace.

Archived Environments remain loadable when the Workspace is reopened.

---

## Application Version Display

The desktop shell displays the current application version in the bottom status area.

The initial version display is static:

    v0.2.0-prealpha

The status bar displays both runtime status and application version.

Example:

    Ready                                      v0.2.0-prealpha

The version value is currently exposed through the desktop ViewModel. Future releases may derive the version from assembly metadata, build automation, release tags, or packaged installer metadata.

---

## Archived Environment UI Lifecycle

The desktop shell visually distinguishes Archived Environments after they are loaded or archived.

The archived UI lifecycle is:

    Environment loaded or archived
        ↓
    Environment status is Archived
        ↓
    Environment list item is visually muted
        ↓
    Detail panel displays archived-state message
        ↓
    Archive command is disabled

Archived Environments remain visible and selectable.

The Archive action is not available for Environments that are already archived. This prevents duplicate archive attempts and avoids unnecessary confirmation prompts.

Archived UI state is based on Environment metadata. No files are deleted, moved, renamed, or removed from the Workspace.

---

## Environment Restore Lifecycle

The desktop shell supports an initial workflow for restoring Archived Environments.

The restore lifecycle is:

    Archived Environment selected
        ↓
    User clicks Restore
        ↓
    Confirmation prompt appears
        ↓
    User confirms restore action
        ↓
    Desktop ViewModel sends restore request to IEnvironmentService
        ↓
    EnvironmentService validates the request
        ↓
    Environment status changes from Archived to Draft
        ↓
    JsonEnvironmentStore updates environment.json
        ↓
    Desktop UI refreshes selected Environment status

Restore is metadata-only.

The restore workflow does not move files, rename folders, start providers, restore deployment state, restore jobs, or restore backups.

The initial restore workflow always restores an Archived Environment to `Draft`. Future workflows may support restoring to the previous status.

---

## Environment Status Display Lifecycle

The desktop shell displays Environment status values as badge-style labels after Environments are loaded or updated.

The status display lifecycle is:

    Environment loaded or updated
        ↓
    Environment status read from metadata
        ↓
    Desktop ViewModel exposes status display helpers
        ↓
    UI displays status badge in list and detail panel

Status badge styling is UI-only. Environment status remains persisted as metadata in `environment.json`.

---

## Recent Workspace Lifecycle

The desktop shell supports initial Recent Workspace tracking.

The Recent Workspace lifecycle is:

    Workspace created or opened
        ↓
    Workspace metadata is recorded as recent
        ↓
    Recent Workspace settings are persisted locally
        ↓
    Recent Workspace list refreshes
        ↓
    User can reopen a recent Workspace from the desktop UI

Recent Workspace data is stored outside the Workspace folder at:

    %AppData%\Deadbelt\OperationsPlatform\settings.json

Recent Workspace data is local application convenience data. It is not part of Workspace domain metadata and does not modify `workspace.json`.

The desktop shell displays Recent Workspaces in two places:

- No-workspace landing screen
- Workspace Overview page

When a recent Workspace is already active, the UI marks it as `Active` and disables the Open Selected action for that item.

If a recent Workspace path is invalid or cannot be opened, the application shows an error and refreshes the Recent Workspace list without crashing.

Future workflows may support pinned Workspaces, removing recent entries, startup restore, Workspace health checks, and Workspace search.

The desktop shell also supports removing a Workspace from recent history.

The removal lifecycle is:

    Recent Workspace selected
        ↓
    User selects Remove
        ↓
    Confirmation prompt appears
        ↓
    Recent Workspace entry is removed from local settings
        ↓
    Recent Workspace list refreshes

Removing a Recent Workspace only updates local recent history.

It does not:

- Delete Workspace files
- Modify `workspace.json`
- Close the active Workspace
- Change Environment metadata

---

## Environment Filtering Lifecycle

The desktop shell supports filtering the Environment list by status.

The filtering lifecycle is:

    Workspace opened
        ↓
    Environments loaded from Workspace storage
        ↓
    Full Environment list is retained in memory
        ↓
    User selects a status filter
        ↓
    Visible Environment list updates
        ↓
    User selects or acts on a visible Environment

Initial filter options include:

- All
- Draft
- Active
- Disabled
- Archived

Environment filtering is UI-only.

Filtering does not:

- Modify Environment metadata
- Modify `environment.json`
- Change Environment status
- Delete or hide Environment files
- Prevent archived Environments from being restored

When an Environment changes status, such as being archived or restored, the visible list refreshes based on the active filter.

For example, if the user is viewing Draft Environments and archives one, that Environment is removed from the visible Draft list. It remains available under the Archived filter.

---

## Environment Search Lifecycle

The desktop shell supports searching the visible Environment list.

The search lifecycle is:

    Workspace opened
        ↓
    Environments loaded from Workspace storage
        ↓
    Full Environment list is retained in memory
        ↓
    User enters search text
        ↓
    Status filter and search text are applied together
        ↓
    Visible Environment list updates

Environment search is UI-only.

Search can match:

- Environment name
- Description
- Game type
- Status
- Environment path

Search does not:

- Modify Environment metadata
- Modify `environment.json`
- Change Environment status
- Delete or hide Environment files

When search text is entered, it works together with the selected Environment status filter.

For example, if the user selects the Archived filter and searches for a name, only Archived Environments matching that search text are displayed.

---

## Provider Creation Lifecycle

The application layer supports an initial Provider creation workflow.

The creation lifecycle is:

    CreateProviderRequest
        ↓
    ProviderService validates the request
        ↓
    ProviderService checks for duplicate Provider safe-name conflicts
        ↓
    Provider domain model is created
        ↓
    IProviderStore saves the Provider
        ↓
    JsonProviderStore creates the Provider folder
        ↓
    JsonProviderStore writes provider.json

The initial Provider persistence layout is:

    <WorkspaceFolder>
      providers
        <provider-safe-name>
          provider.json

Provider creation is Application-layer driven. UI clients should use `IProviderService` rather than writing `provider.json` directly.

Initial validation includes:

- Workspace path is required
- Workspace path must exist
- Provider name is required
- Provider type is required
- Duplicate Provider safe folder names are blocked

The initial Provider creation workflow creates metadata only.

It does not:

- Create Providers from the desktop UI
- Edit Providers
- Archive Providers
- Delete Providers
- Associate Providers with Environments
- Store secrets
- Validate Provider connectivity
- Execute Provider operations
- Monitor Provider health
---

## Provider Loading Lifecycle

The application layer supports loading persisted Providers from Workspace storage.

The loading lifecycle is:

    Workspace path provided
        ↓
    IProviderService validates the Workspace path
        ↓
    ProviderService requests Providers from IProviderStore
        ↓
    JsonProviderStore scans the providers folder
        ↓
    Valid provider.json files are read
        ↓
    Provider metadata is rehydrated into Provider domain models
        ↓
    Provider domain models are returned to the caller

The expected Provider storage layout is:

    <WorkspaceFolder>
      providers
        <provider-safe-name>
          provider.json

Provider loading supports:

- Returning an empty list when the `providers` folder does not exist
- Returning an empty list when the `providers` folder is empty
- Skipping folders that do not contain `provider.json`
- Skipping malformed or invalid Provider metadata without crashing
- Logging skipped Provider metadata for troubleshooting

Provider loading is Application/Infrastructure-layer support and is used by the desktop shell to display loaded Providers.

It does not:

- Create Providers from the desktop UI
- Edit Providers
- Archive Providers
- Delete Providers
- Associate Providers with Environments
- Store secrets
- Validate Provider connectivity
- Execute Provider operations
- Monitor Provider health


---

## Provider Display Lifecycle

The desktop shell supports an initial read-only Provider display workflow.

When a Workspace is opened, DOP loads persisted Providers from disk through the Application layer. Loaded Providers are displayed in the Providers section.

The display lifecycle is:

    Workspace opened
        ↓
    Providers loaded from Workspace storage
        ↓
    Providers collection populated
        ↓
    First Provider selected automatically
        ↓
    Provider detail panel displays selected Provider metadata

The initial Provider detail view displays:

- Provider name
- Provider type
- Provider status
- Provider ID
- Provider path
- Workspace path
- Created UTC timestamp
- Version

When a Workspace has no Providers, the Providers section displays an empty state.

The Provider display workflow is read-only.

It does not:

- Create Providers from the desktop UI
- Edit Providers
- Archive Providers
- Restore Providers
- Delete Providers
- Associate Providers with Environments
- Store secrets
- Validate Provider connectivity
- Execute Provider operations
- Monitor Provider health

---

## Desktop Interface Cleanup

The desktop shell received an interface cleanup pass focused on layout consistency, spacing, containment, and usability.

The cleanup focused on:

- No-workspace landing screen
- Recent Workspaces layout
- Workspace Overview layout
- Environment detail panel
- Environment action button layout
- Status bar spacing
- Default window-size usability

The cleanup did not change core business logic.

Existing workflows remain handled through the Application layer:

- Create Workspace
- Open Workspace
- Open Recent Workspace
- Create Environment
- Load Environment
- Edit Environment
- Archive Environment
- Restore Environment

The cleanup improved the default desktop experience so major workflow sections remain readable and contained at the default window size.

---

# Runtime

Once initialized, DOP enters the Runtime state.

Runtime responsibilities include:

* Job execution
* Scheduling
* Monitoring
* Provider communication
* UI interaction
* Extension execution
* Logging
* Notifications

---

# Shutdown Sequence

```text
User Exit
        │
        ▼
Cancel Running Jobs
        │
        ▼
Save State
        │
        ▼
Dispose Extensions
        │
        ▼
Dispose Providers
        │
        ▼
Flush Logs
        │
        ▼
Shutdown
```

Shutdown should be graceful whenever possible.

---

# Failure Recovery

If initialization fails:

* Log the failure
* Present a clear error to the user
* Stop startup safely
* Avoid partial initialization

The application should never continue in an unknown state.

---

# Future Agent Lifecycle

When the DOP Agent is introduced, it will follow a similar lifecycle:

* Bootstrap
* Load Configuration
* Initialize Providers
* Register Capabilities
* Connect to Manager
* Report Health
* Execute Commands
* Shutdown Gracefully

Keeping the desktop application and agent aligned simplifies diagnostics and maintenance.

---

# Design Principle

Every component in DOP should have a predictable lifecycle.

Predictable initialization and shutdown improve reliability, simplify troubleshooting, and make the platform easier to extend and maintain.