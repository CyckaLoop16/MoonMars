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
            Ui.Heading("STATION SUSPENDED").AddTo(menu);
            Ui.Text("The void waits. So do your pilots.", "ws-small", "ws-muted").AddTo(menu).style.marginBottom = 8;
            Ui.Button("RESUME", () => _game.SetMenuOpen(false)).AddTo(menu);
            Ui.Button("NEW STATION", () => _game.NewGame(System.Environment.TickCount)).AddTo(menu);
            Ui.Button("QUIT", Quit, "ws-button--danger").AddTo(menu);
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
