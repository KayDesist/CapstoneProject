using UnityEngine;
using Unity.Netcode;
public class PlayerMovement: NetworkBehaviour
{

    public float moveSpeed = 5f;

    private void Update()
    {
        // Only the local player should control movement.
        if (!IsOwner) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(h, 0, v);

        transform.Translate(move * moveSpeed * Time.deltaTime);
    }
}
