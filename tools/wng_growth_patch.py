from pathlib import Path

p = Path('Source/WNG/Wraith/WraithMatureHive.cs')
s = p.read_text(encoding='utf-8')
marker = '''        public bool Initialized => initialized;
        public int FoundingPopulationCap => foundingPopulationCap;
        public int LivingDemographicCount => demographicMembers.Count(p => p != null && !p.Dead);
'''
insert = marker + '''
        public bool CanAcceptGrowthReplacement => initialized && foundingPopulationCap > 0 && LivingDemographicCount < foundingPopulationCap;

        public bool TryRegisterGrowthReplacement(Pawn pawn)
        {
            if (!CanAcceptGrowthReplacement || pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != parent.Map || pawn.Faction != parent.Faction)
                return false;
            if (!WraithLifeForceUtility.IsWraith(pawn))
                return false;
            string kind = pawn.kindDef?.defName;
            if (kind != "WNG_WraithHunter" && kind != "WNG_WraithWarrior")
                return false;
            if (demographicMembers.Contains(pawn))
                return false;
            demographicMembers.Add(pawn);
            return true;
        }
'''
if 'TryRegisterGrowthReplacement' not in s:
    if marker not in s:
        raise SystemExit('Hive population API insertion point missing')
    s = s.replace(marker, insert, 1)
p.write_text(s, encoding='utf-8')

h = Path('Defs/ThingDefs/Wraith_HiveHeart.xml')
x = h.read_text(encoding='utf-8')
x = x.replace(
    'The recorded founding Wraith population is the demographic ceiling until a later bounded Growth Chamber layer explicitly replaces losses.',
    'The recorded founding Wraith population is the demographic ceiling; a linked Growth Chamber may replace Hunter/Warrior losses only up to that ceiling.'
)
h.write_text(x, encoding='utf-8')
