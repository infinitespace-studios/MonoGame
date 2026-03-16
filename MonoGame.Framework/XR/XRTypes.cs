// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

namespace Microsoft.Xna.Framework.XR;

/// <summary>
/// Represents a 6DOF pose in 3D space (position + orientation).
/// </summary>
public struct XRPose
{
    /// <summary>
    /// Position in 3D space.
    /// </summary>
    public Vector3 Position;

    /// <summary>
    /// Orientation as a quaternion.
    /// </summary>
    public Quaternion Orientation;

    /// <summary>
    /// Converts this pose to a view matrix for rendering.
    /// </summary>
    public Matrix ToViewMatrix()
    {
        var rotation = Matrix.CreateFromQuaternion(Orientation);
        var translation = Matrix.CreateTranslation(-Position);
        return translation * Matrix.Transpose(rotation);
    }

    /// <summary>
    /// Converts this pose to a world transform matrix for positioning objects.
    /// </summary>
    public Matrix ToMatrix()
    {
        var rotation = Matrix.CreateFromQuaternion(Orientation);
        var translation = Matrix.CreateTranslation(Position);
        return rotation * translation;
    }

    /// <summary>
    /// Returns an identity pose at the origin.
    /// </summary>
    public static XRPose Identity => new XRPose
    {
        Position = Vector3.Zero,
        Orientation = Quaternion.Identity,
    };
}

/// <summary>
/// Per-eye view and projection data for stereo rendering.
/// </summary>
public struct XRView
{
    /// <summary>
    /// The eye pose in tracking space.
    /// </summary>
    public XRPose Pose;

    /// <summary>
    /// The projection matrix for this eye.
    /// </summary>
    public Matrix ProjectionMatrix;

    /// <summary>
    /// The view matrix for this eye.
    /// </summary>
    public Matrix ViewMatrix;
}

/// <summary>
/// Handedness for VR controllers.
/// </summary>
public enum XRHandedness
{
    /// <summary>Left hand controller.</summary>
    Left = 0,

    /// <summary>Right hand controller.</summary>
    Right = 1,
}

/// <summary>
/// The state of an XR session.
/// </summary>
public enum XRSessionState
{
    /// <summary>Session state is unknown.</summary>
    Unknown = 0,
    /// <summary>Session is idle and not rendering.</summary>
    Idle = 1,
    /// <summary>Session is ready to begin.</summary>
    Ready = 2,
    /// <summary>Session is synchronized with the runtime.</summary>
    Synchronized = 3,
    /// <summary>Session content is visible to the user.</summary>
    Visible = 4,
    /// <summary>Session has input focus.</summary>
    Focused = 5,
    /// <summary>Session is stopping.</summary>
    Stopping = 6,
    /// <summary>Session is about to lose the XR runtime.</summary>
    LossPending = 7,
    /// <summary>Session is exiting.</summary>
    Exiting = 8,
}

/// <summary>
/// The type of tracking space origin.
/// </summary>
public enum XRTrackingSpace
{
    /// <summary>Head-relative space (recenters with the user).</summary>
    Local = 0,

    /// <summary>Room-scale space with a fixed floor-level origin.</summary>
    Stage = 1,
}

/// <summary>
/// Tracking validity flags from OpenXR xrLocateSpace.
/// Indicates whether position and orientation data are valid and actively tracked.
/// </summary>
[Flags]
public enum XRSpaceLocationFlags
{
    /// <summary>No flags set — pose data is not valid.</summary>
    None = 0,

    /// <summary>Orientation data is valid (may be predicted).</summary>
    OrientationValid = 1,

    /// <summary>Position data is valid (may be predicted).</summary>
    PositionValid = 2,

    /// <summary>Orientation is actively tracked (not just predicted).</summary>
    OrientationTracked = 4,

    /// <summary>Position is actively tracked (not just predicted).</summary>
    PositionTracked = 8,
}
