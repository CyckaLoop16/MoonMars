using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Bottom of the screen: the hint line, category tabs, tool buttons and a card per buildable
    /// module in the active tab. New categories in <see cref="BuildCategory"/> get a tab automatically.
    /// </summary>
    public sealed class BuildBar
    {
        static readonly Dictionary<BuildingType, string> Hotkeys = new Dictionary<BuildingType, string>
        {
            { BuildingType.LandingPad, "P" },
            { BuildingType.Hangar, "H" },
            { BuildingType.Walkway, "C" },
            { BuildingType.Foundation, "F" },
        };

        readonly GameController _game;
        readonly UiRoot _ui;
        readonly Label _hint;
        readonly VisualElement _cards;
        readonly Button _select, _demolish;
        readonly Dictionary<BuildCategory, Button> _tabs = new Dictionary<BuildCategory, Button>();
        readonly List<(BuildingDef def, VisualElement card)> _cardList = new List<(BuildingDef, VisualElement)>();
        BuildCategory _category = BuildCategory.Landing;

        public VisualElement Root { get; }

        public BuildBar(GameController game, UiRoot ui)
        {
            _game = game;
            _ui = ui;
            Root = Ui.El("ws-bottom");
            Root.pickingMode = PickingMode.Ignore;

            _hint = Ui.Text("", "ws-hint").AddTo(Root);
            _hint.pickingMode = PickingMode.Ignore;

            var panel = Ui.El("ws-panel", "ws-buildbar").AddTo(Root);
            var tabs = Ui.El("ws-tabs").AddTo(panel);
            _select = Ui.Button("SELECT", () => _game.SetTool(Tool.Select)).AddTo(tabs);
            _demolish = Ui.Button("DEMOLISH  X", () => _game.SetTool(Tool.Demolish), "ws-button--danger").AddTo(tabs);
            ui.Tooltip(_demolish, () => $"Tear down a module for a {Rules.DemolishRefund:P0} refund.");
            Ui.El("ws-divider").AddTo(tabs);
            foreach (BuildCategory category in Enum.GetValues(typeof(BuildCategory)))
            {
                var c = category;
                _tabs[c] = Ui.Button(c.ToString().ToUpperInvariant(), () => ShowCategory(c)).AddTo(tabs);
            }

            _cards = Ui.El("ws-cards").AddTo(panel);
            ShowCategory(_category);
        }

        void ShowCategory(BuildCategory category)
        {
            _category = category;
            _cards.Clear();
            _cardList.Clear();
            foreach (var def in BuildingCatalog.All.Where(d => d.Buildable && d.Category == category))
                _cardList.Add((def, Card(def).AddTo(_cards)));
            foreach (var pair in _tabs) pair.Value.EnableInClassList("ws-button--on", pair.Key == category);
        }

        VisualElement Card(BuildingDef def)
        {
            var card = Ui.El("ws-card");
            Ui.Text(def.Name, "ws-card-name").AddTo(card);
            Ui.Text($"{def.Width}×{def.Height} · {def.UpkeepPerMinute:0.#} cr/min", "ws-card-meta").AddTo(card);
            string power = PowerLine(def);
            if (power != null) Ui.Text(power, "ws-card-meta", def.PowerUse > 0 ? "ws-warn" : "ws-good").AddTo(card);
            Ui.El("ws-spacer").AddTo(card);
            Ui.Text(Ui.Money(def.Cost), "ws-card-cost").AddTo(card);
            if (Hotkeys.TryGetValue(def.Type, out var key)) Ui.Text(key, "ws-key").AddTo(card);
            foreach (var child in card.Children()) child.pickingMode = PickingMode.Ignore;

            card.RegisterCallback<ClickEvent>(_ => _game.StartBuilding(def.Type));
            _ui.Tooltip(card, () => Describe(def));
            return card;
        }

        static string PowerLine(BuildingDef def)
        {
            if (def.PowerUse > 0) return $"uses {def.PowerUse:0.#} kW";
            if (def.SolarOutput > 0) return $"+{def.SolarOutput:0} kW in sun";
            if (def.GeneratorOutput > 0) return $"+{def.GeneratorOutput:0} kW backup";
            if (def.BatteryCapacity > 0) return $"stores {def.BatteryCapacity / 1000:0.#} MJ";
            if (def.FuelCapacity > 0) return $"+{def.FuelCapacity:0} fuel";
            return null;
        }

        static string Describe(BuildingDef def)
        {
            string text = def.Description;
            if (def.RequiresFoundation) text += "\nBuilt on foundations.";
            if (def.Serves.Length > 0)
                text += "\nTakes: " + string.Join(", ", def.Serves.Select(c => ShipCatalog.Get(c).Name.ToLowerInvariant() + "s"));
            if (def.Repairs) text += "\nRepairs damaged ships.";
            if (def.IsPaintable) text += "\nDrag to paint several.";
            return text;
        }

        public void Update()
        {
            foreach (var (def, card) in _cardList)
            {
                card.EnableInClassList("ws-card--on", _game.Tool == Tool.Build && _game.BuildType == def.Type);
                card.EnableInClassList("ws-card--poor", _game.Station.Money < def.Cost);
            }
            _select.EnableInClassList("ws-button--on", _game.Tool == Tool.Select);
            _demolish.EnableInClassList("ws-button--on", _game.Tool == Tool.Demolish);

            string hint;
            string tone = null;
            if (Time.unscaledTime - _game.LastErrorTime < 3f)
            {
                hint = _game.LastError;
                tone = "ws-bad";
            }
            else if (_game.PlacementError != null)
            {
                hint = _game.PlacementError;
                tone = "ws-bad";
            }
            else
            {
                switch (_game.Tool)
                {
                    case Tool.Build:
                        var def = BuildingCatalog.Get(_game.BuildType);
                        hint = $"Placing {def.Name} ({Ui.Money(def.Cost)}){(def.IsPaintable ? " · drag to paint" : "")}{(def.RequiresFoundation ? $" · on a {def.Width}×{def.Height} patch of foundations" : "")} · Esc to stop";
                        break;
                    case Tool.Demolish:
                        hint = $"Click a module to demolish it ({Rules.DemolishRefund:P0} refund) · Esc to stop";
                        tone = "ws-warn";
                        break;
                    case Tool.ClearToLand:
                        hint = $"Clearing {_game.ClearingShip?.Name}: click a free pad{(_game.ClearingShip?.Class == ShipClass.Hauler ? " (haulers need a hangar)" : "")} · Esc to cancel";
                        tone = "ws-comms";
                        break;
                    default:
                        hint = _game.Station.Hailing.Any() ? "Ships are hailing: clear them in the Comms panel, or press T" : "";
                        tone = "ws-comms";
                        break;
                }
            }
            Ui.Set(_hint, hint ?? "");
            Ui.Tone(_hint, tone);
        }
    }
}
