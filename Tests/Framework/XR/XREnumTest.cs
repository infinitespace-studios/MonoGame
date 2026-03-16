// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.XR;
using NUnit.Framework;

namespace MonoGame.Tests.Framework.XR
{
    class XREnumTest
    {
        [Test]
        public void XRSessionState_ValuesMatchOpenXRSpec()
        {
            // OpenXR XrSessionState values:
            // XR_SESSION_STATE_UNKNOWN = 0
            // XR_SESSION_STATE_IDLE = 1
            // XR_SESSION_STATE_READY = 2
            // XR_SESSION_STATE_SYNCHRONIZED = 3
            // XR_SESSION_STATE_VISIBLE = 4
            // XR_SESSION_STATE_FOCUSED = 5
            // XR_SESSION_STATE_STOPPING = 6
            // XR_SESSION_STATE_LOSS_PENDING = 7
            // XR_SESSION_STATE_EXITING = 8
            Assert.That((int)XRSessionState.Unknown, Is.EqualTo(0));
            Assert.That((int)XRSessionState.Idle, Is.EqualTo(1));
            Assert.That((int)XRSessionState.Ready, Is.EqualTo(2));
            Assert.That((int)XRSessionState.Synchronized, Is.EqualTo(3));
            Assert.That((int)XRSessionState.Visible, Is.EqualTo(4));
            Assert.That((int)XRSessionState.Focused, Is.EqualTo(5));
            Assert.That((int)XRSessionState.Stopping, Is.EqualTo(6));
            Assert.That((int)XRSessionState.LossPending, Is.EqualTo(7));
            Assert.That((int)XRSessionState.Exiting, Is.EqualTo(8));
        }

        [Test]
        public void XRSessionState_HasExpectedCount()
        {
            var values = System.Enum.GetValues(typeof(XRSessionState));
            Assert.That(values.Length, Is.EqualTo(9));
        }

        [Test]
        public void XRHandedness_Values()
        {
            Assert.That((int)XRHandedness.Left, Is.EqualTo(0));
            Assert.That((int)XRHandedness.Right, Is.EqualTo(1));
        }

        [Test]
        public void XRHandedness_HasExpectedCount()
        {
            var values = System.Enum.GetValues(typeof(XRHandedness));
            Assert.That(values.Length, Is.EqualTo(2));
        }

        [Test]
        public void XRTrackingSpace_Values()
        {
            Assert.That((int)XRTrackingSpace.Local, Is.EqualTo(0));
            Assert.That((int)XRTrackingSpace.Stage, Is.EqualTo(1));
        }

        [Test]
        public void XRTrackingSpace_HasExpectedCount()
        {
            var values = System.Enum.GetValues(typeof(XRTrackingSpace));
            Assert.That(values.Length, Is.EqualTo(2));
        }

        [Test]
        public void XRPose_StructLayout()
        {
            // XRPose should be blittable: Vector3 (12 bytes) + Quaternion (16 bytes) = 28 bytes.
            int size = Marshal.SizeOf<XRPose>();
            Assert.That(size, Is.EqualTo(28));
        }

        [Test]
        public void XRView_StructLayout()
        {
            // XRView = XRPose (28 bytes) + Matrix (64 bytes) + Matrix (64 bytes) = 156 bytes.
            int size = Marshal.SizeOf<XRView>();
            Assert.That(size, Is.EqualTo(156));
        }
    }
}
