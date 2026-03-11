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

    [Header("PlayerPrefs Key")]
    [SerializeField] private string musicStateKey = "MusicEnabled";

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private void Start()
    {
        // Setup AudioSource
        if (backgroundMusicSource != null && audioClip != null)
        {
            backgroundMusicSource.clip = audioClip;
            backgroundMusicSource.loop = true;

            // Load trạng thái đã lưu (mặc định = bật)
            bool isMusicEnabled = PlayerPrefs.GetInt(musicStateKey, 1) == 1;

            if (isMusicEnabled)
            {
                backgroundMusicSource.Play();
            }

            if (showDebug)
                Debug.Log($"[SoundManager] Music enabled: {isMusicEnabled}");
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
            PlayerPrefs.SetInt(musicStateKey, 0);

            if (showDebug)
                Debug.Log("[SoundManager] Music OFF");
        }
        else
        {
            backgroundMusicSource.Play();
            PlayerPrefs.SetInt(musicStateKey, 1);

            if (showDebug)
                Debug.Log("[SoundManager] Music ON");
        }

        PlayerPrefs.Save();
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
