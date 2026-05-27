using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{
    // 侦察兵的有限度还击仅当远程武器射程足够才接战否则交给绕路
    public class JobGiver_FFFScoutFight : JobGiver_AIFightEnemies
    {
        // 接战范围倍率以pawn武器射程为参考
        public float reachMultiplier = 1f;
        // 还击射程下限格小于此值的武器视为白刃只在贴脸时才用
        public float minRangedReach = 12f;
        // 受击后维持还击窗口
        public int retaliateWindowTicks = 600;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            var obj = (JobGiver_FFFScoutFight)base.DeepCopy(resolve);
            obj.reachMultiplier = reachMultiplier;
            obj.minRangedReach = minRangedReach;
            obj.retaliateWindowTicks = retaliateWindowTicks;
            return obj;
        }

        // 自定义索敌目标过滤
        protected override bool ExtraTargetValidator(Pawn pawn, Thing target)
        {
            if (!base.ExtraTargetValidator(pawn, target)) return false;
            if (target is Building) return false;
            if (target is Pawn p)
            {
                if (p.Downed || p.Dead) return false;
                if (!p.HostileTo(pawn)) return false;
            }
            return true;
        }

        // 索敌半径动态由武器射程派生
        protected override Job TryGiveJob(Pawn pawn)
        {
            float weaponR = TryGetPrimaryRange(pawn);
            bool recentlyHarmed = Find.TickManager.TicksGame - pawn.mindState.lastHarmTick <= retaliateWindowTicks;
            if (weaponR < minRangedReach && !recentlyHarmed)
            {
                pawn.mindState.enemyTarget = null;
                return null;
            }
            float r = Mathf.Max(8f, weaponR * reachMultiplier);
            targetAcquireRadius = r;
            targetKeepRadius = r + 7f;
            return base.TryGiveJob(pawn);
        }

        // 主武器主动词射程
        private static float TryGetPrimaryRange(Pawn pawn)
        {
            var eq = pawn?.equipment?.Primary;
            if (eq?.def?.Verbs == null || eq.def.Verbs.Count == 0) return 0f;
            float best = 0f;
            for (int i = 0; i < eq.def.Verbs.Count; i++)
            {
                var v = eq.def.Verbs[i];
                if (v == null) continue;
                if (v.range > best) best = v.range;
            }
            return best;
        }
    }
}