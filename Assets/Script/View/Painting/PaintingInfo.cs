using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text.RegularExpressions;

public class PaintingInfo : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Image      paintingImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI authorText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button closeButton;

    [Header("View Max Button")]
    [SerializeField] private Button      viewMaxButton;   
    [SerializeField] private MaxPainting maxPainting;     

    [Header("Image Settings")]
    [SerializeField] private bool  maintainAspectRatio = true;
    [SerializeField] private float maxWidth            = 800f;
    [SerializeField] private float maxHeight           = 600f;

    [Header("Settings")]
    [SerializeField] private string languageCode = "vi";

    [Header("Text Processing")]
    [SerializeField] private bool removeHtmlTags         = true;
    [SerializeField] private bool convertToTMProRichText = false;

    [Header("Panel Integration")]
    [SerializeField] private PanelListItemVisitor panelListItemVisitor;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // Event thông báo cho PaintingItem highlight khi info đang hiển thị
    public static event System.Action<int> OnPaintingInfoShown;
    public static event System.Action      OnPaintingInfoHidden;

    private Painting  currentPainting;
    private Texture2D currentTexture;
    private RectTransform imageRectTransform;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (paintingImage != null)
            imageRectTransform = paintingImage.GetComponent<RectTransform>();

        if (panelListItemVisitor == null)
        {
#pragma warning disable CS0618
            panelListItemVisitor = FindObjectOfType<PanelListItemVisitor>();
#pragma warning restore CS0618
        }

        //  Fallback tìm MaxPainting nếu chưa kéo thả
        if (maxPainting == null)
        {
#pragma warning disable CS0618
            maxPainting = FindObjectOfType<MaxPainting>();
#pragma warning restore CS0618
        }

        // Đăng ký button ở Awake để tránh Start() chạy sau ShowInfo() override lại interactable
        if (closeButton != null)
            closeButton.onClick.AddListener(HideInfo);

        if (viewMaxButton != null)
        {
            viewMaxButton.onClick.AddListener(OnViewMaxButtonClicked);
            viewMaxButton.interactable = false; // Disable cho đến khi có ảnh
        }
    }


    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HideInfo);

        if (viewMaxButton != null)
            viewMaxButton.onClick.RemoveListener(OnViewMaxButtonClicked);
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

    public void ShowInfo(Painting painting, Texture2D texture = null)
    {
        if (painting == null)
        {
            Debug.LogError("[PaintingInfo] Painting is null!");
            return;
        }

        // Tắt Model3D info nếu đang mở
#pragma warning disable CS0618
        Model3DInfo model3DInfo = FindObjectOfType<Model3DInfo>();
#pragma warning restore CS0618
        if (model3DInfo != null)
        {
            model3DInfo.HideInfo();
            if (showDebug) Debug.Log("[PaintingInfo] Model3D info auto-hidden");
        }

        if (panelListItemVisitor != null)
        {
            panelListItemVisitor.HideForPaintingInfo();
            if (showDebug) Debug.Log("[PaintingInfo] Called HideForPaintingInfo");
        }
        else
        {
            Debug.LogWarning("[PaintingInfo] panelListItemVisitor is NULL — panels will not hide!");
        }

        currentPainting = painting;
        currentTexture  = texture;

        // Đảm bảo gameObject active để coroutine có thể chạy và infoPanel hiển thị được
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (infoPanel != null)
            infoPanel.SetActive(true);

        OnPaintingInfoShown?.Invoke(painting.id);

        DisplayImage(texture, painting);
        DisplayName(painting);
        DisplayAuthor(painting);
        DisplayDescription(painting);

        //  Enable viewMaxButton khi có texture
        if (viewMaxButton != null)
            viewMaxButton.interactable = (texture != null);

        if (showDebug) Debug.Log($"[PaintingInfo] Showing info for: {painting.name}");
    }

    public void ShowInfoById(int paintingId)
    {
        if (APIManager.Instance == null || APIManager.Instance.apiResponse == null)
        {
            Debug.LogError("[PaintingInfo] API data not ready!");
            return;
        }

        Painting painting = APIManager.Instance.GetPaintingById(paintingId);

        if (painting != null)
        {
            if (!string.IsNullOrEmpty(painting.path_url))
                StartCoroutine(LoadTextureAndShow(painting));
            else
                ShowInfo(painting, null);
        }
        else
        {
            Debug.LogError($"[PaintingInfo] Painting not found: ID {paintingId}");
        }
    }

    public void HideInfo()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        OnPaintingInfoHidden?.Invoke();

        //  Ẩn MaxPainting nếu đang mở
        if (maxPainting != null)
            maxPainting.Hide();

        if (panelListItemVisitor != null)
        {
            panelListItemVisitor.RestoreAfterPaintingInfo();
            if (showDebug) Debug.Log("[PaintingInfo] Called RestoreAfterPaintingInfo");
        }

        if (showDebug) Debug.Log("[PaintingInfo] Info panel hidden");
    }

    public void ToggleInfo()
    {
        if (infoPanel == null) return;

        if (infoPanel.activeSelf)
            HideInfo();
        else
            ShowInfo(currentPainting, currentTexture);
    }

    // ════════════════════════════════════════════════
    // VIEW MAX BUTTON
    // ════════════════════════════════════════════════

    private void OnViewMaxButtonClicked()
    {
        if (currentTexture == null)
        {
            Debug.LogWarning("[PaintingInfo] No texture to show in max view!");
            return;
        }

        if (maxPainting == null)
        {
            Debug.LogError("[PaintingInfo] MaxPainting reference is null!");
            return;
        }

        maxPainting.Show(currentTexture);

        if (showDebug) Debug.Log($"[PaintingInfo] Opened MaxPainting for: {currentPainting?.name}");
    }

    // ════════════════════════════════════════════════
    // DISPLAY
    // ════════════════════════════════════════════════

    private void DisplayImage(Texture2D texture, Painting painting)
    {
        if (paintingImage == null) return;

        if (texture != null)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
            paintingImage.sprite = sprite;
            ScaleImageToFit(texture.width, texture.height);

            if (showDebug) Debug.Log($"[PaintingInfo] Image displayed: {texture.width}x{texture.height}");
        }
        else if (!string.IsNullOrEmpty(painting.thumbnail_url))
        {
            StartCoroutine(LoadThumbnail(painting.thumbnail_url));
        }
        else
        {
            paintingImage.sprite = null;
            if (showDebug) Debug.LogWarning("[PaintingInfo] No image available");
        }
    }

    private void ScaleImageToFit(float textureWidth, float textureHeight)
    {
        if (imageRectTransform == null || !maintainAspectRatio) return;

        float aspectRatio = textureWidth / textureHeight;
        float newWidth    = maxWidth;
        float newHeight   = maxWidth / aspectRatio;

        if (newHeight > maxHeight)
        {
            newHeight = maxHeight;
            newWidth  = maxHeight * aspectRatio;
        }

        imageRectTransform.sizeDelta = new Vector2(newWidth, newHeight);
    }

    private void DisplayName(Painting painting)
    {
        if (nameText == null) return;

        string displayName = painting.name;

        if (painting.paintings_lang != null)
        {
            LanguageData langData = GetLanguageData(painting.paintings_lang);
            if (langData != null && !string.IsNullOrEmpty(langData.name))
                displayName = langData.name;
        }

        nameText.text = ProcessHtmlText(displayName);
        if (showDebug) Debug.Log($"[PaintingInfo] Name: {displayName}");
    }

    private void DisplayAuthor(Painting painting)
    {
        if (authorText == null) return;

        authorText.text = !string.IsNullOrEmpty(painting.author)
            ? ProcessHtmlText(painting.author)
            : "Không rõ";

        if (showDebug) Debug.Log($"[PaintingInfo] Author: {painting.author}");
    }

    private void DisplayDescription(Painting painting)
    {
        if (descriptionText == null) return;

        string description = "Không có mô tả";

        if (painting.paintings_lang != null)
        {
            LanguageData langData = GetLanguageData(painting.paintings_lang);
            if (langData != null && !string.IsNullOrEmpty(langData.description))
                description = langData.description;
        }

        descriptionText.text = ProcessHtmlText(description);

        if (showDebug)
        {
            Debug.Log($"[PaintingInfo] Description (raw): {description}");
            Debug.Log($"[PaintingInfo] Description (processed): {descriptionText.text}");
        }
    }

    // ════════════════════════════════════════════════
    // TEXT PROCESSING
    // ════════════════════════════════════════════════

    private string ProcessHtmlText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string processedText = convertToTMProRichText
            ? ConvertHtmlToTMPro(text)
            : removeHtmlTags
                ? RemoveHtmlTags(text)
                : text;

        return processedText.Trim();
    }

    private string RemoveHtmlTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string result = Regex.Replace(text, @"<[^>]+>", string.Empty);
        result = DecodeHtmlEntities(result);
        result = Regex.Replace(result, @"\s+", " ");
        return result;
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
            .Replace("&nbsp;", " ")
            .Replace("&lt;",   "<")
            .Replace("&gt;",   ">")
            .Replace("&amp;",  "&")
            .Replace("&quot;", "\"")
            .Replace("&#39;",  "'")
            .Replace("&apos;", "'");
    }

    private LanguageData GetLanguageData(PaintingLang paintingLang)
    {
        return languageCode.ToLower() switch
        {
            "vi" => paintingLang.vi,
            _    => paintingLang.vi,
        };
    }

    // ════════════════════════════════════════════════
    // COROUTINES
    // ════════════════════════════════════════════════

    private IEnumerator LoadTextureAndShow(Painting painting)
    {
        if (showDebug) Debug.Log($"[PaintingInfo] Loading texture from: {painting.path_url}");

        using var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(painting.path_url);
        yield return request.SendWebRequest();

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
            ShowInfo(painting, texture);
        }
        else
        {
            Debug.LogError($"[PaintingInfo] Failed to load texture: {request.error}");
            ShowInfo(painting, null);
        }
    }

    private IEnumerator LoadThumbnail(string url)
    {
        if (showDebug) Debug.Log($"[PaintingInfo] Loading thumbnail from: {url}");

        using var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);

            if (paintingImage != null && texture != null)
            {
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
                paintingImage.sprite = sprite;
                ScaleImageToFit(texture.width, texture.height);

                //  Enable viewMaxButton khi thumbnail load xong
                if (viewMaxButton != null)
                {
                    currentTexture             = texture;
                    viewMaxButton.interactable = true;
                }

                if (showDebug) Debug.Log($"[PaintingInfo] Thumbnail loaded: {texture.width}x{texture.height}");
            }
        }
        else
        {
            Debug.LogError($"[PaintingInfo] Failed to load thumbnail: {request.error}");
        }
    }

    // ════════════════════════════════════════════════
    // PUBLIC SETTERS
    // ════════════════════════════════════════════════

    public Painting GetCurrentPainting()                => currentPainting;
    public void SetMaxImageSize(float w, float h)       { maxWidth = w; maxHeight = h; }
    public void SetMaintainAspectRatio(bool maintain)   => maintainAspectRatio = maintain;
    public void SetRemoveHtmlTags(bool remove)          => removeHtmlTags = remove;
    public void SetConvertToTMProRichText(bool convert) => convertToTMProRichText = convert;
}