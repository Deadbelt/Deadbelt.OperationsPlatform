using Deadbelt.Application.Doctor;
using Deadbelt.Domain.Doctor;
using Deadbelt.Domain.Environments;
using Deadbelt.Infrastructure.Doctor;
using Deadbelt.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Infrastructure.Tests;

public sealed class DayZDoctorHardeningTests
{
    [Fact]
    public async Task InvalidOperatorPathProducesFindingWithoutFilesystemInspection()
    {
        var fileSystem = new FaultInjectingDoctorFileSystem
        {
            ThrowWhenInspectingPath = string.Empty
        };
        var scanner = CreateScanner(fileSystem);

        var result = await scanner.ScanAsync(CreateRequest("\0invalid"));

        Assert.Equal(
            DoctorFindingCodes.InvalidPath,
            Assert.Single(result.Findings).Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidOperatorOverrideProducesRoutinePathFinding(
        bool startupOverride)
    {
        using var fixture = CreateFixture();
        var request = CreateRequest(
            fixture.RootPath,
            startupOverride ? "\0invalid" : fixture.GetPath("start.bat"),
            startupOverride ? fixture.GetPath("serverDZ.cfg") : "\0invalid");

        var result = await CreateScanner().ScanAsync(request);

        Assert.Equal(DoctorScanStatus.Completed, result.Status);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.InvalidPath);
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.ScanFailed);
    }

    [Fact]
    public async Task ReparsePointMissionIsSkipped()
    {
        using var fixture = CreateFixture();
        var missionPath = fixture.GetPath(
            "mpmissions",
            "dayzOffline.chernarusplus");
        var fileSystem = new FaultInjectingDoctorFileSystem
        {
            InspectDirectoryOverride = path =>
                string.Equals(path, missionPath, StringComparison.OrdinalIgnoreCase)
                    ? new DoctorPathInspection(
                        DoctorFileSystemStatus.Available,
                        FileAttributes.Directory | FileAttributes.ReparsePoint)
                    : null
        };

        var result = await CreateScanner(fileSystem).ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.ReparsePointSkipped
                && finding.SourcePath == missionPath);
        Assert.Empty(result.Inventory!.MissionFiles);
    }

    [Fact]
    public async Task InconsistentFilesystemCycleIsStopped()
    {
        using var fixture = CreateFixture();
        var missionPath = fixture.GetPath(
            "mpmissions",
            "dayzOffline.chernarusplus");
        var fileSystem = new FaultInjectingDoctorFileSystem
        {
            EnumerationOverride = (path, _) =>
                string.Equals(path, missionPath, StringComparison.OrdinalIgnoreCase)
                    ? new DoctorDirectoryEnumerationResult(
                        DoctorFileSystemStatus.Available,
                        [
                            new DoctorFileSystemEntry(
                                missionPath,
                                "cycle",
                                IsDirectory: true,
                                DoctorFileSystemStatus.Available,
                                FileAttributes.Directory,
                                null,
                                DateTime.UtcNow)
                        ])
                    : null
        };

        var result = await CreateScanner(fileSystem).ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.TraversalCycleSkipped);
    }

    [Fact]
    public async Task MaximumDepthStopsOnlyDeeperTraversal()
    {
        using var fixture = CreateFixture();
        fixture.AddFile(
            Path.Combine(
                "mpmissions",
                "dayzOffline.chernarusplus",
                "one",
                "two",
                "deep.xml"),
            "<root />");
        var limits = CreateLimits(maximumDepth: 1);

        var result = await CreateScanner(limits: limits).ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.EnumerationDepthLimit);
        Assert.DoesNotContain(
            result.Inventory!.MissionFiles,
            path => path.EndsWith("deep.xml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MaximumEnumerationEntriesReturnsPartialResult()
    {
        using var fixture = CreateFixture();
        var limits = CreateLimits(maximumEntries: 2);

        var result = await CreateScanner(limits: limits).ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Equal(DoctorScanStatus.Completed, result.Status);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.EnumerationItemLimit);
    }

    [Fact]
    public async Task FindingAndInventoryLimitsReturnStablePartialResults()
    {
        using var fixture = CreateFixture();
        var limits = CreateLimits(
            maximumFindings: 2,
            maximumInventory: 1);

        var result = await CreateScanner(limits: limits).ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.FindingLimit);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.InventoryItemLimit);
        Assert.True(result.Findings.Count <= 2);
    }

    [Fact]
    public async Task InaccessibleChildDoesNotSuppressReadableSibling()
    {
        using var fixture = CreateFixture();
        var mission = fixture.GetPath(
            "mpmissions",
            "dayzOffline.chernarusplus");
        var inaccessible = fixture.AddDirectory(
            "mpmissions",
            "dayzOffline.chernarusplus",
            "inaccessible");
        fixture.AddFile(
            Path.Combine(
                "mpmissions",
                "dayzOffline.chernarusplus",
                "readable",
                "malformed.xml"),
            "<root>");
        var fileSystem = new FaultInjectingDoctorFileSystem
        {
            EnumerationOverride = (path, _) =>
                string.Equals(path, inaccessible, StringComparison.OrdinalIgnoreCase)
                    ? new DoctorDirectoryEnumerationResult(
                        DoctorFileSystemStatus.Unreadable,
                        [])
                    : null
        };

        var result = await CreateScanner(fileSystem).ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.InventoryUnreadable
                && finding.SourcePath == inaccessible);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.MalformedXml
                && finding.SourcePath!.StartsWith(
                    Path.Combine(mission, "readable"),
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FileDisappearingAfterEnumerationDoesNotFailScan()
    {
        using var fixture = CreateFixture();
        var disappearing = fixture.GetPath(
            "mpmissions",
            "dayzOffline.chernarusplus",
            "settings.json");
        var fileSystem = new FaultInjectingDoctorFileSystem
        {
            ReadTextOverride = (path, _) =>
                string.Equals(path, disappearing, StringComparison.OrdinalIgnoreCase)
                    ? new DoctorTextReadResult(DoctorFileSystemStatus.Missing)
                    : null
        };

        var result = await CreateScanner(fileSystem).ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Equal(DoctorScanStatus.Completed, result.Status);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.MissionFileMissing
                && finding.SourcePath == disappearing);
    }

    [Theory]
    [InlineData("start.bat")]
    [InlineData("serverDZ.cfg")]
    [InlineData("meta.cpp")]
    [InlineData("types.xml")]
    [InlineData("settings.json")]
    public async Task OversizedTextResourceIsNotParsed(string fileName)
    {
        using var fixture = CreateFixture();
        var fileSystem = new FaultInjectingDoctorFileSystem
        {
            ReadTextOverride = (path, limit) =>
                string.Equals(
                    Path.GetFileName(path),
                    fileName,
                    StringComparison.OrdinalIgnoreCase)
                    ? new DoctorTextReadResult(
                        DoctorFileSystemStatus.TooLarge,
                        DetectedSize: limit + 1)
                    : null
        };

        var result = await CreateScanner(fileSystem).ScanAsync(
            CreateRequest(
                fixture.RootPath,
                fixture.GetPath("start.bat"),
                fixture.GetPath("serverDZ.cfg")));

        var finding = Assert.Single(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.FileTooLarge
                && string.Equals(
                    Path.GetFileName(finding.SourcePath),
                    fileName,
                    StringComparison.OrdinalIgnoreCase));
        Assert.Contains("bytes", finding.Evidence, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<!DOCTYPE root SYSTEM \"file:///not-read.dtd\"><root />")]
    [InlineData("<!DOCTYPE root [<!ENTITY x \"value\">]><root>&x;</root>")]
    [InlineData("<!DOCTYPE root [<!ENTITY a \"aaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;\">]><root>&b;</root>")]
    public async Task XmlDtdAndEntityDeclarationsAreRejected(string xml)
    {
        using var fixture = CreateFixture(typesXml: xml);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.MalformedXml
                && finding.SourcePath!.EndsWith(
                    "types.xml",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalXmlIsAcceptedByConstrainedReader()
    {
        using var fixture = CreateFixture(typesXml: "<types><type name=\"safe\" /></types>");

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.MalformedXml);
    }

    [Fact]
    public async Task DuplicateAndCrossRoleModReferencesProduceStableFindings()
    {
        using var fixture = CreateFixture(
            startup:
                "DayZServer_x64.exe -config=serverDZ.cfg \"-mod=@CF;@cf\" \"-serverMod=@CF\"");

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.ModDuplicateReference);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.ModRoleConflict);
        Assert.Single(result.Inventory!.ClientMods);
        Assert.Equal(1, result.Inventory.ClientMods[0].DeclaredOrder);
    }

    [Fact]
    public async Task GameplaySettingAndPasswordAreAssessedWithoutSecretExposure()
    {
        const string secret = "never-display-this-value";
        using var fixture = CreateFixture(
            configuration: $$"""
                passwordAdmin = "{{secret}}";
                verifySignatures = 2;
                enableCfgGameplayFile = 0;
                class Missions { class DayZ { template = "dayzOffline.chernarusplus"; }; };
                """);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));
        var rendered = string.Join(
            " ",
            result.Findings.SelectMany(finding =>
                new[]
                {
                    finding.Title,
                    finding.Explanation,
                    finding.Evidence,
                    finding.Recommendation
                }));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.GameplayConfigurationUnexpected);
        Assert.DoesNotContain(secret, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(
            result.Inventory!.ConfigurationValues.Values,
            value => value.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnabledGameplayConfigurationMustExist()
    {
        using var fixture = CreateFixture();
        var gameplayPath = fixture.GetPath(
            "mpmissions",
            "dayzOffline.chernarusplus",
            "cfggameplay.json");
        File.Delete(gameplayPath);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.GameplayConfigurationMissing);
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.GameplayConfigurationUnexpected);
    }

    [Fact]
    public async Task PresentGameplayConfigurationWarnsWhenSettingIsAbsent()
    {
        using var fixture = CreateFixture(
            configuration:
            """
            passwordAdmin = "not-retained";
            verifySignatures = 2;
            class Missions { class DayZ { template = "dayzOffline.chernarusplus"; }; };
            """);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.GameplayConfigurationUnexpected
                && finding.Severity == DoctorSeverity.Warning);
    }

    [Fact]
    public async Task PresentGameplayConfigurationDoesNotWarnWhenEnabled()
    {
        using var fixture = CreateFixture();

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.GameplayConfigurationUnexpected
                || finding.Code == DoctorFindingCodes.GameplayConfigurationMissing);
    }

    [Fact]
    public async Task DescriptionExtIsInventoriedWhenPresentButNotUniversallyRequired()
    {
        using var fixture = CreateFixture();
        var descriptionPath = fixture.GetPath(
            "mpmissions",
            "dayzOffline.chernarusplus",
            "description.ext");
        File.Delete(descriptionPath);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.DoesNotContain(
            result.Inventory!.MissionFiles,
            path => path.Equals(
                descriptionPath,
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.MissionFileMissing
                && finding.SourcePath?.Equals(
                    descriptionPath,
                    StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task InstalledButUnreferencedModProducesFinding()
    {
        using var fixture = CreateFixture();
        var unusedPath = fixture.AddMod(
            "@Unused",
            "Unused",
            "999999");

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == DoctorFindingCodes.ModUnreferenced
                && finding.SourcePath == unusedPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hostname = \"server\";")]
    public async Task MissingOrEmptyAdminPasswordProducesFinding(string passwordPrefix)
    {
        using var fixture = CreateFixture(
            configuration: $$"""
                {{passwordPrefix}}
                verifySignatures = 2;
                enableCfgGameplayFile = 1;
                class Missions { class DayZ { template = "dayzOffline.chernarusplus"; }; };
                """);

        if (passwordPrefix.Length == 0)
        {
            fixture.AddConfiguration(
                "serverDZ.cfg",
                """
                passwordAdmin = "";
                verifySignatures = 2;
                enableCfgGameplayFile = 1;
                class Missions { class DayZ { template = "dayzOffline.chernarusplus"; }; };
                """);
        }

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        Assert.Contains(
            result.Findings,
            finding => finding.Code == (
                passwordPrefix.Length == 0
                    ? DoctorFindingCodes.PasswordAdminEmpty
                    : DoctorFindingCodes.PasswordAdminMissing)
                && finding.Severity == DoctorSeverity.Warning
                && finding.Title == "Administrative password is not configured.");
    }

    [Fact]
    public async Task ModAndLogMetadataInventoryIsCompleteAndContentFree()
    {
        using var fixture = CreateFixture();
        var logPath = fixture.AddFile("server.MDMP", "dump contents are not read");
        var timestamp = new DateTime(2026, 7, 29, 12, 34, 56, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(logPath, timestamp);

        var result = await CreateScanner().ScanAsync(
            CreateRequest(fixture.RootPath));

        var mod = Assert.Single(result.Inventory!.ClientMods);
        Assert.True(mod.AddonsDirectoryExists);
        Assert.True(mod.KeysDirectoryExists);
        Assert.True(mod.ModMetadataExists);
        Assert.True(mod.MetaMetadataExists);
        Assert.Equal(1, mod.PboCount);
        Assert.Equal(1, mod.BisignCount);
        Assert.Equal(1, mod.BikeyCount);
        Assert.Equal("1559212036", mod.PublishedId);
        var log = Assert.Single(
            result.Inventory.LogFiles,
            item => item.FullPath == logPath);
        Assert.Equal(".mdmp", log.LogType);
        Assert.Equal(new FileInfo(logPath).Length, log.FileSize);
        Assert.Equal(timestamp, log.LastModifiedUtc);
        Assert.Equal("ServerRoot", log.SourceCategory);
    }

    private static DayZDoctorFixture CreateFixture(
        string? startup = null,
        string? configuration = null,
        string typesXml = "<types />")
    {
        var fixture = new DayZDoctorFixture();
        fixture.AddExecutable();
        fixture.AddStartup(
            "start.bat",
            startup
            ?? "DayZServer_x64.exe -config=serverDZ.cfg \"-mod=@CF\" -profiles=profiles -storage=storage");
        fixture.AddConfiguration(
            "serverDZ.cfg",
            configuration
            ?? """
            hostname = "Synthetic";
            passwordAdmin = "not-retained";
            verifySignatures = 2;
            enableCfgGameplayFile = 1;
            class Missions { class DayZ { template = "dayzOffline.chernarusplus"; }; };
            """);
        fixture.AddMission(
            "dayzOffline.chernarusplus",
            typesXml);
        fixture.AddMod(
            "@CF",
            "Community Framework",
            "1559212036",
            "Community.bikey");
        fixture.AddGlobalKey("Community.bikey");
        fixture.AddDirectory("profiles");
        fixture.AddDirectory("storage");
        return fixture;
    }

    private static DoctorScanRequest CreateRequest(
        string rootPath,
        string? startupPath = null,
        string? configurationPath = null) =>
        new(
            "workspace-identity",
            EnvironmentId.From(
                Guid.Parse("d4224328-99be-48de-9a9a-ed996379140a")),
            "Synthetic DayZ",
            GameType.DayZ,
            rootPath,
            startupPath,
            configurationPath);

    private static DayZLocalDoctorScanner CreateScanner(
        IDoctorFileSystem? fileSystem = null,
        DoctorScanLimits? limits = null) =>
        new(
            fileSystem ?? new OperatingSystemDoctorFileSystem(),
            NullLogger<DayZLocalDoctorScanner>.Instance,
            limits);

    private static DoctorScanLimits CreateLimits(
        int maximumDepth = 16,
        int maximumEntries = 100_000,
        int maximumFindings = 5_000,
        int maximumInventory = 100_000) =>
        new(
            maximumDepth,
            maximumEntries,
            maximumFindings,
            maximumInventory,
            maximumStartupBytes: 1024 * 1024,
            maximumConfigurationBytes: 2 * 1024 * 1024,
            maximumMetadataBytes: 1024 * 1024,
            maximumMissionDocumentBytes: 8 * 1024 * 1024);
}
