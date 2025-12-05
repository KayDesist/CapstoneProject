using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    // Array of all available player character prefabs
    [Header("Player Character Prefabs")]
    [SerializeField] private GameObject[] playerCharacterPrefabs; // Mizuki, Jaxen, Sam, Elijah, Clint in order

    [Header("Manager Prefabs")]
    [SerializeField] private GameObject roleManagerPrefab;
    [SerializeField] private GameObject taskManagerPrefab;
    [SerializeField] private GameObject endGameManagerPrefab;
    [SerializeField] private GameObject gameHUDManagerPrefab; // Added for completeness

    private HashSet<ulong> spawnedPlayers = new HashSet<ulong>();
    private Dictionary<ulong, int> playerCharacterIndex = new Dictionary<ulong, int>();
    private bool sceneInitialized = false;
    private bool isShuttingDown = false;

    // Network variable to track which character each player is using
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

        Debug.Log("GameScene loaded, initializing game...");
        sceneInitialized = true;
        spawnedPlayers.Clear();
        playerCharacterIndex.Clear();
        nextCharacterIndex.Value = 1; // Reset character index counter, start at 1 (host is 0)

        // Spawn managers with proper order
        StartCoroutine(InitializeManagers());
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"Client {clientId} connected");

        // If scene is already loaded, spawn player for this client
        if (sceneInitialized && SceneManager.GetActiveScene().name == "GameScene")
        {
            Debug.Log($"Spawning player for late-joining client {clientId}");
            StartCoroutine(SpawnPlayerWithDelay(clientId));
        }
        else
        {
            // If in lobby, track the player but don't spawn yet
            Debug.Log($"Client {clientId} connected to lobby");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer || isShuttingDown) return;

        Debug.Log($"Client {clientId} disconnected from game");

        try
        {
            // Remove from tracking
            if (spawnedPlayers.Contains(clientId))
            {
                spawnedPlayers.Remove(clientId);
            }

            if (playerCharacterIndex.ContainsKey(clientId))
            {
                playerCharacterIndex.Remove(clientId);
            }

            // Check if we should end the game (e.g., if host disconnects)
            if (clientId == NetworkManager.Singleton.LocalClientId && !isShuttingDown)
            {
                Debug.Log("Host disconnected - ending game for everyone");
                isShuttingDown = true;

                // Load Main Menu for everyone
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error during client disconnection handling: {e.Message}");
        }
    }
    private IEnumerator InitializeManagers()
    {
        yield return new WaitForSeconds(0.5f);

        // CRITICAL FIX: Spawn EndGameManager FIRST
        if (EndGameManager.Instance == null && endGameManagerPrefab != null)
        {
            GameObject endGameManager = Instantiate(endGameManagerPrefab);
            endGameManager.GetComponent<NetworkObject>().Spawn();
            Debug.Log("Spawned EndGameManager");
        }

        yield return new WaitForSeconds(0.2f);

        // Spawn GameHUDManager if it doesn't exist
        if (GameHUDManager.Instance == null && gameHUDManagerPrefab != null)
        {
            GameObject gameHUDManager = Instantiate(gameHUDManagerPrefab);
            // GameHUDManager doesn't need NetworkObject since it's client-side only
            Debug.Log("Spawned GameHUDManager");
        }

        yield return new WaitForSeconds(0.2f);

        // Spawn RoleManager if it doesn't exist
        if (RoleManager.Instance == null && roleManagerPrefab != null)
        {
            GameObject roleManager = Instantiate(roleManagerPrefab);
            roleManager.GetComponent<NetworkObject>().Spawn();
            Debug.Log("Spawned RoleManager");
        }

        yield return new WaitForSeconds(0.2f);

        // Spawn TaskManager if it doesn't exist
        if (TaskManager.Instance == null && taskManagerPrefab != null)
        {
            GameObject taskManager = Instantiate(taskManagerPrefab);
            taskManager.GetComponent<NetworkObject>().Spawn();
            Debug.Log("Spawned TaskManager");
        }

        yield return new WaitForSeconds(0.2f);

        // Spawn players for all currently connected clients
        StartCoroutine(SpawnPlayersWithDelay());
    }

    private IEnumerator SpawnPlayersWithDelay()
    {
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"Spawning players for {NetworkManager.Singleton.ConnectedClientsIds.Count} clients");

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // Only spawn if not already spawned
            if (!spawnedPlayers.Contains(clientId))
            {
                SpawnPlayerForClient(clientId);
                yield return new WaitForSeconds(0.1f);
            }
        }

        Debug.Log("Finished spawning all players");
    }

    private IEnumerator SpawnPlayerWithDelay(ulong clientId)
    {
        yield return new WaitForSeconds(0.5f);

        // Only spawn if not already spawned and scene is still game scene
        if (!spawnedPlayers.Contains(clientId) && SceneManager.GetActiveScene().name == "GameScene")
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (playerCharacterPrefabs == null || playerCharacterPrefabs.Length == 0)
        {
            Debug.LogError("Player character prefabs array is not assigned or empty in GameManager!");
            return;
        }

        // Check if player already exists for this client
        if (spawnedPlayers.Contains(clientId))
        {
            Debug.LogWarning($"Player for client {clientId} already spawned!");
            return;
        }

        // Assign a character based on connection order
        int characterIndex = GetCharacterIndexForClient(clientId);

        if (characterIndex >= playerCharacterPrefabs.Length)
        {
            Debug.LogError($"Character index {characterIndex} out of bounds! Only {playerCharacterPrefabs.Length} characters available.");
            return;
        }

        GameObject characterPrefab = playerCharacterPrefabs[characterIndex];

        if (characterPrefab == null)
        {
            Debug.LogError($"Character prefab at index {characterIndex} is null!");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition();
        GameObject go = Instantiate(characterPrefab, spawnPos, Quaternion.identity);

        NetworkObject networkObject = go.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            // CRITICAL: Spawn with ownership for the specific client
            networkObject.SpawnWithOwnership(clientId);

            spawnedPlayers.Add(clientId);
            playerCharacterIndex[clientId] = characterIndex;

            // Assign character name to the player object for reference
            string characterName = GetCharacterName(characterIndex);
            go.name = $"{characterName}_Player_{clientId}";

            Debug.Log($"Successfully spawned {characterName} for client {clientId} at position {spawnPos}");

            // Notify client about their character assignment
            AssignCharacterToClientClientRpc(clientId, characterIndex);
        }
        else
        {
            Debug.LogError($"Character prefab doesn't have NetworkObject component for client {clientId}");
            Destroy(go);
        }
    }

    private int GetCharacterIndexForClient(ulong clientId)
    {
        // Host (clientId 0) always gets Mizuki (index 0)
        if (clientId == 0)
        {
            return 0; // Mizuki
        }

        // If we've already assigned a character to this client, return it
        if (playerCharacterIndex.ContainsKey(clientId))
        {
            return playerCharacterIndex[clientId];
        }

        // Assign next available character index
        int index = nextCharacterIndex.Value;

        // If index exceeds available characters, wrap around (skip 0 which is host)
        if (index >= playerCharacterPrefabs.Length)
        {
            index = 1; // Skip 0 (host) and start from 1
        }

        // Increment for next client
        nextCharacterIndex.Value = index + 1;

        // Wrap around if needed
        if (nextCharacterIndex.Value >= playerCharacterPrefabs.Length)
        {
            nextCharacterIndex.Value = 1; // Skip 0
        }

        return index;
    }

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
        // Only process for the client who owns this player
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            string characterName = GetCharacterName(characterIndex);
            Debug.Log($"You have been assigned character: {characterName}");

            // Find the player object for this client
            if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var playerController = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayerController>();
                if (playerController != null)
                {
                    // Initialize character settings
                    SetupCharacterSettings(playerController, characterName);
                }
            }
        }
    }

    private void SetupCharacterSettings(NetworkPlayerController playerController, string characterName)
    {
        Debug.Log($"Setting up character: {characterName}");

        // Apply character-specific settings
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
            default:
                Debug.LogWarning($"Unknown character name: {characterName}, using default settings");
                break;
        }

        Debug.Log($"Character {characterName} setup - Walk: {playerController.walkSpeed}, Sprint: {playerController.sprintSpeed}");
    }

    private Vector3 GetSpawnPosition()
    {
        // Better spawn position logic to avoid overlapping
        int attempts = 0;
        Vector3 spawnPos;

        do
        {
            spawnPos = new Vector3(Random.Range(-8f, 8f), 1.1f, Random.Range(-8f, 8f));
            attempts++;

            // Check if position is clear (simple distance check)
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
        Debug.Log("GameManager network spawned");

        // If we're a client that joined after scene was loaded, request spawn
        if (!IsServer && IsClient)
        {
            RequestPlayerSpawnServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerSpawnServerRpc(ServerRpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"Client {clientId} requested player spawn");

        if (!spawnedPlayers.Contains(clientId) && SceneManager.GetActiveScene().name == "GameScene")
        {
            StartCoroutine(SpawnPlayerWithDelay(clientId));
        }
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
    }

    // Public method to get character info for a player
    public string GetPlayerCharacterName(ulong clientId)
    {
        if (playerCharacterIndex.ContainsKey(clientId))
        {
            return GetCharacterName(playerCharacterIndex[clientId]);
        }
        return "Unknown";
    }

    // Debug method to check spawned players
    [ContextMenu("Debug Spawned Players")]
    public void DebugSpawnedPlayers()
    {
        Debug.Log($"=== SPAWNED PLAYERS ({spawnedPlayers.Count}) ===");
        foreach (var playerId in spawnedPlayers)
        {
            string charName = GetPlayerCharacterName(playerId);
            Debug.Log($"Player ID: {playerId} - Character: {charName}");
        }

        Debug.Log($"=== CONNECTED CLIENTS ({NetworkManager.Singleton.ConnectedClientsIds.Count}) ===");
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Debug.Log($"Client ID: {clientId}");
        }
    }

    [ContextMenu("Print Character Assignments")]
    private void PrintCharacterAssignments()
    {
        Debug.Log("=== CHARACTER ASSIGNMENTS ===");
        foreach (var kvp in playerCharacterIndex)
        {
            Debug.Log($"Client {kvp.Key} → {GetCharacterName(kvp.Value)}");
        }
    }

    [ContextMenu("Force Spawn Test Players")]
    private void ForceSpawnTestPlayers()
    {
        if (!IsServer) return;

        Debug.Log("=== FORCE SPAWNING TEST PLAYERS ===");

        // Clear existing
        spawnedPlayers.Clear();
        playerCharacterIndex.Clear();
        nextCharacterIndex.Value = 1;

        // Spawn host (Mizuki)
        SpawnPlayerForClient(0);

        // Spawn test clients
        for (ulong i = 1; i <= 4; i++)
        {
            SpawnPlayerForClient(i);
        }
    }
}