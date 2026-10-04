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
    OnPullRequestExcludePaths = ["**/*.md", ".skills/**", ".agents/**", ".issue/**", "features/**/.issue/**", "LICENSE"])]
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

    [Solution("Transponder.slnx")] private readonly Solution Solution = null!;

    private AbsolutePath SourceDirectory => RootDirectory / "src";
    private AbsolutePath TestDirectory => RootDirectory / "test";
    private AbsolutePath ArtifactsDirectory => RootDirectory / ".artifacts";

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
        .Executes(() => DotNetFormat());

    Target Restore => definition => definition
        .DependsOn(Clean)
        .DependsOn(NugetRestore)
        .DependsOn(WorkflowRestore);

    Target NugetRestore => definition => definition
        .DependsOn(Clean)
        .DependsOn(WorkflowRestore)
        .Executes(() =>
        {
            DotNetRestore();
        });

    Target WorkflowRestore => definition => definition
        .DependsOn(Clean)
        .Executes(() =>
        {
            // DotNetTasks.DotNetWorkloadRestore();
        });

    Target Compile => definition => definition
        .DependsOn(Restore)
        .DependsOn(Format)
        .Executes(() =>
        {
            DotNetBuild();
        });

    Target Default => definition => definition
        .DependsOn(Format)
        .DependsOn(Compile);
}
