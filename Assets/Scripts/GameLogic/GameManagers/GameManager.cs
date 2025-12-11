using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    [Header("Player Character Prefabs")]
    [SerializeField] private GameObject[] playerCharacterPrefabs;

    [Header("Manager Prefabs")]
    [SerializeField] private GameObject roleManagerPrefab;
    [SerializeField] private GameObject taskManagerPrefab;
    [SerializeField] private GameObject endGameManagerPrefab;
    [SerializeField] private GameObject gameHUDManagerPrefab;

    private HashSet<ulong> spawnedPlayers = new HashSet<ulong>();
    private Dictionary<ulong, int> playerCharacterIndex = new Dictionary<ulong, int>();
    private bool sceneInitialized = false;
    private bool isShuttingDown = false;

    private NetworkVariable<int> nextCharacterIndex = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null && !isShuttingDown)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnSceneLoaded(string sceneName, LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;
        if (sceneName != "GameScene") return;

        sceneInitialized = true;
        spawnedPlayers.Clear();
        playerCharacterIndex.Clear();
        nextCharacterIndex.Value = 1;

        StartCoroutine(InitializeManagers());
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        if (sceneInitialized && SceneManager.GetActiveScene().name == "GameScene")
        {
            StartCoroutine(SpawnPlayerWithDelay(clientId));
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer || isShuttingDown) return;

        try
        {
            if (spawnedPlayers.Contains(clientId))
            {
                spawnedPlayers.Remove(clientId);
            }

            if (playerCharacterIndex.ContainsKey(clientId))
            {
                playerCharacterIndex.Remove(clientId);
            }

            if (clientId == NetworkManager.Singleton.LocalClientId && !isShuttingDown)
            {
                isShuttingDown = true;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
                }
            }
        }
        catch (System.Exception e)
        {
        }
    }

    private IEnumerator InitializeManagers()
    {
        yield return new WaitForSeconds(0.5f);

        if (EndGameManager.Instance == null && endGameManagerPrefab != null)
        {
            GameObject endGameManager = Instantiate(endGameManagerPrefab);
            endGameManager.GetComponent<NetworkObject>().Spawn();
        }

        yield return new WaitForSeconds(0.2f);

        if (GameHUDManager.Instance == null && gameHUDManagerPrefab != null)
        {
            GameObject gameHUDManager = Instantiate(gameHUDManagerPrefab);
        }

        yield return new WaitForSeconds(0.2f);

        if (RoleManager.Instance == null && roleManagerPrefab != null)
        {
            GameObject roleManager = Instantiate(roleManagerPrefab);
            roleManager.GetComponent<NetworkObject>().Spawn();
        }

        yield return new WaitForSeconds(0.2f);

        if (TaskManager.Instance == null && taskManagerPrefab != null)
        {
            GameObject taskManager = Instantiate(taskManagerPrefab);
            taskManager.GetComponent<NetworkObject>().Spawn();
        }

        yield return new WaitForSeconds(0.2f);

        StartCoroutine(SpawnPlayersWithDelay());
    }

    private IEnumerator SpawnPlayersWithDelay()
    {
        yield return new WaitForSeconds(0.5f);

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!spawnedPlayers.Contains(clientId))
            {
                SpawnPlayerForClient(clientId);
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    private IEnumerator SpawnPlayerWithDelay(ulong clientId)
    {
        yield return new WaitForSeconds(0.5f);

        if (!spawnedPlayers.Contains(clientId) && SceneManager.GetActiveScene().name == "GameScene")
        {
            SpawnPlayerForClient(clientId);
        }
    }

    // Spawn player for specific client
    private void SpawnPlayerForClient(ulong clientId)
    {
        if (playerCharacterPrefabs == null || playerCharacterPrefabs.Length == 0) return;

        if (spawnedPlayers.Contains(clientId)) return;

        int characterIndex = GetCharacterIndexForClient(clientId);

        if (characterIndex >= playerCharacterPrefabs.Length) return;

        GameObject characterPrefab = playerCharacterPrefabs[characterIndex];
        if (characterPrefab == null) return;

        Vector3 spawnPos = GetSpawnPosition();
        GameObject go = Instantiate(characterPrefab, spawnPos, Quaternion.identity);

        NetworkObject networkObject = go.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(clientId);

            spawnedPlayers.Add(clientId);
            playerCharacterIndex[clientId] = characterIndex;

            string characterName = GetCharacterName(characterIndex);
            go.name = $"{characterName}_Player_{clientId}";

            AssignCharacterToClientClientRpc(clientId, characterIndex);
        }
        else
        {
            Destroy(go);
        }
    }

    // Get character index for client
    private int GetCharacterIndexForClient(ulong clientId)
    {
        if (clientId == 0) return 0;

        if (playerCharacterIndex.ContainsKey(clientId))
        {
            return playerCharacterIndex[clientId];
        }

        int index = nextCharacterIndex.Value;

        if (index >= playerCharacterPrefabs.Length)
        {
            index = 1;
        }

        nextCharacterIndex.Value = index + 1;

        if (nextCharacterIndex.Value >= playerCharacterPrefabs.Length)
        {
            nextCharacterIndex.Value = 1;
        }

        return index;
    }

    // Get character name from index
    private string GetCharacterName(int index)
    {
        switch (index)
        {
            case 0: return "Mizuki";
            case 1: return "Jaxen";
            case 2: return "Sam";
            case 3: return "Elijah";
            case 4: return "Clint";
            default: return $"Character_{index}";
        }
    }

    [ClientRpc]
    private void AssignCharacterToClientClientRpc(ulong clientId, int characterIndex)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            string characterName = GetCharacterName(characterIndex);

            if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var playerController = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayerController>();
                if (playerController != null)
                {
                    SetupCharacterSettings(playerController, characterName);
                }
            }
        }
    }

    // Setup character-specific settings
    private void SetupCharacterSettings(NetworkPlayerController playerController, string characterName)
    {
        switch (characterName)
        {
            case "Mizuki":
                playerController.walkSpeed = 6.5f;
                playerController.sprintSpeed = 9.5f;
                break;
            case "Jaxen":
                playerController.walkSpeed = 7.5f;
                playerController.sprintSpeed = 11f;
                break;
            case "Sam":
                playerController.walkSpeed = 7f;
                playerController.sprintSpeed = 10f;
                break;
            case "Elijah":
                playerController.walkSpeed = 6f;
                playerController.sprintSpeed = 9f;
                break;
            case "Clint":
                playerController.walkSpeed = 8f;
                playerController.sprintSpeed = 12f;
                break;
        }
    }

    // Get spawn position
    private Vector3 GetSpawnPosition()
    {
        int attempts = 0;
        Vector3 spawnPos;

        do
        {
            spawnPos = new Vector3(Random.Range(-8f, 8f), 1.1f, Random.Range(-8f, 8f));
            attempts++;

            bool positionClear = true;
            foreach (var playerId in spawnedPlayers)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var client))
                {
                    if (Vector3.Distance(spawnPos, client.PlayerObject.transform.position) < 2f)
                    {
                        positionClear = false;
                        break;
                    }
                }
            }

            if (positionClear) break;

        } while (attempts < 10);

        return spawnPos;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer && IsClient)
        {
            RequestPlayerSpawnServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerSpawnServerRpc(ServerRpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;

        if (!spawnedPlayers.Contains(clientId) && SceneManager.GetActiveScene().name == "GameScene")
        {
            StartCoroutine(SpawnPlayerWithDelay(clientId));
        }
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
    }

    // Get player character name
    public string GetPlayerCharacterName(ulong clientId)
    {
        if (playerCharacterIndex.ContainsKey(clientId))
        {
            return GetCharacterName(playerCharacterIndex[clientId]);
        }
        return "Unknown";
    }

    [ContextMenu("Debug Spawned Players")]
    public void DebugSpawnedPlayers()
    {
        foreach (var playerId in spawnedPlayers)
        {
            string charName = GetPlayerCharacterName(playerId);
            Debug.Log($"Player ID: {playerId} - Character: {charName}");
        }
    }

    [ContextMenu("Print Character Assignments")]
    private void PrintCharacterAssignments()
    {
        foreach (var kvp in playerCharacterIndex)
        {
            Debug.Log($"Client {kvp.Key} → {GetCharacterName(kvp.Value)}");
        }
    }

    [ContextMenu("Force Spawn Test Players")]
    private void ForceSpawnTestPlayers()
    {
        if (!IsServer) return;

        spawnedPlayers.Clear();
        playerCharacterIndex.Clear();
        nextCharacterIndex.Value = 1;

        SpawnPlayerForClient(0);

        for (ulong i = 1; i <= 4; i++)
        {
            SpawnPlayerForClient(i);
        }
    }
}