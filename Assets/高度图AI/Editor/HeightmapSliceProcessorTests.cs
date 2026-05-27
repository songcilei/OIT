using NUnit.Framework;
using UnityEngine;

namespace HeightmapAI.Editor
{
    public sealed class HeightmapSliceProcessorTests
    {
        [Test]
        public void SampleWithObjectOverlayReturnsHigherObjectHeight()
        {
            var source = new ConstantHeightmapSource(4, 4, 0.25f);
            var settings = new HeightmapObjectOverlaySettings(
                true,
                new Vector3(10f, 20f, 30f),
                new Vector3(100f, 40f, 100f),
                new[] { new OverlayHit(new Vector2Int(1, 2), 0.75f) });

            float height = HeightmapSliceProcessor.SampleWithOverlay(source, 1, 2, settings);

            Assert.AreEqual(0.75f, height, 0.0001f);
        }

        [Test]
        public void SampleWithObjectOverlayKeepsTerrainWhenObjectIsLower()
        {
            var source = new ConstantHeightmapSource(4, 4, 0.6f);
            var settings = new HeightmapObjectOverlaySettings(
                true,
                new Vector3(10f, 20f, 30f),
                new Vector3(100f, 40f, 100f),
                new[] { new OverlayHit(new Vector2Int(1, 2), 0.3f) });

            float height = HeightmapSliceProcessor.SampleWithOverlay(source, 1, 2, settings);

            Assert.AreEqual(0.6f, height, 0.0001f);
        }

        private sealed class ConstantHeightmapSource : IHeightmapSource
        {
            private readonly float height;

            public ConstantHeightmapSource(int width, int heightmapHeight, float height)
            {
                Width = width;
                Height = heightmapHeight;
                this.height = height;
            }

            public string Name => "constant";
            public int Width { get; }
            public int Height { get; }

            public float Sample(int x, int y)
            {
                return height;
            }
        }
    }
}
