using System.Collections.Generic;
using Verse;

namespace Fortified
{
    // 信息卡显示参数
    public class DamageBlockerStatData
    {
        public float threshold;
        public bool inclusive;
        public int charges;
        public bool isArmorMode;

        // 行为开关
        public bool consumeAbove;
        public bool blockAbove;
        public bool clampAbove;
        public bool consumeBelow;
        public bool blockBelow;

        // 过滤
        public List<DamageDef> allowedDamageDefs;
        public List<DamageDef> excludedDamageDefs;
        public List<string> allowedWeaponTags;
        public bool allowRanged;
        public bool allowMelee;
        public bool allowDirect;

        // ERA
        public List<ERAEntry> eraEntries;
        public bool eraOnConsume;
        public bool eraOnHit;
        public bool eraOnAbove;
        public bool eraOnBelow;

        // 可覆盖显示键
        public string categoryLabelKey;
        public string thresholdLabelKey;
        public string thresholdDescKey;
        public string chargesLabelKey;
        public string chargesDescKey;
        public string eraLabelKey;
        public string behaviorLabelKey;
        public string behaviorDescKey;
        public string filterLabelKey;
        public string filterDescKey;
    }
}
