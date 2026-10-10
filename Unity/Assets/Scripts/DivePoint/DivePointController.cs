using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing
{
    /// <summary>下水点木筏 Demo：左右移动、木箱交互与最小可用背包。</summary>
    public sealed class DivePointController : MonoBehaviour
    {
        [Serializable]
        private sealed class ItemStack
        {
            public string Id;
            public string Name;
            public int Count;

            public ItemStack(string id, string name, int count)
            {
                Id = id;
                Name = name;
                Count = count;
            }
        }

        [Header("场景实体")]
        public RectTransform Player;
        public RectTransform Chest;
        public RawImage ChestImage;
        public Button ChestButton;

        [Header("交互 UI")]
        public Text Prompt;
        public Text Status;
        public GameObject ChestPanel;
        public Transform ChestRows;
        public Transform ChestBagSlots;
        public Button ChestCloseButton;
        public Button TakeAllButton;

        [Header("背包 UI")]
        public GameObject BackpackPanel;
        public Transform BackpackSlots;
        public Button BackpackCloseButton;

        [Header("移动参数")]
        public float MoveSpeed = 330f;
        public float LeftLimit = -560f;
        public float RightLimit = 560f;
        public float InteractDistance = 155f;

        private readonly List<ItemStack> _chestItems = new List<ItemStack>();
        private readonly List<ItemStack> _bagItems = new List<ItemStack>();
        private const int BagCapacity = 12;
        private bool _chestOpen;

        private void Awake()
        {
            _chestItems.Add(new ItemStack("MAT_WOOD", "木材", 6));
            _chestItems.Add(new ItemStack("MAT_ROPE", "绳索", 3));
            _chestItems.Add(new ItemStack("FOOD_RATION", "应急口粮", 2));

            if (ChestButton != null) ChestButton.onClick.AddListener(TryOpenChest);
            if (ChestCloseButton != null) ChestCloseButton.onClick.AddListener(CloseChest);
            if (BackpackCloseButton != null) BackpackCloseButton.onClick.AddListener(CloseBackpack);
            if (TakeAllButton != null) TakeAllButton.onClick.AddListener(TakeAll);

            BindChestRows();
            if (ChestPanel != null) ChestPanel.SetActive(false);
            if (BackpackPanel != null) BackpackPanel.SetActive(false);
            RefreshUi();
        }

        private void Update()
        {
            bool modalOpen = (ChestPanel != null && ChestPanel.activeSelf) ||
                             (BackpackPanel != null && BackpackPanel.activeSelf);
            if (!modalOpen) MovePlayer();

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (_chestOpen) CloseChest();
                else TryOpenChest();
            }
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (BackpackPanel != null && BackpackPanel.activeSelf) CloseBackpack();
                else OpenBackpack();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseChest();
                CloseBackpack();
            }

            UpdatePrompt();
        }

        private void MovePlayer()
        {
            if (Player == null) return;
            float horizontal = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(horizontal) < 0.01f) return;

            var p = Player.anchoredPosition;
            p.x = Mathf.Clamp(p.x + horizontal * MoveSpeed * Time.deltaTime, LeftLimit, RightLimit);
            Player.anchoredPosition = p;

            var scale = Player.localScale;
            scale.x = Mathf.Abs(scale.x) * (horizontal < 0f ? -1f : 1f);
            Player.localScale = scale;
        }

        private bool IsNearChest()
        {
            return Player != null && Chest != null &&
                   Mathf.Abs(Player.anchoredPosition.x - Chest.anchoredPosition.x) <= InteractDistance;
        }

        private void UpdatePrompt()
        {
            if (Prompt == null) return;
            bool near = IsNearChest();
            Prompt.gameObject.SetActive(near && !_chestOpen);
            if (near) Prompt.text = "E  打开木箱";
        }

        public void TryOpenChest()
        {
            if (!IsNearChest())
            {
                SetStatus("再靠近木箱一点");
                return;
            }

            _chestOpen = true;
            if (BackpackPanel != null) BackpackPanel.SetActive(false);
            if (ChestPanel != null) ChestPanel.SetActive(true);
            if (ChestImage != null) ChestImage.color = new Color(1f, 0.88f, 0.58f, 1f);
            SetStatus("木箱已打开");
            RefreshUi();
        }

        public void CloseChest()
        {
            _chestOpen = false;
            if (ChestPanel != null) ChestPanel.SetActive(false);
            if (ChestImage != null) ChestImage.color = Color.white;
            RefreshUi();
        }

        private void OpenBackpack()
        {
            CloseChest();
            if (BackpackPanel != null) BackpackPanel.SetActive(true);
            SetStatus("随身背包");
            RefreshUi();
        }

        private void CloseBackpack()
        {
            if (BackpackPanel != null) BackpackPanel.SetActive(false);
        }

        private void BindChestRows()
        {
            if (ChestRows == null) return;
            for (int i = 0; i < ChestRows.childCount; i++)
            {
                int index = i;
                var button = ChestRows.GetChild(i).Find("拿取")?.GetComponent<Button>();
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => TakeOne(index));
            }
        }

        private void TakeOne(int index)
        {
            if (index < 0 || index >= _chestItems.Count) return;
            var item = _chestItems[index];
            if (!CanAdd(item.Id))
            {
                SetStatus("背包已满");
                return;
            }

            AddToBag(item.Id, item.Name, 1);
            item.Count--;
            SetStatus("获得 " + item.Name + " ×1");
            if (item.Count <= 0) _chestItems.RemoveAt(index);
            RefreshUi();
        }

        private void TakeAll()
        {
            int moved = 0;
            for (int i = _chestItems.Count - 1; i >= 0; i--)
            {
                var item = _chestItems[i];
                if (!CanAdd(item.Id)) continue;
                AddToBag(item.Id, item.Name, item.Count);
                moved += item.Count;
                _chestItems.RemoveAt(i);
            }
            SetStatus(moved > 0 ? "已取走全部物品" : "木箱为空或背包已满");
            RefreshUi();
        }

        private bool CanAdd(string id)
        {
            return _bagItems.Exists(x => x.Id == id) || _bagItems.Count < BagCapacity;
        }

        private void AddToBag(string id, string name, int count)
        {
            var stack = _bagItems.Find(x => x.Id == id);
            if (stack != null) stack.Count += count;
            else _bagItems.Add(new ItemStack(id, name, count));
        }

        private void RefreshUi()
        {
            RefreshChestRows();
            RefreshBagSlots(ChestBagSlots);
            RefreshBagSlots(BackpackSlots);
        }

        private void RefreshChestRows()
        {
            if (ChestRows == null) return;
            for (int i = 0; i < ChestRows.childCount; i++)
            {
                var row = ChestRows.GetChild(i);
                bool used = i < _chestItems.Count;
                row.gameObject.SetActive(used);
                if (!used) continue;
                var item = _chestItems[i];
                var name = row.Find("名称")?.GetComponent<Text>();
                var count = row.Find("数量")?.GetComponent<Text>();
                if (name != null) name.text = item.Name;
                if (count != null) count.text = "×" + item.Count;
            }
        }

        private void RefreshBagSlots(Transform slots)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.childCount; i++)
            {
                var label = slots.GetChild(i).Find("文字")?.GetComponent<Text>();
                if (label == null) continue;
                label.text = i < _bagItems.Count
                    ? _bagItems[i].Name + "\n×" + _bagItems[i].Count
                    : "空";
            }
        }

        private void SetStatus(string message)
        {
            if (Status != null) Status.text = message;
        }
    }
}
