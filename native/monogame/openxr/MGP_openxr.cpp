// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

// MGP_openxr.cpp — Platform implementation for OpenXR
// Replaces SDL for VR platforms; implements the full api_MGP.h interface.

#include "api_MGP.h"
#include "api_MGXR.h"

#include "mg_common.h"

#include <openxr/openxr.h>

#include <cstring>
#include <cstdlib>

#ifdef __ANDROID__
#include <pthread.h>
#include <android/log.h>
#define MGP_LOGI(...) __android_log_print(ANDROID_LOG_INFO, "MonoGame-MGP", __VA_ARGS__)
#define MGP_LOGE(...) __android_log_print(ANDROID_LOG_ERROR, "MonoGame-MGP", __VA_ARGS__)
#else
#define MGP_LOGI(...) do { fprintf(stderr, "[MGP] " __VA_ARGS__); fprintf(stderr, "\n"); } while(0)
#define MGP_LOGE(...) do { fprintf(stderr, "[MGP ERROR] " __VA_ARGS__); fprintf(stderr, "\n"); } while(0)
#endif


// The OpenXR platform has no traditional windows or cursors.
// These structs exist to satisfy the api_MGP.h interface.

struct MGP_Platform
{
    std::vector<MGP_Window*> windows;
    std::queue<MGP_Event> queued_events;

    // OpenXR session state tracking (updated via xrPollEvent)
    MGXR_System* xrSystem;
    MGXR_Session* xrSession;
    bool sessionRunning;
    bool exitRequested;

    // VR controller input — OpenXR action system
    MGXR_ActionSet* actionSet;

    // Per-controller actions (index 0 = left, 1 = right)
    MGXR_Action* triggerAction;
    MGXR_Action* gripAction;
    MGXR_Action* thumbstickAction;
    MGXR_Action* thumbstickClickAction;
    MGXR_Action* primaryButtonAction;     // A (right), X (left)
    MGXR_Action* secondaryButtonAction;   // B (right), Y (left)
    MGXR_Action* menuAction;
    MGXR_Action* hapticAction;
    MGXR_Action* aimPoseAction;
    MGXR_Action* gripPoseAction;

    // Previous frame state for change detection
    struct ControllerState
    {
        float trigger;
        float grip;
        float thumbstickX;
        float thumbstickY;
        bool thumbstickClick;
        bool primaryButton;
        bool secondaryButton;
        bool menuButton;
        bool connected;
    };
    ControllerState prevState[2];
    bool controllersAdded;
    bool actionsAttached;
    bool controllerPolledThisFrame;

    // VR pose tracking — set from C# before polling
    MGXR_Space* activeSpace;
    mglong displayTime;

    // Display names for controller caps
    char leftName[32];
    char rightName[32];
};

struct MGP_Window
{
    MGP_Platform* platform = nullptr;
    std::string identifier;
    mgint width;
    mgint height;
};

struct MGP_Cursor
{
    // No cursor in VR
};


// ============================================================================
// Platform lifecycle
// ============================================================================

MGP_Platform* MGP_Platform_Create(MGGameRunBehavior& behavior)
{
#ifdef __ANDROID__
    behavior = MGGameRunBehavior::Asynchronous;
#else
    behavior = MGGameRunBehavior::Synchronous;
#endif

    auto platform = new MGP_Platform();
    platform->xrSystem = nullptr;
    platform->xrSession = nullptr;
    platform->sessionRunning = false;
    platform->exitRequested = false;
    platform->actionSet = nullptr;
    platform->triggerAction = nullptr;
    platform->gripAction = nullptr;
    platform->thumbstickAction = nullptr;
    platform->thumbstickClickAction = nullptr;
    platform->primaryButtonAction = nullptr;
    platform->secondaryButtonAction = nullptr;
    platform->menuAction = nullptr;
    platform->hapticAction = nullptr;
    platform->aimPoseAction = nullptr;
    platform->gripPoseAction = nullptr;
    platform->controllersAdded = false;
    platform->actionsAttached = false;
    platform->controllerPolledThisFrame = false;
    memset(platform->prevState, 0, sizeof(platform->prevState));
    strncpy(platform->leftName, "OpenXR VR Controllers", sizeof(platform->leftName));
    strncpy(platform->rightName, "OpenXR Right Controller", sizeof(platform->rightName));

    // Create the OpenXR system (instance + HMD detection)
    platform->xrSystem = MGXR_System_Create();

    return platform;
}

void MGP_Platform_Destroy(MGP_Platform* platform)
{
    assert(platform != nullptr);

    for (auto window : platform->windows)
        MGP_Window_Destroy(window);

    // Destroy actions before action set
    if (platform->triggerAction)         MGXR_Action_Destroy(platform->triggerAction);
    if (platform->gripAction)            MGXR_Action_Destroy(platform->gripAction);
    if (platform->thumbstickAction)      MGXR_Action_Destroy(platform->thumbstickAction);
    if (platform->thumbstickClickAction) MGXR_Action_Destroy(platform->thumbstickClickAction);
    if (platform->primaryButtonAction)   MGXR_Action_Destroy(platform->primaryButtonAction);
    if (platform->secondaryButtonAction) MGXR_Action_Destroy(platform->secondaryButtonAction);
    if (platform->menuAction)            MGXR_Action_Destroy(platform->menuAction);
    if (platform->hapticAction)          MGXR_Action_Destroy(platform->hapticAction);
    if (platform->aimPoseAction)         MGXR_Action_Destroy(platform->aimPoseAction);
    if (platform->gripPoseAction)        MGXR_Action_Destroy(platform->gripPoseAction);
    if (platform->actionSet)             MGXR_ActionSet_Destroy(platform->actionSet);

    if (platform->xrSession)
        MGXR_Session_Destroy(platform->xrSession);
    if (platform->xrSystem)
        MGXR_System_Destroy(platform->xrSystem);

    delete platform;
}

// Return the XR system handle from the platform (not part of auto-generated api_MGP.h
// because only the OpenXR platform has an XR system)
MG_EXPORT void* MGP_Platform_GetXRSystem(MGP_Platform* platform)
{
    if (!platform)
        return nullptr;
    return platform->xrSystem;
}

void* MGP_Platform_MakePath(const char* location, const char* path)
{
    assert(location != nullptr);
    assert(path != nullptr);

    size_t location_len = strlen(location);
    size_t path_len = strlen(path);
    size_t separator_len = (location_len > 0) ? strlen(MG_PATH_SEPARATOR) : 0;

    size_t length = location_len + separator_len + path_len + 1;

    char* fpath = (char*)malloc(length);
    assert(fpath != nullptr);

    if (location_len > 0)
        snprintf(fpath, length, "%s%s%s", location, MG_PATH_SEPARATOR, path);
    else
        snprintf(fpath, length, "%s", path);

    return reinterpret_cast<void*>(fpath);
}

void MGP_Platform_Free(void* ptr)
{
    assert(ptr != nullptr);
    free(ptr);
}

void MGP_Platform_BeforeInitialize(MGP_Platform* platform)
{
    assert(platform != nullptr);
}

MGMonoGamePlatform MGP_Platform_GetPlatform()
{
    return MGMonoGamePlatform::OpenXR;
}

MGGraphicsBackend MGP_Platform_GetGraphicsBackend()
{
    return MGGraphicsBackend::Vulkan;
}

void MGP_Platform_StartRunLoop(MGP_Platform* platform)
{
    assert(platform != nullptr);
    // Synchronous mode — nothing to do here
}

void MGP_Platform_StartRunLoopAsync(MGP_Platform* platform, MGP_FrameCallback callback)
{
    assert(platform != nullptr);
    assert(callback != nullptr);

#ifdef __ANDROID__
    // Android: run the game loop on a dedicated thread so the Android UI
    // thread remains free for lifecycle callbacks (onResume/onPause),
    // which the OpenXR runtime needs for session state transitions.
    struct AsyncLoopData
    {
        MGP_FrameCallback callback;
    };

    auto* data = new AsyncLoopData{ callback };

    pthread_t thread;
    pthread_create(&thread, nullptr, [](void* arg) -> void*
    {
        auto* d = static_cast<AsyncLoopData*>(arg);
        auto cb = d->callback;
        delete d;

        while (cb() == 0)
        {
            // callback returns 0 to continue, non-zero to exit
        }

        return nullptr;
    }, data);

    // Detach — the thread runs independently and the callback handles
    // its own cleanup when it signals exit.
    pthread_detach(thread);
#else
    // Non-Android: caller should use RunLoop() for synchronous platforms.
    // Fallback: just run the loop inline.
    while (callback() == 0)
    {
    }
#endif
}

// Creates the default VR controller action set with standard bindings.
// Called once after the OpenXR session is ready (from BeforeRun).
static void InitializeVRActions(MGP_Platform* platform)
{
    if (!platform->xrSystem || platform->actionSet)
        return;

    // Create the default action set for VR controllers
    platform->actionSet = MGXR_ActionSet_Create(platform->xrSystem, "gameplay", "Gameplay Actions");
    if (!platform->actionSet)
        return;

    // Create actions for each input type
    // These are all "split" across left/right hands by the OpenXR runtime
    // via subaction paths set during Action_Create
    platform->triggerAction         = MGXR_Action_Create(platform->actionSet, "trigger",         "Trigger",         MGXRActionType::Float);
    platform->gripAction            = MGXR_Action_Create(platform->actionSet, "grip",            "Grip",            MGXRActionType::Float);
    platform->thumbstickAction      = MGXR_Action_Create(platform->actionSet, "thumbstick",      "Thumbstick",      MGXRActionType::Vector2);
    platform->thumbstickClickAction = MGXR_Action_Create(platform->actionSet, "thumbstick_click","Thumbstick Click", MGXRActionType::Boolean);
    platform->primaryButtonAction   = MGXR_Action_Create(platform->actionSet, "primary_button",  "Primary Button",  MGXRActionType::Boolean);
    platform->secondaryButtonAction = MGXR_Action_Create(platform->actionSet, "secondary_button","Secondary Button", MGXRActionType::Boolean);
    platform->menuAction            = MGXR_Action_Create(platform->actionSet, "menu",            "Menu",            MGXRActionType::Boolean);
    platform->hapticAction          = MGXR_Action_Create(platform->actionSet, "haptic",          "Haptic",          MGXRActionType::Haptic);
    platform->aimPoseAction         = MGXR_Action_Create(platform->actionSet, "aim_pose",        "Aim Pose",        MGXRActionType::Pose);
    platform->gripPoseAction        = MGXR_Action_Create(platform->actionSet, "grip_pose",       "Grip Pose",       MGXRActionType::Pose);

    // Oculus Touch controller bindings (Quest 2 primary target)
    {
        MGXR_Action* actions[] = {
            platform->triggerAction, platform->triggerAction,
            platform->gripAction, platform->gripAction,
            platform->thumbstickAction, platform->thumbstickAction,
            platform->thumbstickClickAction, platform->thumbstickClickAction,
            platform->primaryButtonAction, platform->primaryButtonAction,
            platform->secondaryButtonAction, platform->secondaryButtonAction,
            platform->menuAction,
            platform->hapticAction, platform->hapticAction,
            platform->aimPoseAction, platform->aimPoseAction,
            platform->gripPoseAction, platform->gripPoseAction,
        };
        const char* paths[] = {
            "/user/hand/left/input/trigger/value",  "/user/hand/right/input/trigger/value",
            "/user/hand/left/input/squeeze/value",  "/user/hand/right/input/squeeze/value",
            "/user/hand/left/input/thumbstick",     "/user/hand/right/input/thumbstick",
            "/user/hand/left/input/thumbstick/click","/user/hand/right/input/thumbstick/click",
            "/user/hand/left/input/x/click",        "/user/hand/right/input/a/click",
            "/user/hand/left/input/y/click",        "/user/hand/right/input/b/click",
            "/user/hand/left/input/menu/click",
            "/user/hand/left/output/haptic",        "/user/hand/right/output/haptic",
            "/user/hand/left/input/aim/pose",       "/user/hand/right/input/aim/pose",
            "/user/hand/left/input/grip/pose",      "/user/hand/right/input/grip/pose",
        };
        int count = sizeof(actions) / sizeof(actions[0]);
        MGXR_Action_SuggestBindings(platform->xrSystem,
            "/interaction_profiles/oculus/touch_controller",
            actions, (mgbyte**)paths, count);
    }

    // KHR Simple controller bindings (fallback for any OpenXR runtime)
    {
        MGXR_Action* actions[] = {
            platform->primaryButtonAction, platform->primaryButtonAction,
            platform->menuAction, platform->menuAction,
            platform->hapticAction, platform->hapticAction,
            platform->aimPoseAction, platform->aimPoseAction,
            platform->gripPoseAction, platform->gripPoseAction,
        };
        const char* paths[] = {
            "/user/hand/left/input/select/click",   "/user/hand/right/input/select/click",
            "/user/hand/left/input/menu/click",     "/user/hand/right/input/menu/click",
            "/user/hand/left/output/haptic",        "/user/hand/right/output/haptic",
            "/user/hand/left/input/aim/pose",       "/user/hand/right/input/aim/pose",
            "/user/hand/left/input/grip/pose",      "/user/hand/right/input/grip/pose",
        };
        int count = sizeof(actions) / sizeof(actions[0]);
        MGXR_Action_SuggestBindings(platform->xrSystem,
            "/interaction_profiles/khr/simple_controller",
            actions, (mgbyte**)paths, count);
    }
}

// Converts a float axis value (0..1 or -1..1) to the short range expected by MGP events.
static mgshort ToAxisShort(float value)
{
    int v = (int)(value * 32767.0f);
    if (v > 32767) v = 32767;
    if (v < -32767) v = -32767;
    return (mgshort)v;
}

// Pushes a controller state change event into the platform event queue.
static void PushControllerEvent(MGP_Platform* platform, int controllerId,
                                 MGControllerInput input, mgshort value)
{
    MGP_Event event_ = {};
    event_.Type = MGEventType::ControllerStateChange;
    event_.Timestamp = 0;
    event_.Controller.Id = controllerId;
    event_.Controller.Input = input;
    event_.Controller.Value = value;
    platform->queued_events.push(event_);
}

// Pushes a VR controller pose event.
static void PushPoseEvent(MGP_Platform* platform, int hand, int poseType, const MGXR_Pose& pose, mgint flags)
{
    MGP_Event event_ = {};
    event_.Type = MGEventType::VRControllerPose;
    event_.Timestamp = 0;
    event_.VRPose.Hand = hand;
    event_.VRPose.PoseType = poseType;
    event_.VRPose.PosX = pose.PositionX;
    event_.VRPose.PosY = pose.PositionY;
    event_.VRPose.PosZ = pose.PositionZ;
    event_.VRPose.OriX = pose.OrientationX;
    event_.VRPose.OriY = pose.OrientationY;
    event_.VRPose.OriZ = pose.OrientationZ;
    event_.VRPose.OriW = pose.OrientationW;
    event_.VRPose.Flags = flags;
    platform->queued_events.push(event_);
}

// Called from C# to provide tracking context for pose queries.
MG_EXPORT void MGP_Platform_SetVRTrackingState(MGP_Platform* platform, MGXR_Space* space, mglong displayTime)
{
    if (platform)
    {
        if (!platform->activeSpace && space)
            MGP_LOGI("MGP_Platform_SetVRTrackingState: activeSpace set to %p, displayTime=%lld", space, (long long)displayTime);
        if (space && !displayTime)
            MGP_LOGI("MGP_Platform_SetVRTrackingState: WARNING displayTime is 0");
        platform->activeSpace = space;
        platform->displayTime = displayTime;
    }
}

// Syncs the OpenXR action set and pushes controller state change events.
static void PollVRControllerState(MGP_Platform* platform)
{
    if (!platform->actionSet || !platform->actionsAttached)
    {
        static bool loggedOnce = false;
        if (!loggedOnce)
        {
            MGP_LOGI("PollVRControllerState: SKIPPED (actionSet=%p, actionsAttached=%d)",
                platform->actionSet, (int)platform->actionsAttached);
            loggedOnce = true;
        }
        return;
    }

    // Emit a single ControllerAdded event — both hands merged into one gamepad
    if (!platform->controllersAdded)
    {
        platform->controllersAdded = true;
        MGP_Event event_ = {};
        event_.Type = MGEventType::ControllerAdded;
        event_.Controller.Id = 0;
        platform->queued_events.push(event_);
    }

    // Sync the action set to get current controller state
    MGXR_ActionSet* sets[] = { platform->actionSet };
    MGXR_ActionSet_Sync(nullptr, sets, 1);

    // All events go to controller id 0 (single merged gamepad)
    const int controllerId = 0;

    // Read and push state changes for each hand, mapped to left/right sides of one gamepad
    for (int hand = 0; hand < 2; hand++)
    {
        auto& prev = platform->prevState[hand];
        mgbyte changed = 0;

        // Trigger: left hand → LeftTrigger, right hand → RightTrigger
        {
            float value = 0;
            MGXR_Action_GetStateFloat(platform->triggerAction, &value, &changed, hand);
            if (value != prev.trigger)
            {
                auto input = (hand == 0) ? MGControllerInput::LeftTrigger : MGControllerInput::RightTrigger;
                PushControllerEvent(platform, controllerId, input, ToAxisShort(value));
                prev.trigger = value;
            }
        }

        // Grip: left hand → LeftShoulder, right hand → RightShoulder (button, threshold 0.5)
        {
            float value = 0;
            MGXR_Action_GetStateFloat(platform->gripAction, &value, &changed, hand);
            if (value != prev.grip)
            {
                bool pressed = value > 0.5f;
                bool wasPressed = prev.grip > 0.5f;
                if (pressed != wasPressed)
                {
                    auto input = (hand == 0) ? MGControllerInput::LeftShoulder : MGControllerInput::RightShoulder;
                    PushControllerEvent(platform, controllerId, input, pressed ? 1 : 0);
                }
                prev.grip = value;
            }
        }

        // Thumbstick: left hand → LeftStick, right hand → RightStick
        {
            float x = 0, y = 0;
            MGXR_Action_GetStateVector2(platform->thumbstickAction, &x, &y, &changed, hand);
            if (x != prev.thumbstickX)
            {
                auto input = (hand == 0) ? MGControllerInput::LeftStickX : MGControllerInput::RightStickX;
                PushControllerEvent(platform, controllerId, input, ToAxisShort(x));
                prev.thumbstickX = x;
            }
            if (y != prev.thumbstickY)
            {
                auto input = (hand == 0) ? MGControllerInput::LeftStickY : MGControllerInput::RightStickY;
                PushControllerEvent(platform, controllerId, input, ToAxisShort(y));
                prev.thumbstickY = y;
            }
        }

        // Thumbstick click: left → LeftStick button, right → RightStick button
        {
            mgbyte value = 0;
            MGXR_Action_GetStateBool(platform->thumbstickClickAction, &value, &changed, hand);
            bool pressed = value != 0;
            if (pressed != prev.thumbstickClick)
            {
                auto input = (hand == 0) ? MGControllerInput::LeftStick : MGControllerInput::RightStick;
                PushControllerEvent(platform, controllerId, input, pressed ? 1 : 0);
                prev.thumbstickClick = pressed;
            }
        }

        // Primary button: X (left hand) / A (right hand)
        {
            mgbyte value = 0;
            MGXR_Action_GetStateBool(platform->primaryButtonAction, &value, &changed, hand);
            bool pressed = value != 0;
            if (pressed != prev.primaryButton)
            {
                auto input = (hand == 0) ? MGControllerInput::X : MGControllerInput::A;
                PushControllerEvent(platform, controllerId, input, pressed ? 1 : 0);
                prev.primaryButton = pressed;
            }
        }

        // Secondary button: Y (left hand) / B (right hand)
        {
            mgbyte value = 0;
            MGXR_Action_GetStateBool(platform->secondaryButtonAction, &value, &changed, hand);
            bool pressed = value != 0;
            if (pressed != prev.secondaryButton)
            {
                auto input = (hand == 0) ? MGControllerInput::Y : MGControllerInput::B;
                PushControllerEvent(platform, controllerId, input, pressed ? 1 : 0);
                prev.secondaryButton = pressed;
            }
        }

        // Menu: left hand → Start, right hand → Back
        {
            mgbyte value = 0;
            MGXR_Action_GetStateBool(platform->menuAction, &value, &changed, hand);
            bool pressed = value != 0;
            if (pressed != prev.menuButton)
            {
                auto input = (hand == 0) ? MGControllerInput::Start : MGControllerInput::Back;
                PushControllerEvent(platform, controllerId, input, pressed ? 1 : 0);
                prev.menuButton = pressed;
            }
        }
    }

    // Push pose events for each hand (aim + grip = 4 events per frame)
    // Skip if displayTime is 0 (BeginFrame hasn't been called yet this frame)
    if (platform->activeSpace && platform->displayTime != 0)
    {
        static int poseLogCount = 0;
        for (int hand = 0; hand < 2; hand++)
        {
            MGXR_Pose pose = {};
            mgint aimFlags = 0;
            MGXR_Action_GetStatePose(platform->aimPoseAction, platform->activeSpace,
                                     platform->displayTime, &pose, hand, &aimFlags);
            if (poseLogCount < 10)
                MGP_LOGI("PollPose: hand=%d aim=(%f,%f,%f) ori=(%f,%f,%f,%f) flags=0x%x",
                    hand, pose.PositionX, pose.PositionY, pose.PositionZ,
                    pose.OrientationX, pose.OrientationY, pose.OrientationZ, pose.OrientationW, aimFlags);
            PushPoseEvent(platform, hand, 0, pose, aimFlags); // 0 = aim

            pose = {};
            mgint gripFlags = 0;
            MGXR_Action_GetStatePose(platform->gripPoseAction, platform->activeSpace,
                                     platform->displayTime, &pose, hand, &gripFlags);
            if (poseLogCount < 10)
                MGP_LOGI("PollPose: hand=%d grip=(%f,%f,%f) ori=(%f,%f,%f,%f) flags=0x%x",
                    hand, pose.PositionX, pose.PositionY, pose.PositionZ,
                    pose.OrientationX, pose.OrientationY, pose.OrientationZ, pose.OrientationW, gripFlags);
            PushPoseEvent(platform, hand, 1, pose, gripFlags); // 1 = grip
        }
        poseLogCount++;
    }
    else
    {
        static bool loggedNoSpace = false;
        if (!loggedNoSpace)
        {
            MGP_LOGI("PollVRControllerState: activeSpace is NULL — no pose events will be generated");
            loggedNoSpace = true;
        }
    }
}

mgbyte MGP_Platform_BeforeRun(MGP_Platform* platform)
{
    assert(platform != nullptr);

    // Initialize VR controller action bindings on first run
    InitializeVRActions(platform);

    return 1;
}

// Called from C# after the OpenXR session is created.
// Attaches the platform's action set to the session so input can be synced.
MG_EXPORT void MGXR_Platform_AttachActionsToSession(void* platformPtr, MGXR_Session* session)
{
    auto* platform = (MGP_Platform*)platformPtr;
    if (!platform || !session || !platform->actionSet || platform->actionsAttached)
        return;

    platform->xrSession = session;

    MGXR_ActionSet* sets[] = { platform->actionSet };
    MGXR_ActionSet_AttachToSession(session, sets, 1);
    platform->actionsAttached = true;
}

mgbyte MGP_Platform_BeforeUpdate(MGP_Platform* platform)
{
    assert(platform != nullptr);
    return !platform->exitRequested ? 1 : 0;
}

mgbyte MGP_Platform_BeforeDraw(MGP_Platform* platform)
{
    assert(platform != nullptr);
    return !platform->exitRequested ? 1 : 0;
}


// ============================================================================
// Event polling — processes OpenXR events
// ============================================================================

mgbyte MGP_Platform_PollEvent(MGP_Platform* platform, MGP_Event& event_)
{
    assert(platform != nullptr);

    // Return queued events first
    if (platform->queued_events.size() > 0)
    {
        event_ = platform->queued_events.front();
        platform->queued_events.pop();
        return 1;
    }

    // Sync VR controller inputs once per poll cycle (not on every PollEvent call).
    // After syncing, return any generated events. If no events were generated,
    // return 0 — the next PollEvent call won't re-sync until the queue was fully drained.
    if (!platform->controllerPolledThisFrame)
    {
        platform->controllerPolledThisFrame = true;
        PollVRControllerState(platform);

        if (platform->queued_events.size() > 0)
        {
            event_ = platform->queued_events.front();
            platform->queued_events.pop();
            return 1;
        }
    }

    // Reset for next frame
    platform->controllerPolledThisFrame = false;
    return 0;
}


// ============================================================================
// Window — VR has no traditional window, but the API requires one
// ============================================================================

MGP_Window* MGP_Window_Create(MGP_Platform* platform, mgint& width, mgint& height, const char* title)
{
    assert(platform != nullptr);

    auto window = new MGP_Window();
    window->platform = platform;
    window->identifier = "OpenXR HMD";

    // Use recommended swapchain size from OpenXR if a session exists
    if (platform->xrSession)
    {
        mgint recWidth = 0, recHeight = 0;
        MGXR_Swapchain_GetRecommendedSize(platform->xrSession, &recWidth, &recHeight);
        if (recWidth > 0 && recHeight > 0)
        {
            width = recWidth;
            height = recHeight;
        }
    }

    window->width = width;
    window->height = height;

    platform->windows.push_back(window);
    return window;
}

void MGP_Window_Destroy(MGP_Window* window)
{
    assert(window != nullptr);
    mg_remove(window->platform->windows, window);
    delete window;
}

void MGP_Window_SetIconBitmap(MGP_Window* window, mgbyte* icon, mgint length)
{
    // No-op in VR
}

void* MGP_Window_GetNativeHandle(MGP_Window* window)
{
    assert(window != nullptr);
    // In OpenXR mode, return nullptr — the Vulkan backend will use
    // OpenXR-provided swapchain images instead of a window surface
    return nullptr;
}

mgbyte MGP_Window_GetAllowUserResizing(MGP_Window* window)
{
    return 0; // VR windows aren't user-resizable
}

void MGP_Window_SetAllowUserResizing(MGP_Window* window, mgbyte allow)
{
    // No-op in VR
}

mgbyte MGP_Window_GetIsBorderless(MGP_Window* window)
{
    return 1; // VR is always borderless
}

void MGP_Window_SetIsBorderless(MGP_Window* window, mgbyte borderless)
{
    // No-op in VR
}

void MGP_Window_SetTitle(MGP_Window* window, const char* title)
{
    // No-op in VR
}

void MGP_Window_Show(MGP_Window* window, mgbyte show)
{
    // No-op in VR
}

void MGP_Window_GetPosition(MGP_Window* window, mgint& x, mgint& y)
{
    x = 0;
    y = 0;
}

void MGP_Window_SetPosition(MGP_Window* window, mgint x, mgint y)
{
    // No-op in VR
}

void MGP_Window_SetClientSize(MGP_Window* window, mgint width, mgint height)
{
    if (window)
    {
        window->width = width;
        window->height = height;
    }
}

void MGP_Window_SetCursor(MGP_Window* window, MGP_Cursor* cursor)
{
    // No-op in VR
}

mgint MGP_Window_ShowMessageBox(MGP_Window* window, const char* title, const char* description, const char* buttons, mgint count)
{
    // No message box support in VR — return first button
    return 0;
}

void MGP_Window_EnterFullScreen(MGP_Window* window, mgbyte useHardwareModeSwitch)
{
    // VR is always "fullscreen"
}

void MGP_Window_ExitFullScreen(MGP_Window* window)
{
    // No-op in VR
}


// ============================================================================
// Mouse — no mouse in VR
// ============================================================================

void MGP_Mouse_SetVisible(MGP_Platform* platform, mgbyte visible)
{
    // No-op in VR
}

void MGP_Mouse_WarpPosition(MGP_Window* window, mgint x, mgint y)
{
    // No-op in VR
}


// ============================================================================
// Cursor — no cursor in VR
// ============================================================================

MGP_Cursor* MGP_Cursor_Create(MGSystemCursor cursor_)
{
    return new MGP_Cursor();
}

MGP_Cursor* MGP_Cursor_CreateCustom(mgbyte* rgba, mgint width, mgint height, mgint originx, mgint originy)
{
    return new MGP_Cursor();
}

void MGP_Cursor_Destroy(MGP_Cursor* cursor)
{
    delete cursor;
}


// ============================================================================
// GamePad — VR controllers mapped through the OpenXR action system.
// Left controller = identifier 0 (PlayerIndex.One)
// Right controller = identifier 1 (PlayerIndex.Two)
// ============================================================================

mgint MGP_GamePad_GetMaxSupported()
{
    // Both VR controllers merged into a single gamepad (PlayerIndex.One)
    return 1;
}

void MGP_GamePad_GetCaps(MGP_Platform* platform, mgint identifer, MGP_ControllerCaps* caps)
{
    assert(platform != nullptr);
    assert(caps != nullptr);

    memset(caps, 0, sizeof(MGP_ControllerCaps));

    if (identifer != 0)
        return;

    caps->Identifier = (void*)platform->leftName;
    caps->DisplayName = (void*)platform->leftName;
    caps->GamePadType = MGGamePadType::GamePad;
    caps->HasLeftVibrationMotor = true;
    caps->HasRightVibrationMotor = true;
    caps->HasVoiceSupport = false;

    // Set InputFlags for all supported inputs
    mguint flags = 0;
    flags |= (1u << (mguint)MGControllerInput::A);
    flags |= (1u << (mguint)MGControllerInput::B);
    flags |= (1u << (mguint)MGControllerInput::X);
    flags |= (1u << (mguint)MGControllerInput::Y);
    flags |= (1u << (mguint)MGControllerInput::Start);
    flags |= (1u << (mguint)MGControllerInput::Back);
    flags |= (1u << (mguint)MGControllerInput::LeftStick);
    flags |= (1u << (mguint)MGControllerInput::RightStick);
    flags |= (1u << (mguint)MGControllerInput::LeftShoulder);
    flags |= (1u << (mguint)MGControllerInput::RightShoulder);
    flags |= (1u << (mguint)MGControllerInput::LeftStickX);
    flags |= (1u << (mguint)MGControllerInput::LeftStickY);
    flags |= (1u << (mguint)MGControllerInput::RightStickX);
    flags |= (1u << (mguint)MGControllerInput::RightStickY);
    flags |= (1u << (mguint)MGControllerInput::LeftTrigger);
    flags |= (1u << (mguint)MGControllerInput::RightTrigger);
    caps->InputFlags = flags;
}

mgbyte MGP_GamePad_SetVibration(MGP_Platform* platform, mgint identifer, mgfloat leftMotor, mgfloat rightMotor, mgfloat leftTrigger, mgfloat rightTrigger)
{
    assert(platform != nullptr);

    if (!platform->hapticAction || identifer != 0)
        return 0;

    // leftMotor vibrates the left controller, rightMotor vibrates the right controller.
    // The OpenXR haptic action has subaction paths for each hand — the runtime
    // routes amplitude to the correct controller. For now we apply the stronger
    // of the two values to both hands via a single haptic pulse.
    mgfloat amplitude = (leftMotor > rightMotor) ? leftMotor : rightMotor;
    if (amplitude <= 0.0f)
        return 1;

    // 160Hz is a common haptic frequency for VR controllers
    // Duration: 100ms in nanoseconds
    MGXR_Action_ApplyHaptic(platform->hapticAction, amplitude, 160.0f, 100000000);
    return 1;
}
 
