"""Build a reusable low-poly lunar terrain kit as native Unity prefabs + OBJ."""
from pathlib import Path
import json
import math
import random
import re
import struct
import uuid
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/Regolith/Art/LunarSurface'
RES=ROOT/'Assets/Regolith/Resources/Terrain'
ART.mkdir(parents=True,exist_ok=True); RES.mkdir(parents=True,exist_ok=True)
HAB=ROOT/'Assets/Regolith/Art/Habitat'
mesh_template=(HAB/'HabitatMesh.asset').read_text()
prefab_template=(ROOT/'Assets/Regolith/Resources/Modules/Habitat.prefab').read_text()
material_template=(HAB/'HabitatPalette.mat').read_text()
guid=lambda name:uuid.uuid5(uuid.NAMESPACE_URL,'regolith/lunar/v1/'+name).hex
old_guid=lambda name:uuid.uuid5(uuid.NAMESPACE_URL,'regolith/habitat/v1/'+name).hex
linear='  activeColorSpace: 1' in (ROOT/'ProjectSettings/ProjectSettings.asset').read_text()
PALETTE={'Dust':(157,158,153),'Highland':(181,179,165),'Basalt':(87,97,106),
         'Rock':(139,146,147),'RockLight':(184,184,172),'Shadow':(48,59,74),
         'Ice':(153,178,184),'Cut':(87,95,104)}
LIGHT=(-.45,.82,-.35)

def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def normal(p):
    a,b,c=p[:3]; n=cross([b[i]-a[i] for i in range(3)],[c[i]-a[i] for i in range(3)])
    length=math.sqrt(sum(v*v for v in n)); assert length>1e-9
    return tuple(v/length for v in n)
def upface(faces,points,mat):
    if normal(points)[1]<0:points=points[::-1]
    faces.append((points,mat))
def meta(path,kind,file_id=None):
    extra=f'  mainObjectFileID: {file_id}\n' if file_id else ''
    Path(str(path)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid(path.name)}\n{kind}:\n  externalObjects: {{}}\n'+extra+'  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

mat=ART/'LunarPalette.mat'
mat.write_text(material_template.replace('m_Name: HabitatPalette','m_Name: LunarPalette'))
meta(mat,'NativeFormatImporter',2100000)

def export(name,faces):
    pos=[]; normals=[]; colors=[]; indices=[]
    for points,material in faces:
        n=normal(points); shade=.66+.34*max(0,sum(n[i]*LIGHT[i] for i in range(3)))
        color=[v/255*shade for v in PALETTE[material]]
        if linear:color=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in color]
        col=tuple(round(v*255) for v in color)+(255,)
        start=len(pos);pos.extend(points);normals.extend([n]*len(points));colors.extend([col]*len(points))
        for i in range(1,len(points)-1):indices.extend([start,start+i,start+i+1])
    assert len(pos)<65536 and all(math.isfinite(v) for p in pos for v in p)
    assert all(abs(x)<=.8661 and abs(z)+abs(x)/math.sqrt(3)<=1.001 for x,y,z in pos),name
    bounds=[[min(p[i] for p in pos) for i in range(3)],[max(p[i] for p in pos) for i in range(3)]]
    center=[(a+b)/2 for a,b in zip(*bounds)];extent=[(b-a)/2 for a,b in zip(*bounds)]
    vec=lambda p:'{x: %.7f, y: %.7f, z: %.7f}'%tuple(p)
    vertex=b''.join(struct.pack('<6f4B',*p,*n,*c) for p,n,c in zip(pos,normals,colors))
    index=struct.pack('<'+'H'*len(indices),*indices)
    text=mesh_template.replace('m_Name: HabitatMesh','m_Name: '+name+'Mesh')
    for key,value in [('indexCount',len(indices)),('vertexCount',len(pos)),('m_VertexCount',len(pos)),('m_DataSize',len(vertex)),('m_IndexBuffer',index.hex()),('_typelessdata',vertex.hex()),('m_Center',vec(center)),('m_Extent',vec(extent))]:
        text=re.sub(r'(?m)^(\s*'+re.escape(key)+r': )[^\r\n]*',lambda m:m[1]+str(value),text)
    mesh=ART/(name+'Mesh.asset');mesh.write_text(text);meta(mesh,'NativeFormatImporter',4300000)
    prefab=RES/(name+'.prefab')
    prefab.write_text(prefab_template.replace('m_Name: Habitat','m_Name: '+name).replace(old_guid('HabitatMesh.asset'),guid(mesh.name)).replace(old_guid('HabitatPalette.mat'),guid(mat.name)))
    meta(prefab,'PrefabImporter')
    lines=['# Lunar surface kit | Y up | tile units','mtllib LunarPalette.mtl','o '+name];offset=1
    for points,material in faces:
        lines.extend('v '+' '.join(f'{v:.6f}' for v in p) for p in points)
        lines.extend(['usemtl '+material,'f '+' '.join(str(offset+i) for i in range(len(points)))])
        offset+=len(points)
    (ART/(name+'.obj')).write_text('\n'.join(lines)+'\n')
    return {'name':name,'triangles':len(indices)//3,'vertices':len(pos),'bounds':bounds}

def rock(faces,x,z,r,h,rng,material='Rock'):
    n=7;angle=rng.random()*6.28
    rings=[]
    for radius,y in [(r,0),(r*.90,h*.50),(r*.52,h)]:
        rings.append([(x+radius*math.cos(angle+i*math.tau/n)*(1+rng.uniform(-.12,.12)),y,z+radius*math.sin(angle+i*math.tau/n)*(1+rng.uniform(-.12,.12))) for i in range(n)])
    for a,b in zip(rings,rings[1:]):
        for i in range(n):
            j=(i+1)%n
            # Rocks' side order is outward for XZ rings.
            faces.append(([a[j],a[i],b[i],b[j]],material))
    upface(faces,rings[-1],material)
    faces.append((rings[0],material))

def tile(kind,seed):
    rng=random.Random(seed);faces=[];N=24
    # All boundary vertices are exactly Y=0 and lie on the same pointy hex.
    corners=[(math.cos(math.radians(30+i*60)),math.sin(math.radians(30+i*60))) for i in range(6)]
    boundary=[]
    for i in range(6):
        a,b=corners[i],corners[(i+1)%6]
        for j in range(4):
            t=j/4;boundary.append((a[0]*(1-t)+b[0]*t,a[1]*(1-t)+b[1]*t))
    rings=[]
    for r in [0.16,.38,.58,.76,1.0]:
        ring=[]
        for i,(bx,bz) in enumerate(boundary):
            theta=math.atan2(bz,bx)
            jitter=rng.uniform(-1,1)
            if kind=='LunarHighlands': y=.10*(math.sin(bx*r*4+.5)*math.cos(bz*r*3)+.5)*(1-r)
            elif kind=='LunarMare': y=.008*math.sin(bx*r*6+.4)*math.cos(bz*r*5)*(1-r)
            elif kind in ['ShadowedCrater','SmallImpactCrater']:
                y={.16:-.12,.38:-.08,.58:.13,.76:.045,1.0:0}[r]+(.012*jitter if r!=1 else 0)
            elif kind=='CraterRim':y=max(0,math.cos(theta-.7))**4*.27*math.sin(math.pi*r)+.015*jitter*(1-r)
            else:y={.16:-.15,.38:.10,.58:.15,.76:.035,1.0:0}[r]+.012*jitter*(1-r)
            if r==1:y=0
            # The skylight uses an elongated throat rather than a circular crater.
            rz=r if kind!='LavaTubeEntrance' else r*(.62+.38*r)
            ring.append((bx*r,y,bz*rz))
        rings.append(ring)
    ground='Basalt' if kind in ['LunarMare','LavaTubeEntrance'] else 'Highland' if kind in ['LunarHighlands','CraterRim'] else 'Dust'
    center_y={'SmallImpactCrater':-.13,'ShadowedCrater':-.13,'LavaTubeEntrance':-.17,
              'LunarHighlands':.10*(math.sin(.5)+.5),'LunarMare':.008*math.sin(.4),'CraterRim':0}[kind]
    center=(0,center_y,0)
    for i in range(N):
        j=(i+1)%N
        inner='Shadow' if kind in ['ShadowedCrater','LavaTubeEntrance'] else ground
        upface(faces,[center,rings[0][i],rings[0][j]],inner)
    for level,(a,b) in enumerate(zip(rings,rings[1:])):
        for i in range(N):
            j=(i+1)%N
            material='Shadow' if kind in ['ShadowedCrater','LavaTubeEntrance'] and level==0 else ground
            for p in [[a[i],b[i],b[j]],[a[i],b[j],a[j]]]:upface(faces,p,material)
    for i in range(N):
        j=(i+1)%N;a,b=rings[-1][i],rings[-1][j]
        faces.append(([a,b,(b[0],-.18,b[2]),(a[0],-.18,a[2])],'Cut'))
    faces.append(([(x,-.18,z) for x,z in corners],'Cut'))
    # Sparse rough ejecta around the perimeter. Dark basin ice stays muted.
    for i in range(5):
        angle=rng.uniform(0,math.tau);r=rng.uniform(.66,.73)
        rock(faces,math.cos(angle)*r,math.sin(angle)*r,.035+rng.random()*.025,.04+rng.random()*.025,rng,'RockLight' if kind=='LunarHighlands' else 'Rock')
    if kind=='ShadowedCrater':
        for x,z in [(-.09,.03),(.1,.08),(.01,-.1)]:
            upface(faces,[(x-.04,-.117,z-.02),(x+.04,-.117,z-.025),(x+.02,-.117,z+.028),(x-.02,-.117,z+.035)],'Ice')
    return faces

models={}
for i,name in enumerate(['LunarHighlands','LunarMare','SmallImpactCrater','ShadowedCrater','CraterRim','LavaTubeEntrance']):
    models[name]=tile(name,150+i)
for i,name in enumerate(['BoulderClusterA','BoulderClusterB']):
    rng=random.Random(230+i);faces=[]
    for x,z,r,h in [(-.17,.06,.19,.26),(.12,.13,.145,.18),(.1,-.18,.10,.12),(-.21,-.20,.07,.075),(.29,-.08,.055,.055)]:
        rock(faces,x,z,r*(1+.16*i),h*(1-.15*i),rng,'RockLight' if i else 'Rock')
    models[name]=faces

stats=[export(name,faces) for name,faces in models.items()]
(ART/'LunarPalette.mtl').write_text('\n'.join('newmtl '+name+'\nKd '+' '.join(f'{v/255:.6f}' for v in col)+'\nKs 0 0 0\n' for name,col in PALETTE.items()))
(ART/'LunarSurface.geometry.json').write_text(json.dumps({'models':models,'palette':PALETTE},separators=(',',':')))
(ART/'LunarSurface.stats.json').write_text(json.dumps(stats,indent=2))

# Z-buffered orthographic review sheet, rendered from the exact model faces.
W,H=2000,1250
pixels=np.empty((H,W,3),dtype=np.uint8);pixels[:]=(19,30,43);depth=np.full((H,W),-np.inf)
ya,pi=math.radians(155),math.radians(42)
right=(math.cos(ya),0,-math.sin(ya));forward=(math.sin(ya)*math.cos(pi),math.sin(pi),math.cos(ya)*math.cos(pi));up=cross(forward,right)
dot=lambda a,b:sum(a[i]*b[i] for i in range(3))
for model_index,(name,faces) in enumerate(models.items()):
    cx=250+(model_index%4)*500;cy=410+(model_index//4)*510;scale=210 if model_index<6 else 360
    for pts,material in faces:
        n=normal(pts)
        if dot(n,forward)<=0:continue
        col=tuple(round(v*(.66+.34*max(0,dot(n,LIGHT)))) for v in PALETTE[material])
        p=[(cx+dot(v,right)*scale,cy-dot(v,up)*scale,dot(v,forward)) for v in pts]
        for i in range(1,len(p)-1):
            a,b,c=p[0],p[i],p[i+1]
            x0=max(0,math.floor(min(a[0],b[0],c[0])));x1=min(W-1,math.ceil(max(a[0],b[0],c[0])))
            y0=max(0,math.floor(min(a[1],b[1],c[1])));y1=min(H-1,math.ceil(max(a[1],b[1],c[1])))
            if x1<x0 or y1<y0:continue
            yy,xx=np.mgrid[y0:y1+1,x0:x1+1];xx=xx+.5;yy=yy+.5
            den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
            if abs(den)<1e-9:continue
            u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
            v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w=1-u-v
            z=u*a[2]+v*b[2]+w*c[2];target=depth[y0:y1+1,x0:x1+1]
            mask=(u>=-1e-6)&(v>=-1e-6)&(w>=-1e-6)&(z>target)
            target[mask]=z[mask];pixels[y0:y1+1,x0:x1+1][mask]=col
im=Image.fromarray(pixels);draw=ImageDraw.Draw(im)
font=lambda n:ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',n)
draw.text((60,35),'L01 / LUNAR SURFACE KIT',font=font(44),fill=(239,230,206))
draw.text((63,100),'REGOLITH   /   Faceted regolith, basalt, impact rims and exposed rock',font=font(24),fill=(128,169,181))
labels=['ANORTHOSITE HIGHLANDS','MARE BASALT','SMALL IMPACT CRATER','SHADOWED POLAR CRATER','IMPACT RIM / EJECTA','LAVA TUBE SKYLIGHT','BOULDERS / BASALT','BOULDERS / HIGHLANDS']
for i,stat in enumerate(stats):
    x=60+(i%4)*500;y=585+(i//4)*510
    draw.text((x,y),labels[i],font=font(21),fill=(221,221,204))
    draw.text((x,y+37),f'{stat["triangles"]:,} triangles  /  '+('1 hex' if i<6 else 'scatter prop'),font=font(18),fill=(128,169,181))
draw.text((62,1201),'Shared vertex-colour material  /  Y-up  /  Standard hex boundaries  /  Native Unity prefabs + OBJ sources',font=font(21),fill=(128,169,181))
out=ROOT/'ArtPreviews';out.mkdir(exist_ok=True);im.save(out/'LunarSurface.png')
print(json.dumps(stats,indent=2));print('Preview: '+str(out/'LunarSurface.png'))
