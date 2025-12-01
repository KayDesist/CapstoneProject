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
    /// <summary>
    /// Starts a host through Unity Relay and returns a join code.
    /// </summary>
    public async Task<string> StartHostWithRelay(int maxConnections, string connectionType = "wss")
    {
        try
        {
            Debug.Log("Initializing Unity Services for Relay HOST...");

            // Initialize Unity Services and sign in anonymously
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Signed in anonymously to Authentication Service");
            }

            Debug.Log("Creating Relay allocation...");

            // Create a Relay allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            // Configure the network transport to connect to the Relay server
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // *** FIXED LINE: Use AllocationUtils.ToRelayServerData ***
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            // Additional setting required for WebSocket (WSS) connections
            if (connectionType == "wss")
            {
                transport.UseWebSockets = true;
            }

            // Get the join code for the allocation
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"Relay HOST started. Join code: {joinCode}");

            // Start the host
            bool hostStarted = NetworkManager.Singleton.StartHost();

            if (hostStarted)
            {
                Debug.Log("HOST started successfully via Relay");
                return joinCode;
            }
            else
            {
                Debug.LogError("Failed to start HOST after Relay allocation");
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception in StartHostWithRelay: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Joins an existing host via a provided Relay join code.
    /// </summary>
    public async Task<bool> StartClientWithRelay(string joinCode, string connectionType = "wss")
    {
        try
        {
            Debug.Log($"CLIENT joining Relay with code: {joinCode}");

            // Initialize Unity Services and sign in anonymously
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Signed in anonymously to Authentication Service");
            }

            // Join an existing Relay allocation using the join code
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // Configure the network transport to connect to the Relay server
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            // Additional setting required for WebSocket (WSS) connections
            if (connectionType == "wss")
            {
                transport.UseWebSockets = true;
            }

            Debug.Log("Relay CLIENT configured successfully. Starting client...");

            // Start the client
            bool clientStarted = NetworkManager.Singleton.StartClient();

            if (clientStarted)
            {
                Debug.Log("CLIENT started successfully via Relay");
                return true;
            }
            else
            {
                Debug.LogError("Failed to start CLIENT after Relay join");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception in StartClientWithRelay: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            return false;
        }
    }
}