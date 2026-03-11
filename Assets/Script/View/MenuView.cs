using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// View cho menu — không còn phụ thuộc RPM, bỏ SetAvatarAnimator async
/// </summary>
public class MenuView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField      nameInput;
    [SerializeField] private TextMeshProUGUI     statusText;
    [SerializeField] private Button              multiPlayerButton;
    [SerializeField] private Button              singlePlayerButton;
    [SerializeField] private GameObject          UIInPlay;

    // ─── Events ────────────────────────────────────
    public event Action<string> OnNameChanged;
    public event Action         OnMultiPlayerButtonClicked;
    public event Action         OnSinglePlayerButtonClicked;

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        ValidateComponents();
        if (UIInPlay != null) MoveUIInPlayOffscreen();
    }

    private void Start()
    {
        SetupUIListeners();
    }

    private void OnDestroy()
    {
        if (nameInput          != null) nameInput.onValueChanged.RemoveAllListeners();
        if (multiPlayerButton  != null) multiPlayerButton.onClick.RemoveAllListeners();
        if (singlePlayerButton != null) singlePlayerButton.onClick.RemoveAllListeners();
    }

    // ═══════════════════════════════════════════════
    // SETUP
    // ═══════════════════════════════════════════════

    private void ValidateComponents()
    {
        if (nameInput          == null) Debug.LogError("[MenuView] nameInput not assigned!");
        if (statusText         == null) Debug.LogError("[MenuView] statusText not assigned!");
        if (multiPlayerButton  == null) Debug.LogError("[MenuView] multiPlayerButton not assigned!");
        if (singlePlayerButton == null) Debug.LogError("[MenuView] singlePlayerButton not assigned!");
        if (UIInPlay           == null) Debug.LogWarning("[MenuView] UIInPlay not assigned!");
    }

    private void SetupUIListeners()
    {
        if (nameInput          != null) nameInput.onValueChanged.AddListener(n => OnNameChanged?.Invoke(n));
        if (multiPlayerButton  != null) multiPlayerButton.onClick.AddListener(() => OnMultiPlayerButtonClicked?.Invoke());
        if (singlePlayerButton != null) singlePlayerButton.onClick.AddListener(() => OnSinglePlayerButtonClicked?.Invoke());
    }

    // ═══════════════════════════════════════════════
    // PUBLIC API — Player Name
    // ═══════════════════════════════════════════════

    public string GetPlayerName()            => nameInput != null ? nameInput.text : "";
    public void   SetPlayerName(string name) { if (nameInput != null) nameInput.text = name; }

    // ═══════════════════════════════════════════════
    // PUBLIC API — Status & Buttons
    // ═══════════════════════════════════════════════

    public void UpdateStatusText(string status)
    {
        if (statusText != null) statusText.text = status;
    }

    public void SetPlayButtonsInteractable(bool interactable)
    {
        if (multiPlayerButton  != null) multiPlayerButton.interactable  = interactable;
        if (singlePlayerButton != null) singlePlayerButton.interactable = interactable;
    }

    // ═══════════════════════════════════════════════
    // PUBLIC API — Reset (gọi khi LeaveGame)
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Reset toàn bộ view về trạng thái ban đầu.
    /// Được gọi bởi MenuController.ResetToMenuState().
    /// </summary>
    public void ResetView(string playerName = "")
    {
        // ✅ Re-enable cả 2 nút chọn chế độ chơi
        SetPlayButtonsInteractable(true);

        // ✅ Reset status text về mặc định
        UpdateStatusText("Chọn avatar và nhập tên để bắt đầu!");

        // ✅ Restore tên người chơi nếu có
        if (!string.IsNullOrEmpty(playerName))
            SetPlayerName(playerName);

        // ✅ Ẩn in-game UI
        HideInGameUI();

        Debug.Log("[MenuView] View reset to initial state ✅");
    }

    // ═══════════════════════════════════════════════
    // PUBLIC API — Avatar Animator
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Đặt Animator cho prefab avatar (gọi ngay, không cần async).
    /// </summary>
    public void SetAvatarAnimator(GameObject avatar, RuntimeAnimatorController animatorController)
    {
        if (avatar == null || animatorController == null) return;
        Animator animator = avatar.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.runtimeAnimatorController = animatorController;
            animator.SetFloat("Speed", 0f);
        }
    }

    // ═══════════════════════════════════════════════
    // IN-GAME UI
    // ═══════════════════════════════════════════════

    public void ShowInGameUI()
    {
        if (UIInPlay == null) return;
        UIInPlay.SetActive(true);
        SetRectPos(UIInPlay, Vector2.zero);
    }

    public void HideInGameUI()
    {
        if (UIInPlay == null) return;
        SetRectPos(UIInPlay, new Vector2(-3000f, 0f));
    }

    // ═══════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════

    private void MoveUIInPlayOffscreen() => SetRectPos(UIInPlay, new Vector2(-3000f, 0f));

    private void SetRectPos(GameObject go, Vector2 pos)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
    }
}