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

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
    }

    private void OnSceneLoaded(string sceneName, LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;
        if (sceneName != "GameScene") return;

        Debug.Log("GameScene loaded, initializing game...");

        // Spawn RoleManager if it doesn't exist
        if (RoleManager.Instance == null && roleManagerPrefab != null)
        {
            GameObject roleManager = Instantiate(roleManagerPrefab);
            roleManager.GetComponent<NetworkObject>().Spawn();
            Debug.Log("Spawned RoleManager");
        }
        else
        {
            Debug.Log("RoleManager already exists or prefab is null");
        }

        // Spawn TaskManager if it doesn't exist
        if (TaskManager.Instance == null && taskManagerPrefab != null)
        {
            GameObject taskManager = Instantiate(taskManagerPrefab);
            taskManager.GetComponent<NetworkObject>().Spawn();
            Debug.Log("Spawned TaskManager");
        }
        else
        {
            Debug.Log("TaskManager already exists or prefab is null");
        }

        // Wait a frame to ensure all network objects are ready
        StartCoroutine(SpawnPlayersWithDelay());
    }

    private IEnumerator SpawnPlayersWithDelay()
    {
        // Wait for network objects to be fully spawned and initialized
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"Spawning players for {NetworkManager.Singleton.ConnectedClientsIds.Count} clients");

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerForClient(clientId);
            yield return new WaitForSeconds(0.1f); // Small delay between spawns
        }

        Debug.Log("Finished spawning all players");
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned in GameManager!");
            return;
        }

        Vector3 spawnPos = new Vector3(Random.Range(-5f, 5f), 1.1f, Random.Range(-5f, 5f));
        GameObject go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        NetworkObject networkObject = go.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId);
            Debug.Log($"Successfully spawned player for client {clientId} at position {spawnPos}");
        }
        else
        {
            Debug.LogError($"Player prefab doesn't have NetworkObject component for client {clientId}");
            Destroy(go);
        }
    }
}