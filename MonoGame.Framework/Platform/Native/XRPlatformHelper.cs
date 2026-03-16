// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.XR;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework.Utilities;
using MonoGame.Interop;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework;

/// <summary>
/// Extends the game platform with OpenXR session management.
/// Called from NativeGamePlatform lifecycle hooks when running on the OpenXR platform.
/// </summary>
internal static unsafe class XRPlatformHelper
{
    private static bool _xrSessionCreated;
    private static long _currentDisplayTime;

    [DllImport("libc", EntryPoint = "_exit")]
    private static extern void LibC_Exit(int status);

    /// <summary>
    /// Returns true if the current runtime is the OpenXR platform.
    /// </summary>
    internal static bool IsXRPlatform =>
        PlatformInfo.MonoGamePlatform == MonoGamePlatform.OpenXR;

    /// <summary>
    /// Called during platform initialization. Initializes the XR system
    /// from the native platform handle (which already created MGXR_System in MGP_Platform_Create).
    /// </summary>
    internal static void Initialize(MGP_Platform* platformHandle)
    {
        System.Console.Error.WriteLine($"[XRHelper] Initialize: IsXRPlatform={IsXRPlatform}");
        if (!IsXRPlatform)
            return;

        var system = (MGXR_System*)MGP.Platform_GetXRSystem(platformHandle);
        System.Console.Error.WriteLine($"[XRHelper] Initialize: system={(nint)system:X}");
        if (system != null)
        {
            // Set the XR system on the Vulkan backend BEFORE GraphicsSystem_Create
            // so it can wrap vkCreateInstance/vkCreateDevice through XR_KHR_vulkan_enable2
            MGG.SetXRSystem((nint)system);
            XRDevice.Initialize(system);
        }
        else
            System.Console.Error.WriteLine("[XRHelper] Initialize: system is NULL — XR won't work");
    }

    /// <summary>
    /// Called after the graphics device is created. Creates the OpenXR session
    /// and stereo swapchains using handles from the active graphics backend.
    /// Works with both Vulkan and DX12 backends.
    /// </summary>
    internal static void OnGraphicsDeviceCreated(GraphicsDevice device)
    {
        System.Console.Error.WriteLine($"[XRHelper] OnGraphicsDeviceCreated: IsXRPlatform={IsXRPlatform}, _xrSessionCreated={_xrSessionCreated}");
        if (!IsXRPlatform || _xrSessionCreated)
            return;

        _xrSessionCreated = true;
        XRDevice.SetGraphicsDevice(device);

        // Get opaque device handles from the active graphics backend.
        // The native side fills the struct appropriately for Vulkan or DX12.
        MGXR_DeviceHandles handles;
        MGXR.GraphicsDevice_GetDeviceHandles((nint)device.Handle, &handles);

        System.Console.Error.WriteLine($"[XRHelper] Device handles: h0={handles.Handle0:X}, h1={handles.Handle1:X}, h2={handles.Handle2:X}");

        XRDevice.CreateSession(&handles);
        System.Console.Error.WriteLine($"[XRHelper] CreateSession done, NativeSession={((nint)XRDevice.NativeSession):X}");

        // Attach the platform's VR input action set to the newly created session
        var session = XRDevice.NativeSession;
        if (session != null && NativeGamePlatform.Instance != null)
        {
            var platformHandle = NativeGamePlatform.Instance.Handle;
            MGXR.Platform_AttachActionsToSession((nint)platformHandle, session);
            System.Console.Error.WriteLine("[XRHelper] Actions attached to session");
        }

        XRDevice.CreateSwapchains();
        System.Console.Error.WriteLine($"[XRHelper] CreateSwapchains done, ViewCount={XRDevice.ViewCount}");
    }

    /// <summary>
    /// True when the XR runtime has requested the application to exit
    /// (session state is Stopping, LossPending, or Exiting).
    /// </summary>
    internal static bool ExitRequested { get; private set; }

    /// <summary>
    /// Called at the start of each frame. Performs xrWaitFrame/xrBeginFrame.
    /// The game is responsible for calling XRDevice.BeginEye/EndEye for each eye.
    /// </summary>
    internal static bool BeginFrame(GraphicsDevice device)
    {
        if (!IsXRPlatform)
            return true;

        var result = XRDevice.BeginFrame(out _currentDisplayTime);

        if (!result)
        {
            var state = XRDevice.SessionState;
            if (state == XR.XRSessionState.Stopping ||
                state == XR.XRSessionState.LossPending ||
                state == XR.XRSessionState.Exiting)
            {
                ExitRequested = true;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the predicted display time for the current frame.
    /// </summary>
    internal static long CurrentDisplayTime => _currentDisplayTime;

    /// <summary>
    /// Called after rendering. Performs xrEndFrame with stereo composition layers.
    /// </summary>
    internal static void EndFrame()
    {
        if (!IsXRPlatform)
            return;

        XRDevice.EndFrame(_currentDisplayTime);
    }

    /// <summary>
    /// Called during shutdown. Cleans up the XR session and system.
    /// </summary>
    /// <summary>
    /// Destroys session-level XR resources. Must be called before VkDevice destruction.
    /// </summary>
    internal static void ShutdownSession()
    {
        if (!IsXRPlatform)
            return;

        XRDevice.ShutdownSession();
        _xrSessionCreated = false;
    }

    /// <summary>
    /// Destroys the XR instance. Must be called after VkDevice destruction.
    /// </summary>
    internal static void Shutdown()
    {
        if (!IsXRPlatform)
            return;

        XRDevice.Shutdown();

        // WORKAROUND: Meta XR Simulator on macOS has a double-free bug in its atexit handlers.
        // The simulator's "Shutdown ImGui Vulkan" destroys its embedded MoltenVK resources,
        // then when .NET unloads SIMULATOR.so, C++ destructors try to free already-freed memory.
        // We've already cleaned up all OpenXR/Vulkan resources above, so skip atexit entirely.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            System.Console.Error.WriteLine("[XRHelper] Shutdown: Calling _exit(0) to bypass Meta XR Simulator double-free bug");
            LibC_Exit(0);
        }
    }
}
