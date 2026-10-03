using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Tools.DotNet;

[GitHubActions("ci", GitHubActionsImage.UbuntuLatest, AutoGenerate = true, OnPushBranches = ["main"], OnPullRequestBranches = ["main"])]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(build => build.Default);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    Target Clean => definition => definition
        .Before(Restore)
        .Executes(() =>
        {
        });

    Target Restore => definition => definition
        .DependsOn(DotNetRestore)
        .DependsOn(DotNetWorkflowRestore);

    Target DotNetRestore => definition => definition
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore();
        });

    Target DotNetWorkflowRestore => definition => definition
        .Executes(() =>
        {
            // DotNetTasks.DotNetWorkloadRestore();
        });

    Target Compile => definition => definition
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild();
        });

    Target Default => definition => definition
        .DependsOn(Compile);

}
