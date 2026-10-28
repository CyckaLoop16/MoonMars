using UnityEngine;

namespace Waystation.View
{
    /// <summary>Drives URP emission from the same power and flicker state as the existing pad lamps.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PadArtLighting : MonoBehaviour
    {
        public Renderer[] SouthwestLenses = new Renderer[0];
        public Renderer[] NortheastLenses = new Renderer[0];
        public Renderer[] BeaconLenses = new Renderer[0];
        public Renderer[] PoweredIndicators = new Renderer[0];
        public Transform SouthwestSocket;
        public Transform NortheastSocket;
        public Transform BeaconSocket;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock _block;
        FxLight _southwest, _northeast, _beacon;
        bool _powered = true;

        public void SetPowered(bool powered)
        {
            _powered = powered;
            ApplyAll();
        }

        public void BindLights(FxLight southwest, FxLight northeast, FxLight beacon)
        {
            _southwest = southwest;
            _northeast = northeast;
            _beacon = beacon;
        }

        void LateUpdate() => ApplyAll();

        void ApplyAll()
        {
            float power = _powered ? 1f : 0f;
            Apply(SouthwestLenses, power * Fraction(_southwest));
            Apply(NortheastLenses, power * Fraction(_northeast));
            Apply(BeaconLenses, power * Fraction(_beacon));
            Apply(PoweredIndicators, power);
        }

        static float Fraction(FxLight lamp) => lamp == null ? 1f :
            lamp.Intensity > 0f ? lamp.Current / lamp.Intensity : 0f;

        void Apply(Renderer[] renderers, float scale)
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                renderer.GetPropertyBlock(_block);
                _block.SetColor(EmissionColor, renderer.sharedMaterial.GetColor(EmissionColor) * scale);
                renderer.SetPropertyBlock(_block);
                _block.Clear();
            }
        }
    }
}
