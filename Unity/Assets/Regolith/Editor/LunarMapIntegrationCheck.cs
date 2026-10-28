using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Regolith.Sim;
using Regolith.View;
using UnityEditor;
using UnityEngine;

namespace Regolith.Editor
{
    /// <summary>Exercises the generated terrain, collision, foundations and Mars fallback.</summary>
    static class LunarMapIntegrationCheck
    {
        [InitializeOnLoadMethod]
        static void OnLoad() => EditorApplication.delayCall += CheckOnce;

        static void CheckOnce()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtPreviews"));
            string source = Path.Combine(Application.dataPath, "Regolith/View/LunarTerrainArt.cs");
            string report = Path.Combine(folder, "LunarMapIntegrationCheck.txt");
            if (File.Exists(report) && File.GetLastWriteTimeUtc(report) >= File.GetLastWriteTimeUtc(source)) return;
            Check();
        }

        [MenuItem("Regolith/Art/Check Lunar Map Integration")]
        static void Check()
        {
            PreviewRenderUtility preview = null;
            GameObject root = null;
            try
            {
                root = new GameObject("Lunar map integration check") { hideFlags = HideFlags.HideAndDontSave };
                var renderer = root.AddComponent<HexMapRenderer>();
                if (root.GetComponentsInChildren<LineRenderer>().Length == 0)
                    typeof(HexMapRenderer).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(renderer, null);
                var map = LunarMapGenerator.Generate(16, 12, 17321);
                var landing = HexCoord.FromOffset(7, 5);
                var occupied = new[] { landing };
                var territory = new Dictionary<HexCoord, Color> { { landing, new Color(.31f, .56f, .90f) } };
                var watch = System.Diagnostics.Stopwatch.StartNew();
                renderer.Build(map, territory, occupied);
                watch.Stop();
                long buildMs = watch.ElapsedMilliseconds;
                var meshes = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                if (meshes.Sum(m => m.vertexCount) < map.TileCount * 100) throw new Exception("Lunar art was not combined into the map.");
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>())
                    if (collider.sharedMesh != collider.GetComponent<MeshFilter>().sharedMesh)
                        throw new Exception("Picking collider does not use the terrain chunk mesh.");
                var positions = meshes.SelectMany(m => m.vertices).ToArray();
                var indices = meshes.SelectMany(m => m.triangles).ToArray();
                renderer.Build(map, territory, occupied);
                meshes = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                if (!positions.SequenceEqual(meshes.SelectMany(m => m.vertices)) || !indices.SequenceEqual(meshes.SelectMany(m => m.triangles)))
                    throw new Exception("Repeated generation changed the terrain geometry.");

                foreach (var tile in map.Tiles)
                {
                    var center = HexMapRenderer.ToWorld(tile.Coord) + new Vector3(.05f, 0f, -.65f);
                    var ray = new Ray(center + Vector3.up * 8f, Vector3.down);
                    RaycastHit hit = default;
                    bool found = false;
                    foreach (var collider in root.GetComponentsInChildren<MeshCollider>())
                        if (collider.Raycast(ray, out hit, 20f)) { found = true; break; }
                    if (!found) throw new Exception("Hole at " + tile.Coord);
                    if (HexMapRenderer.FromWorld(hit.point) != tile.Coord) throw new Exception("Picking selects the wrong hex.");
                    if (!renderer.Raycast(ray, out var picked) || HexMapRenderer.FromWorld(picked) != tile.Coord)
                        throw new Exception("Runtime raycast selects the wrong hex.");
                    if (Mathf.Abs(hit.point.y - renderer.SurfaceHeight(tile, center)) > .002f)
                        throw new Exception("Rendered surface and sampled ground height differ at " + tile.Coord);
                }
                var landingTile = map.GetTile(landing);
                var landingCenter = HexMapRenderer.ToWorld(landing);
                foreach (var offset in new[] { Vector3.zero, new Vector3(.258f, 0f, .34f), new Vector3(-.258f, 0f, -.285f), new Vector3(0f, 0f, -.85f) })
                    if (Mathf.Abs(renderer.SurfaceHeight(landingTile, landingCenter + offset) - HexMapRenderer.TopHeight(landingTile)) > .0001f)
                        throw new Exception("Habitat footprint is not level.");
                renderer.SetSelected(landingTile);
                renderer.SetHovered(map.GetTile(HexCoord.FromOffset(8, 5)));
                foreach (var line in root.GetComponentsInChildren<LineRenderer>())
                    if (!line.enabled || line.positionCount != 48) throw new Exception("Selection outlines did not rebuild.");

                // Render the same generated map and its picked terrain in an isolated scene.
                preview = new PreviewRenderUtility();
                preview.AddSingleGO(root);
                var habitat = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Modules/Habitat"), root.transform);
                habitat.transform.position = new Vector3(landingCenter.x, HexMapRenderer.TopHeight(landingTile), landingCenter.z);
                var bounds = new Bounds();
                foreach (var chunk in root.GetComponentsInChildren<MeshRenderer>()) bounds.Encapsulate(chunk.bounds);
                var camera = preview.camera;
                camera.orthographic = true; camera.orthographicSize = 12.0f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 150f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .035f, .055f);
                camera.transform.position = bounds.center + new Vector3(10f, 25f, -22f);
                camera.transform.LookAt(bounds.center);
                preview.BeginStaticPreview(new Rect(0, 0, 1600, 1000));
                preview.Render();
                var image = preview.EndStaticPreview();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtPreviews"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "LunarMapUnity.png"), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                int lunarTriangles = indices.Length / 3;
                UnityEngine.Object.DestroyImmediate(habitat);

                // Mars continues through the original flat-top path.
                var mars = new WorldMap(1, 1, 17321, BodyId.Mars);
                var marsTile = new Tile(new HexCoord(0, 0)) { Terrain = TerrainType.RegolithPlains };
                mars.Add(marsTile);
                renderer.Build(mars);
                if (root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3) != 18)
                    throw new Exception("Mars fallback geometry changed.");
                if (Mathf.Abs(renderer.SurfaceHeight(marsTile, Vector3.zero) - HexMapRenderer.TopHeight(marsTile)) > .0001f)
                    throw new Exception("Mars ground height changed.");

                // Build the normal game-size map too, so index limits and collision cooking are exercised.
                var fullMap = LunarMapGenerator.Generate(64, 44, 17321);
                watch.Restart(); renderer.Build(fullMap); watch.Stop();
                int fullTriangles = root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
                long fullMs = watch.ElapsedMilliseconds;
                var chunks = root.GetComponentsInChildren<MeshFilter>();
                var before = chunks.ToDictionary(f => f, f => f.sharedMesh.vertices);
                var pad = HexCoord.FromOffset(3, 3);
                renderer.SetTerritory(new Dictionary<HexCoord, Color>(), new[] { pad });
                int changed = chunks.Count(f => !before[f].SequenceEqual(f.sharedMesh.vertices));
                if (changed != 1) throw new Exception("Foundation update rebuilt geometry outside its one chunk: " + changed);
                foreach (var offset in new[] { Vector3.zero, new Vector3(.258f, 0f, .34f) })
                    if (Mathf.Abs(renderer.SurfaceHeight(fullMap.GetTile(pad), HexMapRenderer.ToWorld(pad) + offset) - HexMapRenderer.TopHeight(fullMap.GetTile(pad))) > .0001f)
                        throw new Exception("Incremental foundation did not level terrain.");
                File.WriteAllText(Path.Combine(folder, "LunarMapIntegrationCheck.txt"),
                    "PASS: Unity " + Application.unityVersion + "\n" +
                    "Generated 16x12 lunar map: " + lunarTriangles + " triangles, first build " + buildMs + " ms.\n" +
                    "Repeated build deterministic; collision hits all 192 sampled tiles and matches surface heights.\n" +
                    "Habitat feet and ramp sit on level terrain; selection and hover outlines rebuild.\n" +
                    "Mars keeps its original 18-triangle single-tile mesh and ground height.\n" +
                    "64x44 lunar map: " + fullTriangles + " triangles, build " + fullMs + " ms.\n" +
                    "Incremental foundation update changes only its own chunk; other chunk geometry preserved.\n" +
                    "Preview: LunarMapUnity.png\n");
                Debug.Log("Lunar map integration check passed.");
            }
            catch (Exception exception) { Debug.LogError("Lunar map integration check: " + exception); }
            finally
            {
                preview?.Cleanup();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
