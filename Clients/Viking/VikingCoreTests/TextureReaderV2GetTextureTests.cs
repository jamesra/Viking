using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the take-once contract of <see cref="TextureReaderV2.GetTexture"/> and
    /// <see cref="TextureReaderV2.HasTexture"/>: the first successful take moves ownership out of the reader;
    /// a second take sees no texture left.
    /// </summary>
    /// <remarks>
    /// Production code completes a reader via <see cref="TextureReaderV2.LoadTexture"/> and the pending-texture
    /// queue; these tests call the protected <c>SetTexture</c> helper through reflection so the contract can be
    /// checked without decoding PNG bytes or pumping <see cref="PendingTextureQueue"/>.
    /// </remarks>
    [TestClass]
    public class TextureReaderV2GetTextureTests
    {
        private static readonly MethodInfo SetTexture =
            typeof(TextureReaderV2).GetMethod("SetTexture", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(TextureReaderV2), "SetTexture");

        private static readonly GraphicsDevice PlaceholderDevice = CreatePlaceholderDevice();

        private static GraphicsDevice CreatePlaceholderDevice()
        {
            var device = (GraphicsDevice)FormatterServices.GetUninitializedObject(typeof(GraphicsDevice));
            GC.SuppressFinalize(device);
            return device;
        }

        private static Texture2D NewStandInTexture()
        {
            var texture = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
            GC.SuppressFinalize(texture);
            return texture;
        }

        private static TextureReaderV2 NewReader(Uri tileUri)
        {
            using var cancel = new CancellationTokenSource();
            return new TextureReaderV2(PlaceholderDevice, tileUri, 1, null, cancel);
        }

        private static void AssignTexture(TextureReaderV2 reader, Texture2D texture) =>
            SetTexture.Invoke(reader, [texture]);

        [TestMethod]
        public void GetTextureBeforeLoadReturnsNullAndHasTextureIsFalse()
        {
            using var reader = NewReader(new Uri("http://tiles.test/before-load.png"));

            Assert.IsFalse(reader.HasTexture);
            Assert.IsNull(reader.GetTexture());
            Assert.IsFalse(reader.HasTexture);
        }

        [TestMethod]
        public void FirstGetTextureTransfersOwnershipAndClearsTheReader()
        {
            using var reader = NewReader(new Uri("http://tiles.test/take-once.png"));
            Texture2D standIn = NewStandInTexture();
            AssignTexture(reader, standIn);

            Assert.IsTrue(reader.HasTexture);
            Assert.AreSame(standIn, reader.GetTexture());
            Assert.IsFalse(reader.HasTexture);
            Assert.IsNull(reader.GetTexture());
        }

        [TestMethod]
        public void ReadersWithTheSameTileUriCompareEqual()
        {
            var uri = new Uri("http://tiles.test/same-tile.png");
            using var left = NewReader(uri);
            using var right = NewReader(uri);

            Assert.IsTrue(left == right);
            Assert.IsFalse(left != right);
            Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        }

        [TestMethod]
        public void ReadersWithDifferentTileUrisCompareUnequal()
        {
            using var left = NewReader(new Uri("http://tiles.test/a.png"));
            using var right = NewReader(new Uri("http://tiles.test/b.png"));

            Assert.IsFalse(left == right);
            Assert.IsTrue(left != right);
        }

        /// <summary>
        /// After any number of pre-take <see cref="TextureReaderV2.HasTexture"/> checks, exactly one
        /// <see cref="TextureReaderV2.GetTexture"/> succeeds and every later take is null.
        /// </summary>
        [TestMethod]
        public void GetTextureIsTakeOnceRegardlessOfHasTexturePolls()
        {
            Prop.ForAll(Arb.From(Gen.Choose(0, 5)), (int prePolls) =>
            {
                using var reader = NewReader(new Uri($"http://tiles.test/{Guid.NewGuid():N}.png"));
                Texture2D standIn = NewStandInTexture();
                AssignTexture(reader, standIn);

                for (int i = 0; i < prePolls; i++)
                    Assert.IsTrue(reader.HasTexture);

                Assert.AreSame(standIn, reader.GetTexture());
                Assert.IsFalse(reader.HasTexture);
                Assert.IsNull(reader.GetTexture());
            }).QuickCheckThrowOnFailure();
        }
    }
}
