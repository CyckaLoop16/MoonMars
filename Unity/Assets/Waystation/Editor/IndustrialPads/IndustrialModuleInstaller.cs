using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Waystation.Sim;
using Waystation.View;
using ArtMesh=Waystation.EditorTools.IndustrialStructureInstaller.ArtMesh;

namespace Waystation.EditorTools
{
    static class IndustrialModuleInstaller
    {
        const string Folder="Assets/Waystation/Art/IndustrialModules";
        const string Prefabs="Assets/Waystation/Resources/Buildings/";
        const string Shared="Assets/Waystation/Art/IndustrialPads/Materials/";
        const string Revision="3";
        public static readonly string[] Names={"SolarArray","BatteryBank","FuelCellGenerator","FuelTank","IceRefinery","PilotLounge","CommsArray","AtcTower","Hangar",
            "SolarArray_Wing","CommsArray_Dish","AtcTower_Radar","BatteryBank_Charge","IceRefinery_Glow"};
        static Dictionary<string,Material> _materials;
        struct Glow {public ModuleArtLighting.Mode Mode;public Vector3? Position;public Glow(ModuleArtLighting.Mode mode,Vector3? position=null){Mode=mode;Position=position;}}

        [InitializeOnLoadMethod]static void OnLoad()
        {
            EditorApplication.delayCall+=BuildIfNeeded;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=BuildIfNeeded;};
        }
        static void BuildIfNeeded()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.Log("Industrial module import queued until Play mode ends.");return;}
            if(AssetDatabase.LoadAssetAtPath<Material>(Shared+"Pad_Hull.mat")==null){Debug.LogWarning("Industrial module import waiting for shared pad materials.");return;}
            bool ready=File.Exists(Folder+"/Revision.txt")&&File.ReadAllText(Folder+"/Revision.txt")==Revision;
            foreach(string name in Names)ready&=File.Exists(Prefabs+name+".prefab");
            if(!ready)Build();
        }
        [MenuItem("Waystation/Art/Rebuild Industrial Modules")]
        public static void Build()
        {
            try
            {
                Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Textures");AssetDatabase.Refresh();
                Materials();
                foreach(string name in Names)
                {
                    var mesh=new ArtMesh();var glow=new Dictionary<string,Glow>();Vector3? moving=null;
                    if(Enum.TryParse<BuildingType>(name,out var type))
                    {
                        var def=BuildingCatalog.Get(type);
                        float width=def.Width/BuildingRenderer.DesignScale,depth=def.Height/BuildingRenderer.DesignScale;
                        Skid(mesh,width,depth);
                        if(type!=BuildingType.Hangar)AreaLamp(mesh,glow,def);
                        switch(type)
                        {
                            case BuildingType.SolarArray: SolarBase(mesh,glow);moving=new Vector3(0,.98f,0);break;
                            case BuildingType.BatteryBank: Battery(mesh);break;
                            case BuildingType.FuelCellGenerator: Generator(mesh,glow);break;
                            case BuildingType.FuelTank: Tank(mesh,glow);break;
                            case BuildingType.IceRefinery: Refinery(mesh,glow);break;
                            case BuildingType.PilotLounge: Lounge(mesh,glow);break;
                            case BuildingType.CommsArray: Comms(mesh,glow);moving=new Vector3(0,1.25f,0);break;
                            case BuildingType.AtcTower: Tower(mesh,glow);moving=new Vector3(0,2.47f,0);break;
                            case BuildingType.Hangar: Hangar(mesh,glow);break;
                        }
                    }
                    else if(name=="SolarArray_Wing")SolarWing(mesh);
                    else if(name=="CommsArray_Dish")Dish(mesh);
                    else if(name=="AtcTower_Radar")Radar(mesh);
                    else if(name=="BatteryBank_Charge")
                    {
                        mesh.Box(Vector3.zero,new Vector3(.024f,.12f,.22f),"Dark");
                        mesh.Box(new Vector3(.014f,0,0),new Vector3(.006f,.085f,.18f),"Charge");
                    }
                    else if(name=="IceRefinery_Glow")
                    {
                        foreach(float y in new[]{.6f,1.1f})
                        {
                            mesh.Box(new Vector3(.45f,y,-.56f),new Vector3(.15f,.21f,.006f),"RunAmber");
                            mesh.Box(new Vector3(.101f,y,-.2f),new Vector3(.006f,.21f,.15f),"RunAmber");
                        }
                        glow["RunAmber"]=new Glow(ModuleArtLighting.Mode.Running,new Vector3(.45f,.9f,-.75f));
                    }
                    Details(name,mesh);
                    Save(name,mesh,glow,moving);
                }
                AssetDatabase.SaveAssets();IndustrialModuleCheck.Run();
                File.WriteAllText(Folder+"/Revision.txt",Revision);AssetDatabase.ImportAsset(Folder+"/Revision.txt");
                Debug.Log("Industrial modules installed and verified.");
            }
            catch(Exception error){Debug.LogError("Industrial module installation: "+error);if(Application.isBatchMode)throw;}
        }

        static void Materials()
        {
            _materials=new Dictionary<string,Material>();
            foreach(string key in new[]{"Hull","Steel","Deck","Dark","Teal","Orange","Yellow","Paint"})_materials[key]=AssetDatabase.LoadAssetAtPath<Material>(Shared+"Pad_"+key+".mat");
            Material Lit(string key,Color color,Color emission,bool screen=false)
            {
                string path=Folder+"/Materials/"+key+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);bool create=mat==null;
                if(create)mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=key};
                mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.35f);mat.SetFloat("_Metallic",.1f);
                if(emission.maxColorComponent>0)
                {
                    mat.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Waystation/Art/IndustrialPads/Textures/"+(screen?"Screen_Emission.png":"Indicator_Emission.png")));
                    mat.SetColor("_EmissionColor",emission);
                    mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeDirectEmission;
                    mat.EnableKeyword("_EMISSION");
                }
                mat.enableInstancing=true;
                if(create)AssetDatabase.CreateAsset(mat,path);else EditorUtility.SetDirty(mat);
                _materials[key]=mat;return mat;
            }
            var dark=new Color(.025f,.03f,.035f);
            Lit("ModuleLamp",dark,BuildingRenderer.Sodium*3f);
            Lit("Work_A",dark,MeshBuilder.Hex("#cfe0ff")*3f);Lit("Work_B",dark,MeshBuilder.Hex("#cfe0ff")*3f);
            Lit("Screen",dark,MeshBuilder.Hex("#9fc4d1")*1.8f,true);
            Lit("WindowWarm",dark,MeshBuilder.Hex("#ffc88a")*1.7f);
            Lit("ControlGlass",dark,MeshBuilder.Hex("#9feaff")*1.8f);
            Lit("LoungeNeon",dark,MeshBuilder.Hex("#ff3ab0")*2f);
            Lit("SignalRed",dark,MeshBuilder.Hex("#ff3c28")*2f);Lit("SignalGreen",dark,MeshBuilder.Hex("#56dd86")*2f);
            Lit("Charge",dark,MeshBuilder.Hex("#3cff7a")*2f);
            Lit("RunAmber",dark,BuildingRenderer.Sodium*3.5f,true);
            Lit("Ice",MeshBuilder.Hex("#a9cfe0"),Color.black).SetFloat("_Smoothness",.72f);
            var solar=Lit("SolarCells",Color.white,Color.black);solar.SetFloat("_Smoothness",.68f);solar.SetFloat("_Metallic",.35f);
            var texture=new Texture2D(512,512,TextureFormat.RGB24,false);
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)
            {
                int cx=x%128,cy=y%64;bool gap=cx<4||cx>123||cy<3||cy>60;
                bool corner=(cx<13||cx>114)&&(cy<10||cy>53);bool bus=cx%32==16;
                float grain=Mathf.PerlinNoise(x*.21f,y*.21f)*.025f;
                var color=gap||corner?new Color(.09f,.11f,.13f):bus?new Color(.28f,.36f,.46f):new Color(.025f+grain,.07f+grain,.145f+grain);
                texture.SetPixel(x,y,color);
            }
            texture.Apply();string texturePath=Folder+"/Textures/SolarCells_BaseColor.png";File.WriteAllBytes(texturePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.SaveAndReimport();
            solar.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));EditorUtility.SetDirty(solar);
        }

        static void Skid(ArtMesh b,float width,float depth)
        {
            b.Slab(width-.14f,depth-.14f,.055f,.012f,.07f,"Dark");
            foreach(float x in new[]{-(width*.5f-.17f),width*.5f-.17f})
                b.Box(new Vector3(x,.052f,0),new Vector3(.07f,.075f,depth-.25f),"Steel");
        }
        static void AreaLamp(ArtMesh b,Dictionary<string,Glow> glow,BuildingDef def)
        {
            var p=new Vector3(-(def.Width/BuildingRenderer.DesignScale*.5f-.2f),.9f,-(def.Height/BuildingRenderer.DesignScale*.5f-.2f));
            b.Box(new Vector3(p.x,.43f,p.z),new Vector3(.035f,.82f,.035f),"Steel");
            b.Box(p-new Vector3(0,.02f,0),new Vector3(.16f,.07f,.13f),"Dark");
            b.Box(p+new Vector3(0,.025f,0),new Vector3(.12f,.015f,.09f),"ModuleLamp");
            glow["ModuleLamp"]=new Glow(ModuleArtLighting.Mode.AreaLamp,p);
        }
        static void SolarBase(ArtMesh b,Dictionary<string,Glow> g)
        {
            b.Slab(.58f,.58f,.10f,.07f,.15f,"Hull");b.Cylinder(new Vector3(0,.52f,0),.085f,.74f,"Steel",16);
            b.Cylinder(new Vector3(0,.87f,0),.17f,.11f,"Teal",20);b.Cylinder(new Vector3(0,.95f,0),.105f,.06f,"Steel",16);
            foreach(float side in new[]{-1f,1f})b.Beam(new Vector3(side*.22f,.12f,.20f),new Vector3(0,.55f,0),.025f,"Steel",6);
            b.SlabAt(new Vector3(.55f,0,.55f),.5f,.42f,.04f,.07f,.32f,"Hull");
            b.Box(new Vector3(.55f,.23f,.329f),new Vector3(.22f,.09f,.015f),"Screen");g["Screen"]=new Glow(ModuleArtLighting.Mode.Powered);
            for(int i=0;i<6;i++)b.Box(new Vector3(.39f+i*.06f,.339f,.55f),new Vector3(.022f,.035f,.3f),"Steel");
            b.Beam(new Vector3(0,.2f,0),new Vector3(.55f,.14f,.35f),.023f,"Orange",8);
        }
        static void SolarWing(ArtMesh b)
        {
            b.Box(Vector3.zero,new Vector3(1.82f,.055f,.065f),"Steel");
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*.48f;
                b.Box(new Vector3(x,-.012f,0),new Vector3(.78f,.035f,.68f),"Dark");
                b.Surface(new[]{new Vector3(x-.37f,.01f,-.31f),new Vector3(x-.37f,.01f,.31f),new Vector3(x+.37f,.01f,.31f),new Vector3(x+.37f,.01f,-.31f)},Vector3.up,"SolarCells");
                foreach(float z in new[]{-.35f,.35f})b.Box(new Vector3(x,0,z),new Vector3(.81f,.06f,.025f),"Hull");
                foreach(float xx in new[]{x-.397f,x+.397f})b.Box(new Vector3(xx,0,0),new Vector3(.025f,.055f,.70f),"Steel");
                b.Beam(new Vector3(side*.13f,-.05f,0),new Vector3(x,-.025f,.28f),.013f,"Steel",6);
                b.Beam(new Vector3(side*.13f,-.05f,0),new Vector3(x,-.025f,-.28f),.013f,"Steel",6);
            }
        }
        static void Battery(ArtMesh b)
        {
            b.Slab(.72f,1.72f,.055f,.08f,.18f,"Steel");
            for(int i=0;i<4;i++)foreach(float x in new[]{-.16f,.16f})
            {
                float z=-.60f+i*.39f;
                b.SlabAt(new Vector3(x,0,z),.27f,.34f,.025f,.18f,.71f,"Hull");
                b.Box(new Vector3(x,.43f,z-.174f),new Vector3(.21f,.27f,.015f),"Teal");
                b.Box(new Vector3(x,.72f,z),new Vector3(.23f,.035f,.22f),"Dark");
                for(int j=-1;j<=1;j++)b.Box(new Vector3(x+j*.055f,.746f,z),new Vector3(.018f,.025f,.20f),"Steel");
            }
            foreach(float x in new[]{-.34f,.34f})b.Box(new Vector3(x,.43f,0),new Vector3(.035f,.59f,1.58f),"Dark");
            b.Box(new Vector3(0,.40f,-.84f),new Vector3(.64f,.45f,.06f),"Orange");
            for(int i=0;i<4;i++)b.Box(new Vector3(.535f,.3f,-.45f+i*.3f),new Vector3(.035f,.16f,.25f),"Dark");
            b.Beam(new Vector3(-.31f,.13f,-.75f),new Vector3(-.31f,.13f,.75f),.023f,"Orange",8);
            foreach(float x in new[]{-.19f,.19f})b.Cylinder(new Vector3(x,.79f,.68f),.04f,.10f,x<0?"Dark":"Orange",12);
        }
        static void Generator(ArtMesh b,Dictionary<string,Glow> g)
        {
            foreach(float z in new[]{-.55f,0f,.55f})
            {
                b.Cylinder(new Vector3(-.07f,.40f,z),.19f,.52f,"Hull",20);
                foreach(float y in new[]{.18f,.35f,.52f,.67f})b.Cylinder(new Vector3(-.07f,y,z),.207f,.035f,"Steel",20);
                b.Box(new Vector3(.16f,.38f,z),new Vector3(.09f,.38f,.16f),"Teal");
            }
            b.Beam(new Vector3(-.30f,.17f,-.80f),new Vector3(-.30f,.17f,.80f),.025f,"Orange",10);
            b.Cylinder(new Vector3(.25f,.52f,.75f),.055f,.82f,"Orange",12);b.Cylinder(new Vector3(.25f,.94f,.75f),.074f,.04f,"Steel",12);
            b.Box(new Vector3(.325f,.35f,0),new Vector3(.08f,.23f,.62f),"Dark");
            b.Box(new Vector3(.369f,.35f,0),new Vector3(.01f,.13f,.48f),"RunAmber");
            g["RunAmber"]=new Glow(ModuleArtLighting.Mode.Running,new Vector3(.45f,.35f,0));
        }
        static void Tank(ArtMesh b,Dictionary<string,Glow> g)
        {
            foreach(float x in new[]{-.48f,.48f})foreach(float z in new[]{-.48f,.48f})
            {
                b.Box(new Vector3(x,.47f,z),new Vector3(.10f,.80f,.10f),"Steel");
                b.Box(new Vector3(x,.10f,z),new Vector3(.24f,.08f,.24f),"Hull");
            }
            b.Sphere(new Vector3(0,1.05f,0),.72f,"Orange",20,40);
            b.Cylinder(new Vector3(0,1.05f,0),.729f,.055f,"Steel",40);
            b.Cylinder(new Vector3(0,1.80f,0),.12f,.08f,"Steel",20);
            b.Cylinder(new Vector3(0,1.90f,0),.055f,.08f,"SignalRed",16);
            g["SignalRed"]=new Glow(ModuleArtLighting.Mode.Powered,new Vector3(0,1.9f,0));
            b.Beam(new Vector3(.65f,.17f,-.45f),new Vector3(.65f,.56f,-.45f),.04f,"Orange",12);
            b.Beam(new Vector3(.65f,.56f,-.45f),new Vector3(.40f,.56f,-.45f),.035f,"Steel",12);
            b.Cylinder(new Vector3(.65f,.65f,-.45f),.11f,.025f,"Teal",16);
        }
        static void Refinery(ArtMesh b,Dictionary<string,Glow> g)
        {
            b.Cylinder(new Vector3(.45f,.78f,-.2f),.34f,1.30f,"Hull",32);
            foreach(float y in new[]{.2f,.43f,.84f,1.38f})b.Cylinder(new Vector3(.45f,y,-.2f),.355f,.045f,"Steel",32);
            b.Cylinder(new Vector3(.45f,1.52f,-.2f),.07f,.25f,"Orange",16);b.Cylinder(new Vector3(.45f,1.66f,-.2f),.09f,.04f,"Steel",16);
            foreach(float y in new[]{.6f,1.1f})
            {
                b.Box(new Vector3(.45f,y,-.545f),new Vector3(.21f,.27f,.025f),"Dark");
                b.Box(new Vector3(.107f,y,-.2f),new Vector3(.025f,.27f,.21f),"Dark");
            }
            b.SlabAt(new Vector3(-.48f,0,.56f),.58f,.90f,.06f,.10f,.70f,"Steel");
            b.SlabAt(new Vector3(-.48f,0,.56f),.49f,.80f,.04f,.65f,.73f,"Dark");
            foreach(var offset in new[]{new Vector3(-.12f,0,-.2f),new Vector3(.11f,0,0),new Vector3(-.09f,0,.22f)})b.Sphere(new Vector3(-.48f,.76f,.56f)+offset,.14f,"Ice",6,10);
            for(int i=0;i<8;i++)b.Box(new Vector3(-.84f,.57f,.12f+i*.13f),new Vector3(.035f,.9f,.022f),"Steel");
            b.Beam(new Vector3(-.45f,.55f,.40f),new Vector3(.45f,.55f,.40f),.035f,"Orange",10);
            b.Beam(new Vector3(.45f,.55f,.40f),new Vector3(.45f,.55f,.06f),.035f,"Orange",10);
            b.SlabAt(new Vector3(-.45f,0,-1.11f),.55f,.43f,.05f,.08f,.45f,"Hull");
            b.Box(new Vector3(-.45f,.31f,-1.335f),new Vector3(.36f,.17f,.018f),"Screen");g["Screen"]=new Glow(ModuleArtLighting.Mode.Powered);
        }
        static void Lounge(ArtMesh b,Dictionary<string,Glow> g)
        {
            b.Slab(1.65f,1.47f,.14f,.08f,.84f,"Hull");b.Slab(1.70f,1.52f,.14f,.09f,.16f,"Teal");
            b.Slab(1.62f,1.44f,.13f,.84f,.93f,"Steel");
            foreach(float side in new[]{-1f,1f})for(int i=-1;i<=1;i++)
            {
                b.Box(new Vector3(i*.38f,.53f,side*.743f),new Vector3(.33f,.19f,.025f),"Dark");
                b.Box(new Vector3(i*.38f,.53f,side*.76f),new Vector3(.28f,.135f,.008f),"WindowWarm");
                b.Box(new Vector3(side*.833f,.53f,i*.32f),new Vector3(.025f,.19f,.27f),"Dark");
                b.Box(new Vector3(side*.85f,.53f,i*.32f),new Vector3(.008f,.13f,.22f),"WindowWarm");
            }
            g["WindowWarm"]=new Glow(ModuleArtLighting.Mode.Powered);
            b.Box(new Vector3(.50f,.30f,-.77f),new Vector3(.31f,.48f,.065f),"Orange");
            b.Box(new Vector3(.50f,.31f,-.807f),new Vector3(.20f,.35f,.012f),"Dark");
            b.SlabAt(new Vector3(-.43f,0,-.30f),.42f,.32f,.03f,.94f,1.10f,"Orange");
            for(int i=0;i<5;i++)b.Box(new Vector3(-.57f+i*.07f,1.115f,-.30f),new Vector3(.025f,.025f,.24f),"Steel");
            b.Box(new Vector3(.50f,1.0f,.4f),new Vector3(.44f,.16f,.05f),"Dark");
            for(int i=0;i<3;i++)b.Box(new Vector3(.35f+i*.15f,1.0f,.433f),new Vector3(.065f,.10f,.008f),"LoungeNeon");
            g["LoungeNeon"]=new Glow(ModuleArtLighting.Mode.Powered,new Vector3(.5f,1f,.4f));
        }
        static void Comms(ArtMesh b,Dictionary<string,Glow> g)
        {
            foreach(float x in new[]{-.17f,.17f})foreach(float z in new[]{-.17f,.17f})b.Beam(new Vector3(x,.08f,z),new Vector3(x*.6f,1.21f,z*.6f),.025f,"Steel",8);
            for(int i=0;i<4;i++)
            {
                float y=.20f+i*.26f;
                b.Box(new Vector3(0,y,0),new Vector3(.32f,.025f,.32f),"Dark");
                b.Beam(new Vector3(-.14f,y,-.14f),new Vector3(.12f,y+.25f,-.12f),.012f,"Steel",6);
            }
            b.Cylinder(new Vector3(0,1.20f,0),.15f,.09f,"Steel",20);
            b.SlabAt(new Vector3(-.5f,0,.5f),.6f,.6f,.045f,.07f,.44f,"Hull");
            b.Box(new Vector3(-.5f,.27f,.19f),new Vector3(.38f,.11f,.018f),"Screen");g["Screen"]=new Glow(ModuleArtLighting.Mode.Powered);
            b.Cylinder(new Vector3(-.5f,.50f,.5f),.05f,.07f,"SignalRed",16);g["SignalRed"]=new Glow(ModuleArtLighting.Mode.Powered,new Vector3(-.5f,.5f,.5f));
        }
        static void Dish(ArtMesh b)
        {
            const int sides=40;const float r=.65f;
            Vector3 P(float radius,int i,float back=0)=>new Vector3(Mathf.Cos(i*Mathf.PI*2/sides)*radius,Mathf.Sin(i*Mathf.PI*2/sides)*radius,-.18f+.18f*(radius/r)*(radius/r)+back);
            for(int i=0;i<sides;i++)
            {
                int j=i+1;b.Surface(new[]{new Vector3(0,0,-.18f),P(.20f,i),P(.20f,j)},Vector3.forward,"Hull");
                foreach(var pair in new[]{new Vector2(.20f,.43f),new Vector2(.43f,r)})
                {
                    b.Surface(new[]{P(pair.x,i),P(pair.y,i),P(pair.y,j),P(pair.x,j)},Vector3.forward,"Hull");
                    b.Surface(new[]{P(pair.x,i,-.035f),P(pair.y,i,-.035f),P(pair.y,j,-.035f),P(pair.x,j,-.035f)},Vector3.back,"Steel");
                }
                b.Beam(P(r,i),P(r,j),.012f,"Steel",6);
            }
            foreach(float angle in new[]{0f,120f,240f})
            {
                float a=angle*Mathf.Deg2Rad;b.Beam(new Vector3(Mathf.Cos(a)*.55f,Mathf.Sin(a)*.55f,-.02f),new Vector3(0,0,.40f),.012f,"Dark",6);
            }
            b.Box(new Vector3(0,0,.41f),new Vector3(.10f,.10f,.12f),"Orange");
            b.Box(new Vector3(0,-.10f,-.24f),new Vector3(.15f,.20f,.14f),"Steel");
        }
        static void Tower(ArtMesh b,Dictionary<string,Glow> g)
        {
            b.Slab(1.30f,1.30f,.10f,.08f,.43f,"Hull");b.Cylinder(new Vector3(0,1.05f,0),.28f,1.25f,"Hull",8);
            foreach(float y in new[]{.45f,.9f,1.50f})b.Cylinder(new Vector3(0,y,0),.295f,.04f,"Steel",8);
            b.Cylinder(new Vector3(0,1.70f,0),.63f,.09f,"Steel",8);
            b.Cylinder(new Vector3(0,1.93f,0),.60f,.37f,"ControlGlass",8);g["ControlGlass"]=new Glow(ModuleArtLighting.Mode.Powered,new Vector3(0,1.9f,0));
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;b.Box(new Vector3(Mathf.Cos(a)*.60f,1.93f,Mathf.Sin(a)*.60f),new Vector3(.04f,.4f,.04f),"Steel");}
            b.Cylinder(new Vector3(0,2.16f,0),.68f,.10f,"Steel",8);b.Cylinder(new Vector3(0,2.23f,0),.52f,.05f,"Hull",8);
            b.Cylinder(new Vector3(0,2.36f,0),.05f,.23f,"Steel",12);
            b.Box(new Vector3(.6f,.45f,-.6f),new Vector3(.11f,.11f,.11f),"SignalGreen");g["SignalGreen"]=new Glow(ModuleArtLighting.Mode.Powered,new Vector3(.6f,.45f,-.6f));
            b.Box(new Vector3(0,.28f,-.66f),new Vector3(.35f,.30f,.04f),"Orange");
        }
        static void Radar(ArtMesh b)
        {
            b.Box(Vector3.zero,new Vector3(1.04f,.19f,.10f),"Hull");
            b.Box(new Vector3(0,0,.06f),new Vector3(.92f,.12f,.025f),"Dark");
            for(int i=-5;i<=5;i++)b.Box(new Vector3(i*.083f,0,.079f),new Vector3(.018f,.105f,.015f),"Steel");
            b.Cylinder(new Vector3(0,-.09f,0),.11f,.09f,"Teal",16);
        }
        static void Hangar(ArtMesh b,Dictionary<string,Glow> g)
        {
            b.Slab(3.94f,3.94f,.25f,-.24f,0f,"Steel");b.Slab(3.64f,3.64f,.18f,-.025f,.004f,"Deck");
            foreach(float side in new[]{-1f,1f})
            {
                b.Box(new Vector3(side*1.84f,.38f,0),new Vector3(.18f,.76f,3.6f),"Hull");
                foreach(float z in new[]{-1.5f,0f,1.5f})b.Box(new Vector3(side*1.84f,.9f,z),new Vector3(.13f,1.8f,.13f),"Steel");
                b.Box(new Vector3(side*1.84f,1.77f,0),new Vector3(.20f,.13f,3.6f),"Orange");
                for(int i=0;i<4;i++)b.Box(new Vector3(side*1.73f,.3f,-.9f+i*.6f),new Vector3(.06f,.32f,.42f),i%2==0?"Teal":"Dark");
                for(int i=0;i<10;i++)b.Box(new Vector3(side*1.57f,.013f,-1.35f+i*.3f),new Vector3(.12f,.014f,.17f),"Yellow");
            }
            b.Box(new Vector3(0,.63f,1.82f),new Vector3(3.7f,1.26f,.20f),"Hull");
            b.Box(new Vector3(0,1.80f,.70f),new Vector3(3.7f,.18f,.20f),"Teal");
            b.Box(new Vector3(.6f,1.64f,.7f),new Vector3(.40f,.25f,.36f),"Orange");
            b.Beam(new Vector3(.6f,1.55f,.7f),new Vector3(.6f,1.25f,.7f),.018f,"Steel",8);
            b.Box(new Vector3(.64f,1.23f,.7f),new Vector3(.13f,.07f,.07f),"Yellow");
            foreach(float side in new[]{-1f,1f})
            {
                string key=side<0?"Work_A":"Work_B";var p=new Vector3(side*1.55f,1.2f,1.55f);
                b.Box(p,new Vector3(.26f,.13f,.18f),"Dark");b.Box(p+new Vector3(0,.075f,0),new Vector3(.20f,.014f,.12f),key);g[key]=new Glow(ModuleArtLighting.Mode.AreaLamp,p);
            }
            b.Box(new Vector3(0,1.10f,-1.75f),new Vector3(.12f,.12f,.12f),"SignalRed");g["SignalRed"]=new Glow(ModuleArtLighting.Mode.Powered,new Vector3(0,1.1f,-1.75f));
            b.Box(new Vector3(-.22f,.015f,.08f),new Vector3(.20f,.014f,1.1f),"Paint");b.Box(new Vector3(.08f,.015f,-.4f),new Vector3(.80f,.014f,.20f),"Paint");
            b.Box(new Vector3(1.7f,.38f,1.7f),new Vector3(.045f,.76f,.045f),"Steel");
        }

        // Details are authored in the same design units as the machinery and its FX sockets.
        static void Details(string name,ArtMesh b)
        {
            void Bolts(Vector3 center,float width,float depth,float y)
            {
                foreach(float x in new[]{-width*.5f,width*.5f})foreach(float z in new[]{-depth*.5f,depth*.5f})
                    b.Cylinder(center+new Vector3(x,y,z),.018f,.018f,"Steel",6);
            }
            void Vent(Vector3 center,int count,float pitch,float length)
            {
                b.Box(center,new Vector3(count*pitch+.05f,.025f,length+.05f),"Dark");
                for(int i=0;i<count;i++)b.Box(center+new Vector3((i-(count-1)*.5f)*pitch,.018f,0),new Vector3(.018f,.025f,length),"Steel");
            }
            void Ladder(float x,float z,float top)
            {
                foreach(float dx in new[]{-.075f,.075f})b.Box(new Vector3(x+dx,top*.5f,z),new Vector3(.018f,top,.025f),"Steel");
                for(float y=.17f;y<top;y+=.15f)b.Box(new Vector3(x,y,z-.01f),new Vector3(.17f,.018f,.025f),"Steel");
            }
            switch(name)
            {
                case "SolarArray":
                    b.Cylinder(new Vector3(0,.27f,0),.14f,.18f,"Dark",24);
                    foreach(float y in new[]{.21f,.32f,.43f,.66f})b.Cylinder(new Vector3(0,y,0),.10f,.025f,"Steel",24);
                    Bolts(Vector3.zero,.42f,.42f,.165f);
                    foreach(float side in new[]{-1f,1f})
                    {
                        b.Cylinder(new Vector3(side*.22f,.13f,.20f),.055f,.045f,"Dark",12);
                        b.Beam(new Vector3(side*.22f,.16f,.20f),new Vector3(side*.06f,.72f,0),.018f,"Orange",10);
                    }
                    b.Box(new Vector3(.55f,.23f,.327f),new Vector3(.43f,.21f,.015f),"Steel");
                    b.Box(new Vector3(.55f,.23f,.314f),new Vector3(.22f,.09f,.015f),"Screen");
                    foreach(float z in new[]{.43f,.65f})b.Beam(new Vector3(.32f,.13f,z),new Vector3(.12f,.13f,z),.018f,"Dark",8);
                    break;
                case "SolarArray_Wing":
                    foreach(float side in new[]{-1f,1f})
                    {
                        float x=side*.48f;
                        foreach(float z in new[]{-.22f,0f,.22f})b.Box(new Vector3(x,-.04f,z),new Vector3(.75f,.035f,.018f),"Steel");
                        b.Box(new Vector3(x,.017f,0),new Vector3(.018f,.012f,.63f),"Steel");
                        Bolts(new Vector3(x,0,0),.76f,.66f,.034f);
                    }
                    b.Cylinder(new Vector3(0,-.02f,0),.09f,.075f,"Dark",20);
                    break;
                case "BatteryBank":
                    foreach(float side in new[]{-1f,1f})
                    {
                        b.SlabAt(new Vector3(side*.43f,0,0),.13f,1.62f,.018f,.16f,.57f,"Teal");
                        b.Box(new Vector3(side*.505f,.37f,0),new Vector3(.025f,.19f,1.46f),"Dark");
                        for(int i=0;i<10;i++)b.Box(new Vector3(side*.523f,.37f,-.65f+i*.145f),new Vector3(.015f,.13f,.035f),"Steel");
                        b.Beam(new Vector3(side*.28f,.79f,-.64f),new Vector3(side*.28f,.79f,.64f),.018f,side<0?"Orange":"Dark",8);
                    }
                    foreach(float z in new[]{-.78f,.78f})b.Box(new Vector3(0,.81f,z),new Vector3(.67f,.04f,.04f),"Steel");
                    for(int i=0;i<4;i++)
                    {
                        float z=-.60f+i*.39f;
                        foreach(float x in new[]{-.16f,.16f})
                        {
                            b.Box(new Vector3(x,.57f,z-.183f),new Vector3(.12f,.025f,.014f),"Steel");
                            Bolts(new Vector3(x,0,z),.20f,.25f,.768f);
                        }
                    }
                    break;
                case "FuelCellGenerator":
                    b.Slab(.96f,1.78f,.055f,.08f,.15f,"Steel");
                    foreach(float z in new[]{-.55f,0f,.55f})
                    {
                        b.Cylinder(new Vector3(-.07f,.72f,z),.11f,.05f,"Dark",20);
                        Bolts(new Vector3(-.07f,0,z),.21f,.21f,.75f);
                        b.Beam(new Vector3(-.35f,.29f,z),new Vector3(-.07f,.29f,z),.025f,"Orange",10);
                        b.Beam(new Vector3(-.35f,.53f,z),new Vector3(-.07f,.53f,z),.022f,"Steel",10);
                    }
                    foreach(float y in new[]{.29f,.53f})b.Beam(new Vector3(-.35f,y,-.77f),new Vector3(-.35f,y,.77f),.035f,"Steel",12);
                    foreach(float y in new[]{.77f,.84f,.90f})b.Cylinder(new Vector3(.25f,y,.75f),.069f,.018f,"Steel",16);
                    b.SlabAt(new Vector3(.33f,0,-.72f),.29f,.28f,.025f,.17f,.58f,"Teal");
                    Vent(new Vector3(.33f,.60f,-.72f),4,.05f,.21f);
                    break;
                case "FuelTank":
                    foreach(float side in new[]{-1f,1f})
                    {
                        b.Beam(new Vector3(-.48f,.17f,side*.48f),new Vector3(.48f,.73f,side*.48f),.022f,"Steel",8);
                        b.Beam(new Vector3(.48f,.17f,side*.48f),new Vector3(-.48f,.73f,side*.48f),.022f,"Steel",8);
                    }
                    Ladder(-.76f,-.12f,1.24f);
                    b.Box(new Vector3(-.70f,1.23f,-.12f),new Vector3(.24f,.035f,.34f),"Steel");
                    foreach(float z in new[]{-.30f,.06f})b.Box(new Vector3(-.79f,1.40f,z),new Vector3(.025f,.35f,.025f),"Steel");
                    b.Beam(new Vector3(-.79f,1.56f,-.30f),new Vector3(-.79f,1.56f,.06f),.012f,"Yellow",8);
                    b.Beam(new Vector3(.65f,.18f,-.45f),new Vector3(.65f,.18f,.55f),.045f,"Steel",12);
                    foreach(float z in new[]{-.35f,.4f})b.Cylinder(new Vector3(.65f,.19f,z),.08f,.06f,"Dark",16);
                    break;
                case "IceRefinery":
                    Ladder(.84f,-.20f,1.38f);
                    b.SlabAt(new Vector3(.23f,0,1.04f),.93f,.68f,.05f,.12f,.22f,"Steel");
                    foreach(float x in new[]{-.02f,.48f})
                    {
                        b.Cylinder(new Vector3(x,.52f,1.04f),.19f,.59f,"Teal",20);
                        foreach(float y in new[]{.26f,.76f})b.Cylinder(new Vector3(x,y,1.04f),.205f,.04f,"Steel",20);
                        b.Beam(new Vector3(x,.83f,1.04f),new Vector3(x,.98f,1.04f),.022f,"Orange",10);
                        b.Beam(new Vector3(x,.98f,1.04f),new Vector3(x,.98f,.46f),.022f,"Orange",10);
                    }
                    b.Beam(new Vector3(.45f,.98f,.46f),new Vector3(.45f,.98f,.12f),.035f,"Steel",12);
                    foreach(float z in new[]{.17f,.95f})b.Box(new Vector3(-.84f,.57f,z),new Vector3(.065f,.045f,.13f),"Dark");
                    Vent(new Vector3(-.45f,.48f,-1.11f),6,.065f,.30f);
                    break;
                case "PilotLounge":
                    foreach(float x in new[]{-.64f,0f,.64f})b.Box(new Vector3(x,.48f,-.75f),new Vector3(.035f,.64f,.04f),"Steel");
                    foreach(float z in new[]{-.55f,.55f})b.Box(new Vector3(.825f,.48f,z),new Vector3(.035f,.64f,.04f),"Steel");
                    b.SlabAt(new Vector3(.3f,0,-.28f),.52f,.48f,.04f,.94f,1.02f,"Teal");
                    Vent(new Vector3(.3f,1.04f,-.28f),6,.067f,.36f);
                    b.Beam(new Vector3(.3f,.97f,-.02f),new Vector3(.3f,.97f,.4f),.025f,"Steel",10);
                    b.Box(new Vector3(.50f,.12f,-.85f),new Vector3(.42f,.06f,.16f),"Steel");
                    b.Box(new Vector3(.50f,.08f,-.91f),new Vector3(.47f,.04f,.12f),"Yellow");
                    foreach(float z in new[]{-.53f,.53f})Bolts(new Vector3(0,0,z),1.34f,.02f,.951f);
                    break;
                case "CommsArray":
                    foreach(float x in new[]{-.15f,.15f})b.Beam(new Vector3(x,.20f,.14f),new Vector3(-x,1.0f,.11f),.014f,"Steel",6);
                    Ladder(.20f,.19f,1.08f);
                    b.Cylinder(new Vector3(0,1.10f,0),.20f,.05f,"Teal",24);
                    for(int i=0;i<6;i++)b.Box(new Vector3(-.70f+i*.08f,.465f,.50f),new Vector3(.025f,.035f,.44f),"Steel");
                    b.SlabAt(new Vector3(.55f,0,.55f),.43f,.48f,.035f,.08f,.49f,"Teal");
                    Vent(new Vector3(.55f,.51f,.55f),5,.065f,.34f);
                    b.Beam(new Vector3(-.50f,.12f,.25f),new Vector3(.55f,.12f,.25f),.022f,"Orange",10);
                    break;
                case "CommsArray_Dish":
                    for(int i=0;i<12;i++)
                    {
                        float a=i*Mathf.PI/6;
                        Vector3 Back(float radius)=>new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,-.24f+.18f*(radius/.65f)*(radius/.65f));
                        foreach(var radii in new[]{new Vector2(0,.22f),new Vector2(.22f,.44f),new Vector2(.44f,.62f)})
                            b.Beam(Back(radii.x),Back(radii.y),.014f,"Steel",6);
                        b.Box(Back(.62f),new Vector3(.025f,.025f,.025f),"Dark");
                    }
                    break;
                case "AtcTower":
                    Ladder(-.31f,0,1.58f);
                    foreach(float z in new[]{-.4f,.4f})b.Box(new Vector3(.66f,.26f,z),new Vector3(.16f,.27f,.24f),"Teal");
                    Vent(new Vector3(.66f,.405f,.4f),3,.045f,.16f);
                    b.Beam(new Vector3(.32f,.09f,.45f),new Vector3(.32f,1.60f,.45f),.022f,"Orange",10);
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4;
                        b.Beam(new Vector3(Mathf.Cos(a)*.28f,1.46f,Mathf.Sin(a)*.28f),new Vector3(Mathf.Cos(a)*.58f,1.68f,Mathf.Sin(a)*.58f),.018f,"Steel",8);
                    }
                    b.Cylinder(new Vector3(.28f,2.30f,.10f),.06f,.08f,"Teal",16);
                    break;
                case "AtcTower_Radar":
                    foreach(float x in new[]{-.47f,.47f})b.Box(new Vector3(x,0,-.065f),new Vector3(.035f,.22f,.025f),"Steel");
                    b.Beam(new Vector3(-.45f,-.08f,-.07f),new Vector3(.45f,.08f,-.07f),.012f,"Steel",6);
                    break;
                case "Hangar":
                    foreach(float side in new[]{-1f,1f})
                    {
                        for(int i=0;i<5;i++)
                        {
                            float z=-1.40f+i*.62f;
                            b.Beam(new Vector3(side*1.83f,.75f,z),new Vector3(side*1.83f,1.65f,z+.52f),.025f,"Steel",8);
                        }
                        for(int i=0;i<3;i++)
                        {
                            float z=-.95f+i*.70f;
                            b.Box(new Vector3(side*1.61f,.27f,z),new Vector3(.24f,.42f,.43f),"Teal");
                            b.Box(new Vector3(side*1.47f,.28f,z),new Vector3(.025f,.27f,.33f),"Dark");
                            b.Box(new Vector3(side*1.45f,.41f,z),new Vector3(.025f,.025f,.16f),"Steel");
                        }
                        b.Box(new Vector3(side*1.75f,1.65f,.70f),new Vector3(.16f,.13f,.45f),"Steel");
                    }
                    foreach(float y in new[]{1.33f,1.83f})b.Box(new Vector3(0,y,1.83f),new Vector3(3.05f,.045f,.065f),"Steel");
                    for(int i=-5;i<=5;i++)b.Box(new Vector3(i*.27f,1.58f,1.83f),new Vector3(.035f,.48f,.025f),"Steel");
                    Vent(new Vector3(-.95f,1.28f,1.81f),9,.075f,.19f);
                    break;
            }
        }

        static void Save(string name,ArtMesh data,Dictionary<string,Glow> glow,Vector3? moving)
        {
            string path=Folder+"/Meshes/"+name+".asset";var fresh=data.ToMesh(name);var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=fresh;AssetDatabase.CreateAsset(mesh,path);}
            else
            {
                // CopySerialized alone can leave an existing Mesh's GPU buffers on the old geometry.
                // Use Mesh setters so a rebuild updates both the asset and its live render data.
                mesh.Clear();mesh.indexFormat=fresh.indexFormat;mesh.vertices=fresh.vertices;
                mesh.normals=fresh.normals;mesh.uv=fresh.uv;mesh.tangents=fresh.tangents;
                mesh.subMeshCount=fresh.subMeshCount;
                for(int i=0;i<fresh.subMeshCount;i++)mesh.SetTriangles(fresh.GetTriangles(i),i);
                mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
                UnityEngine.Object.DestroyImmediate(fresh);
            }
            var root=new GameObject(name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();var materials=new List<Material>();
                foreach(string key in data.Keys)materials.Add(_materials[key]);renderer.sharedMaterials=materials.ToArray();
                if(glow.Count>0||moving.HasValue)
                {
                    var art=root.AddComponent<ModuleArtLighting>();var surfaces=new List<ModuleArtLighting.Surface>();
                    if(moving.HasValue){var socket=new GameObject("FX_MovingPivot").transform;socket.SetParent(root.transform,false);socket.localPosition=moving.Value;art.MovingSocket=socket;}
                    for(int i=0;i<data.Keys.Count;i++)if(glow.TryGetValue(data.Keys[i],out var control))
                    {
                        Transform socket=null;
                        if(control.Position.HasValue){socket=new GameObject("FX_"+data.Keys[i]).transform;socket.SetParent(root.transform,false);socket.localPosition=control.Position.Value;}
                        surfaces.Add(new ModuleArtLighting.Surface{Renderer=renderer,MaterialIndex=i,Emission=materials[i].GetColor("_EmissionColor"),Control=control.Mode,Socket=socket});
                    }
                    art.Surfaces=surfaces.ToArray();
                }
                PrefabUtility.SaveAsPrefabAsset(root,Prefabs+name+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
