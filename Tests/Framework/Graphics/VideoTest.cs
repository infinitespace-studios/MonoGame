
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
class VideoTest : GraphicsDeviceTestFixtureBase
{
    [Test]
    [TestCase("video_h264_1280x720_5s", 60, "00:00:05.0160000", 1280, 720, VideoSoundtrackType.Music)]
    [TestCase("video_h264_2560x1440_15s", 60, "00:00:15.0180000", 2560, 1440, VideoSoundtrackType.Music)]
    public void Video_CanBeCreated(string videoFile, int fps, string duration, int width, int height, VideoSoundtrackType vstype)
    {
        var video = LoadVideo(videoFile);
        Assert.IsNotNull(video);
        Assert.AreEqual(fps, video.FramesPerSecond);
        Assert.AreEqual(TimeSpan.Parse(duration, CultureInfo.InvariantCulture), video.Duration);
        Assert.AreEqual(height, video.Height);
        Assert.AreEqual(width, video.Width);
	Assert.AreEqual(vstype, video.VideoSoundtrackType);
    }

    private Video LoadVideo(string filename)
    {
        return content.Load<Video>(Paths.Video(filename));
    }
}
