// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#include "mg_common.h"
#include "api_MG_Asset.h"
#include <stdio.h>
#include <string.h>

#if defined(__ANDROID__)
#include <android/asset_manager.h>
#include <android/asset_manager_jni.h>
#include <android/log.h>
#include <jni.h>

static AAssetManager* s_assetManager = nullptr;
static JavaVM* s_javaVM_asset = nullptr;

void MG_Asset_SetAssetManager(void* javaVM, void* assetManagerJobject)
{
    s_javaVM_asset = static_cast<JavaVM*>(javaVM);

    JNIEnv* env = nullptr;
    s_javaVM_asset->AttachCurrentThread(&env, nullptr);
    s_assetManager = AAssetManager_fromJava(env, static_cast<jobject>(assetManagerJobject));

    __android_log_print(ANDROID_LOG_INFO, "MonoGame", "MG_Asset_SetAssetManager: ptr=%p", s_assetManager);
}

struct MG_Asset
{
    AAsset* aasset;  // Android APK asset (null if using file)
    FILE* file;      // Standard file (null if using asset)
};

static bool IsAbsolutePath(const char* path)
{
    return path != nullptr && path[0] == '/';
}

mgbool MG_Asset_Open(const char* path, MG_Asset*& handle, mglong& length)
{
    handle = new MG_Asset();
    handle->aasset = nullptr;
    handle->file = nullptr;

    // Absolute paths bypass the asset manager and use standard file I/O
    if (IsAbsolutePath(path))
    {
        handle->file = fopen(path, "rb");
        if (handle->file == nullptr)
        {
            delete handle;
            return false;
        }
        fseek(handle->file, 0, SEEK_END);
        length = ftell(handle->file);
        fseek(handle->file, 0, SEEK_SET);
        return true;
    }

    // Try the Android asset manager first (APK-bundled content)
    if (s_assetManager != nullptr)
    {
        handle->aasset = AAssetManager_open(s_assetManager, path, AASSET_MODE_BUFFER);
        if (handle->aasset != nullptr)
        {
            length = AAsset_getLength64(handle->aasset);
            return true;
        }
    }

    // Fall back to standard file I/O (external storage, etc.)
    handle->file = fopen(path, "rb");
    if (handle->file == nullptr)
    {
        delete handle;
        return false;
    }
    fseek(handle->file, 0, SEEK_END);
    length = ftell(handle->file);
    fseek(handle->file, 0, SEEK_SET);
    return true;
}

mgint MG_Asset_Read(MG_Asset* handle, mgbyte* buffer, mglong count)
{
    if (handle->aasset != nullptr)
        return AAsset_read(handle->aasset, buffer, count);
    return fread(buffer, 1, count, handle->file);
}

mglong MG_Asset_Seek(MG_Asset* handle, mglong offset, mgint whence)
{
    int origin;
    switch (whence)
    {
        default:
        case 0: origin = SEEK_SET; break;
        case 1: origin = SEEK_CUR; break;
        case 2: origin = SEEK_END; break;
    }

    if (handle->aasset != nullptr)
    {
        AAsset_seek64(handle->aasset, offset, origin);
        return AAsset_getRemainingLength64(handle->aasset) >= 0
            ? AAsset_getLength64(handle->aasset) - AAsset_getRemainingLength64(handle->aasset)
            : -1;
    }

    fseek(handle->file, offset, origin);
    return ftell(handle->file);
}

void MG_Asset_Close(MG_Asset* handle)
{
    if (handle->aasset != nullptr)
        AAsset_close(handle->aasset);
    if (handle->file != nullptr)
        fclose(handle->file);
    delete handle;
}

#else // Non-Android (desktop) — standard file I/O

struct MG_Asset
{
    FILE* file;
};

mgbool MG_Asset_Open(const char* path, MG_Asset*& handle, mglong& length)
{
    handle = new MG_Asset();
    handle->file = fopen(path, "rb");
    if (handle->file == nullptr)
    {
        delete handle;
        return false;
    }

    if (fseek(handle->file, 0, SEEK_END) != 0)
    {
        delete handle;
        return false;
    }

    length = ftell(handle->file);

    if (fseek(handle->file, 0, SEEK_SET) != 0)
    {
        delete handle;
        return false;
    }

    return true;
}

mgint MG_Asset_Read(MG_Asset* handle, mgbyte* buffer, mglong count)
{
    return fread(buffer, 1, count, handle->file);
}

mglong MG_Asset_Seek(MG_Asset* handle, mglong offset, mgint whence)
{
    int origin;
    switch (whence)
    {
        default:
        case 0: // Begin
            origin = SEEK_SET;
            break;
        case 1: // Current
            origin = SEEK_CUR;
            break;
        case 2: // End
            origin = SEEK_END;
            break;
    }

    fseek(handle->file, offset, origin);

    return ftell(handle->file);
}

void MG_Asset_Close(MG_Asset* handle)
{
    fclose(handle->file);
    delete handle;
}

#endif
