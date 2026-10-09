using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BiopunkRV
{
    /// <summary>
    /// 建造系统：按按钮进入建造模式，鼠标点击网格放置浮空岛地块。
    /// </summary>
    public class BuildSystem : MonoBehaviour
    {
        [Header("Build Settings")]
        public Sprite tileSprite;              // 要放置的浮空岛Sprite
        public float tileScale = 12f;           // 放置的地块缩放
        public int sortingOrder = 10;          // 地块sortingOrder

        [Header("Grid References")]
        public Transform gridParent;           // GridLines父物体

        [Header("UI")]
        public Button buildButton;             // 建造按钮
        public Text statusText;                // 状态文字

        [Header("Colors")]
        public Color normalTintColor = new Color(1f, 1f, 1f, 0.4f);
        public Color hoverTintColor = new Color(0.5f, 1f, 0.5f, 0.7f);
        public Color occupiedTintColor = new Color(1f, 0.3f, 0.3f, 0.3f);

        private bool _isBuildMode = false;
        private Camera _cam;
        private GameObject _hoverCell;

        private void Awake()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = GameObject.Find("Main Camera");
                if (camGo != null) _cam = camGo.GetComponent<Camera>();
            }

            // 自动查找并绑定按钮
            if (buildButton == null)
            {
                var btnObj = GameObject.Find("BuildButton");
                if (btnObj != null) buildButton = btnObj.GetComponent<Button>();
            }

            if (buildButton != null)
                buildButton.onClick.AddListener(ToggleBuildMode);

            // 自动查找GridLines
            if (gridParent == null)
            {
                var grid = GameObject.Find("GridLines");
                if (grid != null) gridParent = grid.transform;
            }
        }

        private void Update()
        {
            if (!_isBuildMode) return;

            GameObject cell = GetCellUnderMouse();

            if (cell != _hoverCell)
            {
                if (_hoverCell != null)
                    SetCellTint(_hoverCell, normalTintColor);

                _hoverCell = cell;

                if (_hoverCell != null)
                {
                    bool occupied = _hoverCell.name.StartsWith("Tile_");
                    SetCellTint(_hoverCell, occupied ? occupiedTintColor : hoverTintColor);
                }
            }

            if (Input.GetMouseButtonDown(0) && _hoverCell != null)
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                PlaceTile(_hoverCell);
            }
        }

        public void ToggleBuildMode()
        {
            _isBuildMode = !_isBuildMode;

            if (statusText != null)
                statusText.text = _isBuildMode ? "建造模式：开" : "建造模式：关";

            if (!_isBuildMode && _hoverCell != null)
            {
                SetCellTint(_hoverCell, normalTintColor);
                _hoverCell = null;
            }

            Debug.Log($"[BuildSystem] Build mode: {(_isBuildMode ? "ON" : "OFF")}");
        }

        private GameObject GetCellUnderMouse()
        {
            if (_cam == null) return null;

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1000f))
            {
                if (hit.transform != null && hit.transform.name.StartsWith("Cell_"))
                    return hit.transform.gameObject;
            }

            return null;
        }

        private void SetCellTint(GameObject cell, Color color)
        {
            var sr = cell.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = color;
        }

        private void PlaceTile(GameObject cell)
        {
            if (tileSprite == null)
            {
                Debug.LogWarning("[BuildSystem] tileSprite 未设置！");
                return;
            }

            string tileName = "Tile_" + cell.name;

            // 检查是否已有地块
            if (gridParent != null && gridParent.Find(tileName) != null)
            {
                Debug.Log($"[BuildSystem] {cell.name} 已有地块");
                return;
            }

            GameObject tile = new GameObject(tileName);
            tile.transform.SetParent(gridParent, false);
            tile.transform.localPosition = cell.transform.localPosition;
            tile.transform.localRotation = Quaternion.identity;
            tile.transform.localScale = new Vector3(tileScale, tileScale, 1f);

            var sr = tile.AddComponent<SpriteRenderer>();
            sr.sprite = tileSprite;
            sr.sortingOrder = sortingOrder;

            Debug.Log($"[BuildSystem] 在 {cell.name} 放置了地块");
        }
    }
}
