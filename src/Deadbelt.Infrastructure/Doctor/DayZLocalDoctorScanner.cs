using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Deadbelt.Application.Doctor;
using Deadbelt.Domain.Doctor;
using Deadbelt.Domain.Environments;
using Microsoft.Extensions.Logging;

namespace Deadbelt.Infrastructure.Doctor;

public sealed class DayZLocalDoctorScanner : IDoctorScanner
{
    private const string ServerExecutableName = "DayZServer_x64.exe";

    private static readonly string[] MissionRelativePaths =
    [
        "init.c",
        "description.ext",
        "cfgeconomycore.xml",
        "cfggameplay.json",
        Path.Combine("db", "types.xml"),
        Path.Combine("db", "events.xml"),
        Path.Combine("db", "globals.xml"),
        Path.Combine("db", "economy.xml"),
        Path.Combine("db", "messages.xml")
    ];

    private readonly IDoctorFileSystem _fileSystem;
    private readonly ILogger<DayZLocalDoctorScanner> _logger;
    private readonly DoctorScanLimits _limits;

    public DayZLocalDoctorScanner(ILogger<DayZLocalDoctorScanner> logger)
        : this(
            new OperatingSystemDoctorFileSystem(),
            logger,
            DoctorScanLimits.Default)
    {
    }

    internal DayZLocalDoctorScanner(
        IDoctorFileSystem fileSystem,
        ILogger<DayZLocalDoctorScanner> logger,
        DoctorScanLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);

        _fileSystem = fileSystem;
        _logger = logger;
        _limits = limits ?? DoctorScanLimits.Default;
    }

    public bool Supports(GameType gameType) => gameType == GameType.DayZ;

    public Task<DoctorScanResult> ScanAsync(
        DoctorScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(
            () => Scan(request, cancellationToken),
            cancellationToken);
    }

    private DoctorScanResult Scan(
        DoctorScanRequest request,
        CancellationToken cancellationToken)
    {
        var startedUtc = DateTime.UtcNow;
        var findings = new FindingCollector(_limits.MaximumFindings);
        var budget = new ScanBudget(_limits);

        if (!TryNormalizePath(request.TargetRootPath, null, out var rootPath))
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.InvalidPath,
                    DoctorSeverity.Error,
                    "The DayZ server root path is invalid.",
                    "Doctor rejected the path before attempting filesystem inspection.",
                    "The selected root is not a valid local filesystem path.",
                    "Select a valid local DayZ server directory.",
                    request.TargetRootPath));

            return CompleteEmpty(
                SafeDisplayPath(request.TargetRootPath),
                findings,
                startedUtc);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var rootInspection = InspectDirectory(rootPath, cancellationToken);

        if (rootInspection.Status != DoctorFileSystemStatus.Available)
        {
            var missing = rootInspection.Status == DoctorFileSystemStatus.Missing;
            var invalid = rootInspection.Status == DoctorFileSystemStatus.InvalidPath;
            findings.Add(
                Finding(
                    invalid
                        ? DoctorFindingCodes.InvalidPath
                        : missing
                            ? DoctorFindingCodes.TargetRootMissing
                            : DoctorFindingCodes.TargetRootUnreadable,
                    DoctorSeverity.Error,
                    invalid
                        ? "The DayZ server root path is invalid."
                        : missing
                            ? "The DayZ server root does not exist."
                            : "The DayZ server root cannot be inspected.",
                    "Doctor stopped before traversing an unavailable target root.",
                    invalid
                        ? "The selected root is not a valid local filesystem path."
                        : missing
                            ? $"No directory was found at '{rootPath}'."
                            : $"The selected root is unavailable for read-only inspection: '{rootPath}'.",
                    "Correct the path or grant read access, then run the scan again.",
                    rootPath));

            return CompleteEmpty(rootPath, findings, startedUtc);
        }

        if (rootInspection.IsReparsePoint)
        {
            AddReparseFinding(rootPath, findings);
            return CompleteEmpty(rootPath, findings, startedUtc);
        }

        var rootEntries = EnumerateOneDirectory(
            rootPath,
            budget,
            findings,
            cancellationToken);
        var executablePath = Path.Combine(rootPath, ServerExecutableName);
        var executableInspection = InspectFile(executablePath, cancellationToken);
        var executableExists = executableInspection.Status == DoctorFileSystemStatus.Available
            && !executableInspection.IsReparsePoint;

        if (!executableExists)
        {
            if (executableInspection.IsReparsePoint)
                AddReparseFinding(executablePath, findings);
            else
                AddResourceFinding(
                    executableInspection.Status,
                    executablePath,
                    DoctorFindingCodes.ExecutableMissing,
                    DoctorFindingCodes.InventoryUnreadable,
                    "The DayZ server executable is missing.",
                    "The DayZ server executable cannot be inspected.",
                    findings);
        }

        var startupCandidates = SelectTopLevelFiles(
            rootEntries,
            entry => IsExtension(entry.Name, ".bat")
                || IsExtension(entry.Name, ".cmd")
                || IsExtension(entry.Name, ".ps1"),
            budget,
            findings);
        var configurationCandidates = SelectTopLevelFiles(
            rootEntries,
            entry => IsExtension(entry.Name, ".cfg"),
            budget,
            findings);
        var startupSelection = ResolveStartup(
            request,
            rootPath,
            startupCandidates,
            findings,
            cancellationToken);
        var activeConfigurationPath = ResolveConfigurationPath(
            request.ConfigurationFilePath,
            rootPath,
            startupSelection.Command,
            startupSelection.StartupPath,
            findings);
        var configurationValues = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        string? missionTemplate = null;
        var passwordState = PasswordAdminState.Missing;
        var configurationParsed = false;

        if (activeConfigurationPath is not null)
        {
            var read = ReadText(
                activeConfigurationPath,
                _limits.MaximumConfigurationBytes,
                cancellationToken);

            if (read.Status == DoctorFileSystemStatus.Available)
            {
                var parsed = DayZConfigurationParser.Parse(read.Content!);
                configurationParsed = true;

                foreach (var pair in parsed.Values)
                    configurationValues[pair.Key] = pair.Value;

                missionTemplate = parsed.MissionTemplate;
                passwordState = parsed.PasswordAdminState;

                if (parsed.IsPartial)
                {
                    findings.Add(
                        Finding(
                            DoctorFindingCodes.ConfigurationPartialParse,
                            DoctorSeverity.Warning,
                            "The DayZ configuration was only partially parsed.",
                            "Doctor retained only assignments recognized by its comment-aware lexical parser.",
                            string.Join(" ", parsed.Limitations),
                            "Review malformed or unsupported configuration syntax.",
                            activeConfigurationPath));
                }
            }
            else
            {
                AddReadFailure(
                    read,
                    activeConfigurationPath,
                    _limits.MaximumConfigurationBytes,
                    DoctorFindingCodes.ConfigurationMissing,
                    DoctorFindingCodes.ConfigurationPartialParse,
                    "The active DayZ configuration is missing.",
                    "The active DayZ configuration cannot be read.",
                    findings);
            }
        }

        AssessConfiguration(
            activeConfigurationPath,
            configurationValues,
            passwordState,
            findings);

        var missionAssessment = InspectMission(
            rootPath,
            configurationParsed,
            missionTemplate,
            configurationValues,
            budget,
            findings,
            cancellationToken);
        var globalKeys = InspectGlobalKeys(
            rootPath,
            budget,
            findings,
            cancellationToken);
        var clientPaths = startupSelection.Command?.ClientModPaths ?? [];
        var serverPaths = startupSelection.Command?.ServerModPaths ?? [];

        AnalyzeModReferences(clientPaths, serverPaths, findings);

        var clientMods = InspectMods(
            clientPaths,
            isServerMod: false,
            globalKeys,
            budget,
            findings,
            cancellationToken);
        var serverMods = InspectMods(
            serverPaths,
            isServerMod: true,
            globalKeys,
            budget,
            findings,
            cancellationToken);

        InspectUnreferencedMods(
            rootEntries,
            clientPaths.Concat(serverPaths),
            findings);

        var profilePaths = InspectOptionalDirectory(
            startupSelection.Command?.ProfilesPath,
            DoctorFindingCodes.ProfilesDirectoryMissing,
            "The configured profiles directory is missing.",
            findings,
            cancellationToken);
        var storagePaths = InspectOptionalDirectory(
            startupSelection.Command?.StoragePath,
            DoctorFindingCodes.StorageDirectoryMissing,
            "The configured storage directory is missing.",
            findings,
            cancellationToken);
        var logs = InspectLogs(
            rootPath,
            profilePaths,
            budget,
            findings,
            cancellationToken);

        var inventory = new DoctorInventory(
            rootPath,
            executableExists ? executablePath : null,
            startupCandidates,
            startupSelection.StartupPath,
            configurationCandidates,
            activeConfigurationPath,
            configurationValues,
            missionTemplate,
            missionAssessment.Path,
            missionAssessment.Files,
            clientMods,
            serverMods,
            globalKeys,
            profilePaths,
            storagePaths,
            logs,
            CreateLaunchArguments(startupSelection.Command));

        return DoctorScanResult.Completed(
            inventory,
            findings.Items,
            startedUtc,
            DateTime.UtcNow);
    }

    private StartupSelection ResolveStartup(
        DoctorScanRequest request,
        string rootPath,
        IReadOnlyList<string> startupCandidates,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.StartupFilePath))
        {
            if (!TryNormalizePath(request.StartupFilePath, rootPath, out var explicitPath))
            {
                findings.Add(
                    Finding(
                        DoctorFindingCodes.InvalidPath,
                        DoctorSeverity.Error,
                        "The selected startup path is invalid.",
                        "Doctor rejected the path before attempting filesystem inspection.",
                        "The startup override is not a valid local filesystem path.",
                        "Select a valid `.bat`, `.cmd`, or `.ps1` startup script.",
                        request.StartupFilePath));
                return new StartupSelection(null, null);
            }

            var inspection = InspectFile(explicitPath, cancellationToken);

            if (inspection.Status != DoctorFileSystemStatus.Available
                || inspection.IsReparsePoint)
            {
                if (inspection.IsReparsePoint)
                    AddReparseFinding(explicitPath, findings);
                else
                    AddResourceFinding(
                        inspection.Status,
                        explicitPath,
                        DoctorFindingCodes.StartupFileMissing,
                        DoctorFindingCodes.StartupPartialParse,
                        "The selected startup file is missing.",
                        "The selected startup file cannot be inspected.",
                        findings);

                return new StartupSelection(explicitPath, null);
            }

            var parsed = ParseStartup(explicitPath, rootPath, findings, cancellationToken);

            if (parsed.Commands.Count > 1)
            {
                AddStartupAmbiguityFinding(
                    parsed.Commands.Count,
                    explicitPath,
                    findings);
            }

            return new StartupSelection(
                explicitPath,
                parsed.Commands.Count == 1 ? parsed.Commands[0] : null);
        }

        var discovered = new List<(string Path, DayZLaunchCommand Command)>();
        var ambiguousLaunchCount = 0;

        foreach (var candidate in startupCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = ParseStartup(candidate, rootPath, findings, cancellationToken);

            if (parsed.Commands.Count == 1)
                discovered.Add((candidate, parsed.Commands[0]));
            else if (parsed.Commands.Count > 1)
                ambiguousLaunchCount += parsed.Commands.Count;
        }

        if (discovered.Count == 1 && ambiguousLaunchCount == 0)
            return new StartupSelection(discovered[0].Path, discovered[0].Command);

        var plausibleLaunchCount = discovered.Count + ambiguousLaunchCount;

        if (plausibleLaunchCount > 1)
        {
            AddStartupAmbiguityFinding(
                plausibleLaunchCount,
                rootPath,
                findings);
            return new StartupSelection(null, null);
        }

        findings.Add(
            Finding(
                DoctorFindingCodes.StartupNotDiscovered,
                DoctorSeverity.Information,
                "No unambiguous DayZ startup command was discovered.",
                "Doctor does not execute scripts or choose between ambiguous launch commands.",
                $"{startupCandidates.Count} startup candidate(s) were inspected.",
                "Select the startup file actually used by this server.",
                rootPath));

        return new StartupSelection(null, null);
    }

    private static void AddStartupAmbiguityFinding(
        int commandCount,
        string sourcePath,
        FindingCollector findings)
    {
        findings.Add(
            Finding(
                DoctorFindingCodes.StartupAmbiguous,
                DoctorSeverity.Warning,
                "More than one DayZ startup command was discovered.",
                "Doctor did not execute the script or choose between ambiguous launch commands.",
                $"{commandCount} statically parseable DayZ launch commands were found.",
                "Select an unambiguous startup file or provide an explicit configuration override.",
                sourcePath));
    }

    private StartupParseOutcome ParseStartup(
        string startupPath,
        string rootPath,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        var isBatch = IsExtension(startupPath, ".bat")
            || IsExtension(startupPath, ".cmd");
        var isPowerShell = IsExtension(startupPath, ".ps1");

        if (!isBatch && !isPowerShell)
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.StartupPartialParse,
                    DoctorSeverity.Warning,
                    "The selected startup file type is unsupported.",
                    "Doctor statically reads only `.bat`, `.cmd`, and `.ps1` startup syntax.",
                    $"The selected file extension is '{Path.GetExtension(startupPath)}'.",
                    "Select the `.bat`, `.cmd`, or `.ps1` file used to start this server.",
                    startupPath));
            return new StartupParseOutcome([], []);
        }

        var read = ReadText(
            startupPath,
            _limits.MaximumStartupBytes,
            cancellationToken);

        if (read.Status != DoctorFileSystemStatus.Available)
        {
            AddReadFailure(
                read,
                startupPath,
                _limits.MaximumStartupBytes,
                DoctorFindingCodes.StartupFileMissing,
                DoctorFindingCodes.StartupPartialParse,
                "The startup file is missing.",
                "The startup file cannot be read.",
                findings);
            return new StartupParseOutcome([], []);
        }

        StartupParseOutcome parsed;

        if (isPowerShell)
        {
            var result = DayZPowerShellStartupParser.Parse(
                read.Content!,
                startupPath,
                rootPath);
            parsed = new StartupParseOutcome(
                result.Commands,
                result.Limitations);
        }
        else
        {
            var result = DayZBatchStartupParser.Parse(
                read.Content!,
                startupPath,
                rootPath);
            parsed = new StartupParseOutcome(
                result.Commands,
                result.Limitations);
        }

        if (parsed.IsPartial)
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.StartupPartialParse,
                    DoctorSeverity.Warning,
                    "A DayZ startup file was only partially parsed.",
                    "Doctor statically recovered only supported launch values and never executed the script.",
                    CreateStartupPartialEvidence(parsed),
                    "Simplify the selected startup definition or provide an explicit configuration override.",
                    startupPath));
        }

        return parsed;
    }

    private static string CreateStartupPartialEvidence(
        StartupParseOutcome parsed)
    {
        var command = parsed.Commands.Count == 1
            ? parsed.Commands[0]
            : null;
        var recovered = new List<string>();
        var unresolvedCritical = new List<string>();

        if (command is not null)
        {
            recovered.Add("executable");

            if (command.ConfigurationPath is not null)
                recovered.Add("configuration");

            if (command.ProfilesPath is not null)
                recovered.Add("profiles");

            if (command.StoragePath is not null)
                recovered.Add("storage");

            if (command.Port is not null)
                recovered.Add("port");

            if (command.ClientModPaths.Count > 0)
                recovered.Add("client mods");

            if (command.ServerModPaths.Count > 0)
                recovered.Add("server-only mods");

            if (command.BattleEyePath is not null)
                recovered.Add("BE path");

            if (command.ConfigurationPath is null)
                unresolvedCritical.Add("configuration");
        }
        else
        {
            unresolvedCritical.Add("launch executable or arguments");
        }

        var categories = parsed.Limitations
            .Select(CategorizeStartupLimitation)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var displayedCategories = categories.Take(5).ToArray();
        var remainingCategoryCount = categories.Length - displayedCategories.Length;
        var recoveredText = recovered.Count == 0
            ? "none"
            : string.Join(", ", recovered);
        var unresolvedText = unresolvedCritical.Count == 0
            ? "none"
            : string.Join(", ", unresolvedCritical);
        var categoryText = displayedCategories.Length == 0
            ? "none"
            : string.Join(", ", displayedCategories);
        var remainingText = remainingCategoryCount == 0
            ? string.Empty
            : $" (+{remainingCategoryCount} more)";
        var configurationText = command?.ConfigurationPath is null
            ? "unresolved"
            : "resolved";

        return $"Recovered: {recoveredText}. "
            + $"Unresolved launch-critical values: {unresolvedText}. "
            + $"Configuration: {configurationText}. "
            + $"Static-analysis limits: {categoryText}{remainingText}. "
            + "Script execution: none.";
    }

    private static string CategorizeStartupLimitation(string limitation)
    {
        if (limitation.Contains("parameter", StringComparison.OrdinalIgnoreCase))
            return "runtime-overridable parameters";

        if (limitation.Contains("control flow", StringComparison.OrdinalIgnoreCase)
            || limitation.Contains("conditional", StringComparison.OrdinalIgnoreCase))
        {
            return "runtime control flow";
        }

        if (limitation.Contains("unresolved", StringComparison.OrdinalIgnoreCase)
            || limitation.Contains("could not be resolved", StringComparison.OrdinalIgnoreCase)
            || limitation.Contains("could not be extended", StringComparison.OrdinalIgnoreCase))
        {
            return "unresolved static values";
        }

        if (limitation.Contains("sensitive", StringComparison.OrdinalIgnoreCase))
            return "excluded sensitive values";

        if (limitation.Contains("path", StringComparison.OrdinalIgnoreCase))
            return "invalid or dynamic paths";

        if (limitation.Contains("multiple", StringComparison.OrdinalIgnoreCase))
            return "multiple launch definitions";

        if (limitation.Contains("unterminated", StringComparison.OrdinalIgnoreCase)
            || limitation.Contains("malformed", StringComparison.OrdinalIgnoreCase)
            || limitation.Contains("balanced", StringComparison.OrdinalIgnoreCase))
        {
            return "malformed static syntax";
        }

        return "unsupported runtime syntax";
    }

    private static string? ResolveConfigurationPath(
        string? configurationOverride,
        string rootPath,
        DayZLaunchCommand? command,
        string? startupPath,
        FindingCollector findings)
    {
        if (!string.IsNullOrWhiteSpace(configurationOverride))
        {
            if (TryNormalizePath(configurationOverride, rootPath, out var explicitPath))
                return explicitPath;

            findings.Add(
                Finding(
                    DoctorFindingCodes.InvalidPath,
                    DoctorSeverity.Error,
                    "The configuration override path is invalid.",
                    "Doctor rejected the path before attempting filesystem inspection.",
                    "The configuration override is not a valid local filesystem path.",
                    "Select a valid DayZ configuration file.",
                    configurationOverride));
            return null;
        }

        if (!string.IsNullOrWhiteSpace(command?.ConfigurationPath))
            return command.ConfigurationPath;

        findings.Add(
            Finding(
                DoctorFindingCodes.ConfigurationUnresolved,
                DoctorSeverity.Warning,
                "The active DayZ configuration is unresolved.",
                "Doctor does not assume that a common filename is authoritative.",
                "No explicit override or direct startup command supplied `-config`.",
                "Provide the active configuration as an override.",
                startupPath ?? rootPath));
        return null;
    }

    private static void AssessConfiguration(
        string? configurationPath,
        IReadOnlyDictionary<string, string> values,
        PasswordAdminState passwordState,
        FindingCollector findings)
    {
        if (configurationPath is null)
            return;

        if (!values.TryGetValue("verifySignatures", out var signatures))
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.VerifySignaturesMissing,
                    DoctorSeverity.Warning,
                    "`verifySignatures` is not set.",
                    "The active configuration does not expose the supported signature setting.",
                    "No safely parsed `verifySignatures` assignment was found.",
                    "Set `verifySignatures = 2;` before the next restart.",
                    configurationPath));
        }
        else if (!string.Equals(signatures, "2", StringComparison.Ordinal))
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.VerifySignaturesUnsupported,
                    DoctorSeverity.Error,
                    "`verifySignatures` uses an unsupported value.",
                    "Client-mod signature verification is not set to the supported DayZ value.",
                    $"The safely parsed value is '{signatures}'.",
                    "Set `verifySignatures = 2;` before the next restart.",
                    configurationPath));
        }

        if (passwordState == PasswordAdminState.Missing)
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.PasswordAdminMissing,
                    DoctorSeverity.Warning,
                    "Administrative password is not configured.",
                    "The server configuration does not provide a password for password-based in-game administrator access.",
                    "No `passwordAdmin` assignment was found; no password value was retained.",
                    "Configure `passwordAdmin` only if password-based in-game administrator access is required, or acknowledge this finding when administration is handled another way.",
                    configurationPath));
        }
        else if (passwordState == PasswordAdminState.Empty)
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.PasswordAdminEmpty,
                    DoctorSeverity.Warning,
                    "Administrative password is not configured.",
                    "The server configuration does not provide a password for password-based in-game administrator access.",
                    "An empty assignment was detected; no password value was retained.",
                    "Configure `passwordAdmin` only if password-based in-game administrator access is required, or acknowledge this finding when administration is handled another way.",
                    configurationPath));
        }
    }

    private MissionAssessment InspectMission(
        string rootPath,
        bool configurationParsed,
        string? missionTemplate,
        IReadOnlyDictionary<string, string> configurationValues,
        ScanBudget budget,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        if (!configurationParsed)
            return new MissionAssessment(null, []);

        if (string.IsNullOrWhiteSpace(missionTemplate))
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.MissionTemplateMissing,
                    DoctorSeverity.Warning,
                    "The active mission template is unresolved.",
                    "Mission files cannot be assessed without the intended Missions structure.",
                    "No safely parsed `template` assignment was found under `class Missions`.",
                    "Set the active mission template, then run the scan again."));
            return new MissionAssessment(null, []);
        }

        var missionsRoot = Path.GetFullPath(Path.Combine(rootPath, "mpmissions"));

        if (!TryNormalizePath(missionTemplate, missionsRoot, out var missionPath)
            || !IsWithin(missionPath, missionsRoot))
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.InvalidPath,
                    DoctorSeverity.Error,
                    "The mission template resolves to an invalid location.",
                    "Doctor refused to traverse outside the selected mission tree.",
                    "The template does not resolve to a directory under `mpmissions`.",
                    "Correct the mission template path.",
                    missionTemplate));
            return new MissionAssessment(null, []);
        }

        var inspection = InspectDirectory(missionPath, cancellationToken);

        if (inspection.Status != DoctorFileSystemStatus.Available
            || inspection.IsReparsePoint)
        {
            if (inspection.IsReparsePoint)
                AddReparseFinding(missionPath, findings);
            else
                AddResourceFinding(
                    inspection.Status,
                    missionPath,
                    DoctorFindingCodes.MissionDirectoryMissing,
                    DoctorFindingCodes.InventoryUnreadable,
                    "The active mission directory is missing.",
                    "The active mission directory cannot be inspected.",
                    findings);
            return new MissionAssessment(missionPath, []);
        }

        var files = new List<string>();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DoctorPathInspection? gameplayInspection = null;

        foreach (var relativePath in MissionRelativePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(missionPath, relativePath);
            var fileInspection = InspectFile(path, cancellationToken);

            if (string.Equals(
                    relativePath,
                    "cfggameplay.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                gameplayInspection = fileInspection;
            }

            if (fileInspection.Status == DoctorFileSystemStatus.Available
                && !fileInspection.IsReparsePoint)
            {
                existing.Add(path);

                if (budget.TryAddInventory())
                    files.Add(path);
                else
                    AddInventoryLimitFinding(findings);
            }
            else if (!string.Equals(
                         relativePath,
                         "cfggameplay.json",
                         StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(
                         relativePath,
                         "description.ext",
                         StringComparison.OrdinalIgnoreCase))
            {
                if (fileInspection.IsReparsePoint)
                    AddReparseFinding(path, findings);
                else
                    AddResourceFinding(
                        fileInspection.Status,
                        path,
                        DoctorFindingCodes.MissionFileMissing,
                        DoctorFindingCodes.InventoryUnreadable,
                        "A recognized mission file is missing.",
                        "A recognized mission file cannot be inspected.",
                        findings);
            }
        }

        var gameplayPath = Path.Combine(missionPath, "cfggameplay.json");
        var gameplayPresent = existing.Contains(gameplayPath);
        var gameplayEnabled = configurationValues.TryGetValue(
                "enableCfgGameplayFile",
                out var gameplayValue)
            && IsEnabled(gameplayValue);

        if (!gameplayPresent && gameplayInspection?.IsReparsePoint == true)
        {
            AddReparseFinding(gameplayPath, findings);
        }
        else if (!gameplayPresent
                 && gameplayInspection?.Status is DoctorFileSystemStatus.Unreadable
                     or DoctorFileSystemStatus.InvalidPath)
        {
            AddResourceFinding(
                gameplayInspection.Status,
                gameplayPath,
                DoctorFindingCodes.GameplayConfigurationMissing,
                DoctorFindingCodes.InventoryUnreadable,
                "`cfggameplay.json` is missing.",
                "`cfggameplay.json` cannot be inspected.",
                findings);
        }
        else if (gameplayEnabled && !gameplayPresent)
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.GameplayConfigurationMissing,
                    DoctorSeverity.Error,
                    "`cfggameplay.json` is enabled but missing.",
                    "The active configuration enables the mission gameplay file.",
                    "No readable `cfggameplay.json` was found in the selected mission.",
                    "Restore the gameplay file or disable the setting.",
                    gameplayPath));
        }
        else if (!gameplayEnabled && gameplayPresent)
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.GameplayConfigurationUnexpected,
                    DoctorSeverity.Warning,
                    "`cfggameplay.json` is present but not enabled.",
                    "The file is inventoried but the active configuration does not enable it.",
                    "The setting is absent, disabled, or not safely parsed.",
                    "Enable `enableCfgGameplayFile` if this file is intended to apply.",
                    gameplayPath));
        }

        var documents = TraverseFiles(
            missionPath,
            entry => IsExtension(entry.Name, ".xml") || IsExtension(entry.Name, ".json"),
            budget,
            findings,
            cancellationToken);

        foreach (var entry in documents)
        {
            if (existing.Add(entry.FullPath))
            {
                if (budget.TryAddInventory())
                    files.Add(entry.FullPath);
                else
                    AddInventoryLimitFinding(findings);
            }

            ValidateMissionDocument(entry.FullPath, findings, cancellationToken);
        }

        return new MissionAssessment(
            missionPath,
            files
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private void ValidateMissionDocument(
        string path,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        var read = ReadText(
            path,
            _limits.MaximumMissionDocumentBytes,
            cancellationToken);

        if (read.Status != DoctorFileSystemStatus.Available)
        {
            AddReadFailure(
                read,
                path,
                _limits.MaximumMissionDocumentBytes,
                DoctorFindingCodes.MissionFileMissing,
                DoctorFindingCodes.InventoryUnreadable,
                "A mission document disappeared during inspection.",
                "A mission document cannot be read.",
                findings);
            return;
        }

        try
        {
            if (IsExtension(path, ".xml"))
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = _limits.MaximumMissionDocumentBytes,
                    MaxCharactersFromEntities = 0
                };
                using var textReader = new StringReader(read.Content!);
                using var xmlReader = XmlReader.Create(textReader, settings);
                _ = XDocument.Load(xmlReader, LoadOptions.None);
            }
            else
            {
                using var document = JsonDocument.Parse(read.Content!);
            }
        }
        catch (XmlException)
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.MalformedXml,
                    DoctorSeverity.Error,
                    "A mission XML file is unsafe or malformed.",
                    "DTD declarations, entity declarations, and malformed XML are rejected.",
                    "The document could not be loaded by the constrained XML reader.",
                    "Remove DTD/entity declarations or correct the XML syntax.",
                    path));
        }
        catch (JsonException)
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.MalformedJson,
                    DoctorSeverity.Error,
                    "A mission JSON file is malformed.",
                    "Doctor performed structural parsing only.",
                    "The document is not well-formed JSON.",
                    "Correct the JSON syntax.",
                    path));
        }
    }

    private IReadOnlyList<string> InspectGlobalKeys(
        string rootPath,
        ScanBudget budget,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        var keysPath = Path.Combine(rootPath, "keys");
        var inspection = InspectDirectory(keysPath, cancellationToken);

        if (inspection.Status != DoctorFileSystemStatus.Available
            || inspection.IsReparsePoint)
        {
            if (inspection.IsReparsePoint)
                AddReparseFinding(keysPath, findings);
            return [];
        }

        return SelectTopLevelFiles(
            EnumerateOneDirectory(keysPath, budget, findings, cancellationToken),
            entry => IsExtension(entry.Name, ".bikey"),
            budget,
            findings);
    }

    private void AnalyzeModReferences(
        IReadOnlyList<string> clientPaths,
        IReadOnlyList<string> serverPaths,
        FindingCollector findings)
    {
        AddDuplicateModFindings(clientPaths, "client", findings);
        AddDuplicateModFindings(serverPaths, "server", findings);

        var clientSet = clientPaths
            .Select(NormalizeComparisonPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var serverPath in serverPaths)
        {
            if (!clientSet.Contains(NormalizeComparisonPath(serverPath)))
                continue;

            findings.Add(
                Finding(
                    DoctorFindingCodes.ModRoleConflict,
                    DoctorSeverity.Error,
                    "A mod is declared in both client and server-only roles.",
                    "The same normalized path appears in `-mod` and `-serverMod`.",
                    $"Conflicting normalized path: '{NormalizeComparisonPath(serverPath)}'.",
                    "Declare the mod in only its intended launch role.",
                    serverPath));
        }
    }

    private static void AddDuplicateModFindings(
        IReadOnlyList<string> paths,
        string role,
        FindingCollector findings)
    {
        foreach (var group in paths
                     .Select((path, index) => new
                     {
                         Path = path,
                         Position = index + 1,
                         Normalized = NormalizeComparisonPath(path)
                     })
                     .GroupBy(item => item.Normalized, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.ModDuplicateReference,
                    DoctorSeverity.Warning,
                    $"A {role} mod is referenced more than once.",
                    "Declared order was preserved for duplicate analysis.",
                    $"Positions {string.Join(", ", group.Select(item => item.Position))} reference '{group.Key}'.",
                    "Remove duplicate launch references while preserving the intended order.",
                    group.First().Path));
        }
    }

    private IReadOnlyList<DoctorModInventory> InspectMods(
        IReadOnlyList<string> modPaths,
        bool isServerMod,
        IReadOnlyList<string> globalKeys,
        ScanBudget budget,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        var result = new List<DoctorModInventory>();
        var globalKeyNames = globalKeys
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < modPaths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var modPath = modPaths[index];

            if (!seen.Add(NormalizeComparisonPath(modPath)))
                continue;

            var fallbackName = Path.GetFileName(
                modPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
            var directory = InspectDirectory(modPath, cancellationToken);

            if (directory.Status != DoctorFileSystemStatus.Available
                || directory.IsReparsePoint)
            {
                if (directory.IsReparsePoint)
                    AddReparseFinding(modPath, findings);
                else
                    AddResourceFinding(
                        directory.Status,
                        modPath,
                        DoctorFindingCodes.ModDirectoryMissing,
                        DoctorFindingCodes.InventoryUnreadable,
                        "A referenced mod directory is missing.",
                        "A referenced mod directory cannot be inspected.",
                        findings);

                AddModInventory(
                    result,
                    new DoctorModInventory(
                        fallbackName,
                        modPath,
                        isServerMod,
                        directoryExists: false,
                        publishedId: null,
                        declaredOrder: index + 1),
                    budget,
                    findings);
                continue;
            }

            var addonsPath = Path.Combine(modPath, "addons");
            var keysPath = Path.Combine(modPath, "keys");
            var addons = InspectDirectory(addonsPath, cancellationToken);
            var keys = InspectDirectory(keysPath, cancellationToken);
            var addonsExists = addons.Status == DoctorFileSystemStatus.Available
                && !addons.IsReparsePoint;
            var keysExists = keys.Status == DoctorFileSystemStatus.Available
                && !keys.IsReparsePoint;

            if (addons.IsReparsePoint)
                AddReparseFinding(addonsPath, findings);

            if (keys.IsReparsePoint)
                AddReparseFinding(keysPath, findings);

            if (!addonsExists)
            {
                findings.Add(
                    Finding(
                        DoctorFindingCodes.ModAddonsMissing,
                        DoctorSeverity.Warning,
                        "A referenced mod has no readable addons directory.",
                        "Doctor could not inventory deployable PBO or signature files.",
                        $"No readable normal directory was found at '{addonsPath}'.",
                        "Restore the mod's addons directory.",
                        addonsPath));
            }

            var contentEntries = addonsExists
                ? TraverseFiles(
                    addonsPath,
                    entry => IsExtension(entry.Name, ".pbo")
                        || IsExtension(entry.Name, ".bisign"),
                    budget,
                    findings,
                    cancellationToken)
                : [];
            var keyEntries = keysExists
                ? TraverseFiles(
                    keysPath,
                    entry => IsExtension(entry.Name, ".bikey"),
                    budget,
                    findings,
                    cancellationToken)
                : [];
            var pboCount = contentEntries.Count(entry => IsExtension(entry.Name, ".pbo"));
            var bisignCount = contentEntries.Count(entry => IsExtension(entry.Name, ".bisign"));
            var keyPaths = keyEntries
                .Select(entry => entry.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var modCppPath = Path.Combine(modPath, "mod.cpp");
            var metaCppPath = Path.Combine(modPath, "meta.cpp");
            var modCpp = InspectFile(modCppPath, cancellationToken);
            var metaCpp = InspectFile(metaCppPath, cancellationToken);
            string? publishedId = null;
            var displayName = fallbackName;

            foreach (var metadata in new[]
                     {
                         (Path: metaCppPath, Inspection: metaCpp),
                         (Path: modCppPath, Inspection: modCpp)
                     })
            {
                if (metadata.Inspection.Status != DoctorFileSystemStatus.Available
                    || metadata.Inspection.IsReparsePoint)
                {
                    if (metadata.Inspection.IsReparsePoint)
                        AddReparseFinding(metadata.Path, findings);
                    else if (metadata.Inspection.Status is DoctorFileSystemStatus.Unreadable
                             or DoctorFileSystemStatus.InvalidPath)
                        AddMetadataFailure(metadata.Path, metadata.Inspection.Status, findings);
                    continue;
                }

                var read = ReadText(
                    metadata.Path,
                    _limits.MaximumMetadataBytes,
                    cancellationToken);

                if (read.Status != DoctorFileSystemStatus.Available)
                {
                    AddReadFailure(
                        read,
                        metadata.Path,
                        _limits.MaximumMetadataBytes,
                        DoctorFindingCodes.ModMetadataPartial,
                        DoctorFindingCodes.ModMetadataPartial,
                        "Mod metadata disappeared during inspection.",
                        "Mod metadata cannot be read.",
                        findings);
                    continue;
                }

                var parsed = DayZModMetadataParser.Parse(read.Content!);
                publishedId ??= parsed.PublishedId;
                displayName = parsed.DisplayName ?? displayName;

                if (parsed.Limitations.Count > 0)
                {
                    findings.Add(
                        Finding(
                            DoctorFindingCodes.ModMetadataPartial,
                            DoctorSeverity.Warning,
                            "Mod metadata was only partially parsed.",
                            "Only exact, top-level, comment-aware assignments were accepted.",
                            string.Join(" ", parsed.Limitations),
                            "Correct malformed or duplicate metadata assignments.",
                            metadata.Path));
                }
            }

            if (!isServerMod && (pboCount == 0 || bisignCount == 0))
            {
                findings.Add(
                    Finding(
                        DoctorFindingCodes.ModSignedContentMissing,
                        DoctorSeverity.Warning,
                        "A client mod has no detectable signed content.",
                        "Doctor found no complete PBO-and-BISIGN inventory pair.",
                        $"PBO count: {pboCount}; BISIGN count: {bisignCount}.",
                        "Restore signed mod content before requiring the mod.",
                        modPath));
            }

            if (!isServerMod && keyPaths.Length == 0)
            {
                findings.Add(
                    Finding(
                        DoctorFindingCodes.ModKeyMissing,
                        DoctorSeverity.Warning,
                        "A client mod exposes no public BIKEY.",
                        "Doctor cannot compare a missing mod key with the global key deployment.",
                        "No `.bikey` file was inventoried under the mod keys directory.",
                        "Restore the mod public key and deploy it to the server keys directory.",
                        modPath));
            }

            foreach (var keyPath in keyPaths)
            {
                var keyName = Path.GetFileName(keyPath);

                if (globalKeyNames.Contains(keyName))
                    continue;

                findings.Add(
                    Finding(
                        DoctorFindingCodes.ModKeyMissing,
                        DoctorSeverity.Error,
                        "A mod public key is not deployed globally.",
                        "Doctor compared filenames only and did not claim cryptographic validity.",
                        $"The global keys inventory does not contain '{keyName}'.",
                        "Deploy the public BIKEY to the server keys directory.",
                        keyPath));
            }

            AddModInventory(
                result,
                new DoctorModInventory(
                    displayName,
                    modPath,
                    isServerMod,
                    directoryExists: true,
                    publishedId,
                    keyPaths,
                    declaredOrder: index + 1,
                    addonsDirectoryExists: addonsExists,
                    keysDirectoryExists: keysExists,
                    modMetadataExists: modCpp.Status == DoctorFileSystemStatus.Available
                        && !modCpp.IsReparsePoint,
                    metaMetadataExists: metaCpp.Status == DoctorFileSystemStatus.Available
                        && !metaCpp.IsReparsePoint,
                    pboCount,
                    bisignCount,
                    keyPaths.Length),
                budget,
                findings);
        }

        return result;
    }

    private void AddModInventory(
        ICollection<DoctorModInventory> result,
        DoctorModInventory item,
        ScanBudget budget,
        FindingCollector findings)
    {
        if (budget.TryAddInventory())
            result.Add(item);
        else
            AddInventoryLimitFinding(findings);
    }

    private static void InspectUnreferencedMods(
        IReadOnlyList<DoctorFileSystemEntry> rootEntries,
        IEnumerable<string> referencedPaths,
        FindingCollector findings)
    {
        var referenced = referencedPaths
            .Select(NormalizeComparisonPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in rootEntries.Where(entry =>
                     entry.Status == DoctorFileSystemStatus.Available
                     && entry.IsDirectory
                     && !entry.IsReparsePoint
                     && entry.Name.StartsWith('@')))
        {
            if (referenced.Contains(NormalizeComparisonPath(entry.FullPath)))
                continue;

            findings.Add(
                Finding(
                    DoctorFindingCodes.ModUnreferenced,
                    DoctorSeverity.Information,
                    "An installed mod directory is not referenced.",
                    "The top-level mod-like directory is not present in the selected launch command.",
                    $"Unreferenced directory: '{entry.FullPath}'.",
                    "Confirm whether the mod is intentionally inactive.",
                    entry.FullPath));
        }
    }

    private IReadOnlyList<string> InspectOptionalDirectory(
        string? path,
        string missingCode,
        string missingTitle,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            return [];

        var inspection = InspectDirectory(path, cancellationToken);

        if (inspection.Status == DoctorFileSystemStatus.Available
            && !inspection.IsReparsePoint)
        {
            return [path];
        }

        if (inspection.IsReparsePoint)
            AddReparseFinding(path, findings);
        else
            AddResourceFinding(
                inspection.Status,
                path,
                missingCode,
                DoctorFindingCodes.InventoryUnreadable,
                missingTitle,
                "A configured external directory cannot be inspected.",
                findings);

        return [path];
    }

    private IReadOnlyList<DoctorLogInventory> InspectLogs(
        string rootPath,
        IReadOnlyList<string> profilePaths,
        ScanBudget budget,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        var roots = new[] { (Path: rootPath, Category: "ServerRoot") }
            .Concat(profilePaths.Select(path => (Path: path, Category: "Profiles")))
            .GroupBy(item => NormalizeComparisonPath(item.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
        var result = new List<DoctorLogInventory>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            var inspection = InspectDirectory(root.Path, cancellationToken);

            if (inspection.Status != DoctorFileSystemStatus.Available
                || inspection.IsReparsePoint)
                continue;

            foreach (var entry in EnumerateOneDirectory(
                         root.Path,
                         budget,
                         findings,
                         cancellationToken))
            {
                if (entry.Status != DoctorFileSystemStatus.Available
                    || entry.IsDirectory
                    || entry.IsReparsePoint
                    || !IsLogExtension(entry.Name)
                    || entry.FileSize is null
                    || entry.LastModifiedUtc is null
                    || !seen.Add(NormalizeComparisonPath(entry.FullPath)))
                {
                    continue;
                }

                if (!budget.TryAddInventory())
                {
                    AddInventoryLimitFinding(findings);
                    break;
                }

                result.Add(
                    new DoctorLogInventory(
                        entry.FullPath,
                        entry.Name,
                        Path.GetExtension(entry.Name).ToLowerInvariant(),
                        entry.FileSize.Value,
                        DateTime.SpecifyKind(entry.LastModifiedUtc.Value, DateTimeKind.Utc),
                        root.Category));
            }
        }

        return result
            .OrderBy(log => log.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<DoctorFileSystemEntry> TraverseFiles(
        string rootPath,
        Func<DoctorFileSystemEntry, bool> predicate,
        ScanBudget budget,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        var result = new List<DoctorFileSystemEntry>();
        var pending = new Stack<(string Path, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push((rootPath, 0));

        while (pending.Count > 0 && budget.CanEnumerate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            var normalized = NormalizeComparisonPath(current.Path);

            if (!visited.Add(normalized))
            {
                findings.Add(
                    Finding(
                        DoctorFindingCodes.TraversalCycleSkipped,
                        DoctorSeverity.Warning,
                        "A filesystem traversal cycle was skipped.",
                        "Doctor visits each normalized directory at most once.",
                        $"The directory was already visited: '{current.Path}'.",
                        "Review the directory layout for cyclic references.",
                        current.Path));
                continue;
            }

            var entries = EnumerateOneDirectory(
                current.Path,
                budget,
                findings,
                cancellationToken);

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.Status != DoctorFileSystemStatus.Available)
                {
                    AddEntryFailure(entry, findings);
                    continue;
                }

                if (entry.IsReparsePoint)
                {
                    AddReparseFinding(entry.FullPath, findings);
                    continue;
                }

                if (entry.IsDirectory)
                {
                    if (current.Depth >= _limits.MaximumRecursionDepth)
                    {
                        findings.Add(
                            Finding(
                                DoctorFindingCodes.EnumerationDepthLimit,
                                DoctorSeverity.Warning,
                                "The Doctor traversal depth limit was reached.",
                                "Doctor stopped descending while preserving collected siblings.",
                                $"Maximum recursion depth: {_limits.MaximumRecursionDepth}.",
                                "Reduce unnecessary nesting or inspect the skipped path separately.",
                                entry.FullPath));
                    }
                    else
                    {
                        pending.Push((entry.FullPath, current.Depth + 1));
                    }

                    continue;
                }

                if (predicate(entry))
                    result.Add(entry);
            }
        }

        return result;
    }

    private IReadOnlyList<DoctorFileSystemEntry> EnumerateOneDirectory(
        string path,
        ScanBudget budget,
        FindingCollector findings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!budget.CanEnumerate)
        {
            AddEnumerationLimitFinding(findings);
            return [];
        }

        DoctorDirectoryEnumerationResult enumeration;

        try
        {
            enumeration = _fileSystem.EnumerateDirectory(
                path,
                budget.RemainingEnumerationEntries,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogInspectionFailure(exception, path);
            enumeration = new DoctorDirectoryEnumerationResult(
                DoctorFileSystemStatus.Unreadable,
                []);
        }

        if (enumeration.Status == DoctorFileSystemStatus.Cancelled)
            throw new OperationCanceledException(cancellationToken);

        budget.AddEnumerated(enumeration.Entries.Count);

        if (enumeration.LimitReached || !budget.CanEnumerate)
            AddEnumerationLimitFinding(findings);

        if (enumeration.Status is DoctorFileSystemStatus.Unreadable
            or DoctorFileSystemStatus.InvalidPath)
        {
            findings.Add(
                Finding(
                    enumeration.Status == DoctorFileSystemStatus.InvalidPath
                        ? DoctorFindingCodes.InvalidPath
                        : DoctorFindingCodes.InventoryUnreadable,
                    DoctorSeverity.Warning,
                    "A directory could not be completely enumerated.",
                    "Doctor preserved readable siblings returned before the directory failure.",
                    "The directory enumeration was incomplete.",
                    "Grant read access or correct the affected path.",
                    path));
        }

        foreach (var entry in enumeration.Entries)
        {
            if (entry.Status != DoctorFileSystemStatus.Available)
                AddEntryFailure(entry, findings);
            else if (entry.IsReparsePoint)
                AddReparseFinding(entry.FullPath, findings);
        }

        return enumeration.Entries;
    }

    private IReadOnlyList<string> SelectTopLevelFiles(
        IEnumerable<DoctorFileSystemEntry> entries,
        Func<DoctorFileSystemEntry, bool> predicate,
        ScanBudget budget,
        FindingCollector findings)
    {
        var result = new List<string>();

        foreach (var entry in entries)
        {
            if (entry.Status != DoctorFileSystemStatus.Available
                || entry.IsDirectory
                || entry.IsReparsePoint
                || !predicate(entry))
                continue;

            if (!budget.TryAddInventory())
            {
                AddInventoryLimitFinding(findings);
                break;
            }

            result.Add(entry.FullPath);
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private DoctorPathInspection InspectFile(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = _fileSystem.InspectFile(path, cancellationToken);

            if (result.Status == DoctorFileSystemStatus.Cancelled)
                throw new OperationCanceledException(cancellationToken);

            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogInspectionFailure(exception, path);
            return new DoctorPathInspection(DoctorFileSystemStatus.Unreadable);
        }
    }

    private DoctorPathInspection InspectDirectory(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = _fileSystem.InspectDirectory(path, cancellationToken);

            if (result.Status == DoctorFileSystemStatus.Cancelled)
                throw new OperationCanceledException(cancellationToken);

            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogInspectionFailure(exception, path);
            return new DoctorPathInspection(DoctorFileSystemStatus.Unreadable);
        }
    }

    private DoctorTextReadResult ReadText(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = _fileSystem.ReadText(path, maximumBytes, cancellationToken);

            if (result.Status == DoctorFileSystemStatus.Cancelled)
                throw new OperationCanceledException(cancellationToken);

            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogInspectionFailure(exception, path);
            return new DoctorTextReadResult(DoctorFileSystemStatus.Unreadable);
        }
    }

    private static void AddResourceFinding(
        DoctorFileSystemStatus status,
        string path,
        string missingCode,
        string unreadableCode,
        string missingTitle,
        string unreadableTitle,
        FindingCollector findings)
    {
        var invalid = status == DoctorFileSystemStatus.InvalidPath;
        var missing = status == DoctorFileSystemStatus.Missing;

        findings.Add(
            Finding(
                invalid
                    ? DoctorFindingCodes.InvalidPath
                    : missing
                        ? missingCode
                        : unreadableCode,
                missing ? DoctorSeverity.Error : DoctorSeverity.Warning,
                invalid
                    ? "A Doctor resource path is invalid."
                    : missing
                        ? missingTitle
                        : unreadableTitle,
                invalid
                    ? "Doctor rejected the path without attempting further access."
                    : missing
                        ? "The resource was not present at scan time."
                        : "The resource exists or may exist but could not be safely inspected.",
                invalid
                    ? "The supplied path is not a valid local filesystem path."
                    : missing
                        ? $"No resource was found at '{path}'."
                        : $"The resource at '{path}' was unavailable for read-only inspection.",
                invalid
                    ? "Correct the path and run the scan again."
                    : missing
                        ? "Restore the resource or correct its configured path."
                        : "Grant read access and run the scan again.",
                path));
    }

    private static void AddReadFailure(
        DoctorTextReadResult read,
        string path,
        long limit,
        string missingCode,
        string unreadableCode,
        string missingTitle,
        string unreadableTitle,
        FindingCollector findings)
    {
        if (read.Status == DoctorFileSystemStatus.TooLarge)
        {
            findings.Add(
                Finding(
                    DoctorFindingCodes.FileTooLarge,
                    DoctorSeverity.Warning,
                    "A Doctor text resource exceeds its safe read limit.",
                    "The oversized file was not read or parsed.",
                    $"Configured limit: {limit} bytes; detected size: {read.DetectedSize?.ToString() ?? "unknown"} bytes.",
                    "Reduce the file size or inspect it with a purpose-built offline tool.",
                    path));
            return;
        }

        AddResourceFinding(
            read.Status,
            path,
            missingCode,
            unreadableCode,
            missingTitle,
            unreadableTitle,
            findings);
    }

    private static void AddMetadataFailure(
        string path,
        DoctorFileSystemStatus status,
        FindingCollector findings)
    {
        AddResourceFinding(
            status,
            path,
            DoctorFindingCodes.ModMetadataPartial,
            DoctorFindingCodes.ModMetadataPartial,
            "Mod metadata disappeared during inspection.",
            "Mod metadata cannot be inspected.",
            findings);
    }

    private static void AddEntryFailure(
        DoctorFileSystemEntry entry,
        FindingCollector findings)
    {
        AddResourceFinding(
            entry.Status,
            entry.FullPath,
            DoctorFindingCodes.InventoryUnreadable,
            DoctorFindingCodes.InventoryUnreadable,
            "An enumerated resource disappeared during inspection.",
            "An enumerated resource cannot be inspected.",
            findings);
    }

    private static void AddReparseFinding(
        string path,
        FindingCollector findings)
    {
        findings.Add(
            Finding(
                DoctorFindingCodes.ReparsePointSkipped,
                DoctorSeverity.Information,
                "A reparse point was skipped.",
                "Doctor never follows symbolic links, junctions, mount points, or other reparse points.",
                $"Skipped path: '{path}'.",
                "Inspect the physical target separately if it is intentionally part of the deployment.",
                path));
    }

    private void AddEnumerationLimitFinding(FindingCollector findings)
    {
        findings.Add(
            Finding(
                DoctorFindingCodes.EnumerationItemLimit,
                DoctorSeverity.Warning,
                "The Doctor enumeration item limit was reached.",
                "The affected traversal stopped safely and retained previously collected results.",
                $"Maximum enumerated entries per scan: {_limits.MaximumEnumeratedEntries}.",
                "Narrow the selected target or reduce unnecessary deployment content."));
    }

    private void AddInventoryLimitFinding(FindingCollector findings)
    {
        findings.Add(
            Finding(
                DoctorFindingCodes.InventoryItemLimit,
                DoctorSeverity.Warning,
                "The Doctor inventory item limit was reached.",
                "Doctor stopped adding inventory records while retaining earlier results.",
                $"Maximum inventory entries per scan: {_limits.MaximumInventoryEntries}.",
                "Narrow the selected target or inspect large areas separately."));
    }

    private void LogInspectionFailure(
        Exception exception,
        string path)
    {
        _logger.LogWarning(
            exception,
            "Doctor could not inspect {SourcePath}.",
            path);
    }

    private static bool TryNormalizePath(
        string? value,
        string? basePath,
        out string path)
    {
        path = string.Empty;

        if (string.IsNullOrWhiteSpace(value)
            || value.IndexOf('\0') >= 0)
            return false;

        try
        {
            var trimmed = value.Trim().Trim('"');
            path = Path.GetFullPath(
                Path.IsPathFullyQualified(trimmed)
                    ? trimmed
                    : basePath is null
                        ? trimmed
                        : Path.Combine(basePath, trimmed));
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return false;
        }
    }

    private static string SafeDisplayPath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(invalid path)" : value.Trim();

    private static string NormalizeComparisonPath(string path) =>
        path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);

    private static bool IsWithin(
        string candidatePath,
        string parentPath)
    {
        var parent = NormalizeComparisonPath(parentPath)
            + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExtension(
        string path,
        string extension) =>
        string.Equals(
            Path.GetExtension(path),
            extension,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsLogExtension(string path)
    {
        var extension = Path.GetExtension(path);

        return extension.Equals(".rpt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".adm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".log", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mdmp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEnabled(string value) =>
        value.Equals("1", StringComparison.OrdinalIgnoreCase)
        || value.Equals("true", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> CreateLaunchArguments(
        DayZLaunchCommand? command)
    {
        if (command is null)
            return new Dictionary<string, string>();

        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        Add("config", command.ConfigurationPath);
        Add("profiles", command.ProfilesPath);
        Add("mission", command.Mission);
        Add("port", command.Port);
        Add(
            "mod",
            command.ClientModPaths.Count == 0
                ? null
                : string.Join(';', command.ClientModPaths));
        Add(
            "serverMod",
            command.ServerModPaths.Count == 0
                ? null
                : string.Join(';', command.ServerModPaths));
        Add("storage", command.StoragePath);
        Add("BEpath", command.BattleEyePath);

        return values;

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                values[name] = value;
        }
    }

    private static DoctorFinding Finding(
        string code,
        DoctorSeverity severity,
        string title,
        string explanation,
        string evidence,
        string recommendation,
        string? sourcePath = null) =>
        new(
            code,
            severity,
            title,
            explanation,
            evidence,
            recommendation,
            sourcePath);

    private static DoctorScanResult CompleteEmpty(
        string rootPath,
        FindingCollector findings,
        DateTime startedUtc) =>
        DoctorScanResult.Completed(
            new DoctorInventory(
                rootPath,
                null,
                [],
                null,
                [],
                null,
                null,
                null,
                null,
                [],
                [],
                [],
                [],
                [],
                [],
                []),
            findings.Items,
            startedUtc,
            DateTime.UtcNow);

    private sealed record StartupSelection(
        string? StartupPath,
        DayZLaunchCommand? Command);

    private sealed record StartupParseOutcome(
        IReadOnlyList<DayZLaunchCommand> Commands,
        IReadOnlyList<string> Limitations)
    {
        public bool IsPartial => Limitations.Count > 0;
    }

    private sealed record MissionAssessment(
        string? Path,
        IReadOnlyList<string> Files);

    private sealed class ScanBudget
    {
        private readonly DoctorScanLimits _limits;
        private int _enumerated;
        private int _inventory;

        public ScanBudget(DoctorScanLimits limits)
        {
            _limits = limits;
        }

        public bool CanEnumerate => _enumerated < _limits.MaximumEnumeratedEntries;

        public int RemainingEnumerationEntries =>
            _limits.MaximumEnumeratedEntries - _enumerated;

        public void AddEnumerated(int count)
        {
            _enumerated = Math.Min(
                _limits.MaximumEnumeratedEntries,
                checked(_enumerated + count));
        }

        public bool TryAddInventory()
        {
            if (_inventory >= _limits.MaximumInventoryEntries)
                return false;

            _inventory++;
            return true;
        }
    }

    private sealed class FindingCollector
    {
        private readonly int _maximum;
        private readonly List<DoctorFinding> _items = [];
        private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
        private bool _limitAdded;

        public FindingCollector(int maximum)
        {
            _maximum = maximum;
        }

        public IReadOnlyList<DoctorFinding> Items => _items;

        public void Add(DoctorFinding finding)
        {
            ArgumentNullException.ThrowIfNull(finding);

            var key = string.Join(
                "\u001f",
                finding.Code,
                finding.SourcePath?.ToUpperInvariant() ?? string.Empty,
                finding.Evidence);

            if (!_keys.Add(key))
                return;

            if (_items.Count < _maximum - 1)
            {
                _items.Add(finding);
                return;
            }

            if (_limitAdded)
                return;

            _limitAdded = true;
            _items.Add(
                Finding(
                    DoctorFindingCodes.FindingLimit,
                    DoctorSeverity.Warning,
                    "The Doctor finding limit was reached.",
                    "Additional findings were suppressed while collected inventory was retained.",
                    $"Maximum findings per scan: {_maximum}.",
                    "Resolve reported findings and scan again."));
        }
    }
}
