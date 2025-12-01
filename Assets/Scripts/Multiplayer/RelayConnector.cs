using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using System;
using System.Threading.Tasks;

public class RelayConnector : MonoBehaviour
{
    private bool isInitializing = false;
    private static bool servicesInitialized = false;

    public async Task<string> StartHostWithRelay(int maxConnections, string connectionType = "wss")
    {
        try
        {
            Debug.Log($"[RelayConnector] Starting host...");

            // Initialize services if needed
            await InitializeServicesAsync();

            if (RelayService.Instance == null)
            {
                Debug.LogError("[RelayConnector] RelayService not available!");
                return null;
            }

            // Create allocation
            Debug.Log("[RelayConnector] Creating allocation...");
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            if (allocation == null)
            {
                Debug.LogError("[RelayConnector] Allocation failed!");
                return null;
            }

            // Get join code
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("[RelayConnector] Failed to get join code!");
                return null;
            }

            // Setup transport
            UnityTransport transport = GetOrCreateUnityTransport();
            if (transport == null)
            {
                Debug.LogError("[RelayConnector] Transport failed!");
                return null;
            }

            // Configure relay
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, connectionType);
            transport.SetRelayServerData(relayServerData);

            if (connectionType == "wss")
                transport.UseWebSockets = true;

            // Ensure NetworkManager is ready
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[RelayConnector] NetworkManager not found!");
                return null;
            }

            // Clean up if already running
            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            // Start host
            bool success = NetworkManager.Singleton.StartHost();

            if (!success)
            {
                Debug.LogError("[RelayConnector] Failed to start host!");
                return null;
            }

            Debug.Log($"[RelayConnector] Host started with code: {joinCode}");
            return joinCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayConnector] Error: {e.Message}");
            return null;
        }
    }

    public async Task<bool> StartClientWithRelay(string joinCode, string connectionType = "wss")
    {
        try
        {
            Debug.Log($"[RelayConnector] Joining as client: {joinCode}");

            // Initialize services if needed
            await InitializeServicesAsync();

            if (RelayService.Instance == null)
            {
                Debug.LogError("[RelayConnector] RelayService not available!");
                return false;
            }

            // Join allocation
            Debug.Log("[RelayConnector] Joining allocation...");
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            if (allocation == null)
            {
                Debug.LogError($"[RelayConnector] Failed to join: {joinCode}");
                return false;
            }

            // Setup transport
            UnityTransport transport = GetOrCreateUnityTransport();
            if (transport == null)
            {
                Debug.LogError("[RelayConnector] Transport failed!");
                return false;
            }

            // Configure relay
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, connectionType);
            transport.SetRelayServerData(relayServerData);

            if (connectionType == "wss")
                transport.UseWebSockets = true;

            // Ensure NetworkManager is ready
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[RelayConnector] NetworkManager not found!");
                return false;
            }

            // Clean up if already running
            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            // Start client
            bool success = NetworkManager.Singleton.StartClient();

            if (!success)
            {
                Debug.LogError("[RelayConnector] Failed to start client!");
                return false;
            }

            Debug.Log($"[RelayConnector] Client joined successfully!");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayConnector] Error: {e.Message}");
            return false;
        }
    }

    private async Task InitializeServicesAsync()
    {
        // Skip if already initialized
        if (servicesInitialized)
            return;

        if (isInitializing)
        {
            // Wait if another initialization is in progress
            while (isInitializing)
                await Task.Delay(100);
            return;
        }

        try
        {
            isInitializing = true;

            // Check current state
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                // Already initialized, just check auth
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log("[RelayConnector] Signed in to existing services");
                }
                servicesInitialized = true;
                isInitializing = false;
                return;
            }

            // Initialize fresh
            Debug.Log("[RelayConnector] Initializing Unity Services...");
            await UnityServices.InitializeAsync();

            Debug.Log("[RelayConnector] Signing in anonymously...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            servicesInitialized = true;
            Debug.Log("[RelayConnector] Services initialized successfully!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayConnector] Service init failed: {e.Message}");
            throw;
        }
        finally
        {
            isInitializing = false;
        }
    }

    private UnityTransport GetOrCreateUnityTransport()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("[RelayConnector] No NetworkManager!");
            return null;
        }

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = networkManager.gameObject.AddComponent<UnityTransport>();
            Debug.Log("[RelayConnector] Created UnityTransport");
        }

        return transport;
    }
}