using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions(
    "ci",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = false,
    OnPullRequestBranches = ["main"],
    OnPullRequestIncludePaths = ["**/*"],
    OnPullRequestExcludePaths = ["**/*.md", ".skills/**", ".agents/**", ".issue/**", "**/.issue/**", "LICENSE"])]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft Visual Studio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(build => build.Default);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Seed the sample recording is generated from - Default is 1")]
    private readonly int Seed = 1;

    [Parameter("Where the sample recording is written - Default is recordings/aircraft-sample.ndjson")]
    private readonly string Recording = null!;

    [Parameter("How many payloads the sample recording holds - Default is the tool's own")]
    private readonly int? Polls;

    [Parameter("Seconds between the sample recording's payloads - Default is the tool's own")]
    private readonly double? PollInterval;

    [Solution("Transponder.slnx")] private readonly Solution Solution = null!;

    private AbsolutePath SourceDirectory => RootDirectory / "src";
    private AbsolutePath TestDirectory => RootDirectory / "test";
    private AbsolutePath ArtifactsDirectory => RootDirectory / ".artifacts";

    /// Where the sample recording is written. Absolute, so the tool resolves it to the same place
    /// whatever directory the run started from.
    private AbsolutePath SampleRecordingFile =>
        Recording is null
            ? RootDirectory / "recordings" / "aircraft-sample.ndjson"
            : (AbsolutePath) Path.GetFullPath(Recording);

    /// What this host can build. The MAUI heads in src/Gui target net10.0-ios and
    /// net10.0-maccatalyst, and no Linux SDK can build either, so a Linux run restores,
    /// formats and compiles the library and its tests instead of the whole solution.
    /// Every other host builds everything.
    private IEnumerable<AbsolutePath> BuildScope =>
        EnvironmentInfo.IsLinux
            ? [SourceDirectory / "Transponder" / "Transponder.csproj", TestDirectory / "UnitTests" / "UnitTests.csproj"]
            : [Solution.Path];

    private Target Clean => definition => definition
        .Before(Restore)
        .OnlyWhenStatic(() => IsLocalBuild)
        .Executes(() =>
        {
            DotNetClean(s => s.SetProject(Solution));

            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => x.DeleteDirectory());
            TestDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => x.DeleteDirectory());
            ArtifactsDirectory.CreateOrCleanDirectory();
        });


    private Target Format => target => target
        .DependsOn(Restore)
        .ProceedAfterFailure()
        .Executes(() => BuildScope.ForEach(project => DotNetFormat(settings => settings.SetProject(project))));

    Target Restore => definition => definition
        .DependsOn(Clean)
        .DependsOn(NugetRestore)
        .DependsOn(WorkflowRestore);

    Target NugetRestore => definition => definition
        .DependsOn(Clean)
        .DependsOn(WorkflowRestore)
        .Executes(() => BuildScope.ForEach(project => DotNetRestore(settings => settings.SetProjectFile(project))));

    /// Installs the workloads the MAUI heads need. It runs on a server and not on a developer's
    /// machine: a runner builds with the SDK Nuke downloads into .nuke/temp, which the job owns,
    /// so no elevation is involved, while a local run would target the developer's own installed
    /// SDK, where the same command wants sudo and where the workloads are already present.
    /// Skipped on Linux, which builds nothing that needs one.
    Target WorkflowRestore => definition => definition
        .DependsOn(Clean)
        .OnlyWhenStatic(() => IsServerBuild && !EnvironmentInfo.IsLinux)
        .Executes(() => DotNetWorkloadRestore());

    Target Compile => definition => definition
        .DependsOn(Restore)
        .DependsOn(Format)
        .Executes(() => BuildScope.ForEach(project => DotNetBuild(settings => settings.SetProjectFile(project))));

    private Target Test => definition => definition
        .DependsOn(Compile)
        .Executes(() =>
            TestDirectory.GlobFiles("**/*.csproj")
                .ForEach(project => DotNetTest(settings => settings
                    .SetProjectFile(project)
                    .EnableNoBuild()
                    .EnableNoRestore())));

    /// Writes a synthetic aircraft recording into the recordings root, so the application runs and
    /// the replay path is developed with no provider account (item 0052).
    ///
    /// Deliberately outside Default: recordings/ is where a real rehearsal capture lives, and a
    /// build that wrote there would overwrite one. It shells `dotnet run` rather than referencing
    /// the project, so this build still runs when the application does not compile.
    private Target SampleRecording => definition => definition
        .Executes(() => DotNetRun(settings => settings
            .SetProjectFile(RootDirectory / "tools" / "Transponder.SampleRecording" / "Transponder.SampleRecording.csproj")
            .SetApplicationArguments(SampleRecordingArguments())));

    /// What the generator is told: the seed and the output always, the length and the cadence only
    /// when they were asked for, so the tool's own defaults stay the one answer for them.
    private string[] SampleRecordingArguments()
    {
        var arguments = new List<string>
        {
            "--seed",
            Seed.ToString(CultureInfo.InvariantCulture),
            "--output",
            SampleRecordingFile.ToString(),
        };

        if (Polls is { } polls)
        {
            arguments.Add("--polls");
            arguments.Add(polls.ToString(CultureInfo.InvariantCulture));
        }

        if (PollInterval is { } interval)
        {
            arguments.Add("--interval");
            arguments.Add(interval.ToString(CultureInfo.InvariantCulture));
        }

        return arguments.ToArray();
    }

    Target Default => definition => definition
        .DependsOn(Format)
        .DependsOn(Compile)
        .DependsOn(Test);
}
