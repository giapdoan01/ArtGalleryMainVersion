using TMPro;
using UnityEngine;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Color localPlayerColor = new Color(0f, 0.5f, 0f);

    public void Setup(string playerName, bool isLocalPlayer)
    {
        playerNameText.text = playerName;
        playerNameText.color = isLocalPlayer ? localPlayerColor : Color.white;
    }
}
