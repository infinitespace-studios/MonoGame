// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Interop;


namespace Microsoft.Xna.Framework.Media;

public sealed partial class Video : IDisposable
{
    private unsafe MGM_Video* _video;
    private unsafe MGA_Voice* _voice;
    private bool _looped;
    private bool _muted;
    private float _volume = 1.0f;

    private unsafe void PlatformInitialize()
    {
        var absolutePath = MGP.Platform_MakePath(TitleContainer.Location, FileName);
        // Unsupported
        VideoSoundtrackType = VideoSoundtrackType.MusicAndDialog;
    }

    private unsafe void PlatformDispose(bool disposing)
    {
        Stop();
    }

    internal unsafe Texture2D GetTexture()
    {
        return null;//MGM.Video_GetTexture(_video);
    }

    internal unsafe MediaState State
    {
        get
        {
            if (_video ==null)
                return MediaState.Stopped;
            
            return (MediaState)MGM.Video_GetState(_video);
        }
    }

    internal unsafe TimeSpan Position
    {
        get
        {
            if (_video == null)
                return TimeSpan.Zero;

            var position = MGM.Video_GetPosition(_video);
            return TimeSpan.FromMilliseconds(position);
        }
    }

    internal unsafe bool IsLooped
    {
        set
        {
            _looped = value;
            if (_video != null)
                MGM.Video_SetLooped(_video, (byte)(value ? 1 : 0));
        }

        get
        {
            return _looped;
        }
    }

    internal unsafe bool IsMuted
    {
        set
        {
            if (value)
            {
                _muted = true;
                if (_voice != null)
                    MGA.Voice_SetVolume(_voice, 0.0f);
            }
            else
            {
                _muted = false;
                if (_voice != null)
                    MGA.Voice_SetVolume(_voice, _volume);
            }
        }

        get
        {
            return _muted;
        }
    }

    internal unsafe float Volume
    {
        set
        {
            _volume = value;
            if (!_muted && _voice != null)
                MGA.Voice_SetVolume(_voice, _volume);
        }

        get
        {
            return _volume;
        }
    }

    internal unsafe void Play()
    {
        if (_video == null)
            return;

        // Stop the current playback which cleans stuff up.
        Stop();

        MGM.Video_Play(_video);
        
    }

    internal unsafe void Pause()
    {
        if (_video == null)
            return;

        // Nothing to do if we're not playing.
        if (State != MediaState.Playing)
            return;

        MGM.Video_Pause(_video);
    }

    internal unsafe void Resume()
    {
        if (_video == null)
            return;

        // Nothing to do if we're not paused.
        if (State != MediaState.Paused)
            return;

        MGM.Video_Pause(_video);
    }

    internal unsafe void Stop()
    {
        if (_video == null)
            return;

        // Nothing to do if we're stopped already.
        if (State == MediaState.Stopped)
            return;

        MGM.Video_Stop(_video);
    }
}
