from pathlib import Path
import torch
from diffusers import StableDiffusionPipeline, LCMScheduler
from PIL import Image
from rembg import remove

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/"ArtSource"/"Apparel"/"human_form_uniform"/"candidates"
OUT.mkdir(parents=True,exist_ok=True)

MODEL_URL="https://huggingface.co/Lykon/dreamshaper-8-lcm/resolve/main/DreamShaper8_LCM.safetensors"
pipe=StableDiffusionPipeline.from_single_file(
    MODEL_URL,
    torch_dtype=torch.bfloat16,
    safety_checker=None,
    requires_safety_checker=False,
)
pipe.scheduler=LCMScheduler.from_config(pipe.scheduler.config)
pipe=pipe.to("cpu")
pipe.enable_attention_slicing()
pipe.enable_vae_tiling()

prompt="""masterpiece professional hand-painted game apparel concept art, isolated empty Stargate Atlantis Asuran human-form Replicator uniform garment, front view, no wearer, no mannequin, no human body, no arms, no legs, fitted futuristic tunic torso garment, elegant Ancient technology tailoring, warm ivory and muted stone-grey technical woven cloth, charcoal-brown central underlayer, subtle bronze-gunmetal fasteners, layered stitched fabric, real seams, soft folds, cloth tension, edge wear, brushed textile grain, asymmetrical production wear, physically believable fabric, cinematic broad-form lighting, painterly texture, high detail, dark neutral studio background, centered single object, production-quality RimWorld mod asset source painting, organic tailored costume, practical screen-used sci-fi wardrobe, realistic materials, intricate but restrained"""
negative="""person, human, mannequin, face, head, neck, skin, hands, arms, legs, pants, boots, character, model, cartoon, vector, cel shading, icon, UI, flat panels, armor plates, hard white geometric panels, superhero armor, marine armor, power armor, robot, glowing outline, neon cyan piping, clean plastic, glossy white plastic, spaceship parts, weapon, text, logo, watermark, duplicate garment, clutter, floor, scenery"""

for i,seed in enumerate((84117,84139,84163)):
    g=torch.Generator(device="cpu").manual_seed(seed)
    im=pipe(
        prompt=prompt,
        negative_prompt=negative,
        width=512,height=512,
        num_inference_steps=6,
        guidance_scale=1.7,
        generator=g,
    ).images[0].convert("RGBA")
    cut=remove(im)
    bb=cut.getchannel("A").getbbox()
    if bb:
        crop=cut.crop(bb)
        ratio=min(900/crop.width,900/crop.height)
        crop=crop.resize((max(1,int(crop.width*ratio)),max(1,int(crop.height*ratio))),Image.Resampling.LANCZOS)
        canvas=Image.new("RGBA",(1024,1024),(0,0,0,0))
        canvas.alpha_composite(crop,((1024-crop.width)//2,(1024-crop.height)//2))
    else:
        canvas=im.resize((1024,1024),Image.Resampling.LANCZOS)
    canvas.save(OUT/f"south_{i+1}.png",optimize=True)
print("generated",len(list(OUT.glob("south_*.png"))))
