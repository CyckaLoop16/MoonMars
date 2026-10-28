using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Waystation.View
{
    public enum UiWindow
    {
        None,
        Log,
        Ledger,
    }

    /// <summary>
    /// The game's UI (UI Toolkit). Builds one full-screen document in code, styled by
    /// Resources/UI/Waystation.uss (through the WaystationTheme theme), and owns the widgets:
    /// top bar, build bar, comms panel, inspector, toasts, world labels, windows, pause menu,
    /// tooltips and a scanline overlay. New screens are new widget classes added here.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        GameController _game;
        UIDocument _document;
        VisualElement _root;

        TopBar _topBar;
        BuildBar _buildBar;
        CommsPanel _comms;
        Inspector _inspector;
        ToastFeed _toasts;
        WorldLabels _worldLabels;
        LogWindow _logWindow;
        LedgerWindow _ledgerWindow;
        PauseMenu _pauseMenu;
        VisualElement _windowLayer;
        VisualElement _tooltip;
        Label _tooltipText;
        Func<string> _tooltipSource;
        VisualElement _scanlines;
        int _scanlineHeight;

        public UiWindow OpenWindow { get; private set; }

        public void Init(GameController game)
        {
            _game = game;

            var host = new GameObject("UI");
            host.SetActive(false); // configure before the document enables
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var theme = Resources.Load<ThemeStyleSheet>("UI/WaystationTheme");
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            _document = host.AddComponent<UIDocument>();
            _document.panelSettings = settings;
            host.SetActive(true);

            _root = _document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            if (theme == null)
            {
                Debug.LogWarning("Waystation: UI theme not found; falling back to the bare stylesheet.");
                var sheet = Resources.Load<StyleSheet>("UI/Waystation");
                if (sheet != null) _root.styleSheets.Add(sheet);
            }

            // Layers, bottom to top.
            _worldLabels = new WorldLabels(game);
            _root.Add(_worldLabels.Root);

            _topBar = new TopBar(game, this);
            _comms = new CommsPanel(game, this);
            _inspector = new Inspector(game, this);
            _buildBar = new BuildBar(game, this);
            _root.Add(_topBar.Root);
            _root.Add(_comms.Root);
            _root.Add(_inspector.Root);
            _root.Add(_buildBar.Root);

            _toasts = new ToastFeed(game, _worldLabels);
            _root.Add(_toasts.Root);

            _windowLayer = Ui.El("ws-window-layer").AddTo(_root);
            _windowLayer.pickingMode = PickingMode.Ignore;
            _logWindow = new LogWindow(game, this);
            _ledgerWindow = new LedgerWindow(game, this);
            _windowLayer.Add(_logWindow.Root);
            _windowLayer.Add(_ledgerWindow.Root);
            ShowWindow(UiWindow.None);

            _pauseMenu = new PauseMenu(game);
            _root.Add(_pauseMenu.Root);

            _tooltip = Ui.El("ws-tooltip").AddTo(_root);
            _tooltipText = Ui.Text("", "ws-small").AddTo(_tooltip);
            Ui.IgnorePointer(_tooltip);
            Ui.Show(_tooltip, false);

            _scanlines = Ui.El("ws-layer").AddTo(_root);
            _scanlines.pickingMode = PickingMode.Ignore;
            _scanlines.style.opacity = 0.16f;
        }

        public void OnNewGame()
        {
            _toasts?.Reset();
            _worldLabels?.Reset();
            _logWindow?.Reset();
            ShowWindow(UiWindow.None);
        }

        void Update()
        {
            if (_game == null || _game.Sim == null || _root == null) return;

            _worldLabels.Update();
            _topBar.Update();
            _buildBar.Update();
            _comms.Update();
            _inspector.Update();
            _toasts.Update();
            _pauseMenu.Update();
            if (OpenWindow == UiWindow.Log) _logWindow.Update();
            if (OpenWindow == UiWindow.Ledger) _ledgerWindow.Update();
            if (_tooltipSource != null) Ui.Set(_tooltipText, _tooltipSource());
            UpdateScanlines();
        }

        // ------------------------------------------------------------------ windows

        public void ToggleWindow(UiWindow window) => ShowWindow(OpenWindow == window ? UiWindow.None : window);

        /// <summary>Closes the open window; returns false if none was open.</summary>
        public bool CloseWindow()
        {
            if (OpenWindow == UiWindow.None) return false;
            ShowWindow(UiWindow.None);
            return true;
        }

        void ShowWindow(UiWindow window)
        {
            OpenWindow = window;
            Ui.Show(_logWindow.Root, window == UiWindow.Log);
            Ui.Show(_ledgerWindow.Root, window == UiWindow.Ledger);
            if (window == UiWindow.Log) _logWindow.Update();
            if (window == UiWindow.Ledger) _ledgerWindow.Update();
        }

        // ------------------------------------------------------------------ pointer

        /// <summary>Is this screen position (bottom-left origin, as input reports it) over any interactive UI?</summary>
        public bool IsPointerOver(Vector2 screenPosition)
        {
            var panel = _root?.panel;
            if (panel == null) return false;
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picked = panel.Pick(panelPosition);
            if (picked == null || picked == _root) return false;
            // Only count elements inside our document, never the panel's own root.
            for (var e = picked.parent; e != null; e = e.parent)
                if (e == _root) return true;
            return false;
        }

        public Vector2 WorldToPanel(Vector3 world) => RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, world, _game.Camera);

        // ------------------------------------------------------------------ tooltips

        /// <summary>Show <paramref name="text"/> in a tooltip while the pointer is over <paramref name="target"/>.</summary>
        public void Tooltip(VisualElement target, Func<string> text)
        {
            target.RegisterCallback<PointerEnterEvent>(e =>
            {
                _tooltipSource = text;
                Ui.Set(_tooltipText, text());
                Ui.Show(_tooltip, true);
                PlaceTooltip(e.position);
            });
            target.RegisterCallback<PointerMoveEvent>(e => PlaceTooltip(e.position));
            target.RegisterCallback<PointerLeaveEvent>(e =>
            {
                _tooltipSource = null;
                Ui.Show(_tooltip, false);
            });
        }

        void PlaceTooltip(Vector3 panelPosition)
        {
            var size = _root.layout.size;
            float x = Mathf.Min(panelPosition.x + 16f, size.x - 290f);
            float y = panelPosition.y + 18f;
            if (y > size.y - 90f) y = panelPosition.y - 80f;
            _tooltip.style.left = x;
            _tooltip.style.top = y;
        }

        // ------------------------------------------------------------------ CRT scanlines

        /// <summary>A one-pixel-wide texture of alternating dark rows, stretched over the screen.</summary>
        void UpdateScanlines()
        {
            int height = Mathf.Max(2, Screen.height / 3);
            if (height == _scanlineHeight) return;
            _scanlineHeight = height;

            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[height];
            for (int y = 0; y < height; y++) pixels[y] = y % 2 == 0 ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
            tex.SetPixels32(pixels);
            tex.Apply();
            _scanlines.style.backgroundImage = new StyleBackground(tex);
        }
    }
}
