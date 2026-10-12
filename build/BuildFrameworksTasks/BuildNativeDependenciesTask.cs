using System.Runtime.InteropServices;

namespace BuildScripts;

[TaskName("Build Native Dependencies")]
public sealed class BuildNativeDependenciesTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.Environment.Platform.Family == PlatformFamily.Windows)
        {
            // Cross-compile both architectures on the same x64 runner
            BuildDependenciesForArch(context, "x64");
            BuildDependenciesForArch(context, "arm64");
        }
        else
        {
            // Linux/macOS: build for the host architecture only
            var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            BuildDependenciesForArch(context, arch);

            if (context.Environment.Platform.Family == PlatformFamily.OSX)
            {
                // build Dav1d for the "other" arch since meson does not support doing it in one go like cmake does
                arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "x86_64" : "arm64";
                var crossFile = "native/monogame/external/dav1d/build/crossfile.txt";
                File.WriteAllText(crossFile, @"[binaries]
c = 'clang'
cpp = 'clang++'
ar = 'ar'
strip = 'strip'

[properties]
c_args = ['-arch', 'x86_64']
c_link_args = ['-arch', 'x86_64']

[host_machine]
system = 'darwin'
cpu_family = 'x86_64'
cpu = 'x86_64'
endian = 'little'");

                BuildDav1d(context, arch, "--cross-file", context.MakeAbsoluteForDocker(new DirectoryPath(crossFile)).FullPath);
                BuildNestEgg(context, arch, "--host=x86_64-apple-darwin");

                var dav1dSourceDir = "native/monogame/external/dav1d";
                var dav1dArm64BuildDir = System.IO.Path.Combine(dav1dSourceDir, "build", "arm64", "src", "libdav1d.a");
                var dav1dx64BuildDir = System.IO.Path.Combine(dav1dSourceDir, "build", "x64", "src", "libdav1d.a");
                var dav1dOutputDir = System.IO.Path.Combine(dav1dSourceDir, "build", "libdav1d.a");
                // We need to lipo -create build/libdav1d.a build-x86_64/libdav1d.a -output libdav1d.a on mac evantually
                var lipoArgs = new ProcessArgumentBuilder();
                lipoArgs.Append("-create");
                lipoArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(dav1dArm64BuildDir)).FullPath);
                lipoArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(dav1dx64BuildDir)).FullPath);
                lipoArgs.Append("-output");
                lipoArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(dav1dOutputDir)).FullPath);
                RunLipo(context, lipoArgs, "Failed to lipo dav1d libraries");

                var nestEggSourceDir = "native/monogame/external/nestegg";
                var nestEggArm64BuildDir = System.IO.Path.Combine(nestEggSourceDir, "build", "arm64", "src", ".libs", "libnestegg.a");
                var nestEggx64BuildDir = System.IO.Path.Combine(nestEggSourceDir, "build", "x86_64", "src", ".libs", "libnestegg.a");
                var nestEggOutputDir = System.IO.Path.Combine(nestEggSourceDir, "build", "libnestegg.a");
                // We need to lipo -create build/libnestegg.a build-x86_64/libnestegg.a -output libnestegg.a on mac evantually
                var nestEggArgs = new ProcessArgumentBuilder();
                nestEggArgs.Append("-create");
                nestEggArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(nestEggArm64BuildDir)).FullPath);
                nestEggArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(nestEggx64BuildDir)).FullPath);
                nestEggArgs.Append("-output");
                nestEggArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(nestEggOutputDir)).FullPath);
                RunLipo(context, nestEggArgs, "Failed to lipo nestegg libraries");                
            }
        }
    }

    private void BuildDependenciesForArch(BuildContext context, string targetArch)
    {
        BuildSDL2(context, targetArch);
        BuildFAudio(context, targetArch);
        BuildDav1d(context, targetArch);
        BuildNestEgg(context, targetArch);
    }

    private void BuildDav1d(BuildContext context, string targetArch, params string[] args)
    {
        var dav1dSourceDir = "native/monogame/external/dav1d";
        var dav1dBuildDir = System.IO.Path.Combine(dav1dSourceDir, "build", targetArch);

        RecreateDirectory(context, dav1dBuildDir);

        /*

Investigate building for each "arch" using cross files.
See https://github.com/moonlight-stream/moonlight-qt-deps/tree/main/meson-crossfiles
note we need to use mscv for dav1d since mingw is not compatible.
[binaries]
c = 'clang'
cpp = 'clang++'
ar = 'ar'

[properties]
c_args = ['-arch', 'x86_64']
c_link_args = ['-arch', 'x86_64']

[host_machine]
system = 'darwin'
cpu_family = 'x86_64'
cpu = 'x86_64'
endian = 'little'
        */

        var configureArgs = new ProcessArgumentBuilder();
        configureArgs.Append("setup");
        configureArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(dav1dBuildDir)).FullPath);
        configureArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(dav1dSourceDir)).FullPath);
        configureArgs.Append("-Ddefault_library=static");
        configureArgs.Append("--buildtype release");
        configureArgs.Append("-Denable_tools=false");
        configureArgs.Append("-Denable_tests=false");
        configureArgs.Append("-Denable_docs=false");

        foreach(var arg in args)
        {
            configureArgs.Append(arg);
        }

        RunMeson(context, configureArgs, "Dav1d meson call failed.");

        var buildArgs = new ProcessArgumentBuilder();
        buildArgs.Append("-C");
        buildArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(dav1dBuildDir)).FullPath);
        RunNinja(context, buildArgs, "Dav1d ninja call failed.");
    }

    private void BuildNestEgg(BuildContext context, string targetArch, params string[] args)
    {
        var nestEggSrcDir = "native/monogame/external/nestegg";
        var nestEggBuildDir = System.IO.Path.Combine(nestEggSrcDir, "build", targetArch);

        RecreateDirectory(context, nestEggBuildDir);

        var autoReConfArgs = new ProcessArgumentBuilder();
        autoReConfArgs.Append("--install");
        autoReConfArgs.Append(context.MakeAbsoluteForDocker(new DirectoryPath(nestEggSrcDir)).FullPath);
        RunAutoReConf(context, autoReConfArgs, "");
        var configureArgs = new ProcessArgumentBuilder();
        configureArgs.Append("--enable-static");
        configureArgs.Append("--disable-shared");
        foreach(var arg in args)
        {
            configureArgs.Append(arg);
        }
        RunConfigure(context, nestEggSrcDir, nestEggBuildDir, configureArgs, "", new Dictionary<string, string>()
        {
            {"CC", "clang"},
            {"CFLAGS", $"-O2 -arch {targetArch}"},
            {"LDFLAGS", $"-arch {targetArch}"},
        });
        var makeArgs = new ProcessArgumentBuilder();
        RunMake(context, nestEggBuildDir, makeArgs, "");
    }

    private void BuildSDL2(BuildContext context, string targetArch)
    {
        var sdlSourceDir = "native/monogame/external/sdl2/sdl";
        var sdlBuildDir = System.IO.Path.Combine(sdlSourceDir, "build", targetArch);
        if (context.Environment.Platform.Family != PlatformFamily.Windows)
            sdlBuildDir = System.IO.Path.Combine(sdlSourceDir, "build");

        RecreateDirectory(context, sdlBuildDir);

        var configureArgs = new ProcessArgumentBuilder()
            .Append("-S").AppendQuoted(context.MakeAbsoluteForDocker(new DirectoryPath(sdlSourceDir)).FullPath)
            .Append("-B").AppendQuoted(context.MakeAbsoluteForDocker(new DirectoryPath(sdlBuildDir)).FullPath)
            .Append("-DSDL_STATIC=ON")
            .Append("-DSDL_TEST=OFF");

        AppendPlatformCMakeArgs(configureArgs, context, isSDL: true, targetArch);

        RunCMake(context, configureArgs, "SDL2 CMake configuration failed!");

        RunCMakeBuild(context, sdlBuildDir, "Release", "SDL2 build failed!");
    }

    private void BuildFAudio(BuildContext context, string targetArch)
    {
        var faudioSourceDir = "native/monogame/external/faudio";
        var faudioBuildDir = System.IO.Path.Combine(faudioSourceDir, "build", targetArch);
        if (context.Environment.Platform.Family != PlatformFamily.Windows)
            faudioBuildDir = System.IO.Path.Combine(faudioSourceDir, "build");

        RecreateDirectory(context, faudioBuildDir);

        var sdlIncludeDir = System.IO.Path.Combine("native/monogame/external/sdl2/sdl", "include");

        var configureArgs = new ProcessArgumentBuilder()
            .Append("-S").AppendQuoted(context.MakeAbsoluteForDocker(new DirectoryPath(faudioSourceDir)).FullPath)
            .Append("-B").AppendQuoted(context.MakeAbsoluteForDocker(new DirectoryPath(faudioBuildDir)).FullPath)
            .Append("-DBUILD_SHARED_LIBS=OFF")
            .Append($"-DCMAKE_C_STANDARD_INCLUDE_DIRECTORIES=\"{context.MakeAbsoluteForDocker(new DirectoryPath(sdlIncludeDir)).FullPath}\"")
            .Append($"-DCMAKE_CXX_STANDARD_INCLUDE_DIRECTORIES=\"{context.MakeAbsoluteForDocker(new DirectoryPath(sdlIncludeDir)).FullPath}\"")
            .Append("-DBUILD_SDL3=OFF");

        AppendPlatformCMakeArgs(configureArgs, context, isSDL: false, targetArch);

        RunCMake(context, configureArgs, "FAudio CMake configuration failed!");

        RunCMakeBuild(context, faudioBuildDir, "Release", "FAudio build failed!");
    }

    private void AppendPlatformCMakeArgs(ProcessArgumentBuilder args, BuildContext context, bool isSDL, string targetArch)
    {
        switch (context.Environment.Platform.Family)
        {
            case PlatformFamily.Windows:
                args.Append("-A").Append(targetArch == "arm64" ? "ARM64" : "x64");
                if (isSDL)
                {
                    args.Append("-DSDL_FORCE_STATIC_VCRT=ON");
                }
                else
                {
                    args.Append("-DCMAKE_C_FLAGS_DEBUG=\"/MTd /Zi /Ob0 /Od /RTC1\"");
                    args.Append("-DCMAKE_C_FLAGS_RELEASE=\"/MT /O2 /Ob2 /DNDEBUG\"");
                }
                break;

            case PlatformFamily.Linux:
                args.Append("-DCMAKE_POSITION_INDEPENDENT_CODE=ON");
                break;

            case PlatformFamily.OSX:
                args.Append("-DCMAKE_OSX_ARCHITECTURES=x86_64;arm64");
                args.Append("-DCMAKE_OSX_DEPLOYMENT_TARGET=10.15");
                break;
        }
    }

    private void RunMeson(BuildContext context, ProcessArgumentBuilder args, string errorMessage)
    {
        if (context.StartProcessWithDocker("meson", "", args) != 0)
        {
            throw new Exception(errorMessage);
        }
    }

    private void RunNinja(BuildContext context, ProcessArgumentBuilder args, string errorMessage)
    {
        if (context.StartProcessWithDocker("ninja", "", args) != 0)
        {
            throw new Exception(errorMessage);
        }
    }

    private void RunAutoReConf(BuildContext context, ProcessArgumentBuilder args, string errorMessage)
    {
        if (context.StartProcessWithDocker("autoreconf", "", args) != 0)
        {
            throw new Exception(errorMessage);
        }
    }

    private void RunConfigure(BuildContext context, string path, string workingDirectory, ProcessArgumentBuilder args, string errorMessage, Dictionary<string, string> environmentVariables)
    {
        var exe = System.IO.Path.Combine(path, "configure");
        if (context.StartProcessWithDocker(exe, workingDirectory, args, "", environmentVariables) != 0)
        {
            throw new Exception(errorMessage);
        }
    }

    private void RunMake(BuildContext context, string workingDirectory, ProcessArgumentBuilder args, string errorMessage)
    {
        if (context.StartProcessWithDocker("make", workingDirectory, args) != 0)
        {
            throw new Exception(errorMessage);
        }
    }

    private void RunCMake(BuildContext context, ProcessArgumentBuilder args, string errorMessage)
    {
        if (context.StartProcessWithDocker("cmake", "", args) != 0)
        {
            throw new Exception(errorMessage);
        }
    }

    private void RunLipo(BuildContext context, ProcessArgumentBuilder args, string errorMessage)
    {
        if (context.StartProcessWithDocker("lipo", "", args) != 0)
        {
            throw new Exception(errorMessage);
        }
    }

    private void RunCMakeBuild(BuildContext context, string buildDir, string config, string errorMessage)
    {
        var buildArgs = new ProcessArgumentBuilder()
            .Append("--build")
            .AppendQuoted(context.MakeAbsoluteForDocker(new DirectoryPath(buildDir)).FullPath)
            .Append("--config").Append(config)
            .Append("--parallel");

        RunCMake(context, buildArgs, errorMessage);
    }

    private void RecreateDirectory(BuildContext context, string dir)
    {
        if (context.DirectoryExists(dir))
        {
            context.DeleteDirectory(dir, new DeleteDirectorySettings { Recursive = true });
        }
        context.CreateDirectory(dir);
    }
}
