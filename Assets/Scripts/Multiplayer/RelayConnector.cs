using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

public class RelayConnector : MonoBehaviour
{
    private float lastHeartbeatTime = 0f;
    private const float HEARTBEAT_INTERVAL = 3f;

    // Starts a host through Unity Relay and returns a join code
    public async Task<string> StartHostWithRelay(int maxConnections, string connectionType = "dtls")
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            if (connectionType == "wss")
            {
                transport.UseWebSockets = true;
            }
            else
            {
                transport.UseWebSockets = false;
            }

            bool hostStarted = NetworkManager.Singleton.StartHost();

            if (hostStarted)
            {
                Debug.Log($"Host started successfully via Relay. Join Code: {joinCode}");
                return joinCode;
            }
            else
            {
                Debug.LogError("Failed to start host.");
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception in StartHostWithRelay: {e.Message}");
            return null;
        }
    }

    // Joins an existing host via a provided Relay join code
    public async Task<bool> StartClientWithRelay(string joinCode, string connectionType = "dtls")
    {
        try
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Join code is null or empty!");
                return false;
            }

            Debug.Log($"Client attempting to join with code: '{joinCode}'");

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            if (connectionType == "wss")
            {
                transport.UseWebSockets = true;
            }
            else
            {
                transport.UseWebSockets = false;
            }

            bool clientStarted = NetworkManager.Singleton.StartClient();

            if (clientStarted)
            {
                Debug.Log("Client started successfully via Relay.");
                return true;
            }
            else
            {
                Debug.LogError("Failed to start client.");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception in StartClientWithRelay: {e.Message}");
            return false;
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        if (Time.time - lastHeartbeatTime >= HEARTBEAT_INTERVAL)
        {
            SendRelayHeartbeatServerRpc();
            lastHeartbeatTime = Time.time;
        }
    }

    // Empty RPC that serves as a network ping to keep connection active
    [ServerRpc(RequireOwnership = false)]
    private void SendRelayHeartbeatServerRpc(ServerRpcParams rpcParams = default)
    {
    }
}