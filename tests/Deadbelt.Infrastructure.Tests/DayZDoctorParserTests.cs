using Deadbelt.Infrastructure.Doctor;

namespace Deadbelt.Infrastructure.Tests;

public sealed class DayZDoctorParserTests
{
    [Fact]
    public void BatchParserHandlesVariablesQuotesContinuationAndModLists()
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");
        var startup = Path.Combine(root, "scripts", "start.bat");
        var content = """
            @echo off
            set "CFG=..\server.cfg"
            "C:\synthetic-dayz\DayZServer_x64.exe" -config="%CFG%" ^
              "-mod=@CF;@Map" -serverMod="@Admin" -profiles=profiles -storage storage -port=2302 -mission=dayzOffline.chernarusplus -BEpath=battleye
            """;

        var result = DayZBatchStartupParser.Parse(
            content,
            startup,
            root);

        var command = Assert.Single(result.Commands);
        Assert.False(result.IsPartial);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, "server.cfg")),
            command.ConfigurationPath);
        Assert.Equal(
            [
                Path.Combine(root, "@CF"),
                Path.Combine(root, "@Map")
            ],
            command.ClientModPaths);
        Assert.Equal(
            [Path.Combine(root, "@Admin")],
            command.ServerModPaths);
        Assert.Equal(Path.Combine(root, "profiles"), command.ProfilesPath);
        Assert.Equal(Path.Combine(root, "storage"), command.StoragePath);
        Assert.Equal("2302", command.Port);
        Assert.Equal("dayzOffline.chernarusplus", command.Mission);
        Assert.Equal(Path.Combine(root, "battleye"), command.BattleEyePath);
    }

    [Fact]
    public void BatchParserReportsUnsupportedControlFlowWithoutExecutingIt()
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");
        var startup = Path.Combine(root, "start.cmd");
        var content = """
            if exist serverDZ.cfg goto launch
            powershell -File secret.ps1
            DayZServer_x64.exe -config=serverDZ.cfg
            """;

        var result = DayZBatchStartupParser.Parse(
            content,
            startup,
            root);

        Assert.Single(result.Commands);
        Assert.True(result.IsPartial);
        Assert.Contains(
            result.Limitations,
            limitation => limitation.Contains(
                "Unsupported batch control flow",
                StringComparison.Ordinal));
    }

    [Fact]
    public void BatchParserDoesNotFabricatePathsFromUnresolvedExpansion()
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");
        var startup = Path.Combine(root, "start.cmd");

        var result = DayZBatchStartupParser.Parse(
            "DayZServer_x64.exe -config=%UNKNOWN%\\server.cfg -mod=%MODS%",
            startup,
            root);

        var command = Assert.Single(result.Commands);
        Assert.True(result.IsPartial);
        Assert.Null(command.ConfigurationPath);
        Assert.Empty(command.ClientModPaths);
    }

    [Fact]
    public void ConfigurationParserExtractsSafeValuesAndNestedMissionTemplate()
    {
        var content = """
            // passwordAdmin = "must-not-appear";
            hostname = "Synthetic";
            password = "private";
            verifySignatures = 2;
            forceSameBuild = true;
            /* template = "ignored.comment"; */
            class Missions
            {
                class DayZ
                {
                    template = "dayzOffline.chernarusplus";
                };
            };
            """;

        var result = DayZConfigurationParser.Parse(content);

        Assert.False(result.IsPartial);
        Assert.Equal("Synthetic", result.Values["hostname"]);
        Assert.Equal("2", result.Values["verifySignatures"]);
        Assert.Equal("true", result.Values["forceSameBuild"]);
        Assert.False(result.Values.ContainsKey("password"));
        Assert.False(result.Values.ContainsKey("passwordAdmin"));
        Assert.Equal(
            "dayzOffline.chernarusplus",
            result.MissionTemplate);
    }

    [Fact]
    public void ConfigurationParserHandlesPhysicalNestedMissionsShape()
    {
        var result = DayZConfigurationParser.Parse(
            """
            hostname = "Example";
            passwordAdmin = "";
            verifySignatures = 2;
            forceSameBuild = 1;
            instanceId = 1;
            storageAutoFix = 1;

            class Missions
            {
                class DayZ
                {
                    template="dayzOffline.chernarusplus"; // Inline comment
                    // Vanilla mission: dayzOffline.chernarusplus
                };
            };
            """);

        Assert.Equal(
            "dayzOffline.chernarusplus",
            result.MissionTemplate);
        Assert.Equal("2", result.Values["verifySignatures"]);
        Assert.Equal(PasswordAdminState.Empty, result.PasswordAdminState);
        Assert.DoesNotContain(
            result.Values.Keys,
            key => key.Contains(
                "password",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConfigurationParserDoesNotTreatUnrelatedTemplateAsMission()
    {
        var result = DayZConfigurationParser.Parse(
            """
            template = "unrelated";
            verifySignatures = 2;
            """);

        Assert.Null(result.MissionTemplate);
        Assert.Equal("2", result.Values["verifySignatures"]);
    }

    [Fact]
    public void ConfigurationParserPreservesSafeFieldsWhenSyntaxIsPartial()
    {
        var result = DayZConfigurationParser.Parse(
            """
            hostname = "Synthetic";
            unsupported[] = { "value" };
            class Missions {
                class DayZ { template = "mission";
            """);

        Assert.True(result.IsPartial);
        Assert.Equal("Synthetic", result.Values["hostname"]);
        Assert.Equal("mission", result.MissionTemplate);
        Assert.Contains(
            result.Limitations,
            limitation => limitation.Contains(
                "not balanced",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("echo DayZServer_x64.exe -config=server.cfg")]
    [InlineData("@echo DayZServer_x64.exe -config=server.cfg")]
    [InlineData("rem DayZServer_x64.exe -config=server.cfg")]
    [InlineData("@rem DayZServer_x64.exe -config=server.cfg")]
    [InlineData(":: DayZServer_x64.exe -config=server.cfg")]
    [InlineData(":launch")]
    [InlineData("set COMMAND=DayZServer_x64.exe -config=server.cfg")]
    [InlineData("text DayZServer_x64.exe -config=server.cfg")]
    public void BatchParserIgnoresTextThatIsNotADirectLaunch(string line)
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");

        var result = DayZBatchStartupParser.Parse(
            line,
            Path.Combine(root, "start.cmd"),
            root);

        Assert.Empty(result.Commands);
    }

    [Theory]
    [InlineData("start DayZServer_x64.exe")]
    [InlineData("call DayZServer_x64.exe")]
    [InlineData("cmd /c DayZServer_x64.exe")]
    [InlineData("pwsh -Command DayZServer_x64.exe")]
    [InlineData("DayZServer_x64.exe | more")]
    [InlineData("DayZServer_x64.exe && echo done")]
    public void BatchParserMarksUnsupportedWrappersAndChainingPartial(string line)
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");

        var result = DayZBatchStartupParser.Parse(
            line,
            Path.Combine(root, "start.cmd"),
            root);

        Assert.True(result.IsPartial);
        Assert.Empty(result.Commands);
    }

    [Theory]
    [InlineData("DayZServer_x64.exe -config=server.cfg")]
    [InlineData("\"C:\\synthetic-dayz\\DayZServer_x64.exe\" -config=server.cfg")]
    public void BatchParserAcceptsOnlyDirectExecutablePosition(string line)
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");

        var result = DayZBatchStartupParser.Parse(
            line,
            Path.Combine(root, "start.cmd"),
            root);

        Assert.Single(result.Commands);
    }

    [Fact]
    public void BatchParserPreservesDuplicateModOrder()
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");

        var result = DayZBatchStartupParser.Parse(
            "DayZServer_x64.exe \"-mod=@One;@Two;@one\"",
            Path.Combine(root, "start.cmd"),
            root);

        Assert.Equal(
            [
                Path.Combine(root, "@One"),
                Path.Combine(root, "@Two"),
                Path.Combine(root, "@one")
            ],
            Assert.Single(result.Commands).ClientModPaths);
    }

    [Fact]
    public void BatchParserReportsMultipleRealLaunchCommands()
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");

        var result = DayZBatchStartupParser.Parse(
            """
            DayZServer_x64.exe -config=one.cfg
            "C:\synthetic-dayz\DayZServer_x64.exe" -config=two.cfg
            """,
            Path.Combine(root, "start.cmd"),
            root);

        Assert.Equal(2, result.Commands.Count);
        Assert.True(result.IsPartial);
    }

    [Fact]
    public void ConfigurationParserDoesNotMergeTokensAcrossBlockComments()
    {
        var result = DayZConfigurationParser.Parse(
            "maxPlayers = 1/* comment */2;");

        Assert.True(result.IsPartial);
        Assert.False(result.Values.ContainsKey("maxPlayers"));
    }

    [Fact]
    public void ConfigurationParserIgnoresQuotedAndCommentedFakeMissionClasses()
    {
        var result = DayZConfigurationParser.Parse(
            """
            hostname = "class Missions { template = \"fake\"; };";
            // class Missions { class DayZ { template = "comment"; }; };
            class Other { template = "unrelated"; };
            """);

        Assert.Null(result.MissionTemplate);
        Assert.Equal(
            "class Missions { template = \"fake\"; };",
            result.Values["hostname"]);
    }

    [Fact]
    public void ConfigurationParserAssessesPasswordWithoutRetainingItsValue()
    {
        const string secret = "should-never-escape";

        var present = DayZConfigurationParser.Parse(
            $"passwordAdmin = \"{secret}\";");
        var empty = DayZConfigurationParser.Parse(
            "passwordAdmin = \"\";");
        var missing = DayZConfigurationParser.Parse(
            "hostname = \"server\";");

        Assert.Equal(PasswordAdminState.Present, present.PasswordAdminState);
        Assert.Equal(PasswordAdminState.Empty, empty.PasswordAdminState);
        Assert.Equal(PasswordAdminState.Missing, missing.PasswordAdminState);
        Assert.DoesNotContain(
            present.Values.Values,
            value => value.Contains(secret, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("// publishedid = \"123\";", null)]
    [InlineData("note = \"publishedid = 123;\";", null)]
    [InlineData("publishedid = \"123\";", "123")]
    public void MetadataParserUsesOnlyExactUncommentedAssignments(
        string content,
        string? expected)
    {
        var result = DayZModMetadataParser.Parse(content);

        Assert.Equal(expected, result.PublishedId);
    }

    [Fact]
    public void MetadataParserReportsDuplicateAndMalformedAssignments()
    {
        var result = DayZModMetadataParser.Parse(
            """
            publishedid = "123";
            publishedid = invalid;
            name = "First";
            displayName = "Second";
            """);

        Assert.Null(result.PublishedId);
        Assert.Equal("Second", result.DisplayName);
        Assert.NotEmpty(result.Limitations);
    }
}
