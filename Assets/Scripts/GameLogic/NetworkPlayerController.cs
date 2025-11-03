using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;
    public float jumpForce = 7f;

    [Header("References")]
    public Transform playerCamera;

    private Rigidbody rb;
    private float xRotation = 0f;
    private bool isGrounded = true;
    private RoleManager.PlayerRole currentRole;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.freezeRotation = true;

        // Disable camera and controls for non-owners
        if (!IsOwner)
        {
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        // ? Cursor control: only lock it when we’re actually in the GameScene
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "GameScene")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Get player role
        if (RoleManager.Instance != null)
        {
            currentRole = RoleManager.Instance.GetLocalPlayerRole();
            ApplyRoleSpecificSettings();
        }
    }

    private void ApplyRoleSpecificSettings()
    {
        if (!IsOwner) return;

        switch (currentRole)
        {
            case RoleManager.PlayerRole.Survivor:
                // Survivor-specific settings
                Debug.Log("You are a Survivor");
                break;
            case RoleManager.PlayerRole.Cultist:
                // Cultist-specific settings
                Debug.Log("You are the Cultist!");
                // Example: Cultist might have different speed or abilities
                moveSpeed *= 1.1f; // Cultist is slightly faster
                break;
        }
    }

    public RoleManager.PlayerRole GetPlayerRole()
    {
        return currentRole;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Skip controls in non-game scenes
        if (SceneManager.GetActiveScene().name != "GameScene")
            return;

        HandleMouseLook();
        HandleMovement();
        HandleJump();
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDir = (transform.forward * vertical + transform.right * horizontal).normalized;
        Vector3 moveVelocity = new Vector3(moveDir.x * moveSpeed, rb.linearVelocity.y, moveDir.z * moveSpeed);

        rb.linearVelocity = moveVelocity;
    }

    private void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }
}
