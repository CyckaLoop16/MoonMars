using UnityEngine;

namespace Waystation.View
{
    public enum FxPreset
    {
        /// <summary>Engine exhaust: short, hot, additive.</summary>
        Exhaust,
        /// <summary>Thick dark smoke trailing from a damaged ship.</summary>
        Smoke,
        /// <summary>Sparks shorting out of a damaged hull.</summary>
        Sparks,
        /// <summary>Cold venting vapour from a fuel line.</summary>
        Vapor,
        /// <summary>Welding sparks during hangar repairs.</summary>
        Weld,
        /// <summary>Ice crystals and grit drifting around the station, glinting in the light.</summary>
        Dust,
    }

    /// <summary>
    /// Code-built particle effects (no assets needed). Each effect loops; turn it on and off with
    /// <see cref="SetActive"/>, which only stops new particles, so trails fade out naturally.
    /// </summary>
    public static class Fx
    {
        static Material _additive, _alpha;

        static Material Additive => _additive != null ? _additive : _additive = new Material(Shader.Find("Waystation/Particle Additive"));
        static Material Alpha => _alpha != null ? _alpha : _alpha = new Material(Shader.Find("Waystation/Particle Alpha"));

        public static void SetActive(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            var emission = ps.emission;
            if (emission.enabled != on) emission.enabled = on;
        }

        public static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.rateOverTime = rate;
        }

        /// <summary>Creates an effect under <paramref name="parent"/>, emitting along <paramref name="direction"/> (local).</summary>
        public static ParticleSystem Create(Transform parent, FxPreset preset, Vector3 localPosition, Vector3 direction, bool startActive = false)
        {
            var go = new GameObject($"FX {preset}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.LookRotation(direction == Vector3.zero ? Vector3.up : direction);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            var shape = ps.shape;
            var color = ps.colorOverLifetime;
            var size = ps.sizeOverLifetime;
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            shape.enabled = true;
            color.enabled = true;
            size.enabled = true;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            switch (preset)
            {
                case FxPreset.Exhaust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.26f);
                    main.maxParticles = 120;
                    Rate(ps, 50f);
                    Cone(shape, 7f, 0.04f);
                    color.color = Fade(Hex("#fff1c4"), Hex("#ff7a1a"), 1f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));
                    renderer.sharedMaterial = Additive;
                    break;

                case FxPreset.Smoke:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    main.maxParticles = 80;
                    Rate(ps, 14f);
                    Sphere(shape, 0.15f);
                    color.color = Fade(Hex("#2a2725"), Hex("#121110"), 0.75f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3f));
                    renderer.sharedMaterial = Alpha;
                    break;

                case FxPreset.Sparks:
                case FxPreset.Weld:
                    bool weld = preset == FxPreset.Weld;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, weld ? 0.6f : 0.8f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, weld ? 3.5f : 2.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                    main.gravityModifier = 0.35f;
                    main.maxParticles = 150;
                    Rate(ps, weld ? 45f : 10f);
                    Sphere(shape, 0.08f);
                    color.color = weld ? Fade(Hex("#e6f2ff"), Hex("#ff9a3c"), 1f, 0f) : Fade(Hex("#ffe08a"), Hex("#ff4a1a"), 1f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.velocityScale = 0.06f;
                    renderer.lengthScale = 1f;
                    renderer.sharedMaterial = Additive;
                    break;

                case FxPreset.Dust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
                    main.maxParticles = 500;
                    main.prewarm = true;
                    Rate(ps, 10f);
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(40f, 6f, 40f);
                    shape.randomDirectionAmount = 1f;
                    color.color = Fade(Hex("#dbe8ff"), Hex("#a8c0e0"), 0.5f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.6f));
                    renderer.sharedMaterial = Additive;
                    break;

                case FxPreset.Vapor:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
                    main.maxParticles = 60;
                    Rate(ps, 12f);
                    Cone(shape, 25f, 0.05f);
                    color.color = Fade(Hex("#cfe6ef"), Hex("#8fa9b5"), 0.4f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3.5f));
                    renderer.sharedMaterial = Alpha;
                    break;
            }

            SetActive(ps, startActive);
            ps.Play();
            return ps;
        }

        /// <summary>
        /// One-shot ring of dust (and optionally sparks) at a world position, e.g. where a module touches
        /// down or is torn down. Cleans itself up when done.
        /// </summary>
        public static void Burst(Transform parent, Vector3 position, float radius, bool sparks)
        {
            OneShot(parent, position, radius, false);
            if (sparks) OneShot(parent, position, radius, true);
        }

        static void OneShot(Transform parent, Vector3 position, float radius, bool sparks)
        {
            var go = new GameObject(sparks ? "FX Burst Sparks" : "FX Burst Dust");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(Vector3.up); // the emitter's circle lies flat

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.4f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(12 + radius * 14f, 12, 80)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.2f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            if (sparks)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                color.color = Fade(Hex("#fff1c4"), Hex("#ff7a1a"), 1f, 0f);
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 1f;
                renderer.sharedMaterial = Additive;
            }
            else
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                color.color = Fade(Hex("#b8b4ac"), Hex("#6e6a64"), 0.55f, 0f);
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.4f));
                renderer.sharedMaterial = Alpha;
            }
            ps.Play();
        }

        static void Rate(ParticleSystem ps, float rate)
        {
            var emission = ps.emission;
            emission.rateOverTime = rate;
        }

        static void Cone(ParticleSystem.ShapeModule shape, float angle, float radius)
        {
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
        }

        static void Sphere(ParticleSystem.ShapeModule shape, float radius)
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        static Color Hex(string hex) => MeshBuilder.ToRenderSpace(MeshBuilder.Hex(hex));

        static ParticleSystem.MinMaxGradient Fade(Color from, Color to, float alphaFrom, float alphaTo)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(alphaFrom, 0f), new GradientAlphaKey(alphaFrom * 0.8f, 0.4f), new GradientAlphaKey(alphaTo, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }
    }
}
