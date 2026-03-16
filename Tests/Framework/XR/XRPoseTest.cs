// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.XR;
using NUnit.Framework;

namespace MonoGame.Tests.Framework.XR
{
    class XRPoseTest
    {
        [Test]
        public void Identity_HasZeroPositionAndIdentityOrientation()
        {
            var pose = XRPose.Identity;

            Assert.That(pose.Position.X, Is.EqualTo(0f));
            Assert.That(pose.Position.Y, Is.EqualTo(0f));
            Assert.That(pose.Position.Z, Is.EqualTo(0f));

            Assert.That(pose.Orientation.X, Is.EqualTo(0f));
            Assert.That(pose.Orientation.Y, Is.EqualTo(0f));
            Assert.That(pose.Orientation.Z, Is.EqualTo(0f));
            Assert.That(pose.Orientation.W, Is.EqualTo(1f));
        }

        [Test]
        public void ToViewMatrix_Identity_ReturnsIdentity()
        {
            var pose = XRPose.Identity;
            var view = pose.ToViewMatrix();

            Assert.That(view, Is.EqualTo(Matrix.Identity).Using(MatrixComparer.Epsilon));
        }

        [Test]
        public void ToViewMatrix_TranslationOnly_NegatesPosition()
        {
            // A camera at (1, 2, 3) should produce a view matrix that
            // translates the world by (-1, -2, -3).
            var pose = new XRPose
            {
                Position = new System.Numerics.Vector3(1f, 2f, 3f),
                Orientation = System.Numerics.Quaternion.Identity,
            };

            var view = pose.ToViewMatrix();

            // With identity rotation, the view matrix is just the translation.
            // view = Translation(-pos) * Transpose(I) = Translation(-pos)
            var expected = Matrix.CreateTranslation(-1f, -2f, -3f);
            Assert.That(view, Is.EqualTo(expected).Using(MatrixComparer.Epsilon));
        }

        [Test]
        public void ToViewMatrix_RotationOnly_TransposesRotation()
        {
            // A 90-degree rotation around Y axis.
            float angle = MathHelper.PiOver2;
            var q = System.Numerics.Quaternion.CreateFromAxisAngle(
                System.Numerics.Vector3.UnitY, angle);

            var pose = new XRPose
            {
                Position = System.Numerics.Vector3.Zero,
                Orientation = q,
            };

            var view = pose.ToViewMatrix();

            // The view matrix should be the transpose (inverse) of the rotation.
            var rotation = Matrix.CreateFromQuaternion(new Quaternion(q.X, q.Y, q.Z, q.W));
            var expected = Matrix.Transpose(rotation);
            Assert.That(view, Is.EqualTo(expected).Using(MatrixComparer.Epsilon));
        }

        [Test]
        public void ToViewMatrix_Combined_TranslationThenRotation()
        {
            // Camera at (5, 0, 0), rotated 90 degrees around Y.
            float angle = MathHelper.PiOver2;
            var q = System.Numerics.Quaternion.CreateFromAxisAngle(
                System.Numerics.Vector3.UnitY, angle);

            var pose = new XRPose
            {
                Position = new System.Numerics.Vector3(5f, 0f, 0f),
                Orientation = q,
            };

            var view = pose.ToViewMatrix();

            // view = Translation(-pos) * Transpose(rotation)
            var translation = Matrix.CreateTranslation(-5f, 0f, 0f);
            var rotation = Matrix.CreateFromQuaternion(new Quaternion(q.X, q.Y, q.Z, q.W));
            var expected = translation * Matrix.Transpose(rotation);

            Assert.That(view, Is.EqualTo(expected).Using(MatrixComparer.Epsilon));
        }

        [Test]
        public void ToViewMatrix_IsInvertible()
        {
            // Any valid pose should produce an invertible view matrix.
            var q = System.Numerics.Quaternion.CreateFromAxisAngle(
                new System.Numerics.Vector3(1, 1, 0), 0.7f);
            q = System.Numerics.Quaternion.Normalize(q);

            var pose = new XRPose
            {
                Position = new System.Numerics.Vector3(3f, -1f, 7f),
                Orientation = q,
            };

            var view = pose.ToViewMatrix();
            var det = view.Determinant();

            // Determinant should be non-zero (invertible).
            Assert.That(MathF.Abs(det), Is.GreaterThan(0.001f));
        }

        [Test]
        public void ToViewMatrix_OriginTransformedToZero()
        {
            // Transforming the camera position by the view matrix should
            // give a point at or near the origin.
            var pose = new XRPose
            {
                Position = new System.Numerics.Vector3(10f, -5f, 3f),
                Orientation = System.Numerics.Quaternion.Identity,
            };

            var view = pose.ToViewMatrix();
            var cameraWorldPos = new Vector4(10f, -5f, 3f, 1f);
            var transformed = Vector4.Transform(cameraWorldPos, view);

            Assert.That(transformed.X, Is.EqualTo(0f).Using(FloatComparer.Epsilon));
            Assert.That(transformed.Y, Is.EqualTo(0f).Using(FloatComparer.Epsilon));
            Assert.That(transformed.Z, Is.EqualTo(0f).Using(FloatComparer.Epsilon));
        }

        [Test]
        public void DefaultPose_HasZeroValues()
        {
            var pose = new XRPose();

            Assert.That(pose.Position.X, Is.EqualTo(0f));
            Assert.That(pose.Position.Y, Is.EqualTo(0f));
            Assert.That(pose.Position.Z, Is.EqualTo(0f));
            Assert.That(pose.Orientation.X, Is.EqualTo(0f));
            Assert.That(pose.Orientation.Y, Is.EqualTo(0f));
            Assert.That(pose.Orientation.Z, Is.EqualTo(0f));
            Assert.That(pose.Orientation.W, Is.EqualTo(0f));
        }
    }
}
