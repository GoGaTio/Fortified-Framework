using UnityEngine;
using Verse;

namespace Fortified
{
    public class AmmoOption
    {
        [MustTranslate]
        public string label;
        public ThingDef projectileDef;
        public string iconPath;
		[MustTranslate]
		public string description;
        public float warmUpFactor = 1f;
		public float accuracyFactor = 1f;
		public float rangeFactor = 1f;
		public int? burstShotCountOverride = null;

        public Texture2D ResolveIcon()
        {
            if (!iconPath.NullOrEmpty())
            {
                return ContentFinder<Texture2D>.Get(iconPath, false);
            }
            return projectileDef?.uiIcon;
        }

        public string ResolveLabel()
        {
            if (!label.NullOrEmpty()) return label;
            return projectileDef?.label ?? "N/A";
        }
    }
}