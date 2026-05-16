using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using RimWorld;
using UnityEngine;

namespace Fortified
{
    [StaticConstructorOnStartup]
    public static class Harmony_AirSupportCE
    {
        static Harmony_AirSupportCE()
        {
            // 注册 CE 弹药发射管线
            AirSupportData_LaunchProjectile.ceProjectileLauncher = LaunchCEProjectile;
            Log.Message("[FortifiedCE] 已注册空中支援 CE 兼容管线");
        }

        // CE 弹药发射处理
        private static bool LaunchCEProjectile(Thing projectile, Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo target)
        {
            if (projectile == null || launcher == null) return false;

            var projectileType = projectile.GetType();

            // 获取ProjectilePropertiesCE
            var projectilePropsCE = projectile.def.projectile;
            if (projectilePropsCE == null)
            {
                Log.Error($"[FortifiedCE] 投射物 {projectile.def.defName} 没有projectile属性");
                return false;
            }

            // 获取TrajectoryWorker来计算正确的射击角度
            var trajectoryWorkerField = projectilePropsCE.GetType().GetProperty("TrajectoryWorker");
            if (trajectoryWorkerField == null)
            {
                Log.Error($"[FortifiedCE] 无法获取TrajectoryWorker");
                return false;
            }
            var trajectoryWorker = trajectoryWorkerField.GetValue(projectilePropsCE);

            // 获取速度
            var speedField = projectilePropsCE.GetType().GetProperty("speed");
            float shotSpeed = speedField != null ? (float)speedField.GetValue(projectilePropsCE) : 100f;

            // 计算目标位置（地面高度）
            Vector3 targetPos = target.Cell.ToVector3Shifted();
            targetPos.y = 0f; // 目标在地面

            // 使用TrajectoryWorker的ShotAngle方法计算正确的射击角度
            var shotAngleMethod = trajectoryWorker.GetType().GetMethod("ShotAngle",
                new[] { projectilePropsCE.GetType(), typeof(Vector3), typeof(Vector3), typeof(float?) });

            float shotAngle;
            if (shotAngleMethod != null)
            {
                shotAngle = (float)shotAngleMethod.Invoke(trajectoryWorker, new object[] { projectilePropsCE, origin, targetPos, shotSpeed });
            }
            else
            {
                Log.Warning($"[FortifiedCE] 无法找到ShotAngle方法，使用默认角度");
                shotAngle = 45f * Mathf.Deg2Rad;
            }

            // 使用TrajectoryWorker的ShotRotation方法计算旋转
            var shotRotationMethod = trajectoryWorker.GetType().GetMethod("ShotRotation",
                new[] { projectilePropsCE.GetType(), typeof(Vector3), typeof(Vector3) });

            float shotRotation;
            if (shotRotationMethod != null)
            {
                shotRotation = (float)shotRotationMethod.Invoke(trajectoryWorker, new object[] { projectilePropsCE, origin, targetPos });
            }
            else
            {
                Vector3 w = targetPos - origin;
                shotRotation = (-90f + Mathf.Rad2Deg * Mathf.Atan2(w.z, w.x)) % 360f;
            }

            // CE 弹药使用不同的 Launch 签名
            var launchMethod = projectileType.GetMethod("Launch",
                new[] { typeof(Thing), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(float), typeof(Thing), typeof(float) });

            if (launchMethod != null)
            {
                try
                {
                    Vector2 origin2D = new Vector2(origin.x, origin.z);
                    Vector2 target2D = new Vector2(targetPos.x, targetPos.z);
                    float distance = (target2D - origin2D).magnitude;
                    float shotHeight = origin.y;

                    Log.Message($"[FortifiedCE] 发射参数详情:");
                    Log.Message($"  origin3D={origin}, target3D={targetPos}");
                    Log.Message($"  origin2D={origin2D}, target2D={target2D}");
                    Log.Message($"  distance={distance:F1}, rotation={shotRotation:F1}");
                    Log.Message($"  shotHeight={shotHeight:F1}, shotAngle={shotAngle:F3}rad ({Mathf.Rad2Deg * shotAngle:F1}deg)");
                    Log.Message($"  shotSpeed={shotSpeed:F1}");

                    launchMethod.Invoke(projectile, new object[] { launcher, origin2D, shotAngle, shotRotation, shotHeight, shotSpeed, null, distance });
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error($"[FortifiedCE] 发射 CE 弹药失败: {ex}");
                    projectile.Destroy();
                    return true;
                }
            }

            Log.Warning($"[FortifiedCE] 未找到 CE Launch 方法");
            return false;
        }
    }
}
