using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using System.Collections.Generic;

namespace BiopunkRV.EditorTools
{
    public static class SceneSetup
    {
        [MenuItem("Tools/BiopunkRV/Setup Scene")]
        public static void Run()
        {
            var canvas = GameObject.Find("UICanvas");
            if (canvas == null) { Debug.LogError("No UICanvas"); return; }

            var asmPath = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                "Library/ScriptAssemblies/Assembly-CSharp.dll");
            var loadedAsm = Assembly.LoadFrom(asmPath);
            var segType = loadedAsm.GetType("BiopunkRV.RVCabinSegment");

            var rv = canvas.transform.Find("BiopunkRV");
            var views = canvas.transform.Find("InteriorViews");

            string[] segNames = { "RVSegment_1_驾驶区", "RVSegment_2_起居区", "RVSegment_3_储物区", "RVSegment_4_睡眠区" };
            string[] interiorNames = { "InteriorView_1_驾驶区", "InteriorView_2_起居区", "InteriorView_3_储物卫生区", "InteriorView_4_睡眠区" };
            string[] displayNames = { "驾驶区", "起居区", "储物卫生区", "睡眠区" };

            var segments = new List<object>();

            for (int i = 0; i < 4; i++)
            {
                var segGO = rv.Find(segNames[i]);
                if (segGO == null) { Debug.LogError("Not found: " + segNames[i]); continue; }
                var seg = segGO.GetComponent(segType);
                if (seg == null) { Debug.LogError("No comp: " + segNames[i]); continue; }
                segments.Add(seg);

                var view = views.Find(interiorNames[i]);
                if (view == null) { Debug.LogError("Interior not found: " + interiorNames[i]); continue; }

                // Set fields
                segType.GetField("interiorView").SetValue(seg, view.gameObject);
                segType.GetField("rvExterior").SetValue(seg, rv.gameObject);
                segType.GetField("segmentIndex").SetValue(seg, i + 1);
                segType.GetField("totalSegments").SetValue(seg, 4);

                // CanvasGroup
                var cg = view.GetComponent<CanvasGroup>();
                if (cg == null) cg = view.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                var rvCG = rv.gameObject.GetComponent<CanvasGroup>();
                if (rvCG == null) rvCG = rv.gameObject.AddComponent<CanvasGroup>();

                // Title
                var title = view.Find("Title");
                if (title != null)
                {
                    var tmp = title.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = displayNames[i] + " · 侧面剖视";
                }

                // Page number
                var oldPage = view.Find("PageNumber");
                if (oldPage != null) Object.DestroyImmediate(oldPage.gameObject);

                var pageGO = new GameObject("PageNumber");
                pageGO.transform.SetParent(view, false);
                var pageTMP = pageGO.AddComponent<TextMeshProUGUI>();
                pageTMP.text = (i + 1) + "/4";
                pageTMP.fontSize = 24;
                pageTMP.alignment = TextAlignmentOptions.Center;
                pageTMP.color = new Color(1f, 0.78f, 0.25f, 0.8f);
                var pageRT = pageGO.GetComponent<RectTransform>();
                pageRT.anchorMin = new Vector2(0.5f, 0f);
                pageRT.anchorMax = new Vector2(0.5f, 0f);
                pageRT.pivot = new Vector2(0.5f, 0f);
                pageRT.anchoredPosition = new Vector2(0f, 20f);
                pageRT.sizeDelta = new Vector2(100f, 35f);

                // Left arrow
                var oldLeft = view.Find("ArrowLeft");
                if (oldLeft != null) Object.DestroyImmediate(oldLeft.gameObject);
                if (i > 0) CreateArrow(view, "ArrowLeft", "\u2190", 0f, 20f);

                // Right arrow
                var oldRight = view.Find("ArrowRight");
                if (oldRight != null) Object.DestroyImmediate(oldRight.gameObject);
                if (i < 3) CreateArrow(view, "ArrowRight", "\u2192", 1f, -20f);

                // Back button
                var backBtn = view.Find("BackButton");
                if (backBtn != null)
                {
                    var btn = backBtn.GetComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    int idx = i;
                    btn.onClick.AddListener(() =>
                    {
                        var method = segType.GetMethod("HideInterior");
                        method?.Invoke(segments[idx], null);
                    });
                }

                Debug.Log("Updated: " + interiorNames[i]);
            }

            // Wire prev/next
            for (int i = 0; i < 4; i++)
            {
                if (i > 0) segType.GetField("prevSegment").SetValue(segments[i], segments[i - 1]);
                if (i < 3) segType.GetField("nextSegment").SetValue(segments[i], segments[i + 1]);
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("=== Scene updated ===");
        }

        private static void CreateArrow(Transform parent, string name, string symbol, float anchorX, float posX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.12f, 0.08f, 0.85f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorX, 0.5f);
            rt.anchorMax = new Vector2(anchorX, 0.5f);
            rt.pivot = new Vector2(anchorX, 0.5f);
            rt.anchoredPosition = new Vector2(posX, 0f);
            rt.sizeDelta = new Vector2(60f, 80f);

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.text = symbol;
            txt.fontSize = 36;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(1f, 0.8f, 0.3f, 1f);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
        }
    }
}
