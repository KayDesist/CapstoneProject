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
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        //Create an allocation for the host
        var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

        //Configure transport with Relay server data (FIXED: using wss for WebSockets)
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

        // Get join code for clients
        var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        Debug.Log($"Relay host started. Join code: {joinCode}");

        //Start host
        return NetworkManager.Singleton.StartHost() ? joinCode : null;
    }

    /// <summary>
    /// Joins an existing host via a provided Relay join code.
    /// </summary>
    public async Task<bool> StartClientWithRelay(string joinCode, string connectionType = "wss")
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        //Join an existing Relay allocation using join code
        var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        //Configure transport (FIXED: using wss for WebSockets)
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

        Debug.Log("Relay client joined successfully.");

        //Start client
        return NetworkManager.Singleton.StartClient();
    }
}