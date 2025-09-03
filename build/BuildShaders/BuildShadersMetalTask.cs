
namespace BuildScripts;

[TaskName("Build Metal Shaders")]
[IsDependentOn(typeof(BuildMGFXCTask))]
public sealed class BuildShadersMetalTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnMacOs();
    public override void Run(BuildContext context)
    {
        var mgfxc = context.GetProjectPath(ProjectType.Tools, "MonoGame.Effect.Compiler");
        var shadersDir = "MonoGame.Framework/Platform/Graphics/Effect/Resources";
        var workingDir = "native/monogame/metal/";

        foreach (var filePath in context.GetFiles($"{shadersDir}/*.fx"))
        {
            context.Information($"Building {filePath.GetFilename()}");
            context.DotNetRun(mgfxc, $"\"{filePath}\" {filePath.GetFilenameWithoutExtension()}.mtl.mgfxo.h /Profile:Metal", workingDir);
            context.Information("");
        }
    }
}
