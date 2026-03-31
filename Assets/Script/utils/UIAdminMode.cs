using UnityEngine;

/// <summary>
/// Gắn component này vào bất kỳ GameObject nào trong scene.
/// Target sẽ tự động ẩn/hiện theo AdminMode.
/// </summary>
public class UIAdminMode : MonoBehaviour
{
    [Tooltip("GameObject sẽ ẩn khi Visitor, hiện khi Admin. Để trống = dùng GameObject hiện tại.")]
    [SerializeField] private GameObject target;

    private void OnEnable()
    {
        AdminModeManager.OnAdminModeChanged += ApplyAdminMode;
        ApplyAdminMode(AdminModeManager.Instance != null && AdminModeManager.Instance.IsAdmin);
    }

    private void OnDisable()
    {
        AdminModeManager.OnAdminModeChanged -= ApplyAdminMode;
    }

    private void ApplyAdminMode(bool isAdmin)
    {
        GameObject obj = target != null ? target : gameObject;
        obj.SetActive(isAdmin);
    }
}
