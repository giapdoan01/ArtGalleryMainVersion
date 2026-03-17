// ═══════════════════════════════════════════════════════════════════
// MenuView.cs
// ═══════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class MenuView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject UIInPlay;

    [Header("Mode Selection")]
    [SerializeField] private Button multiPlayerButton;
    [SerializeField] private Button singlePlayerButton;
    [SerializeField] private Button startButton;

    [Header("Mode Indicator")]
    [Tooltip("GameObject sẽ trượt sang trái/phải để chỉ mode đang chọn")]
    [SerializeField] private RectTransform moveElement;
    [SerializeField] private float posXMultiPlayer = 160f;
    [SerializeField] private float posXSinglePlayer = -162f;
    [SerializeField] private float moveSpeed = 10f;   // lerp speed

    [Header("Mode Labels & Icons")]
    [SerializeField] private GameObject singleText;
    [SerializeField] private GameObject multiText;
    [SerializeField] private GameObject singleIcon;
    [SerializeField] private GameObject multiIcon;

    // ─── Events ────────────────────────────────────
    public event Action<string> OnNameChanged;
    public event Action OnMultiPlayerModeSelected;   // ← chỉ chọn mode
    public event Action OnSinglePlayerModeSelected;  // ← chỉ chọn mode
    public event Action OnStartButtonClicked;        // ← mới: trigger vào game

    // ─── State ─────────────────────────────────────
    private bool isMoving = false;
    private float targetPosX = 0f;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        ValidateComponents();
        if (UIInPlay != null) MoveUIInPlayOffscreen();

        // ✅ Mặc định chọn SinglePlayer ngay từ đầu
        if (moveElement != null)
        {
            Vector2 pos = moveElement.anchoredPosition;
            pos.x = posXSinglePlayer;
            moveElement.anchoredPosition = pos;
            targetPosX = posXSinglePlayer;
        }

        // ✅ Hiển thị label/icon theo mode mặc định (SinglePlayer)
        UpdateModeIndicators(isMulti: false);

        // ✅ Start button hiện sẵn (vì đã có mode mặc định)
        if (startButton != null) startButton.gameObject.SetActive(true);
    }

    private void Start()
    {
        SetupUIListeners();
    }

    private void Update()
    {
        UpdateMoveElement();
    }

    private void OnDestroy()
    {
        if (nameInput != null) nameInput.onValueChanged.RemoveAllListeners();
        if (multiPlayerButton != null) multiPlayerButton.onClick.RemoveAllListeners();
        if (singlePlayerButton != null) singlePlayerButton.onClick.RemoveAllListeners();
        if (startButton != null) startButton.onClick.RemoveAllListeners();
    }

    // ════════════════════════════════════════════════
    // SETUP
    // ════════════════════════════════════════════════

    private void ValidateComponents()
    {
        if (nameInput == null) Debug.LogError("[MenuView] nameInput not assigned!");
        if (statusText == null) Debug.LogError("[MenuView] statusText not assigned!");
        if (multiPlayerButton == null) Debug.LogError("[MenuView] multiPlayerButton not assigned!");
        if (singlePlayerButton == null) Debug.LogError("[MenuView] singlePlayerButton not assigned!");
        if (startButton == null) Debug.LogError("[MenuView] startButton not assigned!");
        if (moveElement == null) Debug.LogWarning("[MenuView] moveElement not assigned!");
        if (UIInPlay == null) Debug.LogWarning("[MenuView] UIInPlay not assigned!");
    }

    private void SetupUIListeners()
    {
        if (nameInput != null)
            nameInput.onValueChanged.AddListener(n => OnNameChanged?.Invoke(n));

        if (multiPlayerButton != null)
            multiPlayerButton.onClick.AddListener(OnMultiPlayerClicked);

        if (singlePlayerButton != null)
            singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
    }

    // ════════════════════════════════════════════════
    // BUTTON HANDLERS
    // ════════════════════════════════════════════════

    private void OnMultiPlayerClicked()
    {
        SetMoveElementTarget(posXMultiPlayer);
        UpdateModeIndicators(isMulti: true);
        ShowStartButton(true);
        OnMultiPlayerModeSelected?.Invoke();
    }

    private void OnSinglePlayerClicked()
    {
        SetMoveElementTarget(posXSinglePlayer);
        UpdateModeIndicators(isMulti: false);
        ShowStartButton(true);
        OnSinglePlayerModeSelected?.Invoke();
    }

    private void OnStartClicked()
    {
        // Disable nút tránh double-click
        if (startButton != null) startButton.interactable = false;

        OnStartButtonClicked?.Invoke();
    }

    // ════════════════════════════════════════════════
    // MOVE ELEMENT
    // ════════════════════════════════════════════════

    private void SetMoveElementTarget(float posX)
    {
        if (moveElement == null) return;
        targetPosX = posX;
        isMoving = true;
    }

    private void UpdateMoveElement()
    {
        if (!isMoving || moveElement == null) return;

        Vector2 pos = moveElement.anchoredPosition;
        pos.x = Mathf.Lerp(pos.x, targetPosX, Time.deltaTime * moveSpeed);
        moveElement.anchoredPosition = pos;

        // Dừng lerp khi đủ gần
        if (Mathf.Abs(pos.x - targetPosX) < 0.5f)
        {
            pos.x = targetPosX;
            moveElement.anchoredPosition = pos;
            isMoving = false;
        }
    }

    /// <summary>Snap ngay không lerp — dùng khi reset menu.</summary>
    public void SnapMoveElement(float posX)
    {
        if (moveElement == null) return;
        Vector2 pos = moveElement.anchoredPosition;
        pos.x = posX;
        moveElement.anchoredPosition = pos;
        targetPosX = posX;
        isMoving = false;
    }

    // ════════════════════════════════════════════════
    // START BUTTON
    // ════════════════════════════════════════════════

    public void ShowStartButton(bool show)
    {
        if (startButton == null) return;
        startButton.gameObject.SetActive(show);
        startButton.interactable = show;
    }

    public void SetStartButtonInteractable(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
    }

    // ════════════════════════════════════════════════
    // PUBLIC API — Player Name
    // ════════════════════════════════════════════════

    public string GetPlayerName() => nameInput != null ? nameInput.text : "";
    public void SetPlayerName(string name) { if (nameInput != null) nameInput.text = name; }

    // ════════════════════════════════════════════════
    // PUBLIC API — Status & Buttons
    // ════════════════════════════════════════════════

    public void UpdateStatusText(string status)
    {
        if (statusText != null) statusText.text = status;
    }

    public void SetModeButtonsInteractable(bool interactable)
    {
        if (multiPlayerButton != null) multiPlayerButton.interactable = interactable;
        if (singlePlayerButton != null) singlePlayerButton.interactable = interactable;
    }

    // ════════════════════════════════════════════════
    // PUBLIC API — Reset
    // ════════════════════════════════════════════════

    public void ResetView(string playerName = "")
    {
        SetModeButtonsInteractable(true);
        UpdateStatusText("Chọn avatar và nhập tên để bắt đầu!");

        if (!string.IsNullOrEmpty(playerName))
            SetPlayerName(playerName);

        // ✅ Reset về SinglePlayer mặc định + hiện Start button
        ShowStartButton(true);                   // ← đổi false → true
        SnapMoveElement(posXSinglePlayer);
        UpdateModeIndicators(isMulti: false);

        HideInGameUI();
        Debug.Log("[MenuView] View reset ✅");
    }

    // ════════════════════════════════════════════════
    // PUBLIC API — Avatar Animator
    // ════════════════════════════════════════════════

    public void SetAvatarAnimator(GameObject avatar, RuntimeAnimatorController animatorController)
    {
        if (avatar == null || animatorController == null) return;
        Animator animator = avatar.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.runtimeAnimatorController = animatorController;
            animator.SetFloat("Speed", 0f);
        }
    }

    // ════════════════════════════════════════════════
    // IN-GAME UI
    // ════════════════════════════════════════════════

    public void ShowInGameUI()
    {
        if (UIInPlay == null) return;
        UIInPlay.SetActive(true);
        SetRectPos(UIInPlay, Vector2.zero);
    }

    public void HideInGameUI()
    {
        if (UIInPlay == null) return;
        SetRectPos(UIInPlay, new Vector2(-3000f, 0f));
    }

    // ════════════════════════════════════════════════
    // MODE INDICATORS
    // ════════════════════════════════════════════════

    private void UpdateModeIndicators(bool isMulti)
    {
        if (singleText != null) singleText.SetActive(!isMulti);
        if (singleIcon != null) singleIcon.SetActive(!isMulti);
        if (multiText  != null) multiText.SetActive(isMulti);
        if (multiIcon  != null) multiIcon.SetActive(isMulti);
    }

    // ════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════

    private void MoveUIInPlayOffscreen() => SetRectPos(UIInPlay, new Vector2(-3000f, 0f));

    private void SetRectPos(GameObject go, Vector2 pos)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
    }
}