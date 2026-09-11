from pathlib import Path
import xml.etree.ElementTree as ET

files = list(Path('Defs').rglob('*.xml')) + list(Path('Patches').rglob('*.xml'))
for p in files:
    ET.parse(p)

h = Path('Source/WNG/Wraith/WraithFactionHunger.cs').read_text(encoding='utf-8')
d = Path('Source/WNG/Wraith/WraithFeedingRequestDialog.cs').read_text(encoding='utf-8')

required_hunger = [
    'OpenFeedingRequest(faction, home, subjects, record, ext, now)',
    'Dialog_WraithFeedingSubjectSelection',
    'OpenInvolvedWraithConfirmation',
    'Involved Wraiths: ',
    'ResolveInvolvedWraiths',
    'IsValidInvolvedWraith',
    'faction?.leader',
    'FinishAcceptance',
    'FinishRefusal',
    'AcceptRequest(faction, subject, years, record, ext)',
    'RefuseRequest(faction, record, ext)',
]
required_dialog = [
    'class Dialog_WraithFeedingSubjectSelection : Window',
    'forcePause = true',
    'absorbInputAroundWindow = true',
    'doCloseX = false',
    'closeOnClickedOutside = false',
    'individual Wraiths are not selected here',
    '"Cancel"',
    '"Submit"',
    'OnCancelKeyPressed',
    'OnAcceptKeyPressed',
]
missing = [v for v in required_hunger if v not in h] + [v for v in required_dialog if v not in d]
if missing:
    raise SystemExit('Missing strategic-hunger UI invariants: ' + ', '.join(missing))

if 'OpenFeedingSubjectMenu' in h or 'new FloatMenu(' in h:
    raise SystemExit('Old non-paused FloatMenu prisoner stage remains in strategic hunger source')
if 'PawnGenerator.GeneratePawn' in h or 'PawnGenerator.GeneratePawn' in d:
    raise SystemExit('Strategic hunger UI must not fabricate presentation-only Wraith identities')
if 'DrainLife' in h or 'WNG_WraithFeedingNiche' in h or 'CompWraithDormancyVault' in h or 'WNG_WraithGrowthChamber' in h:
    raise SystemExit('Strategic hunger UI pass crossed into ordinary feeding/Mature-Hive/Growth-Chamber systems')

print(f'Parsed {len(files)} XML files and verified two-stage paused strategic-hunger request invariants')
