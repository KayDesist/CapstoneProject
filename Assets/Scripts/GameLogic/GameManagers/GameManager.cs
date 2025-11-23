using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject roleManagerPrefab;
    [SerializeField] private GameObject taskManagerPrefab;
    [SerializeField] private GameObject endGameManagerPrefab;

    private HashSet<ulong> spawnedPlayers = new HashSet<ulong>();
    private bool sceneInitialized = false;

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
        if (NetworkManager.Singleton != null)
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

        // Spawn managers with proper order
        StartCoroutine(InitializeManagers());
    }

    // FIXED: Handle new clients connecting after scene is loaded
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
    }

    // FIXED: Handle client disconnections gracefully
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"Client {clientId} disconnected from game");

        // Remove from spawned players list
        if (spawnedPlayers.Contains(clientId))
        {
            spawnedPlayers.Remove(clientId);
        }

        // Notify EndGameManager about the disconnection
        if (EndGameManager.Instance != null)
        {
            EndGameManager.Instance.OnClientDisconnected(clientId);
        }

        // Check if we should end the game (e.g., if host disconnects)
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Host disconnected - ending game for everyone");
            // Host disconnected - return all clients to main menu
            NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }
    }

    private IEnumerator InitializeManagers()
    {
        yield return new WaitForSeconds(0.5f);

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

        // Spawn EndGameManager if it doesn't exist
        if (EndGameManager.Instance == null && endGameManagerPrefab != null)
        {
            GameObject endGameManager = Instantiate(endGameManagerPrefab);
            endGameManager.GetComponent<NetworkObject>().Spawn();
            Debug.Log("Spawned EndGameManager");
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
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned in GameManager!");
            return;
        }

        // Check if player already exists for this client
        if (spawnedPlayers.Contains(clientId))
        {
            Debug.LogWarning($"Player for client {clientId} already spawned!");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition();
        GameObject go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        NetworkObject networkObject = go.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId, true);
            spawnedPlayers.Add(clientId);
            Debug.Log($"Successfully spawned player for client {clientId} at position {spawnPos}");
        }
        else
        {
            Debug.LogError($"Player prefab doesn't have NetworkObject component for client {clientId}");
            Destroy(go);
        }
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

        } while (attempts < 10); // Prevent infinite loop

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

    // Debug method to check spawned players
    [ContextMenu("Debug Spawned Players")]
    public void DebugSpawnedPlayers()
    {
        Debug.Log($"=== SPAWNED PLAYERS ({spawnedPlayers.Count}) ===");
        foreach (var playerId in spawnedPlayers)
        {
            Debug.Log($"Player ID: {playerId}");
        }

        Debug.Log($"=== CONNECTED CLIENTS ({NetworkManager.Singleton.ConnectedClientsIds.Count}) ===");
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Debug.Log($"Client ID: {clientId}");
        }
    }
}