from PIL import Image, ImageDraw, ImageFilter, ImageChops
from pathlib import Path
import math, random, hashlib, os
import numpy as np

ROOT = Path(os.environ.get('WNG_APPAREL_ROOT','Textures/Things/Pawn/Humanlike/Apparel'))
ONLY_SAMPLES = os.environ.get('ONLY_SAMPLES','0')=='1'
SCALE=3
FAMILIES={
'Wraith':{'WNG_HunterCoat':'hunter','WNG_WarriorCarapace':'warrior','WNG_CommanderCarapace':'commander','WNG_QueenRaiment':'queen'},
'Precursor':{'WNG_HumanFormUniform':'asuran_uniform','WNG_HumanFormCombatArmor':'asuran_armor','WNG_PrecursorUniform':'precursor_uniform','WNG_PrecursorFieldArmor':'precursor_field','WNG_PrecursorCommandArmor':'precursor_command'}}
P={
'hunter':((31,24,38),(105,73,116),(12,9,16),(116,46,144),(172,70,212),(89,70,98)),
'warrior':((30,25,39),(113,82,124),(10,9,14),(137,52,155),(202,72,224),(103,84,112)),
'commander':((35,28,44),(130,93,140),(11,9,15),(162,75,181),(105,164,247),(185,150,193)),
'queen':((28,19,37),(116,69,126),(9,7,14),(154,47,135),(216,72,196),(190,138,193)),
'asuran_uniform':((59,65,73),(184,194,201),(19,23,29),(50,158,181),(59,222,243),(107,125,137)),
'asuran_armor':((78,87,98),(215,224,230),(20,25,32),(44,168,195),(61,226,248),(130,145,158)),
'precursor_uniform':((142,143,136),(239,235,219),(40,43,46),(55,159,181),(68,221,240),(182,143,77)),
'precursor_field':((117,128,139),(229,234,236),(32,37,43),(48,170,197),(63,224,247),(164,137,84)),
'precursor_command':((126,136,145),(236,238,238),(31,36,42),(52,174,199),(69,228,249),(210,165,71))}

def direction(n):
 s=n.lower()
 for d in ('north','south','east','west'):
  if s.endswith('_'+d+'.png'): return d
 return 'south'

def np_base(mask,pal,seed):
 w,h=mask.size; m=np.asarray(mask,dtype=np.float32)/255.0
 yy,xx=np.mgrid[0:h,0:w]
 nz=np.argwhere(m>0.01)
 if not len(nz): return Image.new('RGBA',(w,h),(0,0,0,0))
 y0,x0=nz.min(0); y1,x1=nz.max(0)+1
 cx=(x0+x1-1)/2; half=max(1,(x1-x0)/2)
 dome=np.clip(1-np.abs(xx-cx)/half,0,1)
 v=np.clip((yy-y0)/max(1,y1-y0-1),0,1)
 rng=np.random.default_rng(seed)
 small=rng.normal(0,1,(max(2,h//12),max(2,w//12))).astype(np.float32)
 nn=((small-small.min())/(small.max()-small.min()+1e-6)*255).astype(np.uint8)
 noise=np.asarray(Image.fromarray(nn).resize((w,h),Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(2)),dtype=np.float32)/255-.5
 base=np.array(pal[0],dtype=np.float32); light=np.array(pal[1],dtype=np.float32)
 t=np.clip(.18+.23*dome+.10*(1-v)+.10*noise,0,.58)[...,None]
 rgb=base*(1-t)+light*t
 er=np.asarray(mask.filter(ImageFilter.MinFilter(7)),dtype=np.float32)/255
 edge=np.clip(m-er,0,1)[...,None]
 rgb*=1-.48*edge
 a=(m*255).astype(np.uint8)
 arr=np.dstack([np.clip(rgb,0,255).astype(np.uint8),a])
 return Image.fromarray(arr,'RGBA')

def pt(bb,x,y,S=1): x0,y0,x1,y1=bb; return (int((x0+(x1-x0)*x)*S),int((y0+(y1-y0)*y)*S))
def poly(d,bb,pts,fill,S): d.polygon([pt(bb,x,y,S) for x,y in pts],fill=fill)
def ln(d,bb,pts,fill,w,S): d.line([pt(bb,x,y,S) for x,y in pts],fill=fill,width=max(1,int(w*S)),joint='curve')
def el(d,bb,box,fill,outline=None,w=1,S=1): a=pt(bb,box[0],box[1],S); b=pt(bb,box[2],box[3],S); d.ellipse((*a,*b),fill=fill,outline=outline,width=max(1,int(w*S)))
def clipped_comp(base,layer,mask):
 la=ImageChops.multiply(layer.getchannel('A'),mask); layer=layer.copy(); layer.putalpha(la); return Image.alpha_composite(base,layer)

def paint(path,style):
 src=Image.open(path).convert('RGBA'); mask=src.getchannel('A'); bb=mask.getbbox()
 if not bb: return
 pal=P[style]; seed=int(hashlib.sha1((path.name+style).encode()).hexdigest()[:8],16)
 base=np_base(mask,pal,seed)
 b=base.resize((192*SCALE,192*SCALE),Image.Resampling.BICUBIC); mh=mask.resize(b.size,Image.Resampling.LANCZOS)
 lay=Image.new('RGBA',b.size,(0,0,0,0)); d=ImageDraw.Draw(lay,'RGBA'); dr=direction(path.name); side=dr in ('east','west'); back=dr=='north'
 dark=(*pal[2],235); accent=(*pal[3],200); glow=(*pal[4],245); trim=(*pal[5],225); light=(*pal[1],235); basec=(*pal[0],235)
 organic=style in ('hunter','warrior','commander','queen')
 if organic:
  if side: poly(d,bb,[(.18,.15),(.78,.12),(.87,.45),(.76,.88),(.24,.90),(.30,.47)],dark,SCALE)
  else: poly(d,bb,[(.28,.12),(.72,.12),(.79,.44),(.69,.90),(.31,.90),(.21,.44)],dark,SCALE)
  if style=='hunter':
   if side:
    poly(d,bb,[(.08,.20),(.42,.08),(.82,.19),(.68,.36),(.25,.36)],light,SCALE); poly(d,bb,[(.25,.36),(.69,.36),(.76,.89),(.49,.79),(.26,.91)],(*pal[3],135),SCALE)
   else:
    poly(d,bb,[(.04,.24),(.29,.07),(.47,.18),(.39,.36),(.10,.40)],light,SCALE); poly(d,bb,[(.96,.24),(.71,.07),(.53,.18),(.61,.36),(.90,.40)],light,SCALE)
    ln(d,bb,[(.28,.17),(.42,.39),(.48,.86)],trim,1.8,SCALE); ln(d,bb,[(.72,.17),(.58,.39),(.52,.86)],trim,1.8,SCALE)
    if back:
     for yy in [.33,.43,.53,.63,.73]: ln(d,bb,[(.38,yy),(.50,yy+.035),(.62,yy)],accent,1.2,SCALE)
    else: poly(d,bb,[(.42,.31),(.58,.31),(.55,.80),(.50,.88),(.45,.80)],(*pal[3],130),SCALE)
  elif style=='warrior':
   if side: poly(d,bb,[(.04,.18),(.47,.04),(.90,.22),(.74,.39),(.23,.37)],light,SCALE)
   else:
    poly(d,bb,[(.01,.24),(.27,.05),(.47,.16),(.39,.35),(.08,.40)],light,SCALE); poly(d,bb,[(.99,.24),(.73,.05),(.53,.16),(.61,.35),(.92,.40)],light,SCALE)
   for i,yy in enumerate([.30,.41,.52,.63,.74]):
    if side: poly(d,bb,[(.24,yy-.04),(.74,yy-.06),(.82,yy+.03),(.66,yy+.08),(.28,yy+.06)],(*pal[3],150+i*5),SCALE)
    else: poly(d,bb,[(.21,yy-.04),(.50,yy-.075),(.79,yy-.04),(.68,yy+.065),(.50,yy+.095),(.32,yy+.065)],(*pal[3],150+i*5),SCALE)
   if back: ln(d,bb,[(.50,.20),(.50,.84)],glow,1.8,SCALE)
   else: ln(d,bb,[(.34,.16),(.50,.28),(.66,.16)],trim,2.0,SCALE)
  elif style=='commander':
   if side:
    poly(d,bb,[(.07,.19),(.45,.05),(.88,.20),(.72,.36),(.26,.35)],trim,SCALE); ln(d,bb,[(.34,.28),(.56,.46),(.48,.81)],accent,2.0,SCALE)
   else:
    poly(d,bb,[(.03,.24),(.30,.05),(.47,.17),(.39,.35),(.10,.40)],trim,SCALE); poly(d,bb,[(.97,.24),(.70,.05),(.53,.17),(.61,.35),(.90,.40)],trim,SCALE)
    ln(d,bb,[(.30,.18),(.50,.36),(.70,.18)],accent,2.0,SCALE); ln(d,bb,[(.29,.48),(.50,.58),(.71,.48)],trim,1.5,SCALE); ln(d,bb,[(.33,.68),(.50,.77),(.67,.68)],accent,1.3,SCALE)
    if not back: el(d,bb,(.45,.31,.55,.41),glow,(220,230,255,240),1,SCALE)
  else:
   if side:
    poly(d,bb,[(.09,.17),(.42,.03),(.84,.18),(.68,.33),(.27,.34)],trim,SCALE); poly(d,bb,[(.27,.34),(.69,.34),(.80,.89),(.50,.80),(.25,.92)],(*pal[3],150),SCALE)
   else:
    poly(d,bb,[(.04,.22),(.29,.01),(.48,.16),(.39,.35),(.10,.40)],trim,SCALE); poly(d,bb,[(.96,.22),(.71,.01),(.52,.16),(.61,.35),(.90,.40)],trim,SCALE); poly(d,bb,[(.30,.31),(.70,.31),(.64,.87),(.50,.96),(.36,.87)],(*pal[3],155),SCALE)
    for yy in [.39,.49,.59,.69,.79]: ln(d,bb,[(.34,yy),(.50,yy+.035),(.66,yy)],trim,1.3,SCALE)
    if not back: el(d,bb,(.45,.28,.55,.38),glow,(235,180,230,235),1,SCALE)
 else:
  uniform=style in ('asuran_uniform','precursor_uniform'); command=style=='precursor_command'
  if side:
   poly(d,bb,[(.18,.15),(.79,.12),(.87,.41),(.77,.88),(.23,.90),(.30,.47)],dark,SCALE); poly(d,bb,[(.08,.20),(.45,.07),(.84,.20),(.68,.40),(.28,.38)],(*pal[1],210 if uniform else 240),SCALE); poly(d,bb,[(.28,.39),(.70,.40),(.77,.84),(.49,.91),(.24,.79)],basec,SCALE)
  else:
   poly(d,bb,[(.24,.12),(.76,.12),(.81,.44),(.72,.91),(.28,.91),(.19,.44)],dark,SCALE); poly(d,bb,[(.04,.23),(.28,.06),(.47,.17),(.39,.38),(.11,.41)],(*pal[1],210 if uniform else 242),SCALE); poly(d,bb,[(.96,.23),(.72,.06),(.53,.17),(.61,.38),(.89,.41)],(*pal[1],210 if uniform else 242),SCALE)
   poly(d,bb,[(.25,.31),(.50,.22),(.75,.31),(.68,.58),(.50,.67),(.32,.58)],basec,SCALE); poly(d,bb,[(.31,.63),(.50,.70),(.69,.63),(.63,.88),(.50,.93),(.37,.88)],basec,SCALE)
  if side:
   ln(d,bb,[(.28,.24),(.52,.39),(.48,.84)],trim,1.3,SCALE); ln(d,bb,[(.48,.42),(.72,.54),(.59,.72)],accent,1.1,SCALE)
  else:
   ln(d,bb,[(.28,.17),(.50,.31),(.72,.17)],trim,1.2 if uniform else 1.7,SCALE); ln(d,bb,[(.25,.44),(.50,.55),(.75,.44)],accent,1.1,SCALE); ln(d,bb,[(.31,.67),(.50,.75),(.69,.67)],trim,1.1,SCALE)
  if command:
   ln(d,bb,[(.14,.25),(.31,.13),(.50,.26),(.69,.13),(.86,.25)],trim,2.2,SCALE); ln(d,bb,[(.28,.43),(.50,.54),(.72,.43)],trim,1.6,SCALE)
   if not back: el(d,bb,(.455,.30,.545,.39),glow,trim,1.3,SCALE)
  elif style=='precursor_uniform': ln(d,bb,[(.20,.28),(.37,.37),(.50,.33),(.63,.37),(.80,.28)],(*pal[5],160),1.0,SCALE)
  if side: channels=[[(.33,.22),(.50,.40),(.47,.79)]]
  elif back: channels=[[(.32,.30),(.50,.36),(.68,.30)],[(.50,.36),(.50,.82)]]
  else: channels=[[(.31,.27),(.50,.42),(.69,.27)],[(.50,.42),(.50,.81)]]
  gl=Image.new('RGBA',b.size,(0,0,0,0)); gd=ImageDraw.Draw(gl,'RGBA')
  for ch in channels: ln(gd,bb,ch,glow,1.15 if uniform else 1.45,SCALE)
  gblur=gl.filter(ImageFilter.GaussianBlur(2.2*SCALE)); gblur.putalpha(ImageChops.multiply(gblur.getchannel('A'),mh).point(lambda v:int(v*.50))); b=Image.alpha_composite(b,gblur); lay=Image.alpha_composite(lay,gl)
 b=clipped_comp(b,lay,mh)
 hi=Image.new('RGBA',b.size,(0,0,0,0)); hd=ImageDraw.Draw(hi,'RGBA')
 if side: ln(hd,bb,[(.16,.19),(.42,.10),(.72,.18)],(255,255,255,70),.8,SCALE)
 else:
  ln(hd,bb,[(.08,.25),(.28,.10),(.43,.18)],(255,255,255,65),.8,SCALE); ln(hd,bb,[(.92,.25),(.72,.10),(.57,.18)],(255,255,255,65),.8,SCALE)
 b=clipped_comp(b,hi,mh)
 out=b.resize((192,192),Image.Resampling.LANCZOS).filter(ImageFilter.UnsharpMask(.6,115,3)); out.putalpha(mask); out.save(path,optimize=True)

for sub,fams in FAMILIES.items():
 d=ROOT/sub
 for prefix,style in fams.items():
  files=sorted(d.glob(prefix+'*.png'))
  if ONLY_SAMPLES: files=[p for p in files if p.name in {prefix+'.png',prefix+'_Male_south.png',prefix+'_Male_north.png',prefix+'_Male_east.png',prefix+'_Male_west.png'}]
  print(prefix,len(files))
  for p in files: paint(p,style)
