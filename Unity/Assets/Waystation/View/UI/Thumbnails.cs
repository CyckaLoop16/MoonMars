using System.Collections.Generic;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Build-menu and inspector pictures of each building type, rendered once from the actual model (GPT
    /// art if it's installed, otherwise the placeholder) with an offscreen camera. A texture at
    /// Resources/UI/Thumbnails/&lt;BuildingType&gt;.png replaces the render. Returns null if rendering
    /// isn't possible, and callers fall back to an icon.
    /// </summary>
    public static class Thumbnails
    {
        /// <summary>Layer the preview models sit on, so only the thumbnail camera sees them.</summary>
        public const int Layer = 31;
        const int Size = 192;

        static readonly Dictionary<BuildingType, Texture2D> Cache = new Dictionary<BuildingType, Texture2D>();
        static readonly int SunDir = Shader.PropertyToID("_WS_SunDir");
        static readonly int SunColor = Shader.PropertyToID("_WS_SunColor");
        static readonly int Ambient = Shader.PropertyToID("_WS_Ambient");
        static readonly int LightCount = Shader.PropertyToID("_WS_LightCount");

        public static Texture2D Get(BuildingRenderer renderer, BuildingType type)
        {
            if (Cache.TryGetValue(type, out var tex)) return tex;
            tex = Resources.Load<Texture2D>("UI/Thumbnails/" + type);
            if (tex == null && renderer != null) tex = Render(renderer, type);
            Cache[type] = tex;
            return tex;
        }

        static Texture2D Render(BuildingRenderer renderer, BuildingType type)
        {
            GameObject model = null, cameraGo = null, lightGo = null;
            RenderTexture target = null;
            Camera camera = null;
            try
            {
                model = renderer.CreatePreview(type);
                model.transform.position = new Vector3(0f, -2000f, 0f);
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return null;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                // The same isometric angle as the game camera, fitted around the model.
                var rotation = Quaternion.Euler(35f, 45f, 0f);
                var inverse = Quaternion.Inverse(rotation);
                float extent = 0.1f;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = inverse * (corner - bounds.center);
                    extent = Mathf.Max(extent, Mathf.Abs(local.x), Mathf.Abs(local.y));
                }

                cameraGo = new GameObject("Thumbnail Camera");
                camera = cameraGo.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = extent * 1.06f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.cullingMask = 1 << Layer;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 200f;
                camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * 80f, rotation);

                // Studio lighting: a warm key from the upper left for lit art and for the placeholder shader.
                lightGo = new GameObject("Thumbnail Light");
                var key = lightGo.AddComponent<Light>();
                key.type = LightType.Directional;
                key.cullingMask = 1 << Layer;
                key.intensity = 1.3f;
                key.color = new Color(1f, 0.95f, 0.88f);
                var toKey = new Vector3(-0.55f, 0.8f, -0.2f).normalized;
                lightGo.transform.rotation = Quaternion.LookRotation(-toKey);
                Shader.SetGlobalVector(SunDir, toKey);
                Shader.SetGlobalColor(SunColor, MeshBuilder.ToRenderSpace(new Color(1.05f, 1f, 0.92f)));
                Shader.SetGlobalColor(Ambient, MeshBuilder.ToRenderSpace(new Color(0.42f, 0.44f, 0.5f)));
                Shader.SetGlobalFloat(LightCount, 0f);
                // (SkyCycle and LightRig put the real values back before the frame renders.)

                target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
                camera.targetTexture = target;
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = $"Thumbnail {type}", wrapMode = TextureWrapMode.Clamp };
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;

                foreach (var px in tex.GetPixels32())
                    if (px.a > 8 && (px.r > 4 || px.g > 4 || px.b > 4)) return tex;
                Object.Destroy(tex); // nothing drew: fall back to an icon
                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Waystation: couldn't render a thumbnail for {type} ({e.Message}); using an icon.");
                return null;
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (model != null) Object.DestroyImmediate(model);
                if (cameraGo != null) Object.DestroyImmediate(cameraGo);
                if (lightGo != null) Object.DestroyImmediate(lightGo);
            }
        }
    }
}
