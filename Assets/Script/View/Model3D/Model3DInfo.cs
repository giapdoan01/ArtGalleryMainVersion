using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text.RegularExpressions;

public class Model3DInfo : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject      infoPanel;
    [SerializeField] private Image           modelImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI authorText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button          closeButton;

    [Header("Preview 3D Button")]
    [SerializeField] private Button          previewButton;
    [SerializeField] private PreviewModel3D  previewModel3D;

    [Header("Panel Integration")]
    [SerializeField] private PanelListItemVisitor panelListItemVisitor;   //  thêm mới

    [Header("Settings")]
    [SerializeField] private string languageCode = "vi";

    [Header("Text Processing")]
    [SerializeField] private bool removeHtmlTags         = true;
    [SerializeField] private bool convertToTMProRichText = false;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // Event thông báo cho Model3DItem highlight khi info đang hiển thị
    public static event System.Action<int> OnModel3DInfoShown;
    public static event System.Action      OnModel3DInfoHidden;

    private Model3D       currentModel3D;
    private Texture2D     currentTexture;
    private Model3DPrefab currentPrefab;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (previewModel3D == null)
        {
#pragma warning disable CS0618
            previewModel3D = FindObjectOfType<PreviewModel3D>();
#pragma warning restore CS0618
        }

        //  Auto-find nếu chưa assign
        if (panelListItemVisitor == null)
        {
#pragma warning disable CS0618
            panelListItemVisitor = FindObjectOfType<PanelListItemVisitor>();
#pragma warning restore CS0618
        }

        // Đăng ký button ở Awake để tránh Start() chạy sau ShowInfo() và override interactable
        if (closeButton != null)
            closeButton.onClick.AddListener(HideInfo);

        if (previewButton != null)
        {
            previewButton.onClick.AddListener(OnPreviewButtonClicked);
            previewButton.interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (closeButton   != null) closeButton.onClick.RemoveListener(HideInfo);
        if (previewButton != null) previewButton.onClick.RemoveListener(OnPreviewButtonClicked);
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

    public void ShowInfo(Model3D model3D, Texture2D texture = null)
    {
        if (model3D == null) { Debug.LogError("[Model3DInfo] Model3D is null!"); return; }

        //  Báo PanelListItemVisitor hide ListItem/Chat trước
        if (panelListItemVisitor != null)
            panelListItemVisitor.HideForModel3DInfo();

#pragma warning disable CS0618
        PaintingInfo paintingInfo = FindObjectOfType<PaintingInfo>();
#pragma warning restore CS0618
        if (paintingInfo != null) paintingInfo.HideInfo();

        currentModel3D = model3D;
        currentTexture = texture;

        if (infoPanel != null)
            infoPanel.SetActive(true);

        OnModel3DInfoShown?.Invoke(model3D.id);

        DisplayImage(texture, model3D);
        DisplayName(model3D);
        DisplayAuthor(model3D);
        DisplayDescription(model3D);

        if (showDebug) Debug.Log($"[Model3DInfo] ShowInfo: {model3D.name}");
    }

    public void ShowInfoWithPrefab(Model3D model3D, Texture2D texture, Model3DPrefab prefab)
    {
        currentPrefab = prefab;
        ShowInfo(model3D, texture);
        StartCoroutine(SetPreviewButtonNextFrame(prefab != null));

        if (showDebug) Debug.Log($"[Model3DInfo] ShowInfoWithPrefab: prefab={prefab?.name ?? "null"}");
    }

    private IEnumerator SetPreviewButtonNextFrame(bool interactable)
    {
        yield return null;
        if (previewButton != null)
        {
            previewButton.interactable = interactable;
            if (showDebug) Debug.Log($"[Model3DInfo] previewButton.interactable = {interactable}");
        }
    }

    public void ShowInfoById(int modelId)
    {
        if (APIManager.Instance == null || APIManager.Instance.apiResponse == null)
        {
            Debug.LogError("[Model3DInfo] API data not ready!");
            return;
        }

        Model3D model3D = APIManager.Instance.GetModel3DById(modelId);
        if (model3D != null)
        {
            currentPrefab = null;
            if (previewButton != null) previewButton.interactable = false;

            if (!string.IsNullOrEmpty(model3D.path_url))
                StartCoroutine(LoadTextureAndShow(model3D));
            else
                ShowInfo(model3D, null);
        }
        else Debug.LogError($"[Model3DInfo] Model3D not found: ID {modelId}");
    }

    public void HideInfo()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        OnModel3DInfoHidden?.Invoke();

        if (previewModel3D != null)
            previewModel3D.Hide();

        currentPrefab = null;
        if (previewButton != null) previewButton.interactable = false;

        //  Báo PanelListItemVisitor restore lại ListItem/Chat
        if (panelListItemVisitor != null)
            panelListItemVisitor.RestoreAfterModel3DInfo();

        if (showDebug) Debug.Log("[Model3DInfo] HideInfo");
    }

    public void ToggleInfo()
    {
        if (infoPanel == null) return;
        if (infoPanel.activeSelf) HideInfo();
        else infoPanel.SetActive(true);
    }

    // ════════════════════════════════════════════════
    // PREVIEW BUTTON
    // ════════════════════════════════════════════════

    private void OnPreviewButtonClicked()
    {
        if (previewModel3D == null) { Debug.LogError("[Model3DInfo] PreviewModel3D is null!"); return; }
        if (currentPrefab  == null) { Debug.LogWarning("[Model3DInfo] currentPrefab is null!"); return; }

        Transform  target = currentPrefab.transform;
        GameObject glbObj = currentPrefab.GetLoadedGLB();

        if (glbObj != null) target = glbObj.transform;

        previewModel3D.Show(target);
        if (showDebug) Debug.Log($"[Model3DInfo] PreviewModel3D → {target.name}");
    }

    // ════════════════════════════════════════════════
    // DISPLAY
    // ════════════════════════════════════════════════

    private void DisplayImage(Texture2D texture, Model3D model3D)
    {
        if (modelImage == null) return;

        if (texture != null)
        {
            modelImage.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
        }
        else if (!string.IsNullOrEmpty(model3D.thumbnail_url))
            StartCoroutine(LoadThumbnail(model3D.thumbnail_url));
        else
            modelImage.sprite = null;
    }

    private void DisplayName(Model3D model3D)
    {
        if (nameText == null) return;
        string displayName = model3D.name;
        if (model3D.model3ds_lang != null)
        {
            LanguageData langData = GetLanguageData(model3D.model3ds_lang);
            if (langData != null && !string.IsNullOrEmpty(langData.name))
                displayName = langData.name;
        }
        nameText.text = ProcessHtmlText(displayName);
    }

    private void DisplayAuthor(Model3D model3D)
    {
        if (authorText == null) return;
        authorText.text = !string.IsNullOrEmpty(model3D.author)
            ? ProcessHtmlText(model3D.author) : "Không rõ";
    }

    private void DisplayDescription(Model3D model3D)
    {
        if (descriptionText == null) return;
        string description = "Không có mô tả";
        if (model3D.model3ds_lang != null)
        {
            LanguageData langData = GetLanguageData(model3D.model3ds_lang);
            if (langData != null && !string.IsNullOrEmpty(langData.description))
                description = langData.description;
        }
        descriptionText.text = ProcessHtmlText(description);
    }

    // ════════════════════════════════════════════════
    // TEXT PROCESSING
    // ════════════════════════════════════════════════

    private string ProcessHtmlText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return (convertToTMProRichText
            ? ConvertHtmlToTMPro(text)
            : removeHtmlTags ? RemoveHtmlTags(text) : text).Trim();
    }

    private string RemoveHtmlTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string result = Regex.Replace(text, @"<[^>]+>", string.Empty);
        result = DecodeHtmlEntities(result);
        return Regex.Replace(result, @"\s+", " ");
    }

    private string ConvertHtmlToTMPro(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string result = text;
        result = Regex.Replace(result, @"<b>(.*?)</b>",          "<b>$1</b>",  RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<i>(.*?)</i>",          "<i>$1</i>",  RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<u>(.*?)</u>",          "<u>$1</u>",  RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<strong>(.*?)</strong>", "<b>$1</b>",  RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<em>(.*?)</em>",        "<i>$1</i>",  RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<br\s*/?>",             "\n",         RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<p>(.*?)</p>",          "$1\n\n",     RegexOptions.IgnoreCase | RegexOptions.Singleline);
        result = Regex.Replace(result, @"<(?!/?[biu]>)[^>]+>",  string.Empty);
        result = DecodeHtmlEntities(result);
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        return result.Trim();
    }

    private string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text
            .Replace("&nbsp;", " ").Replace("&lt;",   "<")
            .Replace("&gt;",   ">").Replace("&amp;",  "&")
            .Replace("&quot;", "\"").Replace("&#39;", "'")
            .Replace("&apos;", "'");
    }

    private LanguageData GetLanguageData(Model3DLang model3DLang)
    {
        return languageCode.ToLower() switch { "vi" => model3DLang.vi, _ => model3DLang.vi };
    }

    // ════════════════════════════════════════════════
    // COROUTINES
    // ════════════════════════════════════════════════

    private IEnumerator LoadTextureAndShow(Model3D model3D)
    {
        using var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(model3D.path_url);
        yield return request.SendWebRequest();

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            ShowInfo(model3D, UnityEngine.Networking.DownloadHandlerTexture.GetContent(request));
        else { Debug.LogError($"[Model3DInfo] Failed: {request.error}"); ShowInfo(model3D, null); }
    }

    private IEnumerator LoadThumbnail(string url)
    {
        using var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
            if (modelImage != null && texture != null)
                modelImage.sprite = Sprite.Create(texture,
                    new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        else Debug.LogError($"[Model3DInfo] Thumbnail failed: {request.error}");
    }

    // ════════════════════════════════════════════════
    // PUBLIC GETTERS / SETTERS
    // ════════════════════════════════════════════════

    public Model3D GetCurrentModel3D()            => currentModel3D;
    public void SetRemoveHtmlTags(bool remove)    => removeHtmlTags = remove;
    public void SetConvertToTMProRichText(bool c) => convertToTMProRichText = c;
}