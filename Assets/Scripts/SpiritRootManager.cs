using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 灵根管理器 — 统一处理多灵根的属性判定。
    /// 优先级：火灵根 > 水灵根（同时存在时，火优先）。
    /// 技能脚本调用 GetEffectiveElement() 替代直接调 FireSpiritRoot.GetEffectiveElement。
    /// </summary>
    public static class SpiritRootManager
    {
        /// <summary>
        /// 获取技能的有效属性。
        /// 融合优先: 双灵根Lv3+时返回融合属性(炎/冰/草/金)。
        /// 否则遍历单灵根优先级链: 火 → 水 → 土 → 风 → 雷。
        /// </summary>
        public static Element GetEffectiveElement(Element baseElement)
        {
            if (baseElement != Element.None) return baseElement;

            // 融合属性优先于单灵根
            Element fusion = ElementFusionSystem.GetFusionElement();
            if (fusion != Element.None) return fusion;

            // 优先级链: 火 → 水 → 土 → 风 → 雷
            if (FireSpiritRoot.IsActive) return Element.Fire;
            if (WaterSpiritRoot.IsActive) return Element.Water;
            if (EarthSpiritRoot.IsActive) return Element.Earth;
            if (WindSpiritRoot.IsActive) return Element.Wind;
            if (ThunderSpiritRoot.IsActive) return Element.Thunder;

            return Element.None;
        }

        /// <summary>
        /// 获取属性对应的额外伤害倍率。
        /// </summary>
        public static float GetDamageMult(Element element)
        {
            // 融合属性
            if (element >= Element.FlameWind)
                return ElementFusionSystem.GetFusionDamageMult(element);

            float mult = 1f;
            if (element == Element.Fire) mult *= FireSpiritRoot.GetFireDamageMult(element);
            if (element == Element.Water) mult *= WaterSpiritRoot.GetWaterDamageMult(element);
            if (element == Element.Earth) mult *= EarthSpiritRoot.GetEarthDamageMult(element);
            if (element == Element.Wind) mult *= WindSpiritRoot.GetWindDamageMult(element);
            if (element == Element.Thunder) mult *= ThunderSpiritRoot.GetThunderDamageMult(element);
            return mult;
        }

        /// <summary>
        /// 获取属性对应的额外范围倍率。
        /// </summary>
        public static float GetRangeMult(Element element)
        {
            // 融合属性
            if (element >= Element.FlameWind)
                return ElementFusionSystem.GetFusionRangeMult(element);

            float mult = 1f;
            if (element == Element.Fire) mult *= FireSpiritRoot.GetFireRangeMult(element);
            if (element == Element.Water) mult *= WaterSpiritRoot.GetWaterRangeMult(element);
            if (element == Element.Earth) mult *= EarthSpiritRoot.GetEarthRangeMult(element);
            if (element == Element.Wind) mult *= WindSpiritRoot.GetWindRangeMult(element);
            if (element == Element.Thunder) mult *= ThunderSpiritRoot.GetThunderRangeMult(element);
            return mult;
        }
    }
}
