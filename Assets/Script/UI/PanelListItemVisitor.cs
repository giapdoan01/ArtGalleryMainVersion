using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PanelListItemVisitor : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button displayListItemButton;
    [SerializeField] private Button hideListItemButton;
    [SerializeField] private Button openPaintingPanelButton;
    [SerializeField] private Button openModel3DPanelButton;
    [SerializeField] private Button openChatButton;
    [SerializeField] private Button closeChatButton;
    [SerializeField] private Button openMinimapPanelBtn;
    [SerializeField] private Button closeMinimapPanelBtn;

    [Header("Panels")]
    [SerializeField] private GameObject ListItemPanel;
    [SerializeField] private GameObject PaintingPanel;
    [SerializeField] private GameObject Model3DPanel;
    [SerializeField] private GameObject ChatPanel;
    [SerializeField] private GameObject MinimapPanel;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("displayListItemButton Positions")]
    [SerializeField] private Vector3 displayListItemButtonShowPosition = Vector3.zero;
    [SerializeField] private Vector3 displayListItemButtonHidePosition = new Vector3(-300, 0, 0);

    [Header("openChatButton Positions")]
    [SerializeField] private Vector3 openChatButtonShowPosition = Vector3.zero;
    [SerializeField] private Vector3 openChatButtonHidePosition = new Vector3(-300, 0, 0);

    [Header("ListItem Positions")]
    [SerializeField] private Vector3 listItemShowPosition = Vector3.zero;
    [SerializeField] private Vector3 listItemHidePosition = new Vector3(0, -400, 0);

    [Header("Chat Positions")]
    [SerializeField] private Vector3 chatPanelShowPosition = new Vector3(-652, -347, 0);
    [SerializeField] private Vector3 chatPanelHidePosition = new Vector3(-1253, -717, 0);

    [Header("Minimap Positions")]
    [SerializeField] private Vector3 minimapPanelShowPosition   = Vector3.zero;
    [SerializeField] private Vector3 minimapPanelHidePosition   = new Vector3(300, 0, 0);
    [SerializeField] private Vector3 openMinimapBtnShowPosition = Vector3.zero;
    [SerializeField] private Vector3 openMinimapBtnHidePosition = new Vector3(300, 0, 0);

    [Header("Panel Integration")]
    [SerializeField] private PaintingInfo paintingInfo;
    [SerializeField] private Model3DInfo  model3DInfo;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ── State ────────────────────────────────────────────────────────────
    private bool isListItemVisible           = false;
    private bool isChatVisible               = false;
    private bool isOpenChatBtnVisible        = false;
    private bool isDisplayListItemBtnVisible = false;
    private bool isMinimapVisible            = false;
    private bool isOpenMinimapBtnVisible     = false;

    private bool paintingInfoIsOpen = false;
    private bool model3DInfoIsOpen  = false;

    // State lưu trước khi mở ListItem (để restore khi hideListItem)
    private bool wasChatVisibleBeforeListItem           = false;
    private bool wasOpenChatBtnVisibleBeforeListItem    = false;
    private bool wasMinimapVisibleBeforeListItem        = false;
    private bool wasOpenMinimapBtnVisibleBeforeListItem = false;

    // ── RectTransforms ───────────────────────────────────────────────────
    private RectTransform listItemRT;
    private RectTransform chatPanelRT;
    private RectTransform minimapPanelRT;
    private RectTransform displayListItemBtnRT;
    private RectTransform openChatBtnRT;
    private RectTransform openMinimapBtnRT;

    // ── CanvasGroups ─────────────────────────────────────────────────────
    private CanvasGroup paintingPanelCG;
    private CanvasGroup model3DPanelCG;

    // ── Animation counter ────────────────────────────────────────────────
    private int runningAnimations = 0;

    // ═══════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        listItemRT           = GetRT(ListItemPanel,                    "ListItemPanel");
        chatPanelRT          = GetRT(ChatPanel,                        "ChatPanel");
        minimapPanelRT       = GetRT(MinimapPanel,                     "MinimapPanel");
        displayListItemBtnRT = GetRT(displayListItemButton?.gameObject, "displayListItemButton");
        openChatBtnRT        = GetRT(openChatButton?.gameObject,        "openChatButton");
        openMinimapBtnRT     = GetRT(openMinimapPanelBtn?.gameObject,   "openMinimapPanelBtn");

        paintingPanelCG = GetOrAddCG(PaintingPanel, "PaintingPanel");
        model3DPanelCG  = GetOrAddCG(Model3DPanel,  "Model3DPanel");
    }

    private void Start()
    {
        SetupButtons();
        InitializePanels();
        CheckGameMode();
    }

    private void OnDestroy()
    {
        if (displayListItemButton   != null) displayListItemButton.onClick.RemoveListener(ShowListItemPanel);
        if (hideListItemButton      != null) hideListItemButton.onClick.RemoveListener(HideListItemPanel);
        if (openPaintingPanelButton != null) openPaintingPanelButton.onClick.RemoveListener(ShowPaintingPanel);
        if (openModel3DPanelButton  != null) openModel3DPanelButton.onClick.RemoveListener(ShowModel3DPanel);
        if (openChatButton          != null) openChatButton.onClick.RemoveListener(OpenChat);
        if (closeChatButton         != null) closeChatButton.onClick.RemoveListener(CloseChat);
        if (openMinimapPanelBtn     != null) openMinimapPanelBtn.onClick.RemoveListener(OpenMinimap);
        if (closeMinimapPanelBtn    != null) closeMinimapPanelBtn.onClick.RemoveListener(CloseMinimap);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SETUP
    // ═══════════════════════════════════════════════════════════════════

    private void SetupButtons()
    {
        AddListener(displayListItemButton,   "displayListItemButton",   ShowListItemPanel);
        AddListener(hideListItemButton,      "hideListItemButton",      HideListItemPanel);
        AddListener(openPaintingPanelButton, "openPaintingPanelButton", ShowPaintingPanel);
        AddListener(openModel3DPanelButton,  "openModel3DPanelButton",  ShowModel3DPanel);
        AddListener(openChatButton,          "openChatButton",          OpenChat);
        AddListener(closeChatButton,         "closeChatButton",         CloseChat);
        AddListener(openMinimapPanelBtn,     "openMinimapPanelBtn",     OpenMinimap);
        AddListener(closeMinimapPanelBtn,    "closeMinimapPanelBtn",    CloseMinimap);
    }

    private void AddListener(Button btn, string name, UnityEngine.Events.UnityAction action)
    {
        if (btn != null) { btn.onClick.RemoveListener(action); btn.onClick.AddListener(action); }
        else if (showDebug) Debug.LogWarning($"[PanelListItemVisitor] {name} is not assigned!");
    }

    private void InitializePanels()
    {
        // ── Content panels → HIDE ────────────────────────────────────────
        SetPositionImmediate(listItemRT,  listItemHidePosition);
        SetPositionImmediate(chatPanelRT, chatPanelHidePosition);
        isListItemVisible = false;
        isChatVisible     = false;

        // ── HUD buttons → SHOW ───────────────────────────────────────────
        SetPositionImmediate(displayListItemBtnRT, displayListItemButtonShowPosition);
        SetPositionImmediate(openChatBtnRT,        openChatButtonShowPosition);
        isDisplayListItemBtnVisible = true;
        isOpenChatBtnVisible        = true;

        // ── Minimap → SHOW, openMinimapBtn → HIDE ────────────────────────
        SetPositionImmediate(minimapPanelRT,   minimapPanelShowPosition);
        SetPositionImmediate(openMinimapBtnRT, openMinimapBtnHidePosition);
        isMinimapVisible        = true;
        isOpenMinimapBtnVisible = false;

        // ── Painting panel mặc định hiện ────────────────────────────────
        ShowPaintingPanel();
    }

    // ═══════════════════════════════════════════════════════════════════
    // PAINTING / MODEL3D PANEL (CanvasGroup)
    // ═══════════════════════════════════════════════════════════════════

    private void SetCG(CanvasGroup cg, bool visible)
    {
        if (cg == null) return;
        cg.alpha          = visible ? 1f : 0f;
        cg.interactable   = visible;
        cg.blocksRaycasts = visible;
    }

    private void ShowPaintingPanel() { SetCG(paintingPanelCG, true);  SetCG(model3DPanelCG, false); }
    private void ShowModel3DPanel()  { SetCG(paintingPanelCG, false); SetCG(model3DPanelCG, true);  }

    // ═══════════════════════════════════════════════════════════════════
    // LUỒNG 1 — CHAT
    // ═══════════════════════════════════════════════════════════════════

    private void OpenChat()
    {
        if (isChatVisible) return;

        SetOpenChatBtnVisible(false);
        SetDisplayListItemBtnVisible(false);
        isChatVisible = true;
        AnimateTo(chatPanelRT, chatPanelShowPosition);

        if (showDebug) Debug.Log("[PanelListItemVisitor] Chat → SHOW");
    }

    private void CloseChat()
    {
        if (!isChatVisible) return;

        isChatVisible = false;
        AnimateTo(chatPanelRT, chatPanelHidePosition);
        SetOpenChatBtnVisible(true);
        SetDisplayListItemBtnVisible(true);

        if (showDebug) Debug.Log("[PanelListItemVisitor] Chat → HIDE");
    }

    // ═══════════════════════════════════════════════════════════════════
    // LUỒNG MINIMAP
    // ═══════════════════════════════════════════════════════════════════

    private void OpenMinimap()
    {
        if (isMinimapVisible) return;
        SetMinimapVisible(true);
        if (showDebug) Debug.Log("[PanelListItemVisitor] Minimap → SHOW");
    }

    private void CloseMinimap()
    {
        if (!isMinimapVisible) return;
        SetMinimapVisible(false);
        if (showDebug) Debug.Log("[PanelListItemVisitor] Minimap → HIDE");
    }

    // ═══════════════════════════════════════════════════════════════════
    // LUỒNG 2 — LIST ITEM
    // ═══════════════════════════════════════════════════════════════════

    public void ShowListItemPanel()
    {
        if (isListItemVisible) return;

        // ── Lưu trạng thái trước khi ẩn ────────────────────────────────
        wasChatVisibleBeforeListItem           = isChatVisible;
        wasOpenChatBtnVisibleBeforeListItem    = isOpenChatBtnVisible;
        wasMinimapVisibleBeforeListItem        = isMinimapVisible;
        wasOpenMinimapBtnVisibleBeforeListItem = isOpenMinimapBtnVisible;

        // ── Ẩn Chat nếu đang show ────────────────────────────────────────
        if (isChatVisible)
        {
            isChatVisible = false;
            AnimateTo(chatPanelRT, chatPanelHidePosition);
        }

        // ── Ẩn tất cả HUD ────────────────────────────────────────────────
        SetOpenChatBtnVisible(false);
        SetDisplayListItemBtnVisible(false);

        // Ẩn cả MinimapPanel lẫn openMinimapBtn — không toggle, ẩn hết
        ForceHideMinimapAll();

        // ── Show ListItemPanel ────────────────────────────────────────────
        isListItemVisible = true;
        AnimateTo(listItemRT, listItemShowPosition);

        if (showDebug) Debug.Log("[PanelListItemVisitor] ListItem → SHOW");
    }

    public void HideListItemPanel()
    {
        if (!isListItemVisible) return;

        // ── Ẩn ListItemPanel ─────────────────────────────────────────────
        isListItemVisible = false;
        AnimateTo(listItemRT, listItemHidePosition);

        // ── Restore Chat / openChatButton ────────────────────────────────
        if (wasChatVisibleBeforeListItem)
        {
            isChatVisible = true;
            AnimateTo(chatPanelRT, chatPanelShowPosition);
            SetOpenChatBtnVisible(false);
        }
        else
        {
            SetOpenChatBtnVisible(wasOpenChatBtnVisibleBeforeListItem);
        }

        // ── Restore displayListItemButton ────────────────────────────────
        SetDisplayListItemBtnVisible(true);

        // Restore Minimap về đúng trạng thái trước khi mở ListItem
        RestoreMinimapState(wasMinimapVisibleBeforeListItem, wasOpenMinimapBtnVisibleBeforeListItem);

        if (showDebug) Debug.Log("[PanelListItemVisitor] ListItem → HIDE (restored)");
    }

    // ═══════════════════════════════════════════════════════════════════
    // LUỒNG 3 — PAINTING INFO
    // ═══════════════════════════════════════════════════════════════════

    public void HideForPaintingInfo()
    {
        if (paintingInfoIsOpen) return;
        paintingInfoIsOpen = true;

        HideContentPanels();
        ShowHUDButtons();

        if (showDebug) Debug.Log("[PanelListItemVisitor] HideForPaintingInfo");
    }

    public void RestoreAfterPaintingInfo()
    {
        if (!paintingInfoIsOpen) return;
        paintingInfoIsOpen = false;
        if (showDebug) Debug.Log("[PanelListItemVisitor] RestoreAfterPaintingInfo — HUD stays visible");
    }

    // ═══════════════════════════════════════════════════════════════════
    // LUỒNG 3 — MODEL3D INFO
    // ═══════════════════════════════════════════════════════════════════

    public void HideForModel3DInfo()
    {
        if (model3DInfoIsOpen) return;
        model3DInfoIsOpen = true;

        HideContentPanels();
        ShowHUDButtons();

        if (showDebug) Debug.Log("[PanelListItemVisitor] HideForModel3DInfo");
    }

    public void RestoreAfterModel3DInfo()
    {
        if (!model3DInfoIsOpen) return;
        model3DInfoIsOpen = false;
        if (showDebug) Debug.Log("[PanelListItemVisitor] RestoreAfterModel3DInfo — HUD stays visible");
    }

    // ═══════════════════════════════════════════════════════════════════
    // SHARED HELPERS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ẩn ListItemPanel và ChatPanel.
    /// ✅ Nếu ListItem đang mở → restore Minimap về state đã lưu trước đó,
    ///    vì ListItem là thứ đã force-hide Minimap.
    /// </summary>
    private void HideContentPanels()
    {
        if (isListItemVisible)
        {
            isListItemVisible = false;
            AnimateTo(listItemRT, listItemHidePosition);

            // ✅ ListItem đang ẩn Minimap → restore về state trước khi ListItem mở
            RestoreMinimapState(wasMinimapVisibleBeforeListItem, wasOpenMinimapBtnVisibleBeforeListItem);
        }

        if (isChatVisible)
        {
            isChatVisible = false;
            AnimateTo(chatPanelRT, chatPanelHidePosition);
        }
    }

    /// <summary>
    /// Đưa openChatButton và displayListItemButton về show.
    /// Minimap KHÔNG bị động — giữ nguyên trạng thái hiện tại.
    /// </summary>
    private void ShowHUDButtons()
    {
        SetOpenChatBtnVisible(true);
        SetDisplayListItemBtnVisible(true);
        // KHÔNG gọi SetMinimapVisible — Minimap tự quản lý riêng
    }

    /// <summary>Ẩn cả MinimapPanel lẫn openMinimapBtn — dùng khi mở ListItem.</summary>
    private void ForceHideMinimapAll()
    {
        if (isMinimapVisible)
        {
            isMinimapVisible = false;
            AnimateTo(minimapPanelRT, minimapPanelHidePosition);
        }
        if (isOpenMinimapBtnVisible)
        {
            isOpenMinimapBtnVisible = false;
            AnimateTo(openMinimapBtnRT, openMinimapBtnHidePosition);
        }
    }

    /// <summary>Restore Minimap về đúng trạng thái đã lưu.</summary>
    private void RestoreMinimapState(bool minimapWasVisible, bool openBtnWasVisible)
    {
        if (isMinimapVisible != minimapWasVisible)
        {
            isMinimapVisible = minimapWasVisible;
            AnimateTo(minimapPanelRT, minimapWasVisible ? minimapPanelShowPosition : minimapPanelHidePosition);
        }
        if (isOpenMinimapBtnVisible != openBtnWasVisible)
        {
            isOpenMinimapBtnVisible = openBtnWasVisible;
            AnimateTo(openMinimapBtnRT, openBtnWasVisible ? openMinimapBtnShowPosition : openMinimapBtnHidePosition);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // STATE SETTERS (animate + track)
    // ═══════════════════════════════════════════════════════════════════

    private void SetOpenChatBtnVisible(bool show)
    {
        if (isOpenChatBtnVisible == show) return;
        isOpenChatBtnVisible = show;
        AnimateTo(openChatBtnRT, show ? openChatButtonShowPosition : openChatButtonHidePosition);
    }

    private void SetDisplayListItemBtnVisible(bool show)
    {
        if (isDisplayListItemBtnVisible == show) return;
        isDisplayListItemBtnVisible = show;
        AnimateTo(displayListItemBtnRT, show ? displayListItemButtonShowPosition : displayListItemButtonHidePosition);
    }

    /// <summary>Minimap show → openMinimapBtn hide (và ngược lại).</summary>
    private void SetMinimapVisible(bool show)
    {
        if (isMinimapVisible == show) return;
        isMinimapVisible = show;
        AnimateTo(minimapPanelRT, show ? minimapPanelShowPosition : minimapPanelHidePosition);
        SetOpenMinimapBtnVisible(!show);
    }

    private void SetOpenMinimapBtnVisible(bool show)
    {
        if (isOpenMinimapBtnVisible == show) return;
        isOpenMinimapBtnVisible = show;
        AnimateTo(openMinimapBtnRT, show ? openMinimapBtnShowPosition : openMinimapBtnHidePosition);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ANIMATION
    // ═══════════════════════════════════════════════════════════════════

    private void AnimateTo(RectTransform rt, Vector3 targetPos)
    {
        if (rt == null) return;
        StartCoroutine(AnimatePanel(rt, targetPos));
    }

    private void SetPositionImmediate(RectTransform rt, Vector3 pos)
    {
        if (rt != null) rt.anchoredPosition = pos;
    }

    private IEnumerator AnimatePanel(RectTransform rt, Vector3 targetPos)
    {
        runningAnimations++;
        Vector3 startPos = rt.anchoredPosition;
        float elapsed    = 0f;

        while (elapsed < animationDuration)
        {
            elapsed            += Time.deltaTime;
            float t             = easeCurve.Evaluate(Mathf.Clamp01(elapsed / animationDuration));
            rt.anchoredPosition = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        rt.anchoredPosition = targetPos;
        runningAnimations--;

        if (showDebug) Debug.Log($"[PanelListItemVisitor] '{rt.name}' → {targetPos}");
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private RectTransform GetRT(GameObject go, string label)
    {
        if (go == null) { if (showDebug) Debug.LogWarning($"[PanelListItemVisitor] {label} not assigned!"); return null; }
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) Debug.LogError($"[PanelListItemVisitor] {label} has no RectTransform!");
        return rt;
    }

    private CanvasGroup GetOrAddCG(GameObject go, string label)
    {
        if (go == null) { Debug.LogError($"[PanelListItemVisitor] {label} not assigned!"); return null; }
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════════

    public bool IsListItemPanelVisible() => isListItemVisible;
    public bool IsChatPanelVisible()     => isChatVisible;
    public bool IsMinimapVisible()       => isMinimapVisible;

    public void SetAnimationDuration(float duration) => animationDuration = Mathf.Max(0.1f, duration);

    public void SetChatButtonVisible(bool show)
    {
        if (openChatButton == null) return;
        openChatButton.gameObject.SetActive(show);
        if (!show && isChatVisible) CloseChat();
    }

    private void CheckGameMode()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsSinglePlayerMode())
            SetChatButtonVisible(false);
    }
}