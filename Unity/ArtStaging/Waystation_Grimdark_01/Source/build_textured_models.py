"""Textured grimdark alternative. Writes only to this staging package.
Source mesh definitions are preserved beside this package in Waystation_Art_01.
The generated colour atlas is used unchanged. Technical maps are procedural data.
"""
from pathlib import Path
base=Path(__file__).resolve().parent/'mesh_authoring.py'
exec(base.read_text().split('\nmodels=[')[0],globals())
TEXDIR=ROOT/'Textures';TEXDIR.mkdir(exist_ok=True)
# Technical maps: micro-pitting and scratches generated independently as surface data.
rng=np.random.default_rng(4829);S=2048
noise=np.zeros((S,S),np.float32)
for size,weight in [(16,.45),(64,.25),(256,.18),(1024,.12)]:
 field=(rng.random((size,size))*255).astype(np.uint8)
 noise+=np.asarray(Image.fromarray(field).resize((S,S),Image.Resampling.BILINEAR),dtype=np.float32)/255*weight
micro=rng.random((S,S)).astype(np.float32)
height=noise*.6+micro*.012
for k in range(450):
 x=int(rng.integers(0,S));y=int(rng.integers(0,S));length=int(rng.integers(3,90));width=int(rng.integers(1,3))
 height[y:min(S,y+length),x:min(S,x+width)]-=.015
# Standard tangent-space (+Y/OpenGL) normal, not a painted purple image.
dy,dx=np.gradient(height);nn=np.dstack([-dx*5,-dy*5,np.ones_like(height)]);nn/=np.linalg.norm(nn,axis=2)[:,:,None]
normal=((nn*.5+.5)*255).clip(0,255).astype(np.uint8)
Image.fromarray(normal).save(TEXDIR/'IndustrialAtlas_Normal.png')
rough=np.empty((S,S),np.float32);metal=np.empty((S,S),np.float32)
for x,y,r,m in [(0,0,.63,.78),(1,0,.84,.03),(0,1,.81,.03),(1,1,.87,.03)]:
 sl=np.s_[y*S//2:(y+1)*S//2,x*S//2:(x+1)*S//2]
 rough[sl]=r+(noise[sl]-.5)*.22;metal[sl]=m
packed=np.dstack([np.ones_like(rough),rough,metal]);packed=(packed*255).clip(0,255).astype(np.uint8)
Image.fromarray(packed).save(TEXDIR/'IndustrialAtlas_ORM.png')
Image.fromarray((rough*255).clip(0,255).astype(np.uint8)).save(TEXDIR/'IndustrialAtlas_Roughness.png')
Image.fromarray((metal*255).clip(0,255).astype(np.uint8)).save(TEXDIR/'IndustrialAtlas_Metallic.png')

# Rocky background objects get their own mineral/grain texture, not the painted-steel atlas.
rock=.28+noise*.65+(micro-.5)*.07
rockrgb=np.dstack([rock*.92,rock*.95,rock])
Image.fromarray((rockrgb*255).clip(0,255).astype(np.uint8)).save(TEXDIR/'Rock_BaseColor.png')
Image.fromarray(normal).save(TEXDIR/'Rock_Normal.png')
rockorm=np.dstack([np.ones_like(rock),np.clip(.84+(noise-.5)*.2,0,1),np.zeros_like(rock)])
Image.fromarray((rockorm*255).astype(np.uint8)).save(TEXDIR/'Rock_ORM.png')

# Equirectangular cloud map with a restrained copper/teal industrial-space palette.
PW,PH=4096,2048
lat=np.linspace(math.pi/2,-math.pi/2,PH)[:,None];lon=np.linspace(0,TAU,PW,endpoint=False)[None,:]
warp=.032*np.sin(lon*3+lat*8)+.018*np.sin(lon*7-lat*11)
bands=np.sin((lat+warp)*24)+.35*np.sin((lat+warp)*61+.8)
t=(bands+1.35)/2.7
copper=np.array(rgb('#9d7660'));cream=np.array(rgb('#c7b9a0'));teal=np.array(rgb('#526e70'))
cloud=copper[None,None,:]*(1-t[:,:,None])+cream[None,None,:]*t[:,:,None]
mask=((lat>.10)&(lat<.42))|(lat<-.65)
cloud=np.where(mask[:,:,None],cloud*.35+teal*.65,cloud)
dl=np.arctan2(np.sin(lon+.55),np.cos(lon+.55));storm=(dl/.29)**2+((lat+.26)/.10)**2
st=np.clip(1-storm/2,0,1)[:,:,None]*.65
cloud=cloud*(1-st)+np.array(rgb('#876254'))*st
cloud*=((.97+.03*np.sin(lon*41+lat*83)*np.sin(lat*53-lon*19)))[:,:,None]
Image.fromarray((cloud*255).clip(0,255).astype(np.uint8)).save(TEXDIR/'GasGiant_BaseColor.png')
# Ring atlas remains geometry-based: fine actual gaps rather than an alpha-cut billboard.

ATLAS=np.asarray(Image.open(TEXDIR/'IndustrialAtlas_BaseColor.png').convert('RGB'),dtype=float)/255
ROCK=np.asarray(Image.open(TEXDIR/'Rock_BaseColor.png').convert('RGB'),dtype=float)/255
CLOUD=np.asarray(Image.open(TEXDIR/'GasGiant_BaseColor.png').convert('RGB'),dtype=float)/255
atlas_mats={'Hull','Deck','Panel','Dark','Steel','Teal','Orange','Yellow','Paint'}
rock_mats={'Basalt','WarmRock','IceRock','Moon'}
TINT={'Hull':(1,1,1),'Deck':(.72,.76,.78),'Panel':(.85,.9,.92),'Dark':(.40,.45,.49),
      'Steel':(1.2,1.23,1.25),'Teal':(.85,.92,.78),'Orange':(.9,.88,.8),'Yellow':(.83,.66,.25),'Paint':(.94,.95,.86)}
QUAD={'Hull':(0,1),'Paint':(0,1),'Yellow':(0,1),'Teal':(1,0),'Orange':(1,1)}

def uv_for(p,n,mat):
 if mat=='Gas':
  r=np.linalg.norm(p,axis=1);uv=np.column_stack([(np.arctan2(p[:,2],p[:,0])/TAU)%1,.5-np.arcsin(np.clip(p[:,1]/r,-1,1))/math.pi])
  if uv[:,0].max()-uv[:,0].min()>.5:uv[uv[:,0]<.5,0]+=1
  return uv
 axis=int(np.argmax(np.abs(np.mean(n,axis=0))))
 q=p[:,[2,1]] if axis==0 else p[:,[0,2]] if axis==1 else p[:,[0,1]]
 if mat in atlas_mats:
  uv=np.column_stack([np.clip((q[:,0]+1.5)/3,0,1),np.clip((q[:,1]+(1.5 if axis==1 else .9))/(3 if axis==1 else 1.8),0,1)])
  cell=QUAD.get(mat,(0,0));return (np.array(cell)+.015+uv*.97)*.5
 return q*.42+.5

def sample(tex,uv):
 h,w=tex.shape[:2];u=uv[:,0]%1;v=uv[:,1]%1
 return tex[np.minimum((v*h).astype(int),h-1),np.minimum((u*w).astype(int),w-1)]

# Retain the geometry, with quieter running lights and worn material response.
models=[landing('LandingPad_Industrial',False),landing('StarterPad_Industrial',True),sphere_model('RingedGasGiant_Industrial','gas'),sphere_model('DistantMoon_Industrial','moon',5),
        sphere_model('Asteroid_Basalt_Industrial','rock',3),sphere_model('Asteroid_Warm_Industrial','rock',4),sphere_model('Asteroid_Icy_Industrial','rock',5)]
for model in models:
 for f in model.faces:
  if f['m']=='Ring':f['c']=(np.array(f['c'])*np.array([.70,.72,.72])).tolist()
  elif f['m'] in rock_mats:f['c']=[color(f['m'])]*len(f['p'])
# Reuse the existing renderer with texture sampling at the interpolated UVs.
render_source=base.read_text().split('def render(m,w,h,scale,')[1].split('\ndef font(')[0]
render_source='def render(m,w,h,scale,'+render_source
render_source=render_source.replace('base=uvw@cols',"""uv=uvw@uv_for(pts,normals,mat)
  if mat in atlas_mats:base=sample(ATLAS,uv)*np.array(TINT[mat])
  elif mat=='Gas':base=sample(CLOUD,uv)
  elif mat in rock_mats:base=sample(ROCK,uv)*(uvw@cols)*1.35
  else:base=uvw@cols""")
render_source=render_source.replace('lit=base*(.30+.70*diffuse[:,None])','lit=base*(.24+.76*diffuse[:,None])')
exec(render_source,globals())

def export_textured(m):
 tris=m.triangulate();groups=defaultdict(list)
 for tri in tris:groups[(tri[3],tri[4])].append(tri)
 used=sorted({t[3]for t in tris});lines=['# Staged grimdark model. Y-up. No game installation.','mtllib '+m.name+'.mtl','o '+m.name];v=1
 for (mat,part),tt in groups.items():
  lines+=['g '+part.replace(' ','_'),'usemtl '+mat]
  for p,n,c,_,_ in tt:
   uv=uv_for(p,n,mat)
   for a in p:lines.append('v '+' '.join(f'{x:.7f}'for x in a))
   for a in uv:lines.append(f'vt {a[0]:.7f} {1-a[1]:.7f}')
   for a in n:lines.append('vn '+' '.join(f'{x:.7f}'for x in a))
   lines.append('f '+' '.join(f'{v+i}/{v+i}/{v+i}'for i in range(3)));v+=3
 (OUT/(m.name+'.obj')).write_text('\n'.join(lines)+'\n')
 gltf={'asset':{'version':'2.0','generator':'Waystation Grimdark Art 01'},'scene':0,'scenes':[{'nodes':[0]}],
       'nodes':[{'name':m.name,'children':[]}],'meshes':[],'materials':[],'buffers':[],'bufferViews':[],'accessors':[],
       'images':[],'textures':[],'samplers':[{'magFilter':9729,'minFilter':9987,'wrapS':10497,'wrapT':10497}]}
 binary=bytearray();tex_indices={}
 def blob(data,target=None):
  while len(binary)%4:binary.append(0)
  offset=len(binary);binary.extend(data);view=len(gltf['bufferViews'])
  d={'buffer':0,'byteOffset':offset,'byteLength':len(data)}
  if target:d['target']=target
  gltf['bufferViews'].append(d);return view
 def texture(filename):
  if filename in tex_indices:return tex_indices[filename]
  view=blob((TEXDIR/filename).read_bytes());image=len(gltf['images'])
  gltf['images'].append({'name':filename,'bufferView':view,'mimeType':'image/png'})
  idx=len(gltf['textures']);gltf['textures'].append({'source':image,'sampler':0});tex_indices[filename]=idx;return idx
 def accessor(data,typ):
  arr=np.asarray(data,dtype='<f4');view=blob(arr.tobytes(),34962);idx=len(gltf['accessors'])
  d={'bufferView':view,'componentType':5126,'count':len(arr),'type':typ}
  if typ=='VEC3':d.update({'min':arr.min(axis=0).tolist(),'max':arr.max(axis=0).tolist()})
  gltf['accessors'].append(d);return idx
 mtl=[]
 for mat in used:
  factor=[*TINT.get(mat,(1,1,1)),1];factor=[min(1,x)for x in factor]
  pbr={'baseColorFactor':factor,'roughnessFactor':.88,'metallicFactor':0}
  entry={'name':mat,'pbrMetallicRoughness':pbr,'doubleSided':mat=='Ring'}
  mtl+=['newmtl '+mat,'Kd '+' '.join(str(x)for x in factor[:3]),'Pr 0.85','Pm 0']
  if mat in atlas_mats:
   pbr.update({'baseColorTexture':{'index':texture('IndustrialAtlas_BaseColor.png')},'metallicRoughnessTexture':{'index':texture('IndustrialAtlas_ORM.png')},'metallicFactor':1,'roughnessFactor':1})
   entry['normalTexture']={'index':texture('IndustrialAtlas_Normal.png'),'scale':.45}
   mtl+=['map_Kd ../Textures/IndustrialAtlas_BaseColor.png','map_Pr ../Textures/IndustrialAtlas_Roughness.png','map_Pm ../Textures/IndustrialAtlas_Metallic.png','norm ../Textures/IndustrialAtlas_Normal.png']
  elif mat=='Gas':pbr['baseColorTexture']={'index':texture('GasGiant_BaseColor.png')};mtl+=['map_Kd ../Textures/GasGiant_BaseColor.png']
  elif mat in rock_mats:
   pbr.update({'baseColorTexture':{'index':texture('Rock_BaseColor.png')},'metallicRoughnessTexture':{'index':texture('Rock_ORM.png')},'metallicFactor':1,'roughnessFactor':1})
   entry['normalTexture']={'index':texture('Rock_Normal.png'),'scale':.6};mtl+=['map_Kd ../Textures/Rock_BaseColor.png','norm ../Textures/Rock_Normal.png']
  if mat in EMISSIVE:entry['emissiveFactor']=tuple(v*.4 for v in EMISSIVE[mat])
  gltf['materials'].append(entry)
 (OUT/(m.name+'.mtl')).write_text('\n'.join(mtl)+'\n')
 for (mat,part),tt in groups.items():
  pp=np.concatenate([t[0]for t in tt]);nn=np.concatenate([t[1]for t in tt]);uv=np.concatenate([uv_for(t[0],t[1],mat)for t in tt])
  cc=np.concatenate([t[2]for t in tt]) if mat in rock_mats or mat=='Ring' or mat in EMISSIVE else np.ones_like(pp)
  cc=np.where(cc<=.04045,cc/12.92,((cc+.055)/1.055)**2.4)
  primitive={'attributes':{'POSITION':accessor(pp,'VEC3'),'NORMAL':accessor(nn,'VEC3'),'TEXCOORD_0':accessor(uv,'VEC2'),'COLOR_0':accessor(cc,'VEC3')},'material':used.index(mat),'mode':4}
  mesh=len(gltf['meshes']);gltf['meshes'].append({'name':part,'primitives':[primitive]})
  node=len(gltf['nodes']);gltf['nodes'].append({'name':part,'mesh':mesh});gltf['nodes'][0]['children'].append(node)
 while len(binary)%4:binary.append(0)
 gltf['buffers']=[{'byteLength':len(binary)}];js=json.dumps(gltf,separators=(',',':')).encode()
 while len(js)%4:js+=b' '
 total=12+8+len(js)+8+len(binary)
 data=struct.pack('<III',0x46546c67,2,total)+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(binary),0x004e4942)+binary
 (OUT/(m.name+'.glb')).write_bytes(data)
 pos=np.concatenate([t[0]for t in tris]);assert np.isfinite(pos).all()
 if 'Pad' in m.name:
  assert max(abs(pos[:,0]))<=1.5 and max(abs(pos[:,2]))<=1.5
  assert not any(abs(p[0])<.75 and abs(p[2])<.75 and p[1]>.04 for p in pos)
 return {'name':m.name,'triangles':len(tris),'parts':len(groups),'embedded_textures':len(tex_indices),'glb_bytes':total}

stats=[export_textured(m)for m in models]
(ROOT/'manifest.json').write_text(json.dumps({'package':'Waystation Grimdark 01','status':'STAGED ONLY - not installed in Unity','models':stats},indent=2))
print(json.dumps(stats,indent=2),flush=True)
im=sheet_base('WAYSTATION / INDUSTRIAL DOCKING PLATFORM','GRIMDARK STUDY 01   /   UV-mapped worn steel + chipped paint   /   STAGED ONLY')
im.paste(render(models[1],1220,940,284,35,34),(20,155))
im.paste(render(models[0],680,450,155,0,76),(1290,170))
im.paste(render(models[1],680,430,150,140,-22,focus=(0,-.28,0)),(1290,690))
d=ImageDraw.Draw(im)
d.text((65,1130),'STARTER PAD / oxidised fuel reservoir + service console',font=font(25),fill=(226,228,213))
d.text((65,1180),'Worn steel  /  Desaturated olive  /  Dirty ivory  /  Rust-red equipment  /  Amber safety marks',font=font(21),fill=(124,166,182))
d.text((1310,635),'LANDING PAD / overhead',font=font(23),fill=(226,228,213))
d.text((1310,1130),'UNDERSIDE / machinery + truss',font=font(23),fill=(226,228,213))
d.text((65,1240),'GLB with embedded PBR textures  /  OBJ + MTL  /  3 x 3 footprint  /  Deck at Y = 0  /  No game changes',font=font(20),fill=(124,166,182))
im.save(PRE/'IndustrialLandingPads.png');print('Textured pad preview ready.',flush=True)
im=sheet_base('WAYSTATION / OUTER ORBIT','GRIMDARK STUDY 01   /   Textured gas giant, mineral surfaces and fine ring geometry')
im.paste(render(models[2],1300,990,300,35,23,focus=(0,0,0),planet=True),(0,150))
im.paste(render(models[3],380,360,138,30,20,focus=(0,0,0),planet=True),(1490,155))
for k,model in enumerate(models[4:]):im.paste(render(model,340,300,105,45,23,focus=(0,0,0)),(1250+(k%2)*360,560+(k//2)*320))
d=ImageDraw.Draw(im)
d.text((65,1120),'GAS GIANT / 4K cloud texture + 3D rings',font=font(26),fill=(226,228,213))
d.text((65,1170),'Muted copper and slate  /  Atmospheric limb  /  Rocky moon  /  Three asteroid silhouettes',font=font(21),fill=(124,166,182))
d.text((1540,510),'DISTANT MOON',font=font(21),fill=(226,228,213))
d.text((1280,845),'BASALT',font=font(20),fill=(226,228,213));d.text((1640,845),'WARM ROCK',font=font(20),fill=(226,228,213));d.text((1640,1030),'ICE-VEINED',font=font(20),fill=(226,228,213))
d.text((65,1240),'Independent staged models  /  UV-mapped textures embedded in GLB  /  No scene or renderer changes',font=font(20),fill=(124,166,182))
im.save(PRE/'IndustrialOrbit.png');print('Textured background preview ready.',flush=True)
