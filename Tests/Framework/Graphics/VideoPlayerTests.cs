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
            Assert.IsFalse(player.IsDisposed);
            Assert.IsFalse(player.IsLooped);
            Assert.IsFalse(player.IsMuted);
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

    [Test]
    public void DefaultVolumeIsOne()
    {
        WithPlayer((player) =>
        {
            Assert.AreEqual(1.0f, player.Volume);
        });
    }

    [Test]
    public void DefaultVideoIsNull()
    {
        WithPlayer((player) =>
        {
            Assert.IsNull(player.Video);
        });
    }

    [Test]
    public void LoadedVideoReturnsCorrectPosition()
    {
        WithPlayer((player)=>
        {
            var video = LoadVideo("video_h264_1280x720_5s");
            Assert.IsNotNull(video);
            player.Play(video);
            Assert.AreEqual(MediaState.Playing, player.State);
            player.Pause();
            Assert.AreEqual(MediaState.Paused, player.State);
            player.Stop();
            Assert.AreEqual(MediaState.Stopped, player.State);
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

    private Video LoadVideo(string filename)
    {
        return content.Load<Video>(Paths.Video(filename));
    }
}
