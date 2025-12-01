using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    [Header("Character Prefabs - Assign in order: 0=Jaxen, 1=Sam, 2=Mizuki, 3=Elijah, 4=Clint")]
    [SerializeField] private GameObject[] characterPrefabs;
    [SerializeField] private GameObject roleManagerPrefab;
    [SerializeField] private GameObject taskManagerPrefab;
    [SerializeField] private GameObject endGameManagerPrefab;

    private HashSet<ulong> spawnedPlayers = new HashSet<ulong>();
    private bool sceneInitialized = false;
    private bool isShuttingDown = false;

    private Dictionary<ulong, int> clientCharacterMap = new Dictionary<ulong, int>();

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
        clientCharacterMap.Clear();

        StartCoroutine(InitializeManagers());
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"Client {clientId} connected");

        if (sceneInitialized && SceneManager.GetActiveScene().name == "GameScene")
        {
            Debug.Log($"Spawning player for late-joining client {clientId}");
            StartCoroutine(SpawnPlayerWithDelay(clientId));
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer || isShuttingDown) return;

        Debug.Log($"Client {clientId} disconnected from game");

        try
        {
            if (spawnedPlayers.Contains(clientId))
            {
                spawnedPlayers.Remove(clientId);
            }
            if (clientCharacterMap.ContainsKey(clientId))
            {
                clientCharacterMap.Remove(clientId);
            }

            if (EndGameManager.Instance != null && EndGameManager.Instance.gameObject != null)
            {
                EndGameManager.Instance.OnClientDisconnected(clientId);
            }

            if (clientId == NetworkManager.Singleton.LocalClientId && !isShuttingDown)
            {
                Debug.Log("Host disconnected - ending game for everyone");
                isShuttingDown = true;
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
        yield return new WaitForSeconds(1f); // Increased delay for stability

        if (RoleManager.Instance == null && roleManagerPrefab != null)
        {
            GameObject roleManager = Instantiate(roleManagerPrefab);
            roleManager.GetComponent<NetworkObject>().Spawn();
            Debug.Log("Spawned RoleManager");
        }

        yield return new WaitForSeconds(0.5f);

        if (TaskManager.Instance == null && taskManagerPrefab != null)
        {
            GameObject taskManager = Instantiate(taskManagerPrefab);
            taskManager.GetComponent<NetworkObject>().Spawn();
            Debug.Log("Spawned TaskManager");
        }

        yield return new WaitForSeconds(0.5f);

        if (EndGameManager.Instance == null && endGameManagerPrefab != null)
        {
            GameObject endGameManager = Instantiate(endGameManagerPrefab);
            endGameManager.GetComponent<NetworkObject>().Spawn();
            Debug.Log("Spawned EndGameManager");
        }

        yield return new WaitForSeconds(0.5f);

        StartCoroutine(SpawnPlayersWithDelay());
    }

    private IEnumerator SpawnPlayersWithDelay()
    {
        yield return new WaitForSeconds(1f); // Increased delay for stability

        Debug.Log($"Spawning players for {NetworkManager.Singleton.ConnectedClientsIds.Count} clients");

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!spawnedPlayers.Contains(clientId))
            {
                SpawnPlayerForClient(clientId);
                yield return new WaitForSeconds(0.2f); // Increased delay between spawns
            }
        }

        Debug.Log("Finished spawning all players");

        // Debug log all spawned players
        foreach (var playerId in spawnedPlayers)
        {
            string charName = "Unknown";
            if (clientCharacterMap.ContainsKey(playerId))
            {
                charName = CrossSceneData.GetCharacterName(clientCharacterMap[playerId]);
            }
            Debug.Log($"Final check - Player {playerId} spawned as {charName}");
        }
    }

    private IEnumerator SpawnPlayerWithDelay(ulong clientId)
    {
        yield return new WaitForSeconds(1f); // Increased delay

        if (!spawnedPlayers.Contains(clientId) && SceneManager.GetActiveScene().name == "GameScene")
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0)
        {
            Debug.LogError("Character prefabs are not assigned in GameManager!");
            return;
        }

        int characterIndex = GetCharacterIndexForClient(clientId);

        if (characterIndex < 0 || characterIndex >= characterPrefabs.Length)
        {
            Debug.LogError($"Invalid character index {characterIndex} for client {clientId}");
            return;
        }

        GameObject selectedCharacterPrefab = characterPrefabs[characterIndex];

        if (selectedCharacterPrefab == null)
        {
            Debug.LogError($"Character prefab at index {characterIndex} is null!");
            return;
        }

        if (spawnedPlayers.Contains(clientId))
        {
            Debug.LogWarning($"Player for client {clientId} already spawned!");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition();

        // Instantiate the character
        GameObject characterInstance = Instantiate(selectedCharacterPrefab, spawnPos, Quaternion.identity);

        // Check for NetworkObject
        NetworkObject networkObject = characterInstance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError($"Character prefab doesn't have NetworkObject component for client {clientId}");
            Destroy(characterInstance);
            return;
        }

        // Store the character assignment
        clientCharacterMap[clientId] = characterIndex;

        // Spawn the player object
        networkObject.SpawnAsPlayerObject(clientId);
        spawnedPlayers.Add(clientId);

        string characterName = CrossSceneData.GetCharacterName(characterIndex);
        Debug.Log($"Successfully spawned {characterName} for client {clientId} at position {spawnPos}");

        // Force ownership update
        StartCoroutine(VerifyPlayerOwnership(clientId, characterInstance));
    }

    private IEnumerator VerifyPlayerOwnership(ulong clientId, GameObject playerObject)
    {
        yield return new WaitForSeconds(1f);

        NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Debug.Log($"Ownership verification - Client {clientId}, IsOwner: {netObj.IsOwner}, OwnerClientId: {netObj.OwnerClientId}");
        }
        else
        {
            Debug.LogError($"Ownership verification failed - No NetworkObject for client {clientId}");
        }
    }

    private int GetCharacterIndexForClient(ulong clientId)
    {
        return CrossSceneData.GetCharacterIndexForClient(clientId);
    }

    private Vector3 GetSpawnPosition()
    {
        // Simple spawn logic - you can enhance this later
        float x = Random.Range(-8f, 8f);
        float z = Random.Range(-8f, 8f);
        return new Vector3(x, 1.1f, z);
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"GameManager network spawned - IsServer: {IsServer}, IsClient: {IsClient}");

        if (!IsServer && IsClient)
        {
            Debug.Log("Client requesting player spawn...");
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

    [ContextMenu("Debug Spawned Players")]
    public void DebugSpawnedPlayers()
    {
        Debug.Log($"=== SPAWNED PLAYERS ({spawnedPlayers.Count}) ===");
        foreach (var playerId in spawnedPlayers)
        {
            string characterName = "Unknown";
            if (clientCharacterMap.ContainsKey(playerId))
            {
                characterName = CrossSceneData.GetCharacterName(clientCharacterMap[playerId]);
            }
            Debug.Log($"Player ID: {playerId} -> Character: {characterName}");
        }

        Debug.Log($"=== CONNECTED CLIENTS ({NetworkManager.Singleton.ConnectedClientsIds.Count}) ===");
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Debug.Log($"Client ID: {clientId}");
        }
    }

    [ContextMenu("Check Character Prefabs")]
    public void CheckCharacterPrefabs()
    {
        Debug.Log("=== CHARACTER PREFAB CHECK ===");
        for (int i = 0; i < characterPrefabs.Length; i++)
        {
            if (characterPrefabs[i] == null)
            {
                Debug.LogError($"Character prefab at index {i} is NULL!");
                continue;
            }

            NetworkObject netObj = characterPrefabs[i].GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"Character prefab at index {i} has NO NetworkObject!");
            }
            else
            {
                Debug.Log($"Character {i}: {characterPrefabs[i].name} - NetworkObject: OK");
            }
        }
    }
}