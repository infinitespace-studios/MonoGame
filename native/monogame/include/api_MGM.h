// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
                
// This code is auto generated, don't modify it by hand.
// To regenerate it run: Tools/MonoGame.Generator.CTypes

#pragma once

#include "api_common.h"
#include "api_enums.h"
#include "api_structs.h"


struct MGM_Song;
struct MGA_System;
struct MGM_AudioDecoder;
struct MGM_Video;
struct MGG_GraphicsDevice;
struct MGG_Texture;

MG_EXPORT MGM_Song* MGM_Song_Create(const char* filepath, MGA_System* system, MGM_SongInfo& info);
MG_EXPORT void MGM_Song_Destroy(MGM_Song* song);
MG_EXPORT mgbyte MGM_Song_Play(MGM_Song* song, mgulong positionMS, mgulong generation);
MG_EXPORT void MGM_Song_Pause(MGM_Song* song);
MG_EXPORT void MGM_Song_Resume(MGM_Song* song);
MG_EXPORT void MGM_Song_Stop(MGM_Song* song);
MG_EXPORT void MGM_Song_SetVolume(MGM_Song* song, mgfloat volume);
MG_EXPORT mgulong MGM_Song_GetPosition(MGM_Song* song);
MG_EXPORT mgulong MGM_Song_GetDuration(MGM_Song* song);
MG_EXPORT mgbyte MGM_Song_TryDequeueEvent(MGM_Song* song, MGM_SongEvent& songEvent);
//MG_EXPORT MGM_AudioDecoder* MGM_AudioDecoder_Create(const char* filepath, MGM_AudioDecoderInfo& info);
//MG_EXPORT void MGM_AudioDecoder_Destroy(MGM_AudioDecoder* decoder);
//MG_EXPORT void MGM_AudioDecoder_SetPosition(MGM_AudioDecoder* decoder, mgulong timeMS);
//MG_EXPORT mgbyte MGM_AudioDecoder_Decode(MGM_AudioDecoder* decoder, mgbyte*& buffer, mguint& size);
MG_EXPORT MGM_Video* MGM_Video_Create(MGG_GraphicsDevice* device, const char* filepath, MGM_VideoInfo& info);
MG_EXPORT void MGM_Video_Destroy(MGM_Video* video);
MG_EXPORT mgbyte MGM_Video_Play(MGM_Video* song);
MG_EXPORT void MGM_Video_Pause(MGM_Video* song);
MG_EXPORT void MGM_Video_Resume(MGM_Video* song);
MG_EXPORT void MGM_Video_Stop(MGM_Video* song);
MG_EXPORT mgulong MGM_Video_GetPosition(MGM_Video* video);
MG_EXPORT mgint MGM_Video_GetState(MGM_Video* video);
MG_EXPORT void MGM_Video_SetLooped(MGM_Video* video, mgbyte looped);
MG_EXPORT MGG_Texture* MGM_Video_GetTexture(MGM_Video* video);
