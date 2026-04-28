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
    [SerializeField] private Model3DRotate model3DRotate;

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

    [Header("Teleport")]
    [Tooltip("Điểm teleport tự động trượt trên đường tròn bán kính 2 quanh prefab, luôn hướng về phía Local Player")]
    [SerializeField] private Transform telePoint;
    [SerializeField] private float     telePointMoveSpeed     = 10f;
    [SerializeField] private float     telePointRotationSpeed = 10f;

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

    // TelePoint
    private Camera _mainCamera;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (gizmo == null)
            gizmo = GetComponent<RuntimeTransformGizmo>();

        if (model3DRotate == null)
            model3DRotate = GetComponentInChildren<Model3DRotate>();
    }

    private void OnEnable()
    {
        AdminModeManager.OnAdminModeChanged += ApplyAdminMode;
        ApplyAdminMode(AdminModeManager.Instance != null && AdminModeManager.Instance.IsAdmin);
    }

    private void OnDisable()
    {
        AdminModeManager.OnAdminModeChanged -= ApplyAdminMode;
    }

    private void ApplyAdminMode(bool isAdmin)
    {
        if (transformButton != null) transformButton.gameObject.SetActive(isAdmin);
        if (removeButton    != null) removeButton.gameObject.SetActive(isAdmin);
    }

    private void Start()
    {
        onDisplayButton += ShowButton;
        _mainCamera = Camera.main;

        if (Model3DClickManager.Instance != null)
            Model3DClickManager.Instance.RegisterModel3D(this);
    }

    private void Update()
    {
        UpdateOutlineLerp();
        UpdateTelePoint();
    }

    private void OnDestroy()
    {
        if (transformButton != null)
            transformButton.onClick.RemoveListener(OnTransformButtonClicked);

        if (removeButton != null)
            removeButton.onClick.RemoveListener(OnRemoveButtonClicked);

        if (gizmo != null)
        {
            gizmo.OnTransformChanged -= OnGizmoTransformChanged;
            gizmo.OnDeactivated      -= OnGizmoDeactivated;
        }

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
    // TELEPOINT
    // ════════════════════════════════════════════════

    private void UpdateTelePoint()
    {
        if (telePoint == null) return;
        if (_mainCamera == null) { _mainCamera = Camera.main; return; }

        Vector3 camPos   = _mainCamera.transform.position;
        Vector3 modelPos = transform.position;

        // Hướng từ tâm model → camera trên mặt phẳng XZ
        Vector3 dir = new Vector3(camPos.x - modelPos.x, 0f, camPos.z - modelPos.z);
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();

        // Vị trí đích trên đường tròn bán kính 2, y = modelPos.y (world)
        Vector3 worldTarget = new Vector3(modelPos.x + dir.x * 2f, modelPos.y, modelPos.z + dir.z * 2f);

        // Chuyển sang local space của prefab, ép y = 0
        Vector3 localTarget = transform.InverseTransformPoint(worldTarget);
        localTarget.y = 0f;

        // Smooth slide
        telePoint.localPosition = Vector3.Lerp(
            telePoint.localPosition,
            localTarget,
            Time.deltaTime * telePointMoveSpeed
        );

        // Billboard: nhìn về phía camera, trục Y cố định
        Vector3 lookDir = camPos - telePoint.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            telePoint.rotation = Quaternion.Slerp(
                telePoint.rotation,
                targetRot,
                Time.deltaTime * telePointRotationSpeed
            );
        }
    }

    /// <summary>
    /// Set rotation nhìn vào model ngay trước khi teleport — player sẽ đối mặt model sau tele.
    /// </summary>
    public void TeleportPlayerToModel()
    {
        if (telePoint == null || PlayerTeleportManager.Instance == null) return;

        Vector3 toModel = transform.position - telePoint.position;
        toModel.y = 0f;
        if (toModel.sqrMagnitude > 0.001f)
            telePoint.rotation = Quaternion.LookRotation(toModel);

        PlayerTeleportManager.Instance.TeleportToPoint(telePoint);

        if (showDebug)
            Debug.Log($"[Model3DPrefab] TeleportPlayerToModel: {telePoint.position}");
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

            // Chờ vài frame để GLTFast apply materials xong.
            // WaitForEndOfFrame KHÔNG được dùng trên WebGL (không hỗ trợ).
            yield return null;
            yield return null;
            yield return null;
            yield return null;

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
            // WebGL: AddComponent<MeshCollider>() (shared generic) = invoke_iiii trong WASM,
            // kết hợp với PhysX WASM backend xử lý non-convex mesh → "function signature mismatch".
            // Trên WebGL dùng infoCollider pre-assigned trên prefab thay vì tạo mới.
#if !UNITY_WEBGL
            if (mr.GetComponent<MeshCollider>() == null)
            {
                MeshCollider mc = mr.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;

                if (mf.sharedMesh.vertexCount <= 255)
                {
                    mc.convex    = true;
                    mc.isTrigger = true;
                }

                if (showDebug)
                    Debug.Log($"[Model3DPrefab] Collider added: {mr.gameObject.name} | convex={mc.convex}");
            }
#endif

            // ── Outline material ──────────────────────
            if (canSetupOutline)
            {
                //  Lấy materials hiện tại — dùng mr.materials (instance) thay vì sharedMaterials
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

        // ── WebGL: set layer cho infoCollider ─────────
        // Trên WebGL không có MeshCollider động, infoCollider là collider duy nhất.
        // Phải set đúng layer "Model3D" để Model3DClickManager raycast trúng.
#if UNITY_WEBGL
        if (infoCollider != null)
        {
            infoCollider.gameObject.layer = LayerMask.NameToLayer("Model3D");
            infoCollider.enabled = true;
            if (showDebug)
                Debug.Log($"[Model3DPrefab] WebGL: infoCollider layer set to Model3D — {infoCollider.gameObject.name}");
        }
#endif

        // ── Outline ready ─────────────────────────────
        if (canSetupOutline && outlineMatInstances.Count > 0)
        {
            currentOutlineVal = outlineScaleIdle;
            outlineReady      = true;

            if (showDebug)
                Debug.Log($"[Model3DPrefab]  Outline READY — {outlineMatInstances.Count} instance(s)");
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

        // Dừng rotate để người dùng move/rotate bằng gizmo chính xác
        model3DRotate?.Stop();

        gizmo.OnTransformChanged -= OnGizmoTransformChanged;
        gizmo.OnTransformChanged += OnGizmoTransformChanged;
        gizmo.OnDeactivated      -= OnGizmoDeactivated;
        gizmo.OnDeactivated      += OnGizmoDeactivated;
        gizmo.Activate();

        if (Model3DTransformEditPopup.Instance != null)
        {
            Model3DTransformEditPopup.Instance.Show(model3DData.id, model3DData, this);
            Model3DTransformEditPopup.Instance.onGizmoModeChanged.Invoke(gizmo.currentMode.ToString());
        }
    }

    private void OnGizmoDeactivated()
    {
        model3DRotate?.Resume();
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

    private void OnGizmoTransformChanged()
    {
        Model3DTransformEditPopup.Instance?.UpdateFromGizmo(gizmo.transform.position, gizmo.transform.eulerAngles);
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

    public Model3D               GetData()           => model3DData;
    public Texture2D             GetThumbnail()      => thumbnailTexture;
    public GameObject            GetLoadedGLB()      => loadedGLBObject;
    public RuntimeTransformGizmo GetGizmo()          => gizmo;
    public Transform             GetTeleportPoint()  => telePoint;
}