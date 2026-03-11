using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PanelListItemVisitor : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button displayListItemButton;
    [SerializeField] private Button openPaintingPanelButton;
    [SerializeField] private Button openModel3DPanelButton;
    [SerializeField] private Button openChatButton;
    [SerializeField] private Button closeChatButton;

    [Header("Panels")]
    [SerializeField] private GameObject ListItemPanel;
    [SerializeField] private GameObject PaintingPanel;
    [SerializeField] private GameObject Model3DPanel;
    [SerializeField] private GameObject ChatPanel;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("ListItem Positions")]
    [SerializeField] private Vector3 listItemShowPosition = Vector3.zero;
    [SerializeField] private Vector3 listItemHidePosition = new Vector3(0, -400, 0);

    [Header("Chat Positions")]
    [SerializeField] private Vector3 chatPanelShowPosition = new Vector3(-652, -347, 0);
    [SerializeField] private Vector3 chatPanelHidePosition = new Vector3(-1253, -717, 0);

    [Header("Panel Integration")]
    [SerializeField] private PaintingInfo paintingInfo;
    [SerializeField] private Model3DInfo  model3DInfo;   // ✅ thêm mới

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ── State ────────────────────────────────────────
    private bool isListItemVisible  = false;
    private bool isChatVisible      = false;
    private bool paintingInfoIsOpen = false;
    private bool model3DInfoIsOpen  = false;   // ✅ thêm mới

    // state trước khi info panel mở
    private bool wasListItemVisibleBeforePaintingInfo = false;
    private bool wasChatVisibleBeforePaintingInfo     = false;
    private bool wasListItemVisibleBeforeModel3DInfo  = false;   // ✅
    private bool wasChatVisibleBeforeModel3DInfo      = false;   // ✅

    // ── Animation counter ────────────────────────────
    private int runningAnimations = 0;

    // ── RectTransforms ───────────────────────────────
    private RectTransform listItemRT;
    private RectTransform chatPanelRT;

    // ── CanvasGroups ─────────────────────────────────
    private CanvasGroup paintingPanelCG;
    private CanvasGroup model3DPanelCG;

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        listItemRT  = GetRT(ListItemPanel, "ListItemPanel");
        chatPanelRT = GetRT(ChatPanel,     "ChatPanel");

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
        if (displayListItemButton   != null) displayListItemButton.onClick.RemoveListener(ToggleListItemPanel);
        if (openPaintingPanelButton != null) openPaintingPanelButton.onClick.RemoveListener(ShowPaintingPanel);
        if (openModel3DPanelButton  != null) openModel3DPanelButton.onClick.RemoveListener(ShowModel3DPanel);
        if (openChatButton          != null) openChatButton.onClick.RemoveListener(OpenChat);
        if (closeChatButton         != null) closeChatButton.onClick.RemoveListener(CloseChat);
    }

    // ═══════════════════════════════════════════════
    // SETUP
    // ═══════════════════════════════════════════════

    private void SetupButtons()
    {
        AddListener(displayListItemButton,   "displayListItemButton",   ToggleListItemPanel);
        AddListener(openPaintingPanelButton, "openPaintingPanelButton", ShowPaintingPanel);
        AddListener(openModel3DPanelButton,  "openModel3DPanelButton",  ShowModel3DPanel);
        AddListener(openChatButton,          "openChatButton",          OpenChat);
        AddListener(closeChatButton,         "closeChatButton",         CloseChat);
    }

    private void AddListener(Button btn, string name, UnityEngine.Events.UnityAction action)
    {
        if (btn != null) { btn.onClick.RemoveListener(action); btn.onClick.AddListener(action); }
        else Debug.LogError($"[PanelListItemVisitor] {name} is not assigned!");
    }

    private void InitializePanels()
    {
        if (listItemRT  != null) listItemRT.anchoredPosition  = listItemHidePosition;
        if (chatPanelRT != null) chatPanelRT.anchoredPosition = chatPanelHidePosition;

        isListItemVisible = false;
        isChatVisible     = false;

        ShowPaintingPanel();
    }

    // ═══════════════════════════════════════════════
    // PAINTING / MODEL3D PANEL (CanvasGroup)
    // ═══════════════════════════════════════════════

    private void SetCG(CanvasGroup cg, bool visible)
    {
        if (cg == null) return;
        cg.alpha          = visible ? 1f : 0f;
        cg.interactable   = visible;
        cg.blocksRaycasts = visible;
    }

    private void ShowPaintingPanel() { SetCG(paintingPanelCG, true);  SetCG(model3DPanelCG, false); }
    private void ShowModel3DPanel()  { SetCG(paintingPanelCG, false); SetCG(model3DPanelCG, true);  }

    // ═══════════════════════════════════════════════
    // LIST ITEM PANEL
    // ═══════════════════════════════════════════════

    private void ToggleListItemPanel()
    {
        if (listItemRT == null) return;

        if (paintingInfo != null && paintingInfoIsOpen)
        {
            paintingInfoIsOpen = false;
            paintingInfo.HideInfo();
        }

        // ✅ Đóng Model3DInfo nếu đang mở
        if (model3DInfo != null && model3DInfoIsOpen)
        {
            model3DInfoIsOpen = false;
            model3DInfo.HideInfo();
        }

        if (!isListItemVisible)
        {
            if (isChatVisible) HideChatPanel();
            isListItemVisible = true;
            AnimateTo(listItemRT, listItemShowPosition);
        }
        else
        {
            isListItemVisible = false;
            AnimateTo(listItemRT, listItemHidePosition);
        }

        if (showDebug) Debug.Log($"[PanelListItemVisitor] ListItem → {(isListItemVisible ? "SHOW" : "HIDE")}");
    }

    // ═══════════════════════════════════════════════
    // CHAT PANEL
    // ═══════════════════════════════════════════════

    private void OpenChat()
    {
        if (chatPanelRT == null) return;

        if (isListItemVisible)
        {
            isListItemVisible = false;
            AnimateTo(listItemRT, listItemHidePosition);
        }

        isChatVisible = true;
        AnimateTo(chatPanelRT, chatPanelShowPosition);

        if (showDebug) Debug.Log("[PanelListItemVisitor] Chat → SHOW");
    }

    private void CloseChat()
    {
        HideChatPanel();
        if (showDebug) Debug.Log("[PanelListItemVisitor] Chat → HIDE");
    }

    private void HideChatPanel()
    {
        if (chatPanelRT == null) return;
        isChatVisible = false;
        AnimateTo(chatPanelRT, chatPanelHidePosition);
    }

    // ═══════════════════════════════════════════════
    // PAINTING INFO INTEGRATION
    // ═══════════════════════════════════════════════

    public void HideForPaintingInfo()
    {
        if (paintingInfoIsOpen) return;
        paintingInfoIsOpen = true;

        wasListItemVisibleBeforePaintingInfo = isListItemVisible;
        wasChatVisibleBeforePaintingInfo     = isChatVisible;

        if (isListItemVisible)
        {
            isListItemVisible = false;
            AnimateTo(listItemRT, listItemHidePosition);
        }
        if (isChatVisible)
        {
            isChatVisible = false;
            AnimateTo(chatPanelRT, chatPanelHidePosition);
        }

        if (showDebug)
            Debug.Log($"[PanelListItemVisitor] HideForPaintingInfo — listItem={wasListItemVisibleBeforePaintingInfo}, chat={wasChatVisibleBeforePaintingInfo}");
    }

    public void RestoreAfterPaintingInfo()
    {
        if (!paintingInfoIsOpen) return;
        paintingInfoIsOpen = false;

        if (showDebug)
            Debug.Log($"[PanelListItemVisitor] RestoreAfterPaintingInfo — listItem={wasListItemVisibleBeforePaintingInfo}, chat={wasChatVisibleBeforePaintingInfo}");

        if (wasListItemVisibleBeforePaintingInfo)
        {
            isListItemVisible = true;
            AnimateTo(listItemRT, listItemShowPosition);
        }
        else if (wasChatVisibleBeforePaintingInfo)
        {
            isChatVisible = true;
            AnimateTo(chatPanelRT, chatPanelShowPosition);
        }
    }

    // ═══════════════════════════════════════════════
    // ✅ MODEL3D INFO INTEGRATION (mới — giống PaintingInfo)
    // ═══════════════════════════════════════════════

    public void HideForModel3DInfo()
    {
        if (model3DInfoIsOpen) return;
        model3DInfoIsOpen = true;

        wasListItemVisibleBeforeModel3DInfo = isListItemVisible;
        wasChatVisibleBeforeModel3DInfo     = isChatVisible;

        if (isListItemVisible)
        {
            isListItemVisible = false;
            AnimateTo(listItemRT, listItemHidePosition);
        }
        if (isChatVisible)
        {
            isChatVisible = false;
            AnimateTo(chatPanelRT, chatPanelHidePosition);
        }

        if (showDebug)
            Debug.Log($"[PanelListItemVisitor] HideForModel3DInfo — listItem={wasListItemVisibleBeforeModel3DInfo}, chat={wasChatVisibleBeforeModel3DInfo}");
    }

    public void RestoreAfterModel3DInfo()
    {
        if (!model3DInfoIsOpen) return;
        model3DInfoIsOpen = false;

        if (showDebug)
            Debug.Log($"[PanelListItemVisitor] RestoreAfterModel3DInfo — listItem={wasListItemVisibleBeforeModel3DInfo}, chat={wasChatVisibleBeforeModel3DInfo}");

        if (wasListItemVisibleBeforeModel3DInfo)
        {
            isListItemVisible = true;
            AnimateTo(listItemRT, listItemShowPosition);
        }
        else if (wasChatVisibleBeforeModel3DInfo)
        {
            isChatVisible = true;
            AnimateTo(chatPanelRT, chatPanelShowPosition);
        }
    }

    // ═══════════════════════════════════════════════
    // ANIMATION
    // ═══════════════════════════════════════════════

    private void AnimateTo(RectTransform rt, Vector3 targetPos)
    {
        if (rt == null) return;
        StartCoroutine(AnimatePanel(rt, targetPos));
    }

    private IEnumerator AnimatePanel(RectTransform rt, Vector3 targetPos)
    {
        runningAnimations++;
        Vector3 startPos = rt.anchoredPosition;
        float elapsed    = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float easedT        = easeCurve.Evaluate(Mathf.Clamp01(elapsed / animationDuration));
            rt.anchoredPosition = Vector3.Lerp(startPos, targetPos, easedT);
            yield return null;
        }

        rt.anchoredPosition = targetPos;
        runningAnimations--;

        if (showDebug) Debug.Log($"[PanelListItemVisitor] '{rt.name}' done → {targetPos}");
    }

    // ═══════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════

    private RectTransform GetRT(GameObject go, string label)
    {
        if (go == null) { Debug.LogError($"[PanelListItemVisitor] {label} not assigned!"); return null; }
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

    // ═══════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════

    public void ShowListItemPanel()      { if (!isListItemVisible) ToggleListItemPanel(); }
    public void HideListItemPanel()      { if (isListItemVisible)  ToggleListItemPanel(); }
    public bool IsListItemPanelVisible() => isListItemVisible;
    public bool IsChatPanelVisible()     => isChatVisible;

    public void SetAnimationDuration(float duration) => animationDuration = Mathf.Max(0.1f, duration);

    public void SetChatButtonVisible(bool show)
    {
        if (openChatButton == null) return;
        openChatButton.gameObject.SetActive(show);
        if (!show && isChatVisible) HideChatPanel();
    }

    private void CheckGameMode()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsSinglePlayerMode())
            SetChatButtonVisible(false);
    }
}