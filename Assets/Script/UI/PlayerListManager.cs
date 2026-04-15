using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerListManager : MonoBehaviour
{
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Transform  container;

    [Header("Panel")]
    [SerializeField] private RectTransform playerListPanel;
    [SerializeField] private Button        openPlayerListButton;

    [Header("Slide Settings")]
    [SerializeField] private float slideDuration = 0.35f;
    [SerializeField] private float slideOffset   = 1000f;

    private readonly Dictionary<string, GameObject> spawnedItems = new Dictionary<string, GameObject>();

    private Vector2 _panelOriginPos;
    private bool    _isHidden      = false;   // false = panel đang ở vị trí ban đầu (hiển thị)
    private Coroutine _slideCoroutine;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        // Ghi nhớ vị trí gốc ngay trong Awake, trước cả OnEnable
        if (playerListPanel != null)
            _panelOriginPos = playerListPanel.anchoredPosition;

        if (openPlayerListButton != null)
            openPlayerListButton.onClick.AddListener(TogglePanel);

        // Panel và nút luôn bật mặc định
        if (playerListPanel != null)
            playerListPanel.gameObject.SetActive(true);

        if (openPlayerListButton != null)
            openPlayerListButton.gameObject.SetActive(true);

        // Đặt trạng thái icon nút về ban đầu (không flip)
        SetButtonFlip(false);
    }

    private void OnEnable()
    {
        ClearAll();
        SubscribeToNetwork();
        PopulateExistingPlayers();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetwork();
        ClearAll();
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetwork();
    }

    // ════════════════════════════════════════════════
    // NETWORK SUBSCRIPTION
    // ════════════════════════════════════════════════

    private void SubscribeToNetwork()
    {
        if (NetworkManager.Instance == null) return;
        NetworkManager.Instance.OnPlayerJoined -= OnPlayerJoined;
        NetworkManager.Instance.OnPlayerLeft   -= OnPlayerLeft;
        NetworkManager.Instance.OnPlayerJoined += OnPlayerJoined;
        NetworkManager.Instance.OnPlayerLeft   += OnPlayerLeft;
    }

    private void UnsubscribeFromNetwork()
    {
        if (NetworkManager.Instance == null) return;
        NetworkManager.Instance.OnPlayerJoined -= OnPlayerJoined;
        NetworkManager.Instance.OnPlayerLeft   -= OnPlayerLeft;
    }

    private void PopulateExistingPlayers()
    {
        if (NetworkManager.Instance == null) return;
        if (NetworkManager.Instance.State == null) return;
        if (NetworkManager.Instance.State.players == null) return;
        NetworkManager.Instance.State.players.ForEach((sessionId, player) =>
        {
            SpawnItem(sessionId, player.username);
        });
    }

    // ════════════════════════════════════════════════
    // EVENT HANDLERS
    // ════════════════════════════════════════════════

    private void OnPlayerJoined(string sessionId, Player player)
    {
        SpawnItem(sessionId, player.username);
    }

    private void OnPlayerLeft(string sessionId, Player player)
    {
        if (!spawnedItems.TryGetValue(sessionId, out GameObject item)) return;
        spawnedItems.Remove(sessionId);
        Destroy(item);
        ReorderItems();
    }

    // ════════════════════════════════════════════════
    // ITEMS
    // ════════════════════════════════════════════════

    private void SpawnItem(string sessionId, string playerName)
    {
        if (spawnedItems.ContainsKey(sessionId)) return;

        bool isLocal = NetworkManager.Instance != null && sessionId == NetworkManager.Instance.SessionId;

        GameObject go = Instantiate(playerListItemPrefab, container);
        go.GetComponent<PlayerListItem>().Setup(playerName, isLocal);
        spawnedItems[sessionId] = go;

        if (isLocal)
            go.transform.SetAsFirstSibling();
    }

    private void ReorderItems()
    {
        string localId = NetworkManager.Instance != null ? NetworkManager.Instance.SessionId : null;
        if (localId != null && spawnedItems.TryGetValue(localId, out GameObject localItem))
            localItem.transform.SetAsFirstSibling();
    }

    public void ClearAll()
    {
        foreach (var item in spawnedItems.Values)
            if (item != null) Destroy(item);
        spawnedItems.Clear();
    }

    // ════════════════════════════════════════════════
    // PANEL TOGGLE
    // ════════════════════════════════════════════════

    private void TogglePanel()
    {
        if (playerListPanel == null) return;

        if (_isHidden)
            SlidePanel(_panelOriginPos, flipButton: false);       // đưa về vị trí ban đầu
        else
            SlidePanel(_panelOriginPos + new Vector2(slideOffset, 0f), flipButton: true); // trượt sang phải

        _isHidden = !_isHidden;
    }

    private void SlidePanel(Vector2 target, bool flipButton)
    {
        if (_slideCoroutine != null)
            StopCoroutine(_slideCoroutine);

        _slideCoroutine = StartCoroutine(SlideTo(target));
        SetButtonFlip(flipButton);
    }

    private IEnumerator SlideTo(Vector2 target)
    {
        Vector2 start   = playerListPanel.anchoredPosition;
        float   elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / slideDuration);
            float e  = EaseInOut(t);
            playerListPanel.anchoredPosition = Vector2.LerpUnclamped(start, target, e);
            yield return null;
        }

        playerListPanel.anchoredPosition = target;
    }

    // Ease in-out cubic
    private static float EaseInOut(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    private void SetButtonFlip(bool flipped)
    {
        if (openPlayerListButton == null) return;
        openPlayerListButton.transform.localEulerAngles = flipped ? new Vector3(0f, 0f, 90f) : new Vector3(0f, 0f, -90f);
    }
}
