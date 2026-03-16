// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.XR;

namespace Microsoft.Xna.Framework.Input;

/// <summary>
/// Represents the tracked pose of a single hand joint.
/// </summary>
public struct HandJointPose
{
    /// <summary>
    /// The position and orientation of this joint in tracking space.
    /// </summary>
    public XRPose Pose;

    /// <summary>
    /// The radius of the joint in meters (useful for collision volumes).
    /// </summary>
    public float Radius;

    /// <summary>
    /// Whether this joint has valid tracking data this frame.
    /// </summary>
    public bool IsTracked;
}
