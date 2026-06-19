using Verse;
using Verse.Grammar;

namespace Fortified
{
    // 支持额外伤害数值的战斗日志
    public class BattleLogEntry_DamageBlocker : BattleLogEntry_Event
    {
        private float damageAmount;

        public BattleLogEntry_DamageBlocker() { }

        public BattleLogEntry_DamageBlocker(Thing subject, RulePackDef eventDef, Thing initiator, float damage)
            : base(subject, eventDef, initiator)
        {
            damageAmount = damage;
        }

        protected override GrammarRequest GenerateGrammarRequest()
        {
            GrammarRequest request = base.GenerateGrammarRequest();
            request.Rules.Add(new Rule_String("DAMAGE", damageAmount.ToString("F0")));
            return request;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref damageAmount, "damageAmount");
        }
    }
}
