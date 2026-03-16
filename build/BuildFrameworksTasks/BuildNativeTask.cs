
namespace BuildScripts;

[TaskName("Build Native")]
[IsDependentOn(typeof(BuildMGFXCTask))]
[IsDependentOn(typeof(BuildNativeDependenciesTask))]
public sealed class BuildNativeTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var buildPremake = new BuildPremake();
        buildPremake.Run(context, "mgruntime", "native/monogame", "monogame.sln");

        // Build Android OpenXR native library if NDK is available
        BuildAndroidNative(context);

        context.DotNetPack(context.GetProjectPath(ProjectType.Framework, "Native"), context.DotNetPackSettings);
        context.DotNetPack("src/NuGetPackages/MonoGame.Framework/MonoGame.Framework.csproj", context.DotNetPackSettings);

        // MonoGame.Runtime.* NuGet packages are packed in the "Pack Native Runtime" task,
        // which downloads native binaries from all platform/arch build agents first.
        // This is necessary because Linux arm64 and x64 are built on separate runners.

        // Pack Android OpenXR runtime NuGet if the native binary was built
        var androidOpenXRLib = "Artifacts/native/mgruntime/openxr/android/Release/libmgruntime.so";
        if (context.FileExists(androidOpenXRLib))
        {
            context.DotNetPack("src/NuGetPackages/MonoGame.Runtime.Android.OpenXR/MonoGame.Runtime.Android.OpenXR.csproj", context.DotNetPackSettings);
        }

        context.PublishBinaries("Native");
    }

    private void BuildAndroidNative(BuildContext context)
    {
        var ndkHome = System.Environment.GetEnvironmentVariable("ANDROID_NDK_HOME")
            ?? System.Environment.GetEnvironmentVariable("NDK_ROOT");

        // GitHub Actions runners have NDK under $ANDROID_HOME/ndk/<version>
        if (string.IsNullOrEmpty(ndkHome))
        {
            var androidHome = System.Environment.GetEnvironmentVariable("ANDROID_HOME")
                ?? System.Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
            if (!string.IsNullOrEmpty(androidHome))
            {
                var ndkDir = System.IO.Path.Combine(androidHome, "ndk");
                if (System.IO.Directory.Exists(ndkDir))
                {
                    var versions = System.IO.Directory.GetDirectories(ndkDir)
                        .OrderByDescending(d => d)
                        .ToArray();
                    if (versions.Length > 0)
                    {
                        ndkHome = versions[0];
                        context.Log.Information($"Discovered NDK at: {ndkHome}");
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(ndkHome))
        {
            context.Log.Warning("Android NDK not found — skipping Android native build.");
            return;
        }

        var toolchainFile = System.IO.Path.Combine(ndkHome, "build", "cmake", "android.toolchain.cmake");
        if (!System.IO.File.Exists(toolchainFile))
        {
            context.Log.Warning($"NDK toolchain not found at {toolchainFile} — skipping Android native build.");
            return;
        }

        var sourceDir = "native/monogame";
        var buildDir = System.IO.Path.Combine(sourceDir, "build", "android");

        if (context.DirectoryExists(buildDir))
            context.DeleteDirectory(buildDir, new DeleteDirectorySettings { Recursive = true });
        context.CreateDirectory(buildDir);

        var configureArgs = new ProcessArgumentBuilder()
            .Append("-S").AppendQuoted(context.MakeAbsolute(new DirectoryPath(sourceDir)).FullPath)
            .Append("-B").AppendQuoted(context.MakeAbsolute(new DirectoryPath(buildDir)).FullPath);

        // Windows defaults to VS generator which doesn't work with NDK cross-compilation
        if (context.Environment.Platform.Family == PlatformFamily.Windows)
            configureArgs.Append("-G").AppendQuoted("Ninja");

        configureArgs
            .AppendQuoted($"-DCMAKE_TOOLCHAIN_FILE={toolchainFile}")
            .Append("-DANDROID_ABI=arm64-v8a")
            .Append("-DANDROID_PLATFORM=android-29")
            .Append("-DCMAKE_BUILD_TYPE=Release");

        var settings = new ProcessSettings { Arguments = configureArgs };
        if (context.StartProcess("cmake", settings) != 0)
            throw new Exception("Android native CMake configuration failed!");

        var buildArgs = new ProcessArgumentBuilder()
            .Append("--build")
            .AppendQuoted(context.MakeAbsolute(new DirectoryPath(buildDir)).FullPath)
            .Append("--config").Append("Release")
            .Append("--parallel");

        settings = new ProcessSettings { Arguments = buildArgs };
        if (context.StartProcess("cmake", settings) != 0)
            throw new Exception("Android native build failed!");
    }
}
