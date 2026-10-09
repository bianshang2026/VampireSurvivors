using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BiopunkRV
{
    /// <summary>
    /// 场景切换管理器 — 大厅↔秘境传送，带淡入淡出过渡效果。
    /// 使用静态数据载体在场景间传递玩家数据。
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Transition")]
        public float fadeDuration = 0.8f;
        public Color fadeColor = new Color(0f, 0f, 0f, 1f);

        private static GameObject _fadeCanvas;
        private static Image _fadeImage;
        private bool _transitioning;

        // ---- 跨场景数据载体 ----
        public static class TransitionData
        {
            public static string TargetScene;
            public static int SpiritStones;        // 灵石
            public static int MethodFragments;     // 功法碎片
            public static int AffixFragments;       // 词缀碎片
            public static int PlayerLevel;          // 玩家等级(局外强化用)
            public static float PlayTime;           // 本局游戏时间
            public static int EnemiesKilled;        // 本局击杀数
            public static bool FromSecretRealm;     // 从秘境返回(用于结算)
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>传送到秘境(大厅→秘境)</summary>
        public void EnterSecretRealm()
        {
            if (_transitioning) return;
            MetaProgression.SettleRun();
            TransitionData.TargetScene = "秘境";
            TransitionData.FromSecretRealm = false;
            StartCoroutine(FadeAndLoadScene("秘境"));
        }

        /// <summary>返回大厅(秘境→大厅)</summary>
        public void ReturnToLobby()
        {
            if (_transitioning) return;
            MetaProgression.SettleRun();
            TransitionData.TargetScene = "大厅";
            TransitionData.FromSecretRealm = true;
            StartCoroutine(FadeAndLoadScene("大厅"));
        }

        /// <summary>通用场景切换</summary>
        public void TransitionToScene(string sceneName)
        {
            if (_transitioning) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "秘境") MetaProgression.SettleRun();
            TransitionData.TargetScene = sceneName;
            StartCoroutine(FadeAndLoadScene(sceneName));
        }

        private IEnumerator FadeAndLoadScene(string sceneName)
        {
            if (_transitioning) yield break;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("场景未加入构建列表: " + sceneName);
                yield break;
            }
            _transitioning = true;
            Time.timeScale = 1f;
            // 创建或获取fade canvas
            EnsureFadeCanvas();

            // 淡出(黑屏)
            yield return StartCoroutine(Fade(0f, 1f));

            // 加载场景
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (!asyncLoad.isDone)
                yield return null;

            // 淡入(透明)
            yield return StartCoroutine(Fade(1f, 0f));

            // 销毁fade canvas
            if (_fadeCanvas != null)
            {
                Destroy(_fadeCanvas);
                _fadeCanvas = null;
            }
            _transitioning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            EnsureFadeCanvas();

            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / fadeDuration);
                float alpha = Mathf.Lerp(from, to, t);
                _fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
                yield return null;
            }
            _fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, to);
        }

        private static void EnsureFadeCanvas()
        {
            if (_fadeCanvas != null) return;

            _fadeCanvas = new GameObject("TransitionFadeCanvas");
            _fadeCanvas.transform.SetParent(null);

            var canvas = _fadeCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            var scaler = _fadeCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _fadeCanvas.AddComponent<GraphicRaycaster>();

            var imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(_fadeCanvas.transform, false);
            _fadeImage = imageObj.AddComponent<Image>();
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);

            var rt = imageObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            DontDestroyOnLoad(_fadeCanvas);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
