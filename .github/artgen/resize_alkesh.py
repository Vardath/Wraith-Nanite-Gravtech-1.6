from pathlib import Path

path = Path('Defs/ThingDefs/Shuttle_Goauld.xml')
text = path.read_text(encoding='utf-8')
marker = '  <!-- Death Glider: exact two-seat fighter, native shuttle travel + WNG physical two-pass sortie. -->'
if marker not in text:
    raise RuntimeError('Death Glider marker not found; refusing broad shuttle edit')
alkesh, rest = text.split(marker, 1)

repls = {
    '<drawSize>(8.8,6.4)</drawSize>': '<drawSize>(9,9)</drawSize>',
    '<size>(8,6)</size>': '<size>(5,9)</size>',
    '<shadowSize>(8.8,6.4)</shadowSize>': '<shadowSize>(5.2,9)</shadowSize>',
    '<shadowData><volume>(7.8,1.4,5.4)</volume><offset>(0,0,0)</offset></shadowData>': '<shadowData><volume>(4.8,1.6,8.2)</volume><offset>(0,0,0)</offset></shadowData>',
    '<interactionCellOffset>(0,0,-4)</interactionCellOffset>': '<interactionCellOffset>(0,0,-5)</interactionCellOffset>',
}

expected = {
    '<drawSize>(8.8,6.4)</drawSize>': 4,
    '<size>(8,6)</size>': 4,
    '<shadowSize>(8.8,6.4)</shadowSize>': 2,
    '<shadowData><volume>(7.8,1.4,5.4)</volume><offset>(0,0,0)</offset></shadowData>': 1,
    '<interactionCellOffset>(0,0,-4)</interactionCellOffset>': 1,
}

# Permit reruns after the correction has already landed.
already = '<size>(5,9)</size>' in alkesh and '<drawSize>(9,9)</drawSize>' in alkesh
if not already:
    for old, n in expected.items():
        got = alkesh.count(old)
        if got != n:
            raise RuntimeError(f'Expected {n} Al\'kesh occurrences of {old!r}, found {got}; refusing partial resize')
    for old, new in repls.items():
        alkesh = alkesh.replace(old, new)

out = alkesh + marker + rest
path.write_text(out, encoding='utf-8')

# Hard assertions: the Al'kesh is a long pyramid-bodied bomber/transport, not a Death-Glider-width flying wing.
check = out.split(marker, 1)[0]
assert check.count('<size>(5,9)</size>') == 4
assert check.count('<drawSize>(9,9)</drawSize>') == 4
assert check.count('<shadowSize>(5.2,9)</shadowSize>') == 2
assert '<shadowData><volume>(4.8,1.6,8.2)</volume><offset>(0,0,0)</offset></shadowData>' in check
assert '<interactionCellOffset>(0,0,-5)</interactionCellOffset>' in check
print("Al'kesh geometry corrected: 5x9 footprint, 9x9 art quad for transparent long-hull sprite, 5.2x9 skyfaller shadow, long landed shadow, boarding cell behind stern.")
