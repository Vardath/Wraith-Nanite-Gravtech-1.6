using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Legacy save-compatibility shell for the old spawn-gear retention component.
    ///
    /// WNG no longer re-equips or re-wears items after pawn generation. Signature equipment
    /// is now expected to stand on its own through normal RimWorld apparel/weapon scoring,
    /// outfit policy and equipment selection, just like vanilla gear.
    ///
    /// The component type remains so saves created while the old retention system existed
    /// can still deserialize its stored state without a missing-type migration failure.
    /// </summary>
    public sealed class MapComponent_WNGSignatureGearRetention : MapComponent
    {
        private Dictionary<int, int> firstSeenTickByPawnId = new Dictionary<int, int>();
        private List<int> weaponRecoveryAttemptedPawnIds = new List<int>();

        public MapComponent_WNGSignatureGearRetention(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref firstSeenTickByPawnId, "wngSignatureGearFirstSeen", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref weaponRecoveryAttemptedPawnIds, "wngSignatureWeaponRecoveryAttempted", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (firstSeenTickByPawnId == null)
                    firstSeenTickByPawnId = new Dictionary<int, int>();
                if (weaponRecoveryAttemptedPawnIds == null)
                    weaponRecoveryAttemptedPawnIds = new List<int>();

                // Historical data is intentionally discarded. The retention behavior is gone.
                firstSeenTickByPawnId.Clear();
                weaponRecoveryAttemptedPawnIds.Clear();
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            // Intentionally no gear manipulation. WNG equipment must be retained because its
            // own defs are desirable, not because a watchdog keeps putting it back on.
        }
    }
}
