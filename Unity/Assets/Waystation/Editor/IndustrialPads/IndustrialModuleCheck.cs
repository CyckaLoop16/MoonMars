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
    static class IndustrialModuleCheck
    {
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Invoke(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
        static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(target);
        static readonly string[] Main={"SolarArray","BatteryBank","FuelCellGenerator","FuelTank","IceRefinery","PilotLounge","CommsArray","AtcTower","Hangar"};

        [MenuItem("Waystation/Art/Check Industrial Modules")]
        public static void Run()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../ArtPreviews/IndustrialModules"));Directory.CreateDirectory(folder);
            var report=new List<string>();float oldDay=LightRig.Daylight,oldPower=LightRig.PowerLevel;
            try
            {
                LightRig.Daylight=0f;LightRig.PowerLevel=1f;
                foreach(string name in IndustrialModuleInstaller.Names)
                {
                    var prefab=Resources.Load<GameObject>("Buildings/"+name);Require(prefab!=null,"Missing module prefab: "+name);
                    var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;var renderer=prefab.GetComponent<MeshRenderer>();
                    Require(mesh.colors.Length==0&&mesh.normals.Length==mesh.vertexCount&&mesh.uv.Length==mesh.vertexCount&&mesh.tangents.Length==mesh.vertexCount,"Missing PBR mesh data: "+name);
                    Require(mesh.subMeshCount==renderer.sharedMaterials.Length,"Material groups mismatch: "+name);
                    var v=mesh.vertices;var n=mesh.normals;var t=mesh.triangles;
                    for(int i=0;i<t.Length;i+=3)
                    {
                        var cross=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]);
                        Require(cross.sqrMagnitude>1e-16f,"Degenerate triangle: "+name);
                        Require(Vector3.Dot(cross.normalized,n[t[i]])>.98f,"Winding/normal disagreement: "+name);
                    }
                    foreach(var material in renderer.sharedMaterials)
                    {
                        Require(material!=null&&material.shader.name=="Universal Render Pipeline/Lit","Not a standard URP Lit material: "+name);
                        if(material.HasProperty("_EmissionColor")&&material.GetColor("_EmissionColor").maxColorComponent>0f)
                        {
                            Require(material.IsKeywordEnabled("_EMISSION"),"Emission shader is disabled: "+material.name);
                            Require(material.GetTexture("_EmissionMap")!=null,"Missing emission map: "+name);
                        }
                    }
                    if(Enum.TryParse<BuildingType>(name,out var type))
                    {
                        var def=BuildingCatalog.Get(type);
                        foreach(var p in v)Require(Mathf.Abs(p.x)<=def.Width*.5f+.0001f&&Mathf.Abs(p.z)<=def.Height*.5f+.0001f,"Module exceeds its footprint: "+name);
                        Require(v.Min(p=>p.y)>=-.25f,"Module has an unexpected low pivot: "+name);
                        if(name=="Hangar")foreach(var p in v)
                            Require(!(Mathf.Abs(p.x)<1.15f&&Mathf.Abs(p.z)<1.15f&&p.y>.05f&&p.y<1.1f),"Hangar obstructs a landed ship.");
                        CheckPower(prefab,type);
                    }
                    report.Add(name+": "+t.Length/3+" triangles; lit materials, UVs, normals and emission maps OK.");
                }
                CheckSolarSweep();
                foreach(string name in Main)RenderModule(name,folder,report);
                report.Add("PASS: native moving parts, solar sweep clearance, charge levels, refinery running state and power-off emissions.");
                File.WriteAllText(Path.Combine(folder,"Validation.txt"),"PASS: industrial module kit.\n"+string.Join("\n",report)+"\n");
                Debug.Log("Industrial module geometry, states, animations and renders passed.");
            }
            finally{LightRig.Daylight=oldDay;LightRig.PowerLevel=oldPower;}
        }

        static void CheckPower(GameObject prefab,BuildingType type)
        {
            var root=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var station=new Station(8,8,1){PowerSatisfaction=1};var owner=new Building{Id=1,Type=type,Connected=true,Enabled=false,Running=true};
                foreach(var art in root.GetComponentsInChildren<ModuleArtLighting>(true))
                {
                    art.Bind(owner,station,root.transform);
                    foreach(var surface in art.Surfaces)
                    {
                        var block=new MaterialPropertyBlock();surface.Renderer.GetPropertyBlock(block,surface.MaterialIndex);
                        Require(block.GetColor("_EmissionColor").maxColorComponent<.001f,"Disabled module still glows: "+type);
                    }
                }
                owner.Enabled=true;
                foreach(var art in root.GetComponentsInChildren<ModuleArtLighting>(true))
                {
                    Invoke(art,"LateUpdate");
                    foreach(var surface in art.Surfaces)
                    {
                        var block=new MaterialPropertyBlock();surface.Renderer.GetPropertyBlock(block,surface.MaterialIndex);
                        Require(block.GetColor("_EmissionColor").maxColorComponent>.01f,"Powered module emission is dark: "+type);
                    }
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void CheckSolarSweep()
        {
            var source=Resources.Load<GameObject>("Buildings/SolarArray_Wing").GetComponent<MeshFilter>().sharedMesh.vertices;
            for(int yaw=0;yaw<360;yaw+=15)foreach(int tilt in new[]{0,30,60})
            {
                var rotation=Quaternion.Euler(0,yaw,0)*Quaternion.Euler(tilt,0,0);
                foreach(var v in source)
                {
                    var p=rotation*v+new Vector3(0,.98f,0);
                    Require(Mathf.Abs(p.x)<=1.001f&&Mathf.Abs(p.z)<=1.001f&&p.y>.08f,"Solar tracking clips its foundation or neighbouring tiles.");
                }
            }
        }

        static void RenderModule(string name,string folder,List<string> report)
        {
            var type=(BuildingType)Enum.Parse(typeof(BuildingType),name);var def=BuildingCatalog.Get(type);
            var root=new GameObject("Module validation "+name){hideFlags=HideFlags.HideAndDontSave};
            PreviewRenderUtility preview=null;VolumeProfile profile=null;var temporary=new HashSet<UnityEngine.Object>();
            try
            {
                var station=new Station(10,10,127){BatteryCapacity=100,BatteryCharge=75,PowerSatisfaction=1};
                int id=2;
                if(def.RequiresFoundation)for(int x=0;x<def.Width;x++)for(int z=0;z<def.Height;z++)
                    station.Buildings.Add(new Building{Id=id++,Type=BuildingType.Foundation,Origin=new Cell(4+x,4+z),Connected=true});
                var owner=new Building{Id=1,Type=type,Origin=new Cell(4,4),Connected=true,Running=true};station.Buildings.Add(owner);
                var renderer=root.AddComponent<BuildingRenderer>();Invoke(renderer,"Awake");renderer.Bind(station);renderer.Rebuild();
                var moduleRoot=root.transform.Find(station.Label(owner)+" #1");Require(moduleRoot!=null,"Missing runtime module "+name);
                if(def.RequiresFoundation)
                {
                    var modules=Field(renderer,"_modules")as IDictionary;var fx=modules[1];
                    foreach(var pair in new[]{("SolarArray","Wing"),("CommsArray","Dish"),("AtcTower","Radar")})if(name==pair.Item1)
                    {
                        var moving=Field(fx,pair.Item2)as Transform;Require(moving!=null,"Missing animated part "+name);
                        Require(AssetDatabase.GetAssetPath(moving.GetComponent<MeshFilter>().sharedMesh).Contains("IndustrialModules"),"Animated part fell back to a placeholder.");
                        if(name=="SolarArray")Require(Mathf.Abs(moving.localPosition.y-.98f)<.001f,"Solar pivot did not use its model socket.");
                        if(name=="CommsArray")moving.localRotation=Quaternion.Euler(-25f,135f,0);
                        if(name=="AtcTower")moving.localRotation=Quaternion.Euler(0,25f,0);
                    }
                    if(name=="BatteryBank")
                    {
                        var lights=(GameObject[])Field(fx,"ChargeLights");
                        foreach(int level in new[]{0,2,4})
                        {
                            station.BatteryCharge=level*25;Invoke(renderer,"UpdateModules");
                            Require(lights.Count(l=>l.activeSelf)==level,"Battery charge display does not match storage.");
                            foreach(var light in lights)Require(light.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="Universal Render Pipeline/Lit","Battery charge display is not native PBR art.");
                        }
                        station.BatteryCharge=75;
                    }
                    if(name=="IceRefinery")
                    {
                        var glow=(GameObject)Field(fx,"Glow");Require(glow!=null,"Missing refinery windows.");
                        owner.Running=false;Invoke(renderer,"UpdateModules");Require(!glow.activeSelf,"Idle refinery glows.");
                        owner.Running=true;Invoke(renderer,"UpdateModules");Require(glow.activeSelf,"Running refinery windows are dark.");
                    }
                }
                Invoke(renderer,"UpdateModules");
                foreach(var lamp in root.GetComponentsInChildren<FxLight>(true))if(lamp.GetComponent<Light>()==null)Invoke(lamp,"OnEnable");
                LightRig.Daylight=.55f;LightRig.PowerLevel=1f;var rig=root.AddComponent<LightRig>();Invoke(rig,"LateUpdate");
                foreach(var art in root.GetComponentsInChildren<ModuleArtLighting>(true))Invoke(art,"LateUpdate");
                foreach(var fixture in root.GetComponentsInChildren<TileLightFixture>(true))Invoke(fixture,"LateUpdate");
                var bounds=new Bounds(moduleRoot.position,Vector3.one*.05f);
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>())bounds.Encapsulate(r.bounds);
                preview=new PreviewRenderUtility();preview.AddSingleGO(root);
                preview.lights[0].intensity=1.1f;preview.lights[0].color=new Color(1,.87f,.72f);preview.lights[0].transform.rotation=Quaternion.Euler(45,-35,0);
                preview.lights[1].intensity=.22f;preview.lights[1].color=new Color(.42f,.58f,.85f);preview.ambientColor=new Color(.11f,.13f,.17f);
                var camera=preview.camera;camera.cameraType=CameraType.Game;camera.orthographic=true;camera.orthographicSize=Mathf.Max(.90f,Mathf.Max(bounds.size.x,bounds.size.z)*.64f,bounds.size.y*.68f);
                camera.allowHDR=true;camera.nearClipPlane=.01f;camera.farClipPlane=100f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.025f,.04f);
                var focus=bounds.center;camera.transform.position=focus+new Vector3(5f,5.5f,-7f);camera.transform.LookAt(focus);camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                var volume=root.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100f;profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(.95f);bloom.intensity.Override(.55f);
                preview.BeginStaticPreview(new Rect(0,0,700,700));preview.Render(true);var image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                report.Add(name+": runtime loaded native art and rendered in URP.");
            }
            finally
            {
                if(root!=null)
                {
                    foreach(var lamp in root.GetComponentsInChildren<FxLight>(true))FxLight.Active.Remove(lamp);
                    foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh))temporary.Add(f.sharedMesh);
                    foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(m!=null&&!AssetDatabase.Contains(m))temporary.Add(m);
                    var renderer=root.GetComponent<BuildingRenderer>();foreach(string field in new[]{"_meshes","_statusMeshes"})
                    {
                        var values=Field(renderer,field)as IDictionary;foreach(UnityEngine.Object value in values.Values)if(value!=null&&!AssetDatabase.Contains(value))temporary.Add(value);
                    }
                }
                preview?.Cleanup();if(root!=null)UnityEngine.Object.DestroyImmediate(root);foreach(var item in temporary)if(item!=null)UnityEngine.Object.DestroyImmediate(item);if(profile!=null)UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
