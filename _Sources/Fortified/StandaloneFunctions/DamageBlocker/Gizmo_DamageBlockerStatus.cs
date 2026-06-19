using UnityEngine;
using Verse;

namespace Fortified
{
    [StaticConstructorOnStartup]
    public class Gizmo_DamageBlockerStatus : Gizmo
    {
        private static readonly Texture2D FullTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.4f, 0.8f, 1.0f));
        private static readonly Texture2D EmptyTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.15f, 0.15f, 0.2f));
        private static readonly Color FullColor = new Color(0.4f, 0.8f, 1.0f);
        private static readonly Color EmptyColor = new Color(0.15f, 0.15f, 0.2f);

        private const int CellThreshold = 20;

        public IDamageBlockerDisplay blocker;

        public Gizmo_DamageBlockerStatus() { Order = -119f; }

        public override float GetWidth(float maxWidth) => 140f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            int cur = blocker.CurrentCharges;
            int max = blocker.MaxCharges;

            Rect outer = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect inner = outer.ContractedBy(6f);
            Widgets.DrawWindowBackground(outer);

            // 上行：阈值
            Rect topRow = new Rect(inner.x, inner.y, inner.width, inner.height * 0.35f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(topRow, blocker.ThresholdLabelKey.Translate(blocker.ThresholdOperator, blocker.Threshold.ToString("F0")));

            if (!blocker.IsArmorMode && max > 0)
            {
                // 下行：条/格子 + 数字叠加
                Rect barArea = new Rect(inner.x, inner.y + inner.height * 0.35f, inner.width, inner.height * 0.65f);

                if (max <= CellThreshold)
                {
                    float cellW = barArea.width / max;
                    for (int i = 0; i < max; i++)
                    {
                        Rect cell = new Rect(barArea.x + i * cellW + 1f, barArea.y + 1f, cellW - 2f, barArea.height - 2f);
                        Widgets.DrawBoxSolid(cell, i < cur ? FullColor : EmptyColor);
                    }
                }
                else
                {
                    Widgets.FillableBar(barArea, (float)cur / max, FullTex, EmptyTex, false);
                }

                // 数字叠在条中心
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(barArea, cur + " / " + max);
                Text.Anchor = TextAnchor.UpperLeft;

                TooltipHandler.TipRegion(inner, blocker.ChargesLabelKey.Translate(cur, max));
            }

            return new GizmoResult(GizmoState.Clear);
        }
    }
}
