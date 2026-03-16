# CMakeLists.txt — Cross-compile libmgruntime.so for Android ARM64 (Quest 2/3)
#
# Usage:
#   mkdir -p build/android && cd build/android
#   cmake ../.. -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake \
#     -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-29 -DCMAKE_BUILD_TYPE=Release
#   make -j$(nproc)
#
# Output: ../../../../Artifacts/native/mgruntime/openxr/android/Release/libmgruntime.so

cmake_minimum_required(VERSION 3.18)
project(mgruntime LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# --- Output path (matches premake convention) ---
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../../Artifacts/native/mgruntime/openxr/android/Release")

# --- Defines ---
add_definitions(
    -DDLL_EXPORT
    -DANDROID
    -D__ANDROID__
    -DMG_VULKAN
    -DMG_OPENXR
    -DMG_FAUDIO
    -DXR_OS_ANDROID
    -DXR_USE_PLATFORM_ANDROID
    -DOPENXR_HAVE_COMMON_CONFIG
    -DXRLOADER_DISABLE_EXCEPTION_HANDLING
    -DVMA_VULKAN_VERSION=1001000
)

# --- Include paths ---
include_directories(
    include
    ${CMAKE_SOURCE_DIR}/../../external/stb
    external/vulkan-headers/include
    external/vma/include
    external/openxr-sdk/include
    external/openxr-sdk/src
    external/openxr-sdk/src/common
    external/openxr-sdk/src/loader
    external/openxr-sdk/src/external/jsoncpp/include
    external/openxr-sdk/src/external/android-jni-wrappers
    external/openxr-sdk/src/external/jnipp
    openxr
)

# --- MonoGame sources ---
set(MG_COMMON_SOURCES
    common/MG_Asset.cpp
    common/mg_audio.cpp
    common/mg_hash.cpp
    common/mg_library.cpp
    common/MGI.cpp
    common/MGM.cpp
)

set(MG_VULKAN_SOURCES
    vulkan/MGG_Vulkan.cpp
)

set(MG_OPENXR_SOURCES
    openxr/MGP_openxr.cpp
    openxr/MGXR_openxr.cpp
    faudio/MGA_faudio.cpp
)

# --- OpenXR loader (built from source) ---
set(OPENXR_LOADER_SOURCES
    external/openxr-sdk/src/loader/android_utilities.cpp
    external/openxr-sdk/src/loader/api_layer_interface.cpp
    external/openxr-sdk/src/loader/loader_core.cpp
    external/openxr-sdk/src/loader/loader_init_data.cpp
    external/openxr-sdk/src/loader/loader_instance.cpp
    external/openxr-sdk/src/loader/loader_logger_recorders.cpp
    external/openxr-sdk/src/loader/loader_logger.cpp
    external/openxr-sdk/src/loader/loader_properties.cpp
    external/openxr-sdk/src/loader/manifest_file.cpp
    external/openxr-sdk/src/loader/runtime_interface.cpp
    external/openxr-sdk/src/loader/xr_generated_loader.cpp
    external/openxr-sdk/src/common/filesystem_utils.cpp
    external/openxr-sdk/src/common/object_info.cpp
    external/openxr-sdk/src/xr_generated_dispatch_table_core.c
    external/openxr-sdk/src/xr_generated_dispatch_table.c
    external/openxr-sdk/src/external/jsoncpp/src/lib_json/json_reader.cpp
    external/openxr-sdk/src/external/jsoncpp/src/lib_json/json_value.cpp
    external/openxr-sdk/src/external/jsoncpp/src/lib_json/json_writer.cpp
    # Android JNI wrappers (needed by OpenXR loader on Android)
    external/openxr-sdk/src/external/android-jni-wrappers/wrap/android.content.cpp
    external/openxr-sdk/src/external/android-jni-wrappers/wrap/android.database.cpp
    external/openxr-sdk/src/external/android-jni-wrappers/wrap/android.net.cpp
    external/openxr-sdk/src/external/jnipp/jnipp.cpp
)

# --- FAudio (native Android backend, no SDL dependency) ---
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(PLATFORM_ANDROID ON CACHE BOOL "" FORCE)
set(BUILD_SDL3 OFF CACHE BOOL "" FORCE)
set(BUILD_UTILS OFF CACHE BOOL "" FORCE)
set(BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(FAUDIO_INSTALL OFF CACHE BOOL "" FORCE)
add_subdirectory(external/faudio faudio)

include_directories(external/faudio/include)

# --- Shared library ---
add_library(mgruntime SHARED
    ${MG_COMMON_SOURCES}
    ${MG_VULKAN_SOURCES}
    ${MG_OPENXR_SOURCES}
    ${OPENXR_LOADER_SOURCES}
)

# --- System libraries (Android provides these) ---
target_link_libraries(mgruntime
    FAudio-static
    vulkan
    android
    log
    dl
    m
)
