using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Gắn vào từng item trong danh sách avatar.
/// Hiển thị ảnh thumbnail + tên + highlight khi được chọn.
/// </summary>
public class AvatarItemPrefab : MonoBehaviour
{
    [SerializeField] private Image            avatarImage;
    [SerializeField] private Image            selectionBorder;   // highlight khi selected
    [SerializeField] private TextMeshProUGUI  nameText;
    [SerializeField] private Button           selectButton;

    public event Action<int> OnSelected;

    private int itemIndex;

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(HandleClick);

        SetSelected(false);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleClick);
    }

    // ═══════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════

    public void Setup(int index, Sprite thumbnail, string avatarName)
    {
        itemIndex = index;

        if (avatarImage != null)
            avatarImage.sprite = thumbnail;

        if (nameText != null)
            nameText.text = avatarName;
    }

    public void SetSelected(bool selected)
    {
        if (selectionBorder != null)
            selectionBorder.gameObject.SetActive(selected);
    }

    // ═══════════════════════════════════════════════
    // INTERNAL
    // ═══════════════════════════════════════════════

    private void HandleClick() => OnSelected?.Invoke(itemIndex);
}
