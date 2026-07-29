> **Status:** Implemented
>
> **Version:** 1.0
>
> **Last Updated:** 2026-07-28
>
> **Applies To:** Deadbelt Operations Platform (DOP)
>
> **Audience:** Contributors, Architects, Maintainers

# JSON Persistence and Load Diagnostics

## Scope

DOP persists Workspace, Environment, Provider, and recent-Workspace metadata as
JSON. Infrastructure owns the filesystem and serialization implementation.
Application owns the load-result and diagnostic contracts consumed by services
and user interfaces.

This design does not change the existing JSON property names, casing, enum
representations, required fields, or stable storage paths.

## Atomic Write Lifecycle

All four JSON stores use the shared Infrastructure atomic writer.

1. The store validates the operation and creates only directories already
   permitted by its existing behavior.
2. The writer creates a unique temporary file in the destination directory.
3. The complete document is serialized to the temporary file.
4. Buffered data is flushed asynchronously, followed by a flush to disk.
5. Cancellation is checked for the final time. The commit boundary begins
   immediately after this check.
6. A new destination is committed with a non-overwriting same-directory move.
   An existing destination is committed with `File.Replace`.
7. Any temporary file remaining after success or failure is deleted.

Temporary names have this form:

```text
.<destination-file-name>.<unique-id>.deadbelt.tmp
```

The temporary file is always a sibling of the destination. `File.Replace`
preserves relevant Windows metadata from an existing destination where the
platform supports it, including its security and filesystem attributes.
Replacement does not request or retain a backup file. A replacement failure is
propagated without falling back to delete-then-move or another weaker commit.

Before the commit point, serialization, cancellation, or I/O failure leaves an
existing destination unchanged. Expected pre-commit cancellation is rethrown
without an Error log. Once the move or replacement begins, cancellation is not
observed so the commit cannot be interrupted by the caller's cancellation
token. A failed commit is reported to the caller and logged; cleanup is
attempted without hiding the original failure. Cleanup failure is logged
separately and can leave a temporary file for later operator cleanup.

Atomicity relies on the operating system's same-volume move semantics. DOP does
not provide multi-file transactions, backups, or cloud synchronization.
Flushing the temporary file improves data durability, but it does not make the
directory replacement fully transactional across sudden power loss.

`File.Replace` has platform-defined failure modes and requires the destination
to exist. DOP therefore uses it only for an expected existing destination and
does not claim stronger guarantees than the underlying filesystem provides.

## Operation-First Read Classification

Persistence loads attempt file reads and directory enumeration directly. They
do not use `File.Exists` or `Directory.Exists` to distinguish missing resources
from inspection failures.

* Missing required Workspace metadata produces a blocking missing diagnostic.
* Missing optional recent settings produce valid empty history.
* Missing optional Environment or Provider collection directories produce
  valid empty collections.
* Missing child metadata produces a recoverable missing diagnostic.
* Invalid JSON produces an invalid diagnostic.
* Access failures and I/O failures produce unreadable diagnostics.

Exceptions and technical details remain in internal logs. Diagnostic messages
contain the precise configured file or collection path and no exception text.

## Structured Load Results

Store and service load operations return `PersistenceLoadResult<T>`. It contains
the successfully loaded value plus zero or more `PersistenceDiagnostic`
instances. A diagnostic provides:

* A stable code
* Severity
* Resource category
* Source path
* A safe operator-facing message

Raw exceptions are retained in internal logs and are not exposed through the
Application contract or Desktop view model.

Current stable codes are:

| Resource | Codes |
| --- | --- |
| Workspace | `DOP.Persistence.Workspace.MetadataMissing`, `DOP.Persistence.Workspace.MetadataInvalid`, `DOP.Persistence.Workspace.MetadataUnreadable` |
| Environment | `DOP.Persistence.Environment.MetadataMissing`, `DOP.Persistence.Environment.MetadataInvalid`, `DOP.Persistence.Environment.MetadataUnreadable`, `DOP.Persistence.Environment.CollectionUnreadable` |
| Provider | `DOP.Persistence.Provider.MetadataMissing`, `DOP.Persistence.Provider.MetadataInvalid`, `DOP.Persistence.Provider.MetadataUnreadable`, `DOP.Persistence.Provider.CollectionUnreadable` |
| Recent Workspaces | `DOP.Persistence.RecentWorkspaces.SettingsInvalid`, `DOP.Persistence.RecentWorkspaces.SettingsUnreadable` |

## Blocking and Recoverable Failures

Workspace metadata is required. Missing, invalid, or unreadable
`workspace.json` produces an error diagnostic and prevents the Workspace from
opening.

Environment and Provider metadata is loaded independently by child directory.
A missing, invalid, or unreadable child produces a warning diagnostic without
suppressing valid siblings. The result therefore distinguishes a genuinely
empty collection from a partially loaded collection.

Recent-Workspace settings are optional local convenience data. Missing settings
produce an empty result without a diagnostic. Invalid or unreadable settings
produce a warning and do not crash startup. Valid entries are retained when
individual entries are invalid; ordering, deduplication, the ten-entry maximum,
and removal behavior remain Application-service responsibilities.

After a Workspace opens, the Desktop displays recoverable warnings in a
non-modal diagnostics area with the warning count, affected resource or path,
and safe message. A load with no diagnostics retains the normal Workspace
appearance. Diagnostics are cleared only when a new active Workspace is
successfully established or the current Workspace is unloaded. Failed open
attempts preserve the active Workspace and its warnings. Reloading a resource
category replaces only that category, preserving unrelated warnings.

## Pre-Alpha API Changes

Store and service load methods now return `PersistenceLoadResult<T>` instead of
bare entities or collections. `OpenWorkspaceResult` now carries structured
diagnostics. Result construction uses success and blocking-failure factories:
success rejects Error diagnostics, blocking failure requires at least one Error
diagnostic, and diagnostic collections are immutable snapshots.

## Logging and Future Doctor Integration

Infrastructure logs failed reads, failed writes, failed atomic commits, and
temporary-file cleanup failures with the original exception and technical
context. Application services log unexpected load failures and propagate only
safe structured diagnostics.

The stable diagnostic model is suitable for future DOP Doctor findings, but
this implementation performs no product-specific Doctor scan. A future Doctor
can consume or translate these codes without requiring Desktop callers to
compare message text.
