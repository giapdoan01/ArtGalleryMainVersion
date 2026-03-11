using UnityEngine;

public class InfoPanelManager : MonoBehaviour
{
    public static InfoPanelManager Instance { get; private set; }

    [Header("Info Panels")]
    [SerializeField] private PaintingInfo paintingInfo;
    [SerializeField] private Model3DInfo model3DInfo;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true; 

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[InfoPanelManager]  Instance created");
        }
        else
        {
            Debug.LogWarning("[InfoPanelManager]  Duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }

        if (paintingInfo == null)
        {
            Debug.LogError("[InfoPanelManager]  PaintingInfo is NULL! Please assign in Inspector.");
        }
        else
        {
            Debug.Log($"[InfoPanelManager]  PaintingInfo assigned: {paintingInfo.name}");
        }

        if (model3DInfo == null)
        {
            Debug.LogError("[InfoPanelManager]  Model3DInfo is NULL! Please assign in Inspector.");
        }
        else
        {
            Debug.Log($"[InfoPanelManager]  Model3DInfo assigned: {model3DInfo.name}");
        }
    }

    //  Show Painting Info
    public void ShowPaintingInfo(Painting painting, Texture2D texture = null)
    {
        Debug.Log("[InfoPanelManager]  ShowPaintingInfo() called");

        if (paintingInfo == null)
        {
            Debug.LogError("[InfoPanelManager]  PaintingInfo is not assigned!");
            return;
        }

        if (painting == null)
        {
            Debug.LogError("[InfoPanelManager]  Painting parameter is null!");
            return;
        }

        if (model3DInfo != null)
        {
            Debug.Log("[InfoPanelManager]  Hiding Model3D info...");
            model3DInfo.HideInfo();
        }

        Debug.Log($"[InfoPanelManager]  Showing Painting: {painting.name}");
        paintingInfo.ShowInfo(painting, texture);
    }

    public void ShowPaintingInfoById(int paintingId)
    {
        Debug.Log($"[InfoPanelManager]  ShowPaintingInfoById({paintingId}) called");

        if (paintingInfo == null)
        {
            Debug.LogError("[InfoPanelManager]  PaintingInfo is not assigned!");
            return;
        }

        if (model3DInfo != null)
        {
            Debug.Log("[InfoPanelManager]  Hiding Model3D info...");
            model3DInfo.HideInfo();
        }

        paintingInfo.ShowInfoById(paintingId);
    }

    public void ShowModel3DInfo(Model3D model3D, Texture2D texture = null)
    {
        Debug.Log("[InfoPanelManager]  ShowModel3DInfo() called");

        if (model3DInfo == null)
        {
            Debug.LogError("[InfoPanelManager]  Model3DInfo is not assigned!");
            return;
        }

        if (model3D == null)
        {
            Debug.LogError("[InfoPanelManager]  Model3D parameter is null!");
            return;
        }

        if (paintingInfo != null)
        {
            Debug.Log("[InfoPanelManager]  Hiding Painting info...");
            paintingInfo.HideInfo();
        }

        Debug.Log($"[InfoPanelManager]  Showing Model3D: {model3D.name}");
        model3DInfo.ShowInfo(model3D, texture);
    }

    public void ShowModel3DInfoById(int modelId)
    {
        Debug.Log($"[InfoPanelManager]  ShowModel3DInfoById({modelId}) called");

        if (model3DInfo == null)
        {
            Debug.LogError("[InfoPanelManager]  Model3DInfo is not assigned!");
            return;
        }

        if (paintingInfo != null)
        {
            Debug.Log("[InfoPanelManager]  Hiding Painting info...");
            paintingInfo.HideInfo();
        }

        model3DInfo.ShowInfoById(modelId);
    }
    public void HideAllInfoPanels()
    {
        Debug.Log("[InfoPanelManager]  HideAllInfoPanels() called");

        if (paintingInfo != null)
        {
            paintingInfo.HideInfo();
            Debug.Log("[InfoPanelManager]  Painting info hidden");
        }

        if (model3DInfo != null)
        {
            model3DInfo.HideInfo();
            Debug.Log("[InfoPanelManager]  Model3D info hidden");
        }
    }

    public bool IsPaintingInfoVisible()
    {
        if (paintingInfo == null) return false;

        GameObject panel = paintingInfo.transform.Find("InfoPanel")?.gameObject;
        if (panel != null)
        {
            return panel.activeSelf;
        }
        
        return false;
    }

    public bool IsModel3DInfoVisible()
    {
        if (model3DInfo == null) return false;

        GameObject panel = model3DInfo.transform.Find("InfoPanel")?.gameObject;
        if (panel != null)
        {
            return panel.activeSelf;
        }
        
        return false;
    }

    public bool IsAnyInfoVisible()
    {
        return IsPaintingInfoVisible() || IsModel3DInfoVisible();
    }

    [ContextMenu("Test Show Painting Info")]
    public void TestShowPaintingInfo()
    {
        Debug.Log("[InfoPanelManager]  Test Show Painting Info");
        
        if (APIManager.Instance != null && APIManager.Instance.apiResponse != null)
        {
            var paintings = APIManager.Instance.apiResponse.data.paintings;
            if (paintings != null && paintings.Count > 0)
            {
                ShowPaintingInfo(paintings[0]);
            }
            else
            {
                Debug.LogError("[InfoPanelManager] No paintings available for test");
            }
        }
        else
        {
            Debug.LogError("[InfoPanelManager] APIManager not ready");
        }
    }

    [ContextMenu("Test Show Model3D Info")]
    public void TestShowModel3DInfo()
    {
        Debug.Log("[InfoPanelManager]  Test Show Model3D Info");
        
        if (APIManager.Instance != null && APIManager.Instance.apiResponse != null)
        {
            var models = APIManager.Instance.apiResponse.data.model3ds;
            if (models != null && models.Count > 0)
            {
                ShowModel3DInfo(models[0]);
            }
            else
            {
                Debug.LogError("[InfoPanelManager] No models available for test");
            }
        }
        else
        {
            Debug.LogError("[InfoPanelManager] APIManager not ready");
        }
    }

    [ContextMenu("Test Hide All")]
    public void TestHideAll()
    {
        Debug.Log("[InfoPanelManager]  Test Hide All");
        HideAllInfoPanels();
    }

    [ContextMenu("Check Status")]
    public void CheckStatus()
    {
        Debug.Log("=== InfoPanelManager Status ===");
        Debug.Log($"Painting Info Visible: {IsPaintingInfoVisible()}");
        Debug.Log($"Model3D Info Visible: {IsModel3DInfoVisible()}");
        Debug.Log($"Any Info Visible: {IsAnyInfoVisible()}");
        Debug.Log("===============================");
    }
}
