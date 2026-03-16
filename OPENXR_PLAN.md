# OpenXR Support for MonoGame — Detailed Implementation Plan

## Progress Summary

| Phase | Description | Status |
|-------|-------------|--------|
| Phase 1 | Native OpenXR Foundation (1.1-1.5) | ✅ DONE |
| Phase 2 | C# Managed Layer (2.1-2.5) | ✅ DONE |
| Phase 3 | Stereo Rendering (3.1-3.3) | ✅ DONE |
| Phase 4 | VR Input System (4.1-4.2) | ✅ DONE |
| Phase 5 | Build System & Distribution (5.1-5.2) | ✅ DONE |
| Phase 6 | Testing & Validation (6.1-6.3) | 🔄 IN PROGRESS — 6.1 done (34 unit tests), 6.2 done (Meta XR Simulator stereo rendering verified), 6.3 needs Quest hardware |
| Phase 7 | Quest 2 Android Support (7.1-7.7) | 🔄 IN PROGRESS — All tasks implemented, needs NDK build + Quest hardware testing |

**Branch:** `features/openxr`

### Key Architecture Decisions (for next session context)
- **Two-pass stereo** (not multiview) — avoids modifying HLSL shaders
- **C# is source of truth** for native API — run `MonoGame.Generator.CTypes` to regen headers
- **Merged GamePad** — both VR controllers → PlayerIndex.One (split gamepad)
- **Templates are in a git submodule** (`external/MonoGame.Templates/`) — commit separately
- **Build command**: `dotnet run --project build/Build.csproj`
- **Quick native build**: `cd native/monogame && premake5 gmake2 && make config=release openxr`

## Problem Statement

MonoGame currently has no VR/XR support. We need to implement OpenXR integration that:
- Uses the existing Vulkan graphics backend for rendering
- Introduces a new native OpenXR platform module (`MGXR`) that replaces SDL for windowing/input when targeting VR
- Adds a new `MonoGamePlatform.OpenXR` platform identifier
- Provides VR-specific input (6DOF controllers, head tracking, hand tracking)
- Supports stereo rendering via `VK_KHR_multiview`
- Keeps the existing MonoGame public API intact so most game code "just works" in VR

## Architecture Overview

MonoGame's native layer has a clean separation:
- **MGP** (Platform) — SDL-based windowing, input, events → `MGP_sdl.cpp`
- **MGG** (Graphics) — Vulkan or DX12 rendering → `MGG_Vulkan.cpp`
- **MGA** (Audio) — FAudio or XAudio → `MGA_faudio.cpp`

For OpenXR, we introduce:
- **MGXR** — New native module handling OpenXR session, swapchain, input, and reference spaces
- **MGP_openxr.cpp** — New platform implementation replacing `MGP_sdl.cpp` for VR (implements the same `MGP_*` API surface)
- Vulkan graphics (`MGG_Vulkan.cpp`) is modified to accept OpenXR-provided swapchain images instead of creating its own

### Key Design Decisions

1. **New platform, not a plugin**: OpenXR becomes `MonoGamePlatform.OpenXR` (value=13) with its own premake project `openxr`, just like `desktopvk` and `windowsdx`.
2. **Replace SDL, don't layer on top**: In VR mode, OpenXR owns the session, swapchain, and frame timing — SDL is not needed. `MGP_openxr.cpp` implements the full `api_MGP.h` interface.
3. **Extend MGG, don't fork it**: The Vulkan backend gains a new code path for OpenXR-provided swapchain images (via `#ifdef MG_OPENXR` blocks) rather than creating a separate graphics module.
4. **Separate `MonoGame.Framework.Native.OpenXR.csproj`**: A new project that `ProjectReference`s `MonoGame.Framework.Native.csproj` and contains only XR-specific C# code (P/Invoke interop declarations for `MGXR_*` functions + high-level XR types). This keeps XR code isolated from the base framework while avoiding duplication. The `XRDevice` static class P/Invokes directly into `mgruntime` to read XR state — no modifications to `NativeGamePlatform` needed for XR state queries. Games reference both packages: `MonoGame.Framework.Native` + `MonoGame.Framework.Native.OpenXR` + runtime NuGet packages. For Android/Quest, a similar `MonoGame.Framework.Android.OpenXR.csproj` depends on the base Android framework.
5. **New C# namespace `Microsoft.Xna.Framework.XR`**: High-level VR types (XRDevice, XRPose, etc.) live in the separate OpenXR assembly under this namespace.
6. **Multiview stereo rendering**: Uses `VK_KHR_multiview` with array textures (2 layers) for single-pass stereo. The `gl_ViewIndex`/`SV_ViewID` shader semantic selects the eye.
7. **Simple API**: The framework handles stereo rendering, head tracking, and frame timing automatically. Most existing MonoGame code "just works" in VR. Developers only need to set per-eye view/projection matrices and optionally read VR-specific input.

---

## Developer Experience — What Using This Looks Like

### Desktop VR Game (PC + SteamVR / Quest Link)

**Project file** — references base Native framework + separate OpenXR extension + runtime:
```xml
<!-- MyVRGame.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <MonoGamePlatform>OpenXR</MonoGamePlatform>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.Native" Version="3.8.5-preview.*" />
    <PackageReference Include="MonoGame.Framework.Native.OpenXR" Version="3.8.5-preview.*" />
    <PackageReference Include="MonoGame.Runtime.Windows.OpenXR" Version="3.8.5-preview.*" />
    <PackageReference Include="MonoGame.Runtime.Linux.OpenXR" Version="3.8.5-preview.*" />
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.5-preview.*" />
  </ItemGroup>
</Project>
```

### Quest 2 Standalone Game (Android APK)

**Project file** — similar to existing Android pattern but with OpenXR:
```xml
<!-- MyVRGame.Quest.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0-android</TargetFramework>
    <SupportedOSPlatformVersion>29</SupportedOSPlatformVersion>
    <MonoGamePlatform>OpenXR</MonoGamePlatform>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.Android.OpenXR" Version="3.8.5-preview.*" />
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.5-preview.*" />
  </ItemGroup>
</Project>
```

### Game Code — Simple VR Game

The API is designed so that **a basic VR game looks almost identical to a regular MonoGame game**:

```csharp
// Game1.cs — A simple VR game
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.XR;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Model _cube;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _cube = Content.Load<Model>("cube");
    }

    protected override void Update(GameTime gameTime)
    {
        // Standard GamePad API works — VR controllers map automatically
        // Left controller = PlayerIndex.One, Right = Two
        var leftHand = GamePad.GetState(PlayerIndex.One);
        var rightHand = GamePad.GetState(PlayerIndex.Two);
        
        if (rightHand.Buttons.A == ButtonState.Pressed)
            // A button on right controller
        
        // For VR-specific input (6DOF poses, hand tracking):
        if (XRDevice.IsAvailable)
        {
            var leftPose = XRDevice.GetControllerPose(XRHandedness.Left);
            var rightPose = XRDevice.GetControllerPose(XRHandedness.Right);
            var headPose = XRDevice.HeadPose;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Option 1: Simple — framework handles stereo automatically via multiview
        // Just draw your scene normally. The shader uses gl_ViewIndex to pick the
        // correct eye's view/projection matrix. This "just works" for most games.
        foreach (var mesh in _cube.Meshes)
        {
            foreach (BasicEffect effect in mesh.Effects)
            {
                // XRDevice provides per-eye matrices; multiview shader handles stereo
                effect.World = Matrix.CreateTranslation(0, 1.5f, -2f);
                effect.View = XRDevice.GetViewMatrix(0); // Left eye (multiview uses both)
                effect.Projection = XRDevice.GetProjectionMatrix(0, 0.1f, 100f);
            }
            mesh.Draw();
        }

        // Option 2: Advanced — render each eye manually (for custom effects)
        // for (int eye = 0; eye < XRDevice.ViewCount; eye++)
        // {
        //     var view = XRDevice.GetViewMatrix(eye);
        //     var proj = XRDevice.GetProjectionMatrix(eye, 0.1f, 100f);
        //     DrawScene(view, proj);
        // }

        base.Draw(gameTime);
    }
}
```

### Program.cs — Entry Point

**Desktop (identical to DesktopVK pattern):**
```csharp
using var game = new Game1();
game.Run();
```

**Quest / Android:**
```csharp
// Activity1.cs
using Android.App;
using Microsoft.Xna.Framework;

[Activity(MainLauncher = true)]
public class Activity1 : OpenXRGameActivity
{
    protected override Game CreateGame()
    {
        return new Game1();
    }
}
```

### Key API Surface

The developer-facing XR API is intentionally minimal:

```csharp
namespace Microsoft.Xna.Framework.XR;

// Static access point — always available, no-ops on non-XR platforms
public static class XRDevice
{
    public static bool IsAvailable { get; }              // Is an HMD connected?
    public static XRSessionState SessionState { get; }   // Current session state
    public static int ViewCount { get; }                  // Usually 2 (stereo)
    public static XRPose HeadPose { get; }                // Current head position/rotation
    
    // Per-eye matrices (the most common thing developers need)
    public static Matrix GetViewMatrix(int eye);
    public static Matrix GetProjectionMatrix(int eye, float nearPlane, float farPlane);
    
    // Controller poses (6DOF)
    public static XRPose GetControllerPose(XRHandedness hand);
    public static XRPose GetControllerAimPose(XRHandedness hand);
    
    // Haptics
    public static void SetControllerVibration(XRHandedness hand, float amplitude, float duration);
    
    // Reference spaces (advanced)
    public static void SetTrackingSpace(XRTrackingSpace space);  // Seated, Standing, RoomScale
}

public struct XRPose
{
    public Vector3 Position;
    public Quaternion Orientation;
    public Matrix ToMatrix();
}

public enum XRHandedness { Left, Right }
public enum XRTrackingSpace { Seated, Standing, RoomScale }
public enum XRSessionState { Unknown, Idle, Ready, Synchronized, Visible, Focused, Stopping, Exiting }
```

---

## Work Breakdown

### Phase 1: Foundation — Native OpenXR Module (MGXR)

#### Task 1.1: Add OpenXR SDK as External Dependency
**Files to create/modify:**
- `native/monogame/external/openxr/` — Add OpenXR-SDK headers and loader
- `native/monogame/premake5.lua` — Add `openxr()` function block

**Details:**
- Download OpenXR-SDK (headers + loader) into `native/monogame/external/openxr/`
- The loader (`openxr_loader`) is a dynamic library that discovers the active OpenXR runtime
- Add premake function:
  ```lua
  function openxr()
      defines {"MG_OPENXR"}
      files {"openxr/**.h", "openxr/**.cpp"}
      includedirs {"external/openxr/include"}
      filter {"system:windows"}
          links {"openxr_loader"}
      filter {"system:linux"}
          links {"openxr_loader"}
      filter {}
  end
  ```
- Add new premake project:
  ```lua
  project "openxr"
      common("openxr")
      openxr()
      vulkan()
      faudio()
      configs()
  end
  ```
- Note: No SDL dependency — OpenXR replaces it entirely

**Acceptance criteria:**
- `premake5 vs2022` (or gmake2) generates a build for the `openxr` project
- The project compiles with OpenXR headers available

---

#### Task 1.2: Create MGXR Native API Header (`api_MGXR.h`)
**Files to create:**
- `native/monogame/include/api_MGXR.h`

**Details:**
Define the C API for the OpenXR subsystem:

```cpp
#pragma once
#include "api_common.h"
#include "api_enums.h"
#include "api_structs.h"

struct MGXR_System;
struct MGXR_Session;
struct MGXR_Swapchain;
struct MGXR_Space;
struct MGXR_ActionSet;
struct MGXR_Action;

// System lifecycle
MG_EXPORT MGXR_System* MGXR_System_Create();
MG_EXPORT void MGXR_System_Destroy(MGXR_System* system);
MG_EXPORT mgbyte MGXR_System_IsHmdPresent(MGXR_System* system);

// Session lifecycle
MG_EXPORT MGXR_Session* MGXR_Session_Create(MGXR_System* system, void* vkInstance, void* vkPhysicalDevice, void* vkDevice, mguint queueFamilyIndex, mguint queueIndex);
MG_EXPORT void MGXR_Session_Destroy(MGXR_Session* session);
MG_EXPORT mgint MGXR_Session_GetState(MGXR_Session* session);
MG_EXPORT mgbyte MGXR_Session_BeginFrame(MGXR_Session* session, mglong* predictedDisplayTime, mglong* predictedDisplayPeriod);
MG_EXPORT void MGXR_Session_EndFrame(MGXR_Session* session, mglong displayTime);
MG_EXPORT void MGXR_Session_RequestExit(MGXR_Session* session);

// Swapchain (OpenXR-managed, provides Vulkan images)
MG_EXPORT MGXR_Swapchain* MGXR_Swapchain_Create(MGXR_Session* session, mgint width, mgint height, mgint sampleCount, mgint arraySize);
MG_EXPORT void MGXR_Swapchain_Destroy(MGXR_Swapchain* swapchain);
MG_EXPORT mgint MGXR_Swapchain_AcquireImage(MGXR_Swapchain* swapchain);
MG_EXPORT void MGXR_Swapchain_WaitImage(MGXR_Swapchain* swapchain, mglong timeout);
MG_EXPORT void MGXR_Swapchain_ReleaseImage(MGXR_Swapchain* swapchain);
MG_EXPORT void MGXR_Swapchain_GetVulkanImage(MGXR_Swapchain* swapchain, mgint index, void** vkImage, mgint* width, mgint* height);
MG_EXPORT void MGXR_Swapchain_GetRecommendedSize(MGXR_Session* session, mgint* width, mgint* height);

// View/Projection (per-eye data)
MG_EXPORT mgint MGXR_Session_GetViewCount(MGXR_Session* session);
MG_EXPORT void MGXR_Session_GetViewProjection(MGXR_Session* session, mgint viewIndex, mglong displayTime, MGXR_ViewProjection* view);

// Reference Spaces
MG_EXPORT MGXR_Space* MGXR_Space_Create(MGXR_Session* session, MGXRReferenceSpaceType type);
MG_EXPORT void MGXR_Space_Destroy(MGXR_Space* space);
MG_EXPORT void MGXR_Space_GetPose(MGXR_Space* space, MGXR_Space* baseSpace, mglong time, MGXR_Pose* pose);

// Action System (VR Input)
MG_EXPORT MGXR_ActionSet* MGXR_ActionSet_Create(MGXR_Session* session, const char* name, const char* localizedName);
MG_EXPORT void MGXR_ActionSet_Destroy(MGXR_ActionSet* actionSet);
MG_EXPORT MGXR_Action* MGXR_Action_Create(MGXR_ActionSet* actionSet, const char* name, const char* localizedName, MGXRActionType type);
MG_EXPORT void MGXR_Action_Destroy(MGXR_Action* action);
MG_EXPORT void MGXR_Action_SuggestBindings(MGXR_System* system, const char* interactionProfile, MGXR_Action** actions, const char** paths, mgint count);
MG_EXPORT void MGXR_ActionSet_Sync(MGXR_Session* session, MGXR_ActionSet** sets, mgint count);
MG_EXPORT void MGXR_Action_GetStateBool(MGXR_Action* action, mgbyte* value, mgbyte* changed);
MG_EXPORT void MGXR_Action_GetStateFloat(MGXR_Action* action, mgfloat* value, mgbyte* changed);
MG_EXPORT void MGXR_Action_GetStatePose(MGXR_Action* action, MGXR_Space* space, mglong time, MGXR_Pose* pose);
MG_EXPORT void MGXR_Action_ApplyHaptic(MGXR_Action* action, mgfloat amplitude, mgfloat frequency, mglong duration);
```

**New structs to add to `api_structs.h`:**
```cpp
struct MGXR_Pose {
    Vector3 Position;     // x, y, z
    Vector4 Orientation;  // quaternion x, y, z, w
};

struct MGXR_ViewProjection {
    MGXR_Pose pose;
    mgfloat fovAngleLeft;
    mgfloat fovAngleRight;
    mgfloat fovAngleUp;
    mgfloat fovAngleDown;
};
```

**New enums to add to `api_enums.h`:**
```cpp
enum class MGXRReferenceSpaceType : mgint {
    View = 0,   // Head-locked
    Local = 1,  // Seated/standing origin
    Stage = 2,  // Room-scale floor
};

enum class MGXRActionType : mgint {
    Boolean = 0,
    Float = 1,
    Vector2 = 2,
    Pose = 3,
    Haptic = 4,
};

enum class MGXRSessionState : mgint {
    Unknown = 0,
    Idle = 1,
    Ready = 2,
    Synchronized = 3,
    Visible = 4,
    Focused = 5,
    Stopping = 6,
    LossPending = 7,
    Exiting = 8,
};
```

**Add to `MGMonoGamePlatform` enum:**
```cpp
OpenXR = 13,
```

**Acceptance criteria:**
- Header compiles cleanly with existing includes
- All opaque structs forward-declared
- Enums and structs match what C# interop will expect

---

#### Task 1.3: Implement `MGXR_openxr.cpp` — Core OpenXR Native Module
**Files to create:**
- `native/monogame/openxr/MGXR_openxr.cpp`

**Details:**
This is the largest single piece of work. Implement all `MGXR_*` functions:

**MGXR_System_Create:**
- Call `xrCreateInstance` with extensions: `XR_KHR_vulkan_enable2`, `XR_EXT_debug_utils` (debug)
- Call `xrGetSystem` with `XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY`
- Query `xrGetVulkanGraphicsRequirements2KHR` to get required Vulkan version/extensions

**MGXR_Session_Create:**
- Populate `XrGraphicsBindingVulkanKHR` with the provided Vulkan handles
- Call `xrCreateSession`
- Create the initial reference spaces (VIEW, LOCAL, STAGE)
- Begin session state machine processing

**MGXR_Session_BeginFrame / EndFrame:**
- `xrWaitFrame` → returns `predictedDisplayTime`
- `xrBeginFrame`
- (Game renders)
- `xrEndFrame` with composition layers (`XrCompositionLayerProjection` containing per-eye sub-images)

**MGXR_Swapchain_*:**
- `xrEnumerateViewConfigurationViews` to get recommended resolution
- `xrCreateSwapchain` with `arraySize=2` for stereo multiview
- `xrEnumerateSwapchainImages` to get `VkImage` handles
- Acquire/Wait/Release follow OpenXR spec lifecycle

**MGXR_Space_*:**
- `xrCreateReferenceSpace` for VIEW/LOCAL/STAGE
- `xrLocateSpace` to get 6DOF pose at a given time

**MGXR_ActionSet/Action_*:**
- Standard OpenXR action system implementation
- Default interaction profiles: `/interaction_profiles/khr/simple_controller`, `/interaction_profiles/oculus/touch_controller`, `/interaction_profiles/valve/index_controller`

**Acceptance criteria:**
- Can create an OpenXR instance, discover an HMD, create a session
- Can create swapchains and retrieve Vulkan images
- Can track head pose and controller poses
- Can poll button/trigger/stick inputs
- Handles session state transitions (IDLE → READY → FOCUSED → STOPPING → EXITING)

---

#### Task 1.4: Implement `MGP_openxr.cpp` — OpenXR Platform Layer (Replaces SDL)
**Files to create:**
- `native/monogame/openxr/MGP_openxr.cpp`

**Details:**
Implement the full `api_MGP.h` interface without SDL. In VR, there is no traditional window, but MonoGame expects one:

**MGP_Platform_Create:**
- Initialize OpenXR system (`MGXR_System_Create`)
- Set `behavior = MGGameRunBehavior::Synchronous`
- Return platform handle

**MGP_Window_Create:**
- Create an `MGXR_Session` using the Vulkan device (obtained later, see coordination with MGG)
- The "window" is a virtual construct — `width`/`height` come from `xrEnumerateViewConfigurationViews`
- Store the recommended eye resolution

**MGP_Window_GetNativeHandle:**
- Returns a sentinel value or the XR session handle — no OS window exists
- The graphics device uses this to detect OpenXR mode

**MGP_Platform_PollEvent:**
- Call `xrPollEvent` to handle session state changes
- Map XR session events to MGP events:
  - `XR_SESSION_STATE_FOCUSED` → `WindowGainedFocus`
  - `XR_SESSION_STATE_VISIBLE` → `WindowLostFocus` (no input)
  - `XR_SESSION_STATE_STOPPING` → `Quit`
- Sync action sets and generate controller events:
  - Map VR controller buttons → `ControllerAdded/Removed/StateChange`
  - Map thumbstick/trigger → analog controller inputs

**MGP_Platform_BeforeUpdate / BeforeDraw:**
- `BeforeDraw`: Call `MGXR_Session_BeginFrame` — returns false if frame should be skipped (session not visible)
- Manage frame timing (OpenXR `xrWaitFrame` provides target timing)

**MGP_Window_EnterFullScreen / ExitFullScreen / SetClientSize / etc.:**
- No-ops in VR mode (VR is always "fullscreen" to the headset)

**MGP_GamePad_*:**
- Map OpenXR controllers to GamePad API:
  - Left controller → PlayerIndex.One
  - Right controller → PlayerIndex.Two
  - Standard buttons (A/B/X/Y, triggers, thumbsticks) map directly
  - Haptic feedback via `MGXR_Action_ApplyHaptic`

**Acceptance criteria:**
- `MGP_Platform_Create` succeeds and OpenXR is initialized
- `MGP_Platform_PollEvent` returns proper events from XR runtime
- GamePad API returns VR controller state
- Frame timing is driven by OpenXR's `xrWaitFrame`

---

#### Task 1.5: Modify `MGG_Vulkan.cpp` — OpenXR Swapchain Integration
**Files to modify:**
- `native/monogame/vulkan/MGG_Vulkan.cpp`

**Details:**
The Vulkan backend currently creates its own swapchain via SDL. For OpenXR, swapchain images come from the XR runtime.

**Key changes:**

1. **Instance Creation (`MGG_GraphicsSystem_Create`):**
   - Under `#ifdef MG_OPENXR`: Query required Vulkan instance extensions from OpenXR via `xrGetVulkanInstanceExtensionsKHR`
   - Add these extensions to `VkInstanceCreateInfo`
   - Skip SDL Vulkan extensions

2. **Device Creation:**
   - Under `#ifdef MG_OPENXR`: Query required device extensions from `xrGetVulkanDeviceExtensionsKHR`
   - Enable `VK_KHR_multiview` extension
   - Use `xrGetVulkanGraphicsDeviceKHR` to select the physical device the runtime requires

3. **`MGVK_RecreateSwapChain` modification:**
   - Under `#ifdef MG_OPENXR`: Instead of creating a `VkSwapchainKHR` via `vkCreateSwapchainKHR`:
     - Get `VkImage` handles from MGXR module (`MGXR_Swapchain_GetVulkanImage`)
     - Create `VkImageView` for each image (as `VK_IMAGE_VIEW_TYPE_2D_ARRAY` with 2 layers)
     - Create framebuffers using multiview-enabled render passes
   - The existing SDL path remains under `#ifdef MG_SDL2`

4. **Render Pass Modification:**
   - Under `#ifdef MG_OPENXR`: When creating render passes for the swapchain target, add `VkRenderPassMultiviewCreateInfo`:
     ```cpp
     uint32_t viewMask = 0b11;  // Both eyes
     uint32_t correlationMask = 0b11;
     VkRenderPassMultiviewCreateInfo multiviewInfo = {};
     multiviewInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_MULTIVIEW_CREATE_INFO;
     multiviewInfo.subpassCount = 1;
     multiviewInfo.pViewMasks = &viewMask;
     multiviewInfo.correlationMaskCount = 1;
     multiviewInfo.pCorrelationMasks = &correlationMask;
     ```

5. **Frame Lifecycle:**
   - `MGG_GraphicsDevice_BeginFrame`: Under `MG_OPENXR`, acquire OpenXR swapchain image
   - `MGG_GraphicsDevice_Present`: Under `MG_OPENXR`, release OpenXR swapchain image (no `vkQueuePresentKHR`)

**Acceptance criteria:**
- Vulkan device uses OpenXR-required physical device and extensions
- Render passes use multiview for stereo rendering
- Swapchain images come from OpenXR, not `vkCreateSwapchainKHR`
- Frame acquire/release follows OpenXR lifecycle

---

### Phase 2: C# Managed Layer

#### Task 2.1: Add `MonoGamePlatform.OpenXR` and Related Enums
**Files to modify:**
- `MonoGame.Framework/Utilities/MonoGamePlatform.cs` — Add `OpenXR = 13` (must match C++ enum value)
- `MonoGame.Framework/Utilities/PlatformInfo.cs` — Handle `OpenXR` in switch
- `native/monogame/include/api_enums.h` — Already done in Task 1.2

**Acceptance criteria:**
- `PlatformInfo.MonoGamePlatform` returns `OpenXR` when running on the OpenXR platform

---

#### Task 2.2: Create C# OpenXR Interop Layer (`XR.Interop.cs`)
**Files to create:**
- `MonoGame.Framework/XR/Interop/XR.Interop.cs` — Lives in the XR directory (will be compiled by the separate OpenXR .csproj, not by Native.csproj)

**Details:**
P/Invoke declarations for all `MGXR_*` functions, mirroring the pattern in `Platform.Interop.cs` and `Graphics.Interop.cs`:

```csharp
namespace MonoGame.Interop;

[MGHandle] internal readonly struct MGXR_System { }
[MGHandle] internal readonly struct MGXR_Session { }
[MGHandle] internal readonly struct MGXR_Swapchain { }
[MGHandle] internal readonly struct MGXR_Space { }
[MGHandle] internal readonly struct MGXR_ActionSet { }
[MGHandle] internal readonly struct MGXR_Action { }

[StructLayout(LayoutKind.Sequential)]
internal struct MGXR_Pose {
    public Vector3 Position;
    public Quaternion Orientation;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MGXR_ViewProjection {
    public MGXR_Pose Pose;
    public float FovAngleLeft;
    public float FovAngleRight;
    public float FovAngleUp;
    public float FovAngleDown;
}

internal static unsafe partial class MGXR
{
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_System_Create")]
    public static extern MGXR_System* System_Create();
    
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_Create")]
    public static extern MGXR_Session* Session_Create(
        MGXR_System* system, nint vkInstance, nint vkPhysicalDevice,
        nint vkDevice, uint queueFamilyIndex, uint queueIndex);
    
    // ... all other P/Invoke declarations matching api_MGXR.h ...
}
```

**Acceptance criteria:**
- All MGXR native functions have matching P/Invoke declarations
- Struct layouts match native side (verified by size)
- Handle types defined with `[MGHandle]` attribute

---

#### Task 2.3: Create High-Level C# XR Types (`Microsoft.Xna.Framework.XR` namespace)
**Files to create:**
- `MonoGame.Framework/XR/XRDevice.cs` — Static access point for all XR state
- `MonoGame.Framework/XR/XRPose.cs` — 6DOF pose struct
- `MonoGame.Framework/XR/XRHandedness.cs` — Left/Right enum
- `MonoGame.Framework/XR/XRSessionState.cs` — Session state enum
- `MonoGame.Framework/XR/XRTrackingSpace.cs` — Seated/Standing/RoomScale enum

**Key class design — intentionally simple, static API:**

```csharp
namespace Microsoft.Xna.Framework.XR;

/// <summary>
/// Static access point for all VR/XR functionality.
/// Safe to call on non-XR platforms (returns defaults/false).
/// The framework manages the OpenXR session lifecycle internally —
/// developers do NOT create/destroy sessions manually.
/// </summary>
public static class XRDevice
{
    // --- State ---
    public static bool IsAvailable { get; }                // Is an HMD connected and session active?
    public static XRSessionState SessionState { get; }     // Current OpenXR session state
    public static int ViewCount { get; }                    // Usually 2 (stereo)
    
    // --- Head Tracking ---
    public static XRPose HeadPose { get; }                  // Current head position/rotation
    
    // --- Per-Eye Matrices (most common developer need) ---
    public static Matrix GetViewMatrix(int eye);
    public static Matrix GetProjectionMatrix(int eye, float nearPlane, float farPlane);
    
    // --- Controller Poses (6DOF) ---
    public static XRPose GetControllerPose(XRHandedness hand);      // Grip pose
    public static XRPose GetControllerAimPose(XRHandedness hand);   // Aim/pointer pose
    
    // --- Haptics ---
    public static void SetControllerVibration(XRHandedness hand, float amplitude, float durationSeconds);
    
    // --- Tracking Space ---
    public static void SetTrackingSpace(XRTrackingSpace space);     // Seated, Standing, RoomScale
}

/// <summary>6DOF pose (position + orientation).</summary>
public struct XRPose
{
    public Vector3 Position;
    public Quaternion Orientation;
    
    public Matrix ToMatrix();
    public static XRPose Identity => new() { Orientation = Quaternion.Identity };
}

public enum XRHandedness { Left, Right }
public enum XRTrackingSpace { Seated, Standing, RoomScale }
public enum XRSessionState { Unknown, Idle, Ready, Synchronized, Visible, Focused, Stopping, Exiting }
```

**Design rationale:**
- `XRDevice` is **static** — no object creation needed. The framework manages the OpenXR session internally via `NativeGamePlatform`.
- On non-XR platforms, `IsAvailable` returns `false` and all methods return safe defaults — game code doesn't need `#if OPENXR` guards.
- Developers only need `GetViewMatrix()` / `GetProjectionMatrix()` for most games.
- VR controllers automatically map to GamePad API, so `XRDevice.GetControllerPose()` is only needed for 6DOF spatial input.

**Acceptance criteria:**
- Clean static API that wraps native interop internally
- Safe to call on any platform (returns defaults when XR not available)
- View/projection matrices correctly computed from OpenXR fov angles

---

#### Task 2.4: Modify `NativeGamePlatform` for OpenXR Support
**Files to modify:**
- `MonoGame.Framework/Platform/Native/GamePlatform.Native.cs`

**Details:**
The `NativeGamePlatform` already uses the `MGP_*` C API. Since `MGP_openxr.cpp` implements the same interface, most code works as-is. Changes needed:

1. **XR Session creation coordination:**
   - After `MGG_GraphicsSystem_Create` and `MGG_GraphicsDevice_Create`, if platform is OpenXR, pass Vulkan handles to the native layer for OpenXR session creation
   - The session must be created after the Vulkan device but before the first frame

2. **Frame loop modification:**
   - `BeforeDraw`: The native `MGP_Platform_BeforeDraw` already handles `xrWaitFrame`/`xrBeginFrame`
   - `Present`: Need to call `xrEndFrame` after `vkQueueSubmit` but instead of `vkQueuePresentKHR`

3. **Update `XRDevice` state each frame:**
   - After `xrWaitFrame`, query view poses and populate `XRDevice.HeadPose`, per-eye matrices, controller poses
   - This happens automatically — the developer just reads `XRDevice.*` properties

**Acceptance criteria:**
- Game loop runs correctly on OpenXR with proper frame timing
- Session state transitions are handled gracefully
- `XRDevice` properties are updated each frame before `Update()` is called

---

#### Task 2.5: Create `MonoGame.Framework.Native.OpenXR.csproj` and Runtime NuGet Packages
**Files to create:**
- `MonoGame.Framework/MonoGame.Framework.Native.OpenXR.csproj` — Separate XR extension project
- `src/NuGetPackages/MonoGame.Runtime.Windows.OpenXR/` — Windows OpenXR runtime NuGet
- `src/NuGetPackages/MonoGame.Runtime.Linux.OpenXR/` — Linux OpenXR runtime NuGet

**Details:**
XR code lives in a **separate project** that depends on the base Native framework. This keeps XR P/Invoke code and types out of the base `MonoGame.Framework.Native` package while avoiding duplication.

1. **Create `MonoGame.Framework.Native.OpenXR.csproj`:**
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>netstandard2.1</TargetFramework>
       <LangVersion>12.0</LangVersion>
       <DefineConstants>XNADESIGNPROVIDED;NATIVE;OPENXR;NETSTANDARD</DefineConstants>
       <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
       <Description>MonoGame OpenXR extension for VR/AR headsets.</Description>
       <PackageId>MonoGame.Framework.Native.OpenXR</PackageId>
     </PropertyGroup>

     <ItemGroup>
       <!-- Depend on base Native framework -->
       <ProjectReference Include="MonoGame.Framework.Native.csproj" />
     </ItemGroup>

     <ItemGroup>
       <!-- XR-specific C# code only -->
       <Compile Include="XR\**\*.cs" />
     </ItemGroup>
   </Project>
   ```

   The XR directory structure:
   ```
   MonoGame.Framework/XR/
   ├── Interop/
   │   └── XR.Interop.cs          ← P/Invoke declarations (MGXR_* functions)
   ├── XRDevice.cs                 ← Static API entry point
   ├── XRPose.cs                   ← 6DOF pose struct
   ├── XRHandedness.cs             ← Left/Right enum
   ├── XRSessionState.cs           ← Session state enum
   └── XRTrackingSpace.cs          ← Seated/Standing/RoomScale enum
   ```

2. **Create runtime NuGet packages** (same pattern as `MonoGame.Runtime.Windows.Vulkan`):
   ```xml
   <!-- src/NuGetPackages/MonoGame.Runtime.Windows.OpenXR/MonoGame.Runtime.Windows.OpenXR.csproj -->
   <Content Include="..\..\..\Artifacts\native\mgruntime\openxr\windows\Release\mgruntime.dll">
     <PackagePath>runtimes\win-x64\native</PackagePath>
   </Content>
   ```

3. **Game projects reference three packages:**
   ```xml
   <PackageReference Include="MonoGame.Framework.Native" Version="3.8.5-preview.*" />
   <PackageReference Include="MonoGame.Framework.Native.OpenXR" Version="3.8.5-preview.*" />
   <PackageReference Include="MonoGame.Runtime.Windows.OpenXR" Version="3.8.5-preview.*" />
   ```

**Why separate project (not adding to Native.csproj):**
- XR P/Invoke declarations and types don't pollute the base framework
- Games that don't use XR don't carry XR code
- Clean NuGet dependency: `MonoGame.Framework.Native.OpenXR` depends on `MonoGame.Framework.Native`
- `XRDevice` queries native state directly via P/Invoke — no circular dependency back to `NativeGamePlatform`

**Acceptance criteria:**
- `MonoGame.Framework.Native.OpenXR.csproj` builds targeting `netstandard2.1`
- Has a `ProjectReference` to `MonoGame.Framework.Native.csproj`
- Contains only XR-specific code (interop + types)
- Runtime NuGet packages contain the OpenXR-variant native library

---

### Phase 3: Graphics Pipeline Modifications (C# Side)

#### Task 3.1: Modify `GraphicsDevice` for Stereo Rendering
**Files to modify:**
- `MonoGame.Framework/Platform/Native/GraphicsDevice.Native.cs`
- `MonoGame.Framework/Graphics/GraphicsDevice.cs`

**Details:**
1. **Detect OpenXR mode:**
   - Check `PlatformInfo.MonoGamePlatform == MonoGamePlatform.OpenXR`
   - Store `IsXRActive` flag on GraphicsDevice

2. **Swapchain resize path:**
   - In OpenXR mode, `PlatformInitialize` / `OnPresentationChanged` use OpenXR swapchain dimensions
   - The native `MGG_GraphicsDevice_ResizeSwapchain` is called with OpenXR-provided images

3. **Per-eye rendering helper:**
   ```csharp
   // Internal: called by the framework each frame
   internal void SetXRViewIndex(int viewIndex)
   {
       // Push view index to shader constant (for gl_ViewIndex)
       // This is handled automatically by multiview, but
       // we also update the view/projection matrices
   }
   ```

**Acceptance criteria:**
- GraphicsDevice correctly uses OpenXR swapchain resolution
- Multiview render passes are active in XR mode
- Backbuffer dimensions match recommended per-eye resolution

---

#### Task 3.2: Shader Pipeline — Multiview Support
**Files to modify:**
- `MonoGame.Framework.Content.Pipeline/` — Effect compilation
- `native/monogame/vulkan/MGG_Vulkan.cpp` — Shader module creation

**Details:**
1. **SPIR-V shaders need multiview:**
   - Built-in effects (SpriteEffect, BasicEffect, etc.) need SPIR-V variants that use `gl_ViewIndex`
   - The vertex shader multiplexes view/projection matrices based on `gl_ViewIndex`:
     ```glsl
     layout(set=0, binding=0) uniform UBO {
         mat4 viewProjection[2]; // One per eye
     };
     void main() {
         gl_Position = viewProjection[gl_ViewIndex] * modelMatrix * vec4(position, 1.0);
     }
     ```

2. **Effect compilation toolchain:**
   - The content pipeline compiles `.fx` files to SPIR-V (for Vulkan)
   - Need to add a multiview variant or make the existing shaders multiview-aware
   - Use a `#define XR_MULTIVIEW` preprocessor flag during compilation

3. **Constant buffer layout:**
   - When in XR mode, the `ViewProjection` constant buffer contains an array of 2 matrices
   - The `BasicEffect` and other built-in effects need a code path that sets both matrices

**Acceptance criteria:**
- Built-in effects compile to multiview-capable SPIR-V
- `gl_ViewIndex` is used to select the correct view/projection
- Non-XR mode is unaffected (single view, no array)

---

#### Task 3.3: Modify Built-in Effects for Stereo
**Files to modify:**
- Effect source files for SpriteEffect, BasicEffect, DualTextureEffect, EnvironmentMapEffect, SkinnedEffect, AlphaTestEffect
- These are compiled to `.vk.mgfxo.h` header files embedded in `MGG_Vulkan.cpp`

**Details:**
- Each effect's vertex shader needs a stereo code path
- When `XR_MULTIVIEW` is defined, use `gl_ViewIndex` to index into a `mat4[2]` array
- The projection/view matrices are set per-eye by the C# layer before drawing
- Generate new `.vk.mgfxo.h` files with multiview support

**Acceptance criteria:**
- All 6 built-in effects render correctly in stereo
- Left/right eye views use correct per-eye view/projection matrices
- No visual artifacts from incorrect matrix selection

---

### Phase 4: VR Input System

#### Task 4.1: Implement OpenXR Action Bindings (Native)
**Files:** Already covered in Task 1.3 (`MGXR_openxr.cpp`)

**Details — Default Action Mappings:**
```
ActionSet: "gameplay"
  - hand_pose (Pose, both hands) → /user/hand/left/input/grip/pose, /user/hand/right/input/grip/pose
  - aim_pose (Pose, both hands) → /user/hand/left/input/aim/pose, /user/hand/right/input/aim/pose
  - trigger (Float, both hands) → /user/hand/left/input/trigger/value, /user/hand/right/input/trigger/value
  - grip (Float, both hands) → /user/hand/left/input/squeeze/value, /user/hand/right/input/squeeze/value
  - thumbstick (Vector2, both hands) → /user/hand/left/input/thumbstick, /user/hand/right/input/thumbstick
  - button_a (Boolean) → /user/hand/right/input/a/click
  - button_b (Boolean) → /user/hand/right/input/b/click
  - button_x (Boolean) → /user/hand/left/input/x/click
  - button_y (Boolean) → /user/hand/left/input/y/click
  - menu (Boolean) → /user/hand/left/input/menu/click
  - haptic (Haptic, both hands) → /user/hand/left/output/haptic, /user/hand/right/output/haptic
```

**Interaction Profiles:**
- `/interaction_profiles/khr/simple_controller` (minimum viable)
- `/interaction_profiles/oculus/touch_controller` (Meta Quest)
- `/interaction_profiles/valve/index_controller` (Valve Index)
- `/interaction_profiles/htc/vive_controller` (HTC Vive)
- `/interaction_profiles/microsoft/motion_controller` (WMR)

**Acceptance criteria:**
- Controller input works on at least 2 different headsets
- Default bindings cover common VR interactions
- Users can create custom action sets/bindings via the C# API

---

#### Task 4.2: Map VR Controllers to GamePad API (Backward Compatibility)
**Files to modify:**
- `native/monogame/openxr/MGP_openxr.cpp` (controller event mapping)
- `MonoGame.Framework/Platform/Native/GamePad.Native.cs` (if needed)

**Details:**
Make existing MonoGame games partially work in VR by mapping VR controllers to the GamePad API:

```
Left Controller → GamePad Index 0:
  - Thumbstick → LeftStick
  - Trigger → LeftTrigger
  - Grip → LeftShoulder (pressed if > 0.5)
  - X → X button
  - Y → Y button
  - Menu → Start button
  - Thumbstick click → LeftStick button

Right Controller → GamePad Index 1 (or merged into Index 0):
  - Thumbstick → RightStick
  - Trigger → RightTrigger
  - Grip → RightShoulder
  - A → A button
  - B → B button
  - Thumbstick click → RightStick button
```

**Acceptance criteria:**
- `GamePad.GetState(PlayerIndex.One)` returns VR controller data
- Existing games using GamePad API get some input in VR
- Haptic feedback via `GamePad.SetVibration` triggers controller haptics

---

### Phase 5: Build System & Distribution

#### Task 5.1: Build System Integration
**Files to modify:**
- `native/monogame/premake5.lua` — Already covered in Task 1.1
- `build/BuildFrameworksTasks/BuildNativeTask.cs` — Add OpenXR build targets

**Details:**
- Add NuGet package definitions:
  - `MonoGame.Runtime.Windows.OpenXR`
  - `MonoGame.Runtime.Linux.OpenXR`
  - (macOS deferred — no major XR runtime for macOS currently)
- Build script packs `mgruntime.dll` (OpenXR variant) into runtime NuGet packages

**Acceptance criteria:**
- `dotnet build MonoGame.Framework.OpenXR.sln` succeeds
- Native runtime is built and packaged correctly
- NuGet packages contain the OpenXR-variant native runtime

---

#### Task 5.2: Project Template
**Files to create:**
- `Templates/MonoGame.Templates.CSharp/content/MonoGame.Application.OpenXR/` — Project template

**Details:**
- Template based on the DesktopVK template
- References `MonoGame.Framework.OpenXR` instead of `MonoGame.Framework.Native`
- Includes a sample `Game1.cs` with basic VR rendering:
  ```csharp
  protected override void Draw(GameTime gameTime)
  {
      // Framework handles stereo via multiview automatically
      GraphicsDevice.Clear(Color.CornflowerBlue);
      
      // Access eye data for custom rendering
      if (XRSession != null && XRSession.State >= XRSessionState.Visible)
      {
          var leftEye = XRSession.GetEye(0, displayTime);
          var rightEye = XRSession.GetEye(1, displayTime);
          // Custom rendering with per-eye matrices...
      }
      
      base.Draw(gameTime);
  }
  ```

**Acceptance criteria:**
- `dotnet new mgopenxr -n MyVRGame` creates a working project
- Project builds and runs on a connected headset

---

### Phase 6: Testing & Validation

#### Task 6.1: Unit Tests for XR Types
**Files to create:**
- `Tests/MonoGame.Tests.OpenXR/` — Test project

**Details:**
- Test `XRPose.ToMatrix()` correctness
- Test `XREye.CreateProjectionMatrix()` against known FOV values
- Test enum value alignment between C# and C++
- Test struct size alignment between C# and C++

---

#### Task 6.2: Integration Test — Basic VR Scene
**Files to create:**
- `Tests/MonoGame.Tests.OpenXR/BasicVRScene.cs`

**Details:**
- Initialize OpenXR session
- Render a colored cube at origin
- Verify stereo rendering (different view per eye)
- Verify head tracking updates cube position relative to head
- Verify controller input reports button presses

---

#### Task 6.3: Platform Compatibility Testing
**Targets:**
- Meta Quest 2/3 (via Link or standalone if Android support added later)
- SteamVR (Valve Index, HTC Vive)
- Windows Mixed Reality
- Monado (open-source Linux runtime)

---

## Dependency Graph

```
Task 1.1 (OpenXR SDK) ──────────────────────────────────────────────┐
Task 1.2 (API Header) ──────────────────────────────────────────────┤
                                                                     ↓
Task 1.3 (MGXR_openxr.cpp) ←── depends on 1.1, 1.2               ┐
Task 1.4 (MGP_openxr.cpp) ←── depends on 1.2, 1.3                 ├── Phase 1
Task 1.5 (MGG_Vulkan.cpp mods) ←── depends on 1.3                 ┘
                                                                     ↓
Task 2.1 (Enums) ←── depends on 1.2                               ┐
Task 2.2 (C# Interop) ←── depends on 1.2                          │
Task 2.3 (C# XR Types) ←── depends on 2.2                         ├── Phase 2
Task 2.4 (NativeGamePlatform mods) ←── depends on 2.2, 1.4        │
Task 2.5 (NuGet packages + csproj update) ←── depends on 2.1-2.3  ┘
                                                                     ↓
Task 3.1 (GraphicsDevice mods) ←── depends on 1.5, 2.2            ┐
Task 3.2 (Shader multiview) ←── depends on 1.5                    ├── Phase 3
Task 3.3 (Built-in effects) ←── depends on 3.2                    ┘
                                                                     ↓
Task 4.1 (Action bindings) ←── depends on 1.3                     ┐
Task 4.2 (GamePad mapping) ←── depends on 4.1, 1.4                ├── Phase 4
                                                                    ┘
                                                                     ↓
Task 5.1 (Build system) ←── depends on 1.1, 2.5                   ┐
Task 5.2 (Template) ←── depends on 5.1                             ├── Phase 5
                                                                    ┘
                                                                     ↓
Task 6.1 (Unit tests) ←── depends on 2.3                          ┐
Task 6.2 (Integration test) ←── depends on ALL above               ├── Phase 6
Task 6.3 (Platform compat) ←── depends on 6.2                     ┘
```

## Session/Agent Work Distribution Strategy

Each task is designed to be self-contained with clear inputs/outputs, suitable for different agents or sessions:

| Task Group | Context Needed | Agent Model Suggestion |
|------------|---------------|----------------------|
| **1.1-1.2** (SDK + Header) | premake5.lua, api_*.h files | Standard — file creation |
| **1.3** (MGXR native impl) | OpenXR spec, Vulkan knowledge, api_MGXR.h | Premium — complex C++ |
| **1.4** (MGP_openxr.cpp) | MGP_sdl.cpp (reference), api_MGP.h, MGXR API | Premium — complex C++ |
| **1.5** (Vulkan mods) | MGG_Vulkan.cpp (5319 lines), MGXR API | Premium — large file C++ |
| **2.1-2.2** (Enums + Interop) | api_MGXR.h, existing Interop.cs files | Standard — mechanical |
| **2.3** (C# XR Types) | Interop layer, MonoGame API conventions | Standard — C# design |
| **2.4** (Platform mods) | GamePlatform.Native.cs, XR types | Standard — integration |
| **2.5** (NuGet + csproj) | Existing NuGet/csproj patterns | Standard — boilerplate |
| **3.1-3.3** (Graphics + Shaders) | SPIR-V, Effect system, multiview | Premium — shader work |
| **4.1-4.2** (Input) | OpenXR action system, GamePad.cs | Standard |
| **5.1-5.2** (Build + Template) | Build system, template conventions | Standard |
| **6.1-6.3** (Testing) | Full system | Standard |

---

### Phase 7: Meta Quest 2 Android Standalone Support (PRIMARY TARGET)

The primary development target is **Meta Quest 2**, which runs Android (API level 29+) with Vulkan as its graphics API. This requires bridging MonoGame's Android platform with the OpenXR/Vulkan native module.

#### Current State of MonoGame Android Support
- `MonoGame.Framework.Android.csproj` targets `net8.0-android` with `ANDROID;GLES` defines
- Uses `AndroidGameActivity` (extends `Activity`) as the entry point
- Uses `MonoGameAndroidGameView` (extends `SurfaceView`) for rendering via OpenGL ES
- Uses EGL for OpenGL context management — **NOT Vulkan**
- Native build (`premake5.lua`) currently only builds for desktop (Windows/macOS/Linux) — **no Android/ARM64 targets**
- No existing Vulkan-on-Android code path

#### Key Differences for Quest 2
- Quest 2 runs Android 10+ (API level 29+) on ARM64 (Snapdragon XR2)
- Quest uses **Vulkan 1.1** exclusively for XR rendering (no OpenGL ES for XR)
- OpenXR runtime is built into the Quest OS — no need to ship `openxr_loader` (though Meta provides one)
- The app must be a standard Android APK with specific manifest entries
- OpenXR session needs `XrGraphicsBindingVulkanKHR` — no `ANativeWindow` surface needed
- The app lifecycle must handle Android Activity lifecycle + OpenXR session state machine

#### Task 7.1: Add Android/ARM64 Target to `premake5.lua`
**Files to modify:**
- `native/monogame/premake5.lua` — Add Android system filters and OpenXR+Android project

**Details:**
Extend the existing premake5 build system to support Android NDK cross-compilation for ARM64:

1. **Add Android NDK toolchain configuration:**
   ```lua
   local android_ndk = os.getenv("ANDROID_NDK_HOME") or os.getenv("NDK_ROOT")
   
   function android_config()
       filter {"system:android"}
           architecture "ARM64"
           toolset "clang"
           buildoptions {
               "--target=aarch64-linux-android29",
               "--sysroot=" .. path.join(android_ndk, "toolchains/llvm/prebuilt/linux-x86_64/sysroot")
           }
           linkoptions {
               "--target=aarch64-linux-android29"
           }
           defines {"ANDROID", "__ANDROID__"}
       filter {}
   end
   ```

2. **Add Vulkan-for-Android function (system Vulkan, no volk):**
   ```lua
   function vulkan_android()
       defines {"MG_VULKAN"}
       files {"vulkan/**.h", "vulkan/**.cpp"}
       includedirs {"external/vulkan-headers/include", "external/vma/include"}
       -- On Android, Vulkan is a system library — no volk, no SDK path needed
       filter {"system:android"}
           links {"vulkan", "android", "log"}
       filter {}
   end
   ```

3. **Add the OpenXR+Android project:**
   ```lua
   project "openxr_android"
       common("openxr")
       android_config()
       openxr()
       vulkan_android()
       faudio()
       configs()
   ```

4. **Premake generation:**
   ```bash
   # Generate Android build files (Makefiles for NDK)
   premake5 gmake2 --os=android
   # Build with NDK
   make config=release_arm64
   ```

   Alternatively, if using the `premake-android-studio` module:
   ```bash
   premake5 android-studio
   cd build/android && ./gradlew assembleRelease
   ```

The output path follows the existing convention: `Artifacts/native/mgruntime/openxr/android/Release/libmgruntime.so`

**Acceptance criteria:**
- `premake5 gmake2 --os=android` generates valid Makefiles for ARM64
- `libmgruntime.so` builds for `android-arm64` with MG_OPENXR + MG_VULKAN + MG_FAUDIO
- Can be linked into an Android APK

---

#### Task 7.2: Create `MonoGame.Framework.Android.OpenXR.csproj`
**Files to create:**
- `MonoGame.Framework/MonoGame.Framework.Android.OpenXR.csproj`

**Details:**
This project hybridizes the Android platform (for Activity lifecycle, APK packaging) with the Native/OpenXR platform (for rendering and input):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-android</TargetFramework>
    <SupportedOSPlatformVersion>29</SupportedOSPlatformVersion>
    <DefineConstants>ANDROID;NATIVE;OPENXR;STBSHARP_INTERNAL</DefineConstants>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Description>MonoGame for Meta Quest / Android OpenXR headsets.</Description>
    <PackageId>MonoGame.Framework.Android.OpenXR</PackageId>
  </PropertyGroup>

  <ItemGroup>
    <!-- Use Native platform layer (P/Invoke to mgruntime) -->
    <Compile Remove="Platform\**\*" />
    <Compile Include="Platform\Native\**\*" />
    <Compile Include="XR\**\*" />
    
    <!-- Android-specific files we still need -->
    <Compile Include="Platform\Android\AndroidCompatibility.cs" />
    <Compile Include="Platform\Utilities\ReflectionHelpers.Default.cs" />
    <Compile Include="Platform\Threading.cs" />
    
    <!-- New: XR-specific Android Activity -->
    <Compile Include="Platform\Android\OpenXR\OpenXRGameActivity.cs" />
  </ItemGroup>
</Project>
```

Key difference from regular Android: Uses `NATIVE` platform layer (P/Invoke to `mgruntime`) instead of managed OpenGL ES code.

**Acceptance criteria:**
- Builds targeting `net8.0-android` with minimum API 29
- References both Native interop layer and Android framework types

---

#### Task 7.3: Create `OpenXRGameActivity` — Android Activity for Quest
**Files to create:**
- `MonoGame.Framework/Platform/Android/OpenXR/OpenXRGameActivity.cs`

**Details:**
Replace the standard `AndroidGameActivity` with one tailored for OpenXR:

```csharp
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace Microsoft.Xna.Framework;

/// <summary>
/// Base activity for MonoGame OpenXR apps on Meta Quest.
/// </summary>
[Activity(
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize,
    ScreenOrientation = ScreenOrientation.Landscape)]
public class OpenXRGameActivity : Activity
{
    internal Game Game { private get; set; }

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        // Quest apps are immersive — no system UI
        Window.AddFlags(WindowManagerFlags.KeepScreenOn);
        
        // Load the native runtime
        Java.Lang.JavaSystem.LoadLibrary("openxr_loader");
        Java.Lang.JavaSystem.LoadLibrary("mgruntime");
        
        Game.Activity = this;
    }

    protected override void OnResume()
    {
        base.OnResume();
        // OpenXR session state transitions handled by native xrPollEvent
    }

    protected override void OnPause()
    {
        base.OnPause();
        // OpenXR handles session stopping via state machine
    }

    protected override void OnDestroy()
    {
        Game?.Dispose();
        Game = null;
        base.OnDestroy();
    }
}
```

**AndroidManifest.xml requirements** (added to the game project):
```xml
<uses-feature android:name="android.hardware.vr.headtracking" android:required="true" />
<meta-data android:name="com.oculus.intent.category.VR" android:value="vr_only" />
<meta-data android:name="com.oculus.supportedDevices" android:value="quest2|questpro|quest3" />
<intent-filter>
    <action android:name="android.intent.action.MAIN" />
    <category android:name="android.intent.category.LAUNCHER" />
    <category android:name="com.oculus.intent.category.VR" />
</intent-filter>
```

**Acceptance criteria:**
- Activity loads native libraries correctly
- Quest recognizes the app as a VR app via manifest metadata
- Handles Android lifecycle without crashing

---

#### Task 7.4: Modify `MGP_openxr.cpp` and `MGXR_openxr.cpp` for Android
**Files to modify:**
- `native/monogame/openxr/MGP_openxr.cpp`
- `native/monogame/openxr/MGXR_openxr.cpp`

**Details:**
Android-specific OpenXR initialization:

1. **Instance Extensions on Android:**
   ```cpp
   #ifdef __ANDROID__
   instanceExtensions.push_back(XR_KHR_ANDROID_CREATE_INSTANCE_EXTENSION_NAME);
   #endif
   ```

2. **Android-specific Instance Creation:**
   ```cpp
   #ifdef __ANDROID__
   XrInstanceCreateInfoAndroidKHR androidInfo = {XR_TYPE_INSTANCE_CREATE_INFO_ANDROID_KHR};
   androidInfo.applicationVM = g_javaVM;
   androidInfo.applicationActivity = g_activity;
   createInfo.next = &androidInfo;
   #endif
   ```

3. **JNI bridge** (called from C# to pass Java context):
   ```cpp
   static JavaVM* g_javaVM = nullptr;
   static jobject g_activity = nullptr;

   extern "C" JNIEXPORT jint JNI_OnLoad(JavaVM* vm, void*) {
       g_javaVM = vm;
       return JNI_VERSION_1_6;
   }

   MG_EXPORT void MGXR_SetAndroidContext(void* vm, void* activity) {
       g_javaVM = (JavaVM*)vm;
       g_activity = (jobject)activity;
   }
   ```

4. **File I/O via Android Asset Manager:**
   - `MGP_Platform_MakePath` must use Android-specific paths
   - Content loading uses Android's internal storage paths

**Acceptance criteria:**
- OpenXR instance creates successfully on Quest with Android extensions
- JNI context properly bridges Java Activity to native OpenXR
- No SDL dependencies in the Android build

---

#### Task 7.5: Quest-Specific Vulkan Modifications in `MGG_Vulkan.cpp`
**Files to modify:**
- `native/monogame/vulkan/MGG_Vulkan.cpp`

**Details:**
On Quest (Android), Vulkan initialization differs from desktop:

1. **No volk on Android** — Vulkan is a system library:
   ```cpp
   #ifdef __ANDROID__
   // Vulkan functions available directly, no dynamic loading
   #else
   #define VOLK_IMPLEMENTATION
   #include <volk.h>
   #endif
   ```

2. **No Window Surface** — OpenXR owns the display:
   - Under `#ifdef MG_OPENXR`, all `VkSurfaceKHR` code is skipped
   - `MGVK_RecreateSwapChain` uses OpenXR swapchain images

3. **Physical Device** — must match what OpenXR requires:
   - Use `xrGetVulkanGraphicsDeviceKHR` to select Quest's Adreno GPU

**Acceptance criteria:**
- Vulkan initializes on Quest without desktop dependencies
- Multiview rendering works on Quest's Adreno GPU

---

#### Task 7.6: NuGet Package for Quest Runtime
**Files to create:**
- `src/NuGetPackages/MonoGame.Runtime.Android.OpenXR/MonoGame.Runtime.Android.OpenXR.csproj`

**Details:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Content Include="..\..\..\Artifacts\native\mgruntime\openxr\android\Release\libmgruntime.so">
      <PackagePath>runtimes\android-arm64\native</PackagePath>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <Content Include="..\..\..\native\monogame\external\openxr\android\arm64-v8a\libopenxr_loader.so">
      <PackagePath>runtimes\android-arm64\native</PackagePath>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
```

**Acceptance criteria:**
- NuGet package installs correctly in Android project
- Native `.so` files deploy to APK

---

#### Task 7.7: Quest Project Template
**Files to create:**
- `Templates/MonoGame.Templates.CSharp/content/MonoGame.Application.Quest/`

**Details:**
Template includes:
- `.csproj` referencing `MonoGame.Framework.Android.OpenXR`
- `OpenXRGameActivity`-derived activity with proper attributes
- `AndroidManifest.xml` with Quest VR metadata
- Sample `Game1.cs` with basic VR rendering
- README with ADB deployment instructions

**User workflow:**
```bash
dotnet new mgquest -n MyVRGame
dotnet build -c Release
adb install -r bin/Release/net8.0-android/com.mycompany.myvrgame-Signed.apk
```

**Acceptance criteria:**
- Template creates a buildable Quest 2 VR project
- APK installs and launches on Quest 2
- Shows basic stereo-rendered scene

---

## Updated Dependency Graph (with Quest/Android)

```
Phase 1: Native Foundation (Desktop + Quest shared)
  Task 1.1 (OpenXR SDK) ────────────────────────────────┐
  Task 1.2 (API Header) ────────────────────────────────┤
  Task 1.3 (MGXR native impl) ←── 1.1, 1.2             │
  Task 1.4 (MGP_openxr.cpp) ←── 1.2, 1.3               │
  Task 1.5 (MGG_Vulkan.cpp mods) ←── 1.3                │
                                                          ↓
Phase 2: C# Managed Layer                               
  Task 2.1 (Enums) ←── 1.2                              
  Task 2.2 (C# Interop) ←── 1.2                         
  Task 2.3 (C# XR Types) ←── 2.2                        
  Task 2.4 (NativeGamePlatform mods) ←── 2.2, 1.4       
  Task 2.5 (NuGet + csproj update) ←── 2.1-2.3          
                                                          ↓
Phase 3: Graphics Pipeline (shared Desktop + Quest)      
  Task 3.1 (GraphicsDevice mods) ←── 1.5, 2.2           
  Task 3.2 (Shader multiview) ←── 1.5                   
  Task 3.3 (Built-in effects) ←── 3.2                   
                                                          ↓
Phase 4: VR Input System                                 
  Task 4.1 (Action bindings) ←── 1.3                    
  Task 4.2 (GamePad mapping) ←── 4.1, 1.4              
                                                          ↓
Phase 5: Desktop Build & Template                        
  Task 5.1 (Build integration) ←── 1.1, 2.5            
  Task 5.2 (Desktop template) ←── 5.1                  
                                                          ↓
Phase 7: Quest 2 Android (PRIMARY TARGET) ←── Phase 1-4
  Task 7.1 (Android NDK build) ←── 1.3, 1.4, 1.5      
  Task 7.2 (Android.OpenXR.csproj) ←── 2.3, 7.1        
  Task 7.3 (OpenXRGameActivity) ←── 7.2                 
  Task 7.4 (Android native mods) ←── 1.4, 7.1           
  Task 7.5 (Vulkan Android mods) ←── 1.5, 7.1           
  Task 7.6 (NuGet package) ←── 7.1, 7.2                 
  Task 7.7 (Quest template) ←── 7.6                     
                                                          ↓
Phase 6: Testing (Desktop + Quest)                       
  Task 6.1 (Unit tests) ←── 2.3                        
  Task 6.2 (Integration test) ←── ALL above             
  Task 6.3 (Platform compat) ←── 6.2                   
```

**Recommended execution order for Quest 2 focus:**
Phase 7 tasks (7.1, 7.4, 7.5) should be developed **alongside** Phase 1, not after. The native code (`MGXR_openxr.cpp`, `MGP_openxr.cpp`, `MGG_Vulkan.cpp`) should be written with `#ifdef __ANDROID__` / `#ifdef MG_SDL2` guards from the start, and tested on Quest hardware ASAP.

## Session/Agent Work Distribution Strategy

Each task is designed to be self-contained with clear inputs/outputs, suitable for different agents or sessions:

| Task Group | Context Needed | Agent Model Suggestion |
|------------|---------------|----------------------|
| **1.1-1.2** (SDK + Header) | premake5.lua, api_*.h files | Standard — file creation |
| **1.3** (MGXR native impl) | OpenXR spec, Vulkan knowledge, api_MGXR.h | Premium — complex C++ |
| **1.4** (MGP_openxr.cpp) | MGP_sdl.cpp (reference), api_MGP.h, MGXR API | Premium — complex C++ |
| **1.5** (Vulkan mods) | MGG_Vulkan.cpp (5319 lines), MGXR API | Premium — large file C++ |
| **2.1-2.2** (Enums + Interop) | api_MGXR.h, existing Interop.cs files | Standard — mechanical |
| **2.3** (C# XR Types) | Interop layer, MonoGame API conventions | Standard — C# design |
| **2.4** (Platform mods) | GamePlatform.Native.cs, XR types | Standard — integration |
| **2.5** (NuGet + csproj) | Existing NuGet/csproj patterns | Standard — boilerplate |
| **3.1-3.3** (Graphics + Shaders) | SPIR-V, Effect system, multiview | Premium — shader work |
| **4.1-4.2** (Input) | OpenXR action system, GamePad.cs | Standard |
| **5.1-5.2** (Build + Template) | Build system, template conventions | Standard |
| **6.1-6.3** (Testing) | Full system | Standard |
| **7.1** (Android NDK build) | premake5.lua, NDK, OpenXR loader | Standard — build config |
| **7.2-7.3** (Android csproj + Activity) | Android .NET, existing Android platform | Standard — C# |
| **7.4-7.5** (Android native mods) | Merged with 1.4/1.5 work | Premium — C++ |
| **7.6-7.7** (NuGet + Template) | Existing NuGet/template patterns | Standard — boilerplate |

## Notes & Considerations

1. **Quest 2 is the primary target**: All native code must cross-compile for ARM64 Android. Phase 7 is NOT a follow-up — it should be developed alongside Phases 1-4. The `#ifdef __ANDROID__` guards should be written from day one.

2. **Separate XR project, not embedded in Native**: `MonoGame.Framework.Native.OpenXR.csproj` is a separate assembly that `ProjectReference`s the base `MonoGame.Framework.Native.csproj`. This keeps XR P/Invoke code and types isolated — games that don't use VR don't carry any XR code. Android also needs its own `MonoGame.Framework.Android.OpenXR.csproj` due to the `net8.0-android` TFM requirement.

3. **Audio spatialization**: 3D audio in VR (HRTF) is out of scope for this plan. FAudio continues to work normally. Spatial audio can be a follow-up using OpenXR's audio extension or a dedicated library.

4. **Passthrough/AR**: OpenXR supports AR via `XR_FB_passthrough` and similar extensions. The architecture supports adding these later without redesign.

5. **Hand tracking**: OpenXR hand tracking (`XR_EXT_hand_tracking`) can be added as an extension to the MGXR module. The current plan covers controller-based input only.

6. **Content Pipeline**: No content pipeline changes needed for textures/models/audio. Only shader compilation needs multiview support (Task 3.2). SPIR-V shaders are compatible across desktop Vulkan and Quest Vulkan.

7. **Backward compatibility**: Existing MonoGame games can run in VR with minimal changes — the framework handles stereo rendering via multiview, and controllers map to GamePad. Games just need to reference the OpenXR runtime package and use `XRDevice` for eye matrices.

8. **Android build tooling**: The native build for Android uses premake5, same as all other platforms. Premake generates Makefiles (via `gmake2`) targeting the Android NDK's clang toolchain for ARM64. All platforms share the same `premake5.lua` configuration file.

9. **Meta OpenXR SDK**: Quest's OpenXR loader and runtime headers should come from the [Meta OpenXR SDK](https://github.com/meta-quest/Meta-OpenXR-SDK). This includes Quest-specific extensions for foveated rendering, passthrough, hand tracking, etc.

10. **Foveated Rendering (Future)**: Quest 2 supports fixed foveated rendering via `XR_FB_foveation`. This is a significant performance optimization that can be added as a follow-up to the core XR support.

11. **.NET 8 Android**: The project uses `net8.0-android` which handles AOT compilation, APK packaging, and ADB deployment. The `libmgruntime.so` native library is loaded via `Java.Lang.JavaSystem.LoadLibrary()`.

## Workflow Rules

1. **Maintain `CHANGELOG.md`**: Update the changelog at each task/stage with a summary of what was added or changed. Follow [Keep a Changelog](https://keepachangelog.com/) format.
2. **Commit after each stage**: Once a task or logical group of changes is complete, commit to git with a descriptive message.
3. **Only commit compiling code**: Every commit must leave the codebase in a buildable state. Do not commit partial or broken code — if a task isn't fully compiling yet, finish it before committing.
