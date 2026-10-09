using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace BiopunkRV
{
    /// <summary>
    /// Interactive furniture inside cabin interior views. Supports hover labels and click feedback.
    /// </summary>
    public class InteractiveFurniture : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public enum FurnitureType
        {
            Stove,          // Click to toggle red glow
            Faucet,         // Click to show water for 2s
            Bed,            // Click to show Zzz
            StorageDoor,    // Click to open door + blue light
            Generic         // Just hover label
        }

        [Header("Furniture")]
        public string furnitureName = "家具";
        public FurnitureType type = FurnitureType.Generic;

        [Header("Hover Label")]
        public GameObject labelPrefab;
        public Vector2 labelOffset = new Vector2(0f, 40f);

        [Header("Effects")]
        public GameObject glowObject;       // For stove: the glow sprite
        public GameObject waterObject;       // For faucet: water stream
        public GameObject zzzObject;         // For bed: Zzz text
        public GameObject doorOpenObject;     // For storage door: open state with blue light

        private GameObject _activeLabel;
        private bool _isStoveOn;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (labelPrefab != null)
            {
                _activeLabel = Instantiate(labelPrefab, transform);
                _activeLabel.name = "HoverLabel";
                _activeLabel.transform.localPosition = labelOffset;

                var rt = _activeLabel.GetComponent<RectTransform>();
                if (rt == null) rt = _activeLabel.AddComponent<RectTransform>();

                var tmp = _activeLabel.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = furnitureName;
                    tmp.fontSize = 16;
                    tmp.color = new Color(1f, 0.85f, 0.5f, 0.9f);
                    tmp.alignment = TextAlignmentOptions.Center;
                }

                var bg = _activeLabel.GetComponent<Image>();
                if (bg != null)
                    bg.color = new Color(0.1f, 0.08f, 0.05f, 0.75f);

                _activeLabel.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_activeLabel != null)
                Destroy(_activeLabel);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            switch (type)
            {
                case FurnitureType.Stove:
                    if (glowObject != null)
                    {
                        _isStoveOn = !_isStoveOn;
                        glowObject.SetActive(_isStoveOn);
                        Debug.Log($"[Furniture] Stove {(_isStoveOn ? "lit" : "off")}");
                    }
                    break;

                case FurnitureType.Faucet:
                    if (waterObject != null)
                        StartCoroutine(WaterRoutine());
                    break;

                case FurnitureType.Bed:
                    if (zzzObject != null)
                        StartCoroutine(ZzzRoutine());
                    else
                        ShowFloatingText("Zzz...");
                    break;

                case FurnitureType.StorageDoor:
                    if (doorOpenObject != null)
                    {
                        bool isOpen = doorOpenObject.activeSelf;
                        doorOpenObject.SetActive(!isOpen);
                        Debug.Log($"[Furniture] Storage door {(!isOpen ? "opened" : "closed")}");
                    }
                    break;

                default:
                    ShowFloatingText(furnitureName);
                    break;
            }
        }

        private IEnumerator WaterRoutine()
        {
            waterObject.SetActive(true);
            Debug.Log("[Furniture] Water running...");
            yield return new WaitForSeconds(2f);
            waterObject.SetActive(false);
        }

        private IEnumerator ZzzRoutine()
        {
            zzzObject.SetActive(true);
            var rt = zzzObject.GetComponent<RectTransform>();
            Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;

            float timer = 0f;
            while (timer < 2f)
            {
                timer += Time.deltaTime;
                if (rt != null)
                {
                    float y = startPos.y + timer * 30f;
                    float alpha = 1f - (timer / 2f);
                    rt.anchoredPosition = new Vector2(startPos.x, y);

                    var tmp = zzzObject.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        var c = tmp.color;
                        c.a = alpha;
                        tmp.color = c;
                    }
                }
                yield return null;
            }
            zzzObject.SetActive(false);
            if (rt != null) rt.anchoredPosition = startPos;
        }

        private void ShowFloatingText(string text)
        {
            Debug.Log($"[Furniture] {furnitureName}: {text}");
        }
    }
}
