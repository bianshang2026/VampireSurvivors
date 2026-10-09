using UnityEngine;
using UnityEngine.UI;

public class CharacterPanelController : MonoBehaviour
{
    [Tooltip("6个装备槽边框Image，顺序: 0武器 1法宝 2头冠 3道袍 4靴子 5饰品")]
    public Image[] equipSlots;

    private Image[] _icons;
    private Behaviour[] _emptyLabels;

    public void SetEquip(int slotIndex, Sprite icon)
    {
        if (!IsValid(slotIndex)) return;
        EnsureIcon(slotIndex);
        _icons[slotIndex].sprite = icon;
        _icons[slotIndex].enabled = icon != null;
        var label = GetEmptyLabel(slotIndex);
        if (label != null) label.enabled = icon == null;
    }

    public void ClearEquip(int slotIndex)
    {
        SetEquip(slotIndex, null);
    }

    private bool IsValid(int slotIndex)
    {
        return equipSlots != null && slotIndex >= 0 && slotIndex < equipSlots.Length && equipSlots[slotIndex] != null;
    }

    private void EnsureIcon(int index)
    {
        if (_icons == null || _icons.Length != equipSlots.Length) _icons = new Image[equipSlots.Length];
        if (_icons[index] != null) return;

        var slotRect = (RectTransform)equipSlots[index].transform;
        var go = new GameObject("EquipIcon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(slotRect, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(slotRect.sizeDelta.x * 0.78f, slotRect.sizeDelta.y * 0.78f);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.enabled = false;
        _icons[index] = img;
    }

    private Behaviour GetEmptyLabel(int index)
    {
        if (_emptyLabels == null || _emptyLabels.Length != equipSlots.Length) _emptyLabels = new Behaviour[equipSlots.Length];
        if (_emptyLabels[index] == null)
        {
            var t = equipSlots[index].transform.Find("EmptyLabel");
            if (t != null) _emptyLabels[index] = t.GetComponent<TMPro.TextMeshProUGUI>();
        }
        return _emptyLabels[index];
    }
}