// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
using System;
using System.Globalization;
using Microsoft.Xna.Framework.Media;
using NUnit.Framework;

namespace MonoGame.Tests.Graphics;

[NonParallelizable]
[RunOnUiTestFixture]
class VideoPlayerTests : GraphicsDeviceTestFixtureBase
{
    [Test]
    public void DefaultStateIsStopped()
    {
        WithPlayer((player) =>
        {
            Assert.AreEqual(MediaState.Stopped, player.State);
        });
    }

    [Test]
    public void DefaultPositionIsZero()
    {
        WithPlayer((player) =>
        {
            Assert.AreEqual(TimeSpan.Zero, player.PlayPosition);
        });
    }

    void WithPlayer(Action<VideoPlayer> action)
    {
        var player = new VideoPlayer();
        try
        {
             action(player);
        }
        finally
        {
            player.Dispose();
        }
    }
}
