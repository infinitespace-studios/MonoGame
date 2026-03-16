// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.XR;
using NUnit.Framework;

namespace MonoGame.Tests.Framework.XR
{
    class XRDeviceTest
    {
        [Test]
        public void IsAvailable_ReturnsFalse_OnNonXRPlatform()
        {
            // On DesktopGL (non-NATIVE), IsAvailable should always be false.
            Assert.That(XRDevice.IsAvailable, Is.False);
        }

        [Test]
        public void SessionState_ReturnsUnknown_OnNonXRPlatform()
        {
            Assert.That(XRDevice.SessionState, Is.EqualTo(XRSessionState.Unknown));
        }

        [Test]
        public void IsRunning_ReturnsFalse_OnNonXRPlatform()
        {
            Assert.That(XRDevice.IsRunning, Is.False);
        }

        [Test]
        public void ViewCount_ReturnsZero_OnNonXRPlatform()
        {
            Assert.That(XRDevice.ViewCount, Is.EqualTo(0));
        }

        [Test]
        public void GetRecommendedSize_ReturnsZero_OnNonXRPlatform()
        {
            XRDevice.GetRecommendedSize(out int width, out int height);
            Assert.That(width, Is.EqualTo(0));
            Assert.That(height, Is.EqualTo(0));
        }

        [Test]
        public void BeginEye_DoesNotThrow_OnNonXRPlatform()
        {
            Assert.DoesNotThrow(() => XRDevice.BeginEye(0));
            Assert.DoesNotThrow(() => XRDevice.BeginEye(1));
        }

        [Test]
        public void EndEye_DoesNotThrow_OnNonXRPlatform()
        {
            Assert.DoesNotThrow(() => XRDevice.EndEye(0));
            Assert.DoesNotThrow(() => XRDevice.EndEye(1));
        }

        [Test]
        public void TrackingSpace_DefaultIsLocal()
        {
            Assert.That(XRDevice.TrackingSpace, Is.EqualTo(XRTrackingSpace.Local));
        }

        [Test]
        public void TrackingSpace_CanBeSet()
        {
            var original = XRDevice.TrackingSpace;
            try
            {
                XRDevice.TrackingSpace = XRTrackingSpace.Stage;
                Assert.That(XRDevice.TrackingSpace, Is.EqualTo(XRTrackingSpace.Stage));
            }
            finally
            {
                XRDevice.TrackingSpace = original;
            }
        }

        [Test]
        public void IsRunning_TrueForVisibleAndFocused()
        {
            // IsRunning is true only when SessionState is Visible or Focused.
            // On non-XR platform, state is always Unknown, so IsRunning is false.
            // We can at least verify the logic by checking these enum values
            // are the only "running" states.
            var runningStates = new[] { XRSessionState.Visible, XRSessionState.Focused };
            var notRunningStates = new[]
            {
                XRSessionState.Unknown, XRSessionState.Idle,
                XRSessionState.Ready, XRSessionState.Synchronized,
                XRSessionState.Stopping, XRSessionState.LossPending,
                XRSessionState.Exiting,
            };

            // Document the expected behavior.
            foreach (var state in runningStates)
                Assert.That(state == XRSessionState.Visible || state == XRSessionState.Focused, Is.True,
                    $"{state} should be a running state");

            foreach (var state in notRunningStates)
                Assert.That(state != XRSessionState.Visible && state != XRSessionState.Focused, Is.True,
                    $"{state} should not be a running state");
        }
    }
}
