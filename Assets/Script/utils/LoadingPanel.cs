using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// LoadingPanel - Hiển thị 5 giây rồi fade out mượt mà
/// </summary>
public class LoadingPanel : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float displayDuration = 5f; // Thời gian hiển thị
    [SerializeField] private float fadeOutDuration = 1f; // Thời gian fade out

    private CanvasGroup canvasGroup;
    
    // Thêm event để thông báo khi panel biến mất
    public event Action OnPanelHidden;

    private void Awake()
    {
        // Lấy hoặc tạo CanvasGroup
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Set alpha ban đầu
        canvasGroup.alpha = 1f;
    }

    private void OnEnable()
    {
        // Tự động bắt đầu khi enable
        StartCoroutine(AutoHideCoroutine());
    }

    /// <summary>
    /// Hiển thị 5 giây rồi fade out
    /// </summary>
    private IEnumerator AutoHideCoroutine()
    {
        // Reset alpha
        canvasGroup.alpha = 1f;

        // Đợi 5 giây
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        float elapsedTime = 0f;
        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeOutDuration);
            yield return null;
        }

        // Tắt panel
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        
        // Kích hoạt sự kiện khi panel biến mất
        OnPanelHidden?.Invoke();
    }
}