// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.XR;
using NUnit.Framework;

namespace MonoGame.Tests.Framework.XR
{
    class XRProjectionTest
    {
        [Test]
        public void SymmetricFov_ProducesSymmetricMatrix()
        {
            // Symmetric 90-degree FOV (45 degrees each direction).
            float angle = MathHelper.PiOver4; // 45 degrees
            var matrix = XRDevice.CreateProjectionFov(
                -angle, angle, angle, -angle,
                0.01f, 1000.0f);

            // For symmetric FOV: M31 and M32 should be zero (no off-center shift).
            Assert.That(matrix.M31, Is.EqualTo(0f).Using(FloatComparer.Epsilon));
            Assert.That(matrix.M32, Is.EqualTo(0f).Using(FloatComparer.Epsilon));
        }

        [Test]
        public void PerspectiveDivide_M34IsNegativeOne()
        {
            // All perspective projection matrices should have M34 = -1.
            float angle = MathHelper.PiOver4;
            var matrix = XRDevice.CreateProjectionFov(
                -angle, angle, angle, -angle,
                0.01f, 1000.0f);

            Assert.That(matrix.M34, Is.EqualTo(-1f).Using(FloatComparer.Epsilon));
        }

        [Test]
        public void UnusedElements_AreZero()
        {
            // M12, M13, M14, M21, M23, M24, M41, M42, M44 should all be zero.
            float angle = MathHelper.PiOver4;
            var matrix = XRDevice.CreateProjectionFov(
                -angle, angle, angle, -angle,
                0.01f, 1000.0f);

            Assert.That(matrix.M12, Is.EqualTo(0f));
            Assert.That(matrix.M13, Is.EqualTo(0f));
            Assert.That(matrix.M14, Is.EqualTo(0f));
            Assert.That(matrix.M21, Is.EqualTo(0f));
            Assert.That(matrix.M23, Is.EqualTo(0f));
            Assert.That(matrix.M24, Is.EqualTo(0f));
            Assert.That(matrix.M41, Is.EqualTo(0f));
            Assert.That(matrix.M42, Is.EqualTo(0f));
            Assert.That(matrix.M44, Is.EqualTo(0f));
        }

        [Test]
        public void NearFarPlanes_AffectM33AndM43()
        {
            float angle = MathHelper.PiOver4;
            float near = 0.1f;
            float far = 100.0f;

            var matrix = XRDevice.CreateProjectionFov(
                -angle, angle, angle, -angle,
                near, far);

            // M33 = -(far + near) / (far - near)
            float expectedM33 = -(far + near) / (far - near);
            Assert.That(matrix.M33, Is.EqualTo(expectedM33).Using(FloatComparer.Epsilon));

            // M43 = -(2 * far * near) / (far - near)
            float expectedM43 = -(2.0f * far * near) / (far - near);
            Assert.That(matrix.M43, Is.EqualTo(expectedM43).Using(FloatComparer.Epsilon));
        }

        [Test]
        public void AsymmetricFov_ProducesOffCenterShift()
        {
            // Typical VR headset has asymmetric FOV (more inward than outward).
            float left = -0.9f;   // ~51 degrees
            float right = 0.8f;   // ~46 degrees
            float up = 0.85f;     // ~48 degrees
            float down = -0.85f;  // ~48 degrees

            var matrix = XRDevice.CreateProjectionFov(
                left, right, up, down,
                0.01f, 1000.0f);

            // Asymmetric horizontal FOV should produce non-zero M31.
            float tanLeft = MathF.Tan(left);
            float tanRight = MathF.Tan(right);
            float width = tanRight - tanLeft;
            float expectedM31 = (tanRight + tanLeft) / width;

            Assert.That(matrix.M31, Is.EqualTo(expectedM31).Using(FloatComparer.Epsilon));
            Assert.That(matrix.M31, Is.Not.EqualTo(0f).Using(FloatComparer.Epsilon));
        }

        [Test]
        public void DiagonalElements_ArePositive()
        {
            // M11 and M22 should always be positive for a valid projection.
            float angle = MathHelper.PiOver4;
            var matrix = XRDevice.CreateProjectionFov(
                -angle, angle, angle, -angle,
                0.01f, 1000.0f);

            Assert.That(matrix.M11, Is.GreaterThan(0f));
            Assert.That(matrix.M22, Is.GreaterThan(0f));
        }

        [Test]
        public void SymmetricFov_MatchesKnownValues()
        {
            // For a symmetric 90-degree FOV with tan(45°)=1:
            // width = tan(45°) - tan(-45°) = 1 - (-1) = 2
            // M11 = 2/width = 1
            // M22 = 2/height = 1
            float angle = MathHelper.PiOver4;
            var matrix = XRDevice.CreateProjectionFov(
                -angle, angle, angle, -angle,
                0.01f, 1000.0f);

            Assert.That(matrix.M11, Is.EqualTo(1f).Using(FloatComparer.Epsilon));
            Assert.That(matrix.M22, Is.EqualTo(1f).Using(FloatComparer.Epsilon));
        }

        [Test]
        public void DifferentNearFar_ChangeDepthRange()
        {
            float angle = MathHelper.PiOver4;

            var narrow = XRDevice.CreateProjectionFov(
                -angle, angle, angle, -angle,
                0.1f, 10.0f);

            var wide = XRDevice.CreateProjectionFov(
                -angle, angle, angle, -angle,
                0.1f, 10000.0f);

            // Wider far plane should produce M33 closer to -1.
            Assert.That(MathF.Abs(wide.M33), Is.GreaterThan(MathF.Abs(narrow.M33) - 0.1f));
        }
    }
}
