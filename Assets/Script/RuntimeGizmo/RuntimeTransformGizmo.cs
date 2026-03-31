using UnityEngine;
using System;

public class RuntimeTransformGizmo : MonoBehaviour
{
    [Header("Gizmo Settings")]
    [SerializeField] private float gizmoSize = 2f;
    [SerializeField] private float lineWidth = 0.5f;
    [SerializeField] private float circleWidth = 0.15f;
    [SerializeField] private float hoverRadius = 0.15f;
    [SerializeField] private bool scaleWithDistance = true;

    [Header("Colors")]
    [SerializeField] private Color xAxisColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color yAxisColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color zAxisColor = new Color(0.2f, 0.2f, 1f, 1f);
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color hoverColor = Color.white;

    [Header("Interaction")]
    [SerializeField] private float dragSensitivity = 1f;
    [SerializeField] private float rotateSensitivity = 180f;
    [SerializeField] private bool snapEnabled = false;
    [SerializeField] private float moveSnapValue = 0.5f;
    [SerializeField] private float rotateSnapValue = 15f;

    [Header("References")]
    [SerializeField] private Camera renderCamera;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // State
    public TransformMode currentMode = TransformMode.Move;
    public TransformSpace currentSpace = TransformSpace.Local;
    private GizmoAxis hoveredAxis = GizmoAxis.None;
    private GizmoAxis selectedAxis = GizmoAxis.None;
    private bool isDragging = false;
    private bool isActive = false;

    // GL Material
    private Material glMaterial;

    // Drag state
    private Vector3 dragStartMousePos;
    private Vector3 dragStartPosition;
    private Vector3 dragStartRotation;
    private Plane dragPlane;

    // Camera control reference
    private CameraFollow cameraFollow;

    // Callbacks
    // NOTE: Không dùng Action<Vector3,Vector3> vì IL2CPP WebGL không AOT-compile
    // generic delegate với value type (struct) đúng cách → "function signature mismatch".
    // Subscriber đọc position/rotation trực tiếp từ gizmo.transform.
    public event Action OnTransformChanged;
    public event Action OnDeactivated;

    // Public property to check if gizmo is active
    public bool IsActive => isActive;

    private const int CIRCLE_SEGMENTS = 64;

    #region Unity Lifecycle

    private void Awake()
    {
        if (renderCamera == null)
            renderCamera = Camera.main;

        if (renderCamera != null)
        {
            cameraFollow = renderCamera.GetComponent<CameraFollow>();
            if (cameraFollow == null && showDebug)
                Debug.LogWarning("[RuntimeTransformGizmo] CameraFollow component not found on camera!");
        }

        CreateGLMaterial();
        isActive = false;
        enabled = false;

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] Awake on {gameObject.name}");
    }

    private void Update()
    {
        if (!isActive) return;

        if (renderCamera == null)
        {
            renderCamera = Camera.main;
            if (renderCamera == null)
            {
                Debug.LogError($"[RuntimeTransformGizmo] Cannot find Camera.main!");
                return;
            }
        }

        if (!isDragging)
            UpdateHover();

        HandleInput();
    }

    private void OnRenderObject()
    {
        if (!isActive || glMaterial == null) return;

        float size = CalculateGizmoSize();
        Vector3 origin = transform.position;

        if (currentMode == TransformMode.Move)
        {
            glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            Quaternion rotation = transform.rotation;
            DrawGLLine(origin, origin + rotation * Vector3.right * size,   GetAxisColor(GizmoAxis.X, xAxisColor), GetAxisWidth(GizmoAxis.X));
            DrawGLLine(origin, origin + rotation * Vector3.up * size,      GetAxisColor(GizmoAxis.Y, yAxisColor), GetAxisWidth(GizmoAxis.Y));
            DrawGLLine(origin, origin + rotation * Vector3.forward * size, GetAxisColor(GizmoAxis.Z, zAxisColor), GetAxisWidth(GizmoAxis.Z));

            GL.End();
            GL.PopMatrix();
        }
        else if (currentMode == TransformMode.Rotate)
        {
            DrawRotateCircle(origin, size, Vector3.right,   GetAxisColor(GizmoAxis.X, xAxisColor), GetCircleWidth(GizmoAxis.X));
            DrawRotateCircle(origin, size, Vector3.up,      GetAxisColor(GizmoAxis.Y, yAxisColor), GetCircleWidth(GizmoAxis.Y));
            DrawRotateCircle(origin, size, Vector3.forward, GetAxisColor(GizmoAxis.Z, zAxisColor), GetCircleWidth(GizmoAxis.Z));
        }
    }

    private void OnDestroy()
    {
        if (glMaterial != null)
            Destroy(glMaterial);
    }

    #endregion

    #region GL Material Setup

    private void CreateGLMaterial()
    {
        if (glMaterial != null) return;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        glMaterial = new Material(shader);
        glMaterial.hideFlags = HideFlags.HideAndDontSave;
        glMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        glMaterial.SetInt("_ZWrite", 0);
        glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        if (showDebug)
            Debug.Log("[RuntimeTransformGizmo] GL Material created");
    }

    private void DrawGLLine(Vector3 start, Vector3 end, Color color, float width)
    {
        int segments = Mathf.Max(1, Mathf.RoundToInt(width * 10f));
        for (int i = 0; i < segments; i++)
        {
            float offset = (i - segments / 2f) * 0.01f;
            Vector3 perpendicular = Vector3.Cross((end - start).normalized, renderCamera.transform.forward) * offset;
            GL.Color(color);
            GL.Vertex3(start.x + perpendicular.x, start.y + perpendicular.y, start.z + perpendicular.z);
            GL.Vertex3(end.x + perpendicular.x, end.y + perpendicular.y, end.z + perpendicular.z);
        }
    }

    private void DrawRotateCircle(Vector3 center, float radius, Vector3 normal, Color color, float width)
    {
        // Build orthonormal basis for the circle plane
        Vector3 tempUp = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
        Vector3 right = Vector3.Cross(normal, tempUp).normalized;
        Vector3 up    = Vector3.Cross(right, normal).normalized;

        int thickness = Mathf.Max(1, Mathf.RoundToInt(width * 10f));

        glMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        GL.Color(color);

        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            float a1 = i       * Mathf.PI * 2f / CIRCLE_SEGMENTS;
            float a2 = (i + 1) * Mathf.PI * 2f / CIRCLE_SEGMENTS;

            Vector3 p1 = center + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * radius;
            Vector3 p2 = center + (right * Mathf.Cos(a2) + up * Mathf.Sin(a2)) * radius;

            for (int j = 0; j < thickness; j++)
            {
                float offset = (j - thickness / 2f) * 0.01f;
                Vector3 perp = Vector3.Cross((p2 - p1).normalized, renderCamera.transform.forward) * offset;
                GL.Vertex(p1 + perp);
                GL.Vertex(p2 + perp);
            }
        }

        GL.End();
        GL.PopMatrix();
    }

    private float GetAxisWidth(GizmoAxis axis)
    {
        if (selectedAxis == axis) return lineWidth * 4f;
        if (hoveredAxis == axis)  return lineWidth * 3f;
        return lineWidth;
    }

    private float GetCircleWidth(GizmoAxis axis)
    {
        if (selectedAxis == axis) return circleWidth * 2f;
        if (hoveredAxis == axis)  return circleWidth * 1.4f;
        return circleWidth;
    }

    #endregion

    #region Public Methods

    public void Activate()
    {
        isActive = true;
        enabled = true;
        isDragging = false;
        selectedAxis = GizmoAxis.None;
        hoveredAxis = GizmoAxis.None;

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] ACTIVATED for: {gameObject.name}");
    }

    public void Deactivate()
    {
        isActive = false;
        enabled = false;
        isDragging = false;
        selectedAxis = GizmoAxis.None;
        hoveredAxis = GizmoAxis.None;

        OnDeactivated?.Invoke();

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] DEACTIVATED for: {gameObject.name}");
    }

    public void SetMode(TransformMode mode)
    {
        currentMode = mode;
        selectedAxis = GizmoAxis.None;
        hoveredAxis = GizmoAxis.None;

        Model3DTransformEditPopup model3DTransformEditPopup = FindObjectOfType<Model3DTransformEditPopup>();
        if (model3DTransformEditPopup != null)
            model3DTransformEditPopup.onGizmoModeChanged?.Invoke(mode.ToString());

        PaintingTransformEditPopup paintingTransformEditPopup = FindObjectOfType<PaintingTransformEditPopup>();
        if (paintingTransformEditPopup != null)
            paintingTransformEditPopup.onGizmoModeChanged?.Invoke(mode.ToString());

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] Mode: {mode}");
    }

    public void SetSpace(TransformSpace space)
    {
        currentSpace = space;
        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] Space: {space}");
    }

    public void SetSnapEnabled(bool enabled)
    {
        snapEnabled = enabled;
    }

    #endregion

    #region Input Handling

    private void HandleInput()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            var currentSelected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && currentSelected.GetComponent<TMPro.TMP_InputField>() != null)
                return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (hoveredAxis != GizmoAxis.None)
                StartDrag();
        }

        if (isDragging)
            UpdateDrag();

        if (Input.GetMouseButtonUp(0) && isDragging)
            EndDrag();

        if (Input.GetKeyDown(KeyCode.Q))
            SetMode(TransformMode.Move);
        else if (Input.GetKeyDown(KeyCode.E))
            SetMode(TransformMode.Rotate);
    }

    private void StartDrag()
    {
        isDragging = true;
        selectedAxis = hoveredAxis;
        dragStartMousePos = Input.mousePosition;
        dragStartPosition = transform.position;
        dragStartRotation = transform.eulerAngles;

        if (currentMode == TransformMode.Move)
        {
            Vector3 normal = GetDragPlaneNormal();
            dragPlane = new Plane(normal, transform.position);
        }

        if (cameraFollow != null)
        {
            cameraFollow.enabled = false;
            if (showDebug) Debug.Log("[RuntimeTransformGizmo] CameraFollow disabled");
        }

        if (showDebug)
        {
            Debug.Log($"[RuntimeTransformGizmo] ========== START DRAG ==========");
            Debug.Log($"[RuntimeTransformGizmo] Mode: {currentMode}, Axis: {selectedAxis}");
            Debug.Log($"[RuntimeTransformGizmo] Start position: {dragStartPosition}");
        }
    }

    private void UpdateDrag()
    {
        switch (currentMode)
        {
            case TransformMode.Move:   UpdateMoveDrag();   break;
            case TransformMode.Rotate: UpdateRotateDrag(); break;
        }
    }

    private void EndDrag()
    {
        if (showDebug)
        {
            Debug.Log($"[RuntimeTransformGizmo] ========== END DRAG ==========");
            Debug.Log($"[RuntimeTransformGizmo] Final position: {transform.position}");
            Debug.Log($"[RuntimeTransformGizmo] Final rotation: {transform.eulerAngles}");
        }

        isDragging = false;
        selectedAxis = GizmoAxis.None;

        if (cameraFollow != null)
        {
            cameraFollow.enabled = true;
            if (showDebug) Debug.Log("[RuntimeTransformGizmo] CameraFollow enabled");
        }
    }

    #endregion

    #region Hover Detection

    private void UpdateHover()
    {
        if (isDragging) return;

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            var currentSelected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && currentSelected.GetComponent<TMPro.TMP_InputField>() != null)
            {
                hoveredAxis = GizmoAxis.None;
                return;
            }
        }

        if (renderCamera == null) { hoveredAxis = GizmoAxis.None; return; }

        Ray ray = renderCamera.ScreenPointToRay(Input.mousePosition);
        float size = CalculateGizmoSize();
        Vector3 origin = transform.position;
        float screenHoverRadius = hoverRadius * 100f;

        GizmoAxis closestAxis = GizmoAxis.None;
        float closestDist = float.MaxValue;

        if (currentMode == TransformMode.Move)
        {
            Quaternion rotation = transform.rotation;
            float dx = GetScreenDistanceToAxis(ray, origin, rotation * Vector3.right,   size);
            float dy = GetScreenDistanceToAxis(ray, origin, rotation * Vector3.up,      size);
            float dz = GetScreenDistanceToAxis(ray, origin, rotation * Vector3.forward, size);

            if (dx < screenHoverRadius && dx < closestDist) { closestDist = dx; closestAxis = GizmoAxis.X; }
            if (dy < screenHoverRadius && dy < closestDist) { closestDist = dy; closestAxis = GizmoAxis.Y; }
            if (dz < screenHoverRadius && dz < closestDist) { closestDist = dz; closestAxis = GizmoAxis.Z; }
        }
        else if (currentMode == TransformMode.Rotate)
        {
            float dx = GetScreenDistanceToCircle(origin, Vector3.right,   size);
            float dy = GetScreenDistanceToCircle(origin, Vector3.up,      size);
            float dz = GetScreenDistanceToCircle(origin, Vector3.forward, size);

            if (dx < screenHoverRadius && dx < closestDist) { closestDist = dx; closestAxis = GizmoAxis.X; }
            if (dy < screenHoverRadius && dy < closestDist) { closestDist = dy; closestAxis = GizmoAxis.Y; }
            if (dz < screenHoverRadius && dz < closestDist) { closestDist = dz; closestAxis = GizmoAxis.Z; }
        }

        hoveredAxis = closestAxis;
    }

    private float GetScreenDistanceToAxis(Ray ray, Vector3 origin, Vector3 direction, float length)
    {
        Vector3 dir = direction.normalized;
        int samples = 20;
        float minDist = float.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 point = origin + dir * (t * length);
            Vector3 screenPoint = renderCamera.WorldToScreenPoint(point);
            if (screenPoint.z < 0) continue;

            float dist = Vector2.Distance(new Vector2(screenPoint.x, screenPoint.y), Input.mousePosition);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    private float GetScreenDistanceToCircle(Vector3 center, Vector3 normal, float radius)
    {
        Vector3 tempUp = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
        Vector3 right  = Vector3.Cross(normal, tempUp).normalized;
        Vector3 up     = Vector3.Cross(right, normal).normalized;

        float minDist = float.MaxValue;
        Vector2 mousePos = Input.mousePosition;

        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            float angle = i * Mathf.PI * 2f / CIRCLE_SEGMENTS;
            Vector3 point = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            Vector3 screenPoint = renderCamera.WorldToScreenPoint(point);
            if (screenPoint.z < 0) continue;

            float dist = Vector2.Distance(new Vector2(screenPoint.x, screenPoint.y), mousePos);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    #endregion

    #region Move Drag

    private void UpdateMoveDrag()
    {
        if (renderCamera == null) { Debug.LogError("[RuntimeTransformGizmo] renderCamera is null!"); return; }

        Ray ray = renderCamera.ScreenPointToRay(Input.mousePosition);
        if (!dragPlane.Raycast(ray, out float enter)) return;

        Vector3 hitPoint = ray.GetPoint(enter);
        Vector3 dragStartHitPoint = GetDragStartHitPoint();
        Vector3 delta = hitPoint - dragStartHitPoint;

        Quaternion rotation = transform.rotation;
        Vector3 constrainedDelta = ConstrainToAxis(delta, rotation);

        if (snapEnabled)
            constrainedDelta = SnapVector(constrainedDelta, moveSnapValue);

        transform.position = dragStartPosition + constrainedDelta;
        OnTransformChanged?.Invoke();
    }

    private Vector3 GetDragStartHitPoint()
    {
        Ray ray = renderCamera.ScreenPointToRay(dragStartMousePos);
        dragPlane.Raycast(ray, out float enter);
        return ray.GetPoint(enter);
    }

    private Vector3 ConstrainToAxis(Vector3 delta, Quaternion rotation)
    {
        switch (selectedAxis)
        {
            case GizmoAxis.X: Vector3 xAxis = rotation * Vector3.right;   return xAxis * Vector3.Dot(delta, xAxis);
            case GizmoAxis.Y: Vector3 yAxis = rotation * Vector3.up;      return yAxis * Vector3.Dot(delta, yAxis);
            case GizmoAxis.Z: Vector3 zAxis = rotation * Vector3.forward; return zAxis * Vector3.Dot(delta, zAxis);
            default: return delta;
        }
    }

    private Vector3 SnapVector(Vector3 vector, float snapValue)
    {
        return new Vector3(
            Mathf.Round(vector.x / snapValue) * snapValue,
            Mathf.Round(vector.y / snapValue) * snapValue,
            Mathf.Round(vector.z / snapValue) * snapValue
        );
    }

    #endregion

    #region Rotate Drag

    private void UpdateRotateDrag()
    {
        Vector3 rotationAxis = GetRotationAxis();

        // Project gizmo center to screen space
        Vector3 centerScreen = renderCamera.WorldToScreenPoint(transform.position);
        Vector2 center2D = new Vector2(centerScreen.x, centerScreen.y);

        Vector2 startDir   = (Vector2)dragStartMousePos        - center2D;
        Vector2 currentDir = (Vector2)(Vector3)Input.mousePosition - center2D;

        if (startDir.sqrMagnitude < 1f || currentDir.sqrMagnitude < 1f)
            return;

        // Signed angle between start and current mouse direction around gizmo center
        float angle = Vector2.SignedAngle(startDir, currentDir);

        // Flip sign if camera faces the same direction as the axis
        if (Vector3.Dot(rotationAxis, renderCamera.transform.forward) > 0)
            angle = -angle;

        if (snapEnabled)
            angle = Mathf.Round(angle / rotateSnapValue) * rotateSnapValue;

        Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);
        transform.rotation = rotation * Quaternion.Euler(dragStartRotation);

        OnTransformChanged?.Invoke();
    }

    private Vector3 GetRotationAxis()
    {
        switch (selectedAxis)
        {
            case GizmoAxis.X: return Vector3.right;
            case GizmoAxis.Y: return Vector3.up;
            case GizmoAxis.Z: return Vector3.forward;
            default:          return Vector3.up;
        }
    }

    #endregion

    #region Utilities

    private Color GetAxisColor(GizmoAxis axis, Color baseColor)
    {
        if (selectedAxis == axis) return selectedColor;
        if (hoveredAxis == axis)  return hoverColor;
        return baseColor;
    }

    private float CalculateGizmoSize()
    {
        if (!scaleWithDistance || renderCamera == null) return gizmoSize;
        float distance = Vector3.Distance(renderCamera.transform.position, transform.position);
        float scale = distance / 10f;
        // Khi gần camera, tăng nhẹ kích thước vòng tròn rotate để dễ thao tác
        if (currentMode == TransformMode.Rotate)
            scale = Mathf.Max(scale, 0.6f);
        return gizmoSize * scale;
    }

    private Vector3 GetDragPlaneNormal()
    {
        if (renderCamera == null) return Vector3.up;

        // Move always uses local rotation
        Quaternion rotation = transform.rotation;
        Vector3 cameraForward = renderCamera.transform.forward;

        Vector3 axis;
        switch (selectedAxis)
        {
            case GizmoAxis.X: axis = rotation * Vector3.right;   break;
            case GizmoAxis.Y: axis = rotation * Vector3.up;      break;
            case GizmoAxis.Z: axis = rotation * Vector3.forward; break;
            default: return cameraForward;
        }

        // Drag plane normal = component of camera forward perpendicular to the move axis.
        // This creates a camera-facing plane that contains the axis, so raycast drag
        // stays accurate regardless of camera angle — same as Unity's Scene View gizmo.
        Vector3 normal = cameraForward - Vector3.Dot(cameraForward, axis) * axis;
        if (normal.sqrMagnitude < 0.001f)
            normal = Vector3.Cross(axis, renderCamera.transform.right);

        return normal.normalized;
    }

    #endregion
}

public enum TransformMode { Move, Rotate, Scale }
public enum TransformSpace { Local, World }
public enum GizmoAxis { None, X, Y, Z }
