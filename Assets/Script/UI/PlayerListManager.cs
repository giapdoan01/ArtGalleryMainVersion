using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerListManager : MonoBehaviour
{
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Transform  container;

    [Header("Panel")]
    [SerializeField] private GameObject playerListPanel;
    [SerializeField] private Button     openPlayerListButton;
    [SerializeField] private Button     closePlayerListButton;

    private readonly Dictionary<string, GameObject> spawnedItems = new Dictionary<string, GameObject>();
    private bool _openedByButton;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (openPlayerListButton  != null) openPlayerListButton.onClick.AddListener(OpenPanel);
        if (closePlayerListButton != null) closePlayerListButton.onClick.AddListener(ClosePanel);
    }

    private void OnEnable()
    {
        // Ẩn panel khi vừa được bật (trừ khi chính OpenPanel đang gọi)
        if (!_openedByButton && playerListPanel != null)
            playerListPanel.SetActive(false);

        // Xóa list cũ của session trước rồi đăng ký + populate lại
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
        // Safety: unsubscribe nếu object bị destroy mà chưa qua OnDisable
        UnsubscribeFromNetwork();
    }

    // ════════════════════════════════════════════════
    // NETWORK SUBSCRIPTION
    // ════════════════════════════════════════════════

    private void SubscribeToNetwork()
    {
        if (NetworkManager.Instance == null) return;
        // -= trước để chắc chắn không đăng ký trùng
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

    private void OpenPanel()
    {
        if (playerListPanel == null) return;
        _openedByButton = true;
        playerListPanel.SetActive(true);
        _openedByButton = false;
    }

    private void ClosePanel()
    {
        if (playerListPanel != null) playerListPanel.SetActive(false);
    }
}
