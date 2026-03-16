// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Runtime.InteropServices;


namespace MonoGame.Interop;

// Opaque handle types
[MGHandle] internal readonly struct MGXR_System { }
[MGHandle] internal readonly struct MGXR_Session { }
[MGHandle] internal readonly struct MGXR_Swapchain { }
[MGHandle] internal readonly struct MGXR_Space { }
[MGHandle] internal readonly struct MGXR_ActionSet { }
[MGHandle] internal readonly struct MGXR_Action { }

// XR Enums
internal enum XRReferenceSpaceType : int
{
    View = 0,
    Local = 1,
    Stage = 2,
}

internal enum XRActionType : int
{
    Boolean = 0,
    Float = 1,
    Vector2 = 2,
    Pose = 3,
    Haptic = 4,
}

internal enum XRSessionState : int
{
    Unknown = 0,
    Idle = 1,
    Ready = 2,
    Synchronized = 3,
    Visible = 4,
    Focused = 5,
    Stopping = 6,
    LossPending = 7,
    Exiting = 8,
}

// XR Structs
[StructLayout(LayoutKind.Sequential)]
internal struct MGXR_Pose
{
    public float PositionX;
    public float PositionY;
    public float PositionZ;
    public float OrientationX;
    public float OrientationY;
    public float OrientationZ;
    public float OrientationW;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MGXR_ViewProjection
{
    public MGXR_Pose Pose;
    public float FovAngleLeft;
    public float FovAngleRight;
    public float FovAngleUp;
    public float FovAngleDown;
}

// Opaque graphics device handles for session creation.
// Filled by the native backend — Vulkan fills all 5, DX12 fills handle0 + handle1 only.
[StructLayout(LayoutKind.Sequential)]
internal struct MGXR_DeviceHandles
{
    public nint Handle0;  // Vulkan: VkInstance,       DX12: ID3D12Device*
    public nint Handle1;  // Vulkan: VkPhysicalDevice, DX12: ID3D12CommandQueue*
    public nint Handle2;  // Vulkan: VkDevice,         DX12: unused
    public uint Handle3;  // Vulkan: queueFamilyIndex, DX12: unused
    public uint Handle4;  // Vulkan: queueIndex,       DX12: unused
}

// P/Invoke declarations for the MGXR native module
internal static unsafe partial class MGXR
{
    // Android context — must be called before System_Create on Android
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_SetAndroidContext", ExactSpelling = true)]
    public static extern void SetAndroidContext(nint javaVM, nint activity);

    // System lifecycle
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_System_Create", ExactSpelling = true)]
    public static extern MGXR_System* System_Create();

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_System_Destroy", ExactSpelling = true)]
    public static extern void System_Destroy(MGXR_System* system);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_System_IsHmdPresent", ExactSpelling = true)]
    public static extern byte System_IsHmdPresent(MGXR_System* system);

    // Session lifecycle
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_Create", ExactSpelling = true)]
    public static extern MGXR_Session* Session_Create(
        MGXR_System* system,
        MGXR_DeviceHandles* deviceHandles);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_Destroy", ExactSpelling = true)]
    public static extern void Session_Destroy(MGXR_Session* session);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_GetState", ExactSpelling = true)]
    public static extern XRSessionState Session_GetState(MGXR_Session* session);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_BeginFrame", ExactSpelling = true)]
    public static extern byte Session_BeginFrame(MGXR_Session* session, long* predictedDisplayTime, long* predictedDisplayPeriod);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_EndFrame", ExactSpelling = true)]
    public static extern void Session_EndFrame(MGXR_Session* session, long displayTime);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_EndFrameStereo", ExactSpelling = true)]
    public static extern void Session_EndFrameStereo(MGXR_Session* session, MGXR_Swapchain* leftSwapchain, MGXR_Swapchain* rightSwapchain, long displayTime);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_RequestExit", ExactSpelling = true)]
    public static extern void Session_RequestExit(MGXR_Session* session);

    // Swapchain (OpenXR-managed, provides Vulkan images)
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_Create", ExactSpelling = true)]
    public static extern MGXR_Swapchain* Swapchain_Create(MGXR_Session* session, int width, int height, int sampleCount, int arraySize);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_Destroy", ExactSpelling = true)]
    public static extern void Swapchain_Destroy(MGXR_Swapchain* swapchain);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_AcquireImage", ExactSpelling = true)]
    public static extern int Swapchain_AcquireImage(MGXR_Swapchain* swapchain);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_WaitImage", ExactSpelling = true)]
    public static extern void Swapchain_WaitImage(MGXR_Swapchain* swapchain, long timeout);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_ReleaseImage", ExactSpelling = true)]
    public static extern void Swapchain_ReleaseImage(MGXR_Swapchain* swapchain);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_GetVulkanImage", ExactSpelling = true)]
    public static extern void Swapchain_GetVulkanImage(MGXR_Swapchain* swapchain, int index, nint* vkImage, int* width, int* height);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_GetRecommendedSize", ExactSpelling = true)]
    public static extern void Swapchain_GetRecommendedSize(MGXR_Session* session, int* width, int* height);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_GetImageCount", ExactSpelling = true)]
    public static extern int Swapchain_GetImageCount(MGXR_Swapchain* swapchain);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_ConfigureDeviceImages", ExactSpelling = true)]
    public static extern void Swapchain_ConfigureDeviceImages(MGXR_Swapchain* swapchain, nint graphicsDevice);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Swapchain_SetActiveImage", ExactSpelling = true)]
    public static extern void Swapchain_SetActiveImage(MGXR_Swapchain* swapchain, nint graphicsDevice, int imageIndex);

    // Graphics device bridge — retrieves opaque device handles for session creation.
    // The native backend fills the struct appropriately for Vulkan or DX12.
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGG_GraphicsDevice_GetDeviceHandles", ExactSpelling = true)]
    public static extern void GraphicsDevice_GetDeviceHandles(
        nint graphicsDevice, MGXR_DeviceHandles* outHandles);

    // View/Projection (per-eye data)
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_GetViewCount", ExactSpelling = true)]
    public static extern int Session_GetViewCount(MGXR_Session* session);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Session_GetViewProjection", ExactSpelling = true)]
    public static extern void Session_GetViewProjection(MGXR_Session* session, int viewIndex, long displayTime, MGXR_ViewProjection* view);

    // Reference Spaces
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Space_Create", ExactSpelling = true)]
    public static extern MGXR_Space* Space_Create(MGXR_Session* session, XRReferenceSpaceType type);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Space_Destroy", ExactSpelling = true)]
    public static extern void Space_Destroy(MGXR_Space* space);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Space_GetPose", ExactSpelling = true)]
    public static extern void Space_GetPose(MGXR_Space* space, MGXR_Space* baseSpace, long time, MGXR_Pose* pose);

    // Action System (VR Input)
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_ActionSet_Create", ExactSpelling = true)]
    public static extern MGXR_ActionSet* ActionSet_Create(
        MGXR_System* system,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string localizedName);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_ActionSet_Destroy", ExactSpelling = true)]
    public static extern void ActionSet_Destroy(MGXR_ActionSet* actionSet);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_ActionSet_AttachToSession", ExactSpelling = true)]
    public static extern void ActionSet_AttachToSession(MGXR_Session* session, MGXR_ActionSet** sets, int count);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Action_Create", ExactSpelling = true)]
    public static extern MGXR_Action* Action_Create(
        MGXR_ActionSet* actionSet,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string localizedName,
        XRActionType type);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Action_Destroy", ExactSpelling = true)]
    public static extern void Action_Destroy(MGXR_Action* action);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Action_SuggestBindings", ExactSpelling = true)]
    public static extern void Action_SuggestBindings(
        MGXR_System* system,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string interactionProfile,
        MGXR_Action** actions,
        byte** paths,
        int count);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_ActionSet_Sync", ExactSpelling = true)]
    public static extern void ActionSet_Sync(MGXR_Session* session, MGXR_ActionSet** sets, int count);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Action_GetStateBool", ExactSpelling = true)]
    public static extern void Action_GetStateBool(MGXR_Action* action, byte* value, byte* changed);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Action_GetStateFloat", ExactSpelling = true)]
    public static extern void Action_GetStateFloat(MGXR_Action* action, float* value, byte* changed);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Action_GetStateVector2", ExactSpelling = true)]
    public static extern void Action_GetStateVector2(MGXR_Action* action, float* x, float* y, byte* changed);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Action_GetStatePose", ExactSpelling = true)]
    public static extern void Action_GetStatePose(MGXR_Action* action, MGXR_Space* space, long time, MGXR_Pose* pose, int hand, int* outFlags);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Action_ApplyHaptic", ExactSpelling = true)]
    public static extern void Action_ApplyHaptic(MGXR_Action* action, float amplitude, float frequency, long duration);

    // Platform integration — attaches actions to an active session
    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGXR_Platform_AttachActionsToSession", ExactSpelling = true)]
    public static extern void Platform_AttachActionsToSession(nint platform, MGXR_Session* session);
}
