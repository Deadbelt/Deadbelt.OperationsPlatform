using Deadbelt.Domain.Doctor;

namespace Deadbelt.Domain.Tests;

public sealed class DoctorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindingRequiresStableTextFields(string? value)
    {
        Assert.Throws<ArgumentException>(() =>
            new DoctorFinding(
                value!,
                DoctorSeverity.Warning,
                "Title",
                "Explanation",
                "Evidence",
                "Recommendation"));
    }

    [Fact]
    public void FindingRejectsUndefinedSeverity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateFinding((DoctorSeverity)999));
    }

    [Fact]
    public void FindingNormalizesPublicText()
    {
        var finding = new DoctorFinding(
            " DOP.Doctor.Test ",
            DoctorSeverity.Warning,
            " Title ",
            " Explanation ",
            " Evidence ",
            " Recommendation ",
            " C:\\server\\file.cfg ");

        Assert.Equal("DOP.Doctor.Test", finding.Code);
        Assert.Equal("Title", finding.Title);
        Assert.Equal("Explanation", finding.Explanation);
        Assert.Equal("Evidence", finding.Evidence);
        Assert.Equal("Recommendation", finding.Recommendation);
        Assert.Equal("C:\\server\\file.cfg", finding.SourcePath);
    }

    [Fact]
    public void InventoryTakesDefensiveSnapshots()
    {
        var startupCandidates = new List<string> { "start.bat" };
        var values = new Dictionary<string, string>
        {
            ["verifySignatures"] = "2"
        };

        var inventory = CreateInventory(
            startupCandidates,
            values);

        startupCandidates.Add("other.cmd");
        values["verifySignatures"] = "0";

        Assert.Equal(["start.bat"], inventory.StartupCandidates);
        Assert.Equal("2", inventory.ConfigurationValues["verifySignatures"]);
    }

    [Fact]
    public void ModInventoryRequiresNameAndPath()
    {
        Assert.Throws<ArgumentException>(() =>
            new DoctorModInventory(
                "",
                "C:\\mods\\@Example",
                false,
                true,
                null));
        Assert.Throws<ArgumentException>(() =>
            new DoctorModInventory(
                "@Example",
                " ",
                false,
                true,
                null));
    }

    [Fact]
    public void CompletedResultReportsSeverityTotalsAndSnapshot()
    {
        var findings = new List<DoctorFinding>
        {
            CreateFinding(DoctorSeverity.Information),
            CreateFinding(DoctorSeverity.Warning),
            CreateFinding(DoctorSeverity.Error)
        };

        var result = DoctorScanResult.Completed(
            CreateInventory([], new Dictionary<string, string>()),
            findings,
            TimeSpan.FromMilliseconds(125));

        findings.Clear();

        Assert.Equal(DoctorScanStatus.Completed, result.Status);
        Assert.NotNull(result.Inventory);
        Assert.Equal(3, result.Findings.Count);
        Assert.Equal(1, result.InformationCount);
        Assert.Equal(1, result.WarningCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(TimeSpan.FromMilliseconds(125), result.Duration);
    }

    [Fact]
    public void CancelledResultDoesNotRetainPartialState()
    {
        var result = DoctorScanResult.Cancelled(
            TimeSpan.FromMilliseconds(10));

        Assert.Equal(DoctorScanStatus.Cancelled, result.Status);
        Assert.Null(result.Inventory);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void CompletedResultRejectsNullFinding()
    {
        Assert.Throws<ArgumentException>(() =>
            DoctorScanResult.Completed(
                CreateInventory([], new Dictionary<string, string>()),
                [null!],
                TimeSpan.Zero));
    }

    [Fact]
    public void CompletedResultDeduplicatesOnlyMatchingCodePathAndEvidence()
    {
        var first = new DoctorFinding(
            "DOP.Doctor.Duplicate",
            DoctorSeverity.Warning,
            "First",
            "Explanation.",
            "Same evidence.",
            "Action.",
            "C:\\Server\\Mods\\@CF\\");
        var duplicate = new DoctorFinding(
            "DOP.Doctor.Duplicate",
            DoctorSeverity.Error,
            "Different presentation",
            "Different explanation.",
            "Same evidence.",
            "Different action.",
            "c:\\server\\mods\\@cf");
        var differentEvidence = new DoctorFinding(
            "DOP.Doctor.Duplicate",
            DoctorSeverity.Warning,
            "Third",
            "Explanation.",
            "Different evidence.",
            "Action.",
            "C:\\Server\\Mods\\@CF");

        var result = DoctorScanResult.Completed(
            CreateInventory([], new Dictionary<string, string>()),
            [first, duplicate, differentEvidence],
            TimeSpan.Zero);

        Assert.Equal([first, differentEvidence], result.Findings);
    }

    [Fact]
    public void ResultTimestampsMustBeUtcAndOrdered()
    {
        var inventory = CreateInventory([], new Dictionary<string, string>());
        var start = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() =>
            DoctorScanResult.Completed(
                inventory,
                [],
                DateTime.SpecifyKind(start, DateTimeKind.Local),
                start.AddSeconds(1)));
        Assert.Throws<ArgumentException>(() =>
            DoctorScanResult.Completed(
                inventory,
                [],
                start,
                start.AddTicks(-1)));
    }

    [Fact]
    public void InventoryRejectsNullAndBlankCollectionElements()
    {
        Assert.Throws<ArgumentException>(() =>
            new DoctorInventory(
                "C:\\server",
                null,
                [" "],
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
                []));

        Assert.Throws<ArgumentException>(() =>
            new DoctorInventory(
                "C:\\server",
                null,
                [],
                null,
                [],
                null,
                null,
                null,
                null,
                [],
                [null!],
                [],
                [],
                [],
                [],
                []));
    }

    [Fact]
    public void InventoryRejectsPasswordValues()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateInventory(
                [],
                new Dictionary<string, string>
                {
                    ["passwordAdmin"] = "must-not-be-retained"
                }));
    }

    [Fact]
    public void LogInventoryRejectsInvalidMetadata()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DoctorLogInventory(
                "C:\\server\\server.rpt",
                "server.rpt",
                ".rpt",
                -1,
                DateTime.UtcNow,
                "ServerRoot"));
        Assert.Throws<ArgumentException>(() =>
            new DoctorLogInventory(
                "C:\\server\\server.rpt",
                "server.rpt",
                ".rpt",
                1,
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local),
                "ServerRoot"));
    }

    [Fact]
    public void FailedResultRequiresErrorFinding()
    {
        Assert.Throws<ArgumentException>(() =>
            DoctorScanResult.Failed(
                CreateFinding(DoctorSeverity.Warning),
                TimeSpan.Zero));
    }

    [Fact]
    public void ResultRejectsNegativeDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DoctorScanResult.Cancelled(
                TimeSpan.FromMilliseconds(-1)));
    }

    private static DoctorFinding CreateFinding(DoctorSeverity severity)
    {
        return new DoctorFinding(
            "DOP.Doctor.Test",
            severity,
            "Test finding",
            "A safe explanation.",
            $"Safe {severity} evidence.",
            "Take a concrete action.");
    }

    private static DoctorInventory CreateInventory(
        IEnumerable<string> startupCandidates,
        IReadOnlyDictionary<string, string> values)
    {
        return new DoctorInventory(
            "C:\\server",
            "C:\\server\\DayZServer_x64.exe",
            startupCandidates,
            "C:\\server\\start.bat",
            ["C:\\server\\serverDZ.cfg"],
            "C:\\server\\serverDZ.cfg",
            values,
            "dayzOffline.chernarusplus",
            "C:\\server\\mpmissions\\dayzOffline.chernarusplus",
            [],
            [],
            [],
            [],
            [],
            [],
            []);
    }
}
