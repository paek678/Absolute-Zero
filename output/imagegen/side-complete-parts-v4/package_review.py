from pathlib import Path
import json, io, zipfile, xml.etree.ElementTree as ET
import numpy as np
from PIL import Image, ImageDraw, ImageFont

P=Path(__file__).resolve().parent
m=json.loads((P/'layout.json').read_text()); size=tuple(m['canvas'])
atlas=Image.open(P/'atlas.png').convert('RGBA')
layers={}
for name,v in m['layers'].items():
    x,y,w,h=v['rect']; layer=Image.new('RGBA',size)
    layer.paste(atlas.crop((x,y,x+w,y+h)),tuple(v['position']))
    layers[name]=layer
def compose(exclude=()):
    im=Image.new('RGBA',size)
    for name in m['drawOrder']:
        if name not in exclude: im.alpha_composite(layers[name])
    return im
assembled=compose()
assert np.array_equal(np.array(assembled),np.array(Image.open(P/'assembled.png')))
bg=(220,229,232,255)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25)
proof=Image.new('RGBA',(1200,750),bg)
for i,(title,exclude) in enumerate([
    ('Assembled',()),
    ('Hair hidden',('backHair','frontHair','frontHair_1','frontHair_2')),
    ('Arms hidden',('arm1','arm2'))]):
    im=compose(exclude); im.thumbnail((400,690),Image.Resampling.LANCZOS)
    proof.alpha_composite(im,(i*400,45)); ImageDraw.Draw(proof).text((i*400+20,10),title,font=font,fill='black')
proof.save(P/'layer_check.png')
root=ET.Element('image',w=str(size[0]),h=str(size[1]),name='Side character v4')
stack=ET.SubElement(root,'stack')
with zipfile.ZipFile(P/'side_character.ora','w') as z:
    z.writestr('mimetype','image/openraster',compress_type=zipfile.ZIP_STORED)
    for name in reversed(m['drawOrder']):
        v=m['layers'][name]; src=f'data/{name}.png'
        z.write(P/'parts'/f'{name}.png',src)
        ET.SubElement(stack,'layer',name=name,src=src,x=str(v['position'][0]),y=str(v['position'][1]),opacity='1.0',visibility='visible',**{'composite-op':'svg:src-over'})
    z.writestr('stack.xml',ET.tostring(root)); z.write(P/'assembled.png','mergedimage.png')
ref=np.array(Image.open(P.parent/'player_side_20260922/player_side_assembled.png').convert('RGBA'))
def metric(im):
    a=np.array(im.convert('RGBA')); ra=ref[:,:,3]>128; aa=a[:,:,3]>128
    return {'silhouette_iou':round(float((ra&aa).sum()/(ra|aa).sum()),5)}
metrics={'atlas_reassembly_pixel_identical':True,'part_count':len(layers),'v4_reference_comparison':metric(assembled),
    'scope':'Static offline assembly only; no Unity, skinning or motion validation. IoU measures silhouette overlap, not artistic likeness.',
    'remaining':['Eye and mouth shapes differ slightly.','Hair tips and leg contours need further art refinement.','Smooth palette fields do not reproduce the reference paint texture.']}
(P/'validation.json').write_text(json.dumps(metrics,indent=2))
(P/'README.md').write_text('''# Side character v4 review

Nine complete parts were redrawn as editable curves in a shared reference coordinate system. Covered surfaces have filled geometry; these are not extracted visible-pixel fragments. Matching materials use common smooth color fields sampled from the reference palette. Hidden attachment edges omit ink strokes.

`atlas.png` and `layout.json` reconstruct `assembled.png` exactly. `side_character.ora` stores the independent layers and original offsets. `drawing_paths.json` and `draw_parts.py` retain editable geometry. `layer_check.png` exposes the underlying head and body.

This remains an art review revision. Reference silhouette, face details, hair tips and paint texture are not identical. Small/large joint movement, skinning and item-use animation have not been verified. No Unity assets were changed.
''')
with zipfile.ZipFile(P/'side_character_review.zip','w',zipfile.ZIP_DEFLATED) as z:
    for f in P.rglob('*'):
        if f.is_file() and f.suffix!='.zip': z.write(f,f.relative_to(P))
print(json.dumps(metrics,indent=2))
