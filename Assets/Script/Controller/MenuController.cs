// ═══════════════════════════════════════════════════════════════════
// MenuController.cs
// ═══════════════════════════════════════════════════════════════════
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MenuController : MonoBehaviour
{
    [SerializeField] private MenuView view;

    [Header("Avatar Prefabs")]
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

    // ✅ Mode đang được chọn
    private enum GameMode { None, MultiPlayer, SinglePlayer }
    private GameMode selectedMode = GameMode.None;

    public static MenuController Instance { get; private set; }

    public int        SelectedIndex   => currentIndex;
    public GameObject SelectedPreview => previewInstances.Count > currentIndex
        ? previewInstances[currentIndex] : null;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

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

        currentIndex = Mathf.Clamp(model.AvatarIndex, 0,
            previewInstances.Count > 0 ? previewInstances.Count - 1 : 0);

        ShowAvatarAtIndex(currentIndex);

        model.SetStatus("Chọn avatar và nhập tên để bắt đầu!");
        model.SetJoinButtonState(true);

        // ✅ Mặc định chọn SinglePlayer để khớp với UI mặc định trong MenuView
        selectedMode = GameMode.SinglePlayer;

        ShowMenuPanel();
    }

    private void OnDestroy()
    {
        UnbindEvents();
        foreach (var go in previewInstances)
            if (go != null) Destroy(go);
        previewInstances.Clear();
    }

    // ════════════════════════════════════════════════
    // EVENT BINDING
    // ════════════════════════════════════════════════

    private void BindEvents()
    {
        // ✅ Mode selection events (không vào game)
        view.OnMultiPlayerModeSelected  += HandleMultiPlayerModeSelected;
        view.OnSinglePlayerModeSelected += HandleSinglePlayerModeSelected;

        // ✅ Start button event (vào game)
        view.OnStartButtonClicked += HandleStartButtonClicked;

        // ✅ Avatar select button → mở panel
        view.OnAvatarSelectButtonClicked += HandleAvatarSelectButtonClicked;

        view.OnNameChanged += OnNameChanged;

        model.OnStatusChanged    += view.UpdateStatusText;
        model.OnJoinStateChanged += view.SetModeButtonsInteractable;

        if (nextAvatarButton     != null) nextAvatarButton.onClick.AddListener(ShowNext);
        if (previousAvatarButton != null) previousAvatarButton.onClick.AddListener(ShowPrevious);

        // ✅ Bind AvatarPanel events
        AvatarPanel panel = view.GetAvatarPanel();
        if (panel != null)
        {
            panel.OnAvatarConfirmed += HandleAvatarConfirmed;
            panel.OnAvatarPreview   += HandleAvatarPreview;
        }
    }

    private void UnbindEvents()
    {
        if (view != null)
        {
            view.OnMultiPlayerModeSelected   -= HandleMultiPlayerModeSelected;
            view.OnSinglePlayerModeSelected  -= HandleSinglePlayerModeSelected;
            view.OnStartButtonClicked        -= HandleStartButtonClicked;
            view.OnAvatarSelectButtonClicked -= HandleAvatarSelectButtonClicked;
            view.OnNameChanged               -= OnNameChanged;

            AvatarPanel panel = view.GetAvatarPanel();
            if (panel != null)
            {
                panel.OnAvatarConfirmed -= HandleAvatarConfirmed;
                panel.OnAvatarPreview   -= HandleAvatarPreview;
            }
        }

        if (model != null)
        {
            model.OnStatusChanged    -= view.UpdateStatusText;
            model.OnJoinStateChanged -= view.SetModeButtonsInteractable;
        }

        if (nextAvatarButton     != null) nextAvatarButton.onClick.RemoveListener(ShowNext);
        if (previousAvatarButton != null) previousAvatarButton.onClick.RemoveListener(ShowPrevious);
    }

    private void OnNameChanged(string name) => model.PlayerName = name;

    // ════════════════════════════════════════════════
    // AVATAR PANEL HANDLERS
    // ════════════════════════════════════════════════

    private void HandleAvatarSelectButtonClicked()
    {
        view.OpenAvatarPanel(currentIndex);
    }

    private void HandleAvatarPreview(int index)
    {
        ShowAvatarAtIndex(index);

        if (showDebug) Debug.Log($"[MenuController] Avatar preview: {index}");
    }

    private void HandleAvatarConfirmed(int index)
    {
        ShowAvatarAtIndex(index);

        if (showDebug) Debug.Log($"[MenuController] Avatar confirmed: {index}");
    }

    // ════════════════════════════════════════════════
    // MODE SELECTION HANDLERS
    // ════════════════════════════════════════════════

    /// <summary>Chỉ lưu mode — chưa vào game.</summary>
    private void HandleMultiPlayerModeSelected()
    {
        selectedMode = GameMode.MultiPlayer;
        model.SetStatus("Nhấn Start để bắt đầu!");

        if (showDebug) Debug.Log("[MenuController] Mode selected: MultiPlayer");
    }

    /// <summary>Chỉ lưu mode — chưa vào game.</summary>
    private void HandleSinglePlayerModeSelected()
    {
        selectedMode = GameMode.SinglePlayer;
        model.SetStatus("Nhấn Start để bắt đầu!");

        if (showDebug) Debug.Log("[MenuController] Mode selected: SinglePlayer");
    }

    // ════════════════════════════════════════════════
    // START BUTTON HANDLER
    // ════════════════════════════════════════════════

    /// <summary>Người chơi đã chọn mode xong và ấn Start → vào game.</summary>
    private void HandleStartButtonClicked()
    {
        if (selectedMode == GameMode.None)
        {
            model.SetStatus("Vui lòng chọn chế độ chơi trước!");
            view.SetStartButtonInteractable(true); // re-enable
            return;
        }

        string playerName = view.GetPlayerName();
        if (!model.IsValidPlayerName(playerName, out string err))
        {
            model.SetStatus(err);
            view.SetStartButtonInteractable(true); // re-enable
            return;
        }

        model.PlayerName  = playerName;
        model.AvatarIndex = currentIndex;
        model.SavePlayerData();

        if (showDebug) Debug.Log($"[MenuController] Start → mode={selectedMode}, name={playerName}, avatarIndex={currentIndex}");

        if (selectedMode == GameMode.MultiPlayer)
            StartMultiPlayer();
        else
            StartSinglePlayer();
    }

    // ════════════════════════════════════════════════
    // GAME START
    // ════════════════════════════════════════════════

    private void StartMultiPlayer()
    {
        model.SetJoinButtonState(false);
        model.SetStatus("Đang kết nối...");

        if (showDebug) Debug.Log($"[MenuController] Connecting multiplayer: {model.PlayerName}, avatarIndex={currentIndex}");

        NetworkManager.Instance.ConnectAndJoinRoom(
            model.PlayerName,
            model.AvatarIndex,
            OnConnectionResult
        );
    }

    private void StartSinglePlayer()
    {
        model.SetStatus("Bắt đầu chế độ chơi đơn...");

        if (showDebug) Debug.Log($"[MenuController] Starting single player: {model.PlayerName}, avatarIndex={currentIndex}");

        StartGame(isMultiplayer: false);
    }

    private void OnConnectionResult(bool success, string message)
    {
        if (!success)
        {
            model.SetStatus(message);
            model.SetJoinButtonState(true);
            view.SetStartButtonInteractable(true); // re-enable khi lỗi
            Debug.LogError($"[MenuController] Connection failed: {message}");
        }
        else
        {
            model.SetStatus("Kết nối thành công!");
            if (showDebug) Debug.Log($"[MenuController] Connected! avatarIndex={currentIndex}");
            StartGame(isMultiplayer: true);
        }
    }

    private void StartGame(bool isMultiplayer)
    {
        foreach (var go in previewInstances)
            PlaceAtUnselectPoint(go);

        HideMenuPanel();
        view.ShowInGameUI(isMultiplayer);

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
        else Debug.LogError("[MenuController] PlayerSpawner not found!");
    }

    // ════════════════════════════════════════════════
    // PREVIEW INSTANTIATION
    // ════════════════════════════════════════════════

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
            if (prefab == null) { previewInstances.Add(null); continue; }
            GameObject instance = Instantiate(prefab, hiddenPos, Quaternion.identity);
            instance.SetActive(true);
            previewInstances.Add(instance);
        }

        if (showDebug) Debug.Log($"[MenuController] {previewInstances.Count} avatar previews ready.");
    }

    // ════════════════════════════════════════════════
    // AVATAR NAVIGATION
    // ════════════════════════════════════════════════

    private void ShowAvatarAtIndex(int index)
    {
        if (previewInstances.Count == 0) return;

        if (currentIndex >= 0 && currentIndex < previewInstances.Count)
            PlaceAtUnselectPoint(previewInstances[currentIndex]);

        currentIndex      = Mathf.Clamp(index, 0, previewInstances.Count - 1);
        model.AvatarIndex = currentIndex;

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

    // ════════════════════════════════════════════════
    // AVATAR PLACEMENT
    // ════════════════════════════════════════════════

    private void PlaceAtSelectPoint(GameObject avatar)
    {
        if (avatar == null) return;
        if (avatarSelectSpawnPoint != null)
            avatar.transform.SetPositionAndRotation(avatarSelectSpawnPoint.position, avatarSelectSpawnPoint.rotation);
        else
            avatar.transform.SetPositionAndRotation(new Vector3(163f, -0.7f, -1235.94f), Quaternion.Euler(0f, -180f, 0f));
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

    // ════════════════════════════════════════════════
    // MENU PANEL
    // ════════════════════════════════════════════════

    public void ShowMenuPanel()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        ShowAvatarAtIndex(currentIndex);
    }

    public void HideMenuPanel()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    // ════════════════════════════════════════════════
    // RESET
    // ════════════════════════════════════════════════

    public void ResetToMenuState()
    {
        if (showDebug) Debug.Log("[MenuController] ResetToMenuState called");

        // ✅ Reset mode về None
        selectedMode = GameMode.None;

        model.SetJoinButtonState(true);
        model.SetStatus("Chọn avatar và nhập tên để bắt đầu!");
        view.SetPlayerName(model.PlayerName);
        view.ResetView(model.PlayerName); // ← ẩn Start button, snap indicator

        bool needReinit = false;
        foreach (var go in previewInstances)
            if (go == null) { needReinit = true; break; }

        if (needReinit)
        {
            foreach (var go in previewInstances)
                if (go != null) Destroy(go);
            previewInstances.Clear();
            InstantiateAllPreviews();
            currentIndex = Mathf.Clamp(currentIndex, 0,
                previewInstances.Count > 0 ? previewInstances.Count - 1 : 0);
        }

        ShowMenuPanel();
        if (showDebug) Debug.Log("[MenuController] Menu reset ✅");
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

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