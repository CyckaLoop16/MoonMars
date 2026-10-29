using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Waystation.EditorTools
{
    /// <summary>Authored industrial floor/edge kit for the existing auto-connecting prefab slots.</summary>
    static class IndustrialStructureInstaller
    {
        const string Folder = "Assets/Waystation/Art/IndustrialStructures";
        const string Materials = "Assets/Waystation/Art/IndustrialPads/Materials/";
        const string Prefabs = "Assets/Waystation/Resources/Buildings/";
        const string Revision = "1";
        static readonly string[] Names = { "Walkway_Deck", "Walkway_Railing", "Foundation", "Foundation_Edge" };

        [InitializeOnLoadMethod]
        static void OnLoad() => EditorApplication.delayCall += BuildIfNeeded;

        static void BuildIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<Material>(Materials + "Pad_Steel.mat") == null) return;
            bool ready = File.Exists(Folder + "/Revision.txt") && File.ReadAllText(Folder + "/Revision.txt") == Revision;
            foreach (string name in Names) ready &= File.Exists(Prefabs + name + ".prefab");
            if (!ready) Build();
        }

        [MenuItem("Waystation/Art/Rebuild Industrial Walkways and Foundations")]
        static void Build()
        {
            try
            {
                Directory.CreateDirectory(Folder + "/Meshes");
                AssetDatabase.Refresh();
                foreach (string name in Names)
                {
                    var art = new ArtMesh();
                    if (name == "Walkway_Deck") Walkway(art);
                    else if (name == "Walkway_Railing") Railing(art);
                    else if (name == "Foundation") Foundation(art);
                    else FoundationEdge(art);
                    Save(name, art);
                }
                AssetDatabase.SaveAssets();
                IndustrialStructureCheck.Run();
                File.WriteAllText(Folder + "/Revision.txt", Revision);
                AssetDatabase.ImportAsset(Folder + "/Revision.txt");
                Debug.Log("Industrial walkways and foundations installed and checked.");
            }
            catch (Exception error) { Debug.LogError("Industrial structure installation: " + error); }
        }

        static void Walkway(ArtMesh b)
        {
            // One-cell grating cassette. All four edges end exactly on the grid, at deck Y=0.
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(new Vector3(side * .455f,-.072f,0),new Vector3(.09f,.144f,1f),"Steel");
                b.Box(new Vector3(0,-.072f,side * .455f),new Vector3(.82f,.144f,.09f),"Steel");
                b.Box(new Vector3(side * .484f,-.085f,0),new Vector3(.025f,.095f,.78f),"Hull");
                b.Box(new Vector3(0,-.085f,side * .484f),new Vector3(.78f,.095f,.025f),"Hull");
            }
            // Real open grating, not black rectangles painted onto a solid floor.
            for (int i=-5;i<=5;i++) b.Box(new Vector3(i*.069f,-.018f,0),new Vector3(.028f,.036f,.82f),"Deck");
            for (int i=-3;i<=3;i++) b.Box(new Vector3(0,-.057f,i*.117f),new Vector3(.82f,.035f,.026f),"Dark");
            // Recessed service bus and a braced chassis stay below the walking surface.
            b.Beam(new Vector3(-.45f,-.18f,-.40f),new Vector3(.45f,-.18f,.40f),.025f,"Steel",6);
            b.Beam(new Vector3(-.45f,-.18f,.40f),new Vector3(.45f,-.18f,-.40f),.025f,"Steel",6);
            b.Box(new Vector3(0,-.22f,0),new Vector3(.26f,.08f,.26f),"Dark");
            b.Beam(new Vector3(-.5f,-.16f,.22f),new Vector3(.5f,-.16f,.22f),.018f,"Orange",8);
            b.Beam(new Vector3(.22f,-.19f,-.5f),new Vector3(.22f,-.19f,.5f),.014f,"Teal",8);
            foreach (float x in new[] { -.455f,.455f }) foreach(float z in new[] {-.455f,.455f})
            {
                b.Box(new Vector3(x,-.006f,z),new Vector3(.072f,.012f,.072f),"Dark");
                b.Cylinder(new Vector3(x,.001f,z),.016f,.006f,"Steel",8);
            }
            // Small corner patches leave the centre visually quiet at strategy-camera scale.
            foreach(float side in new[] {-1f,1f})
            {
                b.Box(new Vector3(-.455f,-.001f,side*.33f),new Vector3(.065f,.003f,.10f),"Yellow");
                b.Box(new Vector3(.455f,-.001f,side*.33f),new Vector3(.065f,.003f,.10f),"Yellow");
            }
        }

        static void Railing(ArtMesh b)
        {
            const float z=.46f;
            foreach(float x in new[] {-.445f,0f,.445f})
            {
                b.Box(new Vector3(x,.028f,z),new Vector3(.085f,.056f,.065f),"Hull");
                b.Box(new Vector3(x,.195f,z),new Vector3(.037f,.34f,.036f),"Steel");
                b.Cylinder(new Vector3(x,.06f,z),.012f,.012f,"Steel",8);
            }
            b.Box(new Vector3(0,.355f,z),new Vector3(.98f,.04f,.045f),"Dark");
            b.Box(new Vector3(0,.17f,z),new Vector3(.90f,.025f,.025f),"Steel");
            b.Box(new Vector3(0,.068f,z),new Vector3(.88f,.09f,.023f),"Hull");
            for(int i=0;i<8;i++)
                b.Box(new Vector3(-.42f+i*.12f,.378f,z),new Vector3(.10f,.006f,.044f),i%2==0?"Yellow":"Dark");
            foreach(float side in new[] {-1f,1f})
            {
                b.Beam(new Vector3(side*.055f,.11f,z),new Vector3(side*.40f,.30f,z),.011f,"Steel",6);
                b.Beam(new Vector3(side*.055f,.30f,z),new Vector3(side*.40f,.11f,z),.009f,"Dark",6);
                b.Box(new Vector3(side*.23f,.071f,z+.015f),new Vector3(.15f,.032f,.008f),"Orange");
            }
        }

        static void Foundation(ArtMesh b)
        {
            // Sandwich armour with clean modular boundaries. Modules still stand at Y=0.
            b.Slab(1f,1f,.022f,-.27f,-.055f,"Dark");
            b.Slab(.985f,.985f,.025f,-.09f,-.025f,"Steel");
            b.Slab(.97f,.97f,.025f,-.042f,-.012f,"Hull");
            foreach(float side in new[]{-1f,1f})
            {
                b.Box(new Vector3(side*.4925f,-.006f,0),new Vector3(.015f,.012f,1f),"Steel");
                b.Box(new Vector3(0,-.006f,side*.4925f),new Vector3(.97f,.012f,.015f),"Steel");
            }
            b.Slab(.805f,.805f,.015f,-.028f,-.004f,"Dark");
            foreach(float x in new[] {-.205f,.205f}) foreach(float z in new[] {-.205f,.205f})
                b.SlabAt(new Vector3(x,0,z),.392f,.392f,.014f,-.02f,0f,"Deck");
            // Flush olive inspection hatch, inset seams and a rust-coloured release lever.
            b.SlabAt(Vector3.zero,.285f,.285f,.025f,-.008f,.002f,"Teal");
            b.Box(new Vector3(0,.003f,.075f),new Vector3(.13f,.003f,.018f),"Dark");
            b.Box(new Vector3(0,.005f,.075f),new Vector3(.065f,.004f,.008f),"Steel");
            b.Box(new Vector3(.085f,.003f,-.08f),new Vector3(.035f,.004f,.045f),"Orange");
            foreach(float x in new[] {-.444f,.444f}) foreach(float z in new[] {-.444f,.444f})
            {
                b.Box(new Vector3(x,-.004f,z),new Vector3(.055f,.015f,.055f),"Dark");
                b.Cylinder(new Vector3(x,.001f,z),.013f,.006f,"Steel",8);
            }
            b.Slab(.69f,.69f,.10f,-.35f,-.27f,"Steel");
            b.Slab(.50f,.50f,.08f,-.39f,-.34f,"Dark");
            foreach(float side in new[] {-1f,1f})
            {
                b.Beam(new Vector3(side*.43f,-.20f,-.4f),new Vector3(side*.23f,-.34f,-.20f),.027f,"Steel",6);
                b.Beam(new Vector3(side*.43f,-.20f,.4f),new Vector3(side*.23f,-.34f,.20f),.027f,"Steel",6);
                b.Beam(new Vector3(-.5f,-.21f,side*.18f),new Vector3(.5f,-.21f,side*.18f),.024f,side>0?"Orange":"Dark",8);
            }
        }

        static void FoundationEdge(ArtMesh b)
        {
            b.Box(new Vector3(0,-.10f,.475f),new Vector3(.95f,.20f,.045f),"Steel");
            b.Box(new Vector3(0,-.015f,.463f),new Vector3(.92f,.05f,.067f),"Dark");
            for(int i=0;i<8;i++)
                b.Box(new Vector3(-.42f+i*.12f,.013f,.464f),new Vector3(.10f,.006f,.055f),i%2==0?"Yellow":"Dark");
            foreach(float x in new[] {-.32f,0f,.32f})
            {
                b.Box(new Vector3(x,-.145f,.483f),new Vector3(.27f,.095f,.022f),"Hull");
                b.Box(new Vector3(x,-.225f,.474f),new Vector3(.16f,.035f,.044f),"Dark");
            }
            b.Box(new Vector3(0,-.275f,.455f),new Vector3(.56f,.055f,.065f),"Dark");
            b.Beam(new Vector3(-.22f,-.265f,.454f),new Vector3(.22f,-.265f,.454f),.018f,"Orange",8);
        }

        static void Save(string name, ArtMesh data)
        {
            var fresh=data.ToMesh(name);string path=Folder+"/Meshes/"+name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=fresh;AssetDatabase.CreateAsset(mesh,path);}
            else{EditorUtility.CopySerialized(fresh,mesh);UnityEngine.Object.DestroyImmediate(fresh);}
            var root=new GameObject(name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=root.AddComponent<MeshRenderer>();var materials=new List<Material>();
                foreach(string key in data.Keys)
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Pad_"+key+".mat");
                    if(material==null)throw new Exception("Missing shared industrial material: "+key);
                    materials.Add(material);
                }
                renderer.sharedMaterials=materials.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,Prefabs+name+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        internal sealed class ArtMesh
        {
            readonly List<Vector3> _vertices=new List<Vector3>(),_normals=new List<Vector3>();
            readonly List<Vector2> _uv=new List<Vector2>();
            readonly Dictionary<string,List<int>> _groups=new Dictionary<string,List<int>>();
            public readonly List<string> Keys=new List<string>();

            void Face(Vector3[] p,Vector3 outward,string material)
            {
                p=(Vector3[])p.Clone();
                var normal=Vector3.Cross(p[1]-p[0],p[2]-p[0]).normalized;
                if(Vector3.Dot(normal,outward)<0f){Array.Reverse(p);normal=-normal;}
                int start=_vertices.Count;
                if(!_groups.TryGetValue(material,out var triangles)){triangles=new List<int>();_groups[material]=triangles;Keys.Add(material);}
                Vector2 cell=material=="Hull"||material=="Paint"||material=="Yellow"?new Vector2(0,0):
                    material=="Teal"?new Vector2(.5f,.5f):material=="Orange"?new Vector2(.5f,0):new Vector2(0,.5f);
                bool atlas=material=="Hull"||material=="Paint"||material=="Yellow"||material=="Teal"||material=="Orange"||material=="Steel"||material=="Deck"||material=="Dark"||material=="Panel";
                int axis=Mathf.Abs(normal.y)>.65f?1:Mathf.Abs(normal.x)>Mathf.Abs(normal.z)?0:2;
                Vector2 Project(Vector3 point)=>axis==1?new Vector2(point.x,point.z):axis==0?new Vector2(point.z,point.y):new Vector2(point.x,point.y);
                var minimum=Project(p[0]);var maximum=minimum;
                foreach(var point in p){minimum=Vector2.Min(minimum,Project(point));maximum=Vector2.Max(maximum,Project(point));}
                foreach(var point in p)
                {
                    _vertices.Add(point);_normals.Add(normal);
                    var q=axis==1?new Vector2(point.x,point.z):axis==0?new Vector2(point.z,point.y):new Vector2(point.x,point.y);
                    // Match the pad atlas's texel density: a tile uses one third of a material square.
                    _uv.Add(atlas?cell+new Vector2(.015f,.015f)+new Vector2((q.x-(minimum.x+maximum.x)*.5f)/Mathf.Max(3f,maximum.x-minimum.x)+.5f,(q.y-(minimum.y+maximum.y)*.5f)/Mathf.Max(3f,maximum.y-minimum.y)+.5f)*.47f:
                        new Vector2((q.x-minimum.x)/Mathf.Max(.00001f,maximum.x-minimum.x),(q.y-minimum.y)/Mathf.Max(.00001f,maximum.y-minimum.y)));
                }
                for(int i=1;i<p.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
            }

            public void Box(Vector3 center,Vector3 size,string material)
            {
                var h=size*.5f;var p=new[]{new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),
                    new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z)};
                for(int i=0;i<p.Length;i++)p[i]+=center;
                var faces=new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}};
                var normals=new[]{Vector3.back,Vector3.forward,Vector3.left,Vector3.right,Vector3.up,Vector3.down};
                for(int i=0;i<6;i++)Face(Array.ConvertAll(faces[i],j=>p[j]),normals[i],material);
            }

            public void Slab(float width,float depth,float bevel,float bottom,float top,string material)=>SlabAt(Vector3.zero,width,depth,bevel,bottom,top,material);
            public void SlabAt(Vector3 offset,float width,float depth,float bevel,float bottom,float top,string material)
            {
                float x=width*.5f,z=depth*.5f;
                var section=new[]{new Vector2(-x+bevel,-z),new Vector2(x-bevel,-z),new Vector2(x,-z+bevel),new Vector2(x,z-bevel),new Vector2(x-bevel,z),new Vector2(-x+bevel,z),new Vector2(-x,z-bevel),new Vector2(-x,-z+bevel)};
                Prism(section,offset,bottom,top,material);
            }

            public void Cylinder(Vector3 center,float radius,float height,string material,int sides)
            {
                var section=new Vector2[sides];for(int i=0;i<sides;i++)section[i]=new Vector2(Mathf.Cos(i*Mathf.PI*2/sides),Mathf.Sin(i*Mathf.PI*2/sides))*radius;
                Prism(section,center,-height*.5f,height*.5f,material);
            }

            void Prism(Vector2[] section,Vector3 offset,float bottom,float top,string material)
            {
                var low=Array.ConvertAll(section,p=>offset+new Vector3(p.x,bottom,p.y));
                var high=Array.ConvertAll(section,p=>offset+new Vector3(p.x,top,p.y));
                Face(low,Vector3.down,material);Face(high,Vector3.up,material);
                for(int i=0;i<section.Length;i++)
                {
                    int j=(i+1)%section.Length;
                    var outward=new Vector3(section[i].x+section[j].x,0,section[i].y+section[j].y);
                    Face(new[]{low[i],low[j],high[j],high[i]},outward,material);
                }
            }

            public void Beam(Vector3 a,Vector3 b,float radius,string material,int sides)
            {
                var axis=(b-a).normalized;var right=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var up=Vector3.Cross(axis,right);
                var low=new Vector3[sides];var high=new Vector3[sides];
                for(int i=0;i<sides;i++){var d=(right*Mathf.Cos(i*Mathf.PI*2/sides)+up*Mathf.Sin(i*Mathf.PI*2/sides))*radius;low[i]=a+d;high[i]=b+d;}
                Face(low,-axis,material);Face(high,axis,material);
                for(int i=0;i<sides;i++){int j=(i+1)%sides;Face(new[]{low[i],low[j],high[j],high[i]},(low[i]+low[j])*.5f-a,material);}
            }

            public void Surface(Vector3[] points,Vector3 outward,string material)=>Face(points,outward,material);

            public void Sphere(Vector3 center,float radius,string material,int rings=16,int sides=32)
            {
                Vector3 P(int ring,int side)
                {
                    float a=-Mathf.PI*.5f+Mathf.PI*ring/rings,t=2f*Mathf.PI*side/sides;
                    return center+new Vector3(Mathf.Cos(a)*Mathf.Cos(t),Mathf.Sin(a),Mathf.Cos(a)*Mathf.Sin(t))*radius;
                }
                for(int j=0;j<rings;j++)for(int i=0;i<sides;i++)
                {
                    var a=P(j,i);var b=P(j+1,i);var c=P(j+1,i+1);var d=P(j,i+1);
                    if(j==0)Face(new[]{a,b,c},((a+b+c)/3-center).normalized,material);
                    else if(j==rings-1)Face(new[]{a,b,d},((a+b+d)/3-center).normalized,material);
                    else Face(new[]{a,b,c,d},((a+b+c+d)/4-center).normalized,material);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(_vertices);mesh.SetNormals(_normals);mesh.SetUVs(0,_uv);
                mesh.subMeshCount=Keys.Count;for(int i=0;i<Keys.Count;i++)mesh.SetTriangles(_groups[Keys[i]],i);
                mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
