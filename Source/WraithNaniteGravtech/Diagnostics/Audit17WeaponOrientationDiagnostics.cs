using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit17WeaponOrientationDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 17 - weapon orientation / rendering",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            List<ThingDef> weapons = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true &&
                            d.thingCategories != null &&
                            d.thingCategories.Any(cat =>
                                cat?.defName == "WeaponsRanged" ||
                                cat?.defName == "WeaponsMelee"))
                .OrderBy(d => d.defName)
                .ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 17] WEAPON ORIENTATION / RENDERING");
            sb.AppendLine("Hand-held WNG weapons: " + weapons.Count);

            foreach (ThingDef def in weapons)
            {
                string role = def.IsRangedWeapon ? "ranged" : "melee";
                try
                {
                    ThingWithComps weapon = ThingMaker.MakeThing(def) as ThingWithComps;
                    if (weapon == null)
                    {
                        failures.Add(def.defName + " did not instantiate as ThingWithComps.");
                        continue;
                    }

                    Graphic graphic = weapon.Graphic;
                    if (graphic == null || graphic.MatSingle == null)
                        failures.Add(def.defName + " failed to resolve a render graphic/material.");

                    Vector2 draw = def.graphicData?.drawSize ?? Vector2.one;
                    if (draw.x <= 0f || draw.y <= 0f)
                        failures.Add(def.defName + " has invalid draw size " + draw);

                    sb.AppendLine(" - " + def.defName +
                                  " [" + role + "] angle=" + def.equippedAngleOffset +
                                  " draw=" + draw);
                }
                catch (Exception ex)
                {
                    failures.Add(def.defName + " render/instantiation probe threw " +
                                 ex.GetType().Name + ": " + ex.Message);
                }
            }

            ThingDef stable = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_PrecursorPulseRifle");
            ThingDef recovered = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_RecoveredPrecursorPulseRifle");
            if (stable == null || Math.Abs(stable.equippedAngleOffset - 180f) > 0.01f)
                failures.Add("WNG_PrecursorPulseRifle lost its 180-degree hold correction.");
            if (recovered == null || Math.Abs(recovered.equippedAngleOffset - 180f) > 0.01f)
                failures.Add("WNG_RecoveredPrecursorPulseRifle lost its 180-degree hold correction.");

            sb.AppendLine("MANUAL VISUAL CHECK STILL REQUIRED:");
            sb.AppendLine("Equip each listed weapon and inspect North/East/South/West while drafted and undrafted.");
            sb.AppendLine("Check muzzle/blade points away from the pawn, sprite flip/handedness is sensible, and scale/offset is acceptable.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 17 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("AUTOMATED PASS: render assets resolve. Manual four-direction orientation check remains required.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 17 automated checks PASS; manual orientation check still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
