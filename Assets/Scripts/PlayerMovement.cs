using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    public float moveSpeed = 5f;

    void Start()
    {
        // Useful debug to confirm ownership
        Debug.Log($"PlayerMovement.Start - IsOwner={IsOwner}, IsServer={IsServer}, OwnerClientId={OwnerClientId}");
    }

    private void Update()
    {
        if (!IsOwner) return; // only local owner runs input

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(h, 0, v);

        transform.Translate(move * moveSpeed * Time.deltaTime, Space.Self);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            // optional: snap to ground on server so clients don't spawn floating
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, 50f))
            {
                transform.position = hit.point + Vector3.up * 0.1f;
            }
        }
    }
}
