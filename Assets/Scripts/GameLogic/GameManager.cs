using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;

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

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        Vector3 spawnPos = new Vector3(Random.Range(-5f, 5f), 1.1f, Random.Range(-5f, 5f)); // replace with your spawn points
        GameObject go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        Debug.Log($"Spawned player for {clientId}");
    }
}
