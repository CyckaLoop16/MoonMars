"""Author the H01 habitat as a flat-shaded Unity mesh and portable OBJ.

Run with Python 3. Pillow is needed only for the preview sheet.
Coordinates are Unity Y-up, metres at art scale; one tile unit represents 10 m.
"""
from pathlib import Path
import json
import math
import struct
import uuid

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'Assets/Regolith/Art/Habitat'
RES = ROOT / 'Assets/Regolith/Resources/Modules'
ART.mkdir(parents=True, exist_ok=True)
RES.mkdir(parents=True, exist_ok=True)
PALETTE = {
    'Ivory': (233, 224, 198), 'Teal': (29, 160, 164),
    'Orange': (244, 114, 49), 'Structure': (50, 67, 81),
    'Glass': (69, 191, 219), 'Solar': (43, 86, 157),
    'CellLine': (93, 144, 191), 'Light': (167, 237, 223),
    'Silver': (158, 176, 180),
}
faces = []

def face(points, material):
    faces.append((points, material))

def box(center, size, material):
    x,y,z = center
    a,b,c = (v/2 for v in size)
    p = [(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x-a,y+b,z-c),
         (x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)]
    for ids in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(3,7,6,2),(0,1,5,4)]:
        face([p[i] for i in ids], material)

def prism(section, lo, hi, axis, material):
    # Section must be CCW in its local 2D coordinates.
    def point(a,b,t):
        return (a,b,t) if axis == 'z' else (b,t,a)
    front = [point(a,b,lo) for a,b in section]
    back = [point(a,b,hi) for a,b in section]
    face(front[::-1],material)
    face(back,material)
    for i in range(len(section)):
        j=(i+1)%len(section)
        face([front[i],front[j],back[j],back[i]],material)

def bevel_section(x,y,w,h,c):
    l,r,b,t = x-w/2,x+w/2,y-h/2,y+h/2
    return [(l+c,b),(r-c,b),(r,b+c),(r,t-c),(r-c,t),(l+c,t),(l,t-c),(l,b+c)]

def chamfer(center,size,cut,material):
    x,y,z = center
    w,h,d = size
    prism(bevel_section(x,y,w,h,cut), z-d/2,z+d/2,'z',material)

def cylinder(center,radius,length,axis,material,n=12):
    x,y,z = center
    if axis == 'z':
        section=[(x+radius*math.cos(i*2*math.pi/n),y+radius*math.sin(i*2*math.pi/n)) for i in range(n)]
        prism(section,z-length/2,z+length/2,axis,material)
    else:
        section=[(z+radius*math.cos(i*2*math.pi/n),x+radius*math.sin(i*2*math.pi/n)) for i in range(n)]
        prism(section,y-length/2,y+length/2,axis,material)

# Raised pressure hull, with faceted insulation and structural end collars.
chamfer((0,.405,.045),(.64,.44,.88),.105,'Ivory')
chamfer((0,.405,-.365),(.672,.468,.045),.112,'Structure')
chamfer((0,.405,.455),(.672,.468,.045),.112,'Teal')
chamfer((0,.405,-.405),(.625,.428,.04),.10,'Ivory')
for z in [-.20,.18]:
    # Broad teal roof bands remain visible at distant strategy zoom.
    box((0,.632,z),(.425,.018,.10),'Teal')
    for x in [-.326,.326]:
        box((x,.405,z),(.016,.245,.07),'Teal')

# Landing feet and low chassis: all contact pads sit at Y=0.
box((0,.165,.04),(.43,.055,.68),'Structure')
for x in [-.258,.258]:
    for z in [-.285,.34]:
        box((x,.102,z),(.055,.17,.065),'Silver')
        chamfer((x,.025,z),(.18,.05,.18),.012,'Structure')
        box((x,.054,z),(.125,.008,.11),'Orange')

# Separate short airlock vestibule, recessed circular pressure door and ramp.
chamfer((0,.324,-.497),(.32,.328,.20),.055,'Orange')
chamfer((0,.324,-.605),(.276,.282,.03),.045,'Structure')
cylinder((0,.327,-.626),.109,.024,'z','Silver',12)
cylinder((0,.327,-.641),.083,.009,'z','Teal',12)
box((0,.327,-.648),(.011,.13,.008),'Structure')
box((0,.327,-.654),(.056,.012,.01),'Ivory')
box((.104,.429,-.626),(.027,.023,.009),'Light')
face([(-.14,.018,-.85),(-.14,.178,-.625),(.14,.178,-.625),(.14,.018,-.85)],'Silver')
for z in [-.81,-.765,-.72,-.675]:
    y=.018+(z+.85)*(.16/.225)
    box((0,y+.004,z),(.255,.009,.012),'Structure')

# Window frames, thick enough to read as deliberate shapes from above.
for x in [-.324,.324]:
    for z in [-.055,.31]:
        box((x,.439,z),(.019,.128,.165),'Structure')
        box((x+math.copysign(.011,x),.445,z),(.008,.083,.124),'Glass')
        box((x+math.copysign(.016,x),.464,z),(.005,.012,.116),'Light')

# Roof life-support cassette, radiator grille, small redundant tanks.
chamfer((0,.673,.03),(.245,.086,.245),.022,'Structure')
box((0,.719,.03),(.19,.012,.195),'Ivory')
for z in [-.025,.005,.035,.065,.095]:
    box((0,.728,z),(.145,.006,.009),'Structure')
for x in [-.165,.165]:
    cylinder((x,.664,.30),.046,.08,'y','Orange',10)
    cylinder((x,.709,.30),.034,.012,'y','Silver',10)
box((0,.44,.51),(.235,.20,.07),'Structure')
box((0,.45,.552),(.19,.14,.025),'Teal')

# Two compact solar wings: integrated starter power, with inset cell fields.
for side in [-1,1]:
    box((side*.423,.282,.055),(.21,.033,.045),'Silver')
    box((side*.633,.302,.055),(.30,.036,.60),'Structure')
    box((side*.633,.324,.055),(.265,.009,.562),'Solar')
    for z in [-.185,-.105,-.025,.055,.135,.215,.295]:
        box((side*.633,.331,z),(.257,.004,.006),'CellLine')
    box((side*.633,.331,.055),(.006,.004,.55),'CellLine')
    for z in [-.24,.35]:
        box((side*.633,.326,z),(.29,.014,.018),'Silver')

# Short communications mast and an orange tracking head.
cylinder((.23,.746,.39),.012,.23,'y','Silver',8)
cylinder((.23,.868,.39),.034,.025,'y','Orange',8)
box((.23,.807,.39),(.13,.012,.018),'Ivory')

def cross(a,b):
    return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])

def normal(points):
    a,b,c=points[:3]
    n=cross(tuple(b[i]-a[i] for i in range(3)),tuple(c[i]-a[i] for i in range(3)))
    length=math.sqrt(sum(v*v for v in n))
    assert length>1e-10, 'Degenerate face'
    return tuple(v/length for v in n)

def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL,'regolith/habitat/v1/'+name).hex

def meta(path, importer='NativeFormatImporter', extra=''):
    Path(str(path)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid(path.name)}\n{importer}:\n'+extra+'  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

# Triangle cross products point outward, matching Unity's mesh normals.
positions=[]; normals=[]; colors=[]; indices=[]
light=(-.45,.82,-.35)
for points,mat in faces:
    n=normal(points)
    shade=.66+.34*max(0,sum(n[i]*light[i] for i in range(3)))
    col=tuple(round(v*shade) for v in PALETTE[mat])+(255,)
    start=len(positions)
    positions.extend(points); normals.extend([n]*len(points)); colors.extend([col]*len(points))
    for i in range(1,len(points)-1): indices.extend([start,start+i,start+i+1])
assert len(positions)<65536
assert all(abs(x)<=.8661 and abs(z)+abs(x)/math.sqrt(3)<=1.001 for x,y,z in positions), 'Outside tile'
assert min(p[1] for p in positions)>=-1e-6
assert all(math.isfinite(v) for p in positions for v in p)
mins=[min(p[i] for p in positions) for i in range(3)]
maxs=[max(p[i] for p in positions) for i in range(3)]
center=[(a+b)/2 for a,b in zip(mins,maxs)]
extent=[(b-a)/2 for a,b in zip(mins,maxs)]
vec=lambda a:'{x: %.7f, y: %.7f, z: %.7f}'%tuple(a)

# The map shader expects render-space colours. Bake the project colour space
# into the mesh, as HexMeshBuilder does for the terrain.
settings=(ROOT/'ProjectSettings/ProjectSettings.asset').read_text()
linear='  activeColorSpace: 1' in settings
def render_color(c):
    if not linear: return c
    def channel(v):
        s=v/255
        return round(255*(s/12.92 if s<=.04045 else ((s+.055)/1.055)**2.4))
    return tuple(channel(v) for v in c[:3])+(255,)
vertex_data=b''.join(struct.pack('<6f4B',*p,*n,*render_color(c)) for p,n,c in zip(positions,normals,colors))
index_data=struct.pack('<'+'H'*len(indices),*indices)
channels=[]
for offset,fmt,dim in [(0,0,3),(12,0,3),(0,0,0),(24,2,4)]+[(0,0,0)]*10:
    channels.append(f'    - stream: 0\n      offset: {offset}\n      format: {fmt}\n      dimension: {dim}')
mesh=ART/'HabitatMesh.asset'
mesh.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!43 &4300000
Mesh:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: HabitatMesh
  serializedVersion: 12
  m_SubMeshes:
  - serializedVersion: 2
    firstByte: 0
    indexCount: INDEXCOUNT
    topology: 0
    baseVertex: 0
    firstVertex: 0
    vertexCount: VERTCOUNT
    localAABB:
      m_Center: CENTER
      m_Extent: EXTENT
  m_Shapes:
    vertices: []
    shapes: []
    channels: []
    fullWeights: []
  m_BindPose: []
  m_BoneNameHashes: 
  m_RootBoneNameHash: 0
  m_BonesAABB: []
  m_VariableBoneCountWeights:
    m_Data: 
  m_MeshCompression: 0
  m_IsReadable: 1
  m_KeepVertices: 0
  m_KeepIndices: 0
  m_IndexFormat: 0
  m_IndexBuffer: INDEXDATA
  m_VertexData:
    serializedVersion: 3
    m_VertexCount: VERTCOUNT
    m_Channels:
CHANNELS
    m_DataSize: DATASIZE
    _typelessdata: VERTDATA
  m_LocalAABB:
    m_Center: CENTER
    m_Extent: EXTENT
  m_MeshUsageFlags: 0
  m_CookingOptions: 30
  m_BakedConvexCollisionMesh: 
  m_BakedTriangleCollisionMesh: 
  m_MeshMetrics[0]: 1
  m_MeshMetrics[1]: 1
  m_MeshOptimizationFlags: -1
  m_StreamData:
    serializedVersion: 2
    offset: 0
    size: 0
    path: 
'''.replace('INDEXCOUNT',str(len(indices))).replace('VERTCOUNT',str(len(positions))).replace('CENTER',vec(center)).replace('EXTENT',vec(extent)).replace('INDEXDATA',index_data.hex()).replace('CHANNELS','\n'.join(channels)).replace('DATASIZE',str(len(vertex_data))).replace('VERTDATA',vertex_data.hex()))
meta(mesh,extra='  externalObjects: {}\n  mainObjectFileID: 4300000\n')

shader_meta=(ROOT/'Assets/Regolith/Resources/Shaders/UnlitVertexColor.shader.meta').read_text()
shader_guid=next(s.split(': ')[1] for s in shader_meta.splitlines() if s.startswith('guid:'))
material=ART/'HabitatPalette.mat'
material.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: HabitatPalette
  m_Shader: {fileID: 4800000, guid: SHADER, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats: []
    m_Colors: []
  m_BuildTextureStacks: []
'''.replace('SHADER',shader_guid))
meta(material,extra='  externalObjects: {}\n  mainObjectFileID: 2100000\n')

for name in ['Habitat','HabCore']:
    prefab=RES/(name+'.prefab')
    prefab.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 400000}
  - component: {fileID: 3300000}
  - component: {fileID: 2300000}
  m_Layer: 0
  m_Name: NAME
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &400000
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!33 &3300000
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  m_Mesh: {fileID: 4300000, guid: MESH, type: 2}
--- !u!23 &2300000
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 0
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: MATERIAL, type: 2}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 0
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {fileID: 0}
'''.replace('NAME',name).replace('MESH',guid(mesh.name)).replace('MATERIAL',guid(material.name)))
    meta(prefab,'PrefabImporter',extra='  externalObjects: {}\n')

# Portable source export at real-world scale (10 m per tile unit).
obj=['# H01 habitat | Y up | metres | outward CCW faces','mtllib Habitat.mtl','o H01_Habitat']
offset=1
for points,mat in faces:
    obj.extend('v '+' '.join(f'{v*10:.6f}' for v in p) for p in points)
    obj.append('usemtl '+mat)
    obj.append('f '+' '.join(str(offset+i) for i in range(len(points))))
    offset+=len(points)
(ART/'Habitat.obj').write_text('\n'.join(obj)+'\n')
(ART/'Habitat.mtl').write_text('\n'.join(f'newmtl {k}\nKd '+ ' '.join(f'{v/255:.6f}' for v in c)+'\nKa 0.2 0.2 0.2\nKs 0 0 0\n' for k,c in PALETTE.items()))

# Keep source geometry for independent inspection and previewing.
(ART/'Habitat.geometry.json').write_text(json.dumps({'faces':faces,'palette':PALETTE},separators=(',',':')))
print(json.dumps({'vertices':len(positions),'triangles':len(indices)//3,'bounds':{'min':mins,'max':maxs},'mesh':str(mesh)},indent=2))

if __name__ == '__main__':
    try:
        from PIL import Image, ImageDraw, ImageFont
        import numpy as np
    except ImportError:
        print('Pillow unavailable; skipped preview.')
    else:
        # Orthographic software render of the exact authored faces.
        W,H=1600,1000
        pixels=np.zeros((H,W,3),dtype=np.uint8); pixels[:]=(19,30,43)
        depth=np.full((H,W),-np.inf)
        font_path='C:/Windows/Fonts/segoeui.ttf'
        font=ImageFont.truetype(font_path,22)
        small=ImageFont.truetype(font_path,17)
        title=ImageFont.truetype(font_path,40)
        def render(cx,cy,scale,yaw,pitch):
            ya,pi=math.radians(yaw),math.radians(pitch)
            right=(math.cos(ya),0,-math.sin(ya))
            forward=(math.sin(ya)*math.cos(pi),math.sin(pi),math.cos(ya)*math.cos(pi))
            up=cross(forward,right)
            dot=lambda a,b:sum(a[i]*b[i] for i in range(3))
            def proj(p): return (cx+dot(p,right)*scale,cy-dot(p,up)*scale,dot(p,forward))
            for pts,mat in faces:
                n=normal(pts)
                if dot(n,forward)<=0: continue
                col=tuple(round(v*(.66+.34*max(0,dot(n,light)))) for v in PALETTE[mat])
                p=[proj(v) for v in pts]
                for i in range(1,len(p)-1):
                    a,b,c=p[0],p[i],p[i+1]
                    x0=max(0,math.floor(min(a[0],b[0],c[0]))); x1=min(W-1,math.ceil(max(a[0],b[0],c[0])))
                    y0=max(0,math.floor(min(a[1],b[1],c[1]))); y1=min(H-1,math.ceil(max(a[1],b[1],c[1])))
                    if x1<x0 or y1<y0: continue
                    yy,xx=np.mgrid[y0:y1+1,x0:x1+1]; xx=xx+.5; yy=yy+.5
                    den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
                    if abs(den)<1e-9: continue
                    u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
                    v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den
                    w=1-u-v; z=u*a[2]+v*b[2]+w*c[2]
                    target=depth[y0:y1+1,x0:x1+1]
                    mask=(u>=-1e-6)&(v>=-1e-6)&(w>=-1e-6)&(z>target)
                    target[mask]=z[mask]; pixels[y0:y1+1,x0:x1+1][mask]=col
        render(500,635,490,155,36)
        render(1240,365,240,180,70)
        render(1240,795,270,0,38)
        im=Image.fromarray(pixels); draw=ImageDraw.Draw(im)
        draw.text((60,35),'H01 / SURFACE HABITAT',font=title,fill=(239,230,206))
        draw.text((62,92),'REGOLITH   /   Moon + Mars   /   Stylized hard-surface study 01',font=font,fill=(128,169,181))
        draw.text((68,897),'Ivory insulation  /  Teal systems  /  Orange access  /  Blue photovoltaics',font=font,fill=(172,191,199))
        draw.text((68,937),f'{len(indices)//3:,} triangles  |  One mesh + material  |  Ground pivot  |  Fits one hex',font=small,fill=(112,147,162))
        draw.text((1040,155),'STRATEGY VIEW / 70 degrees',font=small,fill=(128,169,181))
        draw.text((1040,580),'REAR / LIFE SUPPORT',font=small,fill=(128,169,181))
        out=ROOT/'ArtPreviews'; out.mkdir(exist_ok=True)
        im.save(out/'Habitat.png')
        print('Preview: '+str(out/'Habitat.png'))
