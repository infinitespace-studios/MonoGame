// Generated configuration for OpenXR loader built from source.
// On macOS/iOS neither secure_getenv nor __secure_getenv is available.
// On Linux, secure_getenv is typically available.
// On Windows, neither is needed.

#ifndef OPENXR_COMMON_CONFIG_H
#define OPENXR_COMMON_CONFIG_H

#if defined(__linux__) && !defined(__ANDROID__)
#define HAVE_SECURE_GETENV 1
#endif

// Mark that we have this config
#ifndef OPENXR_HAVE_COMMON_CONFIG
#define OPENXR_HAVE_COMMON_CONFIG
#endif

#endif // OPENXR_COMMON_CONFIG_H
