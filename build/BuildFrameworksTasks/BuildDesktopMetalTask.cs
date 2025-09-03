
namespace BuildScripts;

[TaskName("Build DesktopMetal")]
[IsDependentOn(typeof(BuildShadersMetalTask))]
[IsDependentOn(typeof(BuildNativeDependenciesTask))]
public sealed class BuildDesktopMetalTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnMacOs();
    public override void Run(BuildContext context)
    {
        var buildPremake = new BuildPremake();
        buildPremake.Run(context, "DesktopMetal", "native/monogame", "monogame.sln");
    }
}
