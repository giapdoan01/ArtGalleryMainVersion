using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

public class PaintingPrefab : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private MeshRenderer quadRenderer;

    [Header("Frame Types")]
    [SerializeField] private Transform landscapeFrame;
    [SerializeField] private Transform portraitFrame;

    [Header("UI Buttons")]
    [SerializeField] private Collider infoCollider;
    [SerializeField] private Button transformButton;
    [SerializeField] private Button removeButton;

    [Header("Runtime Gizmo")]
    [SerializeField] private RuntimeTransformGizmo gizmo;

    [Header("Teleport")]
    [SerializeField] private Transform teleportPoint;

    [Header("Outline Hover Effect")]
    [SerializeField] private GameObject outlineObject;
    [SerializeField] private float outlineScaleMin = 1.00f;
    [SerializeField] private float outlineScaleMax = 1.10f;
    [SerializeField] private float fadeInSpeed     = 6.0f;
    [SerializeField] private float fadeOutSpeed    = 8.0f;

    [Header("Painting Name Tag — Landscape")]
    [SerializeField] private GameObject nameTagFrameLandscape;  // Background landscape
    [SerializeField] private TMP_Text   landscapeNameTagText;
    [SerializeField] private TMP_Text   landscapeAuthorTagText;

    [Header("Painting Name Tag — Portrait")]
    [SerializeField] private GameObject nameTagFramePortrait;   // Background portrait
    [SerializeField] private TMP_Text   portraitNameTagText;
    [SerializeField] private TMP_Text   portraitAuthorTagText;

    [Header("Settings")]
    [SerializeField] private float frameWidth    = 1f;
    [SerializeField] private float frameHeight   = 1f;
    [SerializeField] private bool  isVisitorMode = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ── Frame / Quad scale gốc ──────────────────────
    private Vector3 originalLandscapeFrameScale;
    private Vector3 originalPortraitFrameScale;
    private Vector3 originalQuadScale;
    private bool    originalScalesSaved = false;

    // ── Outline state ────────────────────────────────
    private Vector3  outlineBaseScale;
    private float    outlineAlpha = 0f;
    private bool     isHovering   = false;

    private Material outlineMaterialInstance;
    private static readonly int OutlineScaleProp = Shader.PropertyToID("outlineScale");

    // ── Data ─────────────────────────────────────────
    private Painting     paintingData;
    private Texture2D    paintingTexture;
    public  Action<bool> onDisplayButton;

    // ── Frame type hiện tại ──────────────────────────
    private bool isLandscape = true;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (quadRenderer == null)
            quadRenderer = GetComponentInChildren<MeshRenderer>();

        if (landscapeFrame == null)
            landscapeFrame = transform.Find("LandscapeFrame");

        if (portraitFrame == null)
            portraitFrame = transform.Find("PortraitFrame");

        SaveOriginalFrameScales();

        if (gizmo == null)
            gizmo = GetComponent<RuntimeTransformGizmo>();

        if (infoCollider == null)
            infoCollider = GetComponentInChildren<Collider>();

        if (infoCollider != null)
            infoCollider.isTrigger = true;

        if (teleportPoint == null)
        {
            teleportPoint = transform.Find("TeleportPoint");
            if (teleportPoint == null && showDebug)
                Debug.LogWarning($"[PaintingPrefab] TeleportPoint not found in {gameObject.name}");
        }

        SetupOutline();
    }

    private void SaveOriginalFrameScales()
    {
        if (originalScalesSaved) return;

        if (landscapeFrame != null)
        {
            originalLandscapeFrameScale = landscapeFrame.localScale;
            if (showDebug) Debug.Log($"[PaintingPrefab] Saved landscape: {originalLandscapeFrameScale}");
        }

        if (portraitFrame != null)
        {
            originalPortraitFrameScale = portraitFrame.localScale;
            if (showDebug) Debug.Log($"[PaintingPrefab] Saved portrait: {originalPortraitFrameScale}");
        }

        if (quadRenderer != null)
        {
            originalQuadScale = quadRenderer.transform.localScale;
            if (showDebug) Debug.Log($"[PaintingPrefab] Saved quad: {originalQuadScale}");
        }

        originalScalesSaved = true;
    }

    private void SetupOutline()
    {
        if (outlineObject == null) return;

        outlineBaseScale = outlineObject.transform.localScale;

        MeshRenderer outlineRenderer = outlineObject.GetComponent<MeshRenderer>();
        if (outlineRenderer != null)
        {
            outlineMaterialInstance = new Material(outlineRenderer.sharedMaterial);
            outlineRenderer.material = outlineMaterialInstance;
        }

        outlineAlpha = 0f;
        outlineObject.transform.localScale = outlineBaseScale;

        if (outlineMaterialInstance != null)
            outlineMaterialInstance.SetFloat(OutlineScaleProp, outlineScaleMin);

        outlineObject.SetActive(false);
    }

    private void Start()
    {
        onDisplayButton += DisplayButton;

        if (PaintingClickManager.Instance != null)
            PaintingClickManager.Instance.RegisterPainting(this);
    }

    private void Update()
    {
        CheckMouseHover();
        UpdateOutlinePulse();
    }

    private void OnDestroy()
    {
        if (transformButton != null)
            transformButton.onClick.RemoveListener(OnTransformButtonClicked);

        if (removeButton != null)
            removeButton.onClick.RemoveListener(OnRemoveButtonClicked);

        if (gizmo != null)
            gizmo.OnTransformChanged -= OnGizmoTransformChanged;

        onDisplayButton -= DisplayButton;

        if (PaintingClickManager.Instance != null)
            PaintingClickManager.Instance.UnregisterPainting(this);

        if (outlineMaterialInstance != null)
            Destroy(outlineMaterialInstance);
    }

    // ════════════════════════════════════════════════
    // OUTLINE — CORE
    // ════════════════════════════════════════════════

    private void CheckMouseHover()
    {
        if (infoCollider == null || !infoCollider.enabled) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (isHovering)
            {
                isHovering = false;
                if (showDebug) Debug.Log($"[PaintingPrefab] Hover blocked by UI: {paintingData?.name}");
            }
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray  ray = cam.ScreenPointToRay(Input.mousePosition);
        bool hit = infoCollider.Raycast(ray, out _, 200f);

        if (hit && !isHovering)
        {
            isHovering = true;
            outlineObject?.SetActive(true);
            if (showDebug) Debug.Log($"[PaintingPrefab] Hover ON: {paintingData?.name}");
        }
        else if (!hit && isHovering)
        {
            isHovering = false;
            if (showDebug) Debug.Log($"[PaintingPrefab] Hover OFF: {paintingData?.name}");
        }
    }

    private void UpdateOutlinePulse()
    {
        if (outlineObject == null) return;

        if (isHovering)
        {
            outlineAlpha = Mathf.MoveTowards(outlineAlpha, 1f, Time.deltaTime * fadeInSpeed);

            float finalScale = Mathf.Lerp(outlineScaleMin, outlineScaleMax, outlineAlpha);
            outlineObject.transform.localScale = outlineBaseScale * finalScale;

            if (outlineMaterialInstance != null)
                outlineMaterialInstance.SetFloat(OutlineScaleProp, finalScale);
        }
        else
        {
            if (!outlineObject.activeSelf) return;

            outlineAlpha = Mathf.MoveTowards(outlineAlpha, 0f, Time.deltaTime * fadeOutSpeed);

            float finalScale = Mathf.Lerp(outlineScaleMin, outlineScaleMax, outlineAlpha);
            outlineObject.transform.localScale = outlineBaseScale * finalScale;

            if (outlineMaterialInstance != null)
                outlineMaterialInstance.SetFloat(OutlineScaleProp, finalScale);

            if (outlineAlpha <= 0f)
            {
                outlineObject.SetActive(false);
                if (showDebug) Debug.Log($"[PaintingPrefab] Outline hidden: {paintingData?.name}");
            }
        }
    }

    // ════════════════════════════════════════════════
    // SETUP
    // ════════════════════════════════════════════════

    public void Setup(Painting painting, Texture2D texture)
    {
        paintingData    = painting;
        paintingTexture = texture;

        if (texture == null || quadRenderer == null) return;

        SaveOriginalFrameScales();
        ApplyTexture(texture);
        SetupFrame(painting.frame_type, texture);  // ✅ set isLandscape trước
        SetupInfoCollider();
        SetupTransformButton();
        SetupRemoveButton();
        ApplyTransformFromData(painting);
        SetupTeleportPoint();

        SetNameTag(painting.name, painting.author); // ✅ gọi sau SetupFrame
    }

    private void SetupInfoCollider()
    {
        if (infoCollider == null)
        {
            Debug.LogWarning("[PaintingPrefab] Info collider not assigned!");
            return;
        }

        infoCollider.enabled   = true;
        infoCollider.isTrigger = true;

        if (infoCollider is BoxCollider box && quadRenderer != null)
        {
            Vector3 qs = quadRenderer.transform.localScale;
            box.size   = new Vector3(qs.x, qs.y, 0.2f);
            box.center = Vector3.zero;
        }

        if (infoCollider is MeshCollider mc)
            mc.convex = true;
    }

    private void SetupTransformButton()
    {
        if (transformButton != null)
        {
            transformButton.onClick.RemoveListener(OnTransformButtonClicked);
            transformButton.onClick.AddListener(OnTransformButtonClicked);
        }
    }

    private void SetupRemoveButton()
    {
        if (removeButton != null)
        {
            removeButton.onClick.RemoveListener(OnRemoveButtonClicked);
            removeButton.onClick.AddListener(OnRemoveButtonClicked);
        }
    }

    private void SetupTeleportPoint()
    {
        if (teleportPoint != null) return;

        GameObject tp = new GameObject("TeleportPoint");
        tp.transform.SetParent(transform);
        tp.transform.localPosition = new Vector3(0, 0, -2f);
        tp.transform.localRotation = Quaternion.identity;
        teleportPoint = tp.transform;

        if (showDebug)
            Debug.Log($"[PaintingPrefab] Auto-created TeleportPoint for {paintingData.name}");
    }

    // ════════════════════════════════════════════════
    // NAME TAG
    // ════════════════════════════════════════════════

    /// <summary>
    /// Set text + show/hide đúng NameTagFrame theo frame type.
    /// Landscape → nameTagFrameLandscape show, nameTagFramePortrait hide
    /// Portrait  → nameTagFramePortrait show,  nameTagFrameLandscape hide
    /// </summary>
    private void SetNameTag(string paintingName, string author)
    {
        string displayAuthor = string.IsNullOrEmpty(author) ? "Unknown" : author;

        if (isLandscape)
        {
            // ── Show landscape name tag frame ────────────────────────────
            nameTagFrameLandscape?.SetActive(true);
            nameTagFramePortrait?.SetActive(false);

            if (landscapeNameTagText   != null) landscapeNameTagText.text   = paintingName ?? string.Empty;
            if (landscapeAuthorTagText != null) landscapeAuthorTagText.text = displayAuthor;
        }
        else
        {
            // ── Show portrait name tag frame ─────────────────────────────
            nameTagFrameLandscape?.SetActive(false);
            nameTagFramePortrait?.SetActive(true);

            if (portraitNameTagText   != null) portraitNameTagText.text   = paintingName ?? string.Empty;
            if (portraitAuthorTagText != null) portraitAuthorTagText.text = displayAuthor;
        }

        if (showDebug)
            Debug.Log($"[PaintingPrefab] NameTag ({(isLandscape ? "Landscape" : "Portrait")}) → name='{paintingName}' author='{author}'");
    }

    // ════════════════════════════════════════════════
    // DISPLAY / UI
    // ════════════════════════════════════════════════

    public void DisplayButton(bool isShow)
    {
        isShow = !isShow;

        if (infoCollider != null) infoCollider.enabled = isShow;

        if (transformButton != null)
        {
            Image img = transformButton.GetComponent<Image>();
            if (img != null) img.enabled = isShow;
            transformButton.interactable = isShow;
        }

        if (removeButton != null)
        {
            Image img = removeButton.GetComponent<Image>();
            if (img != null) img.enabled = isShow;
            removeButton.interactable = isShow;
        }
    }

    public void OnInfoColliderClicked()
    {
        if (paintingData == null) return;

        if (PaintingController.Instance != null)
            PaintingController.Instance.ShowPaintingInfo(paintingData, paintingTexture);
    }

    // ════════════════════════════════════════════════
    // TEXTURE & FRAME
    // ════════════════════════════════════════════════

    private void ApplyTexture(Texture2D texture)
    {
        if (quadRenderer == null) return;
        Material mat = new Material(quadRenderer.sharedMaterial);
        mat.mainTexture = texture;
        quadRenderer.material = mat;
    }

    private void SetupFrame(string frameType, Texture2D texture)
    {
        float ar = (float)texture.width / texture.height;

        if (frameType == "wood_horizontal" || frameType == "landscape" || frameType == "1")
        {
            isLandscape = true;

            landscapeFrame?.gameObject.SetActive(true);
            portraitFrame?.gameObject.SetActive(false);

            if (quadRenderer != null)
            {
                Vector3 s = originalQuadScale;
                s.x = originalQuadScale.y * ar;
                quadRenderer.transform.localScale = s;
            }

            if (landscapeFrame != null)
            {
                Vector3 s = originalLandscapeFrameScale;
                s.x = originalLandscapeFrameScale.x * ar;
                landscapeFrame.localScale = s;
            }
        }
        else if (frameType == "wood_vertical" || frameType == "portrait" || frameType == "2")
        {
            isLandscape = false;

            landscapeFrame?.gameObject.SetActive(false);
            portraitFrame?.gameObject.SetActive(true);

            if (quadRenderer != null)
            {
                Vector3 s = originalQuadScale;
                s.y = originalQuadScale.x / ar;
                quadRenderer.transform.localScale = s;
            }

            if (portraitFrame != null)
            {
                Vector3 s = originalPortraitFrameScale;
                s.z = originalPortraitFrameScale.z / ar;
                portraitFrame.localScale = s;
            }
        }
        else
        {
            isLandscape = true; // fallback → landscape

            if (showDebug) Debug.LogWarning($"[PaintingPrefab] Unknown frameType '{frameType}', fallback landscape");

            landscapeFrame?.gameObject.SetActive(true);
            portraitFrame?.gameObject.SetActive(false);

            if (quadRenderer != null)
            {
                Vector3 s = originalQuadScale;
                s.x = originalQuadScale.y * ar;
                quadRenderer.transform.localScale = s;
            }

            if (landscapeFrame != null)
            {
                Vector3 s = originalLandscapeFrameScale;
                s.x = originalLandscapeFrameScale.x * ar;
                landscapeFrame.localScale = s;
            }
        }
    }

    // ════════════════════════════════════════════════
    // BUTTONS
    // ════════════════════════════════════════════════

    private void OnTransformButtonClicked()
    {
        if (paintingData == null || gizmo == null) return;

        gizmo.OnTransformChanged -= OnGizmoTransformChanged;
        gizmo.OnTransformChanged += OnGizmoTransformChanged;
        gizmo.Activate();

        if (PaintingTransformEditPopup.Instance != null)
        {
            PaintingTransformEditPopup.Instance.Show(paintingData.id, paintingData, this);
            PaintingTransformEditPopup.Instance.onGizmoModeChanged.Invoke(gizmo.currentMode.ToString());
        }
    }

    private void OnRemoveButtonClicked()
    {
        if (paintingData == null)
        {
            Debug.LogError("[PaintingPrefab] No painting data to remove!");
            return;
        }

        if (PaintingRemoveConfirmPopup.Instance != null)
            PaintingRemoveConfirmPopup.Instance.Show(paintingData, ConfirmRemovePainting);
        else
            ConfirmRemovePainting();
    }

    private void ConfirmRemovePainting()
    {
        if (paintingData == null) return;

        PaintingTransformEditPopup.Instance?.Hide();

        if (APIManager.Instance != null)
        {
            APIManager.Instance.RemovePaintingOrModel(
                paintingData.id, "painting",
                () =>
                {
                    NotifyGalleryItemRemoved(paintingData.id);
                    Destroy(gameObject);
                    PaintingPrefabManager.Instance?.ReloadAllPaintings();
                },
                err => Debug.LogError($"[PaintingPrefab] Remove failed: {err}")
            );
        }
    }

    private void NotifyGalleryItemRemoved(int id)
    {
        FindObjectOfType<PaintingGalleryContainer>()?.OnPaintingRemovedFromScene(id);
    }

    private void OnGizmoTransformChanged(Vector3 pos, Vector3 rot)
    {
        PaintingTransformEditPopup.Instance?.UpdateFromGizmo(pos, rot);
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

    public Painting              GetData()          => paintingData;
    public Texture2D             GetTexture()       => paintingTexture;
    public RuntimeTransformGizmo GetGizmo()         => gizmo;
    public Transform             GetTeleportPoint() => teleportPoint;

    public void ApplyTransformFromData(Painting data)
    {
        if (data == null) return;
        if (data.position != null) transform.position    = data.position.ToVector3();
        if (data.rotate   != null) transform.eulerAngles = data.rotate.ToVector3();
    }

    public void UpdateDataFromTransform()
    {
        if (paintingData == null) return;

        paintingData.position ??= new Position();
        paintingData.position.x = transform.position.x;
        paintingData.position.y = transform.position.y;
        paintingData.position.z = transform.position.z;

        paintingData.rotate ??= new Rotation();
        paintingData.rotate.x = transform.eulerAngles.x;
        paintingData.rotate.y = transform.eulerAngles.y;
        paintingData.rotate.z = transform.eulerAngles.z;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (teleportPoint == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(teleportPoint.position, 0.3f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(teleportPoint.position, teleportPoint.forward);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, teleportPoint.position);
    }
#endif
}