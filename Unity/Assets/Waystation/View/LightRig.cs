using System.Collections.Generic;
using UnityEngine;

namespace Waystation.View
{
    public enum FxLightMode
    {
        Steady,
        /// <summary>A failing lamp: mostly on, with ragged dropouts.</summary>
        Flicker,
        /// <summary>Slow breathing glow.</summary>
        Pulse,
        /// <summary>Rotating warning beacon: short bright sweeps.</summary>
        Strobe,
    }

    /// <summary>
    /// A station lamp. Feeds the placeholder lit shader through <see cref="LightRig"/>, and also drives a
    /// real Unity point light so GPT's lit (URP) assets are lit by the same lamps.
    /// </summary>
    public sealed class FxLight : MonoBehaviour
    {
        public static readonly List<FxLight> Active = new List<FxLight>();
        /// <summary>Also spawn real Unity lights (for lit art assets). Placeholders don't need them.</summary>
        public static bool UseUnityLights = true;

        public Color Color = Color.white;
        public float Intensity = 1f;
        public float Range = 3f;
        public FxLightMode Mode;
        /// <summary>Floodlights and walkway lamps dim in sunlight; indicators and beacons don't.</summary>
        public bool DayDim;

        float _phase;
        Light _light;

        /// <summary>Animated intensity this frame (set by LightRig).</summary>
        public float Current { get; private set; }

        /// <summary>A floodlight: like <see cref="Create"/>, but it dims while the sun is up.</summary>
        public static FxLight Flood(Transform parent, Vector3 localPosition, Color color, float intensity, float range, FxLightMode mode = FxLightMode.Steady)
        {
            var lamp = Create(parent, localPosition, color, intensity, range, mode);
            lamp.DayDim = true;
            return lamp;
        }

        public static FxLight Create(Transform parent, Vector3 localPosition, Color color, float intensity, float range, FxLightMode mode = FxLightMode.Steady)
        {
            var go = new GameObject($"Lamp ({mode})");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var lamp = go.AddComponent<FxLight>();
            lamp.Color = color;
            lamp.Intensity = intensity;
            lamp.Range = range;
            lamp.Mode = mode;
            return lamp;
        }

        void OnEnable()
        {
            _phase = Random.value * 100f;
            Active.Add(this);
            if (UseUnityLights && _light == null)
            {
                _light = gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.shadows = LightShadows.None;
            }
        }

        void OnDisable() => Active.Remove(this);

        public float Evaluate(float time)
        {
            float t = time + _phase;
            switch (Mode)
            {
                case FxLightMode.Flicker:
                    float n = Mathf.PerlinNoise(t * 6f, _phase);
                    return n < 0.32f ? 0.08f : n < 0.4f ? 0.55f : 1f;
                case FxLightMode.Pulse:
                    return 0.55f + 0.45f * Mathf.Sin(t * 2.5f);
                case FxLightMode.Strobe:
                    return Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 3.5f)), 10f) * 1.6f + 0.05f;
                default:
                    return 1f;
            }
        }

        /// <summary>During a brownout lamps stutter and sag (1 = full power).</summary>
        public float Brownout(float time, float power)
        {
            if (power >= 0.999f) return 1f;
            return Mathf.PerlinNoise(time * 7f, _phase + 3f) < 0.45f ? 0.08f : Mathf.Max(0.2f, power);
        }

        public void Apply(float intensity)
        {
            Current = intensity;
            if (_light == null) return;
            _light.enabled = intensity > 0.01f;
            _light.color = Color;
            _light.range = Range;
            _light.intensity = intensity * 2f;
        }
    }

    /// <summary>
    /// Collects the station's lamps every frame and hands the nearest ones to the lit shader as global
    /// arrays, together with the dark ambient level and emissive boost that set the grim-dark mood.
    /// </summary>
    public sealed class LightRig : MonoBehaviour
    {
        const int MaxLights = 32;

        /// <summary>How much light reaches surfaces with no lamp nearby: very little.</summary>
        public static Color Ambient = new Color(0.2f, 0.21f, 0.25f);
        /// <summary>Emissive parts are multiplied by this, pushing them past 1.0 so bloom picks them up.</summary>
        public static float Emission = 2.6f;
        /// <summary>Sunlight on the station, 0–1 (set by SkyCycle); floodlights dim as it rises.</summary>
        public static float Daylight;
        /// <summary>Share of power demand being met (set by SkyCycle); below 1 the lamps stutter.</summary>
        public static float PowerLevel = 1f;

        readonly Vector4[] _positions = new Vector4[MaxLights];
        readonly Vector4[] _colors = new Vector4[MaxLights];
        readonly List<FxLight> _sorted = new List<FxLight>();

        static readonly int LightPos = Shader.PropertyToID("_WS_LightPos");
        static readonly int LightColor = Shader.PropertyToID("_WS_LightColor");
        static readonly int LightCount = Shader.PropertyToID("_WS_LightCount");
        static readonly int AmbientId = Shader.PropertyToID("_WS_Ambient");
        static readonly int EmissionId = Shader.PropertyToID("_WS_Emission");

        void LateUpdate()
        {
            float time = Time.time;
            _sorted.Clear();
            foreach (var lamp in FxLight.Active)
            {
                float dim = lamp.DayDim ? 1f - 0.75f * Daylight : 1f;
                lamp.Apply(lamp.Evaluate(time) * lamp.Intensity * dim * lamp.Brownout(time, PowerLevel));
                if (lamp.Current > 0.01f) _sorted.Add(lamp);
            }

            // With more lamps than the shader takes, keep the ones nearest the middle of the screen.
            var cam = Camera.main;
            if (_sorted.Count > MaxLights && cam != null)
            {
                var focus = cam.transform.position + cam.transform.forward * 100f;
                _sorted.Sort((a, b) => (a.transform.position - focus).sqrMagnitude.CompareTo((b.transform.position - focus).sqrMagnitude));
            }

            int count = Mathf.Min(MaxLights, _sorted.Count);
            for (int i = 0; i < MaxLights; i++)
            {
                if (i < count)
                {
                    var lamp = _sorted[i];
                    var p = lamp.transform.position;
                    var c = MeshBuilder.ToRenderSpace(lamp.Color) * lamp.Current;
                    _positions[i] = new Vector4(p.x, p.y, p.z, lamp.Range);
                    _colors[i] = new Vector4(c.r, c.g, c.b, 0f);
                }
                else
                {
                    _positions[i] = Vector4.zero;
                    _colors[i] = Vector4.zero;
                }
            }

            Shader.SetGlobalVectorArray(LightPos, _positions);
            Shader.SetGlobalVectorArray(LightColor, _colors);
            Shader.SetGlobalFloat(LightCount, count);
            Shader.SetGlobalColor(AmbientId, MeshBuilder.ToRenderSpace(Ambient));
            Shader.SetGlobalFloat(EmissionId, Emission);
        }
    }
}
