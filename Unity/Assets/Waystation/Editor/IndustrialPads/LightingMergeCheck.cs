using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Waystation.View;

namespace Waystation.EditorTools
{
    static class LightingMergeCheck
    {
        [InitializeOnLoadMethod]
        static void OnLoad() => EditorApplication.delayCall += RunIfChanged;

        static string Signature()
        {
            string source = File.ReadAllText("Assets/Waystation/View/BuildingRenderer.cs") + File.ReadAllText("Assets/Waystation/View/LightRig.cs") + File.ReadAllText("Assets/Waystation/View/TileLightFixture.cs");
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(source)));
        }

        static void RunIfChanged()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string report = Path.GetFullPath(Path.Combine(Application.dataPath,"../ArtPreviews/LightingMergeCheck.txt"));
            string signature = Signature();
            if (File.Exists(report) && File.ReadAllText(report).Contains(signature)) return;
            try
            {
                IndustrialPadCheck.Run();
                TileLightingCheck.Run();
                CheckSpotAndPower();
                File.WriteAllText(report,"PASS: modelled pad spotlights and emission bindings preserved.\n" +
                    "PASS: all 64 walkway/foundation connection and power cases.\n" +
                    "PASS: spot aiming, daylight dimming and brownout controls coexist.\nSignature: " + signature + "\n");
                Debug.Log("Lighting merge checks passed.");
            }
            catch (Exception error) { Debug.LogError("Lighting merge check: " + error); }
        }

        static void CheckSpotAndPower()
        {
            var root = new GameObject("Lighting merge validation") { hideFlags = HideFlags.HideAndDontSave };
            float oldDay = LightRig.Daylight, oldPower = LightRig.PowerLevel;
            FxLight lamp = null;
            Material material = null;
            try
            {
                lamp = FxLight.Flood(root.transform,new Vector3(1.2f,1f,1.2f),Color.white,1.5f,3.4f);
                if (lamp.GetComponent<Light>() == null)
                    typeof(FxLight).GetMethod("OnEnable",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lamp,null);
                lamp.ConfigureSpot(new Vector3(-1.2f,-.97f,-1.2f));
                if (!lamp.DayDim || lamp.GetComponent<Light>().type != LightType.Spot) throw new Exception("Floodlight aiming or daylight flag was lost.");
                if (lamp.Brownout(0f,1f) != 1f) throw new Exception("Full-power lamps are unexpectedly dimmed.");
                bool brownout = false;
                for (int i=0;i<20;i++) if(lamp.Brownout(i*.1f,.4f)<1f) brownout=true;
                if(!brownout)throw new Exception("Brownout behaviour was lost.");
                var rig=root.AddComponent<LightRig>();
                var update=typeof(LightRig).GetMethod("LateUpdate",BindingFlags.Instance | BindingFlags.NonPublic);
                LightRig.PowerLevel=1f;LightRig.Daylight=0f;update.Invoke(rig,null);float night=lamp.Current;
                LightRig.Daylight=1f;update.Invoke(rig,null);
                if(Mathf.Abs(lamp.Current/night-.25f)>.001f)throw new Exception("Sunlight no longer dims floodlights.");
                var passiveObject=new GameObject("Passive edge marker");passiveObject.transform.SetParent(root.transform,false);
                material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_EmissionColor",Color.white*4f);
                var lens=passiveObject.AddComponent<MeshRenderer>();lens.sharedMaterial=material;
                var fixture=passiveObject.AddComponent<TileLightFixture>();fixture.Lenses=new Renderer[]{lens};
                LightRig.Daylight=0f;fixture.Bind(true,null);
                var block=new MaterialPropertyBlock();lens.GetPropertyBlock(block);float markerNight=block.GetColor("_EmissionColor").maxColorComponent;
                LightRig.Daylight=1f;typeof(TileLightFixture).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fixture,null);
                lens.GetPropertyBlock(block);
                if(Mathf.Abs(block.GetColor("_EmissionColor").maxColorComponent/markerNight-.25f)>.001f)throw new Exception("Passive foundation markers ignore daylight.");

            }
            finally
            {
                LightRig.Daylight=oldDay;LightRig.PowerLevel=oldPower;
                if(lamp!=null)FxLight.Active.Remove(lamp);
                UnityEngine.Object.DestroyImmediate(root);
                if(material!=null)UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
