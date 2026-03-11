using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controller menu chọn avatar — dùng prefab index, không còn RPM async loading
/// </summary>
public class MenuController : MonoBehaviour
{
    [SerializeField] private MenuView view;

    [Header("Avatar Prefabs")]
    [Tooltip("Kéo các avatar prefab vào đây theo thứ tự index (0, 1, 2, ...)")]
    [SerializeField] private GameObject[] avatarPrefabs;

    [Header("Avatar Spawn Points")]
    [SerializeField] private Transform avatarSelectSpawnPoint;
    [SerializeField] private Transform avatarUnselectSpawnPoint;

    [Header("Avatar Navigation")]
    [SerializeField] private UnityEngine.UI.Button nextAvatarButton;
    [SerializeField] private UnityEngine.UI.Button previousAvatarButton;

    [Header("Game Settings")]
    [SerializeField] private PlayerSpawner playerSpawner;
    [SerializeField] private GameObject    menuPanel;
    [SerializeField] private LoadingPanel  loadingPanel;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ─── State ─────────────────────────────────────
    private MenuModel        model;
    private List<GameObject> previewInstances = new List<GameObject>();
    private int              currentIndex     = 0;

    public static MenuController Instance { get; private set; }

    public int        SelectedIndex   => currentIndex;
    public GameObject SelectedPreview => previewInstances.Count > currentIndex
        ? previewInstances[currentIndex] : null;

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (view == null) view = GetComponent<MenuView>();
        if (view == null) { Debug.LogError("[MenuController] MenuView not found!"); enabled = false; return; }

        if (playerSpawner == null)
        {
#pragma warning disable CS0618
            playerSpawner = FindObjectOfType<PlayerSpawner>();
#pragma warning restore CS0618
        }

        model = new MenuModel();
    }

    private void Start()
    {
        BindEvents();

        view.SetPlayerName(model.PlayerName);

        InstantiateAllPreviews();

        // ✅ FIX 1: Sync currentIndex với model trước khi ShowAvatarAtIndex
        //    Tránh trường hợp currentIndex = 0 nhưng model.AvatarIndex = 3
        currentIndex = Mathf.Clamp(model.AvatarIndex, 0,
            previewInstances.Count > 0 ? previewInstances.Count - 1 : 0);

        ShowAvatarAtIndex(currentIndex);

        model.SetStatus("Chọn avatar và nhập tên để bắt đầu!");
        model.SetJoinButtonState(true);

        ShowMenuPanel();
    }

    private void OnDestroy()
    {
        UnbindEvents();

        foreach (var go in previewInstances)
            if (go != null) Destroy(go);
        previewInstances.Clear();
    }

    // ═══════════════════════════════════════════════
    // EVENT BINDING
    // ═══════════════════════════════════════════════

    private void BindEvents()
    {
        view.OnNameChanged               += OnNameChanged;
        view.OnMultiPlayerButtonClicked  += HandleMultiPlayer;
        view.OnSinglePlayerButtonClicked += HandleSinglePlayer;

        model.OnStatusChanged    += view.UpdateStatusText;
        model.OnJoinStateChanged += view.SetPlayButtonsInteractable;

        if (nextAvatarButton     != null) nextAvatarButton.onClick.AddListener(ShowNext);
        if (previousAvatarButton != null) previousAvatarButton.onClick.AddListener(ShowPrevious);
    }

    private void UnbindEvents()
    {
        if (view != null)
        {
            view.OnNameChanged               -= OnNameChanged;
            view.OnMultiPlayerButtonClicked  -= HandleMultiPlayer;
            view.OnSinglePlayerButtonClicked -= HandleSinglePlayer;
        }

        if (model != null)
        {
            model.OnStatusChanged    -= view.UpdateStatusText;
            model.OnJoinStateChanged -= view.SetPlayButtonsInteractable;
        }

        if (nextAvatarButton     != null) nextAvatarButton.onClick.RemoveListener(ShowNext);
        if (previousAvatarButton != null) previousAvatarButton.onClick.RemoveListener(ShowPrevious);
    }

    private void OnNameChanged(string name) => model.PlayerName = name;

    // ═══════════════════════════════════════════════
    // PREVIEW INSTANTIATION
    // ═══════════════════════════════════════════════

    private void InstantiateAllPreviews()
    {
        previewInstances.Clear();

        if (avatarPrefabs == null || avatarPrefabs.Length == 0)
        {
            Debug.LogError("[MenuController] avatarPrefabs is empty!");
            return;
        }

        Vector3 hiddenPos = avatarUnselectSpawnPoint != null
            ? avatarUnselectSpawnPoint.position
            : new Vector3(99999f, 99999f, 99999f);

        foreach (var prefab in avatarPrefabs)
        {
            if (prefab == null)
            {
                previewInstances.Add(null);
                Debug.LogWarning("[MenuController] One avatar prefab slot is null!");
                continue;
            }

            GameObject instance = Instantiate(prefab, hiddenPos, Quaternion.identity);
            instance.SetActive(true);
            previewInstances.Add(instance);

            if (showDebug) Debug.Log($"[MenuController] Instantiated preview: {prefab.name}");
        }

        if (showDebug) Debug.Log($"[MenuController] {previewInstances.Count} avatar previews ready.");
    }

    // ═══════════════════════════════════════════════
    // AVATAR NAVIGATION
    // ═══════════════════════════════════════════════

    private void ShowAvatarAtIndex(int index)
    {
        if (previewInstances.Count == 0) return;

        // Ẩn avatar đang hiện trước
        if (currentIndex >= 0 && currentIndex < previewInstances.Count)
            PlaceAtUnselectPoint(previewInstances[currentIndex]);

        // Cập nhật index mới
        currentIndex      = Mathf.Clamp(index, 0, previewInstances.Count - 1);
        model.AvatarIndex = currentIndex; // ✅ Luôn sync model với currentIndex

        // Hiện avatar mới
        PlaceAtSelectPoint(previewInstances[currentIndex]);

        UpdateNavigationButtons();

        if (showDebug) Debug.Log($"[MenuController] Showing avatar [{currentIndex}]");
    }

    public void ShowNext()     => ShowAvatarAtIndex((currentIndex + 1) % previewInstances.Count);
    public void ShowPrevious() => ShowAvatarAtIndex((currentIndex - 1 + previewInstances.Count) % previewInstances.Count);

    private void UpdateNavigationButtons()
    {
        bool can = previewInstances.Count > 1;
        if (nextAvatarButton     != null) nextAvatarButton.interactable     = can;
        if (previousAvatarButton != null) previousAvatarButton.interactable = can;
    }

    // ═══════════════════════════════════════════════
    // AVATAR PLACEMENT
    // ═══════════════════════════════════════════════

    private void PlaceAtSelectPoint(GameObject avatar)
    {
        if (avatar == null) return;

        if (avatarSelectSpawnPoint != null)
        {
            avatar.transform.SetPositionAndRotation(
                avatarSelectSpawnPoint.position,
                avatarSelectSpawnPoint.rotation
            );
        }
        else
        {
            avatar.transform.SetPositionAndRotation(
                new Vector3(163f, -0.7f, -1235.94f),
                Quaternion.Euler(0f, -180f, 0f)
            );
            Debug.LogWarning("[MenuController] avatarSelectSpawnPoint not assigned, using fallback");
        }

        avatar.transform.localScale = Vector3.one;
        avatar.SetActive(true);
    }

    private void PlaceAtUnselectPoint(GameObject avatar)
    {
        if (avatar == null) return;

        avatar.transform.position = avatarUnselectSpawnPoint != null
            ? avatarUnselectSpawnPoint.position
            : new Vector3(99999f, 99999f, 99999f);
    }

    // ═══════════════════════════════════════════════
    // MENU PANEL
    // ═══════════════════════════════════════════════

    public void ShowMenuPanel()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        ShowAvatarAtIndex(currentIndex);
    }

    public void HideMenuPanel()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════
    // RESET — Gọi từ GameManager sau khi LeaveGame
    // ═══════════════════════════════════════════════

    public void ResetToMenuState()
    {
        if (showDebug) Debug.Log("[MenuController] ResetToMenuState called");

        model.SetJoinButtonState(true);
        model.SetStatus("Chọn avatar và nhập tên để bắt đầu!");
        view.SetPlayerName(model.PlayerName);

        bool needReinit = false;
        foreach (var go in previewInstances)
        {
            if (go == null) { needReinit = true; break; }
        }

        if (needReinit)
        {
            if (showDebug) Debug.Log("[MenuController] Re-instantiating destroyed previews...");
            foreach (var go in previewInstances)
                if (go != null) Destroy(go);
            previewInstances.Clear();

            InstantiateAllPreviews();
            currentIndex = Mathf.Clamp(currentIndex, 0,
                previewInstances.Count > 0 ? previewInstances.Count - 1 : 0);
        }

        ShowMenuPanel();

        if (showDebug) Debug.Log("[MenuController] Menu reset to initial state ✅");
    }

    // ═══════════════════════════════════════════════
    // HANDLERS
    // ═══════════════════════════════════════════════

    private void HandleMultiPlayer()
    {
        string playerName = view.GetPlayerName();

        if (!model.IsValidPlayerName(playerName, out string err))
        {
            model.SetStatus(err);
            return;
        }

        // ✅ FIX 2: Luôn sync model.AvatarIndex = currentIndex
        //    trước khi SavePlayerData() và ConnectAndJoinRoom()
        //    Đảm bảo không bao giờ gửi stale index lên server
        model.PlayerName  = playerName;
        model.AvatarIndex = currentIndex; // ← KEY FIX

        model.SetJoinButtonState(false);
        model.SetStatus("Đang kết nối...");
        model.SavePlayerData();

        if (showDebug) Debug.Log($"[MenuController] Multiplayer → name={playerName}, avatarIndex={currentIndex}");

        NetworkManager.Instance.ConnectAndJoinRoom(
            model.PlayerName,
            model.AvatarIndex,
            OnConnectionResult
        );
    }

    private void HandleSinglePlayer()
    {
        string playerName = view.GetPlayerName();

        if (!model.IsValidPlayerName(playerName, out string err))
        {
            model.SetStatus(err);
            return;
        }

        // ✅ FIX 2 (same): Sync model.AvatarIndex = currentIndex
        model.PlayerName  = playerName;
        model.AvatarIndex = currentIndex; // ← KEY FIX

        model.SetStatus("Bắt đầu chế độ chơi đơn...");
        model.SavePlayerData();

        if (showDebug) Debug.Log($"[MenuController] Single player → name={playerName}, avatarIndex={currentIndex}");

        StartGame(isMultiplayer: false);
    }

    private void OnConnectionResult(bool success, string message)
    {
        if (!success)
        {
            model.SetStatus(message);
            model.SetJoinButtonState(true);
            Debug.LogError($"[MenuController] Connection failed: {message}");
        }
        else
        {
            model.SetStatus("Kết nối thành công!");
            if (showDebug) Debug.Log($"[MenuController] Connected! avatarIndex={currentIndex}");
            StartGame(isMultiplayer: true);
        }
    }

    // ═══════════════════════════════════════════════
    // START GAME
    // ═══════════════════════════════════════════════

    private void StartGame(bool isMultiplayer)
    {
        foreach (var go in previewInstances)
            PlaceAtUnselectPoint(go);

        HideMenuPanel();
        view.ShowInGameUI();

        if (loadingPanel != null)
            loadingPanel.gameObject.SetActive(true);

        if (!isMultiplayer && playerSpawner != null)
        {
            Player localPlayer = new Player
            {
                username    = model.PlayerName,
                avatarIndex = model.AvatarIndex
            };

            StartCoroutine(SpawnAfterDelay(localPlayer));
        }
    }

    private IEnumerator SpawnAfterDelay(Player localPlayer)
    {
        yield return new WaitForSeconds(0.5f);

        if (playerSpawner != null)
        {
            if (showDebug) Debug.Log($"[MenuController] Spawning: {localPlayer.username}, index={localPlayer.avatarIndex}");
            playerSpawner.SpawnLocalPlayerSingleMode(localPlayer);
        }
        else
        {
            Debug.LogError("[MenuController] PlayerSpawner not found!");
        }
    }

    // ═══════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════

    public int          GetSelectedIndex() => currentIndex;
    public bool         IsReady()          => model.IsReady;
    public GameObject[] GetAvatarPrefabs() => avatarPrefabs;

    public HashSet<GameObject> GetPreviewInstanceSet()
    {
        var set = new HashSet<GameObject>();
        foreach (var go in previewInstances)
            if (go != null) set.Add(go);
        return set;
    }

    public List<GameObject> GetLoadedAvatars() => previewInstances;
}