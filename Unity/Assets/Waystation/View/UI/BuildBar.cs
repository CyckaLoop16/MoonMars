using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// The build dock at the bottom: a hint pill, tool buttons, a tab per <see cref="BuildCategory"/>
    /// (new categories get a tab automatically) and a tile per buildable type with a rendered thumbnail.
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
        readonly List<(BuildingDef def, VisualElement tile)> _tiles = new List<(BuildingDef, VisualElement)>();
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

            var dock = Ui.El("ws-panel", "ws-dock").AddTo(Root);
            var tabs = Ui.El("ws-tabs").AddTo(dock);
            _select = Ui.IconButton("select", () => _game.SetTool(Tool.Select)).AddTo(tabs);
            ui.Tooltip(_select, () => "Select (Esc)");
            _demolish = Ui.IconButton("demolish", () => _game.SetTool(Tool.Demolish), "ws-button--danger").AddTo(tabs);
            ui.Tooltip(_demolish, () => $"Demolish (X): {Rules.DemolishRefund:P0} refund");
            Ui.El("ws-divider").AddTo(tabs);
            foreach (BuildCategory category in Enum.GetValues(typeof(BuildCategory)))
            {
                var c = category;
                _tabs[c] = Ui.IconTextButton(Icons.For(c), c.ToString(), () => ShowCategory(c)).AddTo(tabs);
            }

            _cards = Ui.El("ws-cards").AddTo(dock);
            ShowCategory(_category);
        }

        void ShowCategory(BuildCategory category)
        {
            _category = category;
            _cards.Clear();
            _tiles.Clear();
            foreach (var def in BuildingCatalog.All.Where(d => d.Buildable && d.Category == category))
                _tiles.Add((def, Tile(def).AddTo(_cards)));
            foreach (var pair in _tabs) pair.Value.EnableInClassList("ws-button--on", pair.Key == category);
        }

        VisualElement Tile(BuildingDef def)
        {
            var tile = Ui.El("ws-tile");
            var image = Ui.El("ws-tile-image").AddTo(tile);
            var thumbnail = Thumbnails.Get(_game.Buildings, def.Type);
            if (thumbnail != null)
            {
                image.style.backgroundImage = new StyleBackground(thumbnail);
            }
            else
            {
                image.AddToClassList("ws-tile-image--icon");
                Ui.SetIcon(image, def.Provides is ServiceType service ? Icons.For(service) : Icons.For(def.Category));
            }
            Ui.Text(def.Name, "ws-tile-name").AddTo(tile);
            Ui.Text($"{def.Cost:N0}", "ws-tile-cost").AddTo(tile);
            if (Hotkeys.TryGetValue(def.Type, out var key)) Ui.Text(key, "ws-tile-key").AddTo(tile);
            string power = ShortPower(def);
            if (power != null) Ui.Text(power, "ws-tile-power", def.PowerUse > 0 ? "ws-warn" : "ws-good").AddTo(tile);
            foreach (var child in tile.Children()) child.pickingMode = PickingMode.Ignore;

            tile.RegisterCallback<ClickEvent>(_ => _game.StartBuilding(def.Type));
            _ui.Tooltip(tile, () => Describe(def));
            return tile;
        }

        static string ShortPower(BuildingDef def)
        {
            if (def.PowerUse > 0) return $"−{def.PowerUse:0.#}kW";
            if (def.SolarOutput > 0 || def.GeneratorOutput > 0) return $"+{def.SolarOutput + def.GeneratorOutput:0}kW";
            return null;
        }

        static string Describe(BuildingDef def)
        {
            var lines = new List<string> { $"{def.Name} · {def.Width}×{def.Height}", def.Description };
            var stats = new List<string>();
            if (def.PowerUse > 0) stats.Add($"uses {def.PowerUse:0.#} kW");
            if (def.SolarOutput > 0) stats.Add($"+{def.SolarOutput:0} kW in sunlight");
            if (def.GeneratorOutput > 0) stats.Add($"+{def.GeneratorOutput:0} kW backup");
            if (def.BatteryCapacity > 0) stats.Add($"stores {def.BatteryCapacity / 1000:0.#} MJ");
            if (def.FuelCapacity > 0) stats.Add($"+{def.FuelCapacity:0} fuel");
            if (def.Provides is ServiceType service) stats.Add($"{Services.Name(service)} · {def.Capacity} at a time · {def.VisitFee:0} cr a visit");
            stats.Add($"upkeep {def.UpkeepPerMinute:0.#}/min");
            lines.Add(string.Join(" · ", stats));
            var brings = ShipRoles.All.Where(r => r.AttractedBy == def.Type).Select(r => r.Name).ToList();
            if (brings.Count > 0) lines.Add("Brings " + string.Join(", ", brings) + " ships.");
            if (def.RequiresFoundation) lines.Add("Place on foundations.");
            return string.Join("\n", lines);
        }

        public void Update()
        {
            foreach (var (def, tile) in _tiles)
            {
                tile.EnableInClassList("ws-tile--on", _game.Tool == Tool.Build && _game.BuildType == def.Type);
                tile.EnableInClassList("ws-tile--poor", _game.Station.Money < def.Cost);
            }
            _select.EnableInClassList("ws-button--on", _game.Tool == Tool.Select);
            _demolish.EnableInClassList("ws-button--on", _game.Tool == Tool.Demolish);

            string hint = null;
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
                        hint = def.IsPaintable ? $"{def.Name}: click or drag" : def.RequiresFoundation ? $"{def.Name}: place on foundations" : $"{def.Name}: click to place";
                        break;
                    case Tool.Demolish:
                        hint = "Demolish: click a building";
                        tone = "ws-warn";
                        break;
                    case Tool.ClearToLand:
                        hint = $"Pick a free {(_game.ClearingShip?.Class == ShipClass.Hauler ? "hangar" : "pad")} for {_game.ClearingShip?.Name}";
                        tone = "ws-comms";
                        break;
                }
            }
            Ui.Show(_hint, !string.IsNullOrEmpty(hint));
            Ui.Set(_hint, hint ?? "");
            Ui.Tone(_hint, tone);
        }
    }
}
