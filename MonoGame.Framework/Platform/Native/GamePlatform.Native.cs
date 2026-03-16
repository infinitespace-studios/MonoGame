// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MonoGame.Interop;
using System.Threading;
using Microsoft.Xna.Framework.XR;

namespace Microsoft.Xna.Framework;

partial class GamePlatform
{
    internal static GamePlatform PlatformCreate(Game game) => new NativeGamePlatform(game);
}

class NativeGamePlatform : GamePlatform
{
    internal unsafe MGP_Platform* Handle;

    internal static NativeGamePlatform Instance { get; private set; }

    private static unsafe MGG_GraphicsSystem* _system;

    private NativeGameWindow _window;

    private readonly List<string> _dropList = new List<string>(64);

    private int _isExiting;

    [DllImport("libc", EntryPoint = "_exit")]
    private static extern void _exit(int status);

    public unsafe NativeGamePlatform(Game game) : base(game)
    {
        Instance = this;

#if OPENXR
        // Wire up bidirectional reference like AndroidGamePlatform does.
        // Game.Activity was set in OpenXRGameActivity.OnCreate() before Game was constructed.
        if (Game.Activity is OpenXRGameActivity openXRActivity)
            openXRActivity.Game = game;
#endif

        GameRunBehavior behavior;
        Handle = MGP.Platform_Create(out behavior);

        DefaultRunBehavior = behavior;

        _window = new NativeGameWindow(this, true);

        Window = _window;

        Mouse.WindowHandle = _window.Handle;
        MessageBox._window = _window._handle;
        GamePad.Handle = Handle;
        OnIsMouseVisibleChanged();
        XRPlatformHelper.Initialize(Handle);
    }

    internal static unsafe MGG_GraphicsSystem* GraphicsSystem
    {
        get
        {
            if (_system == null)
                _system = MGG.GraphicsSystem_Create();

            return _system;
        }
    }

    public override GameRunBehavior DefaultRunBehavior { get; }

    public override unsafe void Exit()
    {
        Interlocked.Increment(ref _isExiting);
    }

    public override unsafe void RunLoop()
    {
        _window.Show(true);

        while (true)
        {
            RunOneLoop();

            if (_isExiting > 0 && ShouldExit())
                break;
            else
                _isExiting = 0;
        }
    }

    private unsafe void RunOneLoop()
    {
        PollEvents();

        Game.Tick();

        Threading.Run();
    }

    private unsafe void PollEvents()
    {
#if OPENXR
        VRController.UpdateTrackingState();
#endif

        MGP_Event event_;
        while (MGP.Platform_PollEvent(Handle, out event_) != 0)
        {
            switch (event_.Type)
            {
                case EventType.Quit:
                    Game.Exit();
                    break;

                case EventType.WindowGainedFocus:
                    IsActive = true;
                    break;

                case EventType.WindowLostFocus:
                    IsActive = false;
                    break;

                case EventType.WindowResized:
                { 
                    var window = NativeGameWindow.FromHandle(event_.Window.Window);
                    if (window != null)
                        window.ClientResize(event_.Window.Data1, event_.Window.Data2);
                    break;
                }

                case EventType.WindowClose:
                { 
                    var window = NativeGameWindow.FromHandle(event_.Window.Window);
                    if (Window == window)
                        Game.Exit();
                    break;
                }

                case EventType.KeyDown:
                {
                    var window = NativeGameWindow.FromHandle(event_.Key.Window);
                    var key = event_.Key.Key;
                    var character = (char)event_.Key.Character;

                    if (!Keyboard.Keys.Contains(key))
                        Keyboard.Keys.Add(key);

                    if (window != null)
                    { 
                        window.OnKeyDown(new InputKeyEventArgs(key));

                        if (window.IsTextInputHandled && char.IsControl(character))
                            window.OnTextInput(new TextInputEventArgs(character, key));
                    }

                    break;
                }

                case EventType.KeyUp:
                {
                    var window = NativeGameWindow.FromHandle(event_.Key.Window);
                    var key = event_.Key.Key;

                    Keyboard.Keys.Remove(key);

                    if (window != null)
                        window.OnKeyUp(new InputKeyEventArgs(key));

                    break;
                }

                case EventType.TextInput:
                {
                    var window = NativeGameWindow.FromHandle(event_.Key.Window);
                    if (window != null && window.IsTextInputHandled)
                    {
                        var key = event_.Key.Key;
                        var character = (char)event_.Key.Character;
                        window.OnTextInput(new TextInputEventArgs(character, key));
                    }
                    break;
                }

                case EventType.MouseMove:
                {
                    var window = NativeGameWindow.FromHandle(event_.MouseMove.Window);
                    if (window != null)
                    {
                        window.MouseState.X = event_.MouseMove.X;
                        window.MouseState.Y = event_.MouseMove.Y;
                    }
                    break;
                }

                case EventType.MouseWheel:
                {
                    var window = NativeGameWindow.FromHandle(event_.MouseWheel.Window);
                    if (window != null)
                    {
                        window.MouseState.ScrollWheelValue += event_.MouseWheel.Scroll;
                        window.MouseState.HorizontalScrollWheelValue += event_.MouseWheel.ScrollH;
                    }
                    break;
                }

                case EventType.MouseButtonUp:
                case EventType.MouseButtonDown:
                {
                    var window = NativeGameWindow.FromHandle(event_.MouseButton.Window);
                    if (window != null)
                    {
                        var state = event_.Type == EventType.MouseButtonDown ? ButtonState.Pressed : ButtonState.Released;

                        switch (event_.MouseButton.Button)
                        {
                            case MouseButton.Left:
                                window.MouseState.LeftButton = state;
                                break;
                            case MouseButton.Right:
                                window.MouseState.RightButton = state;
                                break;
                            case MouseButton.Middle:
                                window.MouseState.MiddleButton = state;
                                break;
                            case MouseButton.X1:
                                window.MouseState.XButton1 = state;
                                break;
                            case MouseButton.X2:
                                window.MouseState.XButton2 = state;
                                break;
                         }
                    }
                    break;
                }

                case EventType.ControllerAdded:
                {
                    GamePad.Add(event_.Controller.Id);
                    break;
                }

                case EventType.ControllerRemoved:
                {
                    GamePad.Remove(event_.Controller.Id);
                    break;
                }

                case EventType.ControllerStateChange:
                {
                    GamePad.ChangeState(event_.Controller.Id, event_.Timestamp, event_.Controller.Input, event_.Controller.Value);
#if OPENXR
                    // Dispatch per-hand state to VRController based on input mapping
                    int hand = GetVRHand(event_.Controller.Input);
                    if (hand >= 0)
                        VRController.ChangeState(hand, event_.Controller.Input, event_.Controller.Value);
#endif
                    break;
                }

                case EventType.DropFile:
                {
                    var file = Marshal.PtrToStringUTF8(event_.Drop.File);
                    _dropList.Add(file);
                    break;
                }

                case EventType.DropComplete:
                {
                    var window = NativeGameWindow.FromHandle(event_.Drop.Window);
                    if (window != null )
                        window.OnFileDrop(new FileDropEventArgs(_dropList.ToArray()));
                    _dropList.Clear();
                    break;
                }

#if OPENXR
                case EventType.VRControllerPose:
                {
                    var pose = new XR.XRPose
                    {
                        Position = new System.Numerics.Vector3(
                            event_.VRPose.PosX, event_.VRPose.PosY, event_.VRPose.PosZ),
                        Orientation = new System.Numerics.Quaternion(
                            event_.VRPose.OriX, event_.VRPose.OriY, event_.VRPose.OriZ, event_.VRPose.OriW),
                    };
                    VRController.UpdatePose(event_.VRPose.Hand, event_.VRPose.PoseType, pose,
                        (XR.XRSpaceLocationFlags)event_.VRPose.Flags);
                    break;
                }
#endif
            }
        }
    }

    private bool ShouldExit()
    {
        if (    Keyboard.Keys.Contains(Keys.F4) &&
                (   Keyboard.Keys.Contains(Keys.LeftAlt) ||
                    Keyboard.Keys.Contains(Keys.RightAlt)))
        {
            return Window.AllowAltF4;
        }

        return true;
    }

#if OPENXR
    // Maps a ControllerInput to a VR hand index (0=left, 1=right, -1=unknown)
    private static int GetVRHand(ControllerInput input)
    {
        switch (input)
        {
            case ControllerInput.LeftTrigger:
            case ControllerInput.LeftStickX:
            case ControllerInput.LeftStickY:
            case ControllerInput.LeftStick:
            case ControllerInput.LeftShoulder:
            case ControllerInput.X:
            case ControllerInput.Y:
            case ControllerInput.Start:
                return 0; // Left hand

            case ControllerInput.RightTrigger:
            case ControllerInput.RightStickX:
            case ControllerInput.RightStickY:
            case ControllerInput.RightStick:
            case ControllerInput.RightShoulder:
            case ControllerInput.A:
            case ControllerInput.B:
            case ControllerInput.Back:
                return 1; // Right hand

            default:
                return -1;
        }
    }
#endif

    public override void Present()
    {
        if (Game.GraphicsDevice != null)
            Game.GraphicsDevice.Present();
        XRPlatformHelper.EndFrame();
    }

    // Delegate type matching the native MGP_FrameCallback typedef: int (*)(void)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FrameCallbackDelegate();

    // Must be stored as a field to prevent GC collection while native code holds the pointer.
    private static FrameCallbackDelegate _frameCallbackDelegate;

    public override unsafe void StartRunLoop()
    {
        if (DefaultRunBehavior == GameRunBehavior.Synchronous)
        {
            // Desktop platforms use synchronous mode — delegate to native.
            MGP.Platform_StartRunLoop(Handle);
            return;
        }

        // Async platforms: pass a managed callback to the native layer.
        // The native side decides how to run it (pthread on Android,
        // emscripten_set_main_loop on WASM, etc.) and returns immediately.
        _window.Show(true);

        _frameCallbackDelegate = RunOneLoopCallback;
        var callbackPtr = Marshal.GetFunctionPointerForDelegate(_frameCallbackDelegate);
        MGP.Platform_StartRunLoopAsync(Handle, callbackPtr);
    }

    private static int RunOneLoopCallback()
    {
        var self = Instance;

        self.PollEvents();
        self.Game.Tick();
        Threading.Run();

        if (XRPlatformHelper.ExitRequested)
            Interlocked.Increment(ref self._isExiting);

        if (self._isExiting > 0 && self.ShouldExit())
        {
            XRPlatformHelper.ShutdownSession();
            self.RaiseAsyncRunLoopEnded();
            return 1; // Signal native loop to stop
        }

        self._isExiting = 0;
        return 0; // Continue
    }

    public override unsafe void BeforeInitialize()
    {
        var gdm = Game.graphicsDeviceManager;
        if (gdm == null)
        {
            // TODO: ???
        }
        else
        {
            var pp = Game.GraphicsDevice.PresentationParameters;
            _window.OnPresentationChanged(pp);
        }

        // After graphics device is available, configure OpenXR swapchain
        if (Game.GraphicsDevice != null)
            XRPlatformHelper.OnGraphicsDeviceCreated(Game.GraphicsDevice);

        base.BeforeInitialize();        
    }

    public override unsafe bool BeforeRun()
    {        
        return MGP.Platform_BeforeRun(Handle) == 0 ? false : true;
    }

    public override unsafe bool BeforeUpdate(GameTime gameTime)
    {
        return MGP.Platform_BeforeUpdate(Handle) == 0 ? false : true;
    }

    public override unsafe bool BeforeDraw(GameTime gameTime)
    {
        if (!XRPlatformHelper.BeginFrame(Game.GraphicsDevice))
            return false;
        return MGP.Platform_BeforeDraw(Handle) == 0 ? false : true;
    }

    public override unsafe void EnterFullScreen()
    {
    }

    public override unsafe void ExitFullScreen()
    {
    }

    public override void BeginScreenDeviceChange(bool willBeFullScreen)
    {
    }
    public override void EndScreenDeviceChange(string screenDeviceName, int clientWidth, int clientHeight)
    {

    }

    internal override void OnPresentationChanged(PresentationParameters pp)
    {
        _window.OnPresentationChanged(pp);
    }

    protected override unsafe void OnIsMouseVisibleChanged()
    {
        MGP.Mouse_SetVisible(Handle, (byte)(IsMouseVisible ? 1 : 0));
    }

    protected unsafe override void Dispose(bool disposing)
    {
        XRPlatformHelper.Shutdown();

        if (_window != null)
        {
            _window.Destroy();
            _window = null;
            Window = null;
        }
        
        if (_system != null)
        {
            MGG.GraphicsSystem_Destroy(_system);
            _system = null;
        }

        if (Handle != null)
        {
            MGP.Platform_Destroy(Handle);
            Handle = null;
        }

        base.Dispose(disposing);

        // Meta XR Simulator on macOS crashes on exit due to a double-free in its shared library destructor
        // during .NET process teardown. We must use _exit(0) to bypass atexit handlers and avoid this crash.
        if (XRPlatformHelper.IsXRPlatform && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _exit(0);
        }
    }
}
