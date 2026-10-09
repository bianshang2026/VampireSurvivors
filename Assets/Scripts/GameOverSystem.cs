using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace BiopunkRV
{
    /// <summary>
    /// 游戏结束系统 — 检测玩家死亡，显示游戏结束面板，提供返回大厅按钮。
    /// 挂在 HUD_Canvas 或场景中任意持久物体上。
    /// </summary>
    public class GameOverSystem : MonoBehaviour
    {
        public static GameOverSystem Instance { get; private set; }

        [Header("Settings")]
        public float gameOverDelay = 1f;  // 死亡后延迟显示面板

        private bool _triggered;
        private float _delayTimer;
        private GameObject _panel;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (_panel != null) return;
            if (_triggered)
            {
                _delayTimer -= Time.unscaledDeltaTime;
                if (_delayTimer <= 0f) ShowGameOverPanel();
                return;
            }

            var psc = PlayerStatsComponent.Instance;
            if (psc == null) return;

            if (psc.stats.currentHP <= 0)
            {
                _triggered = true;
                _delayTimer = gameOverDelay;
                MetaProgression.SettleRun();
                // 停止刷怪和游戏
                Time.timeScale = 0.3f;
            }

            if (_triggered)
            {
                _delayTimer -= Time.deltaTime;
                if (_delayTimer <= 0f)
                {
                    ShowGameOverPanel();
                }
            }
        }

        private void ShowGameOverPanel()
        {
            if (_panel != null) return;

            // 创建游戏结束面板
            _panel = new GameObject("GameOverPanel");
            _panel.transform.SetParent(transform, false);

            RectTransform panelRT = _panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            // 半透明黑色背景
            Image bg = _panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.8f);

            // 标题
            var titleObj = new GameObject("GameOverTitle");
            titleObj.transform.SetParent(_panel.transform, false);
            var titleRT = titleObj.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 0.7f);
            titleRT.anchorMax = new Vector2(0.5f, 0.7f);
            titleRT.sizeDelta = new Vector2(600, 120);
            titleRT.anchoredPosition = Vector2.zero;
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "道陨";
            titleText.fontSize = 72;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.9f, 0.2f, 0.15f, 1f);

            // 副标题
            var subObj = new GameObject("GameOverSub");
            subObj.transform.SetParent(_panel.transform, false);
            var subRT = subObj.AddComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0.5f, 0.55f);
            subRT.anchorMax = new Vector2(0.5f, 0.55f);
            subRT.sizeDelta = new Vector2(600, 60);
            subRT.anchoredPosition = Vector2.zero;
            var subText = subObj.AddComponent<TextMeshProUGUI>();
            subText.text = "修炼未成，再接再厉";
            subText.fontSize = 36;
            subText.alignment = TextAlignmentOptions.Center;
            subText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // 返回大厅按钮
            var btnObj = new GameObject("ReturnButton");
            btnObj.transform.SetParent(_panel.transform, false);
            var btnRT = btnObj.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.35f);
            btnRT.anchorMax = new Vector2(0.5f, 0.35f);
            btnRT.sizeDelta = new Vector2(300, 80);
            btnRT.anchoredPosition = Vector2.zero;
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.5f, 0.3f, 1f);
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;

            var btnTextObj = new GameObject("ButtonText");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnTextRT = btnTextObj.AddComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.offsetMin = Vector2.zero;
            btnTextRT.offsetMax = Vector2.zero;
            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "返回大厅";
            btnText.fontSize = 32;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            btn.onClick.AddListener(ReturnToLobby);

            // 恢复时间
            Time.timeScale = 0f;
        }

        public void ReturnToLobby()
        {
            MetaProgression.SettleRun();
            Time.timeScale = 1f;
            var stm = SceneTransitionManager.Instance;
            if (stm != null)
                stm.ReturnToLobby();
            else
                SceneManager.LoadScene("大厅");
        }
    }
}
