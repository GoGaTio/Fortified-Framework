using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Fortified
{
    public class ScenPart_GeneratePrefab : ScenPart
    {
        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect scenPartRect = listing.GetScenPartRect(this, height: (maps.Count * 30f) + ScenPart.RowHeight);
            ScenPart_GeneratePrefab.DrawButtonWithIcon(scenPartRect.y, () => ScenPart_GeneratePrefab.DrawFloatMenu(DefDatabase<PrefabDef>.AllDefsListForReading, d => maps.Add(d), d => d.defName), () =>
            ScenPart_GeneratePrefab.DrawFloatMenu(maps, d => maps.Remove(d), d => d.defName), scenPartRect.width + 20f, 20);
            scenPartRect.y += 30f;
            maps.ForEach(m =>
            {
                Widgets.Label(scenPartRect, m.defName);
                scenPartRect.y += 30f;
            });
        }
        public static void DrawFloatMenu<T>(List<T> list, Action<T> action, Func<T, string> text, List<FloatMenuOption> extra = null, Func<T, bool> validator = null)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            if (extra != null)
            {
                options.AddRange(extra);
            }
            foreach (T t in list)
            {
                if (validator == null || validator(t))
                {
                    FloatMenuOption option = new FloatMenuOption(text(t), () =>
                    {
                        action(t);
                    });
                    options.Add(option);
                }
            }
            if (options.Any())
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        public static void DrawButtonWithIcon(float y, Action addAction, Action removeAction, float x = 10f, float iconSize = 25f, float interval = 35f, Vector2? size = null)
        {
            if (Widgets.ButtonImage(new Rect(x, y, iconSize, iconSize), TexButton.Plus))
            {
                addAction();
            }
            if (Widgets.ButtonImage(new Rect(x + interval, y, iconSize, iconSize), TexButton.Delete))
            {
                removeAction();
            }
        }
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (map == null || !map.IsStartingMap || map.IsPocketMap) return;

            var prefab = maps.RandomElement();

            CellRect cellRect = GenAdj.OccupiedRect(map.Center, Rot4.Random, prefab.size);
            foreach (var c in cellRect)
            {
                foreach (var item in c.GetThingList(map).ListFullCopy())
                {
                    if (ShouldDestroy(item, (float)(prefab.size.x / 2.5f)))
                    {
                        item.Destroy();
                        if (map.roofGrid.Roofed(item.Position)) map.roofGrid.SetRoof(item.Position, null);
                    }
                }
            }
            PrefabUtility.SpawnPrefab(prefab, map, map.Center, Rot4.Random, Faction.OfPlayer,onSpawned: OnSpawned);
            map.fogGrid.Refog(map.BoundsRect());
        }
        private void OnSpawned(Thing thing)
        {
            var t = thing.Position.GetEdificeSafe(thing.Map);
            if (t != null && t != thing)
            {
                t.Destroy(DestroyMode.Vanish);
            }
        }
        private bool ShouldDestroy(Thing item, float cleanRange = 0)
        {
            if (!item.def.destroyable) return false;
            if (item is Skyfaller) return false;
            if (item is Building && (item.Position - item.Map.Center).Magnitude < cleanRange) return true;
            if (item is Pawn p && p.Faction != Faction.OfPlayer) return true;
            if (item.HasThingCategory(ThingCategoryDefOf.PlantMatter)) return false;
            if (item.HasThingCategory(ThingCategoryDefOf.Items)) return false;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref this.maps, "maps", LookMode.Def);
        }

        public List<PrefabDef> maps = new List<PrefabDef>();
    }
    public class GenStep_Prefab : GenStep_Scatterer
    {
        public override int SeedPart => 114514;
        public PrefabDef PrefabDef = null;

        public void SetPrefab(PrefabDef prefabDef)
        {
            this.PrefabDef = prefabDef;
        }

        protected override void ScatterAt(IntVec3 loc, Map map, GenStepParams parms, int count = 1)
        {
            throw new NotImplementedException();
        }
    }
}