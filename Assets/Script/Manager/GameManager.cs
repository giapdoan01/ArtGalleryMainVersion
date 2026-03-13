using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    [Header("Settings")]
    [SerializeField] private float disconnectTimeout = 3f;

    [Header("Move Button Guide")]
    [SerializeField] private GameObject moveButtonGuide;
    [SerializeField] private float      moveButtonGuideDisplayTime = 10f;
    [SerializeField] private Button     closeMoveButtonGuideButton;
    [SerializeField] private Button     openMoveButtonGuideButton;

    [Header("Loading Panel")]
    [SerializeField] private LoadingPanel loadingPanel;

    // ─── State ─────────────────────────────────────
    private bool      isSinglePlayerMode   = false;
    private bool      isLeavingGame        = false;
    private bool      isManuallyOpened     = false;
    private Coroutine hideMoveGuideCoroutine;

    public static GameManager Instance { get; private set; }
    public event Action OnGameLeft;

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        RegisterMenuEvents();
    }

    private void Start()
    {
        SetupMoveButtonGuideButtons();
        SetupLoadingPanel();

        if (moveButtonGuide != null)
            moveButtonGuide.SetActive(false);
    }

    private void OnDestroy()
    {
        UnregisterMenuEvents();

        if (closeMoveButtonGuideButton != null)
            closeMoveButtonGuideButton.onClick.RemoveListener(CloseMoveButtonGuide);
        if (openMoveButtonGuideButton != null)
            openMoveButtonGuideButton.onClick.RemoveListener(OpenMoveButtonGuide);
        if (loadingPanel != null)
            loadingPanel.OnPanelHidden -= OnLoadingPanelHidden;
        if (hideMoveGuideCoroutine != null)
            StopCoroutine(hideMoveGuideCoroutine);
    }

    // ═══════════════════════════════════════════════
    // MENU EVENTS
    // ═══════════════════════════════════════════════

    private void RegisterMenuEvents()
    {
#pragma warning disable CS0618
        MenuView menuView = FindObjectOfType<MenuView>();
#pragma warning restore CS0618

        if (menuView != null)
        {
            menuView.OnSinglePlayerButtonClicked += HandleSinglePlayerMode;
            menuView.OnMultiPlayerButtonClicked  += HandleMultiPlayerMode;
            if (showDebug) Debug.Log("[GameManager] Registered MenuView events");
        }
        else Debug.LogWarning("[GameManager] MenuView not found!");
    }

    private void UnregisterMenuEvents()
    {
#pragma warning disable CS0618
        MenuView menuView = FindObjectOfType<MenuView>();
#pragma warning restore CS0618

        if (menuView != null)
        {
            menuView.OnSinglePlayerButtonClicked -= HandleSinglePlayerMode;
            menuView.OnMultiPlayerButtonClicked  -= HandleMultiPlayerMode;
        }
    }

    // ═══════════════════════════════════════════════
    // GAME MODE
    // ═══════════════════════════════════════════════

    private void HandleSinglePlayerMode()
    {
        isSinglePlayerMode = true;
        SetChatButtonVisible(false);
        if (showDebug) Debug.Log("[GameManager] Single Player mode");
    }

    private void HandleMultiPlayerMode()
    {
        isSinglePlayerMode = false;
        SetChatButtonVisible(true);
        if (showDebug) Debug.Log("[GameManager] Multi Player mode");
    }

    private void SetChatButtonVisible(bool visible)
    {
#pragma warning disable CS0618
        PanelListItemVisitor panelVisitor = FindObjectOfType<PanelListItemVisitor>();
#pragma warning restore CS0618

        if (panelVisitor != null) panelVisitor.SetChatButtonVisible(visible);
        else if (showDebug) Debug.LogWarning("[GameManager] PanelListItemVisitor not found");
    }

    public bool IsSinglePlayerMode() => isSinglePlayerMode;

    // ═══════════════════════════════════════════════
    // LOADING PANEL
    // ═══════════════════════════════════════════════

    private void SetupLoadingPanel()
    {
        if (loadingPanel != null)
        {
            loadingPanel.OnPanelHidden += OnLoadingPanelHidden;
            if (showDebug) Debug.Log("[GameManager] LoadingPanel registered");
        }
        else
        {
            Debug.LogWarning("[GameManager] LoadingPanel not assigned, starting guide timer immediately");
            StartMoveButtonGuideTimer();
        }
    }

    private void OnLoadingPanelHidden()
    {
        if (showDebug) Debug.Log("[GameManager] LoadingPanel hidden → show move guide");
        StartMoveButtonGuideTimer();
    }

    // ═══════════════════════════════════════════════
    // MOVE BUTTON GUIDE
    // ═══════════════════════════════════════════════

    private void SetupMoveButtonGuideButtons()
    {
        if (closeMoveButtonGuideButton != null)
            closeMoveButtonGuideButton.onClick.AddListener(CloseMoveButtonGuide);
        else
            Debug.LogWarning("[GameManager] closeMoveButtonGuideButton not assigned!");

        if (openMoveButtonGuideButton != null)
            openMoveButtonGuideButton.onClick.AddListener(OpenMoveButtonGuide);
        else
            Debug.LogWarning("[GameManager] openMoveButtonGuideButton not assigned!");
    }

    private void StartMoveButtonGuideTimer()
    {
        if (moveButtonGuide == null) return;

        isManuallyOpened = false;
        moveButtonGuide.SetActive(true);

        if (hideMoveGuideCoroutine != null) StopCoroutine(hideMoveGuideCoroutine);
        hideMoveGuideCoroutine = StartCoroutine(HideMoveGuideAfterDelay());

        if (showDebug) Debug.Log($"[GameManager] Move guide auto-show ({moveButtonGuideDisplayTime}s)");
    }

    private IEnumerator HideMoveGuideAfterDelay()
    {
        yield return new WaitForSeconds(moveButtonGuideDisplayTime);

        if (!isManuallyOpened)
        {
            if (moveButtonGuide != null) moveButtonGuide.SetActive(false);
            if (showDebug) Debug.Log("[GameManager] Move guide auto-hidden");
        }
        else
        {
            if (showDebug) Debug.Log("[GameManager] Auto-hide skipped (manually opened)");
        }

        hideMoveGuideCoroutine = null;
    }

    public void CloseMoveButtonGuide()
    {
        if (moveButtonGuide == null) return;

        isManuallyOpened = false;

        if (hideMoveGuideCoroutine != null)
        {
            StopCoroutine(hideMoveGuideCoroutine);
            hideMoveGuideCoroutine = null;
        }

        moveButtonGuide.SetActive(false);
        if (showDebug) Debug.Log("[GameManager] Move guide closed manually");
    }

    public void OpenMoveButtonGuide()
    {
        if (moveButtonGuide == null) return;

        isManuallyOpened = true;

        if (hideMoveGuideCoroutine != null)
        {
            StopCoroutine(hideMoveGuideCoroutine);
            hideMoveGuideCoroutine = null;
        }

        moveButtonGuide.SetActive(true);
        if (showDebug) Debug.Log("[GameManager] Move guide opened manually (stays until closed)");
    }

    public void ToggleMoveButtonGuide()
    {
        if (moveButtonGuide == null) return;
        if (moveButtonGuide.activeSelf) CloseMoveButtonGuide();
        else OpenMoveButtonGuide();
    }

    public bool IsMoveButtonGuideVisible() => moveButtonGuide != null && moveButtonGuide.activeSelf;

    // ═══════════════════════════════════════════════
    // LEAVE GAME
    // ═══════════════════════════════════════════════

    public void OnLeaveButtonClicked() => LeaveGame();

    public void LeaveGame()
    {
        if (isLeavingGame) { if (showDebug) Debug.Log("[GameManager] Already leaving..."); return; }
        isLeavingGame = true;
        StartCoroutine(LeaveGameCoroutine());
    }

    private IEnumerator LeaveGameCoroutine()
    {
        if (showDebug) Debug.Log("[GameManager] LeaveGame started");

        if (moveButtonGuide != null && moveButtonGuide.activeSelf)
            moveButtonGuide.SetActive(false);

        if (hideMoveGuideCoroutine != null)
        {
            StopCoroutine(hideMoveGuideCoroutine);
            hideMoveGuideCoroutine = null;
        }

        isManuallyOpened = false;

        // ✅ Detach MiniCamera TRƯỚC khi destroy player
        DetachFollowMapCamera();

        // ─── Disconnect NetworkManager ───────────────
        NetworkManager nm = NetworkManager.Instance;

        if (nm != null && nm.IsConnected)
        {
            string sessionId = nm.SessionId;
            if (!string.IsNullOrEmpty(sessionId))
                nm.MarkPlayerRemoved(sessionId);

            bool   disconnected     = false;
            Action onDisconnectOnce = null;
            onDisconnectOnce = () =>
            {
                disconnected = true;
                nm.OnDisconnected -= onDisconnectOnce;
            };
            nm.OnDisconnected += onDisconnectOnce;

            nm.Disconnect();

            float t = Time.time;
            while (!disconnected && Time.time - t < disconnectTimeout)
                yield return null;

            if (!disconnected)
            {
                Debug.LogWarning("[GameManager] Disconnect timeout!");
                nm.OnDisconnected -= onDisconnectOnce;
            }
            else if (showDebug) Debug.Log("[GameManager] Disconnected successfully");

            ClearPlayerSession();
        }

        DestroyAllPlayerObjects();

        // ✅ Clear remote players (không có tag "Player") qua PlayerSpawner
#pragma warning disable CS0618
        PlayerSpawner spawner = FindObjectOfType<PlayerSpawner>();
#pragma warning restore CS0618
        if (spawner != null) spawner.ClearAllPlayers();

        OnGameLeft?.Invoke();
        yield return null;

#pragma warning disable CS0618
        MenuView menuView = FindObjectOfType<MenuView>();
#pragma warning restore CS0618
        menuView?.HideInGameUI();

        if (MenuController.Instance != null)
        {
            MenuController.Instance.ResetToMenuState();
            if (showDebug) Debug.Log("[GameManager] Menu reset and shown");
        }
        else Debug.LogWarning("[GameManager] MenuController.Instance not found!");

        isLeavingGame = false;
        if (showDebug) Debug.Log("[GameManager] LeaveGame complete");
    }

    // ═══════════════════════════════════════════════
    // FOLLOW MAP CAMERA — DETACH ON LEAVE
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Detach MiniCamera khỏi Player trước khi destroy.
    /// Reset về position/rotation như trong Inspector (ảnh: pos -0.27, 7.17, -4.2 | rot 45, 0, 0).
    /// </summary>
    private void DetachFollowMapCamera()
    {
#pragma warning disable CS0618
        PlayerSpawner spawner = FindObjectOfType<PlayerSpawner>();
#pragma warning restore CS0618

        if (spawner == null)
        {
            if (showDebug) Debug.LogWarning("[GameManager] PlayerSpawner not found, cannot detach MiniCamera");
            return;
        }

        GameObject miniCam = spawner.GetFollowMapCamera();
        if (miniCam == null)
        {
            if (showDebug) Debug.LogWarning("[GameManager] FollowMapCamera is null, skip detach");
            return;
        }

        // ✅ Detach khỏi Player — về scene root
        miniCam.transform.SetParent(null, true);

        // ✅ Reset về vị trí mặc định (theo ảnh Inspector)
        miniCam.transform.position    = new Vector3(-0.27f, 7.17f, -4.2f);
        miniCam.transform.eulerAngles = new Vector3(45f, 0f, 0f);

        if (showDebug) Debug.Log("[GameManager] FollowMapCamera detached & reset to default position ✅");
    }

    // ═══════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════

    private void DestroyAllPlayerObjects()
    {
        if (showDebug) Debug.Log("[GameManager] Destroying player objects...");

        HashSet<GameObject> safeSet = MenuController.Instance != null
            ? MenuController.Instance.GetPreviewInstanceSet()
            : new HashSet<GameObject>();

        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        int count = 0;

        foreach (var obj in playerObjects)
        {
            if (obj == null) continue;
            if (safeSet.Contains(obj)) { if (showDebug) Debug.Log($"[GameManager] Skip preview: {obj.name}"); continue; }
            if (showDebug) Debug.Log($"[GameManager] Destroy: {obj.name}");
            Destroy(obj);
            count++;
        }

        if (showDebug) Debug.Log($"[GameManager] Destroyed {count} player object(s)");
    }

    private void ClearPlayerSession()
    {
        PlayerPrefs.DeleteKey("CurrentSessionId");
        PlayerPrefs.DeleteKey("LastPosition");
        PlayerPrefs.Save();
        if (showDebug) Debug.Log("[GameManager] Session data cleared");
    }
}