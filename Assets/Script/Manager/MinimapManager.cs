using UnityEngine;
using UnityEngine.UI;

public class MinimapManager : MonoBehaviour
{
    [SerializeField] private RawImage smallMinimap;
    [SerializeField] private GameObject smallMinimapBorder;
    [SerializeField] private RawImage bigMinimap;
    [SerializeField] private GameObject bigMinimapBorder;
    [SerializeField] private Button   goToBigMinimapButton;
    [SerializeField] private Button   goToSmallMinimapButton;


    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Start()
    {
        // Gắn sự kiện
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
        smallMinimap.gameObject.SetActive(true);
        goToBigMinimapButton.gameObject.SetActive(true);

        bigMinimap.gameObject.SetActive(false);
        goToSmallMinimapButton.gameObject.SetActive(false);

        smallMinimapBorder.SetActive(true);
        bigMinimapBorder.SetActive(false);
    }

    private void ShowBigMinimap()
    {
        bigMinimap.gameObject.SetActive(true);
        goToSmallMinimapButton.gameObject.SetActive(true);

        smallMinimap.gameObject.SetActive(false);
        goToBigMinimapButton.gameObject.SetActive(false);

        bigMinimapBorder.SetActive(true);
        smallMinimapBorder.SetActive(false);
    }
}