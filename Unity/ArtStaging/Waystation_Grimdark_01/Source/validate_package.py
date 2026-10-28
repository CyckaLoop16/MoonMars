from pathlib import Path
import json,struct,math
import numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[1]
manifest=json.loads((root/'manifest.json').read_text())
report=[]
for item in manifest['models']:
 path=root/'Models'/(item['name']+'.glb');data=path.read_bytes()
 magic,version,length=struct.unpack_from('<III',data)
 assert magic==0x46546c67 and version==2 and length==len(data)
 size,kind=struct.unpack_from('<II',data,12);assert kind==0x4e4f534a
 doc=json.loads(data[20:20+size]);offset=20+size
 binsize,bintype=struct.unpack_from('<II',data,offset);assert bintype==0x004e4942
 binary=data[offset+8:offset+8+binsize];assert len(binary)==doc['buffers'][0]['byteLength']
 count=0
 def array(index):
  a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
  assert a['componentType']==5126
  components={'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
  start=v.get('byteOffset',0)+a.get('byteOffset',0)
  arr=np.frombuffer(binary,dtype='<f4',count=a['count']*components,offset=start).reshape((-1,components))
  assert np.isfinite(arr).all()
  return arr
 for mesh in doc['meshes']:
  for primitive in mesh['primitives']:
   attr=primitive['attributes'];p=array(attr['POSITION']);n=array(attr['NORMAL']);uv=array(attr['TEXCOORD_0']);c=array(attr['COLOR_0'])
   assert len(p)==len(n)==len(uv)==len(c) and len(p)%3==0
   assert np.allclose(np.linalg.norm(n,axis=1),1,atol=.002)
   assert c.min()>=0 and c.max()<=1.001
   tris=p.reshape((-1,3,3));assert (np.linalg.norm(np.cross(tris[:,1]-tris[:,0],tris[:,2]-tris[:,0]),axis=1)>1e-10).all()
   count+=len(p)//3
 for image in doc['images']:
  view=doc['bufferViews'][image['bufferView']];start=view.get('byteOffset',0);png=binary[start:start+view['byteLength']]
  assert png.startswith(b'\x89PNG\r\n\x1a\n')
  assert struct.unpack_from('>II',png,16)[0]>0
 assert count==item['triangles'] and len(doc['images'])==item['embedded_textures']
 if 'Pad' in item['name']:
  for mesh in doc['meshes']:
   for primitive in mesh['primitives']:
    p=array(primitive['attributes']['POSITION'])
    assert abs(p[:,0]).max()<=1.5001 and abs(p[:,2]).max()<=1.5001
 report.append(f"PASS {item['name']}: {count} triangles; valid UVs/normals; {len(doc['images'])} embedded PNG textures; all geometry finite and non-degenerate.")
for p in (root/'Textures').glob('*.png'):
 with Image.open(p) as im:im.verify()
report.append('PASS: all texture files decode as PNG; model counts match the manifest.')
report.append('Status: staged outside Assets. No game integration performed.')
text='\n'.join(report)+'\n';(root/'Validation.txt').write_text(text);print(text)
