from pathlib import Path
import xml.etree.ElementTree as ET

files = list(Path('Defs').rglob('*.xml')) + list(Path('Patches').rglob('*.xml'))
for p in files:
    ET.parse(p)

g = Path('Source/WNG/Wraith/WraithGrowthChamber.cs').read_text(encoding='utf-8')
m = Path('Source/WNG/Wraith/WraithMatureHive.cs').read_text(encoding='utf-8')
x = Path('Defs/ThingDefs/Wraith_GrowthChamber.xml').read_text(encoding='utf-8')
site = Path('Source/WNG/Wraith/WraithMatureHiveSite.cs').read_text(encoding='utf-8')

required_g = [
    'CompProperties_WraithGrowthChamber',
    'TryRegisterGrowthReplacement',
    'WNG_WraithHiveHeart',
    'WNG_WraithQueen',
    'WNG_WraithHunter',
    'WNG_WraithWarrior',
    'WraithLifeForceUtility.Offset',
    'Biomass.ConsumeFuel',
    'Power.PowerOn',
]
required_m = [
    'CanAcceptGrowthReplacement',
    'TryRegisterGrowthReplacement(Pawn pawn)',
    'kind != "WNG_WraithHunter" && kind != "WNG_WraithWarrior"',
]
required_x = [
    '<defName>WNG_WraithGrowthChamber</defName>',
    '<basePowerConsumption>4000</basePowerConsumption>',
    '<li>WNG_WraithBioSludge</li>',
    '<cycleTicks>60000</cycleTicks>',
    '<biomassCostPerCycle>60</biomassCostPerCycle>',
    '<queenLifeForceCost>0.35</queenLifeForceCost>',
    '<li>WNG_WraithLivingTechnology</li>',
]
missing = [v for v in required_g if v not in g] + [v for v in required_m if v not in m] + [v for v in required_x if v not in x]
if missing:
    raise SystemExit('Missing Growth Chamber invariants: ' + ', '.join(missing))
if 'WNG_WraithGrowthChamber' in site:
    raise SystemExit('Generated Mature Hive site must not receive an inert unpowered Growth Chamber before a ground Wraith power source is implemented')
if 'GetNamedSilentFail("WNG_WraithKeeper")' in g:
    raise SystemExit('Growth Chamber must not clone Keepers')
print(f'Parsed {len(files)} XML files and verified bounded Growth Chamber invariants')
