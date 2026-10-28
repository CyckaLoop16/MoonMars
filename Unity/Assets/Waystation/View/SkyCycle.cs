using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Day and night from the orbit. Moves the sun across the sky (WorldRenderer), turns it into a
    /// directional light for the placeholder shader and for lit art (hard white light high up, raking
    /// orange light at sunrise and sunset, nothing in the planet's shadow), lifts the ambient by day,
    /// dims floodlights in sunlight and makes lamps stutter during brownouts.
    /// </summary>
    public sealed class SkyCycle : MonoBehaviour
    {
        // Brightness knobs. Night is lit by the planet below (cool "planetshine"), day by the sun.
        static readonly Color NightAmbient = new Color(0.36f, 0.39f, 0.46f);
        static readonly Color DayAmbient = new Color(0.46f, 0.48f, 0.54f);
        static readonly Color Planetshine = MeshBuilder.Hex("#8fb0d8");
        /// <summary>Ambient for URP-lit art, relative to the placeholder ambient above.</summary>
        const float ArtAmbientScale = 0.85f;
        static readonly Color LowSun = MeshBuilder.Hex("#ff7a3a");
        static readonly Color HighSun = MeshBuilder.Hex("#fff2dc");
        const float SunStrength = 1.4f;

        static readonly int SunDirId = Shader.PropertyToID("_WS_SunDir");
        static readonly int SunColorId = Shader.PropertyToID("_WS_SunColor");

        /// <summary>World direction towards the sun (for solar panels and the like).</summary>
        public static Vector3 SunDirection { get; private set; } = Vector3.up;
        /// <summary>Sunlight reaching the station, 0–1.</summary>
        public static float Daylight { get; private set; } = 1f;

        Light _light;
        Light _fill;
        Camera _camera;
        WorldRenderer _world;
        Station _station;

        public void Init(Camera camera, WorldRenderer world)
        {
            _camera = camera;
            _world = world;
            _light = new GameObject("Sun").AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.shadows = LightShadows.Soft;
            _light.cullingMask = ~(1 << Thumbnails.Layer);

            // Soft bluish fill from the planet below, so lit art never drops to black in the eclipse.
            _fill = new GameObject("Planetshine").AddComponent<Light>();
            _fill.type = LightType.Directional;
            _fill.shadows = LightShadows.None;
            _fill.color = Planetshine;
            _fill.cullingMask = ~(1 << Thumbnails.Layer);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        public void Bind(Station station) => _station = station;

        void LateUpdate()
        {
            if (_station == null || _camera == null) return;
            double phase = Orbit.Phase(_station.Tick);
            float exposure = (float)Orbit.SunExposure(_station.Tick);
            _world.UpdateSky(phase, exposure);

            // Across the sky from the left of the view (sunrise) to the right (sunset), high at noon.
            float dayEnd = (float)(1 - Orbit.ShadowFraction);
            float p = (float)phase;
            float u = p <= dayEnd ? p / dayEnd : p > (1f + dayEnd) / 2f ? 0f : 1f;
            float height = Mathf.Sin(Mathf.PI * u);
            var right = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            var forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            var toSun = (right * (-Mathf.Cos(Mathf.PI * u) * 0.95f) + Vector3.up * (0.15f + 0.85f * height) + forward * 0.45f).normalized;
            SunDirection = toSun;
            Daylight = exposure;

            var color = Color.Lerp(LowSun, HighSun, Mathf.Clamp01(Mathf.Min(exposure, height * 1.6f)));
            _light.transform.rotation = Quaternion.LookRotation(-toSun);
            _light.color = color;
            _light.intensity = 1.5f * exposure;
            _light.enabled = exposure > 0.001f;
            _fill.transform.rotation = Quaternion.LookRotation(-(Vector3.up * 0.6f - forward).normalized);
            _fill.intensity = Mathf.Lerp(0.55f, 0.25f, exposure);

            Shader.SetGlobalVector(SunDirId, toSun);
            Shader.SetGlobalColor(SunColorId, MeshBuilder.ToRenderSpace(color) * (SunStrength * exposure));

            var ambient = Color.Lerp(NightAmbient, DayAmbient, exposure);
            LightRig.Ambient = ambient;
            RenderSettings.ambientLight = ambient * ArtAmbientScale;
            LightRig.Daylight = exposure;
            LightRig.PowerLevel = (float)_station.PowerSatisfaction;
            GrimPostFx.SetDaylight(exposure);
        }
    }
}
