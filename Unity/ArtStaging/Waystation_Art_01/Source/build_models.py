"""Waystation art study 01. Standalone staging only: never writes to Assets.
Python 3 + numpy + Pillow. Exports glTF 2.0 binary, OBJ/MTL, geometry JSON and previews.
"""
from pathlib import Path
from collections import defaultdict
import json, math, struct, random
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Models'
PRE = ROOT / 'Previews'
OUT.mkdir(exist_ok=True); PRE.mkdir(exist_ok=True)
TAU = math.tau
MATS = {
 'Hull':('#d9e5e5',.72,0), 'Deck':('#3e5362',.83,0),
 'Panel':('#506979',.8,0), 'Dark':('#1c2b3c',.84,.1),
 'Steel':('#819ca9',.48,.5), 'Teal':('#35c5ae',.6,.15),
 'Orange':('#ed784b',.65,.1), 'Yellow':('#f7c65e',.7,0),
 'Paint':('#e8ecdf',.85,0), 'Glass':('#216580',.3,.3),
 'Cyan':('#89f6ef',.3,.1), 'Red':('#f96b73',.35,0),
 'Gas':('#ffffff',.95,0), 'Ring':('#ffffff',.95,0),
 'Basalt':('#8c869e',.95,0), 'WarmRock':('#b4998a',.95,0),
 'IceRock':('#9eb6c3',.87,0), 'Moon':('#b7bbc3',.97,0),
}
EMISSIVE={'Cyan':(.25,.75,.7),'Red':(.8,.1,.12),'Glass':(.01,.05,.08)}
def rgb(h): return tuple(int(h[i:i+2],16)/255 for i in (1,3,5))
def norm(v):
 v=np.asarray(v,dtype=float); return v/(np.linalg.norm(v)+1e-20)
def color(name):return rgb(MATS[name][0])

class Model:
 def __init__(self,name):self.name=name;self.faces=[]
 def face(self,pts,mat,colors=None,normals=None,group='Structure'):
  pts=np.array(pts,float)
  n=norm(np.cross(pts[1]-pts[0],pts[2]-pts[0]))
  if np.linalg.norm(n)<.9:raise ValueError('Degenerate polygon '+self.name+' '+group)
  self.faces.append({'p':pts.tolist(),'m':mat,'c':colors if colors is not None else [color(mat)]*len(pts),'n':normals if normals is not None else [n.tolist()]*len(pts),'g':group})
 def box(self,c,s,mat,yaw=0,group='Structure'):
  c=np.array(c);x,y,z=np.array(s)/2;a=math.radians(yaw)
  rot=np.array([[math.cos(a),0,math.sin(a)],[0,1,0],[-math.sin(a),0,math.cos(a)]])
  p=np.array([[-x,-y,-z],[x,-y,-z],[x,y,-z],[-x,y,-z],[-x,-y,z],[x,-y,z],[x,y,z],[-x,y,z]])@rot.T+c
  for idx in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(3,7,6,2),(0,1,5,4)]:self.face(p[list(idx)],mat,group=group)
 def section(self,section,lo,hi,mat,group='Structure'):
  # CCW X/Z section; sides face out, cap faces upward.
  a=[(x,lo,z) for x,z in section];b=[(x,hi,z) for x,z in section]
  self.face(a,mat,group=group);self.face(b[::-1],mat,group=group)
  for i in range(len(a)):
   j=(i+1)%len(a);self.face([a[j],a[i],b[i],b[j]],mat,group=group)
 def slab(self,w,d,cut,lo,hi,mat,group='Structure'):
  x,z=w/2,d/2
  section=[(-x+cut,-z),(x-cut,-z),(x,-z+cut),(x,z-cut),(x-cut,z),(-x+cut,z),(-x,z-cut),(-x,-z+cut)]
  self.section(section,lo,hi,mat,group)
 def cylinder(self,c,r,h,mat,sides=16,group='Structure'):
  x,y,z=c;self.section([(x+r*math.cos(i*TAU/sides),z+r*math.sin(i*TAU/sides)) for i in range(sides)],y-h/2,y+h/2,mat,group)
 def beam(self,a,b,r,mat,sides=6,group='Structure'):
  a=np.array(a);b=np.array(b);axis=norm(b-a)
  right=norm(np.cross(axis,[0,1,0] if abs(axis[1])<.9 else [1,0,0]));up=np.cross(axis,right)
  rings=[[p+r*(right*math.cos(i*TAU/sides)+up*math.sin(i*TAU/sides)) for i in range(sides)]for p in [a,b]]
  self.face(rings[0][::-1],mat,group=group);self.face(rings[1],mat,group=group)
  for i in range(sides):
   j=(i+1)%sides;self.face([rings[0][i],rings[0][j],rings[1][j],rings[1][i]],mat,group=group)
 def arc(self,c,inner,outer,start,end,mat,segments=48,group='Markings'):
  x,y,z=c; count=max(1,round(segments*abs(end-start)/360))
  for i in range(count):
   a=math.radians(start+(end-start)*i/count);b=math.radians(start+(end-start)*(i+1)/count)
   p=lambda r,t:(x+r*math.cos(t),y,z+r*math.sin(t))
   self.face([p(inner,a),p(inner,b),p(outer,b),p(outer,a)],mat,group=group)
 def triangulate(self):
  out=[]
  for f in self.faces:
   for i in range(1,len(f['p'])-1):
    ids=[0,i,i+1]
    p=np.array([f['p'][j] for j in ids]);area=np.linalg.norm(np.cross(p[1]-p[0],p[2]-p[0]))
    if area<1e-11:continue
    out.append((p,np.array([f['n'][j] for j in ids]),np.array([f['c'][j] for j in ids]),f['m'],f['g']))
  return out

def landing(name,starter):
 m=Model(name)
 # Four distinct layers, bevelled corners and a flat unobstructed touchdown deck.
 m.slab(2.94,2.94,.30,-.24,-.085,'Dark','Chassis')
 m.slab(2.88,2.88,.27,-.105,-.035,'Steel','Chassis')
 m.slab(2.80,2.80,.24,-.045,-.014,'Teal','Deck edge')
 m.slab(2.75,2.75,.23,-.026,0,'Hull','Deck')
 # The deck is a precision segmented heat-resistant plate, seams recessed around it.
 for i in range(8):
  a=i*45+2;b=(i+1)*45-2
  m.arc((0,.002,0),.78,1.09,a,b,'Teal' if i%4!=2 else 'Orange',64,'Deck plating')
  m.arc((0,.004,0),1.11,1.125,a+2,b-2,'Steel',64,'Deck plating')
 m.cylinder((0,-.008,0),.765,.02,'Dark',64,'Touchdown plate')
 m.arc((0,.007,0),.694,.716,0,360,'Steel',96,'Touchdown plate')
 for i in range(4):
  m.arc((0,.010,0),.858,.902,i*90+12,i*90+78,'Paint',96,'Landing markings')
  m.arc((0,.012,0),.953,.974,i*90+25,i*90+65,'Cyan',96,'Guidance lights')
 # Broad L marking from the current game concept, crisp at isometric scale.
 m.box((-.17,.012,.08),(.15,.012,.71),'Paint',group='Landing markings')
 m.box((.055,.012,-.20),(.59,.012,.15),'Paint',group='Landing markings')
 # Cardinal docking aprons remain clear for corridor connections.
 for angle in [0,90,180,270]:
  t=math.radians(angle);direction=np.array([math.sin(t),0,math.cos(t)])
  m.box(direction*1.235+np.array([0,.013,0]),(.46,.016,.22),'Dark',angle,'Docking apron')
  for side in [-1,1]:
   v=np.array([math.cos(t),0,-math.sin(t)])*.25*side+direction*1.235
   m.box(v+[0,.025,0],(.036,.018,.23),'Yellow',angle,'Docking apron')
  for s in [-1,1]:
   for i in range(4):
    x=s*(.49+i*.13);p=np.array([x,.008,1.30]);rot=np.array([[math.cos(t),0,math.sin(t)],[0,1,0],[-math.sin(t),0,math.cos(t)]])
    m.box(rot@p,(.075,.014,.095),'Yellow',angle-28,'Hazard markings')
 # Corner fenders, clamps and navigation lights.
 for sx,sz in [(-1,-1),(-1,1),(1,-1),(1,1)]:
  x,z=sx*1.19,sz*1.19
  m.box((x,-.015,z),(.27,.12,.27),'Orange' if sz>0 else 'Teal',45,'Corner fender')
  m.cylinder((x,.065,z),.062,.045,'Dark',12,'Navigation lights')
  m.cylinder((x,.093,z),.043,.012,'Red' if sx<0 else 'Cyan',12,'Navigation lights')
  m.box((sx*1.32,-.20,sz*.91),(.075,.16,.32),'Dark',group='Impact bumper')
  m.box((sx*1.32,-.20,sz*.91),(.08,.028,.22),'Orange',group='Impact bumper')
 # Ivory modular fairings and coral service panels define the retro-future silhouette.
 for angle in [0,90,180,270]:
  t=math.radians(angle);rot=np.array([[math.cos(t),0,math.sin(t)],[0,1,0],[-math.sin(t),0,math.cos(t)]])
  for x in [-.94,-.59,.59,.94]:
   m.box(rot@np.array([x,-.15,1.443]),(.29,.125,.035),'Hull',angle,'Hull fairings')
  m.box(rot@np.array([0,-.15,1.45]),(.38,.13,.035),'Orange',angle,'Hull fairings')
  m.box(rot@np.array([0,-.15,1.473]),(.19,.036,.014),'Dark',angle,'Hull fairings')
 # Machinery bay under the platform, rather than a solid pointed block.
 m.slab(1.60,1.60,.22,-.52,-.24,'Dark','Underside')
 m.slab(1.36,1.36,.16,-.575,-.50,'Steel','Underside')
 for sx in [-1,1]:
  for sz in [-1,1]:
   a=(sx*.58,-.43,sz*.58);b=(sx*1.20,-.22,sz*1.20)
   m.beam(a,b,.065,'Steel',6,'Support truss')
   m.beam((sx*.45,-.24,sz*.58),b,.032,'Teal',6,'Support truss')
   m.cylinder((sx*.54,-.63,sz*.54),.17,.22,'Dark',16,'Attitude thrusters')
   m.cylinder((sx*.54,-.75,sz*.54),.13,.025,'Steel',16,'Attitude thrusters')
   m.cylinder((sx*.54,-.765,sz*.54),.095,.008,'Glass',16,'Attitude thrusters')
 for side in [-1,1]:
  m.box((side*.86,-.35,0),(.10,.15,.82),'Dark',group='Thermal fins')
  for z in [-.32,-.24,-.16,-.08,0,.08,.16,.24,.32]:
   m.box((side*.92,-.35,z),(.25,.12,.026),'Steel',group='Thermal fins')
  m.beam((side*1.35,-.12,-.70),(side*1.35,-.12,.70),.025,'Teal',8,'Service lines')
 # Inset access panels, handles and deck fasteners.
 for angle in [0,90,180,270]:
  t=math.radians(angle);p=lambda x,z:(math.cos(t)*x+math.sin(t)*z,.004,-math.sin(t)*x+math.cos(t)*z)
  m.box(p(.77,1.14),(.24,.012,.10),'Dark',angle,'Service hatches')
  for i in range(4):m.box(p(.69+i*.055,1.14),(.016,.016,.075),'Steel',angle,'Service hatches')
  for x in [-.98,.98]:m.cylinder(p(x,.96),.015,.015,'Steel',8,'Fasteners')
 # One low service console; starter variant also includes a protected fuel reservoir.
 m.box((-1.06,.115,1.03),(.32,.23,.30),'Hull',group='Service console')
 m.box((-1.06,.238,1.03),(.25,.018,.21),'Dark',group='Service console')
 m.box((-1.06,.250,1.04),(.17,.009,.11),'Glass',group='Service console')
 for x in [-1.13,-1.06,-.99]:m.box((x,.258,.965),(.024,.008,.018),'Cyan',group='Service console')
 if starter:
  m.box((1.03,.055,1.03),(.48,.11,.48),'Dark',group='Fuel reservoir')
  m.cylinder((1.03,.34,1.03),.20,.48,'Orange',24,'Fuel reservoir')
  for y in [.145,.45]:m.cylinder((1.03,y,1.03),.211,.045,'Hull',24,'Fuel reservoir')
  m.cylinder((1.03,.59,1.03),.15,.04,'Steel',24,'Fuel reservoir')
  m.cylinder((1.03,.62,1.03),.065,.025,'Orange',16,'Fuel reservoir')
  m.beam((1.23,.22,1.03),(1.31,.22,.83),.035,'Dark',8,'Fuel reservoir')
  m.beam((1.31,.22,.83),(1.31,.02,.58),.035,'Orange',8,'Fuel reservoir')
 else:
  m.box((1.06,.065,1.03),(.30,.13,.25),'Teal',group='Fuel coupling')
  m.cylinder((1.06,.145,1.03),.075,.025,'Orange',16,'Fuel coupling')
 return m

def asteroid(name,seed):
 m=Model(name);rng=random.Random(seed);phi=(1+math.sqrt(5))/2
 vertices=[(-1,phi,0),(1,phi,0),(-1,-phi,0),(1,-phi,0),(0,-1,phi),(0,1,phi),(0,-1,-phi),(0,1,-phi),(phi,0,-1),(phi,0,1),(-phi,0,-1),(-phi,0,1)]
 vertices=[norm(p)for p in vertices]
 faces=[(0,11,5),(0,5,1),(0,1,7),(0,7,10),(0,10,11),(1,5,9),(5,11,4),(11,10,2),(10,7,6),(7,1,8),(3,9,4),(3,4,2),(3,2,6),(3,6,8),(3,8,9),(4,9,5),(2,4,11),(6,2,10),(8,6,7),(9,8,1)]
 for level in range(2):
  cache={};nextfaces=[]
  def midpoint(a,b):
   key=tuple(sorted([a,b]))
   if key not in cache:cache[key]=len(vertices);vertices.append(norm(vertices[a]+vertices[b]))
   return cache[key]
  for a,b,c in faces:
   ab,bc,ca=midpoint(a,b),midpoint(b,c),midpoint(c,a)
   nextfaces.extend([(a,ab,ca),(b,bc,ab),(c,ca,bc),(ab,bc,ca)])
  faces=nextfaces
 phase=rng.random()*TAU;crater=norm([rng.uniform(-1,1),.5,rng.uniform(-1,1)])
 scale=np.array([rng.uniform(.9,1.2),rng.uniform(.75,.98),rng.uniform(.85,1.12)])
 original=vertices;vertices=[]
 for n in original:
  r=1+.13*math.sin(n[0]*4+phase)*math.cos(n[2]*3-phase)+.05*math.sin(n[1]*7+phase)+rng.uniform(-.025,.025)
  d=float(np.dot(n,crater));r-=.16*max(0,(d-.76)/.24)**1.4
  vertices.append(n*r*scale)
 mat=['Basalt','WarmRock','IceRock'][seed%3];base=np.array(color(mat))
 for ids in faces:
  pp=[vertices[i]for i in ids];center=np.mean(pp,axis=0);nn=norm(np.cross(pp[1]-pp[0],pp[2]-pp[0]))
  if np.dot(nn,center)<0:pp=pp[::-1]
  variation=.88+.17*rng.random();cc=base*variation
  if seed%3==2 and abs(math.sin(center[0]*4+center[1]*2-center[2]*3))<.22:cc=cc*.45+np.array(rgb('#c0ded8'))*.55
  m.face(pp,mat,[cc.tolist()]*3,group='Fractured rock')
 return m

def sphere_model(name,kind,seed=1):
 if kind=='rock':return asteroid(name,seed)
 m=Model(name);rng=random.Random(seed)
 latn,lonn=(64,128) if kind=='gas' else (24,48) if kind=='moon' else (10,18)
 radius=1.;points=[];cols=[];smooth=[]
 phase=rng.random()*TAU
 def at(lat,lon):
  n=np.array([math.cos(lat)*math.cos(lon),math.sin(lat),math.cos(lat)*math.sin(lon)])
  if kind=='gas':
   warp=.032*math.sin(lon*3+lat*8)+.018*math.sin(lon*7-lat*11)
   bands=math.sin((lat+warp)*24)+.35*math.sin((lat+warp)*61+.8)
   amber=np.array(rgb('#dfa36f'));cream=np.array(rgb('#f0d6a5'));teal=np.array(rgb('#3ebcb3'))
   cloud=(bands+1.35)/2.7;c=amber*(1-cloud)+cream*cloud
   if .10<lat<.42 or lat<-.65:c=c*.30+teal*.70
   # A flowing oval storm breaks up the latitude bands.
   dl=math.atan2(math.sin(lon+.55),math.cos(lon+.55));storm=(dl/.29)**2+((lat+.26)/.10)**2
   if storm<2.0:
    t=max(0,1-storm/2);c=c*(1-t*.65)+np.array(rgb('#c77d62'))*t*.65
   c*=.97+.03*math.sin(lon*41+lat*83)*math.sin(lat*53-lon*19)
   return n,c,n
  wobble=(math.sin(lon*3+phase)*math.cos(lat*4)+.5*math.sin(lon*7-lat*5+phase))
  r=1+(.035 if kind=='moon' else .16)*wobble
  craterdir=norm([.7,.35,.5]);ang=float(np.dot(n,craterdir));crater=max(0,(ang-.88)/.12)
  r-=crater*(.09 if kind=='moon' else .16)
  r+=.015*math.sin(lon*21+lat*19)
  if kind=='moon':base=np.array(color('Moon'));mat='Moon'
  else:mat=['Basalt','WarmRock','IceRock'][seed%3];base=np.array(color(mat))
  shade=.86+.12*math.sin(lon*5+phase)*math.cos(lat*6)+.04*rng.random()
  c=base*shade
  if kind=='rock' and seed%3==2:
   vein=abs(math.sin(lon*4+lat*7))
   if vein<.16:c=c*.55+np.array(rgb('#c8e2dc'))*.45
  return n*r,c,n
 # Avoid pole degeneracies by using triangle fans.
 grid={}
 for j in range(1,latn):
  lat=-math.pi/2+math.pi*j/latn
  for i in range(lonn):grid[j,i]=at(lat,i*TAU/lonn)
 south=at(-math.pi/2,0);north=at(math.pi/2,0)
 mat='Gas' if kind=='gas' else 'Moon' if kind=='moon' else ['Basalt','WarmRock','IceRock'][seed%3]
 def emit(vs):
  ps=[v[0].tolist() for v in vs];ns=[v[2].tolist() for v in vs] if kind!='rock' else None
  m.face(ps,mat,[v[1].tolist() for v in vs],ns,'Cloud layers' if kind=='gas' else 'Rock body')
 for i in range(lonn):
  k=(i+1)%lonn
  emit([south,grid[1,i],grid[1,k]])
  emit([north,grid[latn-1,k],grid[latn-1,i]])
 for j in range(1,latn-1):
  for i in range(lonn):
   k=(i+1)%lonn
   emit([grid[j,i],grid[j+1,i],grid[j+1,k]])
   emit([grid[j,i],grid[j+1,k],grid[j,k]])
 # Repair each face winding to face away from the body centre.
 for f in m.faces:
  p=np.array(f['p']);n=np.cross(p[1]-p[0],p[2]-p[0])
  if np.dot(n,p.mean(axis=0))<0:
   for key in ['p','c','n']:f[key]=f[key][::-1]
  if kind=='rock':
   p=np.array(f['p']);n=norm(np.cross(p[1]-p[0],p[2]-p[0]));f['n']=[n.tolist()]*len(p)
 if kind=='gas':
  # Real annular geometry with open gaps, independent of the sphere's depth.
  segments=192;bands=46;inner,outer=1.20,1.96
  rotation=np.array([[math.cos(.17),-math.sin(.17),0],[math.sin(.17),math.cos(.17),0],[0,0,1]])
  ringnormal=rotation@np.array([0,1,0])
  for j in range(bands):
   r0=inner+(outer-inner)*j/bands;r1=inner+(outer-inner)*(j+.84)/bands
   if 1.58<r0<1.66 or 1.34<r0<1.36:continue
   t=j/(bands-1);base=np.array(rgb('#b6a3e5'))*(1-t)+np.array(rgb('#ecd2ac'))*t
   base*=.85+.15*math.sin(j*2.73)**2
   for i in range(segments):
    a=i*TAU/segments;b=(i+1)*TAU/segments
    pp=[rotation@np.array([r*math.cos(theta),0,r*math.sin(theta)]) for r,theta in [(r0,a),(r0,b),(r1,b),(r1,a)]]
    m.face(pp,'Ring',[base.tolist()]*4,[ringnormal.tolist()]*4,'Planetary rings')
 return m

def export(m):
 tris=m.triangulate();assert tris
 groups=defaultdict(list)
 for tri in tris:groups[(tri[3],tri[4])].append(tri)
 # OBJ keeps descriptive part groups and standard palette materials.
 lines=['# Waystation staged art: Y-up, +Z north, no game installation','mtllib '+m.name+'.mtl','o '+m.name];v=1
 for (mat,part),tt in groups.items():
  lines+=['g '+part.replace(' ','_'),'usemtl '+mat]
  for p,n,c,_,_ in tt:
   for a,col in zip(p,c):lines.append('v '+' '.join(f'{x:.7f}'for x in [*a,*col]))
   for a in n:lines.append('vn '+' '.join(f'{x:.7f}'for x in a))
   lines.append('f '+' '.join(f'{v+i}//{v+i}'for i in range(3)));v+=3
 (OUT/(m.name+'.obj')).write_text('\n'.join(lines)+'\n')
 used=sorted(set(t[3]for t in tris));mtl=[]
 for mat in used:
  h,rough,metal=MATS[mat];mtl+=['newmtl '+mat,'Kd '+' '.join(str(x)for x in rgb(h)),f'Pr {rough}',f'Pm {metal}','Ks 0.1 0.1 0.1']
  if mat in EMISSIVE:mtl+=['Ke '+' '.join(str(x)for x in EMISSIVE[mat])]
 (OUT/(m.name+'.mtl')).write_text('\n'.join(mtl)+'\n')
 # GLB uses COLOR_0 plus smooth normals; emissive beacons and PBR roughness travel with it.
 gltf={'asset':{'version':'2.0','generator':'Waystation Art Study 01'},'scene':0,'scenes':[{'nodes':[0]}],
       'nodes':[{'name':m.name,'children':[]}],'meshes':[],'materials':[],'buffers':[], 'bufferViews':[],'accessors':[]}
 for mat in used:
  _,rough,metal=MATS[mat]
  entry={'name':mat,'pbrMetallicRoughness':{'baseColorFactor':[1,1,1,1],'metallicFactor':metal,'roughnessFactor':rough},'doubleSided':mat=='Ring'}
  if mat in EMISSIVE:entry['emissiveFactor']=EMISSIVE[mat]
  gltf['materials'].append(entry)
 binary=bytearray()
 def access(arr,typ):
  arr=np.asarray(arr,dtype='<f4');offset=len(binary);binary.extend(arr.tobytes())
  while len(binary)%4:binary.append(0)
  view=len(gltf['bufferViews']);gltf['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':arr.nbytes,'target':34962})
  index=len(gltf['accessors']);entry={'bufferView':view,'componentType':5126,'count':len(arr),'type':typ}
  if typ=='VEC3':entry.update({'min':arr.min(axis=0).tolist(),'max':arr.max(axis=0).tolist()})
  gltf['accessors'].append(entry);return index
 for (mat,part),tt in groups.items():
  positions=np.concatenate([t[0]for t in tt]);normals=np.concatenate([t[1]for t in tt]);colors=np.concatenate([t[2]for t in tt]);colors=np.where(colors<=.04045,colors/12.92,((colors+.055)/1.055)**2.4)
  prim={'attributes':{'POSITION':access(positions,'VEC3'),'NORMAL':access(normals,'VEC3'),'COLOR_0':access(colors,'VEC3')},'material':used.index(mat),'mode':4}
  idx=len(gltf['meshes']);gltf['meshes'].append({'name':part,'primitives':[prim]})
  node=len(gltf['nodes']);gltf['nodes'].append({'name':part,'mesh':idx});gltf['nodes'][0]['children'].append(node)
 gltf['buffers']=[{'byteLength':len(binary)}]
 js=json.dumps(gltf,separators=(',',':')).encode()
 while len(js)%4:js+=b' '
 total=12+8+len(js)+8+len(binary)
 glb=struct.pack('<III',0x46546c67,2,total)+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(binary),0x004e4942)+binary
 (OUT/(m.name+'.glb')).write_bytes(glb)
 pos=np.concatenate([t[0]for t in tris]); bounds=[pos.min(axis=0).tolist(),pos.max(axis=0).tolist()]
 (OUT/(m.name+'.geometry.json')).write_text(json.dumps({'name':m.name,'faces':m.faces},separators=(',',':')))
 if 'Pad' in m.name:
  assert max(abs(pos[:,0]))<=1.5 and max(abs(pos[:,2]))<=1.5,'Pad exceeds 3x3 footprint'
  assert not any(abs(p[0])<.75 and abs(p[2])<.75 and p[1]>.04 for p in pos),'Touchdown volume obstructed'
 assert np.isfinite(pos).all()
 return {'name':m.name,'triangles':len(tris),'parts':len(groups),'bounds':bounds,'glb_bytes':total}

# Orthographic multisample reference renderer of the exported triangles.
# Deliberately no Unity scripts, scene changes, or resource registrations.
LIGHT=norm([-.5,.85,.8]);VIEWLIGHT=norm([-.4,.6,1.0])
def render(m,w,h,scale,yaw=35,pitch=35,focus=(0,-.12,0),planet=False):
 aa=2;W,H=w*aa,h*aa
 pixels=np.empty((H,W,3),np.float32);pixels[:]=np.array([15,24,38])/255
 depth=np.full((H,W),-np.inf,np.float32)
 ya,pi=math.radians(yaw),math.radians(pitch)
 right=np.array([math.cos(ya),0,-math.sin(ya)])
 forward=np.array([math.sin(ya)*math.cos(pi),math.sin(pi),math.cos(ya)*math.cos(pi)])
 up=np.cross(forward,right);focus=np.array(focus)
 triangles=m.triangulate()
 for pts,normals,cols,mat,_ in triangles:
  face_n=norm(np.cross(pts[1]-pts[0],pts[2]-pts[0]));facing=np.dot(face_n,forward)
  if facing<=0 and mat!='Ring':continue
  if facing<0:normals=-normals
  screen=np.column_stack([(pts-focus)@right*scale*aa+W/2,H/2-(pts-focus)@up*scale*aa,pts@forward])
  a,b,c=screen
  x0=max(0,math.floor(min(a[0],b[0],c[0])));x1=min(W-1,math.ceil(max(a[0],b[0],c[0])))
  y0=max(0,math.floor(min(a[1],b[1],c[1])));y1=min(H-1,math.ceil(max(a[1],b[1],c[1])))
  if x1<x0 or y1<y0:continue
  yy,xx=np.mgrid[y0:y1+1,x0:x1+1];xx=xx+.5;yy=yy+.5
  den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
  if abs(den)<1e-10:continue
  u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
  v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;ww=1-u-v
  zz=u*a[2]+v*b[2]+ww*c[2];target=depth[y0:y1+1,x0:x1+1]
  mask=(u>=-1e-6)&(v>=-1e-6)&(ww>=-1e-6)&(zz>target)
  if not mask.any():continue
  uvw=np.stack([u[mask],v[mask],ww[mask]],axis=1)
  nn=uvw@normals;nn/=np.linalg.norm(nn,axis=1)[:,None]+1e-20
  base=uvw@cols
  ll=VIEWLIGHT[None,:]@np.stack([right,up,forward],axis=1).T if planet else LIGHT[None,:]
  diffuse=np.maximum(0,np.sum(nn*ll,axis=1))
  lit=base*(.30+.70*diffuse[:,None])
  halfv=norm(ll[0]+forward)
  spec=np.maximum(0,nn@halfv)**(12 if planet else 35)
  rough=MATS[mat][1]
  lit+=spec[:,None]*(1-rough)*.35
  if mat in EMISSIVE:lit=base*.9+np.array(EMISSIVE[mat])*.12
  if planet and mat=='Gas':
   # Thin blue atmospheric limb, baked only into the review render.
   rim=(1-np.maximum(0,nn@forward))**5
   lit+=rim[:,None]*np.array([.11,.25,.30])
  target[mask]=zz[mask];pixels[y0:y1+1,x0:x1+1][mask]=np.clip(lit,0,1)
 image=Image.fromarray((pixels*255).astype(np.uint8))
 return image.resize((w,h),Image.Resampling.LANCZOS)

def font(size):return ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',size)
def sheet_base(title,subtitle,w=2000,h=1300):
 im=Image.new('RGB',(w,h),(15,24,38));d=ImageDraw.Draw(im)
 d.text((60,35),title,font=font(42),fill=(232,235,220))
 d.text((63,100),subtitle,font=font(23),fill=(124,166,182));return im

models=[landing('LandingPad_MkII',False),landing('StarterPad_MkII',True),sphere_model('RingedGasGiant','gas'),sphere_model('DistantMoon','moon',5),
        sphere_model('Asteroid_Basalt','rock',3),sphere_model('Asteroid_Warm','rock',4),sphere_model('Asteroid_Icy','rock',5)]
stats=[export(m)for m in models]
(ROOT/'manifest.json').write_text(json.dumps({'package':'Waystation Art 01','status':'STAGED ONLY - not installed in Unity','models':stats},indent=2))
print(json.dumps(stats,indent=2),flush=True)

im=sheet_base('WAYSTATION / DOCKING PLATFORM Mk II','ASSET REVIEW 01   /   Isometric station tycoon   /   Staged models - not installed')
im.paste(render(models[1],1220,940,284,35,34),(20,155))
im.paste(render(models[0],680,450,155,0,76),(1290,170))
im.paste(render(models[1],680,430,150,140,-22,focus=(0,-.28,0)),(1290,690))
d=ImageDraw.Draw(im)
d.text((65,1130),'STARTER PAD / fuel reservoir + console',font=font(26),fill=(226,228,213))
d.text((65,1180),'Chamfered chassis  /  Segmented heat deck  /  Guidance lights  /  Exposed service truss',font=font(22),fill=(124,166,182))
d.text((1310,635),'LANDING PAD / overhead',font=font(23),fill=(226,228,213))
d.text((1310,1130),'UNDERSIDE / structure + thrusters',font=font(23),fill=(226,228,213))
d.text((65,1240),'3 x 3 footprint  /  Deck at Y = 0  /  GLB + grouped OBJ sources  /  Clear central touchdown area',font=font(20),fill=(124,166,182))
im.save(PRE/'LandingPads.png')
print('Pad review rendered.',flush=True)

im=sheet_base('WAYSTATION / ORBITAL BACKGROUND KIT','ASSET REVIEW 01   /   Banded gas giant, fine ring gaps, distant moon + asteroid variants')
im.paste(render(models[2],1300,990,300,35,23,focus=(0,0,0),planet=True),(0,150))
im.paste(render(models[3],380,360,138,30,20,focus=(0,0,0),planet=True),(1490,155))
for k,model in enumerate(models[4:]):im.paste(render(model,340,300,105,45,23,focus=(0,0,0)),(1250+(k%2)*360,560+(k//2)*320))
d=ImageDraw.Draw(im)
d.text((65,1120),'RINGED GAS GIANT / 3D sphere + annular rings',font=font(26),fill=(226,228,213))
d.text((65,1170),'Flowing cloud bands  /  Oval storm  /  Fine radial ring structure  /  Smooth surface normals',font=font(21),fill=(124,166,182))
d.text((1540,510),'DISTANT MOON',font=font(21),fill=(226,228,213))
d.text((1280,845),'BASALT',font=font(20),fill=(226,228,213));d.text((1640,845),'WARM ROCK',font=font(20),fill=(226,228,213))
d.text((1640,1030),'ICE-VEINED',font=font(20),fill=(226,228,213))
d.text((65,1240),'Independent 3D assets  /  Y-up  /  PBR + vertex colour  /  No scene, gameplay or renderer changes',font=font(20),fill=(124,166,182))
im.save(PRE/'OrbitalBackground.png')
print('Background review rendered.',flush=True)
