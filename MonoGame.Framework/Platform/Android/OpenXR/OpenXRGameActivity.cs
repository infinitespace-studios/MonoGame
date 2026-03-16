// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Runtime.InteropServices;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// Base activity for MonoGame OpenXR apps on Meta Quest.
    /// Unlike the standard AndroidGameActivity, this does not use a SurfaceView or OpenGL ES.
    /// Rendering is handled entirely by the native mgruntime library via Vulkan + OpenXR.
    /// </summary>
    [Activity(
        Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden,
        ScreenOrientation = ScreenOrientation.Landscape)]
    public class OpenXRGameActivity : Activity
    {
        internal Game Game { get; set; }

        [DllImport("mgruntime", EntryPoint = "MGXR_SetAndroidContext")]
        private static extern void MGXR_SetAndroidContext(IntPtr javaVM, IntPtr activity);

        [DllImport("mgruntime", EntryPoint = "MG_Asset_SetAssetManager")]
        private static extern void MG_Asset_SetAssetManager(IntPtr javaVM, IntPtr assetManager);

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Quest apps are immersive — no system UI, keep screen on
            Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            Window.DecorView.SystemUiVisibility =
                (StatusBarVisibility)(
                    SystemUiFlags.ImmersiveSticky |
                    SystemUiFlags.LayoutStable |
                    SystemUiFlags.LayoutHideNavigation |
                    SystemUiFlags.LayoutFullscreen |
                    SystemUiFlags.HideNavigation |
                    SystemUiFlags.Fullscreen);

            // Load native runtime (OpenXR loader is statically linked into mgruntime)
            Java.Lang.JavaSystem.LoadLibrary("mgruntime");

            // Pass Android context to native OpenXR code
            var javaVM = Java.Interop.JniEnvironment.Runtime.InvocationPointer;
            var activityHandle = this.Handle;
            MGXR_SetAndroidContext(javaVM, activityHandle);

            // Pass Android AssetManager to native code for APK content loading
            var assetManagerHandle = this.Assets.Handle;
            MG_Asset_SetAssetManager(javaVM, assetManagerHandle);

            Game.Activity = this;
        }

        protected override void OnResume()
        {
            base.OnResume();
            // OpenXR session state transitions are handled by the native xrPollEvent loop.
            // No need to manually resume rendering — the XR runtime manages visibility.
        }

        protected override void OnPause()
        {
            base.OnPause();
            // OpenXR handles session stopping via its state machine.
            // The runtime transitions: Focused → Visible → Synchronized → Idle → Stopping
        }

        protected override void OnDestroy()
        {
            if (Game != null)
            {
                Game.Dispose();
                Game = null;
            }
            base.OnDestroy();
        }
    }
}
