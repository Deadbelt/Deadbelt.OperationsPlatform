using Deadbelt.Application.Doctor;
using Deadbelt.Domain.Doctor;
using Deadbelt.Domain.Environments;
using Deadbelt.Infrastructure.Doctor;
using Deadbelt.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Infrastructure.Tests;

public sealed class DayZLocalDoctorScannerTests
{
    [Fact]
    public async Task CompleteFixtureProducesInventoryWithoutErrors()
    {
        using var fixture = CreateCompleteFixture();
        var scanner = CreateScanner();

        var result = await scanner.ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Equal(DoctorScanStatus.Completed, result.Status);
        Assert.Equal(0, result.ErrorCount);
        var inventory = Assert.IsType<DoctorInventory>(result.Inventory);
        Assert.Equal(
            fixture.GetPath("serverDZ.cfg"),
            inventory.ActiveConfigurationPath);
        Assert.Equal(
            "dayzOffline.chernarusplus",
            inventory.MissionTemplate);
        Assert.Equal("Synthetic DayZ", inventory.ConfigurationValues["hostname"]);
        Assert.DoesNotContain(
            inventory.ConfigurationValues.Keys,
            key => key.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("@Community Framework", Assert.Single(inventory.ClientMods).Name);
        Assert.Equal("@Server Tools", Assert.Single(inventory.ServerMods).Name);
        Assert.Single(inventory.GlobalKeys);
        Assert.Single(inventory.ProfilePaths);
        Assert.Single(inventory.StoragePaths);
        Assert.Single(inventory.LogFiles);
    }

    [Fact]
    public async Task ExplicitConfigurationOverrideHasHighestPrecedence()
    {
        using var fixture = CreateCompleteFixture();
        var overridePath = fixture.AddConfiguration(
            "operator.cfg",
            ValidConfiguration(
                "Operator Override",
                "dayzOffline.chernarusplus"));
        var scanner = CreateScanner();

        var result = await scanner.ScanAsync(
            CreateRequest(
                fixture.RootPath,
                configurationFilePath: overridePath));

        Assert.Equal(
            overridePath,
            Assert.IsType<DoctorInventory>(result.Inventory).ActiveConfigurationPath);
        Assert.Equal(
            "Operator Override",
            result.Inventory!.ConfigurationValues["hostname"]);
    }

    [Fact]
    public async Task ExplicitStartupResolvesConfigurationRelativeToScript()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        fixture.AddConfiguration(
            Path.Combine("scripts", "selected.cfg"),
            ValidConfiguration(
                "Selected",
                "dayzOffline.chernarusplus"));
        var startup = fixture.AddStartup(
            Path.Combine("scripts", "selected.cmd"),
            "\"..\\DayZServer_x64.exe\" -config=selected.cfg");
        fixture.AddStartup(
            "other.bat",
            "DayZServer_x64.exe -config=serverDZ.cfg");
        var scanner = CreateScanner();

        var result = await scanner.ScanAsync(
            CreateRequest(
                fixture.RootPath,
                startupFilePath: startup));

        Assert.Equal(startup, result.Inventory!.SelectedStartupPath);
        Assert.Equal(
            fixture.GetPath("scripts", "selected.cfg"),
            result.Inventory.ActiveConfigurationPath);
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.StartupAmbiguous);
    }

    [Fact]
    public async Task ExplicitPowerShellStartupIsAuthoritativeAndResolvesConfiguration()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        var script = fixture.AddStartup(
            "Start-DayZServer.ps1",
            $$"""
            $serverRoot = '{{fixture.RootPath}}'
            $serverExecutable = Join-Path $serverRoot 'DayZServer_x64.exe'
            $arguments = @(
                '-config=serverDZ.cfg'
                '-profiles=profiles'
                '-storage=storage'
                '-port=2302'
                '-mod=@CF'
                '-serverMod=@Server'
            )
            & $serverExecutable @arguments
            """);
        fixture.AddStartup(
            "other.bat",
            "DayZServer_x64.exe -config=other.cfg");

        var result = await CreateScanner().ScanAsync(
            CreateRequest(
                fixture.RootPath,
                startupFilePath: script));

        Assert.Equal(script, result.Inventory!.SelectedStartupPath);
        Assert.Equal(
            fixture.GetPath("serverDZ.cfg"),
            result.Inventory.ActiveConfigurationPath);
        Assert.Equal("2302", result.Inventory.LaunchArguments["port"]);
        Assert.Equal(
            fixture.GetPath("profiles"),
            result.Inventory.LaunchArguments["profiles"]);
        Assert.Equal(
            fixture.GetPath("storage"),
            result.Inventory.LaunchArguments["storage"]);
        Assert.Single(result.Inventory.ClientMods);
        Assert.Single(result.Inventory.ServerMods);
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.StartupAmbiguous);
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.StartupPartialParse);
    }

    [Fact]
    public async Task SingleDiscoveredPowerShellStartupParticipatesInDiscovery()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        var script = fixture.AddStartup(
            "Start-DayZServer.ps1",
            """
            Start-Process -FilePath '.\DayZServer_x64.exe' `
                -ArgumentList '-config=serverDZ.cfg'
            """);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Equal(script, result.Inventory!.SelectedStartupPath);
        Assert.Equal(
            fixture.GetPath("serverDZ.cfg"),
            result.Inventory.ActiveConfigurationPath);
    }

    [Fact]
    public async Task DiscoveredPowerShellAndBatchLaunchesAreAmbiguous()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        fixture.AddStartup(
            "Start-DayZServer.ps1",
            "& '.\\DayZServer_x64.exe' '-config=serverDZ.cfg'");
        fixture.AddStartup(
            "start.bat",
            "DayZServer_x64.exe -config=serverDZ.cfg");

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Null(result.Inventory!.SelectedStartupPath);
        Assert.Null(result.Inventory.ActiveConfigurationPath);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.StartupAmbiguous);
    }

    [Fact]
    public async Task ExplicitPowerShellStartupWithMultipleLaunchesIsAmbiguous()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        var script = fixture.AddStartup(
            "Start-DayZServer.ps1",
            """
            & '.\DayZServer_x64.exe' '-config=serverDZ.cfg' '-port=2302'
            & '.\DayZServer_x64.exe' '-config=alternate.cfg' '-port=2402'
            """);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(
                fixture.RootPath,
                startupFilePath: script));

        Assert.Equal(script, result.Inventory!.SelectedStartupPath);
        Assert.Null(result.Inventory.ActiveConfigurationPath);
        Assert.Empty(result.Inventory.LaunchArguments);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.StartupAmbiguous);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.ConfigurationUnresolved);
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.MissionTemplateMissing);
    }

    [Fact]
    public async Task DynamicPowerShellStartupRetainsConfigurationOverrideAndPartialFinding()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        var script = fixture.AddStartup(
            "Start-DayZServer.ps1",
            """
            $serverExecutable = $(Get-Item '.\DayZServer_x64.exe')
            & $serverExecutable
            """);
        var configuration = fixture.GetPath("serverDZ.cfg");

        var result = await CreateScanner().ScanAsync(
            CreateRequest(
                fixture.RootPath,
                startupFilePath: script,
                configurationFilePath: configuration));

        Assert.Equal(DoctorScanStatus.Completed, result.Status);
        Assert.Equal(script, result.Inventory!.SelectedStartupPath);
        Assert.Equal(configuration, result.Inventory.ActiveConfigurationPath);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.StartupPartialParse
                && finding.Explanation.Contains(
                    "never executed",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SanitizedPowerShellShapeRecoversConfigurationWithoutOverride()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        var script = fixture.AddStartup(
            Path.Combine("scripts", "Start-DayZServer.ps1"),
            $$"""
            param(
                [string]$ServerDir = "{{fixture.RootPath}}",
                [int]$Port = 2302,
                [string]$ModLine = ""
            )
            $ServerExe = Join-Path $ServerDir "DayZServer_x64.exe"
            $ProfilesDir = Join-Path $ServerDir "profiles"
            $existing = Get-Process -Name "DayZServer_x64"
            if ($existing) { Write-Output "already running" }
            $args = @(
                "-config=serverDZ.cfg",
                "-port=$Port",
                "-profiles=$ProfilesDir"
            )
            if ($ModLine -and $ModLine.Trim().Length -gt 0) {
                $args += "-mod=$ModLine"
            }
            Start-Process -FilePath $ServerExe -ArgumentList $args -WorkingDirectory $ServerDir
            """);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(
                fixture.RootPath,
                startupFilePath: script));

        Assert.Equal(script, result.Inventory!.SelectedStartupPath);
        Assert.Equal(
            fixture.GetPath("serverDZ.cfg"),
            result.Inventory.ActiveConfigurationPath);
        Assert.Equal(
            "dayzOffline.chernarusplus",
            result.Inventory.MissionTemplate);
        Assert.Equal("2302", result.Inventory.LaunchArguments["port"]);
        Assert.Equal(
            fixture.GetPath("profiles"),
            result.Inventory.LaunchArguments["profiles"]);
        var partial = Assert.Single(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.StartupPartialParse);
        Assert.Contains("configuration", partial.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Script execution: none", partial.Evidence, StringComparison.Ordinal);
        Assert.True(partial.Evidence.Length < 400);
        Assert.DoesNotContain(
            "$existing",
            partial.Evidence,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.ConfigurationUnresolved);
    }

    [Fact]
    public async Task PhysicalSmokeShapeWithOverrideProducesCorrectedFindings()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        var configuration = fixture.AddConfiguration(
            "serverDZ.cfg",
            """
            hostname = "Example";
            passwordAdmin = "";
            verifySignatures = 2;
            enableCfgGameplayFile = 0;
            class Missions
            {
                class DayZ
                {
                    template="dayzOffline.chernarusplus"; // Inline comment
                };
            };
            """);
        File.Delete(
            fixture.GetPath(
                "mpmissions",
                "dayzOffline.chernarusplus",
                "description.ext"));
        var script = fixture.AddStartup(
            "Start-DayZServer.ps1",
            """
            $ServerExe = Get-Item '.\DayZServer_x64.exe'
            Start-Process -FilePath $ServerExe -ArgumentList $args
            """);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(
                fixture.RootPath,
                startupFilePath: script,
                configurationFilePath: configuration));

        Assert.Equal(configuration, result.Inventory!.ActiveConfigurationPath);
        Assert.Equal(
            "dayzOffline.chernarusplus",
            result.Inventory.MissionTemplate);
        Assert.Equal(
            fixture.GetPath(
                "mpmissions",
                "dayzOffline.chernarusplus"),
            result.Inventory.MissionPath);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.PasswordAdminEmpty
                && finding.Severity == DoctorSeverity.Warning);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.GameplayConfigurationUnexpected
                && finding.Severity == DoctorSeverity.Warning);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.StartupPartialParse);
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.ConfigurationUnresolved
                || finding.Code == DoctorFindingCodes.MissionTemplateMissing
                || (finding.Code == DoctorFindingCodes.MissionFileMissing
                    && finding.SourcePath?.EndsWith(
                        "description.ext",
                        StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public async Task PowerShellStartupScanDoesNotMutateFixture()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        fixture.AddStartup(
            "Start-DayZServer.ps1",
            "& '.\\DayZServer_x64.exe' '-config=serverDZ.cfg'");
        var before = fixture.CaptureSnapshot();

        _ = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        var after = fixture.CaptureSnapshot();
        Assert.Equal(before.Directories.Keys, after.Directories.Keys);
        Assert.Equal(before.Files.Keys, after.Files.Keys);

        foreach (var path in before.Directories.Keys)
            Assert.Equal(before.Directories[path], after.Directories[path]);

        foreach (var path in before.Files.Keys)
        {
            Assert.Equal(before.Files[path].Content, after.Files[path].Content);
            Assert.Equal(before.Files[path].Attributes, after.Files[path].Attributes);
            Assert.Equal(before.Files[path].LastWriteUtc, after.Files[path].LastWriteUtc);
        }
    }

    [Fact]
    public async Task AmbiguousDiscoveryReturnsPartialInventoryWithoutAssumingCommonConfig()
    {
        using var fixture = CreateCompleteFixture(includeStartup: false);
        fixture.AddStartup(
            "one.bat",
            "DayZServer_x64.exe -config=serverDZ.cfg");
        fixture.AddStartup(
            "two.cmd",
            "DayZServer_x64.exe -config=other.cfg");
        var scanner = CreateScanner();

        var result = await scanner.ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Equal(DoctorScanStatus.Completed, result.Status);
        Assert.Null(result.Inventory!.SelectedStartupPath);
        Assert.Null(result.Inventory.ActiveConfigurationPath);
        Assert.Contains(
            result.Inventory.ConfigurationCandidates,
            path => path == fixture.GetPath("serverDZ.cfg"));
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.StartupAmbiguous);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.ConfigurationUnresolved);
    }

    [Fact]
    public async Task MalformedMissionFilesProduceSpecificFindings()
    {
        using var fixture = CreateCompleteFixture(
            typesXml: "<types>",
            settingsJson: "{");
        var scanner = CreateScanner();

        var result = await scanner.ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.MalformedXml);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.MalformedJson);
    }

    [Fact]
    public async Task MissingGlobalModKeyProducesConcreteFinding()
    {
        using var fixture = CreateCompleteFixture(includeGlobalKey: false);
        var scanner = CreateScanner();

        var result = await scanner.ScanAsync(
            CreateRequest(fixture.RootPath));

        var finding = Assert.Single(
            result.Findings,
            candidate => candidate.Code == DoctorFindingCodes.ModKeyMissing
                && candidate.Severity == DoctorSeverity.Error);
        Assert.Contains("Community.bikey", finding.Evidence);
        Assert.Contains("Deploy", finding.Recommendation);
    }

    [Fact]
    public async Task DeterministicUnreadablePathDoesNotEscapeOrExposeException()
    {
        using var fixture = CreateCompleteFixture();
        var fileSystem = new FaultInjectingDoctorFileSystem
        {
            ThrowWhenInspectingPath = fixture.GetPath("mpmissions")
        };
        var scanner = CreateScanner(fileSystem);

        var result = await scanner.ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Equal(DoctorScanStatus.Completed, result.Status);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.InventoryUnreadable);
        Assert.DoesNotContain(
            result.Findings,
            finding =>
                finding.Evidence.Contains(
                    "Deterministic test-only",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancellationDuringEnumerationIsObserved()
    {
        using var fixture = CreateCompleteFixture();
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FaultInjectingDoctorFileSystem
        {
            BeforeEnumerateFiles = cancellation.Cancel
        };
        var scanner = CreateScanner(fileSystem);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scanner.ScanAsync(
                CreateRequest(fixture.RootPath),
                cancellation.Token));
    }

    [Fact]
    public async Task ScanDoesNotMutateFixture()
    {
        using var fixture = CreateCompleteFixture();
        var before = fixture.CaptureSnapshot();
        var scanner = CreateScanner();

        _ = await scanner.ScanAsync(
            CreateRequest(fixture.RootPath));

        var after = fixture.CaptureSnapshot();
        Assert.Equal(before.Directories.Keys, after.Directories.Keys);
        Assert.Equal(before.Files.Keys, after.Files.Keys);

        foreach (var path in before.Directories.Keys)
        {
            Assert.Equal(
                before.Directories[path].Attributes,
                after.Directories[path].Attributes);
            Assert.Equal(
                before.Directories[path].LastWriteUtc,
                after.Directories[path].LastWriteUtc);
        }

        foreach (var path in before.Files.Keys)
        {
            Assert.Equal(before.Files[path].Content, after.Files[path].Content);
            Assert.Equal(before.Files[path].Attributes, after.Files[path].Attributes);
            Assert.Equal(before.Files[path].LastWriteUtc, after.Files[path].LastWriteUtc);
        }
    }

    [Fact]
    public async Task MissingRootReturnsSafePartialInventory()
    {
        using var fixture = new DayZDoctorFixture();
        var missingRoot = fixture.GetPath("missing");
        var scanner = CreateScanner();

        var result = await scanner.ScanAsync(
            CreateRequest(missingRoot));

        Assert.Equal(DoctorScanStatus.Completed, result.Status);
        Assert.Equal(missingRoot, result.Inventory!.TargetRootPath);
        Assert.Equal(
            DoctorFindingCodes.TargetRootMissing,
            Assert.Single(result.Findings).Code);
    }

    private static DayZDoctorFixture CreateCompleteFixture(
        bool includeStartup = true,
        bool includeGlobalKey = true,
        string typesXml = "<types />",
        string settingsJson = "{}")
    {
        var fixture = new DayZDoctorFixture();
        fixture.AddExecutable();

        if (includeStartup)
        {
            fixture.AddStartup(
                "start.bat",
                """
                @echo off
                DayZServer_x64.exe -config=serverDZ.cfg "-mod=@CF" "-serverMod=@Server" -profiles=profiles -storage=storage
                """);
        }

        fixture.AddConfiguration(
            "serverDZ.cfg",
            ValidConfiguration(
                "Synthetic DayZ",
                "dayzOffline.chernarusplus"));
        fixture.AddMission(
            "dayzOffline.chernarusplus",
            typesXml,
            settingsJson);
        fixture.AddMod(
            "@CF",
            "@Community Framework",
            "1559212036",
            "Community.bikey");
        fixture.AddMod(
            "@Server",
            "@Server Tools",
            "999999");

        if (includeGlobalKey)
            fixture.AddGlobalKey("Community.bikey");
        else
            fixture.AddDirectory("keys");

        var profiles = fixture.AddDirectory("profiles");
        fixture.AddDirectory("storage");
        fixture.AddFile(
            Path.Combine(
                Path.GetRelativePath(fixture.RootPath, profiles),
                "server.RPT"),
            "synthetic log");

        return fixture;
    }

    private static string ValidConfiguration(
        string hostname,
        string missionTemplate)
    {
        return $$"""
            hostname = "{{hostname}}";
            passwordAdmin = "not-exposed";
            verifySignatures = 2;
            enableCfgGameplayFile = 1;
            class Missions
            {
                class DayZ
                {
                    template = "{{missionTemplate}}";
                };
            };
            """;
    }

    private static DoctorScanRequest CreateRequest(
        string rootPath,
        string? startupFilePath = null,
        string? configurationFilePath = null)
    {
        return new DoctorScanRequest(
            "C:\\workspace",
            EnvironmentId.From(
                Guid.Parse("69921e20-09fd-44cc-87c8-d60ecb733997")),
            "Synthetic DayZ",
            GameType.DayZ,
            rootPath,
            startupFilePath,
            configurationFilePath);
    }

    private static DayZLocalDoctorScanner CreateScanner(
        IDoctorFileSystem? fileSystem = null)
    {
        return fileSystem is null
            ? new DayZLocalDoctorScanner(
                NullLogger<DayZLocalDoctorScanner>.Instance)
            : new DayZLocalDoctorScanner(
                fileSystem,
                NullLogger<DayZLocalDoctorScanner>.Instance);
    }
}
