// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.XR;

namespace Microsoft.Xna.Framework.Input;

/// <summary>
/// Represents the full input state of a single VR controller or tracked hand.
/// </summary>
public struct VRControllerState
{
    /// <summary>
    /// Whether this controller/hand is connected and providing data.
    /// </summary>
    public bool IsConnected;

    /// <summary>
    /// True when hand tracking is active (no physical controller).
    /// False when a physical controller is being used.
    /// </summary>
    public bool IsHandTracked;

    /// <summary>
    /// The aiming pose — represents where the controller is pointing.
    /// Use this as the origin and direction for ray casting.
    /// </summary>
    public XRPose AimPose;

    /// <summary>
    /// The grip pose — represents where the hand holds the controller.
    /// Use this for rendering controller or hand models.
    /// </summary>
    public XRPose GripPose;

    /// <summary>
    /// Tracking validity flags for the aim pose.
    /// Check <see cref="IsAimPoseValid"/> for a convenient boolean.
    /// </summary>
    public XRSpaceLocationFlags AimPoseFlags;

    /// <summary>
    /// Tracking validity flags for the grip pose.
    /// Check <see cref="IsGripPoseValid"/> for a convenient boolean.
    /// </summary>
    public XRSpaceLocationFlags GripPoseFlags;

    /// <summary>
    /// True when the aim pose has both valid position and orientation data.
    /// </summary>
    public readonly bool IsAimPoseValid =>
        (AimPoseFlags & (XRSpaceLocationFlags.PositionValid | XRSpaceLocationFlags.OrientationValid))
        == (XRSpaceLocationFlags.PositionValid | XRSpaceLocationFlags.OrientationValid);

    /// <summary>
    /// True when the grip pose has both valid position and orientation data.
    /// </summary>
    public readonly bool IsGripPoseValid =>
        (GripPoseFlags & (XRSpaceLocationFlags.PositionValid | XRSpaceLocationFlags.OrientationValid))
        == (XRSpaceLocationFlags.PositionValid | XRSpaceLocationFlags.OrientationValid);

    /// <summary>
    /// Primary face button: A (right hand) or X (left hand).
    /// </summary>
    public ButtonState PrimaryButton;

    /// <summary>
    /// Secondary face button: B (right hand) or Y (left hand).
    /// </summary>
    public ButtonState SecondaryButton;

    /// <summary>
    /// Menu/Start button.
    /// </summary>
    public ButtonState MenuButton;

    /// <summary>
    /// Thumbstick click (press the stick down).
    /// </summary>
    public ButtonState ThumbStickButton;

    /// <summary>
    /// Analog trigger value from 0 (released) to 1 (fully pressed).
    /// </summary>
    public float Trigger;

    /// <summary>
    /// Analog grip/squeeze value from 0 (released) to 1 (fully squeezed).
    /// </summary>
    public float Grip;

    /// <summary>
    /// Thumbstick position. X and Y range from -1 to 1.
    /// </summary>
    public Vector2 ThumbStick;

    // Hand joint data is stored internally and accessed via GetJointPose.
    // This keeps the struct size small when hand tracking is not in use.
    internal HandJointPose[] _joints;

    /// <summary>
    /// Gets the pose of a specific hand joint. Only valid when
    /// <see cref="IsHandTracked"/> is true.
    /// </summary>
    /// <param name="joint">The joint to query.</param>
    /// <returns>The joint pose, or a default pose if hand tracking is not active.</returns>
    public HandJointPose GetJointPose(HandJoint joint)
    {
        if (_joints == null || (int)joint >= _joints.Length)
            return default;

        return _joints[(int)joint];
    }
}
