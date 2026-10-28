using System.Linq;
using NUnit.Framework;
using Regolith.Sim;

namespace Regolith.Sim.Tests
{
    public class MarsMapGeneratorTests
    {
        const int Width = 64;
        const int Height = 40;

        [Test]
        public void SameSeedProducesIdenticalMap()
        {
            var a = MarsMapGenerator.Generate(Width, Height, 1234);
            var b = MarsMapGenerator.Generate(Width, Height, 1234);

            foreach (var tile in a.Tiles)
            {
                var other = b.GetTile(tile.Coord);
                Assert.That(other.Terrain, Is.EqualTo(tile.Terrain));
                Assert.That(other.Feature, Is.EqualTo(tile.Feature));
                Assert.That(other.Elevation, Is.EqualTo(tile.Elevation));
                Assert.That(other.Ice, Is.EqualTo(tile.Ice));
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentMaps()
        {
            var a = MarsMapGenerator.Generate(Width, Height, 1);
            var b = MarsMapGenerator.Generate(Width, Height, 2);
            Assert.That(a.Tiles.Any(t => b.GetTile(t.Coord).Terrain != t.Terrain));
        }

        [Test]
        public void FillsEveryOffsetCell()
        {
            var map = MarsMapGenerator.Generate(Width, Height, 7);
            Assert.That(map.TileCount, Is.EqualTo(Width * Height));
            for (int row = 0; row < Height; row++)
            for (int col = 0; col < Width; col++)
                Assert.That(map.Contains(HexCoord.FromOffset(col, row)));
        }

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(9001)]
        public void HasMarsGeography(int seed)
        {
            var map = MarsMapGenerator.Generate(Width, Height, seed);

            // Poles are ice caps.
            for (int col = 0; col < Width; col++)
            {
                Assert.That(map.GetTile(HexCoord.FromOffset(col, 0)).Terrain, Is.EqualTo(TerrainType.PolarIce));
                Assert.That(map.GetTile(HexCoord.FromOffset(col, Height - 1)).Terrain, Is.EqualTo(TerrainType.PolarIce));
            }

            Assert.That(map.Tiles.Count(t => t.Terrain == TerrainType.Canyon), Is.GreaterThan(Width / 5), "canyon");
            Assert.That(map.Tiles.Any(t => t.Terrain == TerrainType.CraterFloor), "craters");
            Assert.That(map.Tiles.Any(t => t.Terrain == TerrainType.VolcanicRise), "volcanoes");
        }

        [Test]
        public void ValuesStayInRange()
        {
            var map = MarsMapGenerator.Generate(Width, Height, 99);
            foreach (var t in map.Tiles)
            {
                Assert.That(t.Elevation, Is.InRange(-1f, 1f));
                Assert.That(t.Ice, Is.InRange(0f, 1f));
                Assert.That(t.Minerals, Is.InRange(0f, 1f));
            }
        }
    }

    public class YieldTests
    {
        [Test]
        public void TileYieldsCombineTerrainMineralsRimsAndDeposits()
        {
            var tile = new Tile(new HexCoord(0, 0))
            {
                Terrain = TerrainType.LunarHighlands, Minerals = 0.8f, Feature = TileFeature.CraterRim, Deposit = Deposit.Ilmenite,
            };
            var b = TerrainCatalog.Get(TerrainType.LunarHighlands).BaseYields;

            Assert.That(tile.Yields.Materials, Is.EqualTo(b.Materials + 1 + 1));
            Assert.That(tile.Yields.Science, Is.EqualTo(b.Science + 1));
            Assert.That(tile.Yields.Water, Is.EqualTo(0), "water only comes from ice mines");
        }

        [Test]
        public void EveryTerrainHasADefinition()
        {
            foreach (TerrainType t in System.Enum.GetValues(typeof(TerrainType)))
                Assert.That(TerrainCatalog.Get(t).Name, Is.Not.Empty);
        }
    }
}
