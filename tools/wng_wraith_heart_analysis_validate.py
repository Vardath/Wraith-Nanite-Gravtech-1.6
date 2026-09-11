from pathlib import Path
import xml.etree.ElementTree as ET

xml_files = list(Path('Defs').rglob('*.xml')) + list(Path('Patches').rglob('*.xml'))
for path in xml_files:
    ET.parse(path)

heart_root = ET.parse('Defs/ThingDefs/Wraith_HiveHeart.xml').getroot()
research_root = ET.parse('Defs/ResearchProjectDefs/Research_WraithBootstrap.xml').getroot()

heart = next((x for x in heart_root.findall('ThingDef') if x.findtext('defName') == 'WNG_WraithHiveHeart'), None)
if heart is None:
    raise SystemExit('WNG_WraithHiveHeart missing')
comps = heart.find('comps')
if comps is None:
    raise SystemExit('Hive Heart comps missing')

population = next((x for x in comps.findall('li') if x.get('Class') == 'WraithNaniteGravtech.CompProperties_MatureWraithHivePopulation'), None)
analysis = next((x for x in comps.findall('li') if x.get('Class') == 'CompProperties_CompAnalyzableUnlockResearch'), None)
if population is None or analysis is None:
    raise SystemExit('Hive Heart population or analysis comp missing')
if population.findtext('dormantWakeRadius') != '18' or population.findtext('activeLossesBeforeDormantWake') != '2':
    raise SystemExit('Existing Mature Hive population tuning changed')

expected = {
    'analysisID': '160912002',
    'requiresMechanitor': 'false',
    'analysisDurationHours': '2',
    'destroyedOnAnalyzed': 'false',
    'canStudyInPlace': 'true',
    'activateTexPath': 'UI/Icons/Study',
}
for key, value in expected.items():
    if analysis.findtext(key) != value:
        raise SystemExit(f'Hive Heart analysis invariant failed: {key}')

params = analysis.find('targetingParameters')
if params is None or params.findtext('onlyTargetColonists') != 'true' or params.findtext('canTargetBuildings') != 'false':
    raise SystemExit('Hive Heart analyzer targeting must be colonist-only')

project = next((x for x in research_root.findall('ResearchProjectDef') if x.findtext('defName') == 'WNG_WraithLivingTechnology'), None)
if project is None:
    raise SystemExit('WNG_WraithLivingTechnology missing')
prereqs = [x.text for x in project.findall('./prerequisites/li')]
required = [x.text for x in project.findall('./requiredAnalyzed/li')]
if prereqs != ['Fabrication']:
    raise SystemExit('Wraith Living Technology ordinary prerequisite changed')
if required != ['WNG_WraithHiveHeart']:
    raise SystemExit('Wraith Living Technology must require only analyzed Hive Heart in this pass')

all_analysis_ids = []
for path in Path('Defs').rglob('*.xml'):
    root = ET.parse(path).getroot()
    for li in root.findall('.//li'):
        if li.get('Class') == 'CompProperties_CompAnalyzableUnlockResearch':
            aid = li.findtext('analysisID')
            if aid:
                all_analysis_ids.append((aid, str(path)))
ids = [x[0] for x in all_analysis_ids]
if ids.count('160912002') != 1:
    raise SystemExit('Wraith analysis ID is missing or duplicated')
if '160912001' not in ids:
    raise SystemExit('Existing Replicator evidence analysis ID disappeared')

print(f'Parsed {len(xml_files)} XML files; Wraith Hive Heart evidence bridge invariants passed')
