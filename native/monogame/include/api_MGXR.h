// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
                
// This code is auto generated, don't modify it by hand.
// To regenerate it run: Tools/MonoGame.Generator.CTypes

#pragma once

#include "api_common.h"
#include "api_enums.h"
#include "api_structs.h"


struct MGXR_System;
struct MGXR_Session;
struct MGXR_Swapchain;
struct MGXR_Space;
struct MGXR_ActionSet;
struct MGXR_Action;

MG_EXPORT MGXR_System* MGXR_System_Create();
MG_EXPORT void MGXR_System_Destroy(MGXR_System* system);
MG_EXPORT mgbyte MGXR_System_IsHmdPresent(MGXR_System* system);
struct MGXR_DeviceHandles;
MG_EXPORT MGXR_Session* MGXR_Session_Create(MGXR_System* system, MGXR_DeviceHandles* deviceHandles);
MG_EXPORT void MGXR_Session_Destroy(MGXR_Session* session);
MG_EXPORT MGXRSessionState MGXR_Session_GetState(MGXR_Session* session);
MG_EXPORT mgbyte MGXR_Session_BeginFrame(MGXR_Session* session, mglong* predictedDisplayTime, mglong* predictedDisplayPeriod);
MG_EXPORT void MGXR_Session_EndFrame(MGXR_Session* session, mglong displayTime);
MG_EXPORT void MGXR_Session_EndFrameStereo(MGXR_Session* session, MGXR_Swapchain* leftSwapchain, MGXR_Swapchain* rightSwapchain, mglong displayTime);
MG_EXPORT void MGXR_Session_RequestExit(MGXR_Session* session);
MG_EXPORT MGXR_Swapchain* MGXR_Swapchain_Create(MGXR_Session* session, mgint width, mgint height, mgint sampleCount, mgint arraySize);
MG_EXPORT void MGXR_Swapchain_Destroy(MGXR_Swapchain* swapchain);
MG_EXPORT mgint MGXR_Swapchain_AcquireImage(MGXR_Swapchain* swapchain);
MG_EXPORT void MGXR_Swapchain_WaitImage(MGXR_Swapchain* swapchain, mglong timeout);
MG_EXPORT void MGXR_Swapchain_ReleaseImage(MGXR_Swapchain* swapchain);
MG_EXPORT void MGXR_Swapchain_GetVulkanImage(MGXR_Swapchain* swapchain, mgint index, void** vkImage, mgint* width, mgint* height);
MG_EXPORT void MGXR_Swapchain_GetRecommendedSize(MGXR_Session* session, mgint* width, mgint* height);
MG_EXPORT mgint MGXR_Swapchain_GetImageCount(MGXR_Swapchain* swapchain);
MG_EXPORT void MGXR_Swapchain_ConfigureDeviceImages(MGXR_Swapchain* swapchain, void* graphicsDevice);
MG_EXPORT void MGXR_Swapchain_SetActiveImage(MGXR_Swapchain* swapchain, void* graphicsDevice, mgint imageIndex);
MG_EXPORT void MGG_GraphicsDevice_GetDeviceHandles(void* graphicsDevice, void* outHandles);
MG_EXPORT mgint MGXR_Session_GetViewCount(MGXR_Session* session);
MG_EXPORT void MGXR_Session_GetViewProjection(MGXR_Session* session, mgint viewIndex, mglong displayTime, MGXR_ViewProjection* view);
MG_EXPORT MGXR_Space* MGXR_Space_Create(MGXR_Session* session, MGXRReferenceSpaceType type);
MG_EXPORT void MGXR_Space_Destroy(MGXR_Space* space);
MG_EXPORT void MGXR_Space_GetPose(MGXR_Space* space, MGXR_Space* baseSpace, mglong time, MGXR_Pose* pose);
MG_EXPORT MGXR_ActionSet* MGXR_ActionSet_Create(MGXR_System* system, const char* name, const char* localizedName);
MG_EXPORT void MGXR_ActionSet_Destroy(MGXR_ActionSet* actionSet);
MG_EXPORT void MGXR_ActionSet_AttachToSession(MGXR_Session* session, MGXR_ActionSet** sets, mgint count);
MG_EXPORT MGXR_Action* MGXR_Action_Create(MGXR_ActionSet* actionSet, const char* name, const char* localizedName, MGXRActionType type);
MG_EXPORT void MGXR_Action_Destroy(MGXR_Action* action);
MG_EXPORT void MGXR_Action_SuggestBindings(MGXR_System* system, const char* interactionProfile, MGXR_Action** actions, mgbyte** paths, mgint count);
MG_EXPORT void MGXR_ActionSet_Sync(MGXR_Session* session, MGXR_ActionSet** sets, mgint count);
MG_EXPORT void MGXR_Action_GetStateBool(MGXR_Action* action, mgbyte* value, mgbyte* changed, mgint hand);
MG_EXPORT void MGXR_Action_GetStateFloat(MGXR_Action* action, mgfloat* value, mgbyte* changed, mgint hand);
MG_EXPORT void MGXR_Action_GetStateVector2(MGXR_Action* action, mgfloat* x, mgfloat* y, mgbyte* changed, mgint hand);
MG_EXPORT void MGXR_Action_GetStatePose(MGXR_Action* action, MGXR_Space* space, mglong time, MGXR_Pose* pose, mgint hand, mgint* outFlags);
MG_EXPORT void MGXR_Action_ApplyHaptic(MGXR_Action* action, mgfloat amplitude, mgfloat frequency, mglong duration);
MG_EXPORT void MGXR_Platform_AttachActionsToSession(void* platform, MGXR_Session* session);
