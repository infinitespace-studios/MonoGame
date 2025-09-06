// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#include "api_MGA.h"

#include "mg_common.h"

#include <vector>

// Include the FAudio headers
#include <FAudio.h>
#include <FAudioFX.h>
#include <F3DAudio.h>

struct MGA_System
{
    FAudio* audio = nullptr;
    FAudioMasteringVoice* masterVoice = nullptr;
    FAudioSubmixVoice* reverbVoice = nullptr;
    alignas(16) uint8_t f3daudio[F3DAUDIO_HANDLE_BYTESIZE]; // Use aligned bytes for handle
};

struct MGA_Buffer
{
    FAudioWaveFormatEx* format = nullptr;
    FAudioBuffer buffer;
    uint8_t* data = nullptr;
    uint32_t length = 0;
};

struct MGA_Voice
{
    MGA_System* system = nullptr;
    
    FAudioSourceVoice* voice = nullptr;
    
    MGA_Buffer* buffer = nullptr;
    
    float pan = 0.0f;
    float reverbMix = 0.0f;
    bool looped = false;
    bool paused = false;
};

static std::vector<MGA_Buffer*> s_FreeStreamingBuffers;

MGA_System* MGA_System_Create()
{
    auto system = new MGA_System();
    
    uint32_t flags = 0;
    uint32_t err = FAudioCreate(&system->audio, flags, FAUDIO_DEFAULT_PROCESSOR);
    assert(err == 0);
    
#ifdef _DEBUG
    // Enable debugging features
    FAudioDebugConfiguration debug = { 0 };
    debug.TraceMask = FAUDIO_LOG_ERRORS | FAUDIO_LOG_WARNINGS;
    debug.BreakMask = FAUDIO_LOG_ERRORS;
    FAudio_SetDebugConfiguration(system->audio, &debug, nullptr);
#endif
    
    err = FAudio_CreateMasteringVoice(
        system->audio,
        &system->masterVoice,
        FAUDIO_DEFAULT_CHANNELS,
        FAUDIO_DEFAULT_SAMPLERATE,
        0,
        0, // DeviceIndex
        nullptr // EffectChain
    );
    assert(err == 0);
    
    FAudioVoiceDetails details;
    memset(&details, 0, sizeof(details));
    FAudioVoice_GetVoiceDetails(system->masterVoice, &details);
    
    err = FAudio_CreateSubmixVoice(
        system->audio,
        &system->reverbVoice,
        details.InputChannels,
        details.InputSampleRate,
        0,
        0,
        nullptr,
        nullptr
    );
    assert(err == 0);
    
    // Set up reverb effect
    FAudioEffectDescriptor desc;
    FAudioEffectChain chain;
    desc.InitialState = 1;
    desc.OutputChannels = details.InputChannels;
    
    FAudioCreateReverb(&desc.pEffect, 0);
    assert(desc.pEffect != nullptr);
    
    chain.EffectCount = 1;
    chain.pEffectDescriptors = &desc;
    err = FAudioVoice_SetEffectChain(system->reverbVoice, &chain);
    assert(err == 0);
    
    // Initialize 3D audio
    uint32_t channelMask = SPEAKER_STEREO;
    float speedOfSound = 343.5f; // meters per second
    F3DAudioInitialize(channelMask, speedOfSound, system->f3daudio);
    
    return system;
}

void MGA_System_Destroy(MGA_System* system)
{
    assert(system != nullptr);
    
    // Release the streaming buffers.
    for (auto free : s_FreeStreamingBuffers)
        MGA_Buffer_Destroy(free);
    s_FreeStreamingBuffers.clear();
    
    if (system->reverbVoice)
        FAudioVoice_DestroyVoice(system->reverbVoice);
    
    if (system->masterVoice)
        FAudioVoice_DestroyVoice(system->masterVoice);
    
    if (system->audio)
        FAudio_Release(system->audio);
    
    delete system;
}

mgint MGA_System_GetMaxInstances()
{
    // Return a reasonable value (same as used in XAudio2 implementation)
    return 256;
}

void MGA_System_SetReverbSettings(MGA_System* system, ReverbSettings& settings)
{
    assert(system != nullptr);
    
    FAudioVoiceDetails details;
    memset(&details, 0, sizeof(details));
    FAudioVoice_GetVoiceDetails(system->reverbVoice, &details);
    
    // All parameters related to sampling rate or time are relative to a 48kHz 
    // voice and must be scaled for use with other sampling rates.
    float timeScale = 48000.0f / details.InputSampleRate;
    
    FAudioFXReverbParameters params;
    params.ReflectionsDelay = uint32_t(settings.ReflectionsDelayMs * timeScale);
    params.ReverbDelay = uint8_t(settings.ReverbDelayMs * timeScale);
    params.RearDelay = uint8_t(settings.RearDelayMs * timeScale);
    params.PositionLeft = uint8_t(settings.PositionLeft);
    params.PositionRight = uint8_t(settings.PositionRight);
    params.PositionMatrixLeft = uint8_t(settings.PositionLeftMatrix);
    params.PositionMatrixRight = uint8_t(settings.PositionRightMatrix);
    params.EarlyDiffusion = uint8_t(settings.EarlyDiffusion);
    params.LateDiffusion = uint8_t(settings.LateDiffusion);
    params.LowEQGain = uint8_t(settings.LowEqGain);
    params.LowEQCutoff = uint8_t(settings.LowEqCutoff);
    params.HighEQGain = uint8_t(settings.HighEqGain);
    params.HighEQCutoff = uint8_t(settings.HighEqCutoff);
    params.RoomFilterFreq = settings.RoomFilterFrequencyHz * timeScale;
    params.RoomFilterMain = settings.RoomFilterMainDb;
    params.RoomFilterHF = settings.RoomFilterHighFrequencyDb;
    params.ReflectionsGain = settings.ReflectionsGainDb;
    params.ReverbGain = settings.ReverbGainDb;
    params.DecayTime = settings.DecayTimeSec;
    params.Density = settings.DensityPct;
    params.RoomSize = settings.RoomSizeFeet;
    params.WetDryMix = settings.WetDryMixPct;
    
    uint32_t err = FAudioVoice_SetEffectParameters(system->reverbVoice, 0, &params, sizeof(params), 0);
    assert(err == 0);
}

MGA_Buffer* MGA_Buffer_Create(MGA_System* system)
{
    assert(system != nullptr);
    auto buffer = new MGA_Buffer();
    return buffer;
}

void MGA_Buffer_Destroy(MGA_Buffer* buffer)
{
    assert(buffer != nullptr);
    
    free(buffer->data);
    free(buffer->format);
    
    delete buffer;
}

void MGA_Buffer_InitializeFormat(MGA_Buffer* buffer, mgbyte* waveHeader, mgbyte* waveData, mgint length, mgint loopStart, mgint loopLength)
{
    assert(buffer != nullptr);
    assert(waveHeader != nullptr);
    assert(waveData != nullptr);
    assert(length > 0);
    
    auto wformat = (FAudioWaveFormatEx*)waveHeader;
    
    if (wformat->wFormatTag == 2) // WAVE_FORMAT_ADPCM
    {
        // Handle MSADPCM format
        const size_t size = sizeof(FAudioADPCMWaveFormat) + (7 * sizeof(FAudioADPCMCoefSet));
        auto format = (FAudioADPCMWaveFormat*)malloc(size);
        memset(format, 0, size);
        format->wfx.wFormatTag = 2; // WAVE_FORMAT_ADPCM
        format->wfx.nSamplesPerSec = wformat->nSamplesPerSec;
        format->wfx.nChannels = wformat->nChannels;
        format->wfx.nBlockAlign = wformat->nBlockAlign;
        format->wfx.wBitsPerSample = wformat->wBitsPerSample;
        format->wfx.nAvgBytesPerSec = wformat->nAvgBytesPerSec;
        format->wfx.cbSize = size - sizeof(FAudioWaveFormatEx);
        format->wSamplesPerBlock = (format->wfx.nBlockAlign / format->wfx.nChannels - 7) * 2 + 2;
        format->wNumCoef = 7;
        format->aCoef[0].iCoef1 = 256;
        format->aCoef[0].iCoef2 = 0;
        format->aCoef[1].iCoef1 = 512;
        format->aCoef[1].iCoef2 = -256;
        format->aCoef[2].iCoef1 = 0;
        format->aCoef[2].iCoef2 = 0;
        format->aCoef[3].iCoef1 = 192;
        format->aCoef[3].iCoef2 = 64;
        format->aCoef[4].iCoef1 = 240;
        format->aCoef[4].iCoef2 = 0;
        format->aCoef[5].iCoef1 = 460;
        format->aCoef[5].iCoef2 = -208;
        format->aCoef[6].iCoef1 = 392;
        format->aCoef[6].iCoef2 = -232;
        
        // FAudio supports a higher block size than XAudio2
        buffer->format = (FAudioWaveFormatEx*)format;
        
        // Buffer should be block aligned
        assert((length % wformat->nBlockAlign) == 0);
        
        buffer->length = length;
        buffer->data = (uint8_t*)malloc(length);
        memcpy(buffer->data, waveData, length);
        
        memset(&buffer->buffer, 0, sizeof(FAudioBuffer));
        buffer->buffer.pAudioData = buffer->data;
        buffer->buffer.AudioBytes = length;
        buffer->buffer.LoopBegin = loopStart;
        buffer->buffer.LoopLength = loopLength;
        buffer->buffer.LoopCount = 0;
        buffer->buffer.Flags = 0;
        buffer->buffer.pContext = nullptr;
    }
    else 
    {
        // Handle PCM or other formats
        const size_t size = sizeof(FAudioWaveFormatEx) + wformat->cbSize;
        buffer->format = (FAudioWaveFormatEx*)malloc(size);
        memcpy(buffer->format, wformat, size);
        
        buffer->length = length;
        buffer->data = (uint8_t*)malloc(length);
        memcpy(buffer->data, waveData, length);
        
        memset(&buffer->buffer, 0, sizeof(FAudioBuffer));
        buffer->buffer.pAudioData = buffer->data;
        buffer->buffer.AudioBytes = length;
        buffer->buffer.LoopBegin = loopStart;
        buffer->buffer.LoopLength = loopLength;
        buffer->buffer.LoopCount = 0;
        buffer->buffer.Flags = 0;
        buffer->buffer.pContext = nullptr;
    }
}

void MGA_Buffer_InitializePCM(MGA_Buffer* buffer, mgbyte* waveData, mgint offset, mgint length, mgint sampleBits, mgint sampleRate, mgint channels, mgint loopStart, mgint loopLength)
{
    assert(buffer != nullptr);
    assert(waveData != nullptr);
    assert(offset >= 0);
    assert(length > 0);
    
    auto format = (FAudioWaveFormatEx*)malloc(sizeof(FAudioWaveFormatEx));
    memset(format, 0, sizeof(FAudioWaveFormatEx));
    format->wFormatTag = 1; // WAVE_FORMAT_PCM
    format->nSamplesPerSec = sampleRate;
    format->nChannels = channels;
    format->nBlockAlign = channels * (sampleBits / 8);
    format->wBitsPerSample = sampleBits;
    format->nAvgBytesPerSec = format->nSamplesPerSec * format->nBlockAlign;
    format->cbSize = 0;
    buffer->format = format;
    
    // Buffer should be block aligned
    assert((length % format->nBlockAlign) == 0);
    
    buffer->length = length;
    buffer->data = (uint8_t*)malloc(length);
    if (waveData)
        memcpy(buffer->data, waveData + offset, length);
    
    memset(&buffer->buffer, 0, sizeof(FAudioBuffer));
    buffer->buffer.pAudioData = buffer->data;
    buffer->buffer.AudioBytes = length;
    buffer->buffer.LoopBegin = loopStart;
    buffer->buffer.LoopLength = loopLength;
    buffer->buffer.LoopCount = 0;
    buffer->buffer.Flags = 0;
    buffer->buffer.pContext = nullptr;
}

void MGA_Buffer_InitializeXact(MGA_Buffer* buffer, mguint codec, mgbyte* waveData, mgint length, mgint sampleRate, mgint blockAlignment, mgint channels, mgint loopStart, mgint loopLength)
{
    assert(buffer != nullptr);
    assert(waveData != nullptr);
    assert(length > 0);
    
    if (codec == 0x2) // Adpcm
    {
        // Handle MSADPCM format
        const size_t size = sizeof(FAudioADPCMWaveFormat) + (7 * sizeof(FAudioADPCMCoefSet));
        auto format = (FAudioADPCMWaveFormat*)malloc(size);
        memset(format, 0, size);
        format->wfx.wFormatTag = 2; // WAVE_FORMAT_ADPCM
        format->wfx.nSamplesPerSec = sampleRate;
        format->wfx.nChannels = channels;
        format->wfx.nBlockAlign = blockAlignment;
        format->wfx.wBitsPerSample = 4; // ADPCM is typically 4 bits per sample
        format->wfx.nAvgBytesPerSec = (sampleRate * 4) / 8;
        format->wfx.cbSize = size - sizeof(FAudioWaveFormatEx);
        format->wSamplesPerBlock = (blockAlignment * 2) / (channels) - 12;
        format->wNumCoef = 7;
        format->aCoef[0].iCoef1 = 256;
        format->aCoef[0].iCoef2 = 0;
        format->aCoef[1].iCoef1 = 512;
        format->aCoef[1].iCoef2 = -256;
        format->aCoef[2].iCoef1 = 0;
        format->aCoef[2].iCoef2 = 0;
        format->aCoef[3].iCoef1 = 192;
        format->aCoef[3].iCoef2 = 64;
        format->aCoef[4].iCoef1 = 240;
        format->aCoef[4].iCoef2 = 0;
        format->aCoef[5].iCoef1 = 460;
        format->aCoef[5].iCoef2 = -208;
        format->aCoef[6].iCoef1 = 392;
        format->aCoef[6].iCoef2 = -232;
        
        buffer->format = (FAudioWaveFormatEx*)format;
        
        // Buffer should be block aligned
        assert((length % blockAlignment) == 0);
        
        buffer->length = length;
        buffer->data = (uint8_t*)malloc(length);
        memcpy(buffer->data, waveData, length);
        
        memset(&buffer->buffer, 0, sizeof(FAudioBuffer));
        buffer->buffer.pAudioData = buffer->data;
        buffer->buffer.AudioBytes = length;
        buffer->buffer.LoopBegin = loopStart;
        buffer->buffer.LoopLength = loopLength;
        buffer->buffer.LoopCount = 0;
        buffer->buffer.Flags = 0;
        buffer->buffer.pContext = nullptr;
    }
    else
    {
        // Handle other formats (PCM as fallback)
        auto format = (FAudioWaveFormatEx*)malloc(sizeof(FAudioWaveFormatEx));
        memset(format, 0, sizeof(FAudioWaveFormatEx));
        format->wFormatTag = 1; // WAVE_FORMAT_PCM
        format->nSamplesPerSec = sampleRate;
        format->nChannels = channels;
        format->nBlockAlign = blockAlignment;
        format->wBitsPerSample = 16; // Assume 16-bit
        format->nAvgBytesPerSec = sampleRate * blockAlignment;
        format->cbSize = 0;
        buffer->format = format;
        
        buffer->length = length;
        buffer->data = (uint8_t*)malloc(length);
        memcpy(buffer->data, waveData, length);
        
        memset(&buffer->buffer, 0, sizeof(FAudioBuffer));
        buffer->buffer.pAudioData = buffer->data;
        buffer->buffer.AudioBytes = length;
        buffer->buffer.LoopBegin = loopStart;
        buffer->buffer.LoopLength = loopLength;
        buffer->buffer.LoopCount = 0;
        buffer->buffer.Flags = 0;
        buffer->buffer.pContext = nullptr;
    }
}

mgulong MGA_Buffer_GetDuration(MGA_Buffer* buffer)
{
    assert(buffer != nullptr);
    float seconds = static_cast<float>(buffer->buffer.AudioBytes) / 
                    (buffer->format->nBlockAlign * buffer->format->nSamplesPerSec);
    return static_cast<mgulong>(seconds * 1000);
}

// Voice callbacks - simplified implementation
static void FAUDIOCALL OnBufferEnd(FAudioVoiceCallback* callback, void *pBufferContext)
{
    if (pBufferContext == nullptr)
        return;
    
    s_FreeStreamingBuffers.push_back((MGA_Buffer*)pBufferContext);
}

static FAudioVoiceCallback MGA_VoiceCallbacksHandler = {
    OnBufferEnd,
    NULL, // OnBufferStart
    NULL, // OnLoopEnd
    NULL, // OnStreamEnd
    NULL, // OnVoiceError
    NULL, // OnVoiceProcessingPassEnd
    NULL  // OnVoiceProcessingPassStart
};

MGA_Voice* MGA_Voice_Create(MGA_System* system, mgint sampleRate, mgint channels)
{
    assert(system != nullptr);
    auto voice = new MGA_Voice();
    voice->system = system;
    return voice;
}

void MGA_Voice_Destroy(MGA_Voice* voice)
{
    assert(voice != nullptr);
    
    if (voice->voice)
        FAudioVoice_DestroyVoice(voice->voice);
    
    delete voice;
}

mgint MGA_Voice_GetBufferCount(MGA_Voice* voice)
{
    assert(voice != nullptr);
    
    if (!voice->voice)
        return 0;
    
    FAudioVoiceState state;
    FAudioSourceVoice_GetState(voice->voice, &state, 0);
    return state.BuffersQueued;
}

void MGA_Voice_SetBuffer(MGA_Voice* voice, MGA_Buffer* buffer)
{
    assert(voice != nullptr);
    
    if (voice->voice)
    {
        FAudioSourceVoice_Stop(voice->voice, 0, 0);
        FAudioSourceVoice_FlushSourceBuffers(voice->voice);
    }
    else if (buffer)
    {
        uint32_t flags = 0;
        uint32_t err = FAudio_CreateSourceVoice(
            voice->system->audio,
            &voice->voice,
            buffer->format,
            flags,
            FAUDIO_DEFAULT_FREQ_RATIO,
            &MGA_VoiceCallbacksHandler,
            nullptr,
            nullptr
        );
        assert(err == 0);
    }
    
    voice->buffer = buffer;
}

void MGA_Voice_AppendBuffer(MGA_Voice* voice, mgbyte* buffer, mguint size)
{
    assert(voice != nullptr);
    assert(buffer != nullptr);
    
    // Find a free buffer
    MGA_Buffer* free = nullptr;
    for (size_t i = 0; i < s_FreeStreamingBuffers.size(); i++)
    {
        auto f = s_FreeStreamingBuffers[i];
        if (f->length < size)
            continue;
        
        free = f;
        s_FreeStreamingBuffers.erase(s_FreeStreamingBuffers.begin() + i);
        break;
    }
    
    auto format = voice->buffer->format;
    
    if (free == nullptr)
    {
        free = new MGA_Buffer;
        MGA_Buffer_InitializePCM(free, nullptr, 0, size, 16, format->nSamplesPerSec, format->nChannels, 0, 0);
    }
    
    memcpy(free->data, buffer, size);
    
    if (MGA_Voice_GetState(voice) != MGSoundState::Playing)
        return;
    
    auto xbuffer = free->buffer;
    xbuffer.LoopBegin = xbuffer.LoopLength = xbuffer.LoopCount = 0;
    xbuffer.pContext = free;
    
    FAudioSourceVoice_SubmitSourceBuffer(voice->voice, &xbuffer, nullptr);
}

void MGA_Voice_Play(MGA_Voice* voice, mgbyte looped)
{
    assert(voice != nullptr);
    
    if (voice->buffer != nullptr)
    {
        FAudioSourceVoice_Stop(voice->voice, 0, 0);
        FAudioSourceVoice_FlushSourceBuffers(voice->voice);
        
        voice->looped = looped;
        
        auto buffer = voice->buffer->buffer;
        if (looped)
            buffer.LoopCount = FAUDIO_LOOP_INFINITE;
        else
            buffer.LoopBegin = buffer.LoopLength = buffer.LoopCount = 0;
        
        FAudioSourceVoice_SubmitSourceBuffer(voice->voice, &buffer, nullptr);
    }
    
    FAudioSourceVoice_Start(voice->voice, 0, 0);
    voice->paused = false;
}

void MGA_Voice_Pause(MGA_Voice* voice)
{
    assert(voice != nullptr);
    
    if (voice->paused)
        return;
    
    FAudioVoiceState state;
    FAudioSourceVoice_GetState(voice->voice, &state, 0);
    if (state.BuffersQueued == 0)
        return;
    
    FAudioSourceVoice_Stop(voice->voice, 0, 0);
    voice->paused = true;
}

void MGA_Voice_Resume(MGA_Voice* voice)
{
    assert(voice != nullptr);
    
    if (!voice->paused)
        MGA_Voice_Play(voice, voice->looped);
    else
    {
        FAudioSourceVoice_Start(voice->voice, 0, 0);
        voice->paused = false;
    }
}

void MGA_Voice_Stop(MGA_Voice* voice, mgbyte immediate)
{
    assert(voice != nullptr);
    
    FAudioSourceVoice_Stop(voice->voice, 0, 0);
    FAudioSourceVoice_FlushSourceBuffers(voice->voice);
    voice->paused = false;
}

MGSoundState MGA_Voice_GetState(MGA_Voice* voice)
{
    assert(voice != nullptr);
    
    if (voice->paused)
        return MGSoundState::Paused;
    
    FAudioVoiceState state;
    FAudioSourceVoice_GetState(voice->voice, &state, 0);
    if (state.BuffersQueued == 0)
        return MGSoundState::Stopped;
    
    return MGSoundState::Playing;
}

mgulong MGA_Voice_GetPosition(MGA_Voice* voice)
{
    assert(voice != nullptr);
    
    if (!voice->voice)
        return 0;
    
    return 0; // FAudio does not provide easy timing info, would need custom implementation
}

// Helper function to calculate pan matrix
static float* MGA_Voice_CalculatePanMatrix(float pan, float scale, float* matrix, int srcChannels)
{
    if (srcChannels == 1)
    {
        matrix[0] = (pan >= 0 ? (1.f - pan) : 1.f) * scale; // Left
        matrix[1] = (pan <= 0 ? (-pan - 1.f) : 1.f) * scale; // Right
    }
    else if (srcChannels == 2)
    {
        if (-1.0f <= pan && pan <= 0.0f)
        {
            matrix[0] = (0.5f * pan + 1.0f) * scale;    // .5 when pan is -1, 1 when pan is 0
            matrix[1] = (0.5f * -pan) * scale;          // .5 when pan is -1, 0 when pan is 0
            matrix[2] = 0.0f;                           //  0 when pan is -1, 0 when pan is 0
            matrix[3] = (pan + 1.0f) * scale;           //  0 when pan is -1, 1 when pan is 0
        }
        else
        {
            matrix[0] = (1.0f - pan) * scale;           //  1 when pan is 0, 0 when pan is 1
            matrix[1] = 0.0f;                           //  0 when pan is 0, 0 when pan is 1
            matrix[2] = (0.5f * pan) * scale;           //  0 when pan is 0, .5 when pan is 1
            matrix[3] = (0.5f * pan + 0.5f) * scale;    // .5 when pan is 0, 1 when pan is 1
        }
    }
    
    return matrix;
}

// Helper function to update output matrix
static void MGA_Voice_UpdateOutputMatrix(MGA_Voice* voice)
{
    if (!voice->voice)
        return;
    
    FAudioVoiceDetails details;
    FAudioVoice_GetVoiceDetails(voice->voice, &details);
    int srcChannelCount = details.InputChannels;
    
    FAudioVoice_GetVoiceDetails(voice->system->masterVoice, &details);
    int dstChannelCount = details.InputChannels;
    
    // Default to zero volume on all channels
    float panMatrix[16];
    memset(panMatrix, 0, sizeof(panMatrix));
    
    // Set the pan on the correct channels based on the reverb mix
    if (!(voice->reverbMix > 0.0f))
    {
        FAudioVoice_SetOutputMatrix(
            voice->voice,
            nullptr,
            srcChannelCount,
            dstChannelCount,
            MGA_Voice_CalculatePanMatrix(voice->pan, 1.0f, panMatrix, srcChannelCount),
            0
        );
    }
    else
    {
        FAudioVoice_SetOutputMatrix(
            voice->voice,
            voice->system->reverbVoice,
            srcChannelCount,
            dstChannelCount,
            MGA_Voice_CalculatePanMatrix(voice->pan, voice->reverbMix, panMatrix, srcChannelCount),
            0
        );
        
        FAudioVoice_SetOutputMatrix(
            voice->voice,
            voice->system->masterVoice,
            srcChannelCount,
            dstChannelCount,
            MGA_Voice_CalculatePanMatrix(voice->pan, 1.0f - (voice->reverbMix > 1.0f ? 1.0f : voice->reverbMix), panMatrix, srcChannelCount),
            0
        );
    }
}

void MGA_Voice_SetPan(MGA_Voice* voice, mgfloat pan)
{
    assert(voice != nullptr);
    voice->pan = pan;
    MGA_Voice_UpdateOutputMatrix(voice);
}

void MGA_Voice_SetPitch(MGA_Voice* voice, mgfloat pitch)
{
    assert(voice != nullptr);
    
    if (!voice->voice)
        return;
    
    float ratio = powf(2.0f, pitch);
    FAudioSourceVoice_SetFrequencyRatio(voice->voice, ratio, 0);
}

void MGA_Voice_SetVolume(MGA_Voice* voice, mgfloat volume)
{
    assert(voice != nullptr);
    
    if (!voice->voice)
        return;
    
    FAudioVoice_SetVolume(voice->voice, volume, 0);
}

void MGA_Voice_SetReverbMix(MGA_Voice* voice, mgfloat mix)
{
    assert(voice != nullptr);
    
    if (!voice->voice)
        return;
    
    if (mix < 0)
        voice->reverbMix = 0.0f;
    else if (mix > 2.0f)
        voice->reverbMix = 2.0f;
    else
        voice->reverbMix = mix;
    
    if (voice->reverbMix > 0.0f)
    {
        // Create a send list with two destinations: reverb and master
        FAudioSendDescriptor desc[2];
        desc[0].pOutputVoice = voice->system->reverbVoice;
        desc[0].Flags = 0;
        desc[1].pOutputVoice = voice->system->masterVoice;
        desc[1].Flags = 0;
        
        FAudioVoiceSends sends;
        sends.SendCount = 2;
        sends.pSends = desc;
        FAudioVoice_SetOutputVoices(voice->voice, &sends);
    }
    else
    {
        // Create a send list with just the master destination
        FAudioSendDescriptor desc[1];
        desc[0].pOutputVoice = voice->system->masterVoice;
        desc[0].Flags = 0;
        
        FAudioVoiceSends sends;
        sends.SendCount = 1;
        sends.pSends = desc;
        FAudioVoice_SetOutputVoices(voice->voice, &sends);
    }
    
    MGA_Voice_UpdateOutputMatrix(voice);
}

void MGA_Voice_SetFilterMode(MGA_Voice* voice, MGFilterMode mode, mgfloat filterQ, mgfloat frequency)
{
    assert(voice != nullptr);
    
    if (!voice->voice)
        return;
    
    FAudioVoiceDetails details;
    FAudioVoice_GetVoiceDetails(voice->voice, &details);
    
    if (filterQ > 0.0f)
    {
        filterQ = 1.0f / filterQ;
        if (filterQ > FAUDIO_MAX_FILTER_ONEOVERQ)
            filterQ = FAUDIO_MAX_FILTER_ONEOVERQ;
    }
    else
        filterQ = 1.0f;
    
    FAudioFilterParameters params;
    params.Type = (FAudioFilterType)mode;
    // Calculate the correct frequency value manually if FAudio_cutoffFrequencyToRadians is not available
    params.Frequency = 2.0f * sinf(3.14159f * frequency / details.InputSampleRate);
    params.OneOverQ = filterQ;
    
    FAudioVoice_SetFilterParameters(voice->voice, &params, 0);
}

void MGA_Voice_ClearFilterMode(MGA_Voice* voice)
{
    assert(voice != nullptr);
    
    if (!voice->voice)
        return;
    
    FAudioFilterParameters params;
    params.Type = FAudioFilterType::FAudioLowPassFilter;
    params.Frequency = 1.0f;
    params.OneOverQ = 1.0f;
    
    FAudioVoice_SetFilterParameters(voice->voice, &params, 0);
}

void MGA_Voice_Apply3D(MGA_Voice* voice, Listener& listener, Emitter& emitter, mgfloat distanceScale)
{
    assert(voice != nullptr);
    
    if (!voice->voice)
        return;
    
    alignas(16) F3DAUDIO_LISTENER f3dListener;
    f3dListener.OrientFront.x = listener.Forward.X;
    f3dListener.OrientFront.y = listener.Forward.Y;
    f3dListener.OrientFront.z = listener.Forward.Z;
    f3dListener.OrientTop.x = listener.Up.X;
    f3dListener.OrientTop.y = listener.Up.Y;
    f3dListener.OrientTop.z = listener.Up.Z;
    f3dListener.Position.x = listener.Position.X;
    f3dListener.Position.y = listener.Position.Y;
    f3dListener.Position.z = listener.Position.Z;
    f3dListener.Velocity.x = listener.Velocity.X;
    f3dListener.Velocity.y = listener.Velocity.Y;
    f3dListener.Velocity.z = listener.Velocity.Z;
    f3dListener.pCone = nullptr;
    
    FAudioVoiceDetails details;
    FAudioVoice_GetVoiceDetails(voice->voice, &details);
    int srcChannelCount = details.InputChannels;
    
    alignas(16) static float azimuths[4] = { 0, 0, 0, 0 };
    
    alignas(16) F3DAUDIO_EMITTER f3dEmitter;
    memset(&f3dEmitter, 0, sizeof(f3dEmitter));
    f3dEmitter.OrientFront.x = emitter.Forward.X;
    f3dEmitter.OrientFront.y = emitter.Forward.Y;
    f3dEmitter.OrientFront.z = emitter.Forward.Z;
    f3dEmitter.OrientTop.x = emitter.Up.X;
    f3dEmitter.OrientTop.y = emitter.Up.Y;
    f3dEmitter.OrientTop.z = emitter.Up.Z;
    f3dEmitter.Position.x = emitter.Position.X;
    f3dEmitter.Position.y = emitter.Position.Y;
    f3dEmitter.Position.z = emitter.Position.Z;
    f3dEmitter.Velocity.x = emitter.Velocity.X;
    f3dEmitter.Velocity.y = emitter.Velocity.Y;
    f3dEmitter.Velocity.z = emitter.Velocity.Z;
    f3dEmitter.DopplerScaler = emitter.DopplerScale;
    f3dEmitter.ChannelCount = srcChannelCount;
    f3dEmitter.pChannelAzimuths = azimuths;
    f3dEmitter.CurveDistanceScaler = 1.0f;
    
    alignas(16) static float DspMatrix[FAUDIO_MAX_AUDIO_CHANNELS * 8];
    
    alignas(16) F3DAUDIO_DSP_SETTINGS dsp;
    memset(&dsp, 0, sizeof(dsp));
    dsp.pMatrixCoefficients = DspMatrix;
    
    uint32_t flags = F3DAUDIO_CALCULATE_MATRIX | F3DAUDIO_CALCULATE_DOPPLER;
    F3DAudioCalculate(voice->system->f3daudio, &f3dListener, &f3dEmitter, flags, &dsp);
    
    FAudioVoice_GetVoiceDetails(voice->system->masterVoice, &details);
    int dstChannelCount = details.InputChannels;
    
    FAudioVoice_SetOutputMatrix(voice->voice, nullptr, srcChannelCount, dstChannelCount, dsp.pMatrixCoefficients, 0);
    
    FAudioSourceVoice_SetFrequencyRatio(voice->voice, dsp.DopplerFactor, 0);
}

