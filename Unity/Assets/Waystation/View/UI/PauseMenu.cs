using UnityEngine;
using UnityEngine.UIElements;

namespace Waystation.View
{
    /// <summary>Esc menu: pauses the station. Settings, saves and loads will live here later.</summary>
    public sealed class PauseMenu
    {
        readonly GameController _game;

        public VisualElement Root { get; }

        public PauseMenu(GameController game)
        {
            _game = game;
            Root = Ui.El("ws-menu-layer");
            var menu = Ui.El("ws-panel", "ws-menu").AddTo(Root);
            Ui.Heading("Paused").AddTo(menu).style.marginBottom = 10;
            Ui.Button("Resume", () => _game.SetMenuOpen(false)).AddTo(menu);
            Ui.Button("New station", () => _game.NewGame(System.Environment.TickCount)).AddTo(menu);
            Ui.Button("Cheat: +10,000 cr (F9)", () => _game.Cheat(10000)).AddTo(menu);
            Ui.Button("Quit", Quit, "ws-button--danger").AddTo(menu);
            Ui.Show(Root, false);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Update() => Ui.Show(Root, _game.MenuOpen);
    }
}
