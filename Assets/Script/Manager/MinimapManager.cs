using UnityEngine;
using UnityEngine.UI;

public class MinimapManager : MonoBehaviour
{
    [SerializeField] private RawImage  smallMinimap;
    [SerializeField] private GameObject smallMinimapBorder;
    [SerializeField] private RawImage  bigMinimap;
    [SerializeField] private GameObject bigMinimapBorder;
    [SerializeField] private Button    goToBigMinimapButton;
    [SerializeField] private Button    goToSmallMinimapButton;
    [SerializeField] private Button    closeMapButton;       

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Start()
    {
        goToBigMinimapButton.onClick.AddListener(ShowBigMinimap);
        goToSmallMinimapButton.onClick.AddListener(ShowSmallMinimap);

        // State mặc định: small
        ShowSmallMinimap();
    }

    private void OnDestroy()
    {
        goToBigMinimapButton.onClick.RemoveListener(ShowBigMinimap);
        goToSmallMinimapButton.onClick.RemoveListener(ShowSmallMinimap);
    }

    // ═══════════════════════════════════════════════
    // CORE
    // ═══════════════════════════════════════════════

    private void ShowSmallMinimap()
    {
        // Small
        smallMinimap.gameObject.SetActive(true);
        smallMinimapBorder.SetActive(true);
        goToBigMinimapButton.gameObject.SetActive(true);

        // closeMapButton chỉ hiện khi ở chế độ small
        if (closeMapButton != null)
            closeMapButton.gameObject.SetActive(true);

        // Big → ẩn
        bigMinimap.gameObject.SetActive(false);
        bigMinimapBorder.SetActive(false);
        goToSmallMinimapButton.gameObject.SetActive(false);
    }

    private void ShowBigMinimap()
    {
        // Big
        bigMinimap.gameObject.SetActive(true);
        bigMinimapBorder.SetActive(true);
        goToSmallMinimapButton.gameObject.SetActive(true);

        // closeMapButton ẩn khi ở chế độ big
        if (closeMapButton != null)
            closeMapButton.gameObject.SetActive(false);

        // Small → ẩn
        smallMinimap.gameObject.SetActive(false);
        smallMinimapBorder.SetActive(false);
        goToBigMinimapButton.gameObject.SetActive(false);
    }
}