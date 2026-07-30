using Deadbelt.Infrastructure.Doctor;

namespace Deadbelt.Infrastructure.Tests;

public sealed class DayZPowerShellStartupParserTests
{
    [Fact]
    public void ParsesDirectCallOperatorWithQuotedExecutable()
    {
        var context = CreateContext();

        var result = Parse(
            """& "C:\synthetic-dayz\DayZServer_x64.exe" -config=serverDZ.cfg""",
            context);

        var command = Assert.Single(result.Commands);
        Assert.False(result.IsPartial);
        Assert.Equal(
            Path.Combine(context.Root, "serverDZ.cfg"),
            command.ConfigurationPath);
    }

    [Fact]
    public void ParsesDirectVariableExecutableAndStaticSplat()
    {
        var context = CreateContext();
        var result = Parse(
            """
            $serverExecutable = "C:\synthetic-dayz\DayZServer_x64.exe"
            $arguments = @(
                "-config=serverDZ.cfg"
                "-profiles=profiles"
                "-port=2302"
            )
            & $serverExecutable @arguments
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.Equal("2302", command.Port);
        Assert.Equal(
            Path.Combine(context.Root, "profiles"),
            command.ProfilesPath);
    }

    [Fact]
    public void ParsesDirectQuotedInvocationWithoutCallOperator()
    {
        var context = CreateContext();

        var result = Parse(
            "\"C:\\synthetic-dayz\\DayZServer_x64.exe\" '-port=2302'",
            context);

        Assert.Equal("2302", Assert.Single(result.Commands).Port);
    }

    [Fact]
    public void ParsesUnquotedExecutableWithBacktickEscapedSpace()
    {
        var context = CreateContext();

        var result = Parse(
            @"& C:\synthetic` dayz\DayZServer_x64.exe -port=2302",
            context);

        var command = Assert.Single(result.Commands);
        Assert.Equal(
            Path.GetFullPath(@"C:\synthetic dayz\DayZServer_x64.exe"),
            command.ExecutablePath);
        Assert.Equal("2302", command.Port);
    }

    [Fact]
    public void ParsesStartProcessLiteralFilePathAndArguments()
    {
        var context = CreateContext();
        var result = Parse(
            """
            Start-Process -FilePath "C:\synthetic-dayz\DayZServer_x64.exe" `
                -ArgumentList "-config=serverDZ.cfg", "-port=2302"
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.Equal(
            Path.Combine(context.Root, "serverDZ.cfg"),
            command.ConfigurationPath);
        Assert.Equal("2302", command.Port);
    }

    [Fact]
    public void ParsesStartProcessResolvedVariablesAndJoinPath()
    {
        var context = CreateContext();
        var result = Parse(
            """
            $serverRoot = 'C:\synthetic-dayz'
            $configPath = "$serverRoot\serverDZ.cfg"
            $serverExecutable = Join-Path $serverRoot 'DayZServer_x64.exe'
            $arguments = @(
                "-config=$configPath"
                '-mission=dayzOffline.chernarusplus'
            )
            Start-Process `
                -FilePath $serverExecutable `
                -ArgumentList $arguments
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.Equal(
            Path.Combine(context.Root, "DayZServer_x64.exe"),
            command.ExecutablePath);
        Assert.Equal(
            Path.Combine(context.Root, "serverDZ.cfg"),
            command.ConfigurationPath);
        Assert.Equal("dayzOffline.chernarusplus", command.Mission);
    }

    [Fact]
    public void ParsesCommaSeparatedArrayAndAllSupportedPathArguments()
    {
        var context = CreateContext();
        var result = Parse(
            """
            $arguments = "-profiles=profiles", "-storage=storage", "-BEpath=battleye", "-port=2402"
            & 'C:\synthetic-dayz\DayZServer_x64.exe' @arguments
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.Equal(Path.Combine(context.Root, "profiles"), command.ProfilesPath);
        Assert.Equal(Path.Combine(context.Root, "storage"), command.StoragePath);
        Assert.Equal(Path.Combine(context.Root, "battleye"), command.BattleEyePath);
        Assert.Equal("2402", command.Port);
    }

    [Fact]
    public void PreservesSemicolonModOrderDuplicatesAndRoles()
    {
        var context = CreateContext();
        var result = Parse(
            """
            $arguments = @(
                '-mod=@CF;@Expansion;@cf'
                '-serverMod=@Admin;@Tools'
            )
            & 'C:\synthetic-dayz\DayZServer_x64.exe' @arguments
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.Equal(
            [
                Path.Combine(context.Root, "@CF"),
                Path.Combine(context.Root, "@Expansion"),
                Path.Combine(context.Root, "@cf")
            ],
            command.ClientModPaths);
        Assert.Equal(
            [
                Path.Combine(context.Root, "@Admin"),
                Path.Combine(context.Root, "@Tools")
            ],
            command.ServerModPaths);
    }

    [Fact]
    public void CommentsAndNonExecutableMentionsDoNotCreateLaunches()
    {
        var context = CreateContext();
        var result = Parse(
            """
            # & "C:\synthetic-dayz\DayZServer_x64.exe"
            <#
              Start-Process -FilePath "C:\synthetic-dayz\DayZServer_x64.exe"
            #>
            Write-Host "DayZServer_x64.exe is documented here # not a comment"
            Write-Output "DayZServer_x64.exe is also documentation"
            $documentation = "Run DayZServer_x64.exe later"
            DayZServer_x64.exe is documentation text
            """,
            context);

        Assert.Empty(result.Commands);
        Assert.False(result.IsPartial);
    }

    [Fact]
    public void CommentMarkersInsideStringsRemainLiteral()
    {
        var context = CreateContext();
        var result = Parse(
            """
            $profiles = 'profiles#one'
            & 'C:\synthetic-dayz\DayZServer_x64.exe' "-profiles=$profiles"
            """,
            context);

        Assert.Equal(
            Path.Combine(context.Root, "profiles#one"),
            Assert.Single(result.Commands).ProfilesPath);
    }

    [Fact]
    public void EscapedQuotesDoNotExposeCommentMarkersOrCreateFalseLaunches()
    {
        var context = CreateContext();
        var result = Parse(
            """
            Write-Host "Document DayZServer_x64.exe as `"# text`" only"
            Write-Host 'Document DayZServer_x64.exe as ''# text'' only'
            & 'C:\synthetic-dayz\DayZServer_x64.exe' '-port=2302'
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.Equal("2302", command.Port);
        Assert.False(result.IsPartial);
    }

    [Fact]
    public void UnresolvedExecutableVariableProducesPartialWithoutFabrication()
    {
        var context = CreateContext();
        var result = Parse(
            "& $serverExecutable @arguments",
            context);

        Assert.Empty(result.Commands);
        Assert.True(result.IsPartial);
        Assert.Contains(
            result.Limitations,
            limitation => limitation.Contains(
                "could not be resolved statically",
                StringComparison.Ordinal));
    }

    [Fact]
    public void CommandSubstitutionProducesPartialWithoutLaunch()
    {
        var context = CreateContext();
        var result = Parse(
            """
            $serverExecutable = $(Get-Item '.\DayZServer_x64.exe')
            & $serverExecutable
            """,
            context);

        Assert.Empty(result.Commands);
        Assert.True(result.IsPartial);
    }

    [Theory]
    [InlineData("Invoke-Expression '& DayZServer_x64.exe'")]
    [InlineData("iex '& DayZServer_x64.exe'")]
    [InlineData("Invoke-Command { DayZServer_x64.exe }")]
    [InlineData("powershell.exe -File nested.ps1")]
    [InlineData("pwsh.exe -File nested.ps1")]
    [InlineData("cmd.exe /c DayZServer_x64.exe")]
    [InlineData(". .\\shared.ps1")]
    [InlineData("Import-Module .\\dynamic.psm1")]
    [InlineData("Get-Content args.txt | DayZServer_x64.exe")]
    public void DangerousOrRuntimeSyntaxIsOnlyReportedAsPartial(string source)
    {
        var result = Parse(source, CreateContext());

        Assert.Empty(result.Commands);
        Assert.True(result.IsPartial);
    }

    [Fact]
    public void MultipleStaticLaunchesAreAmbiguous()
    {
        var context = CreateContext();
        var result = Parse(
            """
            & 'C:\synthetic-dayz\DayZServer_x64.exe' '-port=2302'
            Start-Process -FilePath 'C:\synthetic-dayz\DayZServer_x64.exe' -ArgumentList '-port=2402'
            """,
            context);

        Assert.Equal(2, result.Commands.Count);
        Assert.True(result.IsPartial);
        Assert.Contains(
            result.Limitations,
            limitation => limitation.Contains(
                "More than one",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoversSanitizedParameterAndWorkingDirectoryPattern()
    {
        var context = CreateExternalScriptContext();
        var result = Parse(
            """
            param(
                [string]$ServerDir = "C:\synthetic-dayz",
                [int]$Port = 2302,
                [string]$ModLine = ""
            )

            $ServerExe = Join-Path $ServerDir "DayZServer_x64.exe"
            $ProfilesDir = Join-Path $ServerDir "Profiles"
            if (-not (Test-Path $ServerExe)) { throw "missing executable" }
            $existing = Get-Process -Name "DayZServer_x64"
            if ($existing) { Write-Output "already running" }

            $args = @(
                "-config=serverDZ.cfg",
                "-port=$Port",
                "-profiles=$ProfilesDir",
                "-dologs"
            )

            if ($ModLine -and $ModLine.Trim().Length -gt 0) {
                $args += "-mod=$ModLine"
            }

            Start-Process -FilePath $ServerExe -ArgumentList $args -WorkingDirectory $ServerDir
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.True(result.IsPartial);
        Assert.Equal(
            Path.Combine(context.Root, "DayZServer_x64.exe"),
            command.ExecutablePath);
        Assert.Equal(
            Path.Combine(context.Root, "serverDZ.cfg"),
            command.ConfigurationPath);
        Assert.Equal(
            Path.Combine(context.Root, "Profiles"),
            command.ProfilesPath);
        Assert.Equal("2302", command.Port);
        Assert.Empty(command.ClientModPaths);
        Assert.Empty(command.ServerModPaths);
    }

    [Fact]
    public void IncrementalArgumentsPreserveStaticOrderAndRoles()
    {
        var context = CreateContext();
        var result = Parse(
            """
            $Arguments = @("-config=serverDZ.cfg")
            $ARGUMENTS += "-port=2402"
            $arguments += @(
                "-storage=storage"
                "-BEpath=battleye"
                "-mod=@CF;@Expansion;@CF"
                "-serverMod=@ServerOnly;@Tools"
            )
            & 'C:\synthetic-dayz\DayZServer_x64.exe' $arguments
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.False(result.IsPartial);
        Assert.Equal("2402", command.Port);
        Assert.Equal(Path.Combine(context.Root, "storage"), command.StoragePath);
        Assert.Equal(Path.Combine(context.Root, "battleye"), command.BattleEyePath);
        Assert.Equal(
            [
                Path.Combine(context.Root, "@CF"),
                Path.Combine(context.Root, "@Expansion"),
                Path.Combine(context.Root, "@CF")
            ],
            command.ClientModPaths);
        Assert.Equal(
            [
                Path.Combine(context.Root, "@ServerOnly"),
                Path.Combine(context.Root, "@Tools")
            ],
            command.ServerModPaths);
    }

    [Fact]
    public void PositionalStartProcessFilePathIsSupported()
    {
        var context = CreateExternalScriptContext();
        var result = Parse(
            """
            $serverExecutable = 'C:\synthetic-dayz\DayZServer_x64.exe'
            $arguments = '-config=serverDZ.cfg', '-port=2302'
            Start-Process $serverExecutable -ArgumentList $arguments
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.Equal(
            Path.Combine(context.Root, "serverDZ.cfg"),
            command.ConfigurationPath);
        Assert.Equal("2302", command.Port);
    }

    [Fact]
    public void FullyStaticStartProcessSplatIsResolvedWithoutExecution()
    {
        var context = CreateExternalScriptContext();
        var result = Parse(
            """
            $serverRoot = 'C:\synthetic-dayz'
            $serverExecutable = Join-Path $serverRoot 'DayZServer_x64.exe'
            $arguments = '-config=serverDZ.cfg', '-port=2302'
            $launch = @{
                FilePath = $serverExecutable
                ArgumentList = $arguments
                WorkingDirectory = $serverRoot
            }
            Start-Process @launch
            """,
            context);

        var command = Assert.Single(result.Commands);
        Assert.False(result.IsPartial);
        Assert.Equal(
            Path.Combine(context.Root, "serverDZ.cfg"),
            command.ConfigurationPath);
        Assert.Equal("2302", command.Port);
    }

    [Theory]
    [InlineData(
        """
        if ($runtimeChoice) {
            Start-Process 'C:\synthetic-dayz\DayZServer_x64.exe' -ArgumentList '-config=serverDZ.cfg'
        }
        """)]
    [InlineData(
        """
        function Start-Server {
            $serverExecutable = 'C:\synthetic-dayz\DayZServer_x64.exe'
            $arguments = '-config=serverDZ.cfg'
            Start-Process $serverExecutable -ArgumentList $arguments
        }
        """)]
    public void SingleLaunchInsideRuntimeContainerIsRecoveredAsPartial(string source)
    {
        var context = CreateContext();
        var result = Parse(source, context);

        var command = Assert.Single(result.Commands);
        Assert.True(result.IsPartial);
        Assert.Equal(
            Path.Combine(context.Root, "serverDZ.cfg"),
            command.ConfigurationPath);
    }

    [Fact]
    public void IdenticalConditionalLaunchesAreRecoveredOnce()
    {
        var context = CreateContext();
        var result = Parse(
            """
            if ($runtimeChoice) {
                & '.\DayZServer_x64.exe' '-config=serverDZ.cfg'
            }
            else {
                & '.\DayZServer_x64.exe' '-config=serverDZ.cfg'
            }
            """,
            context);

        Assert.Single(result.Commands);
        Assert.True(result.IsPartial);
    }

    [Fact]
    public void MateriallyDifferentConditionalLaunchesRemainAmbiguous()
    {
        var context = CreateContext();
        var result = Parse(
            """
            if ($runtimeChoice) {
                & '.\DayZServer_x64.exe' '-config=primary.cfg'
            }
            else {
                & '.\DayZServer_x64.exe' '-config=alternate.cfg'
            }
            """,
            context);

        Assert.Equal(2, result.Commands.Count);
        Assert.True(result.IsPartial);
    }

    private static PowerShellStartupParseResult Parse(
        string content,
        ParserContext context) =>
        DayZPowerShellStartupParser.Parse(
            content,
            context.Startup,
            context.Root);

    private static ParserContext CreateContext()
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");

        return new ParserContext(
            root,
            Path.Combine(root, "Start-DayZServer.ps1"));
    }

    private static ParserContext CreateExternalScriptContext()
    {
        var root = Path.GetFullPath("C:\\synthetic-dayz");

        return new ParserContext(
            root,
            Path.GetFullPath(
                "C:\\synthetic-scripts\\Start-DayZServer.ps1"));
    }

    private sealed record ParserContext(
        string Root,
        string Startup);
}
