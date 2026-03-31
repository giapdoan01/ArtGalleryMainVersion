using UnityEngine;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource backgroundMusicSource;
    [SerializeField] private AudioClip audioClip;

    [Header("UI Buttons")]
    [SerializeField] private Button musicOnButton;
    [SerializeField] private Button musicOffButton;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private void Start()
    {
        // Setup AudioSource
        if (backgroundMusicSource != null && audioClip != null)
        {
            backgroundMusicSource.clip = audioClip;
            backgroundMusicSource.loop = true;

            backgroundMusicSource.Play();

            if (showDebug)
                Debug.Log("[SoundManager] Music started");
        }

        // Setup buttons
        if (musicOnButton != null)
        {
            musicOnButton.onClick.AddListener(ToggleMusic);
        }

        if (musicOffButton != null)
        {
            musicOffButton.onClick.AddListener(ToggleMusic);
        }

        // Update UI
        UpdateButtonUI();
    }

    public void ToggleMusic()
    {
        if (backgroundMusicSource == null) return;

        if (backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Pause();

            if (showDebug)
                Debug.Log("[SoundManager] Music OFF");
        }
        else
        {
            backgroundMusicSource.Play();

            if (showDebug)
                Debug.Log("[SoundManager] Music ON");
        }

        UpdateButtonUI();
    }

    private void UpdateButtonUI()
    {
        if (backgroundMusicSource == null) return;

        bool isPlaying = backgroundMusicSource.isPlaying;

        if (musicOnButton != null)
        {
            musicOnButton.gameObject.SetActive(isPlaying);
        }

        if (musicOffButton != null)
        {
            musicOffButton.gameObject.SetActive(!isPlaying);
        }
    }

    private void OnDestroy()
    {
        if (musicOnButton != null)
        {
            musicOnButton.onClick.RemoveListener(ToggleMusic);
        }

        if (musicOffButton != null)
        {
            musicOffButton.onClick.RemoveListener(ToggleMusic);
        }
    }
}
