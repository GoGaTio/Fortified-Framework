using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Fortified
{
    // 侦察小队Lord仅含自定义侦察duty与撤离过场不继承AssaultColony避免直冲炮塔
    public class LordJob_FFFScoutAssault : LordJob
    {
        private Faction assaulterFaction;
        private bool useAvoidGridSmart;

        public override bool AddFleeToil => false;
        public override bool GuiltyOnDowned => true;

        public LordJob_FFFScoutAssault() { }

        public LordJob_FFFScoutAssault(Faction assaulterFaction, bool useAvoidGridSmart = false)
        {
            this.assaulterFaction = assaulterFaction;
            this.useAvoidGridSmart = useAvoidGridSmart;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var scout = new LordToil_FFFScout();
            if (useAvoidGridSmart) scout.useAvoidGrid = true;
            graph.AddToil(scout);
            graph.StartingToil = scout;
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref assaulterFaction, "assaulterFaction");
            Scribe_Values.Look(ref useAvoidGridSmart, "useAvoidGridSmart", false);
        }
    }
}
