using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Tests.Graphics;
using NUnit.Framework;
using StbImageWriteSharp;

namespace MonoGame.Tests.Content
{
    [NonParallelizable]
    [RunOnUiTestFixture]
    internal class ContentManagerTest : GraphicsDeviceTestFixtureBase
    {
        [Test]
        // Tests loading a texture from a XNB file
        public void CorrectlyLoadTextureFromXnb()
        {
            ContentManager content = new ContentManager(game.Services);
            
            Texture2D texture = content.Load<Texture2D>(Paths.Texture("MonoGameIcon"));

            Assert.IsNotNull(texture);
        }

        [Test]
        [TestCase("UniquePng")]
        [TestCase("UniqueBmp")]
        [TestCase("UniqueJpg")]
        [TestCase("UniqueJpeg")]
        // Tests loading from a PNG/JPG/JPEG/BMP file when a corresponding XNB file doesn't exist
        public void CorrectlyLoadTextureFromAlternativeImageFormatsWhenNoXnb(string assetName)
        {
            ContentManager content = new ContentManager(game.Services);

            Texture2D texture = content.Load<Texture2D>(Paths.Texture(assetName));

            var color = new Color(assetName.Contains("J") ? 233 : 231, assetName.Contains("J") ? 59 : 60, 0, 255);

            Assert.IsNotNull(texture);

            // check that the image is imported the correct way up
            var data = texture.GetColorData();
            byte[] bytes = new byte[data.Length * 4];
            int i = 0;
            foreach (var c in data)
            {
                bytes[i++] = c.R;
                bytes[i++] = c.G;
                bytes[i++] = c.B;
                bytes[i++] = c.A;

            }

            Assert.AreEqual(texture.Width * texture.Height, data.Length);

            ImageWriter w = new ImageWriter();
            w.WriteBmp(bytes, texture.Width, texture.Height, ColorComponents.RedGreenBlueAlpha, File.OpenWrite(assetName+"out.bmp"));
            // sample 40,25 should be transparent
            Assert.AreNotEqual(color, data[40 * texture.Width + 25]);
            // sample 40,40 should not be transparent
            Assert.AreEqual(color, data[40 * texture.Width + 40]);
        }

        [Test]
        // Tests that an exception is raised when no XNB, PNG, JPG, JPEG or BMP exists for a content name
        public void ThrowExceptionIfNoAssetInAnySupportedImageFormats()
        {
            ContentManager content = new ContentManager(game.Services);

            var exception = Assert.Throws<ContentLoadException>(() => content.Load<Texture2D>(Paths.Texture("NotExisting")));
            StringAssert.StartsWith("The content file was not found.", exception.Message);
            Assert.IsInstanceOf(typeof(FileNotFoundException), exception.InnerException);
        }

        [Test]
        // Tests that an exception is raised when a non XNB format exists but the content type requested is not a Texture
        public void ThrowExceptionIfTypeIsNotTexture()
        {
            ContentManager content = new ContentManager(game.Services);

            var exception = Assert.Throws<ContentLoadException>(() => content.Load<SoundEffect>(Paths.Texture("UniquePng")));
            StringAssert.StartsWith("The content file was not found.", exception.Message);
            Assert.IsInstanceOf(typeof(FileNotFoundException), exception.InnerException);
        }
    }
}
