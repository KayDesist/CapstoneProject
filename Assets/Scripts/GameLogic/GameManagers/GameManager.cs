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

        // Spawn RoleManager if it doesn't exist
        if (RoleManager.Instance == null && roleManagerPrefab != null)
        {
            GameObject roleManager = Instantiate(roleManagerPrefab);
            roleManager.GetComponent<NetworkObject>().Spawn();
        }

        // Spawn TaskManager if it doesn't exist
        if (TaskManager.Instance == null && taskManagerPrefab != null)
        {
            GameObject taskManager = Instantiate(taskManagerPrefab);
            taskManager.GetComponent<NetworkObject>().Spawn();
        }

        // Wait a frame to ensure all network objects are ready
        StartCoroutine(SpawnPlayersWithDelay());
    }

    private IEnumerator SpawnPlayersWithDelay()
    {
        yield return new WaitForSeconds(0.1f);

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        Vector3 spawnPos = new Vector3(Random.Range(-5f, 5f), 1.1f, Random.Range(-5f, 5f));
        GameObject go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        Debug.Log($"Spawned player for {clientId}");
    }
}