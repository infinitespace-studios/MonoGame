// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#include "api_MGG.h"
#include "mg_common.h"

#include <set>
#include <algorithm>
#include <vector>
#include <string>
#include <unordered_map>
#include <memory>

#if defined(MG_SDL2)
#include <SDL.h>
#include <SDL_metal.h>
#include <SDL_video.h>
#endif

#ifdef __APPLE__
#include <CoreGraphics/CoreGraphics.h>
#import <Metal/Metal.h>
#import <QuartzCore/CAMetalLayer.h>
#import <Foundation/Foundation.h>
#endif

// Stub builtin effects - TODO: Create proper Metal shader bytecode
static const uint8_t AlphaTestEffect_metal_mgfxo[] = { 0 };
static const uint8_t BasicEffect_metal_mgfxo[] = { 0 };
static const uint8_t DualTextureEffect_metal_mgfxo[] = { 0 };
static const uint8_t EnvironmentMapEffect_metal_mgfxo[] = { 0 };
static const uint8_t SkinnedEffect_metal_mgfxo[] = { 0 };
static const uint8_t SpriteEffect_metal_mgfxo[] = { 0 };

void MGG_EffectResource_GetBytecode(const char* name, mgbyte*& bytecode, mgint& size)
{
    if (strcmp(name, "AlphaTestEffect") == 0) {
        bytecode = (mgbyte*)AlphaTestEffect_metal_mgfxo;
        size = sizeof(AlphaTestEffect_metal_mgfxo);
    }
    else if (strcmp(name, "BasicEffect") == 0) {
        bytecode = (mgbyte*)BasicEffect_metal_mgfxo;
        size = sizeof(BasicEffect_metal_mgfxo);
    }
    else if (strcmp(name, "DualTextureEffect") == 0) {
        bytecode = (mgbyte*)DualTextureEffect_metal_mgfxo;
        size = sizeof(DualTextureEffect_metal_mgfxo);
    }
    else if (strcmp(name, "EnvironmentMapEffect") == 0) {
        bytecode = (mgbyte*)EnvironmentMapEffect_metal_mgfxo;
        size = sizeof(EnvironmentMapEffect_metal_mgfxo);
    }
    else if (strcmp(name, "SkinnedEffect") == 0) {
        bytecode = (mgbyte*)SkinnedEffect_metal_mgfxo;
        size = sizeof(SkinnedEffect_metal_mgfxo);
    }
    else if (strcmp(name, "SpriteEffect") == 0) {
        bytecode = (mgbyte*)SpriteEffect_metal_mgfxo;
        size = sizeof(SpriteEffect_metal_mgfxo);
    }
    else {
        bytecode = nullptr;
        size = 0;
    }
}

// Enhanced structures using proper Metal objects
struct MGG_GraphicsAdapter
{
    id<MTLDevice> __strong device = nil;
    std::string name;
    MGG_DisplayMode current = { MGSurfaceFormat::Color, 0, 0 };
    std::vector<MGG_DisplayMode> modes;
};

struct MGG_GraphicsSystem
{
    std::vector<MGG_GraphicsAdapter*> adapters;
    bool sdlInitialized = false;
};

struct MGG_GraphicsDevice
{
    id<MTLDevice> __strong device = nil;
    id<MTLCommandQueue> __strong commandQueue = nil;
    CAMetalLayer* __strong metalLayer = nil;
    
#if defined(MG_SDL2)
    SDL_Window* window = nullptr;
    SDL_MetalView metalView = nullptr;
#endif
    
    uint32_t swapchainWidth = 0;
    uint32_t swapchainHeight = 0;
    
    // Frame tracking
    uint32_t frame = 0;
    
    // Current render state
    id<MTLCommandBuffer> __strong currentCommandBuffer = nil;
    id<MTLRenderCommandEncoder> __strong currentRenderEncoder = nil;
    MTLRenderPassDescriptor* __strong currentRenderPass = nil;
    id<CAMetalDrawable> __strong currentDrawable = nil;
    
    // Cached viewport and scissor
    struct {
        mgint x, y, width, height;
        mgfloat minDepth, maxDepth;
        bool set = false;
    } viewport = {0, 0, 0, 0, 0.0f, 1.0f, false};
    
    struct {
        mgint x, y, width, height;
        bool enabled = false;
    } scissor = {0, 0, 0, 0, false};
    
    // Current render targets
    std::vector<MGG_Texture*> renderTargets;
    
    // Current pipeline state
    id<MTLRenderPipelineState> __strong currentPipelineState = nil;
    id<MTLDepthStencilState> __strong currentDepthStencilState = nil;
    
    // Current buffers and textures
    std::unordered_map<mgint, id<MTLBuffer>> vertexBuffers;
    id<MTLBuffer> __strong indexBuffer = nil;
    MGIndexElementSize indexElementSize = MGIndexElementSize::SixteenBits;
    
    std::unordered_map<mgint, id<MTLTexture>> boundTextures[2]; // Vertex and Fragment
    std::unordered_map<mgint, id<MTLSamplerState>> boundSamplers[2]; // Vertex and Fragment
    std::unordered_map<mgint, id<MTLBuffer>> boundConstantBuffers[2]; // Vertex and Fragment
};

struct MGG_Buffer
{
    id<MTLBuffer> __strong buffer = nil;
    int dataSize = 0;
    uint32_t frame = 0;
    MGBufferType type;
};

struct MGG_Texture
{
    id<MTLTexture> __strong texture = nil;
    uint32_t frame = 0;
    MGTextureType type;
    MGSurfaceFormat format;
    mgint width, height, depth;
    mgint mipmaps, slices;
    bool isRenderTarget = false;
};

struct MGG_Shader
{
    id<MTLFunction> __strong function = nil;
    MGShaderStage stage;
    uint32_t id = 0;
};

struct MGG_BlendState
{
    uint32_t hash = 0;
    int refs = 0;
    uint32_t frame = 0;
    MGG_BlendState_Info info;
};

struct MGG_DepthStencilState
{
    id<MTLDepthStencilState> __strong state = nil;
    uint32_t hash = 0;
    int refs = 0;
    uint32_t frame = 0;
    MGG_DepthStencilState_Info info;
};

struct MGG_RasterizerState
{
    uint32_t hash = 0;
    int refs = 0;
    uint32_t frame = 0;
    MGG_RasterizerState_Info info;
};

struct MGG_SamplerState
{
    id<MTLSamplerState> __strong state = nil;
    uint64_t id = 0;
    MGG_SamplerState_Info info;
};

struct MGG_InputLayout
{
    int streamCount = 0;
    std::vector<MGG_InputElement> elements;
    MTLVertexDescriptor* __strong vertexDescriptor = nil;
};

struct MGG_OcclusionQuery
{
    bool isComplete = false;
};

// Implementation
MGG_GraphicsSystem* MGG_GraphicsSystem_Create()
{
    auto system = new MGG_GraphicsSystem();
    
#if defined(MG_SDL2)
    // Initialize SDL2 video subsystem if not already initialized
    if (SDL_WasInit(SDL_INIT_VIDEO) == 0) {
        if (SDL_InitSubSystem(SDL_INIT_VIDEO) != 0) {
            printf("Metal: Failed to initialize SDL video subsystem: %s\n", SDL_GetError());
            delete system;
            return nullptr;
        }
        system->sdlInitialized = true;
    }
#endif
    
#ifdef __APPLE__
    // Create Metal device
    id<MTLDevice> device = MTLCreateSystemDefaultDevice();
    if (!device) {
        printf("Metal: Failed to create Metal device\n");
        delete system;
        return nullptr;
    }
    
    // Create adapter
    auto adapter = new MGG_GraphicsAdapter();
    adapter->device = device;
    adapter->name = std::string([device.name UTF8String]);
    
#if defined(MG_SDL2)
    // Enumerate display modes using SDL2
    int displayIndex = 0; // Primary display
    int numModes = SDL_GetNumDisplayModes(displayIndex);
    
    if (numModes > 0) {
        // Add unique display modes
        std::set<std::pair<int, int>> uniqueModes;
        
        for (int i = 0; i < numModes; i++) {
            SDL_DisplayMode mode;
            if (SDL_GetDisplayMode(displayIndex, i, &mode) == 0) {
                // Only add unique width/height combinations
                auto modePair = std::make_pair(mode.w, mode.h);
                if (uniqueModes.find(modePair) == uniqueModes.end()) {
                    uniqueModes.insert(modePair);
                    
                    MGSurfaceFormat format = MGSurfaceFormat::Color;
                    // Convert SDL pixel format to MonoGame format if needed
                    switch (mode.format) {
                        case SDL_PIXELFORMAT_RGB565:
                            format = MGSurfaceFormat::Bgr565;
                            break;
                        case SDL_PIXELFORMAT_ARGB1555:
                            format = MGSurfaceFormat::Bgra5551;
                            break;
                        default:
                            format = MGSurfaceFormat::Color;
                            break;
                    }
                    
                    adapter->modes.push_back({ format, mode.w, mode.h });
                }
            }
        }
        
        // Sort modes by resolution (width first, then height)
        std::sort(adapter->modes.begin(), adapter->modes.end(), 
            [](const MGG_DisplayMode& a, const MGG_DisplayMode& b) {
                if (a.width != b.width) return a.width < b.width;
                return a.height < b.height;
            });
    }
    
    // Get current display mode
    SDL_DisplayMode currentMode;
    if (SDL_GetCurrentDisplayMode(displayIndex, &currentMode) == 0) {
        adapter->current = { MGSurfaceFormat::Color, currentMode.w, currentMode.h };
    } else {
        // Fallback to desktop display mode
        SDL_DisplayMode desktopMode;
        if (SDL_GetDesktopDisplayMode(displayIndex, &desktopMode) == 0) {
            adapter->current = { MGSurfaceFormat::Color, desktopMode.w, desktopMode.h };
        } else {
            // Final fallback
            adapter->current = { MGSurfaceFormat::Color, 1920, 1080 };
        }
    }
    
    // Ensure we have at least some common display modes
    if (adapter->modes.empty()) {
        adapter->modes.push_back({ MGSurfaceFormat::Color, 1280, 720 });
        adapter->modes.push_back({ MGSurfaceFormat::Color, 1920, 1080 });
        adapter->modes.push_back({ MGSurfaceFormat::Color, 2560, 1440 });
    }
#else
    // Fallback display modes if SDL2 is not available
    adapter->current = { MGSurfaceFormat::Color, 1920, 1080 };
    adapter->modes.push_back({ MGSurfaceFormat::Color, 1280, 720 });
    adapter->modes.push_back({ MGSurfaceFormat::Color, 1920, 1080 });
    adapter->modes.push_back({ MGSurfaceFormat::Color, 2560, 1440 });
#endif
    
    system->adapters.push_back(adapter);
#endif
    
    return system;
}

void MGG_GraphicsSystem_Destroy(MGG_GraphicsSystem* system)
{
    if (!system) return;
    
    // Clean up all adapters
    for (auto adapter : system->adapters)
        delete adapter;
    system->adapters.clear();
    
#if defined(MG_SDL2)
    // Clean up SDL2 if we initialized it
    if (system->sdlInitialized) {
        SDL_QuitSubSystem(SDL_INIT_VIDEO);
    }
#endif
    
    delete system;
}

MGG_GraphicsAdapter* MGG_GraphicsAdapter_Get(MGG_GraphicsSystem* system, mgint index)
{
    if (!system || index < 0 || index >= system->adapters.size())
        return nullptr;
    
    return system->adapters[index];
}

void MGG_GraphicsAdapter_GetInfo(MGG_GraphicsAdapter* adapter, MGG_GraphicsAdaptor_Info& info)
{
    if (!adapter) return;
    
    info.DeviceName = (void*)adapter->name.data();
    info.Description = (void*)adapter->name.data();
    info.DeviceId = 0; // Metal doesn't expose device IDs like D3D
    info.VendorId = 0; // Could potentially query Metal device vendor
    info.SubSystemId = 0;
    info.Revision = 0;
    info.MonitorHandle = 0;
    info.DisplayModeCount = adapter->modes.size();
    info.DisplayModes = adapter->modes.data();
    info.CurrentDisplayMode = adapter->current;
}

MGG_GraphicsDevice* MGG_GraphicsDevice_Create(MGG_GraphicsSystem* system, MGG_GraphicsAdapter* adapter)
{
    if (!system || !adapter) return nullptr;
    
    auto device = new MGG_GraphicsDevice();
    device->device = adapter->device;
    
#ifdef __APPLE__
    // Create command queue
    device->commandQueue = [device->device newCommandQueue];
    if (!device->commandQueue) {
        printf("Metal: Failed to create command queue\n");
        delete device;
        return nullptr;
    }
#endif
    
    return device;
}

void MGG_GraphicsDevice_Destroy(MGG_GraphicsDevice* device)
{
    if (!device) return;
    
#ifdef __APPLE__
    // End any active render encoding
    if (device->currentRenderEncoder) {
        [device->currentRenderEncoder endEncoding];
        device->currentRenderEncoder = nil;
    }
    
    // Commit any pending command buffer
    if (device->currentCommandBuffer) {
        [device->currentCommandBuffer commit];
        device->currentCommandBuffer = nil;
    }
    
    // Clean up Metal resources
    device->commandQueue = nil;
    device->device = nil;
    device->metalLayer = nil;
    device->currentRenderPass = nil;
    device->currentDrawable = nil;
    device->currentPipelineState = nil;
    device->currentDepthStencilState = nil;
    device->indexBuffer = nil;
    device->vertexBuffers.clear();
    device->boundTextures[0].clear();
    device->boundTextures[1].clear();
    device->boundSamplers[0].clear();
    device->boundSamplers[1].clear();
    device->boundConstantBuffers[0].clear();
    device->boundConstantBuffers[1].clear();
#endif
    
    delete device;
}

void MGG_GraphicsDevice_GetCaps(MGG_GraphicsDevice* device, MGG_GraphicsDevice_Caps& caps)
{
    // Metal capabilities - these are reasonable defaults for Metal on modern Apple hardware
    caps.MaxTextureSlots = 31; // Metal allows up to 31 textures per stage
    caps.MaxVertexBufferSlots = 31; // Metal allows up to 31 vertex buffer bindings
    caps.MaxVertexTextureSlots = 31; // Metal supports vertex textures
    caps.ShaderProfile = 5; // Metal supports advanced shader features
}

void MGG_GraphicsDevice_ResizeSwapchain(MGG_GraphicsDevice* device, void* nativeWindowHandle, mgint width, mgint height, MGSurfaceFormat color, MGDepthFormat depth)
{
    if (!device) return;
    
#if defined(MG_SDL2) && defined(__APPLE__)
    device->window = (SDL_Window*)nativeWindowHandle;
    
    // Create Metal view if it doesn't exist
    if (!device->metalView && device->window) {
        device->metalView = SDL_Metal_CreateView(device->window);
        if (!device->metalView) {
            printf("Metal: Failed to create SDL Metal view: %s\n", SDL_GetError());
            return;
        }
    }
    
    // Get the CAMetalLayer from the SDL Metal view
    if (device->metalView) {
        CAMetalLayer* layer = (__bridge CAMetalLayer*)SDL_Metal_GetLayer(device->metalView);
        if (layer) {
            device->metalLayer = layer;
            
            // Configure the layer
            layer.device = device->device;
            
            // Set pixel format based on color format
            switch (color) {
                case MGSurfaceFormat::Color:
                case MGSurfaceFormat::ColorSRgb:
                    layer.pixelFormat = MTLPixelFormatBGRA8Unorm;
                    break;
                case MGSurfaceFormat::Bgra32:
                case MGSurfaceFormat::Bgra32SRgb:
                    layer.pixelFormat = MTLPixelFormatBGRA8Unorm;
                    break;
                case MGSurfaceFormat::HdrBlendable:
                    layer.pixelFormat = MTLPixelFormatRGBA16Float;
                    break;
                default:
                    layer.pixelFormat = MTLPixelFormatBGRA8Unorm;
                    break;
            }
            
            // Set drawable size
            layer.drawableSize = CGSizeMake(width, height);
            
            // Configure frame buffer only mode and other properties
            layer.framebufferOnly = YES;
            layer.wantsExtendedDynamicRangeContent = NO;
        }
    }
#endif
    
    device->swapchainWidth = width;
    device->swapchainHeight = height;
}

mgint MGG_GraphicsDevice_BeginFrame(MGG_GraphicsDevice* device)
{
    if (!device) return 0;
    
#ifdef __APPLE__
    // End any previous render encoding
    if (device->currentRenderEncoder) {
        [device->currentRenderEncoder endEncoding];
        device->currentRenderEncoder = nil;
    }
    
    // Commit any previous command buffer
    if (device->currentCommandBuffer) {
        [device->currentCommandBuffer commit];
        device->currentCommandBuffer = nil;
    }
    
    // Create new command buffer for this frame
    if (device->commandQueue) {
        device->currentCommandBuffer = [device->commandQueue commandBuffer];
        if (!device->currentCommandBuffer) {
            printf("Metal: Failed to create command buffer\n");
            return 0;
        }
    }
    
    // Get next drawable if we have a layer
    if (device->metalLayer) {
        device->currentDrawable = [device->metalLayer nextDrawable];
        if (!device->currentDrawable) {
            printf("Metal: Failed to get next drawable\n");
            return 0;
        }
    }
#endif
    
    return ++device->frame;
}

void MGG_GraphicsDevice_Clear(MGG_GraphicsDevice* device, MGClearOptions options, Vector4& color, mgfloat depth, mgint stencil)
{
    if (!device) return;
    
#ifdef __APPLE__
    // End any existing render encoder
    if (device->currentRenderEncoder) {
        [device->currentRenderEncoder endEncoding];
        device->currentRenderEncoder = nil;
    }
    
    // Create render pass descriptor if we don't have one
    if (!device->currentRenderPass) {
        device->currentRenderPass = [MTLRenderPassDescriptor renderPassDescriptor];
    }
    
    if (device->currentRenderPass && device->currentDrawable && device->currentCommandBuffer) {
        // Configure color attachment
        device->currentRenderPass.colorAttachments[0].texture = device->currentDrawable.texture;
        
        if ((static_cast<mgint>(options) & static_cast<mgint>(MGClearOptions::Target)) != 0) {
            device->currentRenderPass.colorAttachments[0].loadAction = MTLLoadActionClear;
            device->currentRenderPass.colorAttachments[0].clearColor = MTLClearColorMake(color.X, color.Y, color.Z, color.W);
        } else {
            device->currentRenderPass.colorAttachments[0].loadAction = MTLLoadActionLoad;
        }
        device->currentRenderPass.colorAttachments[0].storeAction = MTLStoreActionStore;
        
        // TODO: Configure depth/stencil attachments based on options
        if ((static_cast<mgint>(options) & static_cast<mgint>(MGClearOptions::DepthBuffer)) != 0) {
            // Configure depth clearing
        }
        
        if ((static_cast<mgint>(options) & static_cast<mgint>(MGClearOptions::Stencil)) != 0) {
            // Configure stencil clearing
        }
        
        // Create the render encoder immediately to ensure the clear happens
        device->currentRenderEncoder = [device->currentCommandBuffer renderCommandEncoderWithDescriptor:device->currentRenderPass];
        
        // Apply cached viewport if set
        if (device->viewport.set) {
            MTLViewport metalViewport = {
                static_cast<double>(device->viewport.x), static_cast<double>(device->viewport.y),
                static_cast<double>(device->viewport.width), static_cast<double>(device->viewport.height),
                static_cast<double>(device->viewport.minDepth), static_cast<double>(device->viewport.maxDepth)
            };
            [device->currentRenderEncoder setViewport:metalViewport];
        }
        
        // Apply cached scissor if enabled
        if (device->scissor.enabled) {
            MTLScissorRect scissorRect = {
                static_cast<NSUInteger>(device->scissor.x), static_cast<NSUInteger>(device->scissor.y),
                static_cast<NSUInteger>(device->scissor.width), static_cast<NSUInteger>(device->scissor.height)
            };
            [device->currentRenderEncoder setScissorRect:scissorRect];
        }
    }
#endif
}

void MGG_GraphicsDevice_Present(MGG_GraphicsDevice* device, mgint currentFrame, mgint syncInterval)
{
    if (!device) return;
    
#ifdef __APPLE__
    // End any active render encoding
    if (device->currentRenderEncoder) {
        [device->currentRenderEncoder endEncoding];
        device->currentRenderEncoder = nil;
    }
    
    // Present the drawable
    if (device->currentCommandBuffer && device->currentDrawable) {
        [device->currentCommandBuffer presentDrawable:device->currentDrawable];
        [device->currentCommandBuffer commit];
        
        // Wait for completion if sync interval is greater than 0
        if (syncInterval > 0) {
            [device->currentCommandBuffer waitUntilCompleted];
        }
        
        device->currentCommandBuffer = nil;
        device->currentDrawable = nil;
        device->currentRenderPass = nil;
    }
#endif
}
void MGG_GraphicsDevice_SetViewport(MGG_GraphicsDevice* device, mgint x, mgint y, mgint width, mgint height, mgfloat minDepth, mgfloat maxDepth)
{
    if (!device) return;
    
    device->viewport.x = x;
    device->viewport.y = y;
    device->viewport.width = width;
    device->viewport.height = height;
    device->viewport.minDepth = minDepth;
    device->viewport.maxDepth = maxDepth;
    device->viewport.set = true;
    
#ifdef __APPLE__
    if (device->currentRenderEncoder) {
        MTLViewport metalViewport = {
            static_cast<double>(x), static_cast<double>(y),
            static_cast<double>(width), static_cast<double>(height),
            static_cast<double>(minDepth), static_cast<double>(maxDepth)
        };
        [device->currentRenderEncoder setViewport:metalViewport];
    }
#endif
}

void MGG_GraphicsDevice_SetScissorRectangle(MGG_GraphicsDevice* device, mgint x, mgint y, mgint width, mgint height)
{
    if (!device) return;
    
    device->scissor.x = x;
    device->scissor.y = y;
    device->scissor.width = width;
    device->scissor.height = height;
    device->scissor.enabled = true;
    
#ifdef __APPLE__
    if (device->currentRenderEncoder) {
        MTLScissorRect scissorRect = {
            static_cast<NSUInteger>(x), static_cast<NSUInteger>(y),
            static_cast<NSUInteger>(width), static_cast<NSUInteger>(height)
        };
        [device->currentRenderEncoder setScissorRect:scissorRect];
    }
#endif
}

void MGG_GraphicsDevice_SetVertexBuffer(MGG_GraphicsDevice* device, mgint slot, MGG_Buffer* buffer, mgint vertexOffset)
{
    if (!device) return;
    
#ifdef __APPLE__
    if (buffer && buffer->buffer) {
        device->vertexBuffers[slot] = buffer->buffer;
        
        if (device->currentRenderEncoder) {
            [device->currentRenderEncoder setVertexBuffer:buffer->buffer 
                                                   offset:vertexOffset 
                                                  atIndex:slot];
        }
    } else {
        device->vertexBuffers.erase(slot);
        
        if (device->currentRenderEncoder) {
            [device->currentRenderEncoder setVertexBuffer:nil offset:0 atIndex:slot];
        }
    }
#endif
}

void MGG_GraphicsDevice_SetIndexBuffer(MGG_GraphicsDevice* device, MGIndexElementSize size, MGG_Buffer* buffer)
{
    if (!device) return;
    
#ifdef __APPLE__
    device->indexElementSize = size;
    
    if (buffer && buffer->buffer) {
        device->indexBuffer = buffer->buffer;
    } else {
        device->indexBuffer = nil;
    }
#endif
}

void MGG_GraphicsDevice_SetConstantBuffer(MGG_GraphicsDevice* device, MGShaderStage stage, mgint slot, MGG_Buffer* buffer)
{
    if (!device) return;
    
#ifdef __APPLE__
    mgint stageIndex = static_cast<mgint>(stage);
    if (stageIndex >= 0 && stageIndex < 2) {
        if (buffer && buffer->buffer) {
            device->boundConstantBuffers[stageIndex][slot] = buffer->buffer;
            
            if (device->currentRenderEncoder) {
                if (stage == MGShaderStage::Vertex) {
                    [device->currentRenderEncoder setVertexBuffer:buffer->buffer offset:0 atIndex:slot];
                } else if (stage == MGShaderStage::Pixel) {
                    [device->currentRenderEncoder setFragmentBuffer:buffer->buffer offset:0 atIndex:slot];
                }
            }
        } else {
            device->boundConstantBuffers[stageIndex].erase(slot);
            
            if (device->currentRenderEncoder) {
                if (stage == MGShaderStage::Vertex) {
                    [device->currentRenderEncoder setVertexBuffer:nil offset:0 atIndex:slot];
                } else if (stage == MGShaderStage::Pixel) {
                    [device->currentRenderEncoder setFragmentBuffer:nil offset:0 atIndex:slot];
                }
            }
        }
    }
#endif
}

void MGG_GraphicsDevice_SetTexture(MGG_GraphicsDevice* device, MGShaderStage stage, mgint slot, MGG_Texture* texture)
{
    if (!device) return;
    
#ifdef __APPLE__
    mgint stageIndex = static_cast<mgint>(stage);
    if (stageIndex >= 0 && stageIndex < 2) {
        if (texture && texture->texture) {
            device->boundTextures[stageIndex][slot] = texture->texture;
            
            if (device->currentRenderEncoder) {
                if (stage == MGShaderStage::Vertex) {
                    [device->currentRenderEncoder setVertexTexture:texture->texture atIndex:slot];
                } else if (stage == MGShaderStage::Pixel) {
                    [device->currentRenderEncoder setFragmentTexture:texture->texture atIndex:slot];
                }
            }
        } else {
            device->boundTextures[stageIndex].erase(slot);
            
            if (device->currentRenderEncoder) {
                if (stage == MGShaderStage::Vertex) {
                    [device->currentRenderEncoder setVertexTexture:nil atIndex:slot];
                } else if (stage == MGShaderStage::Pixel) {
                    [device->currentRenderEncoder setFragmentTexture:nil atIndex:slot];
                }
            }
        }
    }
#endif
}

void MGG_GraphicsDevice_SetSamplerState(MGG_GraphicsDevice* device, MGShaderStage stage, mgint slot, MGG_SamplerState* state)
{
    if (!device) return;
    
#ifdef __APPLE__
    mgint stageIndex = static_cast<mgint>(stage);
    if (stageIndex >= 0 && stageIndex < 2) {
        if (state && state->state) {
            device->boundSamplers[stageIndex][slot] = state->state;
            
            if (device->currentRenderEncoder) {
                if (stage == MGShaderStage::Vertex) {
                    [device->currentRenderEncoder setVertexSamplerState:state->state atIndex:slot];
                } else if (stage == MGShaderStage::Pixel) {
                    [device->currentRenderEncoder setFragmentSamplerState:state->state atIndex:slot];
                }
            }
        } else {
            device->boundSamplers[stageIndex].erase(slot);
            
            if (device->currentRenderEncoder) {
                if (stage == MGShaderStage::Vertex) {
                    [device->currentRenderEncoder setVertexSamplerState:nil atIndex:slot];
                } else if (stage == MGShaderStage::Pixel) {
                    [device->currentRenderEncoder setFragmentSamplerState:nil atIndex:slot];
                }
            }
        }
    }
#endif
}
// Helper function to ensure render encoder is active
static void EnsureRenderEncoder(MGG_GraphicsDevice* device)
{
#ifdef __APPLE__
    if (!device->currentRenderEncoder && device->currentCommandBuffer && device->currentRenderPass) {
        device->currentRenderEncoder = [device->currentCommandBuffer renderCommandEncoderWithDescriptor:device->currentRenderPass];
        
        // Apply cached viewport
        if (device->viewport.set) {
            MTLViewport metalViewport = {
                static_cast<double>(device->viewport.x), static_cast<double>(device->viewport.y),
                static_cast<double>(device->viewport.width), static_cast<double>(device->viewport.height),
                static_cast<double>(device->viewport.minDepth), static_cast<double>(device->viewport.maxDepth)
            };
            [device->currentRenderEncoder setViewport:metalViewport];
        }
        
        // Apply cached scissor
        if (device->scissor.enabled) {
            MTLScissorRect scissorRect = {
                static_cast<NSUInteger>(device->scissor.x), static_cast<NSUInteger>(device->scissor.y),
                static_cast<NSUInteger>(device->scissor.width), static_cast<NSUInteger>(device->scissor.height)
            };
            [device->currentRenderEncoder setScissorRect:scissorRect];
        }
        
        // Apply cached render pipeline state
        if (device->currentPipelineState) {
            [device->currentRenderEncoder setRenderPipelineState:device->currentPipelineState];
        }
        
        // Apply cached depth stencil state
        if (device->currentDepthStencilState) {
            [device->currentRenderEncoder setDepthStencilState:device->currentDepthStencilState];
        }
        
        // Reapply all bound resources
        for (const auto& pair : device->vertexBuffers) {
            [device->currentRenderEncoder setVertexBuffer:pair.second offset:0 atIndex:pair.first];
        }
        
        for (const auto& pair : device->boundConstantBuffers[0]) {
            [device->currentRenderEncoder setVertexBuffer:pair.second offset:0 atIndex:pair.first];
        }
        
        for (const auto& pair : device->boundConstantBuffers[1]) {
            [device->currentRenderEncoder setFragmentBuffer:pair.second offset:0 atIndex:pair.first];
        }
        
        for (const auto& pair : device->boundTextures[0]) {
            [device->currentRenderEncoder setVertexTexture:pair.second atIndex:pair.first];
        }
        
        for (const auto& pair : device->boundTextures[1]) {
            [device->currentRenderEncoder setFragmentTexture:pair.second atIndex:pair.first];
        }
        
        for (const auto& pair : device->boundSamplers[0]) {
            [device->currentRenderEncoder setVertexSamplerState:pair.second atIndex:pair.first];
        }
        
        for (const auto& pair : device->boundSamplers[1]) {
            [device->currentRenderEncoder setFragmentSamplerState:pair.second atIndex:pair.first];
        }
    }
#endif
}

// Helper function to convert MonoGame primitive type to Metal
#ifdef __APPLE__
static MTLPrimitiveType ConvertPrimitiveType(MGPrimitiveType primitiveType)
{
    switch (primitiveType) {
        case MGPrimitiveType::TriangleList:
            return MTLPrimitiveTypeTriangle;
        case MGPrimitiveType::TriangleStrip:
            return MTLPrimitiveTypeTriangleStrip;
        case MGPrimitiveType::LineList:
            return MTLPrimitiveTypeLine;
        case MGPrimitiveType::LineStrip:
            return MTLPrimitiveTypeLineStrip;
        case MGPrimitiveType::PointList:
            return MTLPrimitiveTypePoint;
        default:
            return MTLPrimitiveTypeTriangle;
    }
}

static MTLIndexType ConvertIndexType(MGIndexElementSize size)
{
    switch (size) {
        case MGIndexElementSize::SixteenBits:
            return MTLIndexTypeUInt16;
        case MGIndexElementSize::ThirtyTwoBits:
            return MTLIndexTypeUInt32;
        default:
            return MTLIndexTypeUInt16;
    }
}
#endif

void MGG_GraphicsDevice_SetBlendState(MGG_GraphicsDevice* device, MGG_BlendState* state, mgfloat factorR, mgfloat factorG, mgfloat factorB, mgfloat factorA) 
{
    // Blend state is configured in the render pipeline state in Metal
    // We'll need to rebuild the pipeline state when this changes
}

void MGG_GraphicsDevice_SetDepthStencilState(MGG_GraphicsDevice* device, MGG_DepthStencilState* state) 
{
    if (!device) return;
    
#ifdef __APPLE__
    if (state && state->state) {
        device->currentDepthStencilState = state->state;
        
        if (device->currentRenderEncoder) {
            [device->currentRenderEncoder setDepthStencilState:state->state];
        }
    } else {
        device->currentDepthStencilState = nil;
        
        if (device->currentRenderEncoder) {
            [device->currentRenderEncoder setDepthStencilState:nil];
        }
    }
#endif
}

void MGG_GraphicsDevice_SetRasterizerState(MGG_GraphicsDevice* device, MGG_RasterizerState* state) 
{
    // Rasterizer state is mostly configured in the render pipeline state in Metal
    // Some aspects like scissor test are handled separately
}

void MGG_GraphicsDevice_GetTitleSafeArea(mgint& x, mgint& y, mgint& width, mgint& height) 
{ 
#if defined(MG_SDL2)
    // Get the primary display's current mode
    SDL_DisplayMode mode;
    if (SDL_GetCurrentDisplayMode(0, &mode) == 0) {
        x = 0; 
        y = 0; 
        width = mode.w; 
        height = mode.h;
    } else {
        // Fallback
        x = 0; 
        y = 0; 
        width = 1920; 
        height = 1080;
    }
#else
    x = 0; 
    y = 0; 
    width = 1920; 
    height = 1080;
#endif
}

void MGG_GraphicsDevice_SetRenderTargets(MGG_GraphicsDevice* device, MGG_Texture** targets, mgint* arraySlices, mgint count) 
{
    if (!device) return;
    
    // Store render targets for later use
    device->renderTargets.clear();
    if (targets && count > 0) {
        for (mgint i = 0; i < count; i++) {
            if (targets[i]) {
                device->renderTargets.push_back(targets[i]);
            }
        }
    }
    
    // TODO: Update render pass descriptor with new render targets
}

void MGG_GraphicsDevice_SetShader(MGG_GraphicsDevice* device, MGShaderStage stage, MGG_Shader* shader) 
{
    // Shaders are bound as part of the render pipeline state in Metal
    // We'll need to track vertex and fragment shaders and rebuild pipeline state when they change
}

void MGG_GraphicsDevice_SetInputLayout(MGG_GraphicsDevice* device, MGG_InputLayout* layout) 
{
    // Input layout is configured in the render pipeline state in Metal
    // We'll need to rebuild the pipeline state when this changes
}

void MGG_GraphicsDevice_Draw(MGG_GraphicsDevice* device, MGPrimitiveType primitiveType, mgint vertexStart, mgint vertexCount)
{
    if (!device) return;
    
    EnsureRenderEncoder(device);
    
#ifdef __APPLE__
    if (device->currentRenderEncoder) {
        MTLPrimitiveType metalPrimitiveType = ConvertPrimitiveType(primitiveType);
        [device->currentRenderEncoder drawPrimitives:metalPrimitiveType
                                         vertexStart:vertexStart
                                         vertexCount:vertexCount];
    }
#endif
}

void MGG_GraphicsDevice_DrawIndexed(MGG_GraphicsDevice* device, MGPrimitiveType primitiveType, mgint primitiveCount, mgint indexStart, mgint vertexStart)
{
    if (!device) return;
    
    EnsureRenderEncoder(device);
    
#ifdef __APPLE__
    if (device->currentRenderEncoder && device->indexBuffer) {
        MTLPrimitiveType metalPrimitiveType = ConvertPrimitiveType(primitiveType);
        MTLIndexType indexType = ConvertIndexType(device->indexElementSize);
        
        mgint indexCount = primitiveCount;
        if (primitiveType == MGPrimitiveType::TriangleList) {
            indexCount = primitiveCount * 3;
        } else if (primitiveType == MGPrimitiveType::LineList) {
            indexCount = primitiveCount * 2;
        }
        
        mgint indexSize = (device->indexElementSize == MGIndexElementSize::SixteenBits) ? 2 : 4;
        mgint indexBufferOffset = indexStart * indexSize;
        
        [device->currentRenderEncoder drawIndexedPrimitives:metalPrimitiveType
                                                 indexCount:indexCount
                                                  indexType:indexType
                                                indexBuffer:device->indexBuffer
                                          indexBufferOffset:indexBufferOffset];
    }
#endif
}

void MGG_GraphicsDevice_DrawIndexedInstanced(MGG_GraphicsDevice* device, MGPrimitiveType primitiveType, mgint primitiveCount, mgint indexStart, mgint vertexStart, mgint instanceCount)
{
    if (!device) return;
    
    EnsureRenderEncoder(device);
    
#ifdef __APPLE__
    if (device->currentRenderEncoder && device->indexBuffer) {
        MTLPrimitiveType metalPrimitiveType = ConvertPrimitiveType(primitiveType);
        MTLIndexType indexType = ConvertIndexType(device->indexElementSize);
        
        mgint indexCount = primitiveCount;
        if (primitiveType == MGPrimitiveType::TriangleList) {
            indexCount = primitiveCount * 3;
        } else if (primitiveType == MGPrimitiveType::LineList) {
            indexCount = primitiveCount * 2;
        }
        
        mgint indexSize = (device->indexElementSize == MGIndexElementSize::SixteenBits) ? 2 : 4;
        mgint indexBufferOffset = indexStart * indexSize;
        
        [device->currentRenderEncoder drawIndexedPrimitives:metalPrimitiveType
                                                 indexCount:indexCount
                                                  indexType:indexType
                                                indexBuffer:device->indexBuffer
                                          indexBufferOffset:indexBufferOffset
                                              instanceCount:instanceCount];
    }
#endif
}

void MGG_GraphicsDevice_ResolveRenderTargets(MGG_GraphicsDevice* device) 
{
    // Metal handles MSAA resolve automatically in most cases
}

void MGG_GraphicsDevice_GetBackBufferData(MGG_GraphicsDevice* device, mgint x, mgint y, mgint width, mgint height, void* data, mgint count, mgint dataBytes) 
{
    // TODO: Implement back buffer data reading using Metal blit encoder
}

// Resource creation implementations
MGG_Buffer* MGG_Buffer_Create(MGG_GraphicsDevice* device, MGBufferType type, mgint sizeInBytes) 
{ 
    if (!device || sizeInBytes <= 0) return nullptr;
    
    auto buffer = new MGG_Buffer();
    buffer->type = type;
    buffer->dataSize = sizeInBytes;
    
#ifdef __APPLE__
    if (device->device) {
        MTLResourceOptions options = MTLResourceStorageModeShared;
        
        // Choose appropriate resource options based on buffer type
        switch (type) {
            case MGBufferType::Vertex:
            case MGBufferType::Index:
                options = MTLResourceStorageModeShared;
                break;
            case MGBufferType::Constant:
                options = MTLResourceStorageModeShared;
                break;
            default:
                options = MTLResourceStorageModeShared;
                break;
        }
        
        buffer->buffer = [device->device newBufferWithLength:sizeInBytes options:options];
        if (!buffer->buffer) {
            printf("Metal: Failed to create buffer of size %d\n", sizeInBytes);
            delete buffer;
            return nullptr;
        }
    }
#endif
    
    return buffer;
}

void MGG_Buffer_Destroy(MGG_GraphicsDevice* device, MGG_Buffer* buffer) 
{ 
    if (!buffer) return;
    
#ifdef __APPLE__
    buffer->buffer = nil;
#endif
    
    delete buffer;
}

void MGG_Buffer_SetData(MGG_GraphicsDevice* device, MGG_Buffer*& buffer, mgint offset, mgbyte* data, mgint elementCount, mgint vertexStride, mgint elementSizeInBytes, mgbool discard) 
{
    if (!device || !buffer || !data) return;
    
#ifdef __APPLE__
    if (buffer->buffer) {
        mgint totalBytes = elementCount * elementSizeInBytes;
        if (offset + totalBytes > buffer->dataSize) {
            printf("Metal: Buffer data exceeds buffer size\n");
            return;
        }
        
        void* bufferContents = [buffer->buffer contents];
        if (bufferContents) {
            memcpy(static_cast<uint8_t*>(bufferContents) + offset, data, totalBytes);
            
            // Notify Metal about the modified range
            [buffer->buffer didModifyRange:NSMakeRange(offset, totalBytes)];
        }
    }
#endif
}

void MGG_Buffer_GetData(MGG_GraphicsDevice* device, MGG_Buffer* buffer, mgint offset, mgbyte* data, mgint dataCount, mgint dataBytes, mgint dataStride) 
{
    if (!device || !buffer || !data) return;
    
#ifdef __APPLE__
    if (buffer->buffer) {
        if (offset + dataBytes > buffer->dataSize) {
            printf("Metal: Buffer read exceeds buffer size\n");
            return;
        }
        
        void* bufferContents = [buffer->buffer contents];
        if (bufferContents) {
            memcpy(data, static_cast<uint8_t*>(bufferContents) + offset, dataBytes);
        }
    }
#endif
}

// Helper function to convert MonoGame surface format to Metal pixel format
#ifdef __APPLE__
static MTLPixelFormat ConvertSurfaceFormat(MGSurfaceFormat format)
{
    switch (format) {
        case MGSurfaceFormat::Color:
        case MGSurfaceFormat::ColorSRgb:
            return MTLPixelFormatRGBA8Unorm;
        case MGSurfaceFormat::Bgr565:
            return MTLPixelFormatB5G6R5Unorm;
        case MGSurfaceFormat::Bgra5551:
            return MTLPixelFormatBGR5A1Unorm;
        case MGSurfaceFormat::Bgra4444:
            return MTLPixelFormatABGR4Unorm;
        case MGSurfaceFormat::Dxt1:
            return MTLPixelFormatBC1_RGBA;
        case MGSurfaceFormat::Dxt3:
            return MTLPixelFormatBC2_RGBA;
        case MGSurfaceFormat::Dxt5:
            return MTLPixelFormatBC3_RGBA;
        case MGSurfaceFormat::Alpha8:
            return MTLPixelFormatA8Unorm;
        case MGSurfaceFormat::Single:
            return MTLPixelFormatR32Float;
        case MGSurfaceFormat::Vector2:
            return MTLPixelFormatRG32Float;
        case MGSurfaceFormat::Vector4:
            return MTLPixelFormatRGBA32Float;
        case MGSurfaceFormat::HalfSingle:
            return MTLPixelFormatR16Float;
        case MGSurfaceFormat::HalfVector2:
            return MTLPixelFormatRG16Float;
        case MGSurfaceFormat::HalfVector4:
            return MTLPixelFormatRGBA16Float;
        case MGSurfaceFormat::HdrBlendable:
            return MTLPixelFormatRGBA16Float;
        case MGSurfaceFormat::Bgr32:
            return MTLPixelFormatBGRA8Unorm;
        case MGSurfaceFormat::Bgra32:
        case MGSurfaceFormat::Bgra32SRgb:
            return MTLPixelFormatBGRA8Unorm;
        default:
            return MTLPixelFormatRGBA8Unorm;
    }
}

static MTLTextureType ConvertTextureType(MGTextureType type)
{
    switch (type) {
        case MGTextureType::_2D:
            return MTLTextureType2D;
        case MGTextureType::_3D:
            return MTLTextureType3D;
        case MGTextureType::Cube:
            return MTLTextureTypeCube;
        default:
            return MTLTextureType2D;
    }
}
#endif

MGG_Texture* MGG_Texture_Create(MGG_GraphicsDevice* device, MGTextureType type, MGSurfaceFormat format, mgint width, mgint height, mgint depth, mgint mipmaps, mgint slices) 
{
    if (!device || width <= 0 || height <= 0) return nullptr;
    
    auto texture = new MGG_Texture();
    texture->type = type;
    texture->format = format;
    texture->width = width;
    texture->height = height;
    texture->depth = depth;
    texture->mipmaps = mipmaps;
    texture->slices = slices;
    texture->isRenderTarget = false;
    
#ifdef __APPLE__
    if (device->device) {
        MTLTextureDescriptor* descriptor = [MTLTextureDescriptor new];
        descriptor.textureType = ConvertTextureType(type);
        descriptor.pixelFormat = ConvertSurfaceFormat(format);
        descriptor.width = width;
        descriptor.height = height;
        descriptor.depth = (type == MGTextureType::_3D) ? depth : 1;
        descriptor.mipmapLevelCount = mipmaps;
        descriptor.arrayLength = (type == MGTextureType::Cube) ? slices : 1;
        descriptor.usage = MTLTextureUsageShaderRead;
        descriptor.storageMode = MTLStorageModeShared;
        
        texture->texture = [device->device newTextureWithDescriptor:descriptor];
        if (!texture->texture) {
            printf("Metal: Failed to create texture %dx%d\n", width, height);
            delete texture;
            return nullptr;
        }
    }
#endif
    
    return texture;
}

MGG_Texture* MGG_RenderTarget_Create(MGG_GraphicsDevice* device, MGTextureType type, MGSurfaceFormat format, mgint width, mgint height, mgint depth, mgint mipmaps, mgint slices, MGDepthFormat depthFormat, mgint multiSampleCount, MGRenderTargetUsage usage) 
{
    if (!device || width <= 0 || height <= 0) return nullptr;
    
    auto texture = new MGG_Texture();
    texture->type = type;
    texture->format = format;
    texture->width = width;
    texture->height = height;
    texture->depth = depth;
    texture->mipmaps = mipmaps;
    texture->slices = slices;
    texture->isRenderTarget = true;
    
#ifdef __APPLE__
    if (device->device) {
        MTLTextureDescriptor* descriptor = [MTLTextureDescriptor new];
        descriptor.textureType = ConvertTextureType(type);
        descriptor.pixelFormat = ConvertSurfaceFormat(format);
        descriptor.width = width;
        descriptor.height = height;
        descriptor.depth = (type == MGTextureType::_3D) ? depth : 1;
        descriptor.mipmapLevelCount = mipmaps;
        descriptor.arrayLength = (type == MGTextureType::Cube) ? slices : 1;
        descriptor.usage = MTLTextureUsageShaderRead | MTLTextureUsageRenderTarget;
        descriptor.storageMode = MTLStorageModePrivate;
        
        if (multiSampleCount > 1) {
            descriptor.sampleCount = multiSampleCount;
            descriptor.textureType = MTLTextureType2DMultisample;
        }
        
        texture->texture = [device->device newTextureWithDescriptor:descriptor];
        if (!texture->texture) {
            printf("Metal: Failed to create render target %dx%d\n", width, height);
            delete texture;
            return nullptr;
        }
    }
#endif
    
    return texture;
}

void MGG_Texture_Destroy(MGG_GraphicsDevice* device, MGG_Texture* texture) 
{
    if (!texture) return;
    
#ifdef __APPLE__
    texture->texture = nil;
#endif
    
    delete texture;
}

void MGG_Texture_SetData(MGG_GraphicsDevice* device, MGG_Texture* texture, mgint level, mgint slice, mgint x, mgint y, mgint z, mgint width, mgint height, mgint depth, mgbyte* data, mgint dataBytes) 
{
    if (!device || !texture || !data) return;
    
#ifdef __APPLE__
    if (texture->texture) {
        MTLRegion region = MTLRegionMake3D(x, y, z, width, height, depth);
        
        // Calculate bytes per row based on pixel format
        mgint bytesPerPixel = 4; // Default to 4 bytes per pixel (RGBA8)
        switch (texture->format) {
            case MGSurfaceFormat::Alpha8:
                bytesPerPixel = 1;
                break;
            case MGSurfaceFormat::Bgr565:
            case MGSurfaceFormat::Bgra5551:
            case MGSurfaceFormat::Bgra4444:
                bytesPerPixel = 2;
                break;
            case MGSurfaceFormat::Color:
            case MGSurfaceFormat::Bgra32:
                bytesPerPixel = 4;
                break;
            case MGSurfaceFormat::Vector4:
                bytesPerPixel = 16;
                break;
            default:
                bytesPerPixel = 4;
                break;
        }
        
        mgint bytesPerRow = width * bytesPerPixel;
        mgint bytesPerImage = bytesPerRow * height;
        
        [texture->texture replaceRegion:region
                            mipmapLevel:level
                                  slice:slice
                              withBytes:data
                            bytesPerRow:bytesPerRow
                          bytesPerImage:bytesPerImage];
    }
#endif
}

void MGG_Texture_GetData(MGG_GraphicsDevice* device, MGG_Texture* texture, mgint level, mgint slice, mgint x, mgint y, mgint z, mgint width, mgint height, mgint depth, mgbyte* data, mgint dataBytes) 
{
    if (!device || !texture || !data) return;
    
#ifdef __APPLE__
    if (texture->texture) {
        // Getting texture data requires a blit encoder and temporary buffer
        // This is a simplified implementation
        MTLRegion region = MTLRegionMake3D(x, y, z, width, height, depth);
        
        mgint bytesPerPixel = 4; // Default to 4 bytes per pixel
        mgint bytesPerRow = width * bytesPerPixel;
        mgint bytesPerImage = bytesPerRow * height;
        
        [texture->texture getBytes:data
                       bytesPerRow:bytesPerRow
                     bytesPerImage:bytesPerImage
                        fromRegion:region
                       mipmapLevel:level
                             slice:slice];
    }
#endif
}

// Helper functions for state conversion
#ifdef __APPLE__
static MTLBlendFactor ConvertBlendFactor(MGBlend blend)
{
    switch (blend) {
        case MGBlend::Zero: return MTLBlendFactorZero;
        case MGBlend::One: return MTLBlendFactorOne;
        case MGBlend::SourceColor: return MTLBlendFactorSourceColor;
        case MGBlend::InverseSourceColor: return MTLBlendFactorOneMinusSourceColor;
        case MGBlend::SourceAlpha: return MTLBlendFactorSourceAlpha;
        case MGBlend::InverseSourceAlpha: return MTLBlendFactorOneMinusSourceAlpha;
        case MGBlend::DestinationColor: return MTLBlendFactorDestinationColor;
        case MGBlend::InverseDestinationColor: return MTLBlendFactorOneMinusDestinationColor;
        case MGBlend::DestinationAlpha: return MTLBlendFactorDestinationAlpha;
        case MGBlend::InverseDestinationAlpha: return MTLBlendFactorOneMinusDestinationAlpha;
        case MGBlend::BlendFactor: return MTLBlendFactorBlendColor;
        case MGBlend::InverseBlendFactor: return MTLBlendFactorOneMinusBlendColor;
        case MGBlend::SourceAlphaSaturation: return MTLBlendFactorSourceAlphaSaturated;
        default: return MTLBlendFactorOne;
    }
}

static MTLBlendOperation ConvertBlendOperation(MGBlendFunction func)
{
    switch (func) {
        case MGBlendFunction::Add: return MTLBlendOperationAdd;
        case MGBlendFunction::Subtract: return MTLBlendOperationSubtract;
        case MGBlendFunction::ReverseSubtract: return MTLBlendOperationReverseSubtract;
        case MGBlendFunction::Min: return MTLBlendOperationMin;
        case MGBlendFunction::Max: return MTLBlendOperationMax;
        default: return MTLBlendOperationAdd;
    }
}

static MTLCompareFunction ConvertCompareFunction(MGCompareFunction func)
{
    switch (func) {
        case MGCompareFunction::Never: return MTLCompareFunctionNever;
        case MGCompareFunction::Less: return MTLCompareFunctionLess;
        case MGCompareFunction::Equal: return MTLCompareFunctionEqual;
        case MGCompareFunction::LessEqual: return MTLCompareFunctionLessEqual;
        case MGCompareFunction::Greater: return MTLCompareFunctionGreater;
        case MGCompareFunction::NotEqual: return MTLCompareFunctionNotEqual;
        case MGCompareFunction::GreaterEqual: return MTLCompareFunctionGreaterEqual;
        case MGCompareFunction::Always: return MTLCompareFunctionAlways;
        default: return MTLCompareFunctionAlways;
    }
}

static MTLStencilOperation ConvertStencilOperation(MGStencilOperation op)
{
    switch (op) {
        case MGStencilOperation::Keep: return MTLStencilOperationKeep;
        case MGStencilOperation::Zero: return MTLStencilOperationZero;
        case MGStencilOperation::Replace: return MTLStencilOperationReplace;
        case MGStencilOperation::Increment: return MTLStencilOperationIncrementWrap;
        case MGStencilOperation::Decrement: return MTLStencilOperationDecrementWrap;
        case MGStencilOperation::IncrementSaturation: return MTLStencilOperationIncrementClamp;
        case MGStencilOperation::DecrementSaturation: return MTLStencilOperationDecrementClamp;
        case MGStencilOperation::Invert: return MTLStencilOperationInvert;
        default: return MTLStencilOperationKeep;
    }
}

static MTLSamplerAddressMode ConvertAddressMode(MGTextureAddressMode mode)
{
    switch (mode) {
        case MGTextureAddressMode::Wrap: return MTLSamplerAddressModeRepeat;
        case MGTextureAddressMode::Clamp: return MTLSamplerAddressModeClampToEdge;
        case MGTextureAddressMode::Mirror: return MTLSamplerAddressModeMirrorRepeat;
        case MGTextureAddressMode::Border: return MTLSamplerAddressModeClampToBorderColor;
        default: return MTLSamplerAddressModeRepeat;
    }
}

static MTLSamplerMinMagFilter ConvertTextureFilter(MGTextureFilter filter)
{
    switch (filter) {
        case MGTextureFilter::Point:
        case MGTextureFilter::PointMipLinear:
        case MGTextureFilter::MinPointMagLinearMipLinear:
        case MGTextureFilter::MinPointMagLinearMipPoint:
            return MTLSamplerMinMagFilterNearest;
        case MGTextureFilter::Linear:
        case MGTextureFilter::Anisotropic:
        case MGTextureFilter::LinearMipPoint:
        case MGTextureFilter::MinLinearMagPointMipLinear:
        case MGTextureFilter::MinLinearMagPointMipPoint:
            return MTLSamplerMinMagFilterLinear;
        default:
            return MTLSamplerMinMagFilterLinear;
    }
}
#endif

MGG_BlendState* MGG_BlendState_Create(MGG_GraphicsDevice* device, MGG_BlendState_Info* infos) 
{
    if (!device || !infos) return nullptr;
    
    auto blendState = new MGG_BlendState();
    blendState->info = *infos;
    
    // In Metal, blend state is part of the render pipeline state
    // We store the info for later use when creating pipeline states
    
    return blendState;
}

void MGG_BlendState_Destroy(MGG_GraphicsDevice* device, MGG_BlendState* state) 
{
    if (state) delete state;
}

MGG_DepthStencilState* MGG_DepthStencilState_Create(MGG_GraphicsDevice* device, MGG_DepthStencilState_Info* info) 
{
    if (!device || !info) return nullptr;
    
    auto depthStencilState = new MGG_DepthStencilState();
    depthStencilState->info = *info;
    
#ifdef __APPLE__
    if (device->device) {
        MTLDepthStencilDescriptor* descriptor = [MTLDepthStencilDescriptor new];
        descriptor.depthCompareFunction = ConvertCompareFunction(info->depthBufferFunction);
        descriptor.depthWriteEnabled = info->depthBufferWriteEnable;
        
        if (info->stencilEnable) {
            MTLStencilDescriptor* frontStencil = [MTLStencilDescriptor new];
            frontStencil.stencilCompareFunction = ConvertCompareFunction(info->stencilFunction);
            frontStencil.stencilFailureOperation = ConvertStencilOperation(info->stencilFail);
            frontStencil.depthFailureOperation = ConvertStencilOperation(info->stencilDepthBufferFail);
            frontStencil.depthStencilPassOperation = ConvertStencilOperation(info->stencilPass);
            frontStencil.readMask = info->stencilMask;
            frontStencil.writeMask = info->stencilWriteMask;
            
            descriptor.frontFaceStencil = frontStencil;
            descriptor.backFaceStencil = frontStencil; // Use same for both faces
        }
        
        depthStencilState->state = [device->device newDepthStencilStateWithDescriptor:descriptor];
    }
#endif
    
    return depthStencilState;
}

void MGG_DepthStencilState_Destroy(MGG_GraphicsDevice* device, MGG_DepthStencilState* state) 
{
    if (!state) return;
    
#ifdef __APPLE__
    state->state = nil;
#endif
    
    delete state;
}

MGG_RasterizerState* MGG_RasterizerState_Create(MGG_GraphicsDevice* device, MGG_RasterizerState_Info* info) 
{
    if (!device || !info) return nullptr;
    
    auto rasterizerState = new MGG_RasterizerState();
    rasterizerState->info = *info;
    
    // In Metal, rasterizer state is mostly part of the render pipeline state
    // We store the info for later use when creating pipeline states
    
    return rasterizerState;
}

void MGG_RasterizerState_Destroy(MGG_GraphicsDevice* device, MGG_RasterizerState* state) 
{
    if (state) delete state;
}

MGG_SamplerState* MGG_SamplerState_Create(MGG_GraphicsDevice* device, MGG_SamplerState_Info* info) 
{
    if (!device || !info) return nullptr;
    
    auto samplerState = new MGG_SamplerState();
    samplerState->info = *info;
    
#ifdef __APPLE__
    if (device->device) {
        MTLSamplerDescriptor* descriptor = [MTLSamplerDescriptor new];
        descriptor.sAddressMode = ConvertAddressMode(info->AddressU);
        descriptor.tAddressMode = ConvertAddressMode(info->AddressV);
        descriptor.rAddressMode = ConvertAddressMode(info->AddressW);
        descriptor.minFilter = ConvertTextureFilter(info->Filter);
        descriptor.magFilter = ConvertTextureFilter(info->Filter);
        descriptor.maxAnisotropy = info->MaximumAnisotropy;
        descriptor.lodMinClamp = info->MaxMipLevel;
        descriptor.lodMaxClamp = FLT_MAX;
        descriptor.mipFilter = MTLSamplerMipFilterLinear;
        
        if (info->Filter == MGTextureFilter::Anisotropic) {
            descriptor.maxAnisotropy = info->MaximumAnisotropy;
        }
        
        samplerState->state = [device->device newSamplerStateWithDescriptor:descriptor];
    }
#endif
    
    return samplerState;
}

void MGG_SamplerState_Destroy(MGG_GraphicsDevice* device, MGG_SamplerState* state) 
{
    if (!state) return;
    
#ifdef __APPLE__
    state->state = nil;
#endif
    
    delete state;
}

MGG_InputLayout* MGG_InputLayout_Create(MGG_GraphicsDevice* device, MGG_Shader* vertexShader, mgint* strides, mgint streamCount, MGG_InputElement* elements, mgint elementCount) 
{
    if (!device || !elements || elementCount <= 0) return nullptr;
    
    auto inputLayout = new MGG_InputLayout();
    inputLayout->streamCount = streamCount;
    
    // Copy elements
    for (mgint i = 0; i < elementCount; i++) {
        inputLayout->elements.push_back(elements[i]);
    }
    
#ifdef __APPLE__
    // Create MTLVertexDescriptor
    inputLayout->vertexDescriptor = [MTLVertexDescriptor new];
    
    for (mgint i = 0; i < elementCount; i++) {
        const auto& element = elements[i];
        
        MTLVertexFormat format = MTLVertexFormatFloat4; // Default
        switch (element.Format) {
            case MGVertexElementFormat::Single:
                format = MTLVertexFormatFloat;
                break;
            case MGVertexElementFormat::Vector2:
                format = MTLVertexFormatFloat2;
                break;
            case MGVertexElementFormat::Vector3:
                format = MTLVertexFormatFloat3;
                break;
            case MGVertexElementFormat::Vector4:
                format = MTLVertexFormatFloat4;
                break;
            case MGVertexElementFormat::Color:
                format = MTLVertexFormatUChar4Normalized;
                break;
            case MGVertexElementFormat::Byte4:
                format = MTLVertexFormatChar4;
                break;
            case MGVertexElementFormat::Short2:
                format = MTLVertexFormatShort2;
                break;
            case MGVertexElementFormat::Short4:
                format = MTLVertexFormatShort4;
                break;
            case MGVertexElementFormat::NormalizedShort2:
                format = MTLVertexFormatShort2Normalized;
                break;
            case MGVertexElementFormat::NormalizedShort4:
                format = MTLVertexFormatShort4Normalized;
                break;
            case MGVertexElementFormat::HalfVector2:
                format = MTLVertexFormatHalf2;
                break;
            case MGVertexElementFormat::HalfVector4:
                format = MTLVertexFormatHalf4;
                break;
            default:
                format = MTLVertexFormatFloat4;
                break;
        }
        
        inputLayout->vertexDescriptor.attributes[i].format = format;
        inputLayout->vertexDescriptor.attributes[i].bufferIndex = element.VertexBufferSlot;
        inputLayout->vertexDescriptor.attributes[i].offset = element.AlignedByteOffset;
    }
    
    // Set up vertex buffer layouts
    for (mgint i = 0; i < streamCount; i++) {
        if (i < 16 && strides) { // Metal supports up to 16 vertex buffers
            inputLayout->vertexDescriptor.layouts[i].stride = strides[i];
            inputLayout->vertexDescriptor.layouts[i].stepFunction = MTLVertexStepFunctionPerVertex;
        }
    }
#endif
    
    return inputLayout;
}

void MGG_InputLayout_Destroy(MGG_GraphicsDevice* device, MGG_InputLayout* layout) 
{
    if (!layout) return;
    
#ifdef __APPLE__
    layout->vertexDescriptor = nil;
#endif
    
    delete layout;
}

MGG_Shader* MGG_Shader_Create(MGG_GraphicsDevice* device, MGShaderStage stage, mgbyte* bytecode, mgint sizeInBytes) 
{
    if (!device || !bytecode || sizeInBytes <= 0) return nullptr;
    
    auto shader = new MGG_Shader();
    shader->stage = stage;
    
#ifdef __APPLE__
    if (device->device) {
        // Convert NSData to dispatch_data_t for Metal
        dispatch_data_t dispatchData = dispatch_data_create(bytecode, sizeInBytes, NULL, DISPATCH_DATA_DESTRUCTOR_DEFAULT);
        NSError* error = nil;
        
        id<MTLLibrary> library = [device->device newLibraryWithData:dispatchData error:&error];
        if (!library) {
            if (error) {
                printf("Metal: Failed to create shader library: %s\n", [[error localizedDescription] UTF8String]);
            }
            delete shader;
            return nullptr;
        }
        
        // Get the main function from the library
        NSString* functionName = (stage == MGShaderStage::Vertex) ? @"vertexMain" : @"fragmentMain";
        shader->function = [library newFunctionWithName:functionName];
        
        if (!shader->function) {
            printf("Metal: Failed to find shader function '%s'\n", [functionName UTF8String]);
            delete shader;
            return nullptr;
        }
    }
#endif
    
    return shader;
}

void MGG_Shader_Destroy(MGG_GraphicsDevice* device, MGG_Shader* shader) 
{
    if (!shader) return;
    
#ifdef __APPLE__
    shader->function = nil;
#endif
    
    delete shader;
}

MGG_OcclusionQuery* MGG_OcclusionQuery_Create(MGG_GraphicsDevice* device) 
{
    auto query = new MGG_OcclusionQuery();
    query->isComplete = false;
    return query;
}

void MGG_OcclusionQuery_Destroy(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query) 
{
    if (query) delete query;
}

void MGG_OcclusionQuery_Begin(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query) 
{
    // TODO: Implement occlusion query using Metal visibility result buffer
}

void MGG_OcclusionQuery_End(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query) 
{
    // TODO: Implement occlusion query end
    if (query) query->isComplete = true;
}

mgbyte MGG_OcclusionQuery_GetResult(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query, mgint& pixelCount) 
{
    if (!query) {
        pixelCount = 0;
        return false;
    }
    
    pixelCount = 0; // TODO: Return actual occlusion query result
    return query->isComplete;
}
