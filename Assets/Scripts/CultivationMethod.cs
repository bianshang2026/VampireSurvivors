using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// 功法定义 — 局外编辑的完整配置: 基础术法 + 灵根 + 5词缀 + 道方向。
    /// 局内进入秘境时由 MethodRuntime 应用到 Player。
    /// </summary>
    [CreateAssetMenu(fileName = "NewMethod", menuName = "BiopunkRV/CultivationMethod")]
    public class CultivationMethod : ScriptableObject
    {
        [Header("身份")]
        public string methodId = "";
        public string displayName = "未命名功法";
        public bool isPreset = false;      // 官方预设(不可覆盖) / 玩家自定义
        [TextArea] public string description = "";
        public Sprite icon;                // 功法图标 (功法编辑面板列表/标题显示)

        [Header("基础术法 (空=保持默认配置)")]
        public string baseSkillId = "";    // SpiritOrb / Fireball / QiBlade / Talisman / SpiritArray / FlyingSword

        [Header("灵根 (None=不加)")]
        public Element spiritRoot = Element.None;
        public int spiritRootLevel = 1;    // 初始灵根等级

        [Header("5个词缀槽位 (Lv2/4/6/8/10解锁)")]
        public AffixType[] affixes = new AffixType[5]
        {
            AffixType.Split, AffixType.Pierce, AffixType.Multiply,
            AffixType.Track, AffixType.Lingering
        };

        [Header("道方向 (空=不预选)")]
        public string daoId = "";

        [Header("功法加成")]
        public float damageBonus = 0f;         // 伤害 +%
        public float attackSpeedBonus = 0f;   // 攻速 +%

        /// <summary>
        /// 复制为自定义功法 (复制预设后可自由编辑)
        /// </summary>
        public CultivationMethod CloneAsCustom(string newId)
        {
            var m = CreateInstance<CultivationMethod>();
            m.methodId = newId;
            m.displayName = displayName + "·改";
            m.isPreset = false;
            m.description = description;
            m.icon = icon;
            m.baseSkillId = baseSkillId;
            m.spiritRoot = spiritRoot;
            m.spiritRootLevel = spiritRootLevel;
            m.affixes = (AffixType[])affixes.Clone();
            m.daoId = daoId;
            m.damageBonus = damageBonus;
            m.attackSpeedBonus = attackSpeedBonus;
            return m;
        }
    }
}
