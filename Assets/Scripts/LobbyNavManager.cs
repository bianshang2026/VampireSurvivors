using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 大厅导航管理器 — NavBg 下六个按钮的页面切换。
/// 背包/功法/角色/地图/设置 互斥开关, 访市仅占位。
/// </summary>
public class LobbyNavManager : MonoBehaviour
{
    [Header("页面 (初始隐藏, 位于NavBg之下)")]
    public GameObject backpackPanel;
    public GameObject gongFaPanel;
    public GameObject characterPanel;
    public GameObject mapPanel;
    public GameObject settingsPanel;

    [Header("设置页滑块")]
    public Slider musicSlider;
    public Slider sfxSlider;

    private GameObject _current;

    private void Start()
    {
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            AudioListener.volume = musicSlider.value;
            musicSlider.onValueChanged.AddListener(OnMusicVolume);
        }
        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SfxVolume", 1f);
            sfxSlider.onValueChanged.AddListener(OnSfxVolume);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && _current != null)
            CloseAll();
    }

    public void OpenBackpack()  => Toggle(backpackPanel);
    public void OpenGongFa()    => Toggle(gongFaPanel);
    public void OpenCharacter() => Toggle(characterPanel);
    public void OpenMap()       => Toggle(mapPanel);
    public void OpenSettings()  => Toggle(settingsPanel);

    /// <summary>访市暂未开放 — 仅按钮反馈。</summary>
    public void OnClickFangShi()
    {
        Debug.Log("[LobbyNav] 访市 敬请期待");
    }

    /// <summary>地图页"进入秘境"按钮。</summary>
    public void EnterSecretRealm()
    {
        if (BiopunkRV.SceneTransitionManager.Instance != null)
            BiopunkRV.SceneTransitionManager.Instance.EnterSecretRealm();
        else
            SceneManager.LoadScene("秘境");
    }

    public void CloseAll()
    {
        if (backpackPanel != null) backpackPanel.SetActive(false);
        if (gongFaPanel != null) gongFaPanel.SetActive(false);
        if (characterPanel != null) characterPanel.SetActive(false);
        if (mapPanel != null) mapPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        _current = null;
    }

    public void OnMusicVolume(float v)
    {
        PlayerPrefs.SetFloat("MusicVolume", v);
        AudioListener.volume = v;
    }

    public void OnSfxVolume(float v)
    {
        PlayerPrefs.SetFloat("SfxVolume", v);
    }

    private void Toggle(GameObject panel)
    {
        if (panel == null) return;
        if (_current == panel && panel.activeSelf)
        {
            CloseAll();
            return;
        }
        CloseAll();
        panel.SetActive(true);
        // 保持在 NavBg 之下一位: 底部导航栏永远可见可点。
        panel.transform.SetSiblingIndex(panel.transform.parent.childCount - 2);
        _current = panel;
    }
}
