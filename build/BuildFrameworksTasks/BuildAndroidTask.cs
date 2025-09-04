
namespace BuildScripts;

[TaskName("Build Android")]
[IsDependentOn(typeof(BuildShadersOGLTask))]
public sealed class BuildAndroidTask : FrostingTask<BuildContext>
{
    private string platformName = "Android";
    public override bool ShouldRun(BuildContext context) => context.IsWorkloadInstalled("android");

    public override void Run(BuildContext context)
    {
        var arguments = new DotNetMSBuildSettings();
        var androidSdkPath = context.EnvironmentVariable("ANDROID_SDK_ROOT");
        arguments.WithProperty("AndroidSdkDirectory", androidSdkPath);
        arguments.WithProperty("AcceptAndroidSDKLicenses", "true");
        arguments.WithTarget("InstallAndroidDependencies");
        var installSettings = new DotNetBuildSettings
        {
            MSBuildSettings = arguments,
            Verbosity = DotNetVerbosity.Minimal,
            Configuration = context.DotNetPackSettings.Configuration,
        };

        if (!Directory.Exists(androidSdkPath))
        {
            context.DotNetBuild(context.GetProjectPath(ProjectType.Framework, platformName), installSettings);
        }
        context.DotNetPack(context.GetProjectPath(ProjectType.Framework, platformName), context.DotNetPackSettings);
    }
}
