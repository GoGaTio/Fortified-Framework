using HarmonyLib;
using Verse;
using UnityEngine;
using System.Collections.Generic;

namespace Fortified
{
    // 涂装补丁
    [HarmonyPatch]
    public static class Harmony_Painting
    {
        // 属性ID缓存
        public static readonly int OverlayTexID = Shader.PropertyToID("_OverlayTex");
        public static readonly int UseOverlayID = Shader.PropertyToID("_UseOverlay");
        public static readonly int OverlayMultiplyID = Shader.PropertyToID("_OverlayMultiply");
        public static readonly int CamoRotationID = Shader.PropertyToID("_CamoRotation");

        // 动物部件补丁
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PawnRenderNode_AnimalPart), nameof(PawnRenderNode_AnimalPart.GraphicFor))]
        static void AnimalPartPostfix(Pawn pawn, ref Graphic __result)
        {
            TryApplyPaint(pawn, ref __result);
        }

        // 实体图形补丁
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Thing), "get_Graphic")]
        static void ThingGraphicPostfix(Thing __instance, ref Graphic __result)
        {
            try
            {
                if (__result != null && __instance is Building building && building.def.HasComp(typeof(CompPaintable)))
                {
                    var comp = building.GetComp<CompPaintable>();
                    if (comp != null)
                    {
                        __result = comp.GetPaintedGraphic(__result);
                    }
                }
            }
            catch (System.Exception e) { Log.ErrorOnce($"[Fortified] ThingGraphic涂装补丁异常: {e}", __instance.thingIDNumber ^ 0xBEEF); }
        }



        // 应用涂装
        static void TryApplyPaint(Pawn pawn, ref Graphic result)
        {
            try
            {
                if (result == null || pawn.TryGetComp<CompPaintable>() is not CompPaintable comp) return;

                GetActiveParams(comp, out Color c1, out Color c2, out Color c3,
                    out float br, out FFF_CamoDef camo, out FFF_OverlayDef overlay);
                if (c1 == Color.white && c2 == Color.white && c3 == Color.white
                    && br <= 0f && camo == null && overlay == null) return;

                Shader shader = FFF_AssetLoader.PaintShader ?? result.Shader;
                if (FFF_AssetLoader.PaintShader != null) c2.a = br;

                // 缓存参数列表避免频繁GC
                var shaderParams = comp.GetOrBuildShaderParams(c1, c3, camo);
                // 使用白色作为主通道
                result = GraphicDatabase.Get(result.GetType(), result.path, shader,
                    result.drawSize, Color.white, c2, result.data, shaderParams);

                // 应用朝向属性
                ApplyPerDirectionProps(result, camo, overlay);
            }
            catch (System.Exception e) { Log.ErrorOnce($"[Fortified] 涂装补丁异常: {e}", pawn.thingIDNumber ^ 0xABCD); }
        }

        private static readonly System.Reflection.FieldInfo subGraphicsField = AccessTools.Field(typeof(Graphic_Collection), "subGraphics");

        public static void ApplyPerDirectionProps(Graphic graphic, FFF_CamoDef camo, FFF_OverlayDef overlay)
        {
            if (graphic == null) return;

            // 递归处理子集合
            if (graphic is Graphic_Collection collection)
            {
                var subGraphics = (Graphic[])subGraphicsField.GetValue(collection);
                if (subGraphics != null)
                {
                    foreach (var sub in subGraphics)
                    {
                        ApplyPerDirectionProps(sub, camo, overlay);
                    }
                }
                return;
            }
            else if (graphic is Graphic_RandomRotated rotated)
            {
                ApplyPerDirectionProps(rotated.SubGraphic, camo, overlay);
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                var rot = new Rot4(i);
                var mat = graphic.MatAt(rot);
                if (mat == null) continue;

                // 设置叠加层
                if (overlay != null)
                {
                    var tex = overlay.GetTexture(rot);
                    if (tex != null)
                    {
                        mat.SetFloat(UseOverlayID, 1f);
                        mat.SetTexture(OverlayTexID, tex);
                        mat.SetFloat(OverlayMultiplyID, overlay.multiplyBase ? 1f : 0f);
                    }
                    else
                    {
                        mat.SetFloat(UseOverlayID, 0f);
                    }
                }
                else
                {
                    mat.SetFloat(UseOverlayID, 0f);
                }

                // 设置迷彩旋转
                if (camo?.Texture != null)
                {
                    float angle = 0f;
                    if (rot == Rot4.East) angle = -Mathf.PI / 2f;
                    else if (rot == Rot4.North) angle = Mathf.PI;
                    else if (rot == Rot4.West) angle = Mathf.PI / 2f;
                    mat.SetFloat(CamoRotationID, angle);
                }
            }
        }

        static void GetActiveParams(CompPaintable comp, out Color c1, out Color c2, out Color c3,
            out float br, out FFF_CamoDef camo, out FFF_OverlayDef overlay)
        {
            c1 = comp.color1; c2 = comp.color2; c3 = comp.color3;
            camo = comp.camoDef; br = comp.brightness; overlay = comp.overlayDef;
        }
    }
}
