using System.Linq;
using NUnit.Framework;
using Regolith.Sim;

namespace Regolith.Sim.Tests
{
    public class EarthTests
    {
        static Faction Player(Simulation sim) => sim.State.GetFaction(1);

        static void FinishConstruction(Colony colony)
        {
            foreach (var m in colony.Modules) m.BuildDaysRemaining = 0;
        }

        [Test]
        public void EarthPaysFundingEveryTurn()
        {
            var sim = TestMaps.NewGame();
            TestMaps.Found(sim); // completes "Boots on the Regolith" on the next tick
            var f = Player(sim);

            sim.AdvanceToNextMonth();

            Assert.That(f.CompletedObjectives, Does.Contain(ObjectiveId.FirstLanding));
            var landing = EarthCatalog.Get(ObjectiveId.FirstLanding);
            Assert.That(EarthSystem.Funding(sim.State, f), Is.EqualTo(EarthCatalog.BaseFunding + landing.FundingIncrease).Within(1e-9));
            Assert.That(f.Credits, Is.EqualTo(landing.Reward + EarthSystem.Funding(sim.State, f)).Within(1e-9));
        }

        [Test]
        public void CargoCostsCreditsAndArrivesAfterTransit()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var f = Player(sim);

            var order = new OrderCargo(1, colony.Id, CargoType.Materials);
            Assert.That(order.Validate(sim.State), Does.Contain("credits"));

            f.Credits = 100;
            double materials = colony.Stock[Resource.Materials];
            Assert.That(sim.Execute(order, out var error), Is.True, error);
            Assert.That(f.Credits, Is.EqualTo(20));

            sim.Advance(EarthCatalog.TransitDays(BodyId.Moon) - 1);
            Assert.That(colony.Stock[Resource.Materials], Is.LessThan(materials + 100));
            sim.Advance(1);
            Assert.That(colony.Stock[Resource.Materials], Is.GreaterThanOrEqualTo(materials + 100));
        }

        [Test]
        public void CrewFlightsNeedHousing()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            Player(sim).Credits = 1000;

            // 4 crew in housing for 6: one flight of 2 fits, a second doesn't.
            Assert.That(sim.Execute(new OrderCargo(1, colony.Id, CargoType.Crew), out var error), Is.True, error);
            Assert.That(new OrderCargo(1, colony.Id, CargoType.Crew).Validate(sim.State), Does.Contain("housing"));

            sim.Advance(3);
            Assert.That(colony.Pops, Is.EqualTo(6));
        }

        [Test]
        public void SpecialistCargoBringsAPioneer()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            Player(sim).Credits = 1000;
            int before = sim.State.Pioneers.Count;

            sim.Execute(new OrderCargo(1, colony.Id, CargoType.Specialist, Expertise.Explorer), out _);
            sim.Advance(3);

            Assert.That(sim.State.Pioneers.Count, Is.EqualTo(before + 1));
            Assert.That(sim.State.Pioneers.Last().Expertise, Is.EqualTo(Expertise.Explorer));
        }

        [Test]
        public void ExtractorsMineStrategicDepositsForExport()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var f = Player(sim);
            var site = TestMaps.Free(colony, 0);
            var plain = TestMaps.Free(colony, 1);
            sim.State.Map.GetTile(site).Deposit = Deposit.PlatinumMetals;

            Assert.That(new PlaceModule(colony.Id, site, ModuleType.MineralExtractor).Validate(sim.State), Does.Contain("Mineral Beneficiation"));
            f.Researched.Add(TechId.MineralBeneficiation);
            Assert.That(new PlaceModule(colony.Id, plain, ModuleType.MineralExtractor).Validate(sim.State), Does.Contain("strategic deposit"));

            TestMaps.Build(sim, colony, site, ModuleType.MineralExtractor);
            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.SolarArray);
            FinishConstruction(colony);
            sim.Advance(10);

            Assert.That(colony.Stock[Resource.PlatinumMetals], Is.GreaterThan(1));

            double credits = f.Credits;
            double units = System.Math.Floor(colony.Stock[Resource.PlatinumMetals]);
            Assert.That(sim.Execute(new ExportToEarth(1, colony.Id, Resource.PlatinumMetals), out var error), Is.True, error);
            Assert.That(f.Credits, Is.EqualTo(credits + units * EarthCatalog.ExportPrice(Resource.PlatinumMetals)).Within(1e-9));
            Assert.That(f.UnitsExported, Is.EqualTo(units));
        }
    }

    public class PioneerTests
    {
        static Faction Player(Simulation sim) => sim.State.GetFaction(1);

        static void FinishConstruction(Colony colony)
        {
            foreach (var m in colony.Modules) m.BuildDaysRemaining = 0;
        }

        [Test]
        public void TheFirstLandingBringsThreeFoundingPioneers()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);

            var pioneers = sim.State.Pioneers;
            Assert.That(pioneers, Has.Count.EqualTo(3));
            Assert.That(pioneers.Select(p => p.Expertise), Is.EquivalentTo(new[] { Expertise.Administrator, Expertise.Engineer, Expertise.Scientist }));
            Assert.That(pioneers.All(p => !string.IsNullOrWhiteSpace(p.Name) && p.Traits.Count == 1));
            Assert.That(PioneerSystem.AdministratorOf(sim.State, colony).Expertise, Is.EqualTo(Expertise.Administrator));
        }

        [Test]
        public void AssignmentRules()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var s = sim.State;
            var engineer = s.Pioneers.Single(p => p.Expertise == Expertise.Engineer);
            var scientist = s.Pioneers.Single(p => p.Expertise == Expertise.Scientist);

            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.OxygenPlant);
            var plant = colony.Modules.Last().Coord;
            Assert.That(new AssignPioneer(1, engineer.Id, Assignment.ToModule(colony.Id, plant)).Validate(s), Does.Contain("No working module"));

            FinishConstruction(colony);
            Assert.That(sim.Execute(new AssignPioneer(1, engineer.Id, Assignment.ToModule(colony.Id, plant)), out var error), Is.True, error);
            Assert.That(new AssignPioneer(1, scientist.Id, Assignment.ToModule(colony.Id, plant)).Validate(s), Does.Contain(engineer.Name));
            Assert.That(new AssignPioneer(1, scientist.Id, Assignment.AsAdministrator(colony.Id)).Validate(s), Does.Contain("already runs"));
            Assert.That(new AssignPioneer(1, scientist.Id, Assignment.ToModule(colony.Id, TestMaps.Base)).Validate(s), Does.Contain("no use"));

            Assert.That(sim.Execute(new AssignPioneer(1, scientist.Id, Assignment.OnCouncil(CouncilSeat.ChiefScientist)), out error), Is.True, error);
            Assert.That(new AssignPioneer(1, engineer.Id, Assignment.OnCouncil(CouncilSeat.ChiefScientist)).Validate(s), Does.Contain("seat"));
        }

        [Test]
        public void APioneerBoostsTheirModule()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var engineer = sim.State.Pioneers.Single(p => p.Expertise == Expertise.Engineer);
            engineer.Traits.Clear();

            TestMaps.Build(sim, colony, TestMaps.Free(colony, 0), ModuleType.OxygenPlant);
            FinishConstruction(colony);
            var plant = colony.Modules.Last();

            Assert.That(PioneerSystem.ModuleBonus(sim.State, colony, plant), Is.EqualTo(1.0));
            sim.Execute(new AssignPioneer(1, engineer.Id, Assignment.ToModule(colony.Id, plant.Coord)), out _);
            Assert.That(PioneerSystem.ModuleBonus(sim.State, colony, plant), Is.EqualTo(1.0 + 0.08 * engineer.Skill).Within(1e-9));
        }

        [Test]
        public void CouncilSeatsHaveFactionWideEffects()
        {
            var sim = TestMaps.NewGame();
            TestMaps.Found(sim);
            var s = sim.State;
            var f = Player(sim);
            var scientist = s.Pioneers.Single(p => p.Expertise == Expertise.Scientist);
            var commander = s.Pioneers.Single(p => p.Expertise == Expertise.Administrator);
            commander.Traits.Clear();

            sim.Execute(new AssignPioneer(1, scientist.Id, Assignment.OnCouncil(CouncilSeat.ChiefScientist)), out _);
            Assert.That(PioneerSystem.ScienceMultiplier(s, f), Is.EqualTo(1.0 + 0.05 * scientist.Skill).Within(1e-9));

            sim.Execute(new AssignPioneer(1, commander.Id, Assignment.OnCouncil(CouncilSeat.EarthLiaison)), out _);
            Assert.That(PioneerSystem.FundingMultiplier(s, f), Is.EqualTo(1.0 + 0.06 * commander.Skill).Within(1e-9));
        }

        [Test]
        public void FieldWorkIsRiskierThanTheCouncil()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var s = sim.State;
            var p = s.Pioneers.Single(x => x.Expertise == Expertise.Engineer);
            p.Traits.Clear();

            TestMaps.Build(sim, colony, TestMaps.IceCrater, ModuleType.IceMine);
            FinishConstruction(colony);
            sim.Execute(new AssignPioneer(1, p.Id, Assignment.ToModule(colony.Id, TestMaps.IceCrater)), out _);
            double field = PioneerSystem.MonthlyRisk(s, p);

            sim.Execute(new AssignPioneer(1, p.Id, Assignment.OnCouncil(CouncilSeat.ChiefEngineer)), out _);
            double council = PioneerSystem.MonthlyRisk(s, p);

            Assert.That(field, Is.GreaterThan(council * 10));
            p.Traits.Add(Trait.Meticulous);
            Assert.That(PioneerSystem.MonthlyRisk(s, p), Is.EqualTo(council * 0.5).Within(1e-12));
        }

        [Test]
        public void DeathsAreRareButHappenAndNearMissesComeFirst()
        {
            // Many independent careers: everyone works the ice mine for 10 years.
            int deaths = 0, nearMisses = 0, careers = 40;
            for (int seed = 0; seed < careers; seed++)
            {
                var sim = TestMaps.NewGame(seed);
                var colony = TestMaps.Found(sim);
                foreach (var p in sim.State.Pioneers) p.Traits.Clear();
                TestMaps.Build(sim, colony, TestMaps.IceCrater, ModuleType.IceMine);
                FinishConstruction(colony);
                var explorer = sim.State.Pioneers.Single(p => p.Expertise == Expertise.Engineer);
                sim.Execute(new AssignPioneer(1, explorer.Id, Assignment.ToModule(colony.Id, TestMaps.IceCrater)), out _);

                // Keep the base alive regardless of its economy; this test is about accidents.
                for (int day = 0; day < 3650 && explorer.IsAlive; day++)
                {
                    foreach (Resource r in new[] { Resource.Oxygen, Resource.Water, Resource.Food }) colony.Stock[r] = 1000;
                    sim.Tick();
                }
                if (!explorer.IsAlive) deaths++;
                nearMisses += sim.State.Log.Count(e => e.Text.Contains("near miss") && e.Text.Contains(explorer.Name));
            }

            // 0.4%/month for 120 months ≈ 38% die; near misses ≈ 4× as frequent per month.
            Assert.That(deaths, Is.InRange(5, 30), $"deaths {deaths}/{careers}");
            Assert.That(nearMisses, Is.GreaterThan(deaths));
        }

        [Test]
        public void ExperienceRaisesSkill()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var commander = PioneerSystem.AdministratorOf(sim.State, colony);
            commander.Traits.Clear();
            commander.Traits.Add(Trait.Meticulous); // keep the odds of an accident out of the way
            int skill = commander.Skill;

            for (int day = 0; day < PioneerSystem.DaysPerSkillLevel + 1; day++)
            {
                foreach (Resource r in new[] { Resource.Oxygen, Resource.Water, Resource.Food }) colony.Stock[r] = 1000;
                sim.Tick();
            }

            Assert.That(commander.IsAlive);
            Assert.That(commander.Skill, Is.EqualTo(skill + 1));
        }

        [Test]
        public void AdministratorsSpeedUpConstruction()
        {
            var sim = TestMaps.NewGame();
            var colony = TestMaps.Found(sim);
            var def = ModuleCatalog.Get(ModuleType.Greenhouse);
            int withAdmin = Modifiers.BuildDays(sim.State, colony, def);

            var commander = PioneerSystem.AdministratorOf(sim.State, colony);
            sim.Execute(new AssignPioneer(1, commander.Id, Assignment.None), out _);
            Assert.That(Modifiers.BuildDays(sim.State, colony, def), Is.EqualTo(def.BuildDays));
            Assert.That(withAdmin, Is.LessThan(def.BuildDays));
        }
    }
}
