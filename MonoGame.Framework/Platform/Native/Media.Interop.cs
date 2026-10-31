// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Runtime.InteropServices;


namespace MonoGame.Interop;


[MGHandle]
internal readonly struct MGM_AudioDecoder{ }

[MGHandle]
internal readonly struct MGM_VideoDecoder { }

[MGHandle]
internal readonly struct MGM_Song { }

[MGHandle]
internal readonly struct MGM_Video {}

internal enum SongEventType
{
    Completed,
    Failed,
}

struct MGM_SongInfo
{
    public ulong duration;
}

struct MGM_SongEvent
{
    public SongEventType type;
    public ulong generation;
}

struct MGM_AudioDecoderInfo
{
    /// <summary>
    /// The audio samples per second, per channel.
    /// </summary>
    public int samplerate;

    /// <summary>
    /// The number of audio channels (typically 1 or 2).
    /// </summary>
    public int channels;

    /// <summary>
    /// The estimated duration of the audio in milliseconds.
    /// </summary>
    public ulong duration;
}


struct MGM_VideoInfo
{
    /// <summary>
    /// The width of the video frames.
    /// </summary>
    public int width;

    /// <summary>
    /// The height of the video frames.
    /// </summary>
    public int height;

    /// <summary>
    /// The number of frames per second.
    /// </summary>
    public float fps;

    /// <summary>
    /// The estimated duration of the video in milliseconds.
    /// </summary>
    public ulong duration;
}


internal static unsafe partial class MGM
{
    #region Song

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_Create", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    public static extern MGM_Song* Song_Create([MarshalAs(UnmanagedType.LPUTF8Str)] string filepath, MGA_System* system, out MGM_SongInfo info);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_Destroy", ExactSpelling = true)]
    public static extern void Song_Destroy(MGM_Song* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_Play", ExactSpelling = true)]
    public static extern byte Song_Play(MGM_Song* song, ulong positionMS, ulong generation);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_Pause", ExactSpelling = true)]
    public static extern void Song_Pause(MGM_Song* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_Resume", ExactSpelling = true)]
    public static extern void Song_Resume(MGM_Song* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_Stop", ExactSpelling = true)]
    public static extern void Song_Stop(MGM_Song* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_SetVolume", ExactSpelling = true)]
    public static extern void Song_SetVolume(MGM_Song* song, float volume);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_GetPosition", ExactSpelling = true)]
    public static extern ulong Song_GetPosition(MGM_Song* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_GetDuration", ExactSpelling = true)]
    public static extern ulong Song_GetDuration(MGM_Song* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Song_TryDequeueEvent", ExactSpelling = true)]
    public static extern byte Song_TryDequeueEvent(MGM_Song* song, out MGM_SongEvent songEvent);

    #endregion

    #region Video

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_Create", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    public static extern MGM_Video* Video_Create(MGG_GraphicsDevice* device, [MarshalAs(UnmanagedType.LPUTF8Str)] string filepath, out MGM_VideoInfo info);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_Destroy", ExactSpelling = true)]
    public static extern void Video_Destroy(MGM_Video* video);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_Play", ExactSpelling = true)]
    public static extern byte Video_Play(MGM_Video* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_Pause", ExactSpelling = true)]
    public static extern void Video_Pause(MGM_Video* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_Resume", ExactSpelling = true)]
    public static extern void Video_Resume(MGM_Video* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_Stop", ExactSpelling = true)]
    public static extern void Video_Stop(MGM_Video* song);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_GetPosition", ExactSpelling = true)]
    public static extern ulong Video_GetPosition(MGM_Video* video);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_GetState", ExactSpelling = true)]
    public static extern int Video_GetState(MGM_Video* video);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_SetLooped", ExactSpelling = true)]
    public static extern void Video_SetLooped(MGM_Video* video, byte looped);

    [DllImport(MGP.MonoGameNativeDLL, EntryPoint = "MGM_Video_GetTexture", ExactSpelling = true)]
    public static extern MGG_Texture* Video_GetTexture(MGM_Video* video);

    #endregion
}
