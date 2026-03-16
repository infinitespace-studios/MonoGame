// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.XR;
using MonoGame.Interop;

namespace Microsoft.Xna.Framework.Input;

/// <summary>
/// Provides per-hand VR controller input including 6DOF poses, buttons,
/// triggers, and thumbstick state. Also supports hand tracking when available.
/// </summary>
/// <remarks>
/// State is accumulated from native platform events each frame, following the
/// same event-driven pattern as <see cref="GamePad"/> and <see cref="Keyboard"/>.
/// </remarks>
public static unsafe class VRController
{
    // Internal per-hand state, mutated incrementally by events
    private class HandState
    {
        public bool IsConnected;
        public XRPose AimPose;
        public XRPose GripPose;
        public XRSpaceLocationFlags AimPoseFlags;
        public XRSpaceLocationFlags GripPoseFlags;
        public Buttons Buttons;
        public float Trigger;
        public float Grip;
        public Vector2 ThumbStick;
    }

    private static readonly HandState[] _state = { new HandState(), new HandState() };

    /// <summary>
    /// Gets whether VR controller input is available.
    /// </summary>
    public static bool IsAvailable => XRDevice.IsAvailable;

    /// <summary>
    /// Gets the full input state for the specified hand's controller.
    /// </summary>
    public static VRControllerState GetState(XRHandedness hand)
    {
        var s = _state[(int)hand];
        var result = new VRControllerState();

        result.IsConnected = s.IsConnected;
        result.AimPose = s.AimPose;
        result.GripPose = s.GripPose;
        result.AimPoseFlags = s.AimPoseFlags;
        result.GripPoseFlags = s.GripPoseFlags;
        result.Trigger = s.Trigger;
        result.Grip = s.Grip;
        result.ThumbStick = s.ThumbStick;

        result.PrimaryButton = (s.Buttons & Buttons.X) != 0 || (s.Buttons & Buttons.A) != 0
            ? ButtonState.Pressed : ButtonState.Released;
        result.SecondaryButton = (s.Buttons & Buttons.Y) != 0 || (s.Buttons & Buttons.B) != 0
            ? ButtonState.Pressed : ButtonState.Released;
        result.MenuButton = (s.Buttons & Buttons.Start) != 0 || (s.Buttons & Buttons.Back) != 0
            ? ButtonState.Pressed : ButtonState.Released;
        result.ThumbStickButton = (s.Buttons & Buttons.LeftStick) != 0 || (s.Buttons & Buttons.RightStick) != 0
            ? ButtonState.Pressed : ButtonState.Released;

        return result;
    }

    /// <summary>
    /// Sends a haptic vibration pulse to the specified controller.
    /// </summary>
    public static bool SetVibration(XRHandedness hand, float amplitude)
    {
        if (!XRDevice.IsAvailable)
            return false;

        var instance = NativeGamePlatform.Instance;
        if (instance == null)
            return false;

        var platform = instance.Handle;
        if (platform == null)
            return false;

        return MGP.GamePad_SetVibration(platform, 0, amplitude, amplitude, 0, 0) != 0;
    }

    // Called from PollEvents() when a VRControllerPose event arrives
    internal static void UpdatePose(int hand, int poseType, XRPose pose, XRSpaceLocationFlags flags)
    {
        if (hand < 0 || hand > 1) return;

        var s = _state[hand];
        s.IsConnected = true;

        if (poseType == 0)
        {
            s.AimPose = pose;
            s.AimPoseFlags = flags;
        }
        else
        {
            s.GripPose = pose;
            s.GripPoseFlags = flags;
        }
    }

    // Called from PollEvents() for per-hand controller button/analog changes.
    // Uses the same ControllerInput enum and short value format as GamePad.
    internal static void ChangeState(int hand, ControllerInput input, short value)
    {
        if (hand < 0 || hand > 1) return;

        var s = _state[hand];
        s.IsConnected = true;

        if (input > ControllerInput.LAST_BUTTON)
        {
            float fval = value / 32767f;
            switch (input)
            {
                case ControllerInput.LeftStickX:
                case ControllerInput.RightStickX:
                    s.ThumbStick = new Vector2(fval, s.ThumbStick.Y);
                    break;
                case ControllerInput.LeftStickY:
                case ControllerInput.RightStickY:
                    s.ThumbStick = new Vector2(s.ThumbStick.X, fval);
                    break;
                case ControllerInput.LeftTrigger:
                case ControllerInput.RightTrigger:
                    s.Trigger = fval;
                    break;
            }
        }
        else
        {
            var button = InputToButton(input);
            if (value == 0)
                s.Buttons &= ~button;
            else
                s.Buttons |= button;

            // Track grip as analog from shoulder button events
            if (input == ControllerInput.LeftShoulder || input == ControllerInput.RightShoulder)
                s.Grip = value / 32767f;
        }
    }

    private static Buttons InputToButton(ControllerInput input)
    {
        switch (input)
        {
            case ControllerInput.A: return Buttons.A;
            case ControllerInput.B: return Buttons.B;
            case ControllerInput.X: return Buttons.X;
            case ControllerInput.Y: return Buttons.Y;
            case ControllerInput.Back: return Buttons.Back;
            case ControllerInput.Start: return Buttons.Start;
            case ControllerInput.LeftStick: return Buttons.LeftStick;
            case ControllerInput.RightStick: return Buttons.RightStick;
            case ControllerInput.LeftShoulder: return Buttons.LeftShoulder;
            case ControllerInput.RightShoulder: return Buttons.RightShoulder;
            default: return 0;
        }
    }

    // Called from GamePlatform to pass tracking context to native before polling
    internal static void UpdateTrackingState()
    {
        var instance = NativeGamePlatform.Instance;
        if (instance == null) return;

        var platform = instance.Handle;
        if (platform == null) return;

        MGP_Platform_SetVRTrackingState(platform, XRDevice.GetActiveSpace(), XRDevice.CurrentDisplayTime);
    }

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGP_Platform_SetVRTrackingState", ExactSpelling = true)]
    private static extern void MGP_Platform_SetVRTrackingState(MGP_Platform* platform, MGXR_Space* space, long displayTime);
}
