// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
#if NATIVE
using MonoGame.Interop;
#endif

namespace Microsoft.Xna.Framework.XR;

/// <summary>
/// Provides access to VR/XR headset functionality.
/// This is a static API — the framework manages the OpenXR session lifecycle internally.
/// All methods are safe to call on non-XR platforms (they return defaults or no-op).
/// </summary>
public static unsafe class XRDevice
{
#if NATIVE
    private static MGXR_System* _system;
    private static MGXR_Session* _session;
    private static MGXR_Space* _stageSpace;
    private static MGXR_Space* _localSpace;
#endif
    private static XRTrackingSpace _trackingSpace = XRTrackingSpace.Local;
    private static bool _isInitialized;

    /// <summary>
    /// Gets whether the XR runtime is available and an HMD is connected.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
#if NATIVE
            if (_system == null)
                return false;
            return MGXR.System_IsHmdPresent(_system) != 0;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Gets the current session state.
    /// </summary>
    public static XRSessionState SessionState
    {
        get
        {
#if NATIVE
            if (_session == null)
                return XRSessionState.Unknown;
            return (XRSessionState)(int)MGXR.Session_GetState(_session);
#else
            return XRSessionState.Unknown;
#endif
        }
    }

    /// <summary>
    /// Gets whether the session is actively rendering (Visible or Focused).
    /// </summary>
    public static bool IsRunning
    {
        get
        {
            var state = SessionState;
            return state == XRSessionState.Visible || state == XRSessionState.Focused;
        }
    }

    /// <summary>
    /// Gets or sets the tracking space origin type.
    /// </summary>
    public static XRTrackingSpace TrackingSpace
    {
        get => _trackingSpace;
        set => _trackingSpace = value;
    }

    /// <summary>
    /// Gets the number of views (typically 2 for stereo VR).
    /// </summary>
    public static int ViewCount
    {
        get
        {
#if NATIVE
            if (_session == null)
                return 0;
            return MGXR.Session_GetViewCount(_session);
#else
            return 0;
#endif
        }
    }

    /// <summary>
    /// Gets the recommended render target size for one eye.
    /// </summary>
    public static void GetRecommendedSize(out int width, out int height)
    {
        width = 0;
        height = 0;
#if NATIVE
        if (_session != null)
        {
            int w, h;
            MGXR.Swapchain_GetRecommendedSize(_session, &w, &h);
            width = w;
            height = h;
        }
#endif
    }

    /// <summary>
    /// Gets the predicted display time for the current frame.
    /// Set automatically by the framework during BeginFrame.
    /// </summary>
    public static long CurrentDisplayTime { get; internal set; }

    /// <summary>
    /// Gets the view and projection data for a specific eye using the current frame's display time.
    /// </summary>
    /// <param name="viewIndex">0 for left eye, 1 for right eye.</param>
    public static XRView GetView(int viewIndex)
    {
        return GetView(viewIndex, CurrentDisplayTime);
    }

    /// <summary>
    /// Gets the view and projection data for a specific eye.
    /// </summary>
    /// <param name="viewIndex">0 for left eye, 1 for right eye.</param>
    /// <param name="displayTime">The predicted display time from BeginFrame.</param>
    public static XRView GetView(int viewIndex, long displayTime)
    {
        var view = new XRView();
#if NATIVE
        if (_session == null)
            return view;

        MGXR_ViewProjection vp;
        MGXR.Session_GetViewProjection(_session, viewIndex, displayTime, &vp);

        view.Pose = new XRPose
        {
            Position = new Vector3(vp.Pose.PositionX, vp.Pose.PositionY, vp.Pose.PositionZ),
            Orientation = new Quaternion(vp.Pose.OrientationX, vp.Pose.OrientationY, vp.Pose.OrientationZ, vp.Pose.OrientationW),
        };

        view.ViewMatrix = view.Pose.ToViewMatrix();

        view.ProjectionMatrix = CreateProjectionFov(
            vp.FovAngleLeft, vp.FovAngleRight,
            vp.FovAngleUp, vp.FovAngleDown,
            0.01f, 1000.0f);
#endif
        return view;
    }

    /// <summary>
    /// Creates an off-center projection matrix from field-of-view angles (in radians).
    /// </summary>
    internal static Matrix CreateProjectionFov(
        float angleLeft, float angleRight,
        float angleUp, float angleDown,
        float nearPlane, float farPlane)
    {
        float tanLeft = MathF.Tan(angleLeft);
        float tanRight = MathF.Tan(angleRight);
        float tanUp = MathF.Tan(angleUp);
        float tanDown = MathF.Tan(angleDown);

        float width = tanRight - tanLeft;
        float height = tanUp - tanDown;

        var result = new Matrix();
        result.M11 = 2.0f / width;
        result.M22 = 2.0f / height;
        result.M31 = (tanRight + tanLeft) / width;
        result.M32 = (tanUp + tanDown) / height;
        result.M33 = -(farPlane + nearPlane) / (farPlane - nearPlane);
        result.M34 = -1.0f;
        result.M43 = -(2.0f * farPlane * nearPlane) / (farPlane - nearPlane);
        return result;
    }

    // --- Internal lifecycle (called by the framework, not by user code) ---

#if NATIVE
    private static MGXR_Swapchain* _leftSwapchain;
    private static MGXR_Swapchain* _rightSwapchain;
    private static Graphics.GraphicsDevice _graphicsDevice;
    private static int _activeEye = -1;

    internal static void Initialize(MGXR_System* system)
    {
        _system = system;
        _isInitialized = true;
    }

    internal static void CreateSession(MGXR_DeviceHandles* deviceHandles)
    {
        if (_system == null)
            return;

        _session = MGXR.Session_Create(_system, deviceHandles);

        if (_session != null)
        {
            _localSpace = MGXR.Space_Create(_session, MonoGame.Interop.XRReferenceSpaceType.Local);
            _stageSpace = MGXR.Space_Create(_session, MonoGame.Interop.XRReferenceSpaceType.Stage);
        }
    }

    /// <summary>
    /// Sets the graphics device reference and creates stereo swapchains.
    /// Called from XRPlatformHelper.OnGraphicsDeviceCreated.
    /// </summary>
    internal static void SetGraphicsDevice(Graphics.GraphicsDevice device)
    {
        _graphicsDevice = device;
    }

    /// <summary>
    /// Creates the left and right eye swapchains at the recommended size.
    /// </summary>
    internal static void CreateSwapchains()
    {
        if (_session == null)
            return;

        int width, height;
        MGXR.Swapchain_GetRecommendedSize(_session, &width, &height);
        if (width <= 0 || height <= 0)
            return;

        _leftSwapchain = MGXR.Swapchain_Create(_session, width, height, 1, 1);
        _rightSwapchain = MGXR.Swapchain_Create(_session, width, height, 1, 1);

        // Configure both swapchains' images in the graphics device
        if (_leftSwapchain != null && _graphicsDevice != null)
            MGXR.Swapchain_ConfigureDeviceImages(_leftSwapchain, (nint)_graphicsDevice.Handle);
    }

    /// <summary>
    /// Begins rendering to the specified eye. Acquires the eye's swapchain image
    /// and sets it as the graphics device backbuffer.
    /// Call this before rendering each eye in your Draw() method.
    /// </summary>
    public static void BeginEye(int eye)
    {
        if (_graphicsDevice == null)
            return;

        var swapchain = (eye == 0) ? _leftSwapchain : _rightSwapchain;
        if (swapchain == null)
            return;

        _activeEye = eye;

        int imageIndex = MGXR.Swapchain_AcquireImage(swapchain);
        MGXR.Swapchain_WaitImage(swapchain, 100_000_000); // 100ms timeout
        MGXR.Swapchain_SetActiveImage(swapchain, (nint)_graphicsDevice.Handle, imageIndex);
    }

    /// <summary>
    /// Finishes rendering to the current eye. Releases the swapchain image.
    /// Call this after rendering each eye in your Draw() method.
    /// </summary>
    public static void EndEye(int eye)
    {
        var swapchain = (eye == 0) ? _leftSwapchain : _rightSwapchain;
        if (swapchain == null)
            return;

        // Ensure the GPU has finished rendering before releasing
        if (_graphicsDevice != null)
            _graphicsDevice.Present();

        MGXR.Swapchain_ReleaseImage(swapchain);
        _activeEye = -1;
    }

    internal static bool BeginFrame(out long displayTime)
    {
        displayTime = 0;
        if (_session == null)
            return false;

        long predictedTime, predictedPeriod;
        byte result = MGXR.Session_BeginFrame(_session, &predictedTime, &predictedPeriod);
        displayTime = predictedTime;
        CurrentDisplayTime = predictedTime;
        return result != 0;
    }

    internal static void EndFrame(long displayTime)
    {
        if (_session == null)
            return;

        // Use stereo EndFrame if we have both swapchains
        if (_leftSwapchain != null && _rightSwapchain != null)
            MGXR.Session_EndFrameStereo(_session, _leftSwapchain, _rightSwapchain, displayTime);
        else
            MGXR.Session_EndFrame(_session, displayTime);
    }

    /// <summary>
    /// Phase 1: Destroy session-level XR resources (swapchains, spaces, session).
    /// Called before VkDevice destruction.
    /// </summary>
    internal static void ShutdownSession()
    {
        if (_leftSwapchain != null)
        {
            MGXR.Swapchain_Destroy(_leftSwapchain);
            _leftSwapchain = null;
        }
        if (_rightSwapchain != null)
        {
            MGXR.Swapchain_Destroy(_rightSwapchain);
            _rightSwapchain = null;
        }
        if (_stageSpace != null)
        {
            MGXR.Space_Destroy(_stageSpace);
            _stageSpace = null;
        }
        if (_localSpace != null)
        {
            MGXR.Space_Destroy(_localSpace);
            _localSpace = null;
        }
        if (_session != null)
        {
            MGXR.Session_Destroy(_session);
            _session = null;
        }
        _graphicsDevice = null;
        _activeEye = -1;
    }

    /// <summary>
    /// Phase 2: Destroy the XR instance. Called after VkDevice destruction.
    /// </summary>
    internal static void Shutdown()
    {
        ShutdownSession();
        if (_system != null)
        {
            MGXR.System_Destroy(_system);
            _system = null;
        }
        _isInitialized = false;
    }

    internal static MGXR_Session* NativeSession => _session;

    internal static MGXR_System* NativeSystem => _system;

    internal static MGXR_Space* GetActiveSpace()
    {
        return _trackingSpace == XRTrackingSpace.Stage ? _stageSpace : _localSpace;
    }
#else
    /// <summary>
    /// Begins rendering to the specified eye (no-op on non-XR platforms).
    /// </summary>
    public static void BeginEye(int eye) { }

    /// <summary>
    /// Finishes rendering to the current eye (no-op on non-XR platforms).
    /// </summary>
    public static void EndEye(int eye) { }
#endif
}
