using System;
using UnityEngine;

/// <summary>
/// Singleton quản lý chế độ Admin / Visitor.
/// - Gọi SetAdminMode(true/false) từ code Unity.
/// - Gọi SetAdminModeFromWeb("true"/"false") từ index.html qua SendMessage.
/// - Subscribe OnAdminModeChanged để nhận thông báo khi mode thay đổi.
/// </summary>
public class AdminModeManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────
    public static AdminModeManager Instance { get; private set; }

    // ── Event ────────────────────────────────────────────────────────────
    /// <summary>
    /// Fired mỗi khi admin mode thay đổi.
    /// bool = true  → Admin mode
    /// bool = false → Visitor mode
    /// </summary>
    public static event Action<bool> OnAdminModeChanged;

    // ── State ────────────────────────────────────────────────────────────
    public bool IsAdmin { get; private set; } = false;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // PUBLIC API — Unity code
    // ════════════════════════════════════════════════════════════════════

    /// <summary>Bật / tắt admin mode và fire event cho tất cả subscriber.</summary>
    public void SetAdminMode(bool isAdmin)
    {
        if (IsAdmin == isAdmin) return;

        IsAdmin = isAdmin;

        if (showDebug)
            Debug.Log($"[AdminModeManager] Mode → {(isAdmin ? "ADMIN" : "VISITOR")}");

        OnAdminModeChanged?.Invoke(IsAdmin);
    }

    // ════════════════════════════════════════════════════════════════════
    // PUBLIC API — Gọi từ index.html qua SendMessage
    // GameObject name phải là "AdminModeManager" trong scene
    // unityInstance.SendMessage('AdminModeManager', 'SetAdminModeFromWeb', 'true')
    // ════════════════════════════════════════════════════════════════════

    /// <summary>Entry point cho JavaScript SendMessage. Nhận "true" hoặc "false".</summary>
    public void SetAdminModeFromWeb(string value)
    {
        bool isAdmin = value?.Trim().ToLower() == "true";
        SetAdminMode(isAdmin);
    }
}
