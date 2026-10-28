"""Prepare the lit pad revision for Unity. No baked lighting or vertex-colour dependency."""
from pathlib import Path
base=Path(__file__).resolve().parent/'mesh_authoring.py'
exec(base.read_text().split('\nmodels=[')[0],globals())
from PIL import ImageDraw
TARGET=ROOT/'UnityPadRevision';TARGET.mkdir(exist_ok=True)
TEX=TARGET/'Textures';TEX.mkdir(exist_ok=True)
import shutil
for name in ['IndustrialAtlas_BaseColor.png','IndustrialAtlas_Normal.png']:
 shutil.copy2(ROOT/'Textures'/name,TEX/name)
# URP Lit uses metallic in R and smoothness in A, unlike glTF's ORM convention.
orm=np.asarray(Image.open(ROOT/'Textures/IndustrialAtlas_ORM.png').convert('RGB'))
ms=np.zeros((*orm.shape[:2],4),np.uint8);ms[:,:,0]=orm[:,:,2];ms[:,:,3]=255-orm[:,:,1]
Image.fromarray(ms).save(TEX/'IndustrialAtlas_MetallicSmoothness.png')
light=Image.new('RGB',(128,64),(0,0,0));d=ImageDraw.Draw(light)
for x in range(7,124,15):
 for y in range(8,60,15):d.rounded_rectangle((x,y,x+9,y+8),radius=2,fill=(255,245,222))
light.save(TEX/'Floodlight_Emission.png')
screen=Image.new('RGB',(64,64),(0,0,0));d=ImageDraw.Draw(screen)
for y in [10,20,30,40,50]:d.rectangle((6,y,48 if y%20 else 58,y+2),fill=(200,230,255))
screen.save(TEX/'Screen_Emission.png')
Image.new('RGB',(8,8),(255,255,255)).save(TEX/'Indicator_Emission.png')

TINT={'Hull':(1,1,1),'Deck':(.72,.76,.78),'Panel':(.85,.9,.92),'Dark':(.40,.45,.49),'Steel':(1,1,1),
      'Teal':(.85,.92,.78),'Orange':(.9,.88,.8),'Yellow':(.83,.66,.25),'Paint':(.94,.95,.86)}
QUAD={'Hull':(0,1),'Paint':(0,1),'Yellow':(0,1),'Teal':(1,0),'Orange':(1,1)}
ATLAS=set(TINT)
def uv_for(p,n,mat):
 if mat not in ATLAS:
  # Each lamp lens, screen and indicator fills its complete emission texture.
  axis=int(np.argmax(np.abs(np.mean(n,axis=0))))
  q=p[:,[2,1]] if axis==0 else p[:,[0,2]] if axis==1 else p[:,[0,1]]
  lo=q.min(axis=0);hi=q.max(axis=0);return (q-lo)/np.maximum(hi-lo,1e-6)
 axis=int(np.argmax(np.abs(np.mean(n,axis=0))))
 q=p[:,[2,1]] if axis==0 else p[:,[0,2]] if axis==1 else p[:,[0,1]]
 uv=np.column_stack([np.clip((q[:,0]+1.5)/3,0,1),np.clip((q[:,1]+(1.5 if axis==1 else .9))/(3 if axis==1 else 1.8),0,1)])
 return (np.array(QUAD.get(mat,(0,0)))+.015+uv*.97)*.5

MATS['FloodLamp']=('#fff1ca',.4,.05)
MATS['Beacon']=('#e54630',.5,.05)
# Create a head looking along +Z, then aim it at the pad centre.
def floodlight(m,side):
 sx=sz=side;x=sx*1.2;z=sz*1.2
 m.box((x,.035,z),(.18,.07,.18),'Dark',group='Floodlight base')
 for dx,dz in [(-.055,-.055),(.055,.055)]:m.cylinder((x+dx,.076,z+dz),.014,.014,'Steel',8,'Floodlight fasteners')
 m.cylinder((x,.49,z),.034,.84,'Steel',12,'Floodlight mast')
 m.cylinder((x,.19,z),.039,.065,'Yellow',12,'Floodlight mast')
 m.beam((x+side*.065,.08,z),(x+side*.027,.31,z),.014,'Dark',6,'Floodlight brace')
 m.beam((x+side*.04,.12,z+side*.04),(x+side*.04,.89,z+side*.04),.009,'Dark',6,'Floodlight cable')
 centre=np.array([x,1.,z]);forward=norm(np.array([0,.03,0])-centre)
 right=norm(np.cross([0,1,0],forward));up=np.cross(forward,right);rotation=np.column_stack([right,up,forward])
 head=Model('Floodhead')
 head.box((0,0,0),(.34,.17,.12),'Dark',group='Floodlight housing')
 head.box((0,0,.064),(.305,.135,.012),'Steel',group='Floodlight reflector')
 for i in range(2):
  xx=-.074+i*.148
  # A proper emissive map on the lens front; the casing itself stays non-emissive.
  head.box((xx,0,.076),(.126,.104,.018),'FloodLamp',group='Flood_SW' if side<0 else 'Flood_NE')
 for xx in [-.125,-.075,-.025,.025,.075,.125]:head.box((xx,0,-.076),(.012,.15,.045),'Steel',group='Floodlight cooling fins')
 for xx in [-.19,.19]:
  a=centre+right*xx+np.array([0,-.04,0]);b=np.array([x,.88,z])+right*xx
  m.beam(a,b,.017,'Steel',6,'Floodlight yoke')
  m.beam(centre+right*xx,centre+right*(xx*.82),.027,'Steel',10,'Floodlight hinge')
 m.beam((x,.88,z)-right*.19,(x,.88,z)+right*.19,.017,'Steel',6,'Floodlight yoke')
 for f in head.faces:
  f['p']=(np.array(f['p'])@rotation.T+centre).tolist();f['n']=(np.array(f['n'])@rotation.T).tolist();m.faces.append(f)
 if side>0:m.box((x,.78,z),(.18,.15,.13),'Dark',group='Status mounting bracket')
 return {'name':'FX_Flood_SW' if side<0 else 'FX_Flood_NE','position':centre.tolist(),'forward':forward.tolist()}

def revise(starter):
 m=landing('StarterPad' if starter else 'LandingPad',starter)
 # Match Claude's service and FX locations; the original corner console is superseded by the south pump.
 m.faces=[f for f in m.faces if f['g'] not in ['Service console','Fuel coupling']]
 if starter:
  for f in m.faces:
   if f['g']=='Fuel reservoir':
    points=np.array(f['p'])+[-2.03,0,-.03]
    # Keep the relocated tank pipe outside the shuttle's clear touchdown volume.
    if (f['m']=='Orange' and points[:,1].max()<.4) or (f['m']=='Dark' and points[:,1].min()>.1 and points[:,1].max()<.4):points[:,0]-=.15
    f['p']=points.tolist()
 m.box((.6,.22,-1.27),(.30,.44,.22),'Orange',group='Fuel pump')
 m.box((.6,.30,-1.388),(.19,.12,.016),'Dark',group='Fuel pump')
 m.face([(.52,.25,-1.4),(.52,.35,-1.4),(.68,.35,-1.4),(.68,.25,-1.4)],'Glass',group='Pump screen')
 m.cylinder((.6,.46,-1.27),.10,.04,'Steel',16,'Fuel pump')
 # Hose loop kept entirely within the footprint, aligned with the vapour FX outlet.
 for i in range(12):
  a=i*TAU/12;b=(i+1)*TAU/12
  m.beam((.6+.07*math.cos(a),.13+.07*math.sin(a),-1.4),(.6+.07*math.cos(b),.13+.07*math.sin(b),-1.4),.013,'Dark',6,'Fuel hose reel')
 anchors=[floodlight(m,-1),floodlight(m,1)]
 anchors.append({'name':'FX_Status','position':[1.2,.78,1.2],'forward':[0,0,1]})
 anchors.append({'name':'FX_Fuel','position':[.6,.45,-1.27],'forward':[0,1,0]})
 if starter:
  m.cylinder((1.05,.83,-1.05),.024,1.66,'Steel',12,'Communications mast')
  m.beam((1.05,1.45,-1.05),(.88,1.61,-.88),.023,'Steel',8,'Communications mast')
  m.box((1.05,1.70,-1.05),(.16,.06,.16),'Dark',group='Beacon housing')
  m.cylinder((1.05,1.75,-1.05),.051,.06,'Beacon',16,'Beacon lens')
  anchors.append({'name':'FX_Beacon','position':[1.05,1.75,-1.05],'forward':[0,1,0]})
 return m,anchors

materials=[]
for mat in sorted(set(TINT)|{'FloodLamp','Cyan','Red','Glass','Beacon'}):
 col=TINT.get(mat,color(mat))
 # Plain emissive lens bases stay dark: the emission map defines the bright pixels.
 if mat in ['FloodLamp','Glass','Beacon','Cyan','Red']:col=(.025,.025,.025)
 emission=(0,0,0);emissionmap=''
 if mat=='FloodLamp':emission=tuple(v*4 for v in rgb('#ffba69'));emissionmap='Floodlight_Emission.png'
 elif mat=='Glass':emission=tuple(v*1.6 for v in rgb('#9fc4d1'));emissionmap='Screen_Emission.png'
 elif mat in ['Cyan','Red','Beacon']:
  emission=tuple(v*(2.1 if mat!='Cyan' else 1.1)for v in rgb('#ff3c28' if mat!='Cyan' else '#a5bbb0'));emissionmap='Indicator_Emission.png'
 materials.append({'name':mat,'baseColor':list(col),'atlas':mat in ATLAS,'emission':list(emission),'emissionMap':emissionmap})
models=[]
for starter in [False,True]:
 m,anchors=revise(starter);parts=defaultdict(list)
 for tri in m.triangulate():
  mat,group=tri[3],tri[4]
  controlled=group if group in ['Flood_SW','Flood_NE','Beacon lens'] else 'Powered glow' if mat in ['Cyan','Red','Glass'] else 'Structure'
  parts[(mat,controlled)].append(tri)
 result=[]
 for (mat,group),tt in parts.items():
  p=np.concatenate([t[0]for t in tt]);n=np.concatenate([t[1]for t in tt]);uv=np.concatenate([uv_for(t[0],t[1],mat)for t in tt])
  assert np.isfinite(p).all() and np.max(np.abs(p[:,[0,2]]))<=1.5001
  result.append({'name':mat+' '+group,'material':mat,'control':group,'positions':p.ravel().tolist(),'normals':n.ravel().tolist(),'uv':uv.ravel().tolist()})
 models.append({'name':m.name,'parts':result,'anchors':anchors})
(TARGET/'PadSource.json').write_text(json.dumps({'revision':1,'materials':materials,'models':models},separators=(',',':')))
print(json.dumps([{'name':m['name'],'parts':len(m['parts']),'triangles':sum(len(p['positions'])//9 for p in m['parts'])}for m in models],indent=2))
