using UnityEngine;
using UnityEngine.UI;

public class SectUIController : MonoBehaviour
{
    public GameObject inventoryPanel;
    public Button[] navButtons;

    private bool inventoryOpen = false;

    void Start()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        if (navButtons != null)
        {
            for (int i = 0; i < navButtons.Length; i++)
            {
                int index = i;
                if (navButtons[i] != null)
                    navButtons[i].onClick.AddListener(() => OnNavButtonClick(index));
            }
        }

        // Register close button
        Button closeBtn = FindCloseButton();
        if (closeBtn != null)
            closeBtn.onClick.AddListener(CloseInventory);
    }

    Button FindCloseButton()
    {
        Button[] allBtns = FindObjectsOfType<Button>();
        foreach (Button b in allBtns)
        {
            if (b.name.Contains("Close"))
                return b;
        }
        return null;
    }

    void OnNavButtonClick(int index)
    {
        // 0=宗门, 1=洞府, 2=秘境, 3=功法, 4=储物
        switch (index)
        {
            case 4:
                ToggleInventory();
                break;
            default:
                Debug.Log("Tab " + index + " clicked");
                break;
        }
    }

    public void ToggleInventory()
    {
        if (inventoryPanel == null) return;
        inventoryOpen = !inventoryOpen;
        inventoryPanel.SetActive(inventoryOpen);
    }

    public void CloseInventory()
    {
        if (inventoryPanel == null) return;
        inventoryOpen = false;
        inventoryPanel.SetActive(false);
    }
}
