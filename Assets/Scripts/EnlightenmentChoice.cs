using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BiopunkRV
{
    /// <summary>Queues choices when several techniques reach enlightenment in one frame.</summary>
    public class EnlightenmentChoice : MonoBehaviour
    {
        private class Request { public string[] options; public Action<string> choose; }
        private static EnlightenmentChoice instance;
        private readonly Queue<Request> requests = new Queue<Request>();
        private GameObject canvasObject;
        public static bool IsOpen => instance != null && instance.requests.Count > 0;

        public static void Show(string[] options, Action<string> choose)
        {
            if (options == null || options.Length == 0) return;
            if (instance == null) instance = new GameObject("EnlightenmentChoices").AddComponent<EnlightenmentChoice>();
            instance.requests.Enqueue(new Request { options = options, choose = choose });
            if (instance.requests.Count == 1) instance.Build();
        }

        private void LateUpdate() { if (requests.Count > 0) Time.timeScale = 0f; }

        private void Build()
        {
            if (canvasObject != null) { canvasObject.SetActive(false); Destroy(canvasObject); }
            if (EventSystem.current == null) new GameObject("DaoEvents", typeof(EventSystem), typeof(StandaloneInputModule));
            canvasObject = new GameObject("悟道选择", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
            var scale = canvasObject.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scale.referenceResolution = new Vector2(1600, 900);
            var root = new GameObject("Background", typeof(RectTransform), typeof(Image)); root.transform.SetParent(canvasObject.transform, false);
            var rt = root.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(.06f, .13f, .16f, .94f);
            Text(root.transform, "悟道 · 选择进化方向", new Vector2(0, 230), new Vector2(1100, 80), 40);
            Text(root.transform, "此处展示已在宗门领悟的道；选择后术法进化，再次满级可蓄积神通。", new Vector2(0, 145), new Vector2(1300, 65), 23);
            var request = requests.Peek();
            for (int i = 0; i < request.options.Length; i++)
            {
                string id = request.options[i];
                var card = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button)); card.transform.SetParent(root.transform, false);
                var cr = card.GetComponent<RectTransform>(); cr.sizeDelta = new Vector2(380, 290); cr.anchoredPosition = new Vector2((i - (request.options.Length - 1) / 2f) * 410, -60);
                card.GetComponent<Image>().color = new Color(.23f, .43f, .4f);
                Text(card.transform, DaoSystem.GetDaoName(id) + "\n\n" + DaoSystem.GetDaoDescription(id), Vector2.zero, new Vector2(330, 250), 26);
                card.GetComponent<Button>().onClick.AddListener(() => Select(id));
            }
            Time.timeScale = 0f;
        }

        private void Select(string id)
        {
            if (requests.Count == 0) return;
            var request = requests.Dequeue();
            request.choose(id);
            if (requests.Count > 0) Build();
            else { canvasObject.SetActive(false); Destroy(canvasObject); Time.timeScale = 1f; }
        }

        private static void Text(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.anchoredPosition = position;
            var text = go.GetComponent<Text>(); text.font = Resources.Load<Font>("CultivationFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        }
    }
}
