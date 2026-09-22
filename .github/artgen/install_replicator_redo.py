from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Textures' / 'Things' / 'Pawn' / 'Replicator'
ROLES = ['Artillery','Bulwark','Burrower','Controller','Drone','Hunter','Repairer','SiegeMass','Titan']
DIRS = ['', '_north', '_east', '_south', '_west']

# The 45 authored sprites are committed directly to the live texture paths.
# This script is an integrity check only; it never regenerates or overwrites the artwork.
for role in ROLES:
    for suffix in DIRS:
        path = OUT / f'WNG_Replicator{role}{suffix}.png'
        if not path.exists():
            raise FileNotFoundError(path)
        with Image.open(path) as im:
            im.load()
            if im.size != (512,512) or im.mode != 'RGBA':
                raise RuntimeError(f'{path}: expected 512x512 RGBA, got {im.size} {im.mode}')
            alpha = im.getchannel('A')
            if alpha.getbbox() is None:
                raise RuntimeError(f'{path}: empty alpha')
            edges = [
                alpha.crop((0,0,512,1)).getextrema()[1],
                alpha.crop((0,511,512,512)).getextrema()[1],
                alpha.crop((0,0,1,512)).getextrema()[1],
                alpha.crop((511,0,512,512)).getextrema()[1],
            ]
            if any(edges):
                raise RuntimeError(f'{path}: alpha touches canvas edge {edges}')
print(f'Validated {len(ROLES)*len(DIRS)} professional six-legged Replicator pawn sprites.')
