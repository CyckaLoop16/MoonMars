using UnityEngine;

namespace Waystation.View
{
    /// <summary>Shares a small fixture's power/flicker state between its emission map and real lamp.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class TileLightFixture : MonoBehaviour
    {
        public Renderer[] Lenses = new Renderer[0];
        public Transform Socket;
        FxLight _lamp;
        bool _powered;
        MaterialPropertyBlock _block;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        public void Bind(bool powered, FxLight lamp)
        {
            _powered = powered;
            _lamp = lamp;
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            float passive = (1f - .75f * Mathf.Clamp01(LightRig.Daylight)) * Mathf.Clamp01(LightRig.PowerLevel);
            float level = !_powered ? 0f : _lamp == null ? passive :
                _lamp.Intensity > 0f ? _lamp.Current / _lamp.Intensity : 0f;
            if (_block == null) _block = new MaterialPropertyBlock();
            foreach (var lens in Lenses)
            {
                if (lens == null || lens.sharedMaterial == null) continue;
                lens.GetPropertyBlock(_block);
                _block.SetColor(Emission, lens.sharedMaterial.GetColor(Emission) * level);
                lens.SetPropertyBlock(_block);
                _block.Clear();
            }
        }
    }
}
