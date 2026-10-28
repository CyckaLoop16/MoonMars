using UnityEngine;
#if WAYSTATION_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Waystation.View
{
    /// <summary>
    /// The grim-dark camera look: ACES tonemapping, bloom on emissives, heavy vignette, film grain,
    /// desaturated grade with teal shadows and sodium highlights, slight chromatic aberration.
    /// Needs URP (installed automatically by the editor setup); under the Built-in pipeline it does nothing.
    /// </summary>
    public static class GrimPostFx
    {
        public static bool Available
        {
            get
            {
#if WAYSTATION_URP
                return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
#else
                return false;
#endif
            }
        }

        public static void Apply(Camera camera)
        {
#if WAYSTATION_URP
            if (!Available)
            {
                Debug.LogWarning("Waystation: URP is installed but not active; post-processing is off. Reopen the project to finish setup.");
                return;
            }

            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var go = new GameObject("Grim Post FX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.5f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.82f, 0.66f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.46f);
            vignette.smoothness.Override(0.42f);
            vignette.color.Override(Color.black);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.55f);
            grain.response.Override(0.75f);

            var grade = profile.Add<ColorAdjustments>(true);
            grade.saturation.Override(-38f);
            grade.contrast.Override(22f);
            grade.postExposure.Override(0.25f);

            var toning = profile.Add<SplitToning>(true);
            toning.shadows.Override(new Color(0.24f, 0.42f, 0.46f));
            toning.highlights.Override(new Color(1f, 0.68f, 0.42f));
            toning.balance.Override(-15f);

            profile.Add<ChromaticAberration>(true).intensity.Override(0.2f);
#endif
        }
    }
}
