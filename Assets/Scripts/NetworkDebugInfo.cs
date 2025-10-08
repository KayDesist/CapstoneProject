using UnityEngine;
using Unity.Netcode;

public class NetworkDebugInfo : MonoBehaviour
{
    void OnGUI()
    {
        var no = GetComponent<NetworkObject>();
        string owner = no != null ? no.OwnerClientId.ToString() : "no NO";
        string local = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId.ToString() : "no NM";
        string isOwner = no != null && no.IsOwner ? "IsOwner" : "NotOwner";
        GUI.Label(new Rect(10, 10 + (20 * (int)NetworkManager.Singleton.LocalClientId), 300, 20),
                  $"Local:{local} Owner:{owner} {isOwner}");
    }
}
