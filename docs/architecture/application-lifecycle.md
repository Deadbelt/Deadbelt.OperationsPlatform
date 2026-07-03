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