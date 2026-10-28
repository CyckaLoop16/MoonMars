using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Waystation.View
{
    /// <summary>Small helpers for building UI Toolkit trees in code with the Waystation classes.</summary>
    public static class Ui
    {
        public static VisualElement El(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            l.AddToClassList("ws-text");
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static Label Heading(string text) => Text(text, "ws-heading");

        public static Button Button(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("ws-button");
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        /// <summary>A tinted icon (see <see cref="Icons"/>).</summary>
        public static VisualElement Icon(string name, params string[] classes)
        {
            var e = El("ws-icon");
            foreach (var c in classes) e.AddToClassList(c);
            SetIcon(e, name);
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        public static void SetIcon(VisualElement e, string name) =>
            e.style.backgroundImage = new StyleBackground(Icons.Get(name));

        /// <summary>Swap an icon between the meaning tints (good / warn / bad / comms / muted / accent).</summary>
        public static void IconTone(VisualElement e, string tone)
        {
            foreach (var t in new[] { "good", "warn", "bad", "comms", "muted", "accent" })
                e.EnableInClassList("ws-icon--" + t, t == tone);
        }

        /// <summary>A square button showing just an icon.</summary>
        public static Button IconButton(string icon, Action onClick, params string[] classes)
        {
            var b = Button("", onClick, classes);
            b.AddToClassList("ws-icon-button");
            Icon(icon, "ws-icon--big").AddTo(b);
            return b;
        }

        /// <summary>A button with an icon and a label.</summary>
        public static Button IconTextButton(string icon, string text, Action onClick, params string[] classes)
        {
            var b = Button("", onClick, classes);
            b.AddToClassList("ws-tab");
            Icon(icon).AddTo(b);
            Text(text, "ws-small").AddTo(b).pickingMode = PickingMode.Ignore;
            return b;
        }

        public static T AddTo<T>(this T child, VisualElement parent) where T : VisualElement
        {
            parent.Add(child);
            return child;
        }

        public static void Show(VisualElement e, bool visible) =>
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Make an element and everything inside it click-through.</summary>
        public static void IgnorePointer(VisualElement e)
        {
            e.pickingMode = PickingMode.Ignore;
            foreach (var child in e.Children()) IgnorePointer(child);
        }

        /// <summary>Set text only when it changed (keeps layout work down when called every frame).</summary>
        public static void Set(Label label, string text)
        {
            if (label.text != text) label.text = text;
        }

        /// <summary>Swap a label between the meaning colours (good / warn / bad / comms / muted).</summary>
        public static void Tone(VisualElement e, string tone)
        {
            foreach (var t in new[] { "ws-good", "ws-warn", "ws-bad", "ws-comms", "ws-muted" })
                e.EnableInClassList(t, t == tone);
        }

        public static string Money(double amount) => $"{amount:N0} cr";

        public static string Signed(double amount) => amount >= 0 ? $"+{amount:N0}" : $"−{-amount:N0}";

        public static string Clock(double seconds)
        {
            int s = Mathf.Max(0, (int)seconds);
            return $"{s / 3600:0}:{s / 60 % 60:00}:{s % 60:00}";
        }
    }

    /// <summary>A thin horizontal progress bar.</summary>
    public sealed class Bar : VisualElement
    {
        readonly VisualElement _fill;

        public Bar(params string[] classes)
        {
            AddToClassList("ws-bar");
            foreach (var c in classes) AddToClassList(c);
            _fill = new VisualElement();
            _fill.AddToClassList("ws-bar__fill");
            Add(_fill);
        }

        public void Set(double fraction, Color color)
        {
            _fill.style.width = new Length(Mathf.Clamp01((float)fraction) * 100f, LengthUnit.Percent);
            _fill.style.backgroundColor = color;
        }

        public static readonly Color Good = new Color(0.43f, 1f, 0.6f);
        public static readonly Color Warn = new Color(1f, 0.69f, 0.13f);
        public static readonly Color Bad = new Color(1f, 0.29f, 0.35f);
        public static readonly Color Comms = new Color(0.31f, 0.89f, 1f);

        /// <summary>Red when low, amber in the middle, green when healthy.</summary>
        public static Color Health(double fraction) => fraction < 0.33 ? Bad : fraction < 0.66 ? Warn : Good;
    }
}
