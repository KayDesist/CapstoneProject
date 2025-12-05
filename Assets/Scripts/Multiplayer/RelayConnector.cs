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

    /// <summary>
    /// Starts a host through Unity Relay and returns a join code.
    /// Follows Unity's official pattern to avoid timeout issues.
    /// </summary>
    public async Task<string> StartHostWithRelay(int maxConnections, string connectionType = "dtls")
    {
        try
        {
            // 1. Initialize Services
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // 2. Create Relay Allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            // 3. Get the Join Code
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // 4. Configure Network Transport
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            // Use the correct utility method as per official docs[citation2]
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            // CRITICAL: Only use WebSockets if explicitly using "wss"[citation2]
            if (connectionType == "wss")
            {
                transport.UseWebSockets = true;
            }
            else
            {
                transport.UseWebSockets = false; // Explicitly set to false for DTLS/UDP
            }

            // 5. START HOST IMMEDIATELY (This keeps the allocation alive)[citation1][citation4]
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

    /// <summary>
    /// Joins an existing host via a provided Relay join code.
    /// </summary>
    public async Task<bool> StartClientWithRelay(string joinCode, string connectionType = "dtls")
    {
        try
        {
            // 0. Validate input first[citation7]
            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Join code is null or empty!");
                return false;
            }

            // Debug: Log the exact code being used[citation9]
            Debug.Log($"Client attempting to join with code: '{joinCode}'");

            // 1. Initialize Services
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // 2. Join Allocation using the code
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // 3. Configure Network Transport
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            // Use the correct utility method as per official docs[citation2]
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            // CRITICAL: Only use WebSockets if explicitly using "wss"[citation2]
            if (connectionType == "wss")
            {
                transport.UseWebSockets = true;
            }
            else
            {
                transport.UseWebSockets = false; // Explicitly set to false for DTLS/UDP
            }

            // 4. Start Client
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
        // Only run on active network sessions
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        // Send a heartbeat approximately every 3 seconds to stay well under the 10-second timeout
        if (Time.time - lastHeartbeatTime >= 3.0f)
        {
            SendRelayHeartbeatServerRpc();
            lastHeartbeatTime = Time.time;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendRelayHeartbeatServerRpc(ServerRpcParams rpcParams = default)
    {
        // This empty RPC serves as a network "ping"
        // It keeps the connection to the Relay server active for the sender
        // The ServerRpc ensures it travels over the network, triggering Relay activity
        // No logic needed here - the act of sending the message is what matters
    }

   

}  
