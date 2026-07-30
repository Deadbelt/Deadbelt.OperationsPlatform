> **Status:** Implemented MVP
>
> **Version:** 0.1
>
> **Last Updated:** 2026-07-29
>
> **Applies To:** Deadbelt Operations Platform (DOP)

# DOP Doctor: Local DayZ Scan

## Scope and ownership

DOP Doctor performs a read-only assessment of an operator-selected local DayZ
server installation. A request carries the active Workspace identity,
Environment identity, game type, server root, and optional startup and
configuration overrides. Results exist only in memory; changing Workspace or
Environment cancels an active scan and rejects late results.

- `Deadbelt.Domain` owns immutable findings, scan results, and inventory.
- `Deadbelt.Application` owns request validation, scanner selection,
  cancellation, and safe technical failures.
- `Deadbelt.Infrastructure` owns the read-only operating-system adapter,
  bounded traversal, DayZ assessment, and purpose-built parsers.
- `Deadbelt.Desktop` owns operator selection and nonmodal presentation.

Doctor does not change existing Workspace, Environment, Provider, or
persistence JSON. Doctor results are not persisted.

## Read-only and offline guarantees

The Doctor filesystem adapter exposes structured inspection, bounded text
reads, and one-directory enumeration only. The reachable production call graph
is tested to prohibit file and directory writes, deletes, moves, replacements,
attribute or timestamp changes, process starts, registry writes, shell
execution, and PowerShell execution.

The scan:

- does not start, stop, or execute DayZ;
- does not execute batch or PowerShell startup files;
- does not read logs, dumps, PBOs, BISIGNs, or BIKEYs for content;
- does not call Steam or Workshop APIs;
- does not make network calls of any kind;
- does not call RCON;
- does not auto-fix, copy, create, or rewrite target content.

## Path and traversal policy

Operator paths are validated before normalization. Relative startup and
configuration overrides resolve from the selected server root. Paths declared
by a startup command resolve as follows:

- batch `-config`: startup-script directory;
- PowerShell `-config`: static `Start-Process -WorkingDirectory`, then the
  resolved server-executable directory when no working directory is supplied;
- `-mod`, `-serverMod`, `-profiles`, and `-storage`: server root.

Explicitly selected or startup-referenced external paths are allowed and remain
visible in inventory. Mission traversal is restricted to the normalized
selected mission under `mpmissions`.

Recursive inventory uses explicit, one-directory-at-a-time traversal:

- maximum recursion depth: 16;
- maximum enumerated entries per scan: 100,000;
- maximum findings per scan: 5,000;
- maximum inventory entries per scan: 100,000.

Normalized directory paths are visited once. Symbolic links, junctions, mount
points, and all other reparse points are never followed. A skipped reparse
point, cycle, depth limit, or item limit produces a stable finding and retains
already collected results. Enumeration failures are isolated per directory so
readable siblings already returned by the adapter remain assessable.

## Bounded reads and safe XML

Every text read checks available metadata and reads through a read-only
`FileStream` and `StreamReader` with a fixed ceiling:

| Resource | Maximum |
| --- | ---: |
| Startup `.bat` / `.cmd` / `.ps1` | 1 MiB |
| Server configuration | 2 MiB |
| `mod.cpp` / `meta.cpp` | 1 MiB each |
| Mission XML / JSON | 8 MiB each |

Oversized files are not parsed. Evidence includes only the configured limit and
detected size.

Mission XML is loaded through `XmlReader` with `DtdProcessing.Prohibit`,
`XmlResolver = null`, a bounded document-character count, and no permitted
entity expansion. External DTDs, internal entity declarations, entity
expansion attempts, and malformed XML are rejected without resolving external
resources. JSON receives structural parsing only. Doctor does not perform a
DayZ schema or gameplay-balance validation.

## Structured filesystem outcomes

Filesystem operations return one of: Available, Missing, Unreadable,
InvalidPath, TooLarge, or Cancelled. Missing resources produce only their
applicable missing finding; unreadable and invalid resources do not also
produce a contradictory missing finding. Files that disappear during a scan
are treated as missing or unavailable without failing the complete scan. Raw
exception messages are logged only through the internal logging boundary and
never enter findings or inventory.

## Startup parsers

All startup files are parsed as source text and are never executed.

The batch parser supports direct quoted or unquoted
`DayZServer_x64.exe` commands, simple `set` assignments, `%NAME%`
substitution, caret continuation, recognized launch arguments, and ordered
semicolon mod lists. It ignores blank lines, labels, `rem`, `@rem`, `::`,
`echo`, `@echo`, variable assignments that merely mention the executable, and
other text mentions. The executable must be in direct command position.
`start`, `call`, `cmd`, nested PowerShell, `if`, `for`, `goto`, delayed
expansion, pipelines, command chaining, and other wrappers produce a partial
parse.

The purpose-built PowerShell parser supports only these static patterns:

- direct invocation with or without the `&` call operator;
- literal or previously resolved executable variables;
- typed `param(...)` declarations with static default values;
- `Start-Process` with a named or unambiguous positional file path, a
  statically resolved `-ArgumentList`, and an optional static
  `-WorkingDirectory`;
- single-quoted and double-quoted strings;
- interpolation of previously resolved scalar variables;
- simple two-operand `Join-Path`;
- `@(...)` and comma-separated static argument arrays;
- incremental static array construction with `+=`;
- an explicitly initialized `$args` variable as a normal local argument
  collection;
- statically resolved argument splatting and fully static `Start-Process`
  parameter hashtables;
- backtick line continuation;
- `-config`, `-profiles`, `-mission`, `-port`, `-mod`, `-serverMod`,
  `-storage`, and `-BEpath`.

Line comments, block comments, `Write-Host` mentions, documentation strings,
and assignments that merely contain the executable name never create a launch.
Duplicate mod declarations retain their original order for duplicate and
client/server-role conflict analysis.

Parameter defaults are recoverable but make the result partial because a
caller may override them at runtime. Relative configuration arguments resolve
from a static `Start-Process -WorkingDirectory` when supplied, otherwise from
the statically resolved server executable directory. Unsupported maintenance,
filesystem, process-check, logging, or cleanup statements do not discard
independent static launch values.

The PowerShell parser does not implement the PowerShell language. It does not
dot-source scripts, import modules to construct values, invoke expressions,
run commands, evaluate command substitutions, inspect the registry or
environment, invoke nested `powershell.exe`, `pwsh.exe`, or `cmd.exe`, follow
pipelines, evaluate functions or script blocks, or select branches controlled
by `if`, `switch`, loops, or other runtime state. Unsupported constructs create
a startup partial-parse finding while retaining values already recovered
safely. A single static launch enclosed by runtime control flow may be
recovered as partial, but conditional argument additions are not treated as
authoritative. Multiple materially different static launch commands are
ambiguous. When a script is too dynamic, select the script for partial
inventory and provide the active configuration through the explicit
configuration override.

DOP never executes PowerShell and does not reference
`System.Management.Automation`.

## Configuration and metadata parsers

The configuration parser is a small comment-aware and string-aware lexical
parser. Comments become token boundaries, comment markers inside strings
remain literal, and class declarations inside strings or comments are ignored.
The mission template is accepted only from the intended `Missions` structure.
Malformed, duplicate, or unsupported assignments produce a partial finding.

Supported operational scalars include `verifySignatures` and
`enableCfgGameplayFile`. `passwordAdmin` is assessed only as missing, empty, or
present. Its value is never placed in findings, evidence, inventory, logs, view
models, or test output. Missing or empty `passwordAdmin` produces a Warning
because password-based in-game administration may intentionally be disabled.

The shared metadata parser accepts exact, top-level, uncommented `publishedid`,
`name`, and `displayName` assignments. It ignores commented assignments,
unrelated properties, and assignment-like text inside strings. Duplicate or
malformed assignments produce a partial metadata finding and never fabricate a
Workshop ID.

## Inventory and assessment

Mission recognition covers:

- `init.c`;
- `description.ext`;
- `cfgeconomycore.xml`;
- `cfggameplay.json`;
- `db/types.xml`;
- `db/events.xml`;
- `db/globals.xml`;
- `db/economy.xml`;
- `db/messages.xml`.

Recognized files that exist are inventoried, and discovered mission XML/JSON is
structurally validated independently so one malformed document does not
suppress siblings. `cfggameplay.json` is cross-checked with
`enableCfgGameplayFile`.

`description.ext` is inventoried when present but is not universally required.
When `cfggameplay.json` is present and `enableCfgGameplayFile` is absent,
disabled, or not safely parsed, Doctor warns that the file may be inactive.
When the setting is enabled, a missing gameplay file is reported; no warning is
produced when the file is present and the setting is enabled.

Each referenced mod inventories its normalized path, display name, published
ID, client/server-only role, declared order, directory presence, addons and
keys presence, `mod.cpp` and `meta.cpp` presence, PBO count, BISIGN count,
BIKEY count, and BIKEY paths. Counts are metadata-only and do not claim
cryptographic validity. Duplicate references, cross-role conflicts, missing
signed content, undeployed BIKEYs, and top-level installed-but-unreferenced mod
directories are reported.

Log inventory recognizes `.rpt`, `.adm`, `.log`, and `.mdmp`
case-insensitively. Each item contains full path, filename, normalized type,
file size, last-modified UTC timestamp, and source category (`ServerRoot` or
`Profiles`). Overlapping locations are deduplicated by normalized path. Log and
dump contents are never read.

## Stable finding codes

- `DOP.Doctor.Request.Invalid`
- `DOP.Doctor.Game.Unsupported`
- `DOP.Doctor.Scan.Failed`
- `DOP.Doctor.DayZ.TargetRootMissing`
- `DOP.Doctor.DayZ.TargetRootUnreadable`
- `DOP.Doctor.DayZ.InvalidPath`
- `DOP.Doctor.DayZ.FileTooLarge`
- `DOP.Doctor.DayZ.ReparsePointSkipped`
- `DOP.Doctor.DayZ.EnumerationDepthLimit`
- `DOP.Doctor.DayZ.EnumerationItemLimit`
- `DOP.Doctor.DayZ.FindingLimit`
- `DOP.Doctor.DayZ.InventoryItemLimit`
- `DOP.Doctor.DayZ.TraversalCycleSkipped`
- `DOP.Doctor.DayZ.ExecutableMissing`
- `DOP.Doctor.DayZ.StartupFileMissing`
- `DOP.Doctor.DayZ.StartupNotDiscovered`
- `DOP.Doctor.DayZ.StartupAmbiguous`
- `DOP.Doctor.DayZ.StartupPartialParse`
- `DOP.Doctor.DayZ.ConfigurationUnresolved`
- `DOP.Doctor.DayZ.ConfigurationMissing`
- `DOP.Doctor.DayZ.ConfigurationPartialParse`
- `DOP.Doctor.DayZ.VerifySignaturesMissing`
- `DOP.Doctor.DayZ.VerifySignaturesUnsupported`
- `DOP.Doctor.DayZ.PasswordAdminMissing`
- `DOP.Doctor.DayZ.PasswordAdminEmpty`
- `DOP.Doctor.DayZ.GameplayConfigurationMissing`
- `DOP.Doctor.DayZ.GameplayConfigurationUnexpected`
- `DOP.Doctor.DayZ.MissionTemplateMissing`
- `DOP.Doctor.DayZ.MissionDirectoryMissing`
- `DOP.Doctor.DayZ.MissionFileMissing`
- `DOP.Doctor.DayZ.MalformedXml`
- `DOP.Doctor.DayZ.MalformedJson`
- `DOP.Doctor.DayZ.ModDirectoryMissing`
- `DOP.Doctor.DayZ.ModAddonsMissing`
- `DOP.Doctor.DayZ.ModSignedContentMissing`
- `DOP.Doctor.DayZ.ModDuplicateReference`
- `DOP.Doctor.DayZ.ModRoleConflict`
- `DOP.Doctor.DayZ.ModUnreferenced`
- `DOP.Doctor.DayZ.ModMetadataPartial`
- `DOP.Doctor.DayZ.ModKeyMissing`
- `DOP.Doctor.DayZ.ProfilesDirectoryMissing`
- `DOP.Doctor.DayZ.StorageDirectoryMissing`
- `DOP.Doctor.DayZ.InventoryUnreadable`

## Known limitations and future work

The MVP deliberately does not fully interpret Windows batch, PowerShell, or
the complete Bohemia configuration language. It does not inspect PBO contents,
validate signatures cryptographically, validate gameplay balance, determine
Workshop freshness, or inspect runtime ports and processes.

Remote Agent scanning, hosting-provider scanners, provider-specific assessment,
network diagnostics, scan persistence, rule plugins, and auto-fix workflows
remain future work and require separate designs.
