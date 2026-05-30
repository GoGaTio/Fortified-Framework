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
        private static readonly Type ceProjectileType;

        // 反射缓存
        private static PropertyInfo cachedTrajectoryWorkerProp;
        private static FieldInfo cachedSpeedField;
        private static MethodInfo cachedShotAngleMethod;
        private static MethodInfo cachedShotRotationMethod;
        private static MethodInfo cachedLaunchMethod;
        private static bool reflectionCacheInitialized;

        static Harmony_AirSupportCE()
        {
            ceProjectileType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
            if (ceProjectileType == null)
            {
                Log.Error("[FortifiedCE] 无法找到 ProjectileCE 类型，CE兼容管线未注册");
                return;
            }
            AirSupportData_LaunchProjectile.ceProjectileLauncher = LaunchCEProjectile;
            Log.Message("[FortifiedCE] 已注册空中支援 CE 兼容管线");
        }

        // 初始化反射缓存
        private static bool InitReflectionCache(Type propsType, Type projectileType)
        {
            if (reflectionCacheInitialized) return true;

            cachedTrajectoryWorkerProp = propsType.GetProperty("TrajectoryWorker");
            if (cachedTrajectoryWorkerProp == null)
            {
                Log.Error("[FortifiedCE] 无法缓存TrajectoryWorker属性");
                return false;
            }

            cachedSpeedField = propsType.GetField("speed");

            cachedLaunchMethod = projectileType.GetMethod("Launch",
                new[] { typeof(Thing), typeof(Vector2), typeof(float), typeof(float),
                        typeof(float), typeof(float), typeof(Thing), typeof(float) });

            reflectionCacheInitialized = true;
            return true;
        }

        // 缓存TrajectoryWorker方法（按worker类型缓存）
        private static Type cachedTWType;
        private static void CacheTrajectoryWorkerMethods(object trajectoryWorker, Type propsType)
        {
            var twType = trajectoryWorker.GetType();
            if (twType == cachedTWType) return;
            cachedTWType = twType;
            cachedShotAngleMethod = twType.GetMethod("ShotAngle",
                new[] { propsType, typeof(Vector3), typeof(Vector3), typeof(float?) });
            cachedShotRotationMethod = twType.GetMethod("ShotRotation",
                new[] { propsType, typeof(Vector3), typeof(Vector3) });
        }

        // CE弹药发射处理
        private static bool LaunchCEProjectile(Thing projectile, Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo target, float configuredSpeed)
        {
            if (projectile == null || launcher == null) return false;

            var projectileType = projectile.GetType();

            // 非CE投射物直接跳过
            if (!ceProjectileType.IsAssignableFrom(projectileType)) return false;

            var projectilePropsCE = projectile.def.projectile;
            if (projectilePropsCE == null)
            {
                Log.Error($"[FortifiedCE] 投射物 {projectile.def.defName} 没有projectile属性");
                return false;
            }

            var propsType = projectilePropsCE.GetType();

            // 初始化反射缓存
            if (!InitReflectionCache(propsType, projectileType)) return false;

            var trajectoryWorker = cachedTrajectoryWorkerProp.GetValue(projectilePropsCE);
            if (trajectoryWorker == null)
            {
                Log.Error("[FortifiedCE] TrajectoryWorker为null");
                return false;
            }

            CacheTrajectoryWorkerMethods(trajectoryWorker, propsType);

            // 优先使用配置速度，投射物自身速度为回退
            float shotSpeed;
            if (configuredSpeed > 0f)
            {
                shotSpeed = configuredSpeed;
            }
            else
            {
                shotSpeed = cachedSpeedField != null
                    ? (float)cachedSpeedField.GetValue(projectilePropsCE) : 100f;
            }

            // 计算目标位置
            Vector3 targetPos = target.Cell.ToVector3Shifted();
            targetPos.y = 0f;

            // 计算射击角度
            float shotAngle;
            if (cachedShotAngleMethod != null)
            {
                shotAngle = (float)cachedShotAngleMethod.Invoke(
                    trajectoryWorker, new object[] { projectilePropsCE, origin, targetPos, shotSpeed });
            }
            else
            {
                shotAngle = 45f * Mathf.Deg2Rad;
            }

            // 计算旋转
            float shotRotation;
            if (cachedShotRotationMethod != null)
            {
                shotRotation = (float)cachedShotRotationMethod.Invoke(
                    trajectoryWorker, new object[] { projectilePropsCE, origin, targetPos });
            }
            else
            {
                Vector3 w = targetPos - origin;
                shotRotation = (-90f + Mathf.Rad2Deg * Mathf.Atan2(w.z, w.x)) % 360f;
            }

            // 发射
            if (cachedLaunchMethod != null)
            {
                try
                {
                    Vector2 origin2D = new Vector2(origin.x, origin.z);
                    float distance = (new Vector2(targetPos.x, targetPos.z) - origin2D).magnitude;
                    float shotHeight = origin.y;

                    cachedLaunchMethod.Invoke(projectile, new object[] {
                        launcher, origin2D, shotAngle, shotRotation,
                        shotHeight, shotSpeed, null, distance });
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error($"[FortifiedCE] 发射CE弹药失败: {ex}");
                    projectile.Destroy();
                    return true;
                }
            }

            Log.Warning("[FortifiedCE] 未找到CE Launch方法");
            return false;
        }
    }
}
