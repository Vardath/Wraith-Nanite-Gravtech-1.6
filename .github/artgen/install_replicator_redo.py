from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / '.github' / 'arttransfer' / 'replicator_redo'
OUT = ROOT / 'Textures' / 'Things' / 'Pawn' / 'Replicator'
ROLES = ['Artillery','Bulwark','Burrower','Controller','Drone','Hunter','Repairer','SiegeMass','Titan']
DIRS = ['', '_north', '_east', '_south', '_west']

OUT.mkdir(parents=True, exist_ok=True)
for role in ROLES:
    for suffix in DIRS:
        src = SRC / f'WNG_Replicator{role}{suffix}.png'
        dst = OUT / src.name
        if not src.exists():
            raise FileNotFoundError(src)
        with Image.open(src) as im:
            im.load()
            if im.size != (512,512) or im.mode != 'RGBA':
                raise RuntimeError(f'{src}: expected 512x512 RGBA, got {im.size} {im.mode}')
            alpha = im.getchannel('A')
            if alpha.getbbox() is None:
                raise RuntimeError(f'{src}: empty alpha')
            # transparent edge safety
            edges = [
                alpha.crop((0,0,512,1)).getextrema()[1],
                alpha.crop((0,511,512,512)).getextrema()[1],
                alpha.crop((0,0,1,512)).getextrema()[1],
                alpha.crop((511,0,512,512)).getextrema()[1],
            ]
            if any(edges):
                raise RuntimeError(f'{src}: alpha touches canvas edge {edges}')
            im.save(dst, 'PNG', optimize=True)
print(f'Installed {len(ROLES)*len(DIRS)} professional six-legged Replicator pawn sprites.')
