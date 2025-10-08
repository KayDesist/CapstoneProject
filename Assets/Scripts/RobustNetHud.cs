// RobustNetHud.cs
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class RobustNetHud : MonoBehaviour
{
    public string hostAddress = "127.0.0.1";
    public ushort hostPort = 7777;
    private UnityTransport utp;

    void Awake()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No NetworkManager in scene!");
            enabled = false;
            return;
        }
        utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        NetworkManager.Singleton.OnClientConnectedCallback += id => Debug.Log("Connected: " + id);
        NetworkManager.Singleton.OnClientDisconnectCallback += id => Debug.Log("Disconnected: " + id);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 300, 200), GUI.skin.box);
        GUILayout.Label($"LocalClientId: {(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId.ToString() : "N/A")}");
        if (!NetworkManager.Singleton.IsListening)
        {
            if (GUILayout.Button("Start Host")) StartHost();
            GUILayout.Space(8);
            GUILayout.Label("Client Target Address:");
            hostAddress = GUILayout.TextField(hostAddress);
            GUILayout.Label("Client Target Port:");
            ushort.TryParse(GUILayout.TextField(hostPort.ToString()), out hostPort);
            if (GUILayout.Button("Start Client")) StartClient();
        }
        else
        {
            if (NetworkManager.Singleton.IsHost) GUILayout.Label("Running as Host");
            else if (NetworkManager.Singleton.IsServer) GUILayout.Label("Running as Server");
            else if (NetworkManager.Singleton.IsClient) GUILayout.Label("Running as Client");
            if (GUILayout.Button("Shutdown")) NetworkManager.Singleton.Shutdown();
        }
        GUILayout.EndArea();
    }

    public void StartHost()
    {
        Debug.Log("StartHost called");
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        utp.ConnectionData.Address = hostAddress;
        utp.ConnectionData.Port = hostPort;
        Debug.Log($"StartClient called -> {hostAddress}:{hostPort}");
        NetworkManager.Singleton.StartClient();
    }
}
