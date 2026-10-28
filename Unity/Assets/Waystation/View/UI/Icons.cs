using System.Collections.Generic;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// HUD icons. Each one is drawn in code from simple shapes (white on transparent, tinted by USS),
    /// unless a texture exists at Resources/UI/Icons/&lt;name&gt;.png, which replaces it: drop-in slots
    /// for proper icon art.
    /// </summary>
    public static class Icons
    {
        const int Size = 64;
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string name)
        {
            if (Cache.TryGetValue(name, out var tex) && tex != null) return tex;
            tex = Resources.Load<Texture2D>("UI/Icons/" + name);
            if (tex == null) tex = Draw(Shape(name));
            Cache[name] = tex;
            return tex;
        }

        public static string For(ServiceType service) => service.ToString().ToLowerInvariant();

        public static string For(BuildCategory category)
        {
            switch (category)
            {
                case BuildCategory.Landing: return "landing";
                case BuildCategory.Structure: return "structure";
                case BuildCategory.Power: return "power";
                case BuildCategory.Fuel: return "fuel";
                case BuildCategory.Operations: return "hail";
                case BuildCategory.Business: return "corporate";
                case BuildCategory.Leisure: return "food";
                case BuildCategory.Commerce: return "trade";
                default: return "build";
            }
        }

        public static string For(ShipRole role)
        {
            switch (role)
            {
                case ShipRole.Freighter: return "hauler";
                case ShipRole.Merchant: return "trade";
                case ShipRole.Military: return "military";
                case ShipRole.Explorer: return "survey";
                case ShipRole.Executive: return "corporate";
                default: return "shuttle";
            }
        }

        /// <summary>Signed distance (in 0..1 icon space, y up): negative inside the shape.</summary>
        delegate float Sdf(Vector2 p);

        static Texture2D Draw(Sdf shape)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            const float aa = 1.3f / Size;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = shape(new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size));
                byte a = (byte)(Mathf.Clamp01(0.5f - d / aa) * 255f);
                pixels[y * Size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ shapes

        static Sdf Shape(string name)
        {
            switch (name)
            {
                case "credits":
                    // A stack of coins.
                    return p => Union(Box(p, V(0.46f, 0.24f), V(0.28f, 0.07f)) - 0.02f, Box(p, V(0.54f, 0.45f), V(0.28f, 0.07f)) - 0.02f, Box(p, V(0.46f, 0.66f), V(0.28f, 0.07f)) - 0.02f);
                case "reputation":
                    return p => Polygon(p, Star(5, 0.4f, 0.17f));
                case "fuel":
                    return p => Union(Circle(p, V(0.5f, 0.38f), 0.22f), Polygon(p, V(0.5f, 0.88f), V(0.29f, 0.44f), V(0.71f, 0.44f)));
                case "power":
                    return p => Polygon(p, V(0.6f, 0.92f), V(0.26f, 0.46f), V(0.48f, 0.46f), V(0.4f, 0.08f), V(0.76f, 0.56f), V(0.53f, 0.56f));
                case "sun":
                    return p =>
                    {
                        float d = Circle(p, C, 0.16f);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            d = Mathf.Min(d, Seg(p, C + dir * 0.26f, C + dir * 0.4f, 0.035f));
                        }
                        return d;
                    };
                case "eclipse":
                    return p => Subtract(Circle(p, C, 0.32f), Circle(p, V(0.64f, 0.6f), 0.27f));
                case "ship":
                case "shuttle":
                    return p => Polygon(p, V(0.5f, 0.9f), V(0.74f, 0.2f), V(0.5f, 0.34f), V(0.26f, 0.2f));
                case "hauler":
                    return p => Union(Box(p, V(0.5f, 0.5f), V(0.16f, 0.32f)), Box(p, V(0.5f, 0.32f), V(0.3f, 0.1f)), Polygon(p, V(0.34f, 0.82f), V(0.66f, 0.82f), V(0.5f, 0.94f)));
                case "clock":
                    return p => Union(Ring(p, C, 0.34f, 0.05f), Seg(p, C, V(0.5f, 0.7f), 0.045f), Seg(p, C, V(0.65f, 0.5f), 0.045f));
                case "log":
                    return p => Union(Seg(p, V(0.36f, 0.72f), V(0.8f, 0.72f), 0.045f), Seg(p, V(0.36f, 0.5f), V(0.8f, 0.5f), 0.045f), Seg(p, V(0.36f, 0.28f), V(0.68f, 0.28f), 0.045f),
                        Circle(p, V(0.2f, 0.72f), 0.06f), Circle(p, V(0.2f, 0.5f), 0.06f), Circle(p, V(0.2f, 0.28f), 0.06f));
                case "ledger":
                    return p => Union(Box(p, V(0.26f, 0.32f), V(0.08f, 0.14f)), Box(p, V(0.5f, 0.42f), V(0.08f, 0.24f)), Box(p, V(0.74f, 0.52f), V(0.08f, 0.34f)));
                case "grid":
                    return p => Union(Subtract(Box(p, C, V(0.36f, 0.36f)), Box(p, C, V(0.3f, 0.3f))),
                        Seg(p, V(0.38f, 0.16f), V(0.38f, 0.84f), 0.025f), Seg(p, V(0.62f, 0.16f), V(0.62f, 0.84f), 0.025f),
                        Seg(p, V(0.16f, 0.38f), V(0.84f, 0.38f), 0.025f), Seg(p, V(0.16f, 0.62f), V(0.84f, 0.62f), 0.025f));
                case "select":
                    return p => Polygon(p, V(0.3f, 0.86f), V(0.3f, 0.2f), V(0.45f, 0.35f), V(0.56f, 0.12f), V(0.65f, 0.16f), V(0.54f, 0.39f), V(0.74f, 0.39f));
                case "demolish":
                    return p => Union(Seg(p, V(0.24f, 0.24f), V(0.76f, 0.76f), 0.075f), Seg(p, V(0.24f, 0.76f), V(0.76f, 0.24f), 0.075f));
                case "close":
                    return p => Union(Seg(p, V(0.3f, 0.3f), V(0.7f, 0.7f), 0.055f), Seg(p, V(0.3f, 0.7f), V(0.7f, 0.3f), 0.055f));
                case "pause":
                    return p => Union(Box(p, V(0.36f, 0.5f), V(0.08f, 0.26f)), Box(p, V(0.64f, 0.5f), V(0.08f, 0.26f)));
                case "speed1":
                    return p => Polygon(p, V(0.32f, 0.22f), V(0.76f, 0.5f), V(0.32f, 0.78f));
                case "speed2":
                    return p => Union(Polygon(p, V(0.18f, 0.26f), V(0.5f, 0.5f), V(0.18f, 0.74f)), Polygon(p, V(0.5f, 0.26f), V(0.82f, 0.5f), V(0.5f, 0.74f)));
                case "speed3":
                    return p => Union(Polygon(p, V(0.08f, 0.3f), V(0.36f, 0.5f), V(0.08f, 0.7f)), Polygon(p, V(0.36f, 0.3f), V(0.64f, 0.5f), V(0.36f, 0.7f)), Polygon(p, V(0.64f, 0.3f), V(0.92f, 0.5f), V(0.64f, 0.7f)));
                case "focus":
                    return p => Union(Ring(p, C, 0.24f, 0.045f), Circle(p, C, 0.06f), Seg(p, V(0.5f, 0.06f), V(0.5f, 0.2f), 0.045f), Seg(p, V(0.5f, 0.8f), V(0.5f, 0.94f), 0.045f),
                        Seg(p, V(0.06f, 0.5f), V(0.2f, 0.5f), 0.045f), Seg(p, V(0.8f, 0.5f), V(0.94f, 0.5f), 0.045f));
                case "toggle":
                    return p => Union(Subtract(Ring(p, C, 0.3f, 0.055f), Box(p, V(0.5f, 0.82f), V(0.11f, 0.2f))), Seg(p, V(0.5f, 0.5f), V(0.5f, 0.88f), 0.055f));
                case "hail":
                    return p =>
                    {
                        var o = V(0.26f, 0.26f);
                        float d = Circle(p, o, 0.08f);
                        foreach (float r in new[] { 0.26f, 0.44f, 0.62f })
                            d = Mathf.Min(d, Mathf.Max(Ring(p, o, r, 0.045f), Mathf.Max(o.x - p.x, o.y - p.y)));
                        return d;
                    };
                case "landing":
                    return p => Union(Ring(p, C, 0.36f, 0.05f), Seg(p, V(0.38f, 0.32f), V(0.38f, 0.68f), 0.05f), Seg(p, V(0.62f, 0.32f), V(0.62f, 0.68f), 0.05f), Seg(p, V(0.38f, 0.5f), V(0.62f, 0.5f), 0.05f));
                case "structure":
                    return p => Union(Box(p, V(0.32f, 0.32f), V(0.14f, 0.14f)), Box(p, V(0.68f, 0.32f), V(0.14f, 0.14f)), Box(p, V(0.32f, 0.68f), V(0.14f, 0.14f)), Box(p, V(0.68f, 0.68f), V(0.14f, 0.14f)));
                case "services":
                    return p => Union(Circle(p, V(0.5f, 0.7f), 0.13f), Subtract(Circle(p, V(0.5f, 0.2f), 0.3f), Box(p, V(0.5f, 0.0f), V(0.5f, 0.18f))));
                case "warning":
                    return p => Union(Subtract(Polygon(p, V(0.5f, 0.88f), V(0.1f, 0.14f), V(0.9f, 0.14f)), Polygon(p, V(0.5f, 0.74f), V(0.22f, 0.22f), V(0.78f, 0.22f))),
                        Seg(p, V(0.5f, 0.42f), V(0.5f, 0.6f), 0.045f), Circle(p, V(0.5f, 0.31f), 0.045f));
                case "build":
                    return p => Union(Box(p, V(0.5f, 0.22f), V(0.34f, 0.07f)), Box(p, V(0.5f, 0.44f), V(0.22f, 0.07f)), Box(p, V(0.5f, 0.66f), V(0.1f, 0.07f)));
                // Services
                case "rest": // a bed
                    return p => Union(Box(p, V(0.5f, 0.38f), V(0.36f, 0.07f)), Box(p, V(0.18f, 0.42f), V(0.05f, 0.22f)), Box(p, V(0.82f, 0.32f), V(0.05f, 0.13f)),
                        Box(p, V(0.32f, 0.54f), V(0.1f, 0.06f)) - 0.02f, Box(p, V(0.6f, 0.5f), V(0.2f, 0.04f)));
                case "food": // a mug
                    return p => Union(Box(p, V(0.42f, 0.42f), V(0.2f, 0.24f)) - 0.03f, Ring(p, V(0.66f, 0.44f), 0.11f, 0.045f),
                        Seg(p, V(0.34f, 0.8f), V(0.38f, 0.92f), 0.03f), Seg(p, V(0.48f, 0.8f), V(0.52f, 0.92f), 0.03f));
                case "sightseeing": // an eye: a lens outline and a pupil
                    return p =>
                    {
                        float outer = Mathf.Max(Circle(p, V(0.5f, 0.15f), 0.5f), Circle(p, V(0.5f, 0.85f), 0.5f));
                        float inner = Mathf.Max(Circle(p, V(0.5f, 0.2f), 0.43f), Circle(p, V(0.5f, 0.8f), 0.43f));
                        return Union(Subtract(outer, inner), Circle(p, C, 0.11f));
                    };
                case "trade": // a crate
                    return p => Union(Subtract(Box(p, C, V(0.34f, 0.3f)), Box(p, C, V(0.27f, 0.23f))), Seg(p, V(0.24f, 0.28f), V(0.76f, 0.72f), 0.04f), Seg(p, V(0.24f, 0.72f), V(0.76f, 0.28f), 0.04f));
                case "shopping": // a bag
                    return p => Union(Box(p, V(0.5f, 0.38f), V(0.3f, 0.26f)) - 0.02f, Subtract(Ring(p, V(0.5f, 0.66f), 0.14f, 0.04f), Box(p, V(0.5f, 0.5f), V(0.3f, 0.1f))));
                case "parts": // a gear
                    return p =>
                    {
                        float d = Ring(p, C, 0.22f, 0.07f);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            d = Mathf.Min(d, Seg(p, C + dir * 0.26f, C + dir * 0.38f, 0.06f));
                        }
                        return d;
                    };
                case "military": // a shield with a chevron
                    return p => Subtract(Polygon(p, V(0.2f, 0.84f), V(0.8f, 0.84f), V(0.8f, 0.48f), V(0.5f, 0.1f), V(0.2f, 0.48f)),
                        Union(Seg(p, V(0.32f, 0.56f), V(0.5f, 0.4f), 0.045f), Seg(p, V(0.68f, 0.56f), V(0.5f, 0.4f), 0.045f)));
                case "survey": // a compass star
                    return p => Union(Polygon(p, Star(4, 0.42f, 0.1f)), Ring(p, C, 0.24f, 0.03f));
                case "corporate": // a briefcase
                    return p => Union(Box(p, V(0.5f, 0.42f), V(0.36f, 0.24f)) - 0.02f, Subtract(Box(p, V(0.5f, 0.7f), V(0.14f, 0.08f)), Box(p, V(0.5f, 0.7f), V(0.08f, 0.04f))));
                case "info":
                default:
                    return p => Union(Ring(p, C, 0.36f, 0.05f), Seg(p, V(0.5f, 0.28f), V(0.5f, 0.52f), 0.05f), Circle(p, V(0.5f, 0.68f), 0.055f));
            }
        }

        static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        static float Union(params float[] d)
        {
            float m = d[0];
            for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]);
            return m;
        }

        static float Subtract(float a, float b) => Mathf.Max(a, -b);
        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        static float Ring(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs((p - c).magnitude - r) - w;

        static float Box(Vector2 p, Vector2 c, Vector2 half)
        {
            var d = new Vector2(Mathf.Abs(p.x - c.x) - half.x, Mathf.Abs(p.y - c.y) - half.y);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        static float Seg(Vector2 p, Vector2 a, Vector2 b, float w)
        {
            var pa = p - a;
            var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - w;
        }

        /// <summary>Signed distance to any simple polygon (convex or not).</summary>
        static float Polygon(Vector2 p, params Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        static Vector2[] Star(int points, float outer, float inner)
        {
            var v = new Vector2[points * 2];
            for (int i = 0; i < v.Length; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / points;
                float r = i % 2 == 0 ? outer : inner;
                v[i] = C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return v;
        }
    }
}
