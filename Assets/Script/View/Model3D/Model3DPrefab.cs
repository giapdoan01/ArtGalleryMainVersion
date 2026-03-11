using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using GLTFast;
using System;

public class Model3DPrefab : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform glbContainer;
    [SerializeField] private Collider  infoCollider;

    [Header("Scale Settings")]
    [SerializeField] private bool  useAutoFit    = true;
    [SerializeField] private float maxModelSize  = 2f;
    [SerializeField] private float fallbackScale = 0.01f;

    [Header("UI Buttons")]
    [SerializeField] private Button transformButton;
    [SerializeField] private Button removeButton;

    [Header("Runtime Gizmo")]
    [SerializeField] private RuntimeTransformGizmo gizmo;

    // ════════════════════════════════════════════════
    // HOVER OUTLINE
    // ════════════════════════════════════════════════
    [Header("Hover Outline")]
    [Tooltip("Material có shader hỗ trợ outline")]
    [SerializeField] private Material outlineMaterial;

    [Tooltip("Tên property trong shader điều khiển scale outline\nVD: outlineScale, _OutlineWidth")]
    [SerializeField] private string outlineScaleProperty = "outlineScale";

    [Tooltip("Giá trị outline khi KHÔNG hover")]
    [SerializeField] private float outlineScaleIdle  = 1f;

    [Tooltip("Giá trị outline khi hover")]
    [SerializeField] private float outlineScaleHover = 1.1f;

    [Tooltip("Tốc độ lerp (cao = nhanh hơn)")]
    [SerializeField] private float outlineLerpSpeed  = 8f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ────────────────────────────────────────────────
    // Private fields
    // ────────────────────────────────────────────────
    private Model3D      model3DData;
    private Texture2D    thumbnailTexture;
    private GameObject   loadedGLBObject;
    public  Action<bool> onDisplayButton;

    public GameObject glbInstance => loadedGLBObject;

    // Outline
    private readonly List<Material> outlineMatInstances = new List<Material>();
    private bool  isHovering        = false;
    private float currentOutlineVal = 0f;
    private bool  outlineReady      = false;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (gizmo == null)
            gizmo = GetComponent<RuntimeTransformGizmo>();
    }

    private void Start()
    {
        onDisplayButton += ShowButton;

        if (Model3DClickManager.Instance != null)
            Model3DClickManager.Instance.RegisterModel3D(this);
    }

    private void Update()
    {
        UpdateOutlineLerp();
    }

    private void OnDestroy()
    {
        if (transformButton != null)
            transformButton.onClick.RemoveListener(OnTransformButtonClicked);

        if (removeButton != null)
            removeButton.onClick.RemoveListener(OnRemoveButtonClicked);

        if (gizmo != null)
            gizmo.OnTransformChanged -= OnGizmoTransformChanged;

        onDisplayButton -= ShowButton;

        if (Model3DClickManager.Instance != null)
            Model3DClickManager.Instance.UnregisterModel3D(this);

        foreach (var mat in outlineMatInstances)
            if (mat != null) Destroy(mat);

        outlineMatInstances.Clear();
    }

    // ════════════════════════════════════════════════
    // SETUP
    // ════════════════════════════════════════════════

    public void Setup(Model3D model3D, Texture2D thumbnail)
    {
        model3DData      = model3D;
        thumbnailTexture = thumbnail;

        if (!string.IsNullOrEmpty(model3D.path_url))
            StartCoroutine(LoadGLBModel(model3D.path_url));

        SetupTransformButton();
        SetupRemoveButton();
    }

    private void SetupTransformButton()
    {
        if (transformButton == null) return;
        transformButton.onClick.RemoveListener(OnTransformButtonClicked);
        transformButton.onClick.AddListener(OnTransformButtonClicked);
    }

    private void SetupRemoveButton()
    {
        if (removeButton == null) return;
        removeButton.onClick.RemoveListener(OnRemoveButtonClicked);
        removeButton.onClick.AddListener(OnRemoveButtonClicked);
    }

    public void ShowButton(bool isShow)
    {
        isShow = !isShow;

        if (infoCollider != null)
            infoCollider.enabled = isShow;

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

    // ════════════════════════════════════════════════
    // HOVER OUTLINE — PUBLIC API
    // ════════════════════════════════════════════════

    public void OnHoverEnter()
    {
        if (!outlineReady) return;
        isHovering = true;
        if (showDebug) Debug.Log($"[Model3DPrefab] HoverEnter: {name}");
    }

    public void OnHoverExit()
    {
        isHovering = false;
        if (showDebug) Debug.Log($"[Model3DPrefab] HoverExit: {name}");
    }

    // ════════════════════════════════════════════════
    // HOVER OUTLINE — INTERNAL
    // ════════════════════════════════════════════════

    private void UpdateOutlineLerp()
    {
        if (!outlineReady || outlineMatInstances.Count == 0) return;

        float target = isHovering ? outlineScaleHover : outlineScaleIdle;

        currentOutlineVal = Mathf.Lerp(currentOutlineVal, target, Time.deltaTime * outlineLerpSpeed);

        if (Mathf.Abs(currentOutlineVal - target) > 0.0001f || isHovering)
        {
            foreach (var mat in outlineMatInstances)
                if (mat != null)
                    mat.SetFloat(outlineScaleProperty, currentOutlineVal);
        }
    }

    // ════════════════════════════════════════════════
    // LOAD GLB
    // ════════════════════════════════════════════════

    private IEnumerator LoadGLBModel(string url)
    {
        if (loadedGLBObject != null)
        {
            Destroy(loadedGLBObject);
            loadedGLBObject = null;
        }

        var gltf     = new GltfImport();
        var loadTask = gltf.Load(url);
        while (!loadTask.IsCompleted) yield return null;

        if (!loadTask.Result)
        {
            Debug.LogError($"[Model3DPrefab] Failed to load GLB: {url}");
            yield break;
        }

        Transform parent          = glbContainer != null ? glbContainer : transform;
        var       instantiateTask = gltf.InstantiateMainSceneAsync(parent);
        while (!instantiateTask.IsCompleted) yield return null;

        if (parent.childCount > 0)
        {
            loadedGLBObject = parent.GetChild(parent.childCount - 1).gameObject;

            Vector3 originalRotation = loadedGLBObject.transform.localEulerAngles;

            if (showDebug)
            {
                Debug.Log($"[Model3DPrefab] ===== GLB LOADED =====");
                Debug.Log($"[Model3DPrefab] Model   : {model3DData?.name ?? "Unknown"}");
                Debug.Log($"[Model3DPrefab] Rotation: {originalRotation}");
            }

            if (Model3DPrefabManager.Instance != null && model3DData != null)
                Model3DPrefabManager.Instance.RegisterOriginalRotation(model3DData.id, originalRotation);

            loadedGLBObject.transform.localPosition = Vector3.zero;
            loadedGLBObject.transform.localScale    = Vector3.one;

            // ✅ Chờ 2 frame + end of frame để GLTFast apply materials xong
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            // Debug trước khi setup
            if (showDebug) DebugMeshRenderers();

            SetupMeshColliderAndOutline();

            ApplyTransformFromData(model3DData);

            if (showDebug)
                Debug.Log($"[Model3DPrefab] ===== GLB SETUP COMPLETE =====");
        }
    }

    // ════════════════════════════════════════════════
    // DEBUG HELPER
    // ════════════════════════════════════════════════

    private void DebugMeshRenderers()
    {
        if (loadedGLBObject == null) return;

        MeshRenderer[] mrs = loadedGLBObject.GetComponentsInChildren<MeshRenderer>();
        Debug.Log($"[Model3DPrefab] ── DEBUG MeshRenderers ({mrs.Length}) ──");
        Debug.Log($"[Model3DPrefab] outlineMaterial     = {(outlineMaterial != null ? outlineMaterial.name : "NULL")}");
        Debug.Log($"[Model3DPrefab] outlineScaleProperty= '{outlineScaleProperty}'");

        if (outlineMaterial != null)
        {
            bool hasProp = outlineMaterial.HasProperty(outlineScaleProperty);
            Debug.Log($"[Model3DPrefab] HasProperty('{outlineScaleProperty}') = {hasProp}");
            Debug.Log($"[Model3DPrefab] Shader name = '{outlineMaterial.shader.name}'");
        }

        foreach (var mr in mrs)
        {
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            Debug.Log($"[Model3DPrefab]   MR: {mr.gameObject.name} | " +
                      $"sharedMaterials={mr.sharedMaterials.Length} | " +
                      $"mesh={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "NULL")}");
        }
    }

    // ════════════════════════════════════════════════
    // COLLIDER + OUTLINE — 1 HÀM, 1 LẦN DUYỆT
    // ════════════════════════════════════════════════

    private void SetupMeshColliderAndOutline()
    {
        if (loadedGLBObject == null) return;

        outlineMatInstances.Clear();
        outlineReady = false;

        // ── Validate outline ──────────────────────────
        bool canSetupOutline = false;

        if (outlineMaterial == null)
        {
            Debug.LogWarning("[Model3DPrefab] outlineMaterial chưa được gán — bỏ qua outline");
        }
        else if (!outlineMaterial.HasProperty(outlineScaleProperty))
        {
            Debug.LogWarning(
                $"[Model3DPrefab] Shader '{outlineMaterial.shader.name}' " +
                $"không có property '{outlineScaleProperty}'. " +
                $"Kiểm tra lại tên property trong Shader Graph."
            );
        }
        else
        {
            canSetupOutline = true;
            if (showDebug)
                Debug.Log($"[Model3DPrefab] Outline validate OK — property '{outlineScaleProperty}' found");
        }

        // ── Duyệt MeshRenderer ────────────────────────
        MeshRenderer[] meshRenderers = loadedGLBObject.GetComponentsInChildren<MeshRenderer>(true);

        if (meshRenderers.Length == 0)
        {
            Debug.LogWarning("[Model3DPrefab] No MeshRenderer found in GLB!");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DPrefab] Processing {meshRenderers.Length} MeshRenderer(s)...");

        foreach (MeshRenderer mr in meshRenderers)
        {
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                if (showDebug)
                    Debug.LogWarning($"[Model3DPrefab] Skip {mr.gameObject.name} — no MeshFilter/Mesh");
                continue;
            }

            // ── Layer ─────────────────────────────────
            mr.gameObject.layer = LayerMask.NameToLayer("Model3D");

            // ── Collider ──────────────────────────────
            if (mr.GetComponent<MeshCollider>() == null)
            {
                MeshCollider mc = mr.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh   = mf.sharedMesh;

                try
                {
                    mc.convex    = true;
                    mc.isTrigger = true;
                }
                catch (Exception e)
                {
                    mc.convex    = false;
                    mc.isTrigger = false;
                    if (showDebug)
                        Debug.LogWarning($"[Model3DPrefab] Convex failed ({mr.gameObject.name}): {e.Message} → non-convex");
                }

                if (showDebug)
                    Debug.Log($"[Model3DPrefab] Collider added: {mr.gameObject.name} | convex={mc.convex}");
            }

            // ── Outline material ──────────────────────
            if (canSetupOutline)
            {
                // ✅ Lấy materials hiện tại — dùng mr.materials (instance) thay vì sharedMaterials
                Material[] currentMats = mr.materials;

                if (showDebug)
                    Debug.Log($"[Model3DPrefab] {mr.gameObject.name} currentMats.Length = {currentMats.Length}");

                // Tạo instance outline riêng
                Material outlineInstance  = new Material(outlineMaterial);
                outlineInstance.name      = $"{outlineMaterial.name}_Outline_{mr.gameObject.name}";
                outlineInstance.SetFloat(outlineScaleProperty, outlineScaleIdle);

                // Append vào cuối
                Material[] newMats        = new Material[currentMats.Length + 1];
                Array.Copy(currentMats, newMats, currentMats.Length);
                newMats[newMats.Length - 1] = outlineInstance;

                mr.materials = newMats;

                outlineMatInstances.Add(outlineInstance);

                if (showDebug)
                    Debug.Log($"[Model3DPrefab] Outline assigned: {mr.gameObject.name} | total mats = {mr.materials.Length}");
            }
        }

        // ── infoCollider fallback ─────────────────────
        if (infoCollider == null)
            infoCollider = loadedGLBObject.GetComponentInChildren<Collider>();

        // ── Outline ready ─────────────────────────────
        if (canSetupOutline && outlineMatInstances.Count > 0)
        {
            currentOutlineVal = outlineScaleIdle;
            outlineReady      = true;

            if (showDebug)
                Debug.Log($"[Model3DPrefab] ✅ Outline READY — {outlineMatInstances.Count} instance(s)");
        }
        else if (canSetupOutline)
        {
            Debug.LogWarning("[Model3DPrefab] canSetupOutline=true nhưng không có instance nào được tạo!");
        }
    }

    // ════════════════════════════════════════════════
    // SCALE HELPERS
    // ════════════════════════════════════════════════

    private Vector3 CalculateTargetScale()
    {
        if (model3DData.size != null)
            return new Vector3(model3DData.size.x, model3DData.size.y, model3DData.size.z);

        if (useAutoFit && loadedGLBObject != null)
            return CalculateAutoFitScale(loadedGLBObject, maxModelSize);

        return Vector3.one * fallbackScale;
    }

    private Vector3 CalculateAutoFitScale(GameObject obj, float maxSize)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return Vector3.one * fallbackScale;

        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

        float currentMax = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (currentMax <= 0) return Vector3.one * fallbackScale;

        return Vector3.one * (maxSize / currentMax);
    }

    // ════════════════════════════════════════════════
    // CLICK / BUTTON HANDLERS
    // ════════════════════════════════════════════════

    public void OnInfoColliderClicked()
    {
        if (model3DData == null) return;

        if (Model3DController.Instance != null)
        {
            Model3DController.Instance.ShowModel3DInfoWithPrefab(model3DData, thumbnailTexture, this);
            if (showDebug) Debug.Log($"[Model3DPrefab] Info clicked: {model3DData.name}");
        }
    }

    private void OnTransformButtonClicked()
    {
        if (model3DData == null || gizmo == null) return;

        gizmo.OnTransformChanged -= OnGizmoTransformChanged;
        gizmo.OnTransformChanged += OnGizmoTransformChanged;
        gizmo.Activate();

        if (Model3DTransformEditPopup.Instance != null)
        {
            Model3DTransformEditPopup.Instance.Show(model3DData.id, model3DData, this);
            Model3DTransformEditPopup.Instance.onGizmoModeChanged.Invoke(gizmo.currentMode.ToString());
        }
    }

    private void OnRemoveButtonClicked()
    {
        if (model3DData == null) return;

        if (Model3DRemoveConfirmPopup.Instance != null)
            Model3DRemoveConfirmPopup.Instance.Show(model3DData, ConfirmRemoveModel3D);
        else
            ConfirmRemoveModel3D();
    }

    private void ConfirmRemoveModel3D()
    {
        if (model3DData == null) return;

        Model3DTransformEditPopup.Instance?.Hide();

        if (APIManager.Instance != null)
        {
            APIManager.Instance.RemovePaintingOrModel(
                model3DData.id, "model3d",
                () =>
                {
                    NotifyGalleryItemRemoved(model3DData.id);
                    Destroy(gameObject);
                    Model3DPrefabManager.Instance?.ReloadAllModels();
                },
                (error) => Debug.LogError($"[Model3DPrefab] Remove failed: {error}")
            );
        }
    }

    private void NotifyGalleryItemRemoved(int id)
    {
        Model3DGalleryContainer gallery = FindObjectOfType<Model3DGalleryContainer>();
        gallery?.OnModel3DRemovedFromScene(id);
    }

    private void OnGizmoTransformChanged(Vector3 position, Vector3 rotation)
    {
        Model3DTransformEditPopup.Instance?.UpdateFromGizmo(position, rotation);
    }

    // ════════════════════════════════════════════════
    // TRANSFORM
    // ════════════════════════════════════════════════

    public void ApplyTransformFromData(Model3D data)
    {
        if (data == null) return;
        model3DData = data;

        if (data.position != null) transform.position    = data.position.ToVector3();
        if (data.rotate   != null) transform.eulerAngles = data.rotate.ToVector3();

        if (loadedGLBObject != null)
            loadedGLBObject.transform.localScale = CalculateTargetScale();
    }

    public void UpdateDataFromTransform()
    {
        if (model3DData == null) return;

        model3DData.position ??= new Position();
        model3DData.position.x = transform.position.x;
        model3DData.position.y = transform.position.y;
        model3DData.position.z = transform.position.z;

        model3DData.rotate ??= new Rotation();
        model3DData.rotate.x = transform.eulerAngles.x;
        model3DData.rotate.y = transform.eulerAngles.y;
        model3DData.rotate.z = transform.eulerAngles.z;

        if (loadedGLBObject != null)
        {
            model3DData.size ??= new Size();
            model3DData.size.x = loadedGLBObject.transform.localScale.x;
            model3DData.size.y = loadedGLBObject.transform.localScale.y;
            model3DData.size.z = loadedGLBObject.transform.localScale.z;
        }
    }

    // ════════════════════════════════════════════════
    // PUBLIC GETTERS
    // ════════════════════════════════════════════════

    public Model3D               GetData()      => model3DData;
    public Texture2D             GetThumbnail() => thumbnailTexture;
    public GameObject            GetLoadedGLB() => loadedGLBObject;
    public RuntimeTransformGizmo GetGizmo()     => gizmo;
}