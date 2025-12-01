using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

public class MainMenuCharacterManager : MonoBehaviour
{
    public static MainMenuCharacterManager Instance;

    [Header("MainMenu Only - Simplified Prefabs")]
    [SerializeField] private GameObject[] mainMenuCharacterPrefabs; // Create NEW prefabs without network components
    [SerializeField] private Transform[] characterSpawnPoints;
    [SerializeField] private Transform campfireCenter;

    [Header("Animation Settings")]
    [SerializeField] private string sittingAnimation = "Sitting";
    [SerializeField] private string talkingAnimation = "Talking";
    [SerializeField] private float minIdleTime = 3f;
    [SerializeField] private float maxIdleTime = 8f;
    [SerializeField] private float minTalkTime = 1f;
    [SerializeField] private float maxTalkTime = 3f;

    private List<GameObject> spawnedCharacters = new List<GameObject>();
    private List<Animator> characterAnimators = new List<Animator>();
    private bool isInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DON'T use DontDestroyOnLoad here - we want this to be scene-specific
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Check if we're in the main menu scene
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            // Wait a frame to ensure scene is fully loaded
            Invoke("SetupMainMenuCharacters", 0.1f);
        }
        else
        {
            // If somehow in wrong scene, destroy this manager
            Destroy(gameObject);
        }
    }

    private void SetupMainMenuCharacters()
    {
        if (isInitialized) return;

        Debug.Log("Setting up main menu characters...");

        // Clear any existing characters
        ClearCharacters();

        // Validate setup
        if (mainMenuCharacterPrefabs.Length == 0)
        {
            Debug.LogError("No main menu character prefabs assigned!");
            return;
        }

        if (characterSpawnPoints.Length == 0)
        {
            Debug.LogError("No character spawn points assigned!");
            return;
        }

        // Spawn characters around campfire
        for (int i = 0; i < Mathf.Min(mainMenuCharacterPrefabs.Length, characterSpawnPoints.Length); i++)
        {
            GameObject characterPrefab = mainMenuCharacterPrefabs[i];
            Transform spawnPoint = characterSpawnPoints[i];

            if (characterPrefab == null || spawnPoint == null) continue;

            // Instantiate character at spawn point
            GameObject characterInstance = Instantiate(
                characterPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            // Name it for debugging
            characterInstance.name = $"MenuCharacter_{i}";

            spawnedCharacters.Add(characterInstance);

            // Setup animations
            SetupCharacterAnimations(characterInstance, i);

            // Make character look at campfire (if campfire exists)
            if (campfireCenter != null)
            {
                Vector3 lookAtPosition = campfireCenter.position;
                lookAtPosition.y = characterInstance.transform.position.y; // Keep same height
                characterInstance.transform.LookAt(lookAtPosition);
            }

            Debug.Log($"Spawned character {i} at position {spawnPoint.position}");
        }

        // Start idle behavior coroutines
        StartCoroutine(RandomIdleBehavior());

        isInitialized = true;
        Debug.Log($"Main menu setup complete: {spawnedCharacters.Count} characters spawned");
    }

    private void SetupCharacterAnimations(GameObject character, int characterIndex)
    {
        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            characterAnimators.Add(animator);

            // Start with sitting animation
            animator.Play(sittingAnimation, 0, Random.Range(0f, 1f)); // Random start time for variation

            // Set animation speed variation for natural look
            float speedVariation = 0.9f + (characterIndex * 0.05f);
            animator.speed = speedVariation;

            Debug.Log($"Set up animations for character {characterIndex}");
        }
        else
        {
            Debug.LogWarning($"Character {characterIndex} has no Animator component!");
        }
    }

    private IEnumerator RandomIdleBehavior()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minIdleTime, maxIdleTime));

            // Randomly trigger talking animation on random character
            if (characterAnimators.Count > 0)
            {
                int randomIndex = Random.Range(0, characterAnimators.Count);
                Animator randomAnimator = characterAnimators[randomIndex];

                if (randomAnimator != null && randomAnimator.isActiveAndEnabled)
                {
                    // Play talking animation
                    randomAnimator.SetTrigger(talkingAnimation);

                    // Return to sitting after random time
                    yield return new WaitForSeconds(Random.Range(minTalkTime, maxTalkTime));

                    if (randomAnimator != null)
                    {
                        randomAnimator.Play(sittingAnimation);
                    }
                }
            }
        }
    }

    public void ClearCharacters()
    {
        Debug.Log($"Clearing {spawnedCharacters.Count} main menu characters...");

        foreach (GameObject character in spawnedCharacters)
        {
            if (character != null)
            {
                Destroy(character);
            }
        }

        spawnedCharacters.Clear();
        characterAnimators.Clear();

        // Stop all coroutines
        StopAllCoroutines();

        isInitialized = false;
    }

    private void OnDestroy()
    {
        ClearCharacters();
    }

    // Debug method
    [ContextMenu("Debug Spawn Info")]
    public void DebugSpawnInfo()
    {
        Debug.Log($"=== Main Menu Character Debug ===");
        Debug.Log($"Is in MainMenu scene: {SceneManager.GetActiveScene().name == "MainMenu"}");
        Debug.Log($"Prefabs assigned: {mainMenuCharacterPrefabs.Length}");
        Debug.Log($"Spawn points assigned: {characterSpawnPoints.Length}");
        Debug.Log($"Characters spawned: {spawnedCharacters.Count}");

        for (int i = 0; i < characterSpawnPoints.Length; i++)
        {
            if (characterSpawnPoints[i] != null)
                Debug.Log($"Spawn point {i}: {characterSpawnPoints[i].position}");
            else
                Debug.LogError($"Spawn point {i} is NULL!");
        }
    }
}