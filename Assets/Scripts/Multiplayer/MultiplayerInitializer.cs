using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using System.Threading.Tasks;

public class MultiplayerInitializer : MonoBehaviour
{
    private async void Awake()
    {
        await InitializeServicesAsync();
        DontDestroyOnLoad(gameObject);
    }

    // Initializes Unity Services and signs in anonymously
    private async Task InitializeServicesAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("[Multiplayer] Signed in anonymously to Unity Services.");
            }

            Debug.Log("[Multiplayer] Services Initialized Successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Multiplayer] Initialization failed: {e.Message}");
        }
    }
}