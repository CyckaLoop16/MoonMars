using System;
using System.Collections.Generic;
using System.Linq;

namespace Waystation.Sim
{
    /// <summary>Something a ship's crew or passengers want from the station.</summary>
    public enum ServiceType
    {
        // Leisure
        Rest,
        Food,
        Sightseeing,
        // Commerce
        Trade,
        Shopping,
        Parts,
        // Business
        Military,
        Survey,
        Corporate,
    }

    /// <summary>Who's flying the ship, which decides what they need and wish for.</summary>
    public enum ShipRole
    {
        Traveller,
        Freighter,
        Merchant,
        Military,
        Explorer,
        Executive,
    }

    public sealed class ShipRoleDef
    {
        public ShipRole Role;
        public string Name;
        public string Description;
        public ShipClass Hull;
        /// <summary>What they came for. Not getting it sends them off disappointed.</summary>
        public ServiceType? Need;
        /// <summary>Nice to have: each one met is a bonus.</summary>
        public ServiceType[] Wishes = new ServiceType[0];
        public int MinWishes;
        public int MaxWishes;
        public int MinCrew = 1;
        public int MaxCrew = 2;
        /// <summary>Relative share of arrivals once this role comes to the station.</summary>
        public double Weight = 1;
        /// <summary>They only come once the station has one of these (connected and switched on).</summary>
        public BuildingType? AttractedBy;
    }

    public static class ShipRoles
    {
        static readonly Dictionary<ShipRole, ShipRoleDef> Defs = new Dictionary<ShipRole, ShipRoleDef>
        {
            [ShipRole.Traveller] = new ShipRoleDef
            {
                Role = ShipRole.Traveller, Name = "Traveller", Hull = ShipClass.Shuttle, Weight = 6,
                Description = "Drifters, pilgrims and contract workers between jobs. Want a meal, a bunk or a view.",
                Wishes = new[] { ServiceType.Rest, ServiceType.Food, ServiceType.Sightseeing, ServiceType.Shopping },
                MinWishes = 1, MaxWishes = 2, MinCrew = 2, MaxCrew = 4,
            },
            [ShipRole.Freighter] = new ShipRoleDef
            {
                Role = ShipRole.Freighter, Name = "Freighter", Hull = ShipClass.Hauler, Weight = 2,
                Description = "Independent haulers with cargo to sell. They need a trading outpost.",
                Need = ServiceType.Trade, Wishes = new[] { ServiceType.Food, ServiceType.Rest, ServiceType.Parts },
                MaxWishes = 1, MinCrew = 2, MaxCrew = 3,
            },
            [ShipRole.Merchant] = new ShipRoleDef
            {
                Role = ShipRole.Merchant, Name = "Merchant", Hull = ShipClass.Shuttle, Weight = 3, AttractedBy = BuildingType.TradingOutpost,
                Description = "Traders working the lanes. Come for the trading outpost, stay for the bazaar.",
                Need = ServiceType.Trade, Wishes = new[] { ServiceType.Shopping, ServiceType.Food },
                MaxWishes = 1, MinCrew = 1, MaxCrew = 2,
            },
            [ShipRole.Military] = new ShipRoleDef
            {
                Role = ShipRole.Military, Name = "USM Patrol", Hull = ShipClass.Shuttle, Weight = 2.5, AttractedBy = BuildingType.MilitaryBarracks,
                Description = "United Systems of Mankind patrol craft. Marines report to the barracks, then look for a hot meal.",
                Need = ServiceType.Military, Wishes = new[] { ServiceType.Food, ServiceType.Rest },
                MaxWishes = 1, MinCrew = 2, MaxCrew = 4,
            },
            [ShipRole.Explorer] = new ShipRoleDef
            {
                Role = ShipRole.Explorer, Name = "Guild Explorer", Hull = ShipClass.Shuttle, Weight = 2.5, AttractedBy = BuildingType.ExplorersGuild,
                Description = "Explorers' Guild surveyors back from the dark, with charts to file and gear to replace.",
                Need = ServiceType.Survey, Wishes = new[] { ServiceType.Parts, ServiceType.Shopping, ServiceType.Rest },
                MaxWishes = 2, MinCrew = 1, MaxCrew = 3,
            },
            [ShipRole.Executive] = new ShipRoleDef
            {
                Role = ShipRole.Executive, Name = "Executive", Hull = ShipClass.Shuttle, Weight = 2, AttractedBy = BuildingType.CorporateOffice,
                Description = "Corporate shuttles. Meetings at the corporate office, then the view and a decent dinner.",
                Need = ServiceType.Corporate, Wishes = new[] { ServiceType.Sightseeing, ServiceType.Food },
                MaxWishes = 1, MinCrew = 1, MaxCrew = 2,
            },
        };

        public static ShipRoleDef Get(ShipRole role) => Defs[role];
        public static IEnumerable<ShipRoleDef> All => Defs.Values;
    }

    public static class Services
    {
        public static string Name(ServiceType service)
        {
            switch (service)
            {
                case ServiceType.Rest: return "Rest";
                case ServiceType.Food: return "Food & drink";
                case ServiceType.Sightseeing: return "Sightseeing";
                case ServiceType.Trade: return "Trade cargo";
                case ServiceType.Shopping: return "Shopping";
                case ServiceType.Parts: return "Spare parts";
                case ServiceType.Military: return "Report to USM";
                case ServiceType.Survey: return "File survey";
                default: return "Business meeting";
            }
        }

        public static BuildCategory Category(ServiceType service)
        {
            switch (service)
            {
                case ServiceType.Rest:
                case ServiceType.Food:
                case ServiceType.Sightseeing:
                    return BuildCategory.Leisure;
                case ServiceType.Trade:
                case ServiceType.Shopping:
                case ServiceType.Parts:
                    return BuildCategory.Commerce;
                default:
                    return BuildCategory.Business;
            }
        }

        /// <summary>The building type that provides a service.</summary>
        public static BuildingDef Provider(ServiceType service) => BuildingCatalog.All.First(d => d.Provides == service);
    }

    public enum ErrandOutcome
    {
        Pending,
        Met,
        Failed,
    }

    /// <summary>One need or wish of a ship, done by one or more of its people.</summary>
    public sealed class Errand
    {
        public ServiceType Service;
        public bool IsNeed;
        public ErrandOutcome Outcome;
    }

    public enum PersonState
    {
        /// <summary>On the way to a service building.</summary>
        Walking,
        /// <summary>At the door, waiting for a free spot.</summary>
        Queueing,
        /// <summary>Inside a service building.</summary>
        Visiting,
        /// <summary>Walking back to the ship.</summary>
        Returning,
    }

    /// <summary>A crew member or passenger out on the station. Gone (back aboard) once they reach the ship.</summary>
    public sealed class Person
    {
        public int Id;
        public int ShipId;
        public ShipRole Role;
        public Errand Errand;
        public int BuildingId;
        public PersonState State;
        /// <summary>Cells to walk, from where they started to where they're going.</summary>
        public List<Cell> Path = new List<Cell>();
        public int PathIndex;
        /// <summary>0..1 between Path[PathIndex] and Path[PathIndex + 1].</summary>
        public double Progress;
        public int PathRevision;
        /// <summary>Ticks left visiting, or patience left queueing.</summary>
        public int Ticks;
        /// <summary>The cell by the ship's ramp, where they go back to.</summary>
        public Cell Home;
        /// <summary>Position within their group (for spacing them out).</summary>
        public int Slot;

        public Cell Cell => Path.Count == 0 ? Home : Path[Math.Min(PathIndex, Path.Count - 1)];

        /// <summary>Where they are, in cell units (cell centres at .5).</summary>
        public (double x, double y) Position
        {
            get
            {
                if (Path.Count == 0) return (Home.X + 0.5, Home.Y + 0.5);
                var a = Path[Math.Min(PathIndex, Path.Count - 1)];
                var b = Path[Math.Min(PathIndex + 1, Path.Count - 1)];
                return (a.X + 0.5 + (b.X - a.X) * Progress, a.Y + 0.5 + (b.Y - a.Y) * Progress);
            }
        }

        /// <summary>The step they're taking, for facing (zero when standing).</summary>
        public (int dx, int dy) Heading
        {
            get
            {
                if (PathIndex + 1 >= Path.Count || State == PersonState.Visiting || State == PersonState.Queueing) return (0, 0);
                var a = Path[PathIndex];
                var b = Path[PathIndex + 1];
                return (b.X - a.X, b.Y - a.Y);
            }
        }
    }

    /// <summary>
    /// People on the station: disembarking, walking the walkways and foundations to service buildings,
    /// queueing, visiting and paying, then walking back. A ship can't leave until all its people are aboard.
    /// </summary>
    public static class Crew
    {
        /// <summary>Walking speed, cells (2 m) per game second: a brisk 5 m/s, since game time runs fast.</summary>
        public const double WalkSpeed = 2.5;
        /// <summary>How long someone waits at a full building before giving up.</summary>
        public const double QueueSeconds = 20;
        /// <summary>Extra "distance" for a full building when choosing where to go.</summary>
        const int FullPenalty = 30;

        /// <summary>Where people get off: the middle of the pad or hangar.</summary>
        public static Cell HomeCell(Building pad) => new Cell(pad.Origin.X + (pad.Def.Width - 1) / 2, pad.Origin.Y + (pad.Def.Height - 1) / 2);

        public static IEnumerable<Person> Of(Station s, Ship ship) => s.People.Where(p => p.ShipId == ship.Id);
        public static bool AllAboard(Station s, Ship ship) => s.People.All(p => p.ShipId != ship.Id);

        /// <summary>Everyone back aboard at once (the ship is leaving in a hurry).</summary>
        public static void Recall(Station s, Ship ship) => s.People.RemoveAll(p => p.ShipId == ship.Id);

        public static int Visitors(Station s, Building b) => s.People.Count(p => p.BuildingId == b.Id && p.State == PersonState.Visiting);
        public static int Heading(Station s, Building b) => s.People.Count(p => p.BuildingId == b.Id && p.State != PersonState.Returning);

        /// <summary>
        /// The ship has docked: work out where each errand can be done and send people. Errands with
        /// nowhere to go fail straight away. Spare crew tag along with the first errands.
        /// </summary>
        public static void Disembark(Station s, Ship ship, Building pad)
        {
            ship.Disembarked = true;
            if (pad == null) return;
            var home = HomeCell(pad);

            var plans = new List<(Errand errand, Building building, List<Cell> path)>();
            var planned = new Dictionary<int, int>();
            foreach (var errand in ship.Errands)
            {
                var (building, path) = FindProvider(s, home, errand.Service, null, planned);
                if (building == null)
                {
                    errand.Outcome = ErrandOutcome.Failed;
                    continue;
                }
                plans.Add((errand, building, path));
                planned[building.Id] = planned.TryGetValue(building.Id, out var n) ? n + 1 : 1;
            }
            if (plans.Count == 0) return;

            int people = Math.Max(ship.Crew, plans.Count);
            var slots = new int[plans.Count];
            for (int i = 0; i < people; i++)
            {
                int k = i % plans.Count;
                var (errand, building, path) = plans[k];
                s.People.Add(new Person
                {
                    Id = s.NextPersonId++,
                    ShipId = ship.Id,
                    Role = ship.Role,
                    Errand = errand,
                    BuildingId = building.Id,
                    State = PersonState.Walking,
                    Path = new List<Cell>(path),
                    PathRevision = s.Revision,
                    Home = home,
                    Slot = slots[k]++,
                });
            }
        }

        /// <summary>One tick for everyone out on the station.</summary>
        public static void Step(Station s)
        {
            if (s.People.Count == 0) return;
            double stride = WalkSpeed / Station.TicksPerSecond;

            foreach (var p in s.People.ToList())
            {
                var building = s.GetBuilding(p.BuildingId);
                switch (p.State)
                {
                    case PersonState.Walking:
                    case PersonState.Returning:
                        if (p.PathRevision != s.Revision && !Repath(s, p, building)) break;
                        p.Progress += stride;
                        while (p.Progress >= 1 && p.PathIndex < p.Path.Count - 1)
                        {
                            p.Progress -= 1;
                            p.PathIndex++;
                        }
                        if (p.PathIndex >= p.Path.Count - 1)
                        {
                            p.Progress = 0;
                            if (p.State == PersonState.Returning) s.People.Remove(p);
                            else Arrive(s, p, building);
                        }
                        break;

                    case PersonState.Queueing:
                        if (!Open(s, building)) GoHome(s, p);
                        else if (Visitors(s, building) < building.Def.Capacity) StartVisit(p, building);
                        else if (--p.Ticks <= 0) GoHome(s, p);
                        break;

                    case PersonState.Visiting:
                        if (building == null)
                        {
                            GoHome(s, p);
                            break;
                        }
                        if (s.PowerCredit >= 1) p.Ticks--; // services slow down in a brownout
                        if (p.Ticks > 0) break;
                        Pay(s, p, building);
                        GoHome(s, p);
                        break;
                }
            }
        }

        static void Arrive(Station s, Person p, Building building)
        {
            if (!Open(s, building))
            {
                GoHome(s, p);
                return;
            }
            if (Visitors(s, building) < building.Def.Capacity) StartVisit(p, building);
            else
            {
                p.State = PersonState.Queueing;
                p.Ticks = Rules.Ticks(QueueSeconds);
            }
        }

        static void StartVisit(Person p, Building building)
        {
            p.State = PersonState.Visiting;
            p.Ticks = Rules.Ticks(building.Def.VisitSeconds);
        }

        static void Pay(Station s, Person p, Building building)
        {
            var def = building.Def;
            s.Earn(def.VisitFee, Ledger.LineFor(def.Category), building);
            building.Visits++;
            p.Errand.Outcome = ErrandOutcome.Met;
            s.Report($"+{def.VisitFee:0} cr at {s.Label(building)}.", EventKind.Revenue, building.Id, def.VisitFee);
        }

        /// <summary>Head back to the ship (or straight aboard if there's no way back on foot).</summary>
        static void GoHome(Station s, Person p)
        {
            p.State = PersonState.Returning;
            var path = FindPath(s, p.Cell, c => c == p.Home);
            if (path == null)
            {
                s.People.Remove(p);
                return;
            }
            p.Path = path;
            p.PathIndex = 0;
            p.Progress = 0;
            p.PathRevision = s.Revision;
        }

        /// <summary>The station changed under their feet: find a new way, or give up on the errand.</summary>
        static bool Repath(Station s, Person p, Building building)
        {
            var from = p.Cell;
            p.PathRevision = s.Revision;
            p.PathIndex = 0;
            p.Progress = 0;
            if (p.State == PersonState.Returning)
            {
                var back = FindPath(s, from, c => c == p.Home);
                if (back == null)
                {
                    s.People.Remove(p);
                    return false;
                }
                p.Path = back;
                return true;
            }

            var (target, path) = building == null ? (null, null) : FindProvider(s, from, p.Errand.Service, building, null);
            if (target == null)
            {
                GoHome(s, p);
                return false;
            }
            p.Path = path;
            return true;
        }

        static bool Open(Station s, Building b) => b != null && b.Operating && s.SystemsOnline;

        // ------------------------------------------------------------------ path finding

        /// <summary>
        /// The best open building for a service, reachable on foot from <paramref name="from"/>, and the
        /// path to a walkable cell next to it. Nearer is better; full buildings count as further away.
        /// </summary>
        public static (Building building, List<Cell> path) FindProvider(Station s, Cell from, ServiceType service, Building only, Dictionary<int, int> planned)
        {
            var providers = new Dictionary<Cell, Building>();
            foreach (var b in s.Buildings)
            {
                if (b.Def.Provides != service || !Open(s, b)) continue;
                if (only != null && b != only) continue;
                foreach (var c in b.Footprint()) providers[c] = b;
            }
            if (providers.Count == 0) return (null, null);

            var parents = new Dictionary<Cell, Cell>();
            var distance = new Dictionary<Cell, int> { [from] = 0 };
            var queue = new Queue<Cell>();
            queue.Enqueue(from);
            Building best = null;
            Cell bestDoor = default;
            int bestScore = int.MaxValue;
            var seen = new HashSet<int>();

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int d = distance[c];
                if (d >= bestScore) break;
                foreach (var n in c.Neighbors())
                {
                    if (providers.TryGetValue(n, out var b) && seen.Add(b.Id))
                    {
                        int load = Heading(s, b) + (planned != null && planned.TryGetValue(b.Id, out var extra) ? extra : 0);
                        int score = d + (load >= b.Def.Capacity ? FullPenalty : 0);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = b;
                            bestDoor = c;
                        }
                    }
                    if (distance.ContainsKey(n) || !s.IsWalkable(n)) continue;
                    distance[n] = d + 1;
                    parents[n] = c;
                    queue.Enqueue(n);
                }
            }
            return best == null ? (null, null) : (best, Trace(parents, from, bestDoor));
        }

        /// <summary>Shortest walk from one cell to the first cell that matches, or null.</summary>
        public static List<Cell> FindPath(Station s, Cell from, Func<Cell, bool> goal)
        {
            if (goal(from)) return new List<Cell> { from };
            var parents = new Dictionary<Cell, Cell>();
            var queue = new Queue<Cell>();
            var seen = new HashSet<Cell> { from };
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var n in c.Neighbors())
                {
                    if (!seen.Add(n) || !s.IsWalkable(n)) continue;
                    parents[n] = c;
                    if (goal(n)) return Trace(parents, from, n);
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        static List<Cell> Trace(Dictionary<Cell, Cell> parents, Cell from, Cell to)
        {
            var path = new List<Cell> { to };
            while (path[path.Count - 1] != from) path.Add(parents[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }
    }
}
