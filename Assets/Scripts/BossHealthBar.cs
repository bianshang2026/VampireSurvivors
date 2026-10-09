using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BiopunkRV
{
    /// <summary>
    /// Boss血条UI — 屏幕顶部显示Boss名称和血量条。
    /// Boss出现时自动显示，死亡时自动隐藏。
    /// </summary>
    public class BossHealthBar : MonoBehaviour
    {
        private static BossHealthBar _instance;

        private GameObject _bar;
        private Image _fill;
        private TextMeshProUGUI _nameText;
        private EnemyController _target;

        public static void Show(EnemyController boss, string bossName)
        {
            EnsureInstance();
            if (_instance == null) return;
            _instance._target = boss;
            _instance._bar.SetActive(true);
            if (_instance._nameText != null)
                _instance._nameText.text = bossName;
        }

        public static void Hide()
        {
            if (_instance != null && _instance._bar != null)
                _instance._bar.SetActive(false);
            if (_instance != null)
                _instance._target = null;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("BossHealthBar");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<BossHealthBar>();

            // 创建血条根
            _instance._bar = new GameObject("BossBarRoot");
            _instance._bar.transform.SetParent(go.transform, false);
            var barRT = _instance._bar.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f, 0.93f);
            barRT.anchorMax = new Vector2(0.5f, 0.93f);
            barRT.sizeDelta = new Vector2(800, 50);
            barRT.anchoredPosition = Vector2.zero;

            // 背景
            var bgObj = new GameObject("BossBarBG");
            bgObj.transform.SetParent(_instance._bar.transform, false);
            var bgRT = bgObj.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.05f, 0.05f, 0.9f);

            // 填充
            var fillObj = new GameObject("BossBarFill");
            fillObj.transform.SetParent(bgObj.transform, false);
            var fillRT = fillObj.AddComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0.5f);
            fillRT.anchorMax = new Vector2(1f, 0.5f);
            fillRT.pivot = new Vector2(0f, 0.5f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fillRT.sizeDelta = new Vector2(-4, -4);
            _instance._fill = fillObj.AddComponent<Image>();
            _instance._fill.color = new Color(0.8f, 0.15f, 0.1f, 1f);
            _instance._fill.type = Image.Type.Filled;
            _instance._fill.fillMethod = Image.FillMethod.Horizontal;

            // Boss名称
            var nameObj = new GameObject("BossName");
            nameObj.transform.SetParent(_instance._bar.transform, false);
            var nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.5f, 1f);
            nameRT.anchorMax = new Vector2(0.5f, 1f);
            nameRT.pivot = new Vector2(0.5f, 0f);
            nameRT.sizeDelta = new Vector2(400, 30);
            nameRT.anchoredPosition = Vector2.zero;
            _instance._nameText = nameObj.AddComponent<TextMeshProUGUI>();
            _instance._nameText.text = "Boss";
            _instance._nameText.fontSize = 28;
            _instance._nameText.alignment = TextAlignmentOptions.Center;
            _instance._nameText.color = new Color(1f, 0.4f, 0.3f, 1f);

            _instance._bar.SetActive(false);
        }

        private void Update()
        {
            if (_target == null || _bar == null || !_bar.activeSelf) return;

            // 检查Boss是否已死
            if (_target == null || _target.gameObject == null)
            {
                Hide();
                return;
            }

            // 更新血条
            if (_fill != null)
            {
                float pct = Mathf.Clamp01(_target.currentHealth / _target.maxHealth);
                _fill.fillAmount = pct;
            }
        }
    }
}
