using UnityEngine;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// 道系统 — 技能Lv10满级后触发悟道，替换为新功法。
    /// 每属性3条道×5属性=15条/术法。
    /// 道需局外悟道树解锁。
    /// 新功法再满级→神通(全屏大招)。
    /// </summary>
    public class DaoSystem : MonoBehaviour
    {
        public static DaoSystem Instance { get; private set; }

        [Header("已解锁的道 (局外悟道树设置)")]
        public List<string> unlockedDaos = new List<string>();

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 检查某个道是否已解锁
        /// </summary>
        public bool IsDaoUnlocked(string daoId)
        {
            return unlockedDaos.Contains(daoId) || MetaProgression.Data.daos.Contains(daoId) || daoId.StartsWith("Default");
        }

        /// <summary>
        /// 解锁某个道 (局外悟道树调用)
        /// </summary>
        public void UnlockDao(string daoId)
        {
            if (!unlockedDaos.Contains(daoId))
                unlockedDaos.Add(daoId);
        }

        /// <summary>
        /// 获取某属性技能的3条道选项
        /// </summary>
        public static string[] GetDaoOptions(string skillId, Element element)
        {
            var result = new List<string>();
            foreach (var id in GetAllDaoOptions(skillId, element))
                if (id.StartsWith("Default") || MetaProgression.Data.daos.Contains(id) || (Instance != null && Instance.unlockedDaos.Contains(id))) result.Add(id);
            return result.ToArray();
        }

        public static string[] GetAllDaoOptions(string skillId, Element element)
        {
            // 灵球术
            if (skillId == "SpiritOrb")
            {
                switch (element)
                {
                    case Element.None: return new[] { "DefaultDaoA", "DefaultDaoB", "DefaultDaoC" };
                    case Element.Fire: return new[] { "FireDragon", "SunBlaze", "BurnHell" };
                    case Element.Water: return new[] { "OceanDragon", "Abyss", "FrostIce" };
                    case Element.Earth: return new[] { "MountainDragon", "Bedrock", "EarthSplit" };
                    case Element.Wind: return new[] { "StormDragon", "Gale", "GaleForce" };
                    case Element.Thunder: return new[] { "ThunderDragon", "TenThunder", "PurpleLightning" };
                    // 融合属性 (预留, Element值待定)
                    default: return new[] { "FlameDragon", "BurnSky", "FlameDomain" };
                }
            }
            // 御剑术
            if (skillId == "FlyingSword")
            {
                switch (element)
                {
                    case Element.Fire: return new[] { "CrimsonSword", "FlameDragonSword", "BurnSkySword" };
                    case Element.Water: return new[] { "FrostSword", "OceanSword", "AbyssSword" };
                    case Element.Thunder: return new[] { "PurpleThunder", "TenThunder", "SkyThunder" };
                    default: return new[] { "DefaultDaoA", "DefaultDaoB", "DefaultDaoC" };
                }
            }
            // 灵气刃
            if (skillId == "QiBlade")
            {
                switch (element)
                {
                    case Element.Fire: return new[] { "FlameBlade", "InfernoBlade", "BurnBlade" };
                    case Element.Wind: return new[] { "GaleBlade", "StormBlade", "SwiftBlade" };
                    case Element.Thunder: return new[] { "ThunderBlade", "PurpleBlade", "TenThunderBlade" };
                    default: return new[] { "DefaultDaoA", "DefaultDaoB", "DefaultDaoC" };
                }
            }
            // 符箓术
            if (skillId == "Talisman")
            {
                switch (element)
                {
                    case Element.Fire: return new[] { "FlameTalisman", "BurnHellTalisman", "FlameRain" };
                    case Element.Water: return new[] { "OceanTalisman", "IceSeal", "ColdTide" };
                    case Element.Thunder: return new[] { "SkyThunderTalisman", "ThunderPrison", "ThunderRain" };
                    default: return new[] { "DefaultDaoA", "DefaultDaoB", "DefaultDaoC" };
                }
            }
            // 灵阵
            if (skillId == "SpiritArray")
            {
                switch (element)
                {
                    case Element.Fire: return new[] { "BurnArray", "InfernoArray", "BurnSkyArray" };
                    case Element.Water: return new[] { "FrostArray", "OceanArray", "IceSealArray" };
                    case Element.Thunder: return new[] { "ThunderArray", "StormArray", "TenThunderArray" };
                    default: return new[] { "DefaultDaoA", "DefaultDaoB", "DefaultDaoC" };
                }
            }
            return new[] { "DefaultDaoA", "DefaultDaoB", "DefaultDaoC" };
        }

        /// <summary>
        /// 获取道的显示名 (中文)
        /// </summary>
        public static string GetDaoName(string daoId)
        {
            var names = new Dictionary<string, string>
            {
                // 灵球术-火
                {"FireDragon", "炎龙道"}, {"SunBlaze", "赤阳道"}, {"BurnHell", "焚狱道"},
                // 灵球术-水
                {"OceanDragon", "沧海道"}, {"Abyss", "深渊道"}, {"FrostIce", "寒冰道"},
                // 灵球术-土
                {"MountainDragon", "山岳道"}, {"Bedrock", "磐石道"}, {"EarthSplit", "裂地道"},
                // 灵球术-风
                {"StormDragon", "风暴道"}, {"Gale", "疾风道"}, {"GaleForce", "罡风道"},
                // 灵球术-雷
                {"ThunderDragon", "雷霆道"}, {"TenThunder", "万雷道"}, {"PurpleLightning", "紫电道"},
                // 御剑术
                {"CrimsonSword", "赤炎剑道"}, {"FlameDragonSword", "炎龙剑道"}, {"BurnSkySword", "焚天剑道"},
                {"FrostSword", "寒霜剑道"}, {"OceanSword", "沧海剑道"}, {"AbyssSword", "深渊剑道"},
                {"PurpleThunder", "紫雷剑道"}, {"SkyThunder", "天雷剑道"},
                // 灵气刃
                {"FlameBlade", "炎刃道"}, {"InfernoBlade", "烈焰道"}, {"BurnBlade", "焚刃道"},
                {"GaleBlade", "罡风刃道"}, {"StormBlade", "风暴刃道"}, {"SwiftBlade", "疾风刃道"},
                {"ThunderBlade", "雷霆刃道"}, {"PurpleBlade", "紫电刃道"}, {"TenThunderBlade", "万雷刃道"},
                // 符箓术
                {"FlameTalisman", "炎符道"}, {"BurnHellTalisman", "炎狱符道"}, {"FlameRain", "炎雨符道"},
                {"OceanTalisman", "沧海符道"}, {"IceSeal", "冰封符道"}, {"ColdTide", "寒潮符道"},
                {"SkyThunderTalisman", "天雷符道"}, {"ThunderPrison", "雷狱符道"}, {"ThunderRain", "雷雨符道"},
                // 灵阵
                {"BurnArray", "炎狱阵道"}, {"InfernoArray", "烈焰阵道"}, {"BurnSkyArray", "焚天阵道"},
                {"FrostArray", "寒泉阵道"}, {"OceanArray", "沧海阵道"}, {"IceSealArray", "冰封阵道"},
                {"ThunderArray", "雷罚阵道"}, {"StormArray", "雷暴阵道"}, {"TenThunderArray", "万雷阵道"},
                // 融合属性 (预留)
                {"FlameDragon", "炎龙道"}, {"BurnSky", "焚天道"}, {"FlameDomain", "烈焰道"},
                // 默认
                {"DefaultDaoA", "强化道A"}, {"DefaultDaoB", "强化道B"}, {"DefaultDaoC", "强化道C"}
            };
            return names.TryGetValue(daoId, out var name) ? name : daoId;
        }

        /// <summary>
        /// 获取道的描述
        /// </summary>
        public static string GetDaoDescription(string daoId)
        {
            var descs = new Dictionary<string, string>
            {
                {"FireDragon", "火球→火龙, 直线穿透全屏, 路径灼烧"},
                {"SunBlaze", "火球变大×2, 单体爆发, 伤害×2.5"},
                {"BurnHell", "火球命中后留下大范围灼烧区域5s"},
                {"OceanDragon", "水弹→水龙, 路径减速+伤害"},
                {"Abyss", "水弹追踪+范围扩大, 全屏减速"},
                {"FrostIce", "水弹命中冻结2s, 冻结传播"},
                {"MountainDragon", "石弹→岩龙, 路径击退+晕眩"},
                {"Bedrock", "石弹巨大化, 范围震击+护盾"},
                {"EarthSplit", "石弹命中后地面突刺, 范围+50%"},
                {"StormDragon", "风球→风龙, 路径留风刃"},
                {"Gale", "风球速度×2, 弹射无限"},
                {"GaleForce", "风球变大, 环绕+穿透"},
                {"ThunderDragon", "雷球→雷龙, 路径降下闪电"},
                {"TenThunder", "雷球命中范围雷击+连锁3"},
                {"PurpleLightning", "雷球暴击率+30%, 暴击即秒杀低血"},
                {"DefaultDaoA", "强化路径A"}, {"DefaultDaoB", "强化路径B"}, {"DefaultDaoC", "强化路径C"}
            };
            return descs.TryGetValue(daoId, out var desc) ? desc : "强化技能";
        }

        /// <summary>
        /// 应用道效果 (技能满级悟道后调用)
        /// 修改技能的基础参数, 模拟技能替换
        /// </summary>
        public static void ApplyDaoEffect(MonoBehaviour skill, string daoId)
        {
            Debug.Log($"[DaoSystem] Applied Dao '{GetDaoName(daoId)}' to {skill.GetType().Name}");
        }

        /// <summary>
        /// 检查是否为神通阶段 (第二次满级)
        /// </summary>
        public static bool IsDivineStage(int evolutionCount)
        {
            return evolutionCount >= 2;
        }

        /// <summary>
        /// 神通充能 (第二次满级触发) — 不再立即发动, 存入DivineSystem由玩家按R手动释放。
        /// 御剑系→万剑天降 / 火属性→陨星雨 / 其他→全屏神通伤害。
        /// </summary>
        public static void TriggerDivine(MonoBehaviour sourceSkill, string daoId, Transform player)
        {
            DivineSystem.ChargeDivine(sourceSkill, daoId);
        }

        /// <summary>
        /// 立即发动的通用全屏神通 (DivineSystem不可用时的回退, 也是"其他属性"神通的实现)。
        /// </summary>
        public static void TriggerDivineImmediate(string daoId, Transform player)
        {
            string divineName = "Divine_" + daoId;
            Debug.Log($"[DaoSystem] 神通发动: {GetDaoName(divineName)}");

            // 全屏范围伤害 (复用现有伤害计算)
            var psc = PlayerStatsComponent.Instance;
            float baseDmg = psc != null ? psc.stats.GetBaseAttackDamage() : 6f;
            int divineDmg = Mathf.Max(1, Mathf.CeilToInt(baseDmg * 3.5f));

            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                enemy.TakeDamage(divineDmg, Element.None);
                DamageNumber.CreateAt(enemy.transform.position, divineDmg, Element.None);
            }
        }
    }
}
