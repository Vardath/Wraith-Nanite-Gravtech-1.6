using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Explicit marker comp for the mature-swarm controller body. Coordination remains map-based
    /// and cached; this comp gives the XML a concrete runtime contract without making Controllers
    /// mandatory for ordinary Replicator autonomy.
    /// </summary>
    public sealed class CompProperties_ReplicatorController : CompProperties
    {
        public CompProperties_ReplicatorController()
        {
            compClass = typeof(CompReplicatorController);
        }
    }

    public sealed class CompReplicatorController : ThingComp
    {
        public override string CompInspectStringExtra()
        {
            return "Replicator coordination node: improves nearby swarm organization; ordinary Replicators remain autonomous if this body is lost.";
        }
    }
}
