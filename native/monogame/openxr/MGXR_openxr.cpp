// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

// MGXR_openxr.cpp — Core OpenXR native module
// Implements the MGXR_* API surface defined in api_MGXR.h

#include "api_MGXR.h"

// Select graphics API based on backend define.
// MG_VULKAN is set by vulkan()/vulkan_openxr()/vulkan_android() in premake.
// MG_DIRECTX12 is set by directx12() in premake.
// Exactly one must be defined when building with MG_OPENXR.
#if defined(MG_DIRECTX12)
    #include <d3d12.h>
    #include <dxgi1_6.h>
    #define XR_USE_GRAPHICS_API_D3D12
    #ifdef _WIN32
    #define XR_USE_PLATFORM_WIN32
    #endif
#elif defined(MG_VULKAN)
    #include <vulkan/vulkan.h>
    #define XR_USE_GRAPHICS_API_VULKAN
#else
    #error "MGXR_openxr.cpp requires either MG_VULKAN or MG_DIRECTX12 to be defined"
#endif

#ifdef __ANDROID__
#define XR_USE_PLATFORM_ANDROID
#include <jni.h>
#endif
#include <openxr/openxr.h>
#include <openxr/openxr_platform.h>

#include <cstring>
#include <cstdlib>
#include <cstdio>
#include <vector>
#include <cassert>

// ============================================================================
// Android JNI bridge
// ============================================================================

#ifdef __ANDROID__
#include <android/log.h>
#define MGXR_LOG_TAG "MonoGame-XR"
#define MGXR_LOGI(...) __android_log_print(ANDROID_LOG_INFO, MGXR_LOG_TAG, __VA_ARGS__)
#define MGXR_LOGE(...) __android_log_print(ANDROID_LOG_ERROR, MGXR_LOG_TAG, __VA_ARGS__)

static JavaVM* g_javaVM = nullptr;
static jobject g_activity = nullptr;

// Called from C# (OpenXRGameActivity) to pass the Android context to native code.
// JNI_OnLoad is not defined here — SDL2 provides it when linked.
// The JavaVM pointer is captured via this explicit call instead.
MG_EXPORT void MGXR_SetAndroidContext(void* vm, void* activity)
{
    g_javaVM = (JavaVM*)vm;
    g_activity = (jobject)activity;
    MGXR_LOGI("MGXR_SetAndroidContext: VM=%p Activity=%p", vm, activity);
}
#else
#define MGXR_LOGI(...) do { \
    fprintf(stderr, "[MGXR] " __VA_ARGS__); fprintf(stderr, "\n"); \
} while(0)
#define MGXR_LOGE(...) do { \
    fprintf(stderr, "[MGXR ERROR] " __VA_ARGS__); fprintf(stderr, "\n"); \
} while(0)
#endif

// Internal structures

struct MGXR_System
{
    XrInstance instance;
    XrSystemId systemId;
    XrDebugUtilsMessengerEXT debugMessenger;

    // Graphics requirements queried from OpenXR
#if defined(MG_VULKAN)
    XrGraphicsRequirementsVulkanKHR vulkanRequirements;
#elif defined(MG_DIRECTX12)
    XrGraphicsRequirementsD3D12KHR d3d12Requirements;
#endif
};

struct MGXR_Session
{
    MGXR_System* system;
    XrSession session;
    XrSessionState state;
    bool sessionRunning;

    // Reference spaces
    XrSpace viewSpace;
    XrSpace localSpace;
    XrSpace stageSpace;

    // Frame state
    XrFrameState frameState;
    bool frameActive;

    // View configuration
    XrViewConfigurationType viewConfigType;
    std::vector<XrViewConfigurationView> configViews;
    std::vector<XrView> views;
};

struct MGXR_Swapchain
{
    MGXR_Session* session;
    XrSwapchain swapchain;
    mgint width;
    mgint height;
    mgint arraySize;
    mgint lastAcquiredIndex;
#if defined(MG_VULKAN)
    std::vector<XrSwapchainImageVulkanKHR> images;
#elif defined(MG_DIRECTX12)
    std::vector<XrSwapchainImageD3D12KHR> images;
#endif
};

struct MGXR_Space
{
    MGXR_Session* session;
    XrSpace space;
    MGXRReferenceSpaceType type;
};

struct MGXR_ActionSet
{
    MGXR_System* system;
    MGXR_Session* session; // set when attached to a session
    XrActionSet actionSet;
};

struct MGXR_Action
{
    MGXR_ActionSet* actionSet;
    XrAction action;
    MGXRActionType type;
    XrPath subactionPaths[2]; // left, right
    XrSpace cachedActionSpaces[2]; // per-hand cached action spaces (index 0=left, 1=right)
};


// Helper: Check XrResult and log errors
static bool xr_check(XrInstance instance, XrResult result, const char* msg)
{
    if (XR_SUCCEEDED(result))
        return true;

    char resultStr[XR_MAX_RESULT_STRING_SIZE];
    if (instance != XR_NULL_HANDLE)
        xrResultToString(instance, result, resultStr);
    else
        snprintf(resultStr, sizeof(resultStr), "XrResult=%d", (int)result);

    // TODO: Hook into MonoGame logging
    fprintf(stderr, "OpenXR Error [%s]: %s\n", msg, resultStr);
    return false;
}


// ============================================================================
// System lifecycle
// ============================================================================

MG_EXPORT MGXR_System* MGXR_System_Create()
{
    MGXR_LOGI("MGXR_System_Create: starting");
    auto system = new MGXR_System();
    memset(system, 0, sizeof(MGXR_System));

    // Required extensions
    std::vector<const char*> extensions = {
#if defined(MG_VULKAN)
        XR_KHR_VULKAN_ENABLE2_EXTENSION_NAME,
#elif defined(MG_DIRECTX12)
        XR_KHR_D3D12_ENABLE_EXTENSION_NAME,
#endif
    };

#ifdef __ANDROID__
    extensions.push_back(XR_KHR_ANDROID_CREATE_INSTANCE_EXTENSION_NAME);
#endif

#ifndef NDEBUG
    extensions.push_back(XR_EXT_DEBUG_UTILS_EXTENSION_NAME);
#endif

    MGXR_LOGI("MGXR_System_Create: requesting %d extensions", (int)extensions.size());

    XrInstanceCreateInfo createInfo = {XR_TYPE_INSTANCE_CREATE_INFO};
    strncpy(createInfo.applicationInfo.applicationName, "MonoGame", XR_MAX_APPLICATION_NAME_SIZE);
    createInfo.applicationInfo.applicationVersion = 1;
    strncpy(createInfo.applicationInfo.engineName, "MonoGame", XR_MAX_ENGINE_NAME_SIZE);
    createInfo.applicationInfo.engineVersion = 1;
    createInfo.applicationInfo.apiVersion = XR_API_VERSION_1_0;
    createInfo.enabledExtensionCount = (uint32_t)extensions.size();
    createInfo.enabledExtensionNames = extensions.data();

#ifdef __ANDROID__
    // On Android, the OpenXR runtime needs the JavaVM and Activity to initialize.
    XrInstanceCreateInfoAndroidKHR androidInfo = {XR_TYPE_INSTANCE_CREATE_INFO_ANDROID_KHR};
    androidInfo.applicationVM = g_javaVM;
    androidInfo.applicationActivity = g_activity;
    createInfo.next = &androidInfo;

    if (!g_javaVM || !g_activity)
    {
        MGXR_LOGE("MGXR_System_Create: Missing Android context (call MGXR_SetAndroidContext first)");
        delete system;
        return nullptr;
    }

    // Android requires xrInitializeLoaderKHR before any other OpenXR calls
    PFN_xrInitializeLoaderKHR xrInitializeLoaderKHR = nullptr;
    xrGetInstanceProcAddr(XR_NULL_HANDLE, "xrInitializeLoaderKHR",
        (PFN_xrVoidFunction*)&xrInitializeLoaderKHR);
    if (xrInitializeLoaderKHR)
    {
        XrLoaderInitInfoAndroidKHR loaderInitInfo = {XR_TYPE_LOADER_INIT_INFO_ANDROID_KHR};
        loaderInitInfo.applicationVM = g_javaVM;
        loaderInitInfo.applicationContext = g_activity;
        XrResult initResult = xrInitializeLoaderKHR((XrLoaderInitInfoBaseHeaderKHR*)&loaderInitInfo);
        MGXR_LOGI("MGXR_System_Create: xrInitializeLoaderKHR result=%d", (int)initResult);
    }
    else
    {
        MGXR_LOGE("MGXR_System_Create: xrInitializeLoaderKHR not available");
    }
#endif



    MGXR_LOGI("MGXR_System_Create: calling xrCreateInstance");
    XrResult result = xrCreateInstance(&createInfo, &system->instance);
    if (!xr_check(XR_NULL_HANDLE, result, "xrCreateInstance"))
    {
        delete system;
        return nullptr;
    }
    MGXR_LOGI("MGXR_System_Create: xrCreateInstance succeeded, instance=%p", (void*)system->instance);

    // Get system (HMD)
    XrSystemGetInfo systemInfo = {XR_TYPE_SYSTEM_GET_INFO};
    systemInfo.formFactor = XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY;

    MGXR_LOGI("MGXR_System_Create: calling xrGetSystem");
    result = xrGetSystem(system->instance, &systemInfo, &system->systemId);
    MGXR_LOGI("MGXR_System_Create: xrGetSystem returned %d, systemId=%llu", (int)result, (unsigned long long)system->systemId);
    if (!xr_check(system->instance, result, "xrGetSystem"))
    {
        xrDestroyInstance(system->instance);
        delete system;
        return nullptr;
    }

    // Query graphics requirements (required before xrCreateSession)
#if defined(MG_VULKAN)
    MGXR_LOGI("MGXR_System_Create: querying Vulkan requirements");
    PFN_xrGetVulkanGraphicsRequirements2KHR xrGetVulkanGraphicsRequirements2KHR = nullptr;
    xrGetInstanceProcAddr(system->instance, "xrGetVulkanGraphicsRequirements2KHR",
        (PFN_xrVoidFunction*)&xrGetVulkanGraphicsRequirements2KHR);

    if (xrGetVulkanGraphicsRequirements2KHR)
    {
        system->vulkanRequirements = {XR_TYPE_GRAPHICS_REQUIREMENTS_VULKAN_KHR};
        xrGetVulkanGraphicsRequirements2KHR(system->instance, system->systemId, &system->vulkanRequirements);
        MGXR_LOGI("MGXR_System_Create: Vulkan requirements OK");
    }
#elif defined(MG_DIRECTX12)
    MGXR_LOGI("MGXR_System_Create: querying D3D12 requirements");
    PFN_xrGetD3D12GraphicsRequirementsKHR xrGetD3D12GraphicsRequirementsKHR = nullptr;
    xrGetInstanceProcAddr(system->instance, "xrGetD3D12GraphicsRequirementsKHR",
        (PFN_xrVoidFunction*)&xrGetD3D12GraphicsRequirementsKHR);

    if (xrGetD3D12GraphicsRequirementsKHR)
    {
        system->d3d12Requirements = {XR_TYPE_GRAPHICS_REQUIREMENTS_D3D12_KHR};
        xrGetD3D12GraphicsRequirementsKHR(system->instance, system->systemId, &system->d3d12Requirements);
        MGXR_LOGI("MGXR_System_Create: D3D12 requirements OK (minFeatureLevel=0x%x, adapterLuid=%llu)",
            (unsigned)system->d3d12Requirements.minFeatureLevel,
            (unsigned long long)system->d3d12Requirements.adapterLuid.LowPart);
    }
#endif

    MGXR_LOGI("MGXR_System_Create: complete");
    return system;
}

MG_EXPORT void MGXR_System_Destroy(MGXR_System* system)
{
    if (!system)
        return;

    if (system->debugMessenger != XR_NULL_HANDLE)
    {
        PFN_xrDestroyDebugUtilsMessengerEXT xrDestroyDebugUtilsMessengerEXT = nullptr;
        xrGetInstanceProcAddr(system->instance, "xrDestroyDebugUtilsMessengerEXT",
            (PFN_xrVoidFunction*)&xrDestroyDebugUtilsMessengerEXT);
        if (xrDestroyDebugUtilsMessengerEXT)
            xrDestroyDebugUtilsMessengerEXT(system->debugMessenger);
    }

    if (system->instance != XR_NULL_HANDLE)
        xrDestroyInstance(system->instance);

    delete system;
}

MG_EXPORT mgbyte MGXR_System_IsHmdPresent(MGXR_System* system)
{
    if (!system)
        return 0;
    return system->systemId != XR_NULL_SYSTEM_ID ? 1 : 0;
}

// ============================================================================
// Vulkan instance/device creation wrappers (Vulkan-only)
// DX12 does not need these — devices are created normally by the graphics backend.
// ============================================================================

#if defined(MG_VULKAN)

// Wraps vkCreateInstance through the OpenXR runtime (XR_KHR_vulkan_enable2).
// This lets the runtime initialize its internal Vulkan state (e.g. volk in Meta XR Simulator).
MG_EXPORT mgint MGXR_System_CreateVulkanInstance(MGXR_System* system,
    void* pfnGetInstanceProcAddr, void* vkCreateInfo, void** outVkInstance)
{
    if (!system || !outVkInstance)
        return -1;

    PFN_xrCreateVulkanInstanceKHR xrCreateVulkanInstanceKHR = nullptr;
    xrGetInstanceProcAddr(system->instance, "xrCreateVulkanInstanceKHR",
        (PFN_xrVoidFunction*)&xrCreateVulkanInstanceKHR);

    if (!xrCreateVulkanInstanceKHR)
    {
        MGXR_LOGE("MGXR_System_CreateVulkanInstance: xrCreateVulkanInstanceKHR not available");
        return -1;
    }

    XrVulkanInstanceCreateInfoKHR createInfo = {XR_TYPE_VULKAN_INSTANCE_CREATE_INFO_KHR};
    createInfo.systemId = system->systemId;
    createInfo.pfnGetInstanceProcAddr = (PFN_vkGetInstanceProcAddr)pfnGetInstanceProcAddr;
    createInfo.vulkanCreateInfo = (const VkInstanceCreateInfo*)vkCreateInfo;
    createInfo.vulkanAllocator = nullptr;

    VkInstance vkInstance = VK_NULL_HANDLE;
    VkResult vkResult = VK_SUCCESS;
    XrResult xrResult = xrCreateVulkanInstanceKHR(system->instance, &createInfo, &vkInstance, &vkResult);

    MGXR_LOGI("MGXR_System_CreateVulkanInstance: xr=%d, vk=%d, instance=%p",
        (int)xrResult, (int)vkResult, (void*)vkInstance);

    *outVkInstance = vkInstance;
    return (mgint)vkResult;
}

// Gets the VkPhysicalDevice the OpenXR runtime wants us to use.
MG_EXPORT mgint MGXR_System_GetVulkanPhysicalDevice(MGXR_System* system,
    void* vkInstance, void** outPhysicalDevice)
{
    if (!system || !outPhysicalDevice)
        return -1;

    PFN_xrGetVulkanGraphicsDevice2KHR xrGetVulkanGraphicsDevice2KHR = nullptr;
    xrGetInstanceProcAddr(system->instance, "xrGetVulkanGraphicsDevice2KHR",
        (PFN_xrVoidFunction*)&xrGetVulkanGraphicsDevice2KHR);

    if (!xrGetVulkanGraphicsDevice2KHR)
    {
        MGXR_LOGE("MGXR_System_GetVulkanPhysicalDevice: xrGetVulkanGraphicsDevice2KHR not available");
        return -1;
    }

    XrVulkanGraphicsDeviceGetInfoKHR deviceGetInfo = {XR_TYPE_VULKAN_GRAPHICS_DEVICE_GET_INFO_KHR};
    deviceGetInfo.systemId = system->systemId;
    deviceGetInfo.vulkanInstance = (VkInstance)vkInstance;

    VkPhysicalDevice physicalDevice = VK_NULL_HANDLE;
    XrResult result = xrGetVulkanGraphicsDevice2KHR(system->instance, &deviceGetInfo, &physicalDevice);

    MGXR_LOGI("MGXR_System_GetVulkanPhysicalDevice: xr=%d, physDev=%p",
        (int)result, (void*)physicalDevice);

    *outPhysicalDevice = physicalDevice;
    return (result == XR_SUCCESS) ? 0 : -1;
}

// Wraps vkCreateDevice through the OpenXR runtime (XR_KHR_vulkan_enable2).
MG_EXPORT mgint MGXR_System_CreateVulkanDevice(MGXR_System* system,
    void* pfnGetInstanceProcAddr, void* vkPhysicalDevice,
    void* vkCreateInfo, void** outVkDevice)
{
    if (!system || !outVkDevice)
        return -1;

    PFN_xrCreateVulkanDeviceKHR xrCreateVulkanDeviceKHR = nullptr;
    xrGetInstanceProcAddr(system->instance, "xrCreateVulkanDeviceKHR",
        (PFN_xrVoidFunction*)&xrCreateVulkanDeviceKHR);

    if (!xrCreateVulkanDeviceKHR)
    {
        MGXR_LOGE("MGXR_System_CreateVulkanDevice: xrCreateVulkanDeviceKHR not available");
        return -1;
    }

    XrVulkanDeviceCreateInfoKHR createInfo = {XR_TYPE_VULKAN_DEVICE_CREATE_INFO_KHR};
    createInfo.systemId = system->systemId;
    createInfo.pfnGetInstanceProcAddr = (PFN_vkGetInstanceProcAddr)pfnGetInstanceProcAddr;
    createInfo.vulkanCreateInfo = (const VkDeviceCreateInfo*)vkCreateInfo;
    createInfo.vulkanPhysicalDevice = (VkPhysicalDevice)vkPhysicalDevice;
    createInfo.vulkanAllocator = nullptr;

    VkDevice vkDevice = VK_NULL_HANDLE;
    VkResult vkResult = VK_SUCCESS;
    XrResult xrResult = xrCreateVulkanDeviceKHR(system->instance, &createInfo, &vkDevice, &vkResult);

    MGXR_LOGI("MGXR_System_CreateVulkanDevice: xr=%d, vk=%d, device=%p",
        (int)xrResult, (int)vkResult, (void*)vkDevice);

    *outVkDevice = vkDevice;
    return (mgint)vkResult;
}

// Gets the adapter LUID that the OpenXR runtime requires (DX12 would need this for adapter matching).
MG_EXPORT void MGXR_System_GetAdapterLuid(MGXR_System* system, mglong* outLuidLow, mglong* outLuidHigh)
{
    // Vulkan path — not applicable, zero out
    if (outLuidLow) *outLuidLow = 0;
    if (outLuidHigh) *outLuidHigh = 0;
}

#elif defined(MG_DIRECTX12)

// Gets the adapter LUID that the OpenXR runtime requires for D3D12 device creation.
// The graphics backend must create the device on the adapter matching this LUID.
MG_EXPORT void MGXR_System_GetAdapterLuid(MGXR_System* system, mglong* outLuidLow, mglong* outLuidHigh)
{
    if (!system || !outLuidLow || !outLuidHigh)
        return;
    *outLuidLow = (mglong)system->d3d12Requirements.adapterLuid.LowPart;
    *outLuidHigh = (mglong)system->d3d12Requirements.adapterLuid.HighPart;
}

#endif // MG_VULKAN / MG_DIRECTX12


// ============================================================================
// Session lifecycle
// ============================================================================

// The MGXR_DeviceHandles struct layout matches the C# MGXR_DeviceHandles:
// handle0 (nint), handle1 (nint), handle2 (nint), handle3 (uint32), handle4 (uint32)
struct MGXR_DeviceHandles
{
    void* handle0;
    void* handle1;
    void* handle2;
    uint32_t handle3;
    uint32_t handle4;
};

MG_EXPORT MGXR_Session* MGXR_Session_Create(MGXR_System* system, MGXR_DeviceHandles* handles)
{
    if (!system || !handles)
    {
        MGXR_LOGE("MGXR_Session_Create: system or handles is NULL");
        return nullptr;
    }

    auto session = new MGXR_Session();
    memset(session, 0, sizeof(MGXR_Session));
    session->system = system;
    session->state = XR_SESSION_STATE_UNKNOWN;
    session->sessionRunning = false;
    session->viewConfigType = XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;

#if defined(MG_VULKAN)
    MGXR_LOGI("MGXR_Session_Create: Vulkan — instance=%p, physDev=%p, device=%p, queueFamily=%u, queue=%u",
        handles->handle0, handles->handle1, handles->handle2, handles->handle3, handles->handle4);

    XrGraphicsBindingVulkanKHR graphicsBinding = {XR_TYPE_GRAPHICS_BINDING_VULKAN_KHR};
    graphicsBinding.instance = (VkInstance)handles->handle0;
    graphicsBinding.physicalDevice = (VkPhysicalDevice)handles->handle1;
    graphicsBinding.device = (VkDevice)handles->handle2;
    graphicsBinding.queueFamilyIndex = handles->handle3;
    graphicsBinding.queueIndex = handles->handle4;

#elif defined(MG_DIRECTX12)
    MGXR_LOGI("MGXR_Session_Create: D3D12 — device=%p, queue=%p",
        handles->handle0, handles->handle1);

    XrGraphicsBindingD3D12KHR graphicsBinding = {XR_TYPE_GRAPHICS_BINDING_D3D12_KHR};
    graphicsBinding.device = (ID3D12Device*)handles->handle0;
    graphicsBinding.queue = (ID3D12CommandQueue*)handles->handle1;

#endif

    XrSessionCreateInfo sessionInfo = {XR_TYPE_SESSION_CREATE_INFO};
    sessionInfo.systemId = system->systemId;
    sessionInfo.next = &graphicsBinding;

    MGXR_LOGI("MGXR_Session_Create: calling xrCreateSession");
    XrResult result = xrCreateSession(system->instance, &sessionInfo, &session->session);
    if (!xr_check(system->instance, result, "xrCreateSession"))
    {
        MGXR_LOGE("MGXR_Session_Create: xrCreateSession failed");
        delete session;
        return nullptr;
    }
    MGXR_LOGI("MGXR_Session_Create: xrCreateSession succeeded, session=%p", (void*)session->session);

    // Enumerate view configuration views (to get recommended sizes)
    uint32_t viewCount = 0;
    xrEnumerateViewConfigurationViews(system->instance, system->systemId,
        session->viewConfigType, 0, &viewCount, nullptr);
    session->configViews.resize(viewCount, {XR_TYPE_VIEW_CONFIGURATION_VIEW});
    session->views.resize(viewCount, {XR_TYPE_VIEW});
    xrEnumerateViewConfigurationViews(system->instance, system->systemId,
        session->viewConfigType, viewCount, &viewCount, session->configViews.data());
    MGXR_LOGI("MGXR_Session_Create: viewCount=%u", viewCount);
    for (uint32_t i = 0; i < viewCount; i++)
    {
        MGXR_LOGI("  view[%u]: recommended=%ux%u, max=%ux%u", i,
            session->configViews[i].recommendedImageRectWidth, session->configViews[i].recommendedImageRectHeight,
            session->configViews[i].maxImageRectWidth, session->configViews[i].maxImageRectHeight);
    }

    // Create reference spaces
    XrReferenceSpaceCreateInfo spaceInfo = {XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
    spaceInfo.poseInReferenceSpace.orientation.w = 1.0f;

    spaceInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_VIEW;
    xrCreateReferenceSpace(session->session, &spaceInfo, &session->viewSpace);

    spaceInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_LOCAL;
    xrCreateReferenceSpace(session->session, &spaceInfo, &session->localSpace);

    spaceInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_STAGE;
    result = xrCreateReferenceSpace(session->session, &spaceInfo, &session->stageSpace);
    if (!XR_SUCCEEDED(result))
        session->stageSpace = XR_NULL_HANDLE; // Stage may not be supported

    MGXR_LOGI("MGXR_Session_Create: complete");
    return session;
}

MG_EXPORT void MGXR_Session_Destroy(MGXR_Session* session)
{
    if (!session)
        return;

    // End any active frame that wasn't completed
    if (session->frameActive)
    {
        XrFrameEndInfo endInfo = {XR_TYPE_FRAME_END_INFO};
        endInfo.displayTime = session->frameState.predictedDisplayTime;
        endInfo.environmentBlendMode = XR_ENVIRONMENT_BLEND_MODE_OPAQUE;
        endInfo.layerCount = 0;
        endInfo.layers = nullptr;
        xrEndFrame(session->session, &endInfo);
        session->frameActive = false;
    }

    // End the session if it's still running
    if (session->sessionRunning)
    {
        xrEndSession(session->session);
        session->sessionRunning = false;
    }

    if (session->stageSpace != XR_NULL_HANDLE)
        xrDestroySpace(session->stageSpace);
    if (session->localSpace != XR_NULL_HANDLE)
        xrDestroySpace(session->localSpace);
    if (session->viewSpace != XR_NULL_HANDLE)
        xrDestroySpace(session->viewSpace);
    if (session->session != XR_NULL_HANDLE)
        xrDestroySession(session->session);

    delete session;
}

MG_EXPORT MGXRSessionState MGXR_Session_GetState(MGXR_Session* session)
{
    if (!session)
        return MGXRSessionState::Unknown;

    switch (session->state)
    {
        case XR_SESSION_STATE_IDLE: return MGXRSessionState::Idle;
        case XR_SESSION_STATE_READY: return MGXRSessionState::Ready;
        case XR_SESSION_STATE_SYNCHRONIZED: return MGXRSessionState::Synchronized;
        case XR_SESSION_STATE_VISIBLE: return MGXRSessionState::Visible;
        case XR_SESSION_STATE_FOCUSED: return MGXRSessionState::Focused;
        case XR_SESSION_STATE_STOPPING: return MGXRSessionState::Stopping;
        case XR_SESSION_STATE_LOSS_PENDING: return MGXRSessionState::LossPending;
        case XR_SESSION_STATE_EXITING: return MGXRSessionState::Exiting;
        default: return MGXRSessionState::Unknown;
    }
}

MG_EXPORT mgbyte MGXR_Session_BeginFrame(MGXR_Session* session, mglong* predictedDisplayTime, mglong* predictedDisplayPeriod)
{
    if (!session)
    {
        MGXR_LOGE("MGXR_Session_BeginFrame: session is NULL");
        return 0;
    }

    // Poll OpenXR events and handle session state transitions
    XrEventDataBuffer eventData = {XR_TYPE_EVENT_DATA_BUFFER};
    while (xrPollEvent(session->system->instance, &eventData) == XR_SUCCESS)
    {
        if (eventData.type == XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED)
        {
            auto* stateEvent = (XrEventDataSessionStateChanged*)&eventData;
            session->state = stateEvent->state;

            if (session->state == XR_SESSION_STATE_READY)
            {
                XrSessionBeginInfo beginInfo = {XR_TYPE_SESSION_BEGIN_INFO};
                beginInfo.primaryViewConfigurationType = session->viewConfigType;
                XrResult r = xrBeginSession(session->session, &beginInfo);
                if (XR_SUCCEEDED(r))
                {
                    session->sessionRunning = true;
                }
                else
                {
                }
            }
            else if (session->state == XR_SESSION_STATE_STOPPING)
            {
                session->sessionRunning = false;
                xrEndSession(session->session);
            }
        }
        eventData = {XR_TYPE_EVENT_DATA_BUFFER};
    }

    // Only render frames when the session is running
    if (!session->sessionRunning)
        return 0;

    session->frameState = {XR_TYPE_FRAME_STATE};
    XrResult result = xrWaitFrame(session->session, nullptr, &session->frameState);
    if (!xr_check(session->system->instance, result, "xrWaitFrame"))
        return 0;

    result = xrBeginFrame(session->session, nullptr);
    if (!xr_check(session->system->instance, result, "xrBeginFrame"))
        return 0;

    // If the runtime says don't render, submit an empty frame immediately
    // (xrBeginFrame/xrEndFrame must always be paired)
    if (!session->frameState.shouldRender)
    {
        XrFrameEndInfo endInfo = {XR_TYPE_FRAME_END_INFO};
        endInfo.displayTime = session->frameState.predictedDisplayTime;
        endInfo.environmentBlendMode = XR_ENVIRONMENT_BLEND_MODE_OPAQUE;
        endInfo.layerCount = 0;
        endInfo.layers = nullptr;
        xrEndFrame(session->session, &endInfo);

        if (predictedDisplayTime)
            *predictedDisplayTime = session->frameState.predictedDisplayTime;
        if (predictedDisplayPeriod)
            *predictedDisplayPeriod = session->frameState.predictedDisplayPeriod;
        return 0;
    }

    session->frameActive = true;

    if (predictedDisplayTime)
        *predictedDisplayTime = session->frameState.predictedDisplayTime;
    if (predictedDisplayPeriod)
        *predictedDisplayPeriod = session->frameState.predictedDisplayPeriod;

    MGXR_LOGI("MGXR_Session_BeginFrame: shouldRender=%d, displayTime=%lld",
        session->frameState.shouldRender, (long long)session->frameState.predictedDisplayTime);
    return 1;
}

MG_EXPORT void MGXR_Session_EndFrame(MGXR_Session* session, mglong displayTime)
{
    if (!session || !session->frameActive)
        return;

    // Note: Composition layers are submitted by the caller via the frame end info
    // For now, end with an empty frame if no layers are provided
    XrFrameEndInfo endInfo = {XR_TYPE_FRAME_END_INFO};
    endInfo.displayTime = displayTime;
    endInfo.environmentBlendMode = XR_ENVIRONMENT_BLEND_MODE_OPAQUE;
    endInfo.layerCount = 0;
    endInfo.layers = nullptr;

    xrEndFrame(session->session, &endInfo);
    session->frameActive = false;
}

MG_EXPORT void MGXR_Session_EndFrameStereo(
    MGXR_Session* session,
    MGXR_Swapchain* leftSwapchain,
    MGXR_Swapchain* rightSwapchain,
    mglong displayTime)
{
    if (!session || !session->frameActive)
        return;

    if (!leftSwapchain || !rightSwapchain)
    {
        // Fall back to empty frame if swapchains unavailable
        MGXR_Session_EndFrame(session, displayTime);
        return;
    }

    // Locate views to get per-eye pose and FoV at the display time
    XrViewLocateInfo locateInfo = {XR_TYPE_VIEW_LOCATE_INFO};
    locateInfo.viewConfigurationType = session->viewConfigType;
    locateInfo.displayTime = displayTime;
    locateInfo.space = session->localSpace;

    XrViewState viewState = {XR_TYPE_VIEW_STATE};
    uint32_t viewCount = (uint32_t)session->views.size();
    xrLocateViews(session->session, &locateInfo, &viewState,
        viewCount, &viewCount, session->views.data());

    // Build per-eye sub-images
    XrCompositionLayerProjectionView projViews[2] = {};
    for (int eye = 0; eye < 2 && eye < (int)viewCount; eye++)
    {
        auto* sc = (eye == 0) ? leftSwapchain : rightSwapchain;

        projViews[eye].type = XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW;
        projViews[eye].pose = session->views[eye].pose;
        projViews[eye].fov = session->views[eye].fov;
        projViews[eye].subImage.swapchain = sc->swapchain;
        projViews[eye].subImage.imageRect.offset = {0, 0};
        projViews[eye].subImage.imageRect.extent = {sc->width, sc->height};
        projViews[eye].subImage.imageArrayIndex = 0;
    }

    XrCompositionLayerProjection projLayer = {XR_TYPE_COMPOSITION_LAYER_PROJECTION};
    projLayer.space = session->localSpace;
    projLayer.viewCount = (viewCount < 2) ? viewCount : 2;
    projLayer.views = projViews;

    const XrCompositionLayerBaseHeader* layers[] = {
        (const XrCompositionLayerBaseHeader*)&projLayer
    };

    XrFrameEndInfo endInfo = {XR_TYPE_FRAME_END_INFO};
    endInfo.displayTime = displayTime;
    endInfo.environmentBlendMode = XR_ENVIRONMENT_BLEND_MODE_OPAQUE;
    endInfo.layerCount = 1;
    endInfo.layers = layers;

    xrEndFrame(session->session, &endInfo);
    session->frameActive = false;
}

MG_EXPORT void MGXR_Session_RequestExit(MGXR_Session* session)
{
    if (!session)
        return;
    xrRequestExitSession(session->session);
}


// ============================================================================
// Swapchain
// ============================================================================

MG_EXPORT MGXR_Swapchain* MGXR_Swapchain_Create(MGXR_Session* session, mgint width, mgint height, mgint sampleCount, mgint arraySize)
{
    MGXR_LOGI("MGXR_Swapchain_Create: session=%p, %dx%d, samples=%d, arraySize=%d",
        session, width, height, sampleCount, arraySize);

    if (!session)
    {
        MGXR_LOGE("MGXR_Swapchain_Create: session is NULL");
        return nullptr;
    }

    auto swapchain = new MGXR_Swapchain();
    swapchain->session = session;
    swapchain->width = width;
    swapchain->height = height;
    swapchain->arraySize = arraySize;

    XrSwapchainCreateInfo createInfo = {XR_TYPE_SWAPCHAIN_CREATE_INFO};
    createInfo.usageFlags = XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT | XR_SWAPCHAIN_USAGE_SAMPLED_BIT;
#if defined(MG_VULKAN)
    createInfo.format = 43; // VK_FORMAT_R8G8B8A8_SRGB
#elif defined(MG_DIRECTX12)
    createInfo.format = 29; // DXGI_FORMAT_R8G8B8A8_UNORM_SRGB
#endif
    createInfo.sampleCount = sampleCount;
    createInfo.width = width;
    createInfo.height = height;
    createInfo.faceCount = 1;
    createInfo.arraySize = arraySize;
    createInfo.mipCount = 1;

    XrResult result = xrCreateSwapchain(session->session, &createInfo, &swapchain->swapchain);
    if (!xr_check(session->system->instance, result, "xrCreateSwapchain"))
    {
        MGXR_LOGE("MGXR_Swapchain_Create: xrCreateSwapchain failed");
        delete swapchain;
        return nullptr;
    }

    // Enumerate swapchain images
    uint32_t imageCount = 0;
    xrEnumerateSwapchainImages(swapchain->swapchain, 0, &imageCount, nullptr);
#if defined(MG_VULKAN)
    swapchain->images.resize(imageCount, {XR_TYPE_SWAPCHAIN_IMAGE_VULKAN_KHR});
#elif defined(MG_DIRECTX12)
    swapchain->images.resize(imageCount, {XR_TYPE_SWAPCHAIN_IMAGE_D3D12_KHR});
#endif
    xrEnumerateSwapchainImages(swapchain->swapchain, imageCount, &imageCount,
        (XrSwapchainImageBaseHeader*)swapchain->images.data());

    MGXR_LOGI("MGXR_Swapchain_Create: success, imageCount=%u", imageCount);
    for (uint32_t i = 0; i < imageCount; i++)
#if defined(MG_VULKAN)
        MGXR_LOGI("  image[%u]: VkImage=%p", i, (void*)swapchain->images[i].image);
#elif defined(MG_DIRECTX12)
        MGXR_LOGI("  image[%u]: ID3D12Resource=%p", i, (void*)swapchain->images[i].texture);
#endif

    return swapchain;
}

MG_EXPORT void MGXR_Swapchain_Destroy(MGXR_Swapchain* swapchain)
{
    if (!swapchain)
        return;

    if (swapchain->swapchain != XR_NULL_HANDLE)
        xrDestroySwapchain(swapchain->swapchain);

    delete swapchain;
}

MG_EXPORT mgint MGXR_Swapchain_AcquireImage(MGXR_Swapchain* swapchain)
{
    if (!swapchain)
    {
        MGXR_LOGE("MGXR_Swapchain_AcquireImage: swapchain is NULL");
        return -1;
    }

    XrSwapchainImageAcquireInfo acquireInfo = {XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO};
    uint32_t imageIndex = 0;
    XrResult result = xrAcquireSwapchainImage(swapchain->swapchain, &acquireInfo, &imageIndex);
    if (!xr_check(swapchain->session->system->instance, result, "xrAcquireSwapchainImage"))
        return -1;

    swapchain->lastAcquiredIndex = (mgint)imageIndex;
#if defined(MG_VULKAN)
    MGXR_LOGI("MGXR_Swapchain_AcquireImage: index=%u, VkImage=%p", imageIndex, (void*)swapchain->images[imageIndex].image);
#elif defined(MG_DIRECTX12)
    MGXR_LOGI("MGXR_Swapchain_AcquireImage: index=%u, ID3D12Resource=%p", imageIndex, (void*)swapchain->images[imageIndex].texture);
#endif
    return (mgint)imageIndex;
}

MG_EXPORT void MGXR_Swapchain_WaitImage(MGXR_Swapchain* swapchain, mglong timeout)
{
    if (!swapchain)
        return;

    XrSwapchainImageWaitInfo waitInfo = {XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO};
    waitInfo.timeout = timeout;
    xrWaitSwapchainImage(swapchain->swapchain, &waitInfo);
}

MG_EXPORT void MGXR_Swapchain_ReleaseImage(MGXR_Swapchain* swapchain)
{
    if (!swapchain)
        return;

    XrSwapchainImageReleaseInfo releaseInfo = {XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO};
    xrReleaseSwapchainImage(swapchain->swapchain, &releaseInfo);
}

#if defined(MG_VULKAN)
MG_EXPORT void MGXR_Swapchain_GetVulkanImage(MGXR_Swapchain* swapchain, mgint index, void** vkImage, mgint* width, mgint* height)
{
    if (!swapchain || index < 0 || index >= (mgint)swapchain->images.size())
        return;

    if (vkImage)
        *vkImage = (void*)swapchain->images[index].image;
    if (width)
        *width = swapchain->width;
    if (height)
        *height = swapchain->height;
}
#elif defined(MG_DIRECTX12)
MG_EXPORT void MGXR_Swapchain_GetD3D12Texture(MGXR_Swapchain* swapchain, mgint index, void** d3d12Resource, mgint* width, mgint* height)
{
    if (!swapchain || index < 0 || index >= (mgint)swapchain->images.size())
        return;

    if (d3d12Resource)
        *d3d12Resource = (void*)swapchain->images[index].texture;
    if (width)
        *width = swapchain->width;
    if (height)
        *height = swapchain->height;
}
#endif

MG_EXPORT void MGXR_Swapchain_GetRecommendedSize(MGXR_Session* session, mgint* width, mgint* height)
{
    if (!session || session->configViews.empty())
        return;

    if (width)
        *width = session->configViews[0].recommendedImageRectWidth;
    if (height)
        *height = session->configViews[0].recommendedImageRectHeight;
}


// ============================================================================
// View/Projection
// ============================================================================

MG_EXPORT mgint MGXR_Session_GetViewCount(MGXR_Session* session)
{
    if (!session)
        return 0;
    return (mgint)session->configViews.size();
}

MG_EXPORT void MGXR_Session_GetViewProjection(MGXR_Session* session, mgint viewIndex, mglong displayTime, MGXR_ViewProjection* view)
{
    if (!session || !view)
        return;

    XrViewLocateInfo locateInfo = {XR_TYPE_VIEW_LOCATE_INFO};
    locateInfo.viewConfigurationType = session->viewConfigType;
    locateInfo.displayTime = displayTime;
    locateInfo.space = session->localSpace;

    XrViewState viewState = {XR_TYPE_VIEW_STATE};
    uint32_t viewCount = (uint32_t)session->views.size();
    XrResult result = xrLocateViews(session->session, &locateInfo, &viewState,
        viewCount, &viewCount, session->views.data());

    if (!xr_check(session->system->instance, result, "xrLocateViews"))
        return;

    if (viewIndex >= 0 && viewIndex < (mgint)viewCount)
    {
        const auto& xrView = session->views[viewIndex];
        view->Pose.PositionX = xrView.pose.position.x;
        view->Pose.PositionY = xrView.pose.position.y;
        view->Pose.PositionZ = xrView.pose.position.z;
        view->Pose.OrientationX = xrView.pose.orientation.x;
        view->Pose.OrientationY = xrView.pose.orientation.y;
        view->Pose.OrientationZ = xrView.pose.orientation.z;
        view->Pose.OrientationW = xrView.pose.orientation.w;
        view->FovAngleLeft = xrView.fov.angleLeft;
        view->FovAngleRight = xrView.fov.angleRight;
        view->FovAngleUp = xrView.fov.angleUp;
        view->FovAngleDown = xrView.fov.angleDown;
    }
}


// ============================================================================
// Reference Spaces
// ============================================================================

MG_EXPORT MGXR_Space* MGXR_Space_Create(MGXR_Session* session, MGXRReferenceSpaceType type)
{
    if (!session)
        return nullptr;

    auto space = new MGXR_Space();
    space->session = session;
    space->type = type;

    XrReferenceSpaceCreateInfo createInfo = {XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
    createInfo.poseInReferenceSpace.orientation.w = 1.0f;

    switch (type)
    {
        case MGXRReferenceSpaceType::View:
            createInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_VIEW;
            break;
        case MGXRReferenceSpaceType::Local:
            createInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_LOCAL;
            break;
        case MGXRReferenceSpaceType::Stage:
            createInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_STAGE;
            break;
    }

    XrResult result = xrCreateReferenceSpace(session->session, &createInfo, &space->space);
    if (!xr_check(session->system->instance, result, "xrCreateReferenceSpace"))
    {
        delete space;
        return nullptr;
    }

    return space;
}

MG_EXPORT void MGXR_Space_Destroy(MGXR_Space* space)
{
    if (!space)
        return;

    if (space->space != XR_NULL_HANDLE)
        xrDestroySpace(space->space);

    delete space;
}

MG_EXPORT void MGXR_Space_GetPose(MGXR_Space* space, MGXR_Space* baseSpace, mglong time, MGXR_Pose* pose)
{
    if (!space || !baseSpace || !pose)
        return;

    XrSpaceLocation location = {XR_TYPE_SPACE_LOCATION};
    XrResult result = xrLocateSpace(space->space, baseSpace->space, time, &location);
    if (!xr_check(space->session->system->instance, result, "xrLocateSpace"))
        return;

    if (location.locationFlags & XR_SPACE_LOCATION_POSITION_VALID_BIT)
    {
        pose->PositionX = location.pose.position.x;
        pose->PositionY = location.pose.position.y;
        pose->PositionZ = location.pose.position.z;
    }

    if (location.locationFlags & XR_SPACE_LOCATION_ORIENTATION_VALID_BIT)
    {
        pose->OrientationX = location.pose.orientation.x;
        pose->OrientationY = location.pose.orientation.y;
        pose->OrientationZ = location.pose.orientation.z;
        pose->OrientationW = location.pose.orientation.w;
    }
}


// ============================================================================
// Action System (VR Input)
// ============================================================================

MG_EXPORT MGXR_ActionSet* MGXR_ActionSet_Create(MGXR_System* system, const char* name, const char* localizedName)
{
    if (!system)
        return nullptr;

    auto actionSet = new MGXR_ActionSet();
    actionSet->system = system;
    actionSet->session = nullptr;

    XrActionSetCreateInfo createInfo = {XR_TYPE_ACTION_SET_CREATE_INFO};
    strncpy(createInfo.actionSetName, name, XR_MAX_ACTION_SET_NAME_SIZE);
    strncpy(createInfo.localizedActionSetName, localizedName, XR_MAX_LOCALIZED_ACTION_SET_NAME_SIZE);
    createInfo.priority = 0;

    XrResult result = xrCreateActionSet(system->instance, &createInfo, &actionSet->actionSet);
    if (!xr_check(system->instance, result, "xrCreateActionSet"))
    {
        delete actionSet;
        return nullptr;
    }

    return actionSet;
}

MG_EXPORT void MGXR_ActionSet_Destroy(MGXR_ActionSet* actionSet)
{
    if (!actionSet)
        return;

    if (actionSet->actionSet != XR_NULL_HANDLE)
        xrDestroyActionSet(actionSet->actionSet);

    delete actionSet;
}

MG_EXPORT MGXR_Action* MGXR_Action_Create(MGXR_ActionSet* actionSet, const char* name, const char* localizedName, MGXRActionType type)
{
    if (!actionSet)
        return nullptr;

    auto action = new MGXR_Action();
    action->actionSet = actionSet;
    action->type = type;
    action->cachedActionSpaces[0] = XR_NULL_HANDLE;
    action->cachedActionSpaces[1] = XR_NULL_HANDLE;

    // Create subaction paths for left/right hands
    XrInstance instance = actionSet->system->instance;
    xrStringToPath(instance, "/user/hand/left", &action->subactionPaths[0]);
    xrStringToPath(instance, "/user/hand/right", &action->subactionPaths[1]);

    XrActionCreateInfo createInfo = {XR_TYPE_ACTION_CREATE_INFO};
    strncpy(createInfo.actionName, name, XR_MAX_ACTION_NAME_SIZE);
    strncpy(createInfo.localizedActionName, localizedName, XR_MAX_LOCALIZED_ACTION_NAME_SIZE);
    createInfo.countSubactionPaths = 2;
    createInfo.subactionPaths = action->subactionPaths;

    switch (type)
    {
        case MGXRActionType::Boolean:
            createInfo.actionType = XR_ACTION_TYPE_BOOLEAN_INPUT;
            break;
        case MGXRActionType::Float:
            createInfo.actionType = XR_ACTION_TYPE_FLOAT_INPUT;
            break;
        case MGXRActionType::Vector2:
            createInfo.actionType = XR_ACTION_TYPE_VECTOR2F_INPUT;
            break;
        case MGXRActionType::Pose:
            createInfo.actionType = XR_ACTION_TYPE_POSE_INPUT;
            break;
        case MGXRActionType::Haptic:
            createInfo.actionType = XR_ACTION_TYPE_VIBRATION_OUTPUT;
            break;
    }

    XrResult result = xrCreateAction(actionSet->actionSet, &createInfo, &action->action);
    if (!xr_check(instance, result, "xrCreateAction"))
    {
        delete action;
        return nullptr;
    }

    return action;
}

MG_EXPORT void MGXR_Action_Destroy(MGXR_Action* action)
{
    if (!action)
        return;

    for (int i = 0; i < 2; i++)
    {
        if (action->cachedActionSpaces[i] != XR_NULL_HANDLE)
            xrDestroySpace(action->cachedActionSpaces[i]);
    }

    if (action->action != XR_NULL_HANDLE)
        xrDestroyAction(action->action);

    delete action;
}

MG_EXPORT void MGXR_Action_SuggestBindings(MGXR_System* system, const char* interactionProfile, MGXR_Action** actions, mgbyte** paths, mgint count)
{
    if (!system || !actions || !paths || count <= 0)
        return;

    XrPath profilePath;
    xrStringToPath(system->instance, interactionProfile, &profilePath);

    std::vector<XrActionSuggestedBinding> bindings(count);
    for (mgint i = 0; i < count; i++)
    {
        bindings[i].action = actions[i]->action;
        xrStringToPath(system->instance, (const char*)paths[i], &bindings[i].binding);
    }

    XrInteractionProfileSuggestedBinding suggestedBindings = {XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING};
    suggestedBindings.interactionProfile = profilePath;
    suggestedBindings.countSuggestedBindings = (uint32_t)count;
    suggestedBindings.suggestedBindings = bindings.data();

    xrSuggestInteractionProfileBindings(system->instance, &suggestedBindings);
}

MG_EXPORT void MGXR_ActionSet_AttachToSession(MGXR_Session* session, MGXR_ActionSet** sets, mgint count)
{
    if (!session || !sets || count <= 0)
        return;

    std::vector<XrActionSet> xrSets(count);
    for (mgint i = 0; i < count; i++)
    {
        xrSets[i] = sets[i]->actionSet;
        sets[i]->session = session;
    }

    XrSessionActionSetsAttachInfo attachInfo = {XR_TYPE_SESSION_ACTION_SETS_ATTACH_INFO};
    attachInfo.countActionSets = (uint32_t)count;
    attachInfo.actionSets = xrSets.data();

    xrAttachSessionActionSets(session->session, &attachInfo);
}

MG_EXPORT void MGXR_ActionSet_Sync(MGXR_Session* session, MGXR_ActionSet** sets, mgint count)
{
    if (!sets || count <= 0)
        return;

    // Use the session from the first action set if not provided
    MGXR_Session* sess = session ? session : sets[0]->session;
    if (!sess)
        return;

    std::vector<XrActiveActionSet> activeSets(count);
    for (mgint i = 0; i < count; i++)
    {
        activeSets[i].actionSet = sets[i]->actionSet;
        activeSets[i].subactionPath = XR_NULL_PATH;
    }

    XrActionsSyncInfo syncInfo = {XR_TYPE_ACTIONS_SYNC_INFO};
    syncInfo.countActiveActionSets = (uint32_t)count;
    syncInfo.activeActionSets = activeSets.data();

    xrSyncActions(sess->session, &syncInfo);
}

MG_EXPORT void MGXR_Action_GetStateBool(MGXR_Action* action, mgbyte* value, mgbyte* changed, mgint hand)
{
    if (!action || !value || hand < 0 || hand > 1)
        return;

    XrActionStateGetInfo getInfo = {XR_TYPE_ACTION_STATE_GET_INFO};
    getInfo.action = action->action;
    getInfo.subactionPath = action->subactionPaths[hand];

    XrActionStateBoolean state = {XR_TYPE_ACTION_STATE_BOOLEAN};
    xrGetActionStateBoolean(action->actionSet->session->session, &getInfo, &state);

    *value = state.currentState ? 1 : 0;
    if (changed)
        *changed = state.changedSinceLastSync ? 1 : 0;
}

MG_EXPORT void MGXR_Action_GetStateFloat(MGXR_Action* action, mgfloat* value, mgbyte* changed, mgint hand)
{
    if (!action || !value || hand < 0 || hand > 1)
        return;

    XrActionStateGetInfo getInfo = {XR_TYPE_ACTION_STATE_GET_INFO};
    getInfo.action = action->action;
    getInfo.subactionPath = action->subactionPaths[hand];

    XrActionStateFloat state = {XR_TYPE_ACTION_STATE_FLOAT};
    xrGetActionStateFloat(action->actionSet->session->session, &getInfo, &state);

    *value = state.currentState;
    if (changed)
        *changed = state.changedSinceLastSync ? 1 : 0;
}

MG_EXPORT void MGXR_Action_GetStateVector2(MGXR_Action* action, mgfloat* x, mgfloat* y, mgbyte* changed, mgint hand)
{
    if (!action || !x || !y || hand < 0 || hand > 1)
        return;

    XrActionStateGetInfo getInfo = {XR_TYPE_ACTION_STATE_GET_INFO};
    getInfo.action = action->action;
    getInfo.subactionPath = action->subactionPaths[hand];

    XrActionStateVector2f state = {XR_TYPE_ACTION_STATE_VECTOR2F};
    xrGetActionStateVector2f(action->actionSet->session->session, &getInfo, &state);

    *x = state.currentState.x;
    *y = state.currentState.y;
    if (changed)
        *changed = state.changedSinceLastSync ? 1 : 0;
}

MG_EXPORT void MGXR_Action_GetStatePose(MGXR_Action* action, MGXR_Space* space, mglong time, MGXR_Pose* pose, mgint hand, mgint* outFlags)
{
    if (outFlags)
        *outFlags = 0;

    if (!action || !space || !pose || time == 0 || hand < 0 || hand > 1)
        return;

    // Create per-hand action space on first use and cache it
    if (action->cachedActionSpaces[hand] == XR_NULL_HANDLE)
    {
        XrActionSpaceCreateInfo spaceInfo = {XR_TYPE_ACTION_SPACE_CREATE_INFO};
        spaceInfo.action = action->action;
        spaceInfo.subactionPath = action->subactionPaths[hand];
        spaceInfo.poseInActionSpace.orientation.w = 1.0f;

        XrResult result = xrCreateActionSpace(action->actionSet->session->session, &spaceInfo, &action->cachedActionSpaces[hand]);
        if (!XR_SUCCEEDED(result))
        {
            MGXR_LOGE("MGXR_Action_GetStatePose: xrCreateActionSpace failed for hand=%d, result=%d", hand, (int)result);
            return;
        }
    }

    XrSpaceLocation location = {XR_TYPE_SPACE_LOCATION};
    XrResult result = xrLocateSpace(action->cachedActionSpaces[hand], space->space, time, &location);
    if (!XR_SUCCEEDED(result))
        return;

    if (outFlags)
        *outFlags = (mgint)location.locationFlags;

    if (location.locationFlags & XR_SPACE_LOCATION_POSITION_VALID_BIT)
    {
        pose->PositionX = location.pose.position.x;
        pose->PositionY = location.pose.position.y;
        pose->PositionZ = location.pose.position.z;
    }

    if (location.locationFlags & XR_SPACE_LOCATION_ORIENTATION_VALID_BIT)
    {
        pose->OrientationX = location.pose.orientation.x;
        pose->OrientationY = location.pose.orientation.y;
        pose->OrientationZ = location.pose.orientation.z;
        pose->OrientationW = location.pose.orientation.w;
    }
}

MG_EXPORT void MGXR_Action_ApplyHaptic(MGXR_Action* action, mgfloat amplitude, mgfloat frequency, mglong duration)
{
    if (!action)
        return;

    XrHapticVibration vibration = {XR_TYPE_HAPTIC_VIBRATION};
    vibration.amplitude = amplitude;
    vibration.frequency = frequency;
    vibration.duration = duration;

    XrHapticActionInfo hapticInfo = {XR_TYPE_HAPTIC_ACTION_INFO};
    hapticInfo.action = action->action;

    xrApplyHapticFeedback(action->actionSet->session->session, &hapticInfo, (XrHapticBaseHeader*)&vibration);
}

// --- Graphics device bridge functions ---
// These connect OpenXR swapchain images to the graphics device backbuffer.
// The actual setup happens in the graphics backend (MGG_Vulkan.cpp or MGG_DX12.cpp).

// Forward declaration of MGG functions we call — signature differs per backend
#if defined(MG_VULKAN)
extern "C" void MGG_GraphicsDevice_SetXRSwapchainImage(void* device, VkImage image, mgint width, mgint height, VkFormat format, mgint imageIndex, mgint imageCount);
#elif defined(MG_DIRECTX12)
extern "C" void MGG_GraphicsDevice_SetXRSwapchainImage(void* device, void* d3d12Resource, mgint width, mgint height, mgint format, mgint imageIndex, mgint imageCount);
#endif

MG_EXPORT mgint MGXR_Swapchain_GetImageCount(MGXR_Swapchain* swapchain)
{
    if (!swapchain)
        return 0;
    return (mgint)swapchain->images.size();
}

MG_EXPORT void MGXR_Swapchain_ConfigureDeviceImages(MGXR_Swapchain* swapchain, void* graphicsDevice)
{
    if (!swapchain || !graphicsDevice)
        return;

    mgint count = (mgint)swapchain->images.size();
    for (mgint i = 0; i < count; i++)
    {
#if defined(MG_VULKAN)
        MGG_GraphicsDevice_SetXRSwapchainImage(
            graphicsDevice,
            swapchain->images[i].image,
            swapchain->width,
            swapchain->height,
            VK_FORMAT_R8G8B8A8_SRGB,
            i,
            count);
#elif defined(MG_DIRECTX12)
        MGG_GraphicsDevice_SetXRSwapchainImage(
            graphicsDevice,
            (void*)swapchain->images[i].texture,
            swapchain->width,
            swapchain->height,
            29, // DXGI_FORMAT_R8G8B8A8_UNORM_SRGB
            i,
            count);
#endif
    }
}

MG_EXPORT void MGXR_Swapchain_SetActiveImage(MGXR_Swapchain* swapchain, void* graphicsDevice, mgint imageIndex)
{
    if (!swapchain || !graphicsDevice)
    {
        MGXR_LOGE("MGXR_Swapchain_SetActiveImage: null (swapchain=%p, device=%p)", swapchain, graphicsDevice);
        return;
    }

#if defined(MG_VULKAN)
    MGXR_LOGI("MGXR_Swapchain_SetActiveImage: index=%d, VkImage=%p, %dx%d",
        imageIndex, (void*)swapchain->images[imageIndex].image, swapchain->width, swapchain->height);

    // Set which swapchain image the graphics device should use as its backbuffer
    MGG_GraphicsDevice_SetXRSwapchainImage(
        graphicsDevice,
        swapchain->images[imageIndex].image,
        swapchain->width,
        swapchain->height,
        VK_FORMAT_R8G8B8A8_SRGB,
        imageIndex,
        (mgint)swapchain->images.size());
#elif defined(MG_DIRECTX12)
    MGXR_LOGI("MGXR_Swapchain_SetActiveImage: index=%d, ID3D12Resource=%p, %dx%d",
        imageIndex, (void*)swapchain->images[imageIndex].texture, swapchain->width, swapchain->height);

    MGG_GraphicsDevice_SetXRSwapchainImage(
        graphicsDevice,
        (void*)swapchain->images[imageIndex].texture,
        swapchain->width,
        swapchain->height,
        29, // DXGI_FORMAT_R8G8B8A8_UNORM_SRGB
        imageIndex,
        (mgint)swapchain->images.size());
#endif
}
