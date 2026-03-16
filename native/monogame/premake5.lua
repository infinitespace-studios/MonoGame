-- MonoGame - Copyright (C) MonoGame Foundation, Inc
-- This file is subject to the terms and conditions defined in
-- file 'LICENSE.txt', which is part of this source code package.

local vulkan_sdk = os.getenv("VULKAN_SDK")

if vulkan_sdk == nil and os.target() == "macosx" then
    error("Error: VULKAN_SDK environment variable is not set. Please set it to your Vulkan SDK installation path.")
end

newoption {
    trigger = "arch",
    value = "ARCH",
    description = "Target architecture (x64 or arm64)",
    default = "x64",
    allowed = {
        { "x64", "64-bit x86" },
        { "arm64", "64-bit ARM" }
    }
}
-- Android NDK configuration for Quest/Android ARM64 builds.
local android_ndk = os.getenv("ANDROID_NDK_HOME") or os.getenv("NDK_ROOT")

function ndk_host_tag()
    if os.host() == "macosx" then
        return "darwin-x86_64"
    elseif os.host() == "windows" then
        return "windows-x86_64"
    else
        return "linux-x86_64"
    end
end

function android_config()
    filter {"system:android"}
        architecture "ARM64"
        toolset "clang"
        if android_ndk then
            local sysroot = path.join(android_ndk, "toolchains/llvm/prebuilt/" .. ndk_host_tag() .. "/sysroot")
            buildoptions {
                "--target=aarch64-linux-android29",
                "--sysroot=" .. sysroot,
            }
            linkoptions {
                "--target=aarch64-linux-android29",
                "--sysroot=" .. sysroot,
            }
        end
        defines {"ANDROID", "__ANDROID__"}
    filter {}
end

function common(project_name)
    if os.target() == "windows" then
        filter "platforms:x64"
        architecture "x86_64"
        filter "platforms:arm64"
        architecture "ARM64"
        filter {}
        platform_target_path = "../../Artifacts/native/mgruntime/" .. project_name .. "/%{cfg.system}/%{cfg.platform}/%{cfg.buildcfg}"
    else
        local target_arch = _OPTIONS["arch"] or "x64"
        architecture(target_arch == "arm64" and "ARM64" or "x64")
        if os.target() == "macosx" then
            platform_target_path = "../../Artifacts/native/mgruntime/" .. project_name .. "/%{cfg.system}/%{cfg.buildcfg}"
        else
            platform_target_path = "../../Artifacts/native/mgruntime/" .. project_name .. "/%{cfg.system}/" .. target_arch .. "/%{cfg.buildcfg}"
        end
    end
    kind "SharedLib"
    language "C++"
    filter "system:linux"
    pic "On"
    filter {}
    defines {"DLL_EXPORT"}
    targetdir(platform_target_path)
    targetname "mgruntime"
    cppdialect "C++17"

    files {"include/**.h", "common/**.h", "common/**.cpp"}
    includedirs {"include", "../../external/stb"}
end

-- SDL is supported on all desktop platforms.
-- Links SDL2 libraries without including the SDL platform source files.
-- Used by OpenXR which needs SDL2 only as a FAudio dependency (not for windowing/input).
function sdl2_libs()
    includedirs {"external/sdl2/sdl/include"}

    filter {"system:windows"}
    links {"external/sdl2/sdl/build/%{cfg.platform}/Release/SDL2-static.lib", "winmm", "imm32", "user32", "gdi32", "advapi32",
           "setupapi", "ole32", "oleaut32", "version", "shell32"}
    filter {"system:macosx"}
    libdirs {"external/sdl2/sdl/build"}
    linkoptions {"-Wl,-force_load,external/sdl2/sdl/build/libSDL2.a"}
    links {"SDL2"}
    links {"Cocoa.framework", "IOKit.framework", "ForceFeedback.framework", "CoreAudio.framework",
        "AudioToolbox.framework", "CoreGraphics.framework", "CoreFoundation.framework", "Metal.framework",
        "CoreVideo.framework", "GameController.framework", "CoreHaptics.framework", "Carbon.framework", "iconv"}

    filter {"system:linux"}
    linkoptions {"external/sdl2/sdl/build/libSDL2.a"}
    links {"dl", "pthread", "m", "rt"}
    filter {}
end

function sdl2()
    defines {"MG_SDL2"}
    files {"sdl/**.h", "sdl/**.cpp"}
    sdl2_libs()
end

-- Vulkan is supported for all desktop platforms.
function vulkan()
    defines {"MG_VULKAN"}

    files {"vulkan/**.h", "vulkan/**.cpp"}

    includedirs {"external/vulkan-headers/include", "external/volk", "external/vma/include",
        path.join(vulkan_sdk, "include")}

    filter {"system:macosx"}
    libdirs {path.join(vulkan_sdk, "lib/MoltenVK.xcframework/macos-arm64_x86_64")}
    links {"MoltenVK", "IOSurface.framework", "Foundation.framework", "QuartzCore.framework", "AppKit.framework"}
    filter {}
end

-- Vulkan for OpenXR on macOS — use the Vulkan loader instead of linking MoltenVK directly.
-- This avoids duplicate ObjC MoltenVK classes when the XR runtime also embeds MoltenVK.
function vulkan_openxr()
    defines {"MG_VULKAN"}

    files {"vulkan/**.h", "vulkan/**.cpp"}

    includedirs {"external/vulkan-headers/include", "external/volk", "external/vma/include",
        path.join(vulkan_sdk, "include")}

    filter {"system:macosx"}
    libdirs {path.join(vulkan_sdk, "lib")}
    links {"vulkan", "IOSurface.framework", "Foundation.framework", "QuartzCore.framework", "AppKit.framework"}
    linkoptions {"-Wl,-rpath,@loader_path"}
    filter {}
end

-- Vulkan for Android — uses system Vulkan library (no volk, no SDK).
function vulkan_android()
    defines {"MG_VULKAN"}

    files {"vulkan/**.h", "vulkan/**.cpp"}

    includedirs {"external/vulkan-headers/include", "external/vma/include"}

    -- On Android, Vulkan is a system library — no volk, no SDK path needed
    filter {"system:android"}
        links {"vulkan"}
    filter {}
end

-- DirectX12 is supported on Xbox and Windows.
function directx12()
    defines {"MG_DIRECTX12"}

    files {"directx12/**.h", "directx12/**.cpp"}

    filter {"system:windows"}
    links {"dxguid", "dxgi", "d3d12"}
    filter {}
end

-- FAudio is supported for all desktop platforms.
function faudio()
    defines {"MG_FAUDIO"}

    files {"faudio/**.h", "faudio/**.cpp"}

    includedirs {"external/faudio/include"}
    
    filter {"system:windows"}
    libdirs {"external/faudio/build/%{cfg.platform}/Release"}
    links {"FAudio.lib"}
    
    filter {"system:macosx"}
    libdirs {"external/faudio/build"}
    linkoptions {
        "-Wl,-force_load,external/faudio/build/libFAudio.a",
        "-Wl,-ld_classic"
    }
    
    filter {"system:linux"}
    linkoptions {"external/faudio/build/libFAudio.a"}
    filter {}
end

-- FAudio for Android — expects prebuilt static library for ARM64.
-- Build FAudio for Android: cd external/faudio && mkdir -p build/android && cd build/android
--   && cmake ../.. -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake
--     -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-29 && make
function faudio_android()
    defines {"MG_FAUDIO"}

    files {"faudio/**.h", "faudio/**.cpp"}

    includedirs {"external/faudio/include"}

    filter {"system:android"}
        linkoptions {"external/faudio/build/android/libFAudio.a"}
        links {"OpenSLES"}
    filter {}
end

-- Xaudio is supported on Windows and Xbox.
function xaudio()
    defines {"MG_XAUDIO"}

    files {"xaudio/**.h", "xaudio/**.cpp"}
end

-- OpenXR replaces SDL for VR/XR platforms.
-- Builds on macOS as a compile check; ships on Windows and Android.
function openxr()
    defines {"MG_OPENXR"}

    files {"openxr/**.h", "openxr/**.cpp"}

    -- OpenXR loader built from source
    files {
        "external/openxr-sdk/src/loader/*.cpp",
        "external/openxr-sdk/src/loader/*.hpp",
        "external/openxr-sdk/src/common/object_info.cpp",
        "external/openxr-sdk/src/common/filesystem_utils.cpp",
        "external/openxr-sdk/src/xr_generated_dispatch_table_core.c",
        "external/openxr-sdk/src/xr_generated_dispatch_table.c",
        "external/openxr-sdk/src/external/jsoncpp/src/lib_json/*.cpp",
    }

    includedirs {
        "external/openxr-sdk/include",
        "external/openxr-sdk/src",
        "external/openxr-sdk/src/common",
        "external/openxr-sdk/src/loader",
        "external/openxr-sdk/src/external/jsoncpp/include",
        "openxr",  -- for common_config.h
    }

    defines {
        "OPENXR_HAVE_COMMON_CONFIG",
        "XRLOADER_DISABLE_EXCEPTION_HANDLING",
    }

    filter {"system:windows"}
        defines {"XR_OS_WINDOWS", "WIN32_LEAN_AND_MEAN", "NOMINMAX"}
        links {"advapi32"}
        -- OpenXR loader sources (platform_utils.hpp, loader_platform.hpp) need Win32 APIs.
        -- Force-include windows.h so loader code compiles without XR_USE_PLATFORM_WIN32
        -- (which would pull in D3D types we don't need).
        forceincludes {"windows.h"}
    filter {"system:macosx"}
        defines {"XR_OS_APPLE"}
        links {"dl"}
    filter {"system:linux"}
        defines {"XR_OS_LINUX"}
        links {"dl", "pthread", "m", "rt"}
    filter {"system:android"}
        defines {"XR_OS_ANDROID"}
        links {"log", "android"}
    filter {}
end

function configs()
    filter "configurations:Debug"
    defines {"DEBUG"}
    symbols "On"

    filter "configurations:Release"
    defines {"NDEBUG"}
    optimize "On"

    filter {"system:windows"}
    staticruntime "On"
    filter {"system:windows", "configurations:Debug"}
    runtime "Debug"
    filter {"system:windows", "configurations:Release"}
    runtime "Release"

    filter "system:macosx"
    buildoptions {"-arch x86_64", "-arch arm64"}
    linkoptions {"-arch x86_64", "-arch arm64"}
    filter {}
end

workspace "monogame"
configurations {"Debug", "Release"}
if os.target() == "windows" then
    platforms { "x64", "arm64" }
end

project "desktopvk"
common("desktopvk")
sdl2()
vulkan()
faudio()
configs()

if os.target() == "windows" then
    project "windowsdx"
    common("windowsdx")
    sdl2()
    directx12()
    xaudio()
    configs()
end

if os.target() == "windows" or os.target() == "macosx" or os.target() == "linux" then
    project "openxr"
        common("openxr")
        openxr()
        vulkan_openxr()
        faudio()
        sdl2_libs()  -- FAudio depends on SDL2 (audio/threading only, no platform source)
        configs()

        -- Copy libvulkan alongside the output dylib so @loader_path rpath resolves
        filter {"system:macosx"}
        postbuildcommands {
            "{COPYFILE} " .. path.join(vulkan_sdk, "lib", "libvulkan.1.dylib") .. " %{cfg.targetdir}/libvulkan.1.dylib"
        }
        filter {}
end

if os.target() == "android" then
    project "openxr_android"
        common("openxr")
        android_config()
        openxr()
        vulkan_android()
        faudio_android()
        configs()
end

if os.target() == "windows" then
    project "openxr_dx12"
        common("openxr_dx12")
        openxr()
        directx12()
        xaudio()
        configs()
end
