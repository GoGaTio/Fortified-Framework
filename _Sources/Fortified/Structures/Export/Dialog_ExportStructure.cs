// 当白昼倾坠之时
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified.Structures
{
    public class Dialog_ExportStructure : Window
    {
        private FFF_ExportUtility.ExportOptions options = new FFF_ExportUtility.ExportOptions();
        private CellRect sourceRect;
        private Map sourceMap;
        private List<Def> uniqueDefs = new List<Def>();
        private List<Thing> uniqueThings = new List<Thing>();
        private Vector2 scrollPos;
        
        public override Vector2 InitialSize => new Vector2(1000f, 650f);

        public Dialog_ExportStructure(CellRect rect, Map map)
        {
            this.sourceRect = rect;
            this.sourceMap = map;
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            
            AnalyzeContent();
        }

        private void AnalyzeContent()
        {
            HashSet<Def> foundDefs = new HashSet<Def>();
            HashSet<Thing> foundThings = new HashSet<Thing>();

            foreach (IntVec3 c in sourceRect)
            {
                TerrainDef terr = c.GetTerrain(sourceMap);
                if (terr != null) foundDefs.Add(terr);

                foreach (Thing t in c.GetThingList(sourceMap))
                {
                    if (IsValidForExport(t))
                    {
                        foundDefs.Add(t.def);
                        foundThings.Add(t);
                    }
                }
            }
            uniqueDefs = foundDefs.OrderBy(d => d is TerrainDef ? 0 : 1).ThenBy(d => d.label).ToList();
            // 按 AltitudeLayer 排序，确保预览图渲染顺序正确
            uniqueThings = foundThings.OrderBy(t => t.def.altitudeLayer).ToList();
        }

        private bool IsValidForExport(Thing t)
        {
            if (t is Pawn || t.def.category == ThingCategory.Pawn) return false;
            if (t.def.category == ThingCategory.Mote || t.def.category == ThingCategory.Projectile) return false;
            if (t is Blueprint || t is Frame) return false;
            
            return t.def.category == ThingCategory.Building || 
                   t.def.category == ThingCategory.Plant || 
                   t.def.category == ThingCategory.Filth ||
                   t.def.category == ThingCategory.Item;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect leftRect = inRect.LeftPart(0.65f).ContractedBy(5f);
            Rect rightRect = inRect.RightPart(0.35f).ContractedBy(5f);

            DrawPreview(leftRect);
            DrawSettings(rightRect);
        }

        private void DrawPreview(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            float cellSize = Mathf.Min(rect.width / sourceRect.Width, rect.height / sourceRect.Height);
            Vector2 startPos = new Vector2(rect.center.x - (sourceRect.Width * cellSize) / 2f, rect.center.y - (sourceRect.Height * cellSize) / 2f);

            if (options.includeTerrain) DrawTerrainPreview(startPos, cellSize);
            if (options.includeThings) DrawThingsPreview(startPos, cellSize);
            
            DrawGridLines(startPos, cellSize);
        }

        private void DrawTerrainPreview(Vector2 startPos, float cellSize)
        {
            foreach (IntVec3 c in sourceRect)
            {
                TerrainDef terr = c.GetTerrain(sourceMap);
                if (terr == null || options.excludedDefs.Contains(terr)) continue;

                GUI.color = terr.uiIconColor;
                if (options.includeTerrainColors)
                {
                    ColorDef cd = sourceMap.terrainGrid.ColorAt(c);
                    if (cd != null) GUI.color *= cd.color;
                }
                Widgets.DrawTextureFitted(GetCellRect(c, startPos, cellSize), terr.uiIcon, 1f);
            }
            GUI.color = Color.white;
        }

        private void DrawThingsPreview(Vector2 startPos, float cellSize)
        {
            foreach (Thing t in uniqueThings)
            {
                if (options.excludedDefs.Contains(t.def) || t.def.uiIcon == null) continue;

                float drawCenterX = startPos.x + (t.Position.x - sourceRect.minX + 0.5f) * cellSize;
                float drawCenterZ = startPos.y + (sourceRect.maxZ - t.Position.z + 0.5f) * cellSize;

                Vector2 drawSize = GetThingDrawSize(t);
                float drawW = drawSize.x * cellSize, drawH = drawSize.y * cellSize;
                Rect drawRect = new Rect(drawCenterX - drawW / 2f, drawCenterZ - drawH / 2f, drawW, drawH);

                Widgets.ThingIcon(drawRect, t, 1f, t.Rotation);
            }
            GUI.color = Color.white;
        }

        private Vector2 GetThingDrawSize(Thing t)
        {
            if (t.def.category == ThingCategory.Building) return t.def.size.ToVector2();
            return t.def.graphicData?.drawSize ?? Vector2.one;
        }

        private void DrawSettings(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            DrawMainSettings(listing);
            DrawElementList(rect, listing);
            DrawBottomButtons(rect);
            listing.End();
        }

        private void DrawMainSettings(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("Export Config");
            Text.Font = GameFont.Small;
            listing.GapLine();
            options.defName = listing.TextEntryLabeled("DefName:  ", options.defName);
            listing.CheckboxLabeled("Include Terrain", ref options.includeTerrain);
            listing.CheckboxLabeled("Include Colors", ref options.includeTerrainColors);
            listing.CheckboxLabeled("Include Things", ref options.includeThings);
			listing.CheckboxLabeled("Include Export Tools", ref options.includeExportTools);
			listing.GapLine();
        }

        private void DrawElementList(Rect rect, Listing_Standard listing)
        {
            listing.Label("Elements in Selection:");
            Rect scrollRect = new Rect(0, listing.CurHeight, rect.width, rect.height - listing.CurHeight - 50f);
            Rect viewRect = new Rect(0, 0, rect.width - 20f, uniqueDefs.Count * 30f);
            
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);
            float curY = 0;
            foreach (Def def in uniqueDefs)
            {
                DrawDefRow(def, viewRect.width, ref curY);
            }
            Widgets.EndScrollView();
        }

        private void DrawDefRow(Def def, float width, ref float curY)
        {
            bool isIncluded = !options.excludedDefs.Contains(def), wasIncluded = isIncluded;
            Rect rowRect = new Rect(0, curY, width, 28f);
            if (Mouse.IsOver(rowRect)) Widgets.DrawHighlight(rowRect);

            if (def is BuildableDef bdef && bdef.uiIcon != null)
            {
                GUI.color = bdef.uiIconColor;
                Widgets.DrawTextureFitted(new Rect(2, curY + 2, 24, 24), bdef.uiIcon, 1f);
                GUI.color = Color.white;
            }
            
            Widgets.CheckboxLabeled(new Rect(32, curY, width - 35, 28), def.label?.CapitalizeFirst() ?? def.defName, ref isIncluded);
            if (isIncluded != wasIncluded)
            {
                if (isIncluded) options.excludedDefs.Remove(def);
                else options.excludedDefs.Add(def);
            }
            curY += 30f;
        }

        private Rect GetCellRect(IntVec3 c, Vector2 startPos, float cellSize)
        {
            return new Rect(startPos.x + (c.x - sourceRect.minX) * cellSize, startPos.y + (sourceRect.maxZ - c.z) * cellSize, cellSize, cellSize);
        }

        private void DrawGridLines(Vector2 startPos, float cellSize)
        {
            GUI.color = new Color(1, 1, 1, 0.05f);
            for (int x = 0; x <= sourceRect.Width; x++)
                Widgets.DrawLineVertical(startPos.x + x * cellSize, startPos.y, sourceRect.Height * cellSize);
            for (int z = 0; z <= sourceRect.Height; z++)
                Widgets.DrawLineHorizontal(startPos.x, startPos.y + z * cellSize, sourceRect.Width * cellSize);
            GUI.color = Color.white;
        }

        private void DrawBottomButtons(Rect rect)
        {
            if (Widgets.ButtonText(new Rect(0, rect.height - 40f, rect.width, 40f), "Copy XML to Clipboard"))
            {
                GUIUtility.systemCopyBuffer = FFF_ExportUtility.ExportToXML(sourceRect, sourceMap, options);
                Messages.Message("FFF: Structure XML copied!", MessageTypeDefOf.PositiveEvent);
                Close();
            }
        }
    }
}
