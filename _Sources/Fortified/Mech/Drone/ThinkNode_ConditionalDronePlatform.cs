using Fortified;
using Verse;
using Verse.AI;

namespace Fortified
{
    public class ThinkNode_ConditionalDronePlatform : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            return pawn.TryGetComp<CompMechPlatform>() != null;
        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            ThinkNode_ConditionalDronePlatform obj = (ThinkNode_ConditionalDronePlatform)base.DeepCopy(resolve);
            return obj;
        }
    }
}