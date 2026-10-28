using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
#if WAYSTATION_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Waystation.EditorTools
{
    /// <summary>
    /// Switches the project to URP automatically, so nobody has to click through settings:
    ///   1. If the Universal RP package is missing, installs it (Package Manager picks the version
    ///      that matches this editor). Unity recompiles, which runs this script again.
    ///   2. Creates Assets/Waystation/Settings/Waystation_URP.asset (+ renderer), makes it the default
    ///      pipeline, clears per-quality overrides, and switches the project to Linear colour.
    /// Run it again any time from the menu: Waystation > Set Up URP.
    /// </summary>
    [InitializeOnLoad]
    static class UrpSetup
    {
        const string SettingsFolder = "Assets/Waystation/Settings";
        const string PipelinePath = SettingsFolder + "/Waystation_URP.asset";
        const string RendererPath = SettingsFolder + "/Waystation_URP_Renderer.asset";
        const string PackageName = "com.unity.render-pipelines.universal";

        static UrpSetup()
        {
            EditorApplication.delayCall += () => Run(manual: false);
        }

        [MenuItem("Waystation/Set Up URP")]
        static void RunFromMenu() => Run(manual: true);

        static void Run(bool manual)
        {
#if WAYSTATION_URP
            try
            {
                ConfigurePipeline(manual);
            }
            catch (Exception e)
            {
                Debug.LogError($"Waystation: automatic URP setup failed ({e.Message}). Manual steps are in docs/TECH.md.\n{e}");
            }
#else
            // Ask once per editor session; installing triggers a recompile that re-runs this script.
            if (!manual && SessionState.GetBool("Waystation.UrpRequested", false)) return;
            SessionState.SetBool("Waystation.UrpRequested", true);
            Debug.Log("Waystation: installing the Universal Render Pipeline package…");
            var request = Client.Add(PackageName);
            EditorApplication.update += Wait;

            void Wait()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Wait;
                if (request.Status == StatusCode.Success)
                    Debug.Log($"Waystation: installed {request.Result.displayName} {request.Result.version}. Finishing setup after recompiling.");
                else
                    Debug.LogError($"Waystation: couldn't install URP ({request.Error?.message}). Install 'Universal RP' from the Package Manager, then use Waystation > Set Up URP.");
            }
#endif
        }

#if WAYSTATION_URP
        static void ConfigurePipeline(bool manual)
        {
            bool changed = false;

            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                if (pipeline == null) pipeline = CreatePipelineAsset();
                GraphicsSettings.defaultRenderPipeline = pipeline;
                changed = true;
            }

            // Per-quality-level overrides would bypass the default; clear them.
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null && QualitySettings.renderPipeline != pipeline)
                {
                    QualitySettings.renderPipeline = null;
                    changed = true;
                }
            }
            QualitySettings.SetQualityLevel(current, false);

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Waystation: URP is active (Linear colour, HDR on). Press Play for the grim-dark post-processing.");
            }
            else if (manual)
            {
                Debug.Log("Waystation: URP is already set up.");
            }
        }

        static UniversalRenderPipelineAsset CreatePipelineAsset()
        {
            Directory.CreateDirectory(SettingsFolder);

            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssignDefaultPostProcessData(renderer);
            AssetDatabase.CreateAsset(renderer, RendererPath);

            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = true;
            // Clean edges on thin railings and cables; shadows reach the station (the camera sits ~100 units back).
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 160f;
            var so = new SerializedObject(pipeline);
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        /// <summary>
        /// URP's Create menu gives new renderers the package's default post-processing resources;
        /// a renderer made from code doesn't get them, so assign the same asset here.
        /// </summary>
        static void AssignDefaultPostProcessData(UniversalRendererData renderer)
        {
            if (renderer.postProcessData != null) return;
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/" + PackageName + "/Runtime/Data/PostProcessData.asset");
            if (renderer.postProcessData == null)
                Debug.LogWarning("Waystation: couldn't find URP's default PostProcessData; post-processing may not render. " +
                                 "Select Assets/Waystation/Settings/Waystation_URP_Renderer and assign it under Post-processing.");
        }
#endif
    }
}
