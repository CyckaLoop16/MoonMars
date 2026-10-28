using System.Linq;
using NUnit.Framework;
using Regolith.Sim;

namespace Regolith.Sim.Tests
{
    public class ResearchTests
    {
        static Faction Player(Simulation sim) => sim.State.GetFaction(1);

        static void FinishConstruction(Colony colony)
        {
            foreach (var m in colony.Modules) m.BuildDaysRemaining = 0;
        }

        [Test]
        public void LockedModulesNeedTheirTech()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var at = TestMaps.Free(colony, 0);

            Assert.That(new PlaceModule(colony.Id, at, ModuleType.Foundry).Validate(sim.State), Does.Contain("Regolith Sintering"));
            Player(sim).Researched.Add(TechId.RegolithSintering);
            Assert.That(new PlaceModule(colony.Id, at, ModuleType.Foundry).Validate(sim.State), Is.Null);
        }

        [Test]
        public void PrerequisitesAreEnforced()
        {
            var sim = TestMaps.NewGame();
            Assert.That(new SetResearch(1, TechId.KilopowerReactors).Validate(sim.State), Does.Contain("Regolith Sintering"));
            Assert.That(new SetResearch(1, TechId.RegolithSintering).Validate(sim.State), Is.Null);
        }

        [Test]
        public void LabsProduceScienceThatCompletesResearch()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.Lab);
            FinishConstruction(colony);
            Assert.That(sim.Execute(new SetResearch(1, TechId.IceProspecting), out var error), Is.True, error);

            sim.Advance(60);

            Assert.That(Player(sim).Has(TechId.IceProspecting));
            Assert.That(Player(sim).CurrentResearch, Is.Null);
            Assert.That(sim.State.Log.Any(e => e.Text.Contains("research complete: Ice Prospecting")));
        }

        [Test]
        public void ScienceIsBankedUntilAResearchIsPicked()
        {
            var sim = TestMaps.NewGame();
            TestMaps.Found(sim);
            sim.Advance(10);

            var f = Player(sim);
            Assert.That(f.ScienceBank, Is.GreaterThan(0));
            double banked = f.ScienceBank;
            sim.Execute(new SetResearch(1, TechId.RegolithSintering), out _);
            Assert.That(f.Progress[TechId.RegolithSintering], Is.EqualTo(banked).Within(1e-9));
            Assert.That(f.ScienceBank, Is.EqualTo(0));
        }

        [Test]
        public void BoostsCutTheCost()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var f = Player(sim);
            Assert.That(ResearchSystem.EffectiveCost(f, TechId.RegolithSintering), Is.EqualTo(150));

            for (int i = 0; i < 3; i++) TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.SolarArray);
            FinishConstruction(colony);
            sim.Tick();

            Assert.That(f.Boosted, Does.Contain(TechId.RegolithSintering));
            Assert.That(ResearchSystem.EffectiveCost(f, TechId.RegolithSintering), Is.EqualTo(90).Within(1e-9));
            Assert.That(sim.State.Log.Any(e => e.Text.Contains("breakthrough")));
        }

        [Test]
        public void BatteriesCarryTheColonyIntoTheNight()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            Player(sim).Researched.Add(TechId.BatteryStorage);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.SolarArray);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.OxygenPlant);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.BatteryBank);
            FinishConstruction(colony);

            sim.Advance(16); // day 16: the first lunar night has begun
            Assert.That(Bodies.IsLunarDaylight(sim.State.Day), Is.False);

            var plant = colony.Modules.Single(m => m.Type == ModuleType.OxygenPlant);
            Assert.That(plant.Efficiency, Is.EqualTo(1.0).Within(1e-9), "batteries cover the night deficit");
            Assert.That(colony.StoredEnergy, Is.GreaterThan(0));
        }

        [Test]
        public void ResearchedTechsChangeTheEconomy()
        {
            var f = new Faction();
            Assert.That(Modifiers.OutputMultiplier(f, ModuleType.IceMine), Is.EqualTo(1.0));
            f.Researched.Add(TechId.IceProspecting);
            Assert.That(Modifiers.OutputMultiplier(f, ModuleType.IceMine), Is.EqualTo(1.3));

            f.Researched.Add(TechId.AdditiveConstruction);
            Assert.That(Modifiers.BuildDays(f, ModuleCatalog.Get(ModuleType.Habitat)), Is.EqualTo(8));
        }
    }

    public class ResourceTests
    {
        [Test]
        public void DepositsBoostTheRightModules()
        {
            Assert.That(DepositCatalog.Multiplier(Deposit.Volatiles, ModuleType.IceMine), Is.EqualTo(1.5));
            Assert.That(DepositCatalog.Multiplier(Deposit.Volatiles, ModuleType.Greenhouse), Is.EqualTo(1.0));
            Assert.That(DepositCatalog.Multiplier(Deposit.Ilmenite, ModuleType.OxygenPlant), Is.EqualTo(1.5));
            Assert.That(DepositCatalog.Multiplier(Deposit.None, ModuleType.OxygenPlant), Is.EqualTo(1.0));
        }

        [Test]
        public void TerritoryYieldsMaterialsAutomatically()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            double before = colony.Stock[Resource.Materials];

            sim.Advance(30);

            // 19 highland hexes at 1 material per month each, no modules or crew needed.
            double expected = colony.Territory.Sum(c => sim.State.Map.GetTile(c).Yields.Materials);
            Assert.That(colony.Stock[Resource.Materials] - before, Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void GeneratedMapsHaveEveryDepositType()
        {
            var (w, h) = GameSetup.Dimensions(MapSize.Large);
            var map = LunarMapGenerator.Generate(w, h, 11);
            foreach (Deposit d in System.Enum.GetValues(typeof(Deposit)))
                if (d != Deposit.None)
                    Assert.That(map.Tiles.Any(t => t.Deposit == d), d.ToString());

            Assert.That(map.Tiles.Where(t => t.Deposit == Deposit.Volatiles).All(t => t.Terrain == TerrainType.ShadowedCrater));
        }
    }
}
