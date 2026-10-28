using System.Linq;
using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>Shared bits for showing what a ship wants: a row of service icons coloured by progress.</summary>
    public static class ShipUi
    {
        /// <summary>One icon per errand: the need in amber, wishes muted; green once met, red if it failed.</summary>
        public static VisualElement ErrandIcons(Ship ship, Station s, UiRoot ui, System.Collections.Generic.List<System.Action> updaters)
        {
            var row = Ui.El("ws-row");
            foreach (var errand in ship.Errands)
            {
                var e = errand;
                var icon = Ui.Icon(Icons.For(e.Service)).AddTo(row);
                icon.style.marginRight = 5;
                icon.pickingMode = PickingMode.Position;
                ui.Tooltip(icon, () => $"{(e.IsNeed ? "Needs" : "Would like")}: {Services.Name(e.Service)}\n{Status(s, ship, e)}");
                updaters.Add(() => Ui.IconTone(icon, Tone(s, ship, e)));
            }
            return row;
        }

        public static string Tone(Station s, Ship ship, Errand e)
        {
            if (e.Outcome == ErrandOutcome.Met) return "good";
            if (e.Outcome == ErrandOutcome.Failed) return e.IsNeed ? "bad" : "muted";
            if (s.People.Any(p => p.Errand == e)) return "comms";
            return e.IsNeed ? "accent" : "muted";
        }

        public static string Status(Station s, Ship ship, Errand e)
        {
            if (e.Outcome == ErrandOutcome.Met) return "Done";
            if (e.Outcome == ErrandOutcome.Failed)
                return ship.Disembarked && !s.Buildings.Any(b => b.Def.Provides == e.Service) ? $"No {Services.Provider(e.Service).Name} on the station" : "Couldn't get it";
            var person = s.People.FirstOrDefault(p => p.Errand == e);
            if (person != null)
            {
                var b = s.GetBuilding(person.BuildingId);
                string where = b != null ? s.Label(b) : "?";
                switch (person.State)
                {
                    case PersonState.Visiting: return $"Inside the {where}";
                    case PersonState.Queueing: return $"Waiting outside the {where}";
                    case PersonState.Returning: return "Heading back";
                    default: return $"Walking to the {where}";
                }
            }
            bool offered = s.Buildings.Any(b => b.Def.Provides == e.Service && b.Operating);
            return offered ? "When they dock" : $"Needs a {Services.Provider(e.Service).Name}";
        }
    }
}
