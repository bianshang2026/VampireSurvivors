using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 属性融合系统 — 双灵根均达Lv3时激活融合属性。
    /// 火+风=炎(扩散灼烧/范围扩大) | 水+风=冰(冻结控制) | 水+土=草(命中回血) | 火+土=金(破甲穿透)
    /// 雷灵根为天道属性不参与融合。
    /// 融合优先级高于单灵根: 双灵根Lv3+时技能属性直接变为融合属性。
    /// </summary>
    public static class ElementFusionSystem
    {
        // 融合触发所需灵根等级
        public const int FusionLevel = 3;

        /// <summary>
        /// 获取当前激活的融合属性 (未满足条件返回None)
        /// </summary>
        public static Element GetFusionElement()
        {
            if (FireAtLeast(FusionLevel) && WindAtLeast(FusionLevel)) return Element.FlameWind;   // 炎
            if (WaterAtLeast(FusionLevel) && WindAtLeast(FusionLevel)) return Element.Frost;       // 冰
            if (WaterAtLeast(FusionLevel) && EarthAtLeast(FusionLevel)) return Element.Wood;       // 草
            if (FireAtLeast(FusionLevel) && EarthAtLeast(FusionLevel)) return Element.MetalFusion; // 金
            return Element.None;
        }

        /// <summary>
        /// 是否处于融合状态 (任一融合组合激活)
        /// </summary>
        public static bool IsFusionActive => GetFusionElement() != Element.None;

        /// <summary>
        /// 融合属性伤害倍率
        /// </summary>
        public static float GetFusionDamageMult(Element element)
        {
            switch (element)
            {
                case Element.FlameWind: return 1.15f;   // 炎: 灼烧伤害加成
                case Element.Frost: return 1.0f;        // 冰: 控制系无伤害加成
                case Element.Wood: return 1.0f;         // 草: 回血系无伤害加成
                case Element.MetalFusion: return 1.2f;  // 金: 破甲附带伤害提升
                default: return 1f;
            }
        }

        /// <summary>
        /// 融合属性范围倍率
        /// </summary>
        public static float GetFusionRangeMult(Element element)
        {
            switch (element)
            {
                case Element.FlameWind: return 1.3f;    // 炎: 扩散灼烧范围扩大
                case Element.Frost: return 1.1f;        // 冰: 冻结范围略增
                case Element.Wood: return 1.0f;
                case Element.MetalFusion: return 1.0f;
                default: return 1f;
            }
        }

        /// <summary>
        /// 融合命中效果 — EnemyController.TakeDamage调用
        /// 炎=灼烧DoT | 冰=强减速 | 草=玩家回血 | 金=破甲(伤害计算处处理)
        /// </summary>
        public static void OnHitFusionEffect(EnemyController enemy, Element element)
        {
            if (enemy == null) return;
            switch (element)
            {
                case Element.FlameWind:
                    // 炎: 灼烧DoT (固定参数, 不依赖火灵根)
                    enemy.ApplyBurnFromFusion(0.08f, 3f);
                    break;
                case Element.Frost:
                    // 冰: 强冻结减速 (移速35%, 2.5秒)
                    enemy.ApplySlow(0.35f, 2.5f);
                    break;
                case Element.Wood:
                    // 草: 命中回血 (1 + 攻击力×2%)
                    var psc = PlayerStatsComponent.Instance;
                    if (psc != null)
                    {
                        int heal = Mathf.Max(1, Mathf.CeilToInt(psc.stats.GetBaseAttackDamage() * 0.02f));
                        psc.stats.Heal(heal);
                    }
                    break;
                case Element.MetalFusion:
                    // 金: 破甲在伤害计算中处理 (无视50%防御)
                    break;
            }
        }

        /// <summary>
        /// 金融合破甲 — 敌人防御减免系数 (0.5=无视50%防御)
        /// </summary>
        public static float GetDefensePenaltyFactor(Element element)
        {
            return element == Element.MetalFusion ? 0.5f : 1f;
        }

        public static string GetFusionName(Element element)
        {
            switch (element)
            {
                case Element.FlameWind: return "炎";
                case Element.Frost: return "冰";
                case Element.Wood: return "草";
                case Element.MetalFusion: return "金";
                default: return "";
            }
        }

        // ── 灵根等级查询 (反射太重, 直接引用具体类) ──
        private static bool FireAtLeast(int lv) => FireSpiritRoot.IsActive && FireSpiritRoot.Instance.level >= lv;
        private static bool WaterAtLeast(int lv) => WaterSpiritRoot.IsActive && WaterSpiritRoot.Instance.level >= lv;
        private static bool EarthAtLeast(int lv) => EarthSpiritRoot.IsActive && EarthSpiritRoot.Instance.level >= lv;
        private static bool WindAtLeast(int lv) => WindSpiritRoot.IsActive && WindSpiritRoot.Instance.level >= lv;
    }
}
