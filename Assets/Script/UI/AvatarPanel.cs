using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Panel chọn avatar: hiển thị danh sách AvatarItemPrefab với thumbnail.
/// Khi ấn Xác nhận → fire OnAvatarConfirmed(index).
/// </summary>
public class AvatarPanel : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;   // panel chính — ẩn khi AvatarPanel mở

    [Header("Layout")]
    [SerializeField] private Transform        contentParent;    // ScrollView Content
    [SerializeField] private AvatarItemPrefab itemPrefab;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button closeButton;

    [Header("Selected Preview")]
    [SerializeField] private Image           selectedPreviewImage;  // ảnh avatar đang pending
    [SerializeField] private TextMeshProUGUI selectedNameText;

    [Header("Avatar Data")]
    [SerializeField] private Sprite[] avatarThumbnails;
    [SerializeField] private string[] avatarNames;

    // ── Events ───────────────────────────────────
    public event Action<int> OnAvatarConfirmed;
    public event Action<int> OnAvatarPreview;   // fire ngay khi chọn item (chưa confirm)
    public event Action      OnClosed;

    // ── State ────────────────────────────────────
    private readonly List<AvatarItemPrefab> items = new List<AvatarItemPrefab>();
    private int pendingIndex  = 0;
    private int originalIndex = 0;  // index đã confirm trước khi mở panel

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirm);
        if (closeButton   != null) closeButton.onClick.AddListener(HandleClose);

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirm);
        if (closeButton   != null) closeButton.onClick.RemoveListener(HandleClose);

        ClearItems();
    }

    // ═══════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════

    /// <summary>Mở panel, highlight avatar đang được chọn ngoài menu.</summary>
    public void Show(int currentIndex)
    {
        pendingIndex  = Mathf.Clamp(currentIndex, 0, ThumbnailCount() - 1);
        originalIndex = pendingIndex;   // lưu lại để restore khi đóng không confirm

        BuildList();
        RefreshSelection(pendingIndex);

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    // ═══════════════════════════════════════════════
    // BUILD LIST
    // ═══════════════════════════════════════════════

    private void BuildList()
    {
        ClearItems();

        if (itemPrefab == null || avatarThumbnails == null || avatarThumbnails.Length == 0)
        {
            Debug.LogWarning("[AvatarPanel] itemPrefab hoặc avatarThumbnails chưa được assign!");
            return;
        }

        for (int i = 0; i < avatarThumbnails.Length; i++)
        {
            AvatarItemPrefab item = Instantiate(itemPrefab, contentParent);
            string name = (avatarNames != null && i < avatarNames.Length)
                ? avatarNames[i]
                : $"Avatar {i + 1}";

            item.Setup(i, avatarThumbnails[i], name);
            item.OnSelected += HandleItemSelected;
            items.Add(item);
        }
    }

    private void ClearItems()
    {
        foreach (var item in items)
        {
            if (item == null) continue;
            item.OnSelected -= HandleItemSelected;
            Destroy(item.gameObject);
        }
        items.Clear();
    }

    // ═══════════════════════════════════════════════
    // SELECTION
    // ═══════════════════════════════════════════════

    private void HandleItemSelected(int index)
    {
        pendingIndex = index;
        RefreshSelection(index);
        OnAvatarPreview?.Invoke(index);   // preview ngay tại selectSpawnPoint, chưa confirm
    }

    private void RefreshSelection(int index)
    {
        for (int i = 0; i < items.Count; i++)
            items[i].SetSelected(i == index);

        // Cập nhật ảnh preview + tên trong panel
        if (selectedPreviewImage != null && avatarThumbnails != null && index < avatarThumbnails.Length)
            selectedPreviewImage.sprite = avatarThumbnails[index];

        if (selectedNameText != null)
        {
            string name = (avatarNames != null && index < avatarNames.Length)
                ? avatarNames[index]
                : $"Avatar {index + 1}";
            selectedNameText.text = name;
        }
    }

    // ═══════════════════════════════════════════════
    // BUTTON HANDLERS
    // ═══════════════════════════════════════════════

    private void HandleConfirm()
    {
        OnAvatarConfirmed?.Invoke(pendingIndex);
        Hide();
    }

    private void HandleClose()
    {
        // Restore avatar cũ vì chưa confirm
        if (pendingIndex != originalIndex)
            OnAvatarPreview?.Invoke(originalIndex);

        OnClosed?.Invoke();
        Hide();
    }

    // ═══════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════

    private int ThumbnailCount() => avatarThumbnails != null ? avatarThumbnails.Length : 0;
}
