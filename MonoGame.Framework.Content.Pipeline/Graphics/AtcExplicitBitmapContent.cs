﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics
{
    public class AtcExplicitBitmapContent : AtcBitmapContent
    {
        /// <summary>
        /// Creates an instance of AtcExplicitBitmapContent.
        /// </summary>
        public AtcExplicitBitmapContent()
        {
        }

        /// <summary>
        /// Creates an instance of AtcExplicitBitmapContent with the specified width and height.
        /// </summary>
        /// <param name="width">The width in pixels of the bitmap.</param>
        /// <param name="height">The height in pixels of the bitmap.</param>
        public AtcExplicitBitmapContent(int width, int height)
            : base(width, height)
        {
        }

        /// <summary>
        /// Gets the corresponding GPU texture format for the specified bitmap type.
        /// </summary>
        /// <param name="format">Format being retrieved.</param>
        /// <returns>The GPU texture format of the bitmap type.</returns>
        public override bool TryGetFormat(out SurfaceFormat format)
        {
            format = SurfaceFormat.RgbaATCExplicitAlpha;
            return true;
        }
    }
}