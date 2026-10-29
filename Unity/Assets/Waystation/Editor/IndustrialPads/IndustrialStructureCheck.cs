using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Waystation.Sim;
using Waystation.View;

namespace Waystation.EditorTools
{
    static class IndustrialStructureCheck
    {
        static void Require(bool condition,string error){if(!condition)throw new Exception(error);}
        static void Invoke(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
        static int OpenSides(int mask){int n=0;for(int i=0;i<4;i++)if((mask&(1<<i))==0)n++;return n;}

        [MenuItem("Waystation/Art/Check Industrial Structures")]
        public static void Run()
        {
            var statistics=new List<string>();
            foreach(string name in new[]{"Walkway_Deck","Walkway_Railing","Foundation","Foundation_Edge"})
            {
                var prefab=Resources.Load<GameObject>("Buildings/"+name);
                Require(prefab!=null,"Missing structure prefab: "+name);
                var filter=prefab.GetComponent<MeshFilter>();var renderer=prefab.GetComponent<MeshRenderer>();
                Require(filter!=null&&renderer!=null,"Structure is not a native mesh prefab.");
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;
                Require(mesh.colors.Length==0&&mesh.uv.Length==mesh.vertexCount&&mesh.tangents.Length==mesh.vertexCount,"Lit attributes missing or baked colours present.");
                Require(mesh.subMeshCount==renderer.sharedMaterials.Length,"Material groups do not match the mesh.");
                foreach(var p in vertices)
                {
                    Require(!float.IsNaN(p.x)&&!float.IsInfinity(p.y),"Invalid vertex.");
                    Require(Mathf.Abs(p.x)<=.5001f&&Mathf.Abs(p.z)<=.5001f,"Structure exceeds its tile.");
                    if(name=="Foundation")Require(p.y<=.01f,"Foundation detail interferes with modules.");
                    if(name=="Walkway_Railing"||name=="Foundation_Edge")Require(p.z>=.40f,"Edge piece extends into the route.");
                }
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                    Require(cross.sqrMagnitude>1e-16f,"Degenerate structure triangle.");
                    Require(Vector3.Dot(cross.normalized,normals[a])>.99f,"A face normal disagrees with its winding.");
                }
                foreach(var material in renderer.sharedMaterials)
                    Require(material!=null&&material.shader.name=="Universal Render Pipeline/Lit"&&material.GetTexture("_BaseMap")!=null&&material.GetTexture("_BumpMap")!=null,"Structure material does not match the textured buildings.");
                if(name=="Walkway_Deck")
                {
                    Require(Mathf.Abs(HeightAt(mesh,0f,0f))<.001f,"Walking grate is not at deck level.");
                    Require(HeightAt(mesh,.0345f,.06f)<-.02f,"Walkway grating has no real open gaps.");
                }
                statistics.Add(name+": "+triangles.Length/3+" triangles; URP textured materials, normals, UVs and bounds OK.");
            }

            // Exercise the real auto-connecting renderer without altering the live scene or simulation.
            var maskRoot=new GameObject("Structure connection checks"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var renderer=maskRoot.AddComponent<BuildingRenderer>();
                var method=typeof(BuildingRenderer).GetMethod("BuildTile",BindingFlags.Instance|BindingFlags.NonPublic);
                Func<Mesh> missing=()=>throw new Exception("A structure fell back to placeholder geometry.");
                foreach(bool foundation in new[]{false,true})for(int mask=0;mask<16;mask++)
                {
                    var tile=new GameObject("Connection mask "+mask);
                    try
                    {
                        method.Invoke(renderer,new object[]{tile,mask,foundation?"Foundation":"Walkway_Deck",missing,foundation?"Foundation_Edge":"Walkway_Railing",missing});
                        Require(tile.transform.childCount==1+OpenSides(mask),"Incorrect rail/trim count.");
                        foreach(Transform child in tile.transform)
                        {
                            if(!child.name.StartsWith(foundation?"Foundation_Edge":"Walkway_Railing"))continue;
                            var d=child.forward;int side=Mathf.Abs(d.x)>.5f?d.x>0?1:3:d.z>0?0:2;
                            Require((mask&(1<<side))==0,"A rail or bumper blocks a connected side.");
                        }
                    }
                    finally{UnityEngine.Object.DestroyImmediate(tile);}
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(maskRoot);}
            float oldDay=LightRig.Daylight,oldPower=LightRig.PowerLevel;
            try{LightRig.Daylight=0f;LightRig.PowerLevel=1f;TileLightingCheck.Run();}
            finally{LightRig.Daylight=oldDay;LightRig.PowerLevel=oldPower;}
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../ArtPreviews"));Directory.CreateDirectory(folder);
            File.Copy(Path.Combine(folder,"WalkwayFoundationLightsUnity.png"),Path.Combine(folder,"IndustrialStructuresNight.png"),true);
            RenderDaylight(folder);
            File.WriteAllText(Path.Combine(folder,"IndustrialStructuresCheck.txt"),"PASS: industrial structural kit.\n"+string.Join("\n",statistics)+
                "\nPASS: all 32 walkway/foundation neighbour masks, with connected sides clear.\nPASS: all 64 existing fixture power/connection cases.\nDay and night URP previews rendered.\n");
            Debug.Log("Industrial structure geometry, connections and lighting checks passed.");
        }

        static float HeightAt(Mesh mesh,float x,float z)
        {
            var v=mesh.vertices;var t=mesh.triangles;float height=float.NegativeInfinity;
            for(int i=0;i<t.Length;i+=3)
            {
                var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];
                if(Vector3.Cross(b-a,c-a).y<=0)continue;
                float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<1e-9f)continue;
                float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/d,vv=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/d,w=1-u-vv;
                if(u>=-.00001f&&vv>=-.00001f&&w>=-.00001f)height=Mathf.Max(height,u*a.y+vv*b.y+w*c.y);
            }
            return height;
        }

        static void RenderDaylight(string folder)
        {
            var root=new GameObject("Industrial structure daylight preview"){hideFlags=HideFlags.HideAndDontSave};
            PreviewRenderUtility preview=null;VolumeProfile profile=null;var temporary=new HashSet<UnityEngine.Object>();
            float day=LightRig.Daylight,power=LightRig.PowerLevel;
            try
            {
                var station=new Station(16,14,523);
                station.Buildings.Add(new Building{Id=1,Type=BuildingType.StarterPad,Origin=new Cell(1,6)});
                int id=2;for(int x=4;x<=9;x++)station.Buildings.Add(new Building{Id=id++,Type=BuildingType.Walkway,Origin=new Cell(x,7)});
                foreach(int y in new[]{6,8})station.Buildings.Add(new Building{Id=id++,Type=BuildingType.Walkway,Origin=new Cell(7,y)});
                for(int x=10;x<=12;x++)for(int y=6;y<=8;y++)station.Buildings.Add(new Building{Id=id++,Type=BuildingType.Foundation,Origin=new Cell(x,y)});
                station.RecomputeConnections();var buildings=root.AddComponent<BuildingRenderer>();Invoke(buildings,"Awake");buildings.Bind(station);buildings.Rebuild();
                LightRig.Daylight=.85f;LightRig.PowerLevel=1f;
                foreach(var lamp in root.GetComponentsInChildren<FxLight>())if(lamp.GetComponent<Light>()==null)Invoke(lamp,"OnEnable");
                var rig=root.AddComponent<LightRig>();Invoke(rig,"LateUpdate");
                foreach(var fixture in root.GetComponentsInChildren<TileLightFixture>())Invoke(fixture,"LateUpdate");
                foreach(var pad in root.GetComponentsInChildren<PadArtLighting>())Invoke(pad,"LateUpdate");
                preview=new PreviewRenderUtility();preview.AddSingleGO(root);
                preview.lights[0].intensity=1.2f;preview.lights[0].color=new Color(1f,.88f,.73f);preview.lights[0].transform.rotation=Quaternion.Euler(45f,-35f,0);
                preview.lights[1].intensity=.18f;preview.lights[1].color=new Color(.45f,.58f,.8f);
                preview.ambientColor=new Color(.12f,.14f,.18f);
                var camera=preview.camera;camera.cameraType=CameraType.Game;camera.orthographic=true;camera.orthographicSize=4.4f;camera.allowHDR=true;
                camera.nearClipPlane=.01f;camera.farClipPlane=100f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.025f,.04f);
                var focus=new Vector3(-.5f,.1f,.4f);camera.transform.position=focus+new Vector3(8f,12f,-14f);camera.transform.LookAt(focus);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                var volume=root.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100f;profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(.95f);bloom.intensity.Override(.45f);
                preview.BeginStaticPreview(new Rect(0,0,2200,1300));preview.Render(true);var image=preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(folder,"IndustrialStructuresDay.png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                LightRig.Daylight=day;LightRig.PowerLevel=power;
                if(root!=null)
                {
                    foreach(var lamp in root.GetComponentsInChildren<FxLight>())FxLight.Active.Remove(lamp);
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&!AssetDatabase.Contains(filter.sharedMesh))temporary.Add(filter.sharedMesh);
                    foreach(var r in root.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)if(m!=null&&!AssetDatabase.Contains(m))temporary.Add(m);
                    var buildings=root.GetComponent<BuildingRenderer>();
                    foreach(string name in new[]{"_meshes","_statusMeshes"})
                    {
                        var dictionary=typeof(BuildingRenderer).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(buildings)as IDictionary;
                        foreach(UnityEngine.Object value in dictionary.Values)if(value!=null&&!AssetDatabase.Contains(value))temporary.Add(value);
                    }
                }
                preview?.Cleanup();if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                foreach(var item in temporary)if(item!=null)UnityEngine.Object.DestroyImmediate(item);
                if(profile!=null)UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
