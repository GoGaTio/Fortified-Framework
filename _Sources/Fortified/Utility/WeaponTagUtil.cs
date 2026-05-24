using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld;

namespace Fortified
{
    /// <summary>
    /// 武器標籤工具類，用於管理和查詢武器定義及其關聯資料
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WeaponTagUtil
    {
        private static readonly Dictionary<string, HashSet<ThingDef>> AllTags = new Dictionary<string, HashSet<ThingDef>>();
        private static ThingDef[] _turrets = new ThingDef[0];
        private static ThingDef[] _weaponUseableMechs = new ThingDef[0];
        private static ThingDef[] _allWeaponDefs = new ThingDef[0];
        private static readonly Dictionary<string, ThingDef> _caches = new Dictionary<string, ThingDef>();

        public static ThingDef[] GetTurrets => _turrets;

        static WeaponTagUtil()
        {
            InitializeAllWeapons();
            InitializeTurrets();
            InitializeWeaponUseableMechs();
        }

        /// <summary>
        /// 初始化所有武器及其標籤映射
        /// </summary>
        private static void InitializeAllWeapons()
        {
            _allWeaponDefs = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.IsWeapon)
                .ToArray();

            foreach (ThingDef weaponDef in _allWeaponDefs)
            {
                RegisterWeaponTags(weaponDef);
            }
        }

        /// <summary>
        /// 註冊單個武器的所有標籤
        /// </summary>
        private static void RegisterWeaponTags(ThingDef weaponDef)
        {
            if (weaponDef.weaponTags.NullOrEmpty())
            {
                return;
            }

            foreach (string tag in weaponDef.weaponTags.Distinct())
            {
                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }

                if (!AllTags.TryGetValue(tag, out var set))
                {
                    set = new HashSet<ThingDef>();
                    AllTags[tag] = set;
                }

                set.Add(weaponDef);
            }
        }

        /// <summary>
        /// 初始化可操作的砲塔
        /// </summary>
        private static void InitializeTurrets()
        {
            var turretList = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.building?.turretGunDef != null && HasMannableComponent(def))
                .ToList();

            turretList.SortBy(def => def.BaseMass);
            _turrets = turretList.ToArray();
        }

        /// <summary>
        /// 初始化機械體可用的重型裝備
        /// </summary>
        private static void InitializeWeaponUseableMechs()
        {
            var mechList = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.GetModExtension<MechWeaponExtension>() != null)
                .ToList();

            mechList.SortBy(def => def.BaseMass);
            _weaponUseableMechs = mechList.ToArray();
        }

        /// <summary>
        /// 檢查定義是否具有可操作元件
        /// </summary>
        private static bool HasMannableComponent(ThingDef def)
        {
            return def.GetCompProperties<CompProperties_Mannable>() != null;
        }

        /// <summary>
        /// 根據標籤列表獲取所有匹配的武器
        /// </summary>
        public static IEnumerable<ThingDef> GetWeapons(List<string> tags)
        {
            var weapons = new HashSet<ThingDef>();

            if (tags == null)
            {
                return weapons;
            }

            foreach (string tag in tags)
            {
                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }

                if (AllTags.TryGetValue(tag, out var set))
                {
                    weapons.UnionWith(set);
                }
            }

            return weapons;
        }

        /// <summary>
        /// 檢查武器是否存在
        /// </summary>
        public static bool WeaponExists(string defName, out ThingDef weaponDef)
        {
            if (string.IsNullOrEmpty(defName))
            {
                weaponDef = null;
                return false;
            }

            if (_caches.TryGetValue(defName, out weaponDef))
            {
                return true;
            }

            weaponDef = _allWeaponDefs.FirstOrDefault(def => def.defName == defName);

            if (weaponDef != null)
            {
                _caches[defName] = weaponDef;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 檢查砲塔定義是否存在
        /// </summary>
        public static bool WeaponExistsInTurretDict(string defName, out ThingDef weaponDef)
        {
            weaponDef = _turrets.FirstOrDefault(t => t.defName == defName);
            return weaponDef != null;
        }

        /// <summary>
        /// 獲取能使用指定武器的機械體列表
        /// </summary>
        public static ThingDef[] UseableByListsOfMechs(ThingWithComps weapon)
        {
            var compatibleMechs = new List<ThingDef>();

            foreach (ThingDef mechDef in _weaponUseableMechs)
            {
                if (CanMechUseWeapon(mechDef, weapon))
                {
                    compatibleMechs.AddDistinct(mechDef);
                }
            }

            return compatibleMechs.ToArray();
        }

        /// <summary>
        /// 檢查機械體是否可以使用指定武器
        /// </summary>
        public static bool CanMechUseWeapon(ThingDef mechDef, ThingWithComps weapon)
        {
            var mechExtension = mechDef.GetModExtension<MechWeaponExtension>();

            if (mechExtension == null || weapon == null)
            {
                return false;
            }

            if (mechExtension.CanUse(weapon) || mechExtension.CanUseAsHeavyWeapon(weapon, mechDef.race.baseBodySize))
            {
                return true;
            }

            if (!mechExtension.EnableWeaponFilter)
            {
                return CanEquipAsHeavyWeapon(weapon, mechDef);
            }

            return false;
        }

        /// <summary>
        /// 檢查武器是否可作為重型裝備安裝
        /// </summary>
        private static bool CanEquipAsHeavyWeapon(ThingWithComps weapon, ThingDef mechDef)
        {
            var heavyEquipExtension = weapon.def.GetModExtension<HeavyEquippableExtension>();

            return heavyEquipExtension != null &&
                   heavyEquipExtension.EquippableDef != null &&
                   heavyEquipExtension.CanEquippedBy(mechDef);
        }
    }
}