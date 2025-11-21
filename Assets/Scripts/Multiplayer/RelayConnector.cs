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
            // FIXED: Better initialization check
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // FIXED: Check if NetworkManager is already running
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("NetworkManager is already running. Shutting down before creating new host.");
                NetworkManager.Singleton.Shutdown();
                // Wait a frame for shutdown to complete
                await Task.Delay(100);
            }

            Debug.Log("Creating Relay allocation...");

            //Create an allocation for the host
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            //Configure transport with Relay server data
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport component not found on NetworkManager!");
                return null;
            }

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            // Get join code for clients
            Debug.Log("Getting join code...");
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"Relay host started. Join code: {joinCode}");

            // FIXED: Better host startup with validation
            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("Host started successfully!");
                return joinCode;
            }
            else
            {
                Debug.LogError("NetworkManager.StartHost() returned false!");
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RelayConnector: Failed to start host with Relay. Exception: {e.Message}");
            Debug.LogError($"Full exception: {e}");
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
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // FIXED: Check if NetworkManager is already running
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("NetworkManager is already running. Shutting down before joining as client.");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(100);
            }

            Debug.Log($"Joining Relay allocation with code: {joinCode}");

            //Join an existing Relay allocation using join code
            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            //Configure transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport component not found on NetworkManager!");
                return false;
            }

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            Debug.Log("Relay client configured successfully.");

            //Start client
            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("Client started successfully!");
                return true;
            }
            else
            {
                Debug.LogError("NetworkManager.StartClient() returned false!");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RelayConnector: Failed to start client with Relay. Exception: {e.Message}");
            Debug.LogError($"Full exception: {e}");
            return false;
        }
    }
}