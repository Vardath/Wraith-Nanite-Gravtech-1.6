using System;
using System.Linq;
using System.Reflection;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Marks a WNG-owned Goa'uld buildable that should live in ONAC's Architect category when ONAC
    /// exists, but remain available through WNG's own category when ONAC is absent.
    /// </summary>
    public sealed class WNGGoauldArchitectExtension : DefModExtension
    {
    }

    [StaticConstructorOnStartup]
    public static class WNGGoauldArchitectRouter
    {
        private const string WngCategoryDefName = "WNG";

        static WNGGoauldArchitectRouter()
        {
            LongEventHandler.ExecuteWhenFinished(RouteBuildables);
        }

        private static void RouteBuildables()
        {
            try
            {
                DesignationCategoryDef fallback = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(WngCategoryDefName);
                DesignationCategoryDef onac = WNGOptionalIntegrations.ResolveOnacArchitectCategory();
                DesignationCategoryDef target = onac ?? fallback;

                if (target == null)
                {
                    Log.Error("[WNG] Could not resolve either the ONAC Architect category or WNG fallback category for Goa'uld buildables.");
                    return;
                }

                int routed = 0;
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(d => d.GetModExtension<WNGGoauldArchitectExtension>() != null))
                {
                    if (def.designationCategory == target)
                        continue;
                    def.designationCategory = target;
                    routed++;
                }

                // DesignationCategoryDef caches its build designators after Def resolution. Rebuild
                // every category after routing so a Goa'uld buildable cannot remain duplicated in
                // WNG while also appearing under ONAC, or disappear after a category move.
                MethodInfo resolve = typeof(DesignationCategoryDef).GetMethod("ResolveDesignators", BindingFlags.Instance | BindingFlags.NonPublic);
                if (resolve == null)
                {
                    Log.Error("[WNG] RimWorld 1.6 DesignationCategoryDef.ResolveDesignators could not be found; Goa'uld Architect routing cannot refresh the UI cache.");
                    return;
                }

                foreach (DesignationCategoryDef category in DefDatabase<DesignationCategoryDef>.AllDefs)
                {
                    resolve.Invoke(category, null);
                    category.DirtyCache();
                }

                if (WNGOptionalIntegrations.OnacActive && onac == null)
                    Log.Warning("[WNG] ONAC is active but ONAC_Architect was not found; Goa'uld WNG construction has fallen back to the WNG Architect category.");

                if (Prefs.DevMode && routed > 0)
                    Log.Message($"[WNG] Routed {routed} Goa'uld buildable(s) to Architect category {target.defName}.");
            }
            catch (Exception ex)
            {
                Log.Error("[WNG] Failed to route Goa'uld buildables between ONAC and WNG Architect categories: " + ex);
            }
        }
    }
}
