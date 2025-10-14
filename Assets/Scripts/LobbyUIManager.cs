using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerNamePrefab;
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaveButton;

    private readonly List<GameObject> activePlayers = new();

    private void Start()
    {
        startButton.interactable = false;
    }

    public void UpdateLobbyCode(string code)
    {
        lobbyCodeText.text = $"Lobby Code: {code}";
    }

    public void UpdatePlayerList(List<string> names)
    {
        foreach (var obj in activePlayers) Destroy(obj);
        activePlayers.Clear();

        foreach (var name in names)
        {
            var item = Instantiate(playerNamePrefab, playerListContainer);
            item.GetComponent<TMP_Text>().text = name;
            activePlayers.Add(item);
        }
    }

    public void SetStartInteractable(bool state)
    {
        startButton.interactable = state;
    }
}
