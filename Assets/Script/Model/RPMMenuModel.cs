using UnityEngine;
using System;

/// <summary>
/// Model cho menu chọn avatar — dùng prefab index thay vì RPM URL
/// </summary>
public class MenuModel
{
    // ✅ Số lượng avatar prefab (phải khớp với mảng avatarPrefabs trong MenuController)
    public const int AVATAR_COUNT = 4;

    // ─── Player Data ───────────────────────────────
    public string PlayerName  { get; set; }
    public int    AvatarIndex { get; set; }   // ✅ Index của prefab được chọn
    public bool   IsReady     { get; set; }   // Avatar đã sẵn sàng (prefab không cần load async)

    public RuntimeAnimatorController AnimatorController { get; set; }

    // ─── Events ────────────────────────────────────
    public event Action<string> OnStatusChanged;
    public event Action<bool>   OnJoinStateChanged;

    // ───────────────────────────────────────────────
    // CONSTRUCTOR
    // ───────────────────────────────────────────────

    public MenuModel(RuntimeAnimatorController animatorController = null)
    {
        AnimatorController = animatorController;

        PlayerName  = PlayerPrefs.GetString("PlayerName",  $"Player{UnityEngine.Random.Range(1000, 9999)}");
        AvatarIndex = PlayerPrefs.GetInt   ("AvatarIndex", 0);

        // Clamp phòng trường hợp PlayerPrefs lưu index cũ vượt quá số prefab
        AvatarIndex = Mathf.Clamp(AvatarIndex, 0, AVATAR_COUNT - 1);

        // ✅ Prefab luôn sẵn sàng ngay, không cần load async
        IsReady = true;

        Debug.Log($"[MenuModel] Init — Name: {PlayerName}, AvatarIndex: {AvatarIndex}");
    }

    // ───────────────────────────────────────────────
    // VALIDATION
    // ───────────────────────────────────────────────

    public bool IsValidPlayerName(string name, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errorMessage = "Vui lòng nhập tên!";
            return false;
        }
        if (name.Length < 3)
        {
            errorMessage = "Tên phải dài hơn 3 ký tự!";
            return false;
        }
        if (name.Length > 20)
        {
            errorMessage = "Tên quá dài! (tối đa 20 ký tự)";
            return false;
        }
        errorMessage = string.Empty;
        return true;
    }

    // ───────────────────────────────────────────────
    // SAVE / LOAD
    // ───────────────────────────────────────────────

    public void SavePlayerData()
    {
        PlayerPrefs.SetString("PlayerName",  PlayerName);
        PlayerPrefs.SetInt   ("AvatarIndex", AvatarIndex);
        PlayerPrefs.Save();
        Debug.Log($"[MenuModel] Saved — Name: {PlayerName}, AvatarIndex: {AvatarIndex}");
    }

    public void ClearSavedData()
    {
        PlayerPrefs.DeleteKey("PlayerName");
        PlayerPrefs.DeleteKey("AvatarIndex");
        PlayerPrefs.Save();
        PlayerName  = $"Player{UnityEngine.Random.Range(1000, 9999)}";
        AvatarIndex = 0;
        Debug.Log("[MenuModel] Saved data cleared");
    }

    // ───────────────────────────────────────────────
    // NOTIFY HELPERS
    // ───────────────────────────────────────────────

    public void SetStatus(string message)
    {
        OnStatusChanged?.Invoke(message);
        Debug.Log($"[MenuModel] Status: {message}");
    }

    public void SetJoinButtonState(bool enabled) => OnJoinStateChanged?.Invoke(enabled);
}