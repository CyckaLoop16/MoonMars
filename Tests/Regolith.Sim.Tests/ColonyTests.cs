using System.Linq;
using NUnit.Framework;
using Regolith.Sim;

namespace Regolith.Sim.Tests
{
    /// <summary>A small hand-built lunar map so economy tests don't depend on procedural generation.</summary>
    static class TestMaps
    {
        public static readonly HexCoord Base = HexCoord.FromOffset(8, 8);
        public static readonly HexCoord IceCrater = HexCoord.FromOffset(9, 8);
        public static readonly HexCoord Peak = HexCoord.FromOffset(7, 8);

        public static Simulation NewGame()
        {
            var map = new WorldMap(16, 16, 0, BodyId.Moon);
            for (int row = 0; row < 16; row++)
            for (int col = 0; col < 16; col++)
            {
                map.Add(new Tile(HexCoord.FromOffset(col, row))
                {
                    Terrain = TerrainType.LunarHighlands, Ice = 0.1f, Minerals = 0.5f,
                });
            }

            var ice = map.GetTile(IceCrater);
            ice.Terrain = TerrainType.ShadowedCrater;
            ice.Ice = 0.9f;
            map.GetTile(Peak).Feature = TileFeature.PeakOfLight;

            var state = new GameState(map);
            state.Factions.Add(new Faction { Id = 1, Name = "Test Faction", Color = "#ffffff" });
            state.Factions.Add(new Faction { Id = 2, Name = "Rival", Color = "#ff0000" });
            return new Simulation(state);
        }

        public static Colony Found(Simulation sim)
        {
            Assert.That(sim.Execute(new FoundColony(1, Base, "Shackleton Base"), out var error), Is.True, error);
            return sim.State.Colonies.Single();
        }

        public static void Build(Simulation sim, Colony colony, HexCoord at, ModuleType type)
        {
            Assert.That(sim.Execute(new PlaceModule(colony.Id, at, type), out var error), Is.True, error);
        }

        /// <summary>Some tile in the colony's territory other than the ones reserved above.</summary>
        public static HexCoord Free(Colony colony, int index) =>
            colony.Territory.Where(c => c != Base && c != IceCrater && c != Peak && colony.ModuleAt(c) == null)
                .OrderBy(c => c.Q).ThenBy(c => c.R).ElementAt(index);
    }

    public class ColonyTests
    {
        [Test]
        public void FoundingClaimsTerritoryAndLandsTheHabCore()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);

            Assert.That(colony.Territory, Has.Count.EqualTo(19));
            Assert.That(colony.ModuleAt(TestMaps.Base).Type, Is.EqualTo(ModuleType.HabCore));
            Assert.That(colony.Pops, Is.EqualTo(FoundColony.StartingPops));
            Assert.That(colony.Housing, Is.EqualTo(6));
        }

        [Test]
        public void FoundingRules()
        {
            var sim = TestMaps.NewGame();
            Assert.That(new FoundColony(1, TestMaps.IceCrater, "x").Validate(sim.State), Does.Contain("cold"));

            TestMaps.Found(sim);
            Assert.That(new FoundColony(1, HexCoord.FromOffset(1, 1), "x").Validate(sim.State), Does.Contain("already"));
            Assert.That(new FoundColony(2, HexCoord.FromOffset(10, 9), "x").Validate(sim.State), Does.Contain("Too close"));
            Assert.That(new FoundColony(2, HexCoord.FromOffset(1, 1), "x").Validate(sim.State), Is.Null);
        }

        [Test]
        public void PlacementRules()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var s = sim.State;

            Assert.That(new PlaceModule(colony.Id, HexCoord.FromOffset(1, 1), ModuleType.SolarArray).Validate(s), Does.Contain("Outside"));
            Assert.That(new PlaceModule(colony.Id, TestMaps.Base, ModuleType.SolarArray).Validate(s), Does.Contain("already"));
            Assert.That(new PlaceModule(colony.Id, TestMaps.Free(colony, 0), ModuleType.IceMine).Validate(s), Does.Contain("ice"));
            Assert.That(new PlaceModule(colony.Id, TestMaps.IceCrater, ModuleType.SolarArray).Validate(s), Does.Contain("dark"));
            Assert.That(new PlaceModule(colony.Id, TestMaps.IceCrater, ModuleType.IceMine).Validate(s), Is.Null);
            Assert.That(new PlaceModule(colony.Id, TestMaps.Free(colony, 0), ModuleType.HabCore).Validate(s), Is.Not.Null);
        }

        [Test]
        public void ConstructionCostsMaterialsAndRunsOneAtATime()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            double materials = colony.Stock[Resource.Materials];

            TestMaps.Build(sim, colony, TestMaps.Peak, ModuleType.SolarArray);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.Habitat);
            Assert.That(colony.Stock[Resource.Materials], Is.EqualTo(materials - 20 - 25));

            var solar = colony.ModuleAt(TestMaps.Peak);
            var habitat = colony.Modules.Single(m => m.Type == ModuleType.Habitat);

            sim.Advance(5);
            Assert.That(solar.IsBuilt, Is.False);
            sim.Advance(1);
            Assert.That(solar.IsBuilt, Is.True);
            Assert.That(habitat.BuildDaysRemaining, Is.EqualTo(ModuleCatalog.Get(ModuleType.Habitat).BuildDays));

            sim.Advance(10);
            Assert.That(habitat.IsBuilt, Is.True);
            Assert.That(colony.Housing, Is.EqualTo(12));
        }

        [Test]
        public void AColonyWithoutLifeSupportDies()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);

            sim.Advance(120);
            Assert.That(colony.Pops, Is.LessThan(FoundColony.StartingPops));
            Assert.That(sim.State.Log.Any(e => e.Text.Contains("hypoxia")));
        }

        [Test]
        public void PowerShortfallSlowsConsumers()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.OxygenPlant);
            sim.Advance(ModuleCatalog.Get(ModuleType.OxygenPlant).BuildDays + 1);

            // Hab core supplies 3 energy; demand is 2 (hab) + 4 (plant) = 6.
            var plant = colony.Modules.Single(m => m.Type == ModuleType.OxygenPlant);
            Assert.That(colony.LastReport.EnergySupply, Is.EqualTo(3).Within(1e-9));
            Assert.That(colony.LastReport.EnergyDemand, Is.EqualTo(6).Within(1e-9));
            Assert.That(plant.Efficiency, Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void LunarNightTurnsOffOrdinarySolarButNotPeaksOfLight()
        {
            var sim = TestMaps.NewGame();
            var map = sim.State.Map;
            var plain = map.GetTile(HexCoord.FromOffset(3, 3));
            var peak = map.GetTile(TestMaps.Peak);

            Assert.That(Bodies.SolarAvailability(BodyId.Moon, plain, 5), Is.EqualTo(1.0));
            Assert.That(Bodies.SolarAvailability(BodyId.Moon, plain, 20), Is.EqualTo(0.0));
            Assert.That(Bodies.SolarAvailability(BodyId.Moon, peak, 20), Is.EqualTo(0.9));
            Assert.That(Bodies.SolarAvailability(BodyId.Moon, map.GetTile(TestMaps.IceCrater), 5), Is.EqualTo(0.0));
        }

        [Test]
        public void AWellPlannedBaseSurvivesItsFirstYearAndGrows()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);

            // Power first, then air, water, food, then more power and room to grow.
            TestMaps.Build(sim, colony, TestMaps.Peak, ModuleType.SolarArray);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.OxygenPlant);
            TestMaps.Build(sim, colony, TestMaps.IceCrater, ModuleType.IceMine);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.Greenhouse);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.SolarArray);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.Foundry);

            sim.Advance(365);

            Assert.That(colony.IsAlive);
            Assert.That(colony.Pops, Is.GreaterThan(FoundColony.StartingPops));
            Assert.That(sim.State.Log.Any(e => e.Text.Contains("died")), Is.False,
                string.Join("\n", sim.State.Log.Select(e => $"day {e.Day}: {e.Text}")));
        }

        [Test]
        public void SameCommandsSameOutcome()
        {
            Colony Play()
            {
                var sim = TestMaps.NewGame();
                var colony = TestMaps.Found(sim);
                TestMaps.Build(sim, colony, TestMaps.Peak, ModuleType.SolarArray);
                TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.OxygenPlant);
                sim.Advance(200);
                return colony;
            }

            var a = Play();
            var b = Play();
            for (int i = 0; i < Stockpile.Count; i++)
                Assert.That(b.Stock[(Resource)i], Is.EqualTo(a.Stock[(Resource)i]));
            Assert.That(b.Pops, Is.EqualTo(a.Pops));
        }

        [Test]
        public void TurnsAdvanceOneCalendarMonth()
        {
            var sim = TestMaps.NewGame();
            Assert.That(sim.Turn, Is.EqualTo(1));
            sim.AdvanceToNextMonth();
            Assert.That(sim.State.Date, Is.EqualTo(new System.DateTime(2040, 2, 1)));
            sim.AdvanceToNextMonth();
            Assert.That(sim.State.Date, Is.EqualTo(new System.DateTime(2040, 3, 1)));
            Assert.That(sim.Turn, Is.EqualTo(3));
        }

        [Test]
        public void CalendarStartsIn2040AndMonthlyPassRuns()
        {
            var sim = TestMaps.NewGame();
            Assert.That(sim.State.Date, Is.EqualTo(new System.DateTime(2040, 1, 1)));
            sim.Advance(31);
            Assert.That(sim.State.Date, Is.EqualTo(new System.DateTime(2040, 2, 1)));
        }
    }

    public class LunarMapGeneratorTests
    {
        [TestCase(1, MapSize.Small)]
        [TestCase(42, MapSize.Standard)]
        [TestCase(777, MapSize.Large)]
        [TestCase(3, MapSize.Huge)]
        public void HasThePolarPuzzlePieces(int seed, MapSize size)
        {
            var (width, height) = GameSetup.Dimensions(size);
            var map = LunarMapGenerator.Generate(width, height, seed);

            Assert.That(map.Body, Is.EqualTo(BodyId.Moon));
            Assert.That(map.TileCount, Is.EqualTo(width * height));

            var shadowed = map.Tiles.Where(t => t.Terrain == TerrainType.ShadowedCrater).ToList();
            Assert.That(shadowed, Is.Not.Empty);
            Assert.That(shadowed.All(t => t.Ice >= 0.55f));

            int peaks = map.Tiles.Count(t => t.Feature == TileFeature.PeakOfLight);
            Assert.That(peaks, Is.GreaterThan(5));
            Assert.That(peaks, Is.LessThan(map.TileCount / 20), "peaks of light should be scarce");
        }

        [Test]
        public void RealLandmarksSitWhereTheyShould()
        {
            var (width, height) = GameSetup.Dimensions(MapSize.Large);
            var map = LunarMapGenerator.Generate(width, height, 9);
            var pole = HexCoord.FromOffset(width / 2, height / 2);

            var shackleton = map.Landmarks.Single(l => l.Name == "Shackleton");
            Assert.That(shackleton.Center.DistanceTo(pole), Is.LessThanOrEqualTo(4), "Shackleton is at the pole");
            Assert.That(map.GetTile(shackleton.Center).Terrain, Is.EqualTo(TerrainType.ShadowedCrater));
            Assert.That(map.LandmarkAt(shackleton.Center).Name, Is.EqualTo("Shackleton"));

            // Malapert faces Earth (longitude ~0°, to the right of the pole) and its summit is sunlit.
            var malapert = map.Landmarks.Single(l => l.Name == "Malapert Massif");
            Assert.That(malapert.Center.Q, Is.GreaterThan(pole.Q));
            Assert.That(malapert.Center.WithinRange(2).Any(c => map.GetTile(c)?.Feature == TileFeature.PeakOfLight));
        }

        [Test]
        public void Deterministic()
        {
            var a = LunarMapGenerator.Generate(96, 64, 5);
            var b = LunarMapGenerator.Generate(96, 64, 5);
            Assert.That(a.Tiles.All(t => b.GetTile(t.Coord).Terrain == t.Terrain && b.GetTile(t.Coord).Elevation == t.Elevation));
        }

        [Test]
        public void HexRangeCount()
        {
            Assert.That(new HexCoord(3, 3).WithinRange(2).Count(), Is.EqualTo(19));
            Assert.That(new HexCoord(3, 3).WithinRange(2).All(c => c.DistanceTo(new HexCoord(3, 3)) <= 2));
        }
    }
}
