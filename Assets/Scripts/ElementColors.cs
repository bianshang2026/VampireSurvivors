using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 属性颜色工具 — 根据Element返回对应的tint颜色。
    /// 所有技能投射物/法阵/特效使用此方法实现属性视觉变化，无需生成额外贴图。
    /// </summary>
    public static class ElementColors
    {
        /// <summary>
        /// 获取属性对应的主色调 (投射物/法阵着色)
        /// </summary>
        public static Color GetTint(Element element)
        {
            switch (element)
            {
                case Element.None:    return new Color(0.85f, 0.95f, 1f, 1f);   // 白蓝
                case Element.Fire:    return new Color(1f, 0.5f, 0.2f, 1f);      // 橙红
                case Element.Water:   return new Color(0.3f, 0.6f, 1f, 1f);      // 淡蓝
                case Element.Earth:   return new Color(0.6f, 0.45f, 0.25f, 1f);  // 棕色
                case Element.Wind:    return new Color(0.4f, 0.9f, 0.85f, 1f);   // 青色
                case Element.Thunder: return new Color(0.7f, 0.3f, 0.9f, 1f);    // 紫色
                case Element.Metal:   return new Color(0.8f, 0.8f, 0.85f, 1f);   // 银白
                // 融合属性
                case Element.FlameWind:   return new Color(1f, 0.35f, 0.15f, 1f);  // 炎=炽橙红
                case Element.Frost:       return new Color(0.5f, 0.85f, 1f, 1f);    // 冰=冰蓝
                case Element.Wood:        return new Color(0.4f, 0.8f, 0.3f, 1f);   // 草=翠绿
                case Element.MetalFusion: return new Color(1f, 0.8f, 0.3f, 1f);     // 金=亮金
                default:              return new Color(0.85f, 0.95f, 1f, 1f);
            }
        }

        /// <summary>
        /// 获取属性对应的发光颜色 (Light组件)
        /// </summary>
        public static Color GetGlow(Element element)
        {
            switch (element)
            {
                case Element.None:    return new Color(0.8f, 0.9f, 1f);
                case Element.Fire:    return new Color(1f, 0.4f, 0.1f);
                case Element.Water:   return new Color(0.2f, 0.5f, 1f);
                case Element.Earth:   return new Color(0.5f, 0.35f, 0.15f);
                case Element.Wind:    return new Color(0.3f, 0.85f, 0.8f);
                case Element.Thunder: return new Color(0.6f, 0.2f, 0.85f);
                case Element.Metal:   return new Color(0.7f, 0.7f, 0.75f);
                case Element.FlameWind:   return new Color(1f, 0.25f, 0.1f);
                case Element.Frost:       return new Color(0.35f, 0.75f, 1f);
                case Element.Wood:        return new Color(0.3f, 0.7f, 0.2f);
                case Element.MetalFusion: return new Color(1f, 0.7f, 0.2f);
                default:              return new Color(0.8f, 0.9f, 1f);
            }
        }

        /// <summary>
        /// 获取属性对应的拖尾颜色
        /// </summary>
        public static Color GetTrail(Element element)
        {
            var tint = GetTint(element);
            return new Color(tint.r, tint.g, tint.b, 0.4f);
        }

        /// <summary>
        /// 获取属性中文名
        /// </summary>
        public static string GetName(Element element)
        {
            switch (element)
            {
                case Element.None:    return "无";
                case Element.Fire:    return "火";
                case Element.Water:   return "水";
                case Element.Earth:   return "土";
                case Element.Wind:    return "风";
                case Element.Thunder: return "雷";
                case Element.Metal:   return "金";
                case Element.FlameWind:   return "炎";
                case Element.Frost:       return "冰";
                case Element.Wood:        return "草";
                case Element.MetalFusion: return "金·融";
                default:              return "???";
            }
        }
    }
}
