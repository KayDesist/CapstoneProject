using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Unity.Collections;
using System;

public class TaskManager : NetworkBehaviour
{
    public static TaskManager Instance;

    private NetworkList<TaskData> survivorTasks;
    private NetworkList<TaskData> cultistTasks;

    private GameHUDManager hudManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        survivorTasks = new NetworkList<TaskData>();
        cultistTasks = new NetworkList<TaskData>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializeTasks();
        }

        // All clients should get the HUD manager reference
        hudManager = GameHUDManager.Instance;

        Debug.Log("TaskManager spawned and ready");
    }

    private void InitializeTasks()
    {
        // Clear any existing tasks
        survivorTasks.Clear();
        cultistTasks.Clear();

        // Initialize survivor tasks
        survivorTasks.Add(new TaskData
        {
            baseDescription = "Repair Generator",
            currentProgress = 0,
            requiredProgress = 3,
            isCompleted = false
        });
        survivorTasks.Add(new TaskData
        {
            baseDescription = "Collect Firewood",
            currentProgress = 0,
            requiredProgress = 5,
            isCompleted = false
        });
        survivorTasks.Add(new TaskData
        {
            baseDescription = "Find Car Keys",
            currentProgress = 0,
            requiredProgress = 1,
            isCompleted = false
        });

        // Initialize cultist tasks
        cultistTasks.Add(new TaskData
        {
            baseDescription = "Place Ritual Candles",
            currentProgress = 0,
            requiredProgress = 3,
            isCompleted = false
        });
        cultistTasks.Add(new TaskData
        {
            baseDescription = "Collect Sacrificial Items",
            currentProgress = 0,
            requiredProgress = 2,
            isCompleted = false
        });
        cultistTasks.Add(new TaskData
        {
            baseDescription = "Activate Altars",
            currentProgress = 0,
            requiredProgress = 2,
            isCompleted = false
        });

        Debug.Log($"Initialized {survivorTasks.Count} survivor tasks and {cultistTasks.Count} cultist tasks");
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateTaskProgressServerRpc(int taskIndex, bool isSurvivorTask, int progressToAdd)
    {
        Debug.Log($"ServerRpc: Updating task {taskIndex}, isSurvivor: {isSurvivorTask}, progressToAdd: {progressToAdd}");

        if (isSurvivorTask && taskIndex >= 0 && taskIndex < survivorTasks.Count)
        {
            TaskData task = survivorTasks[taskIndex];

            // Accumulate progress instead of setting it
            task.currentProgress = Mathf.Min(task.currentProgress + progressToAdd, task.requiredProgress);
            bool wasCompleted = task.isCompleted;
            task.isCompleted = (task.currentProgress >= task.requiredProgress);

            survivorTasks[taskIndex] = task;

            // Notify all clients about this survivor task update
            UpdateTaskClientRpc(taskIndex, true, task.currentProgress, task.requiredProgress, task.isCompleted);

            Debug.Log($"Updated survivor task {taskIndex}: {task.baseDescription} - Progress: {task.currentProgress}/{task.requiredProgress}, Completed: {task.isCompleted}");

            // Check if all survivor tasks are now complete
            if (!wasCompleted && task.isCompleted && AreAllTasksComplete(true))
            {
                Debug.Log("🎉 ALL SURVIVOR TASKS COMPLETED! Notifying EndGameManager...");
                if (EndGameManager.Instance != null)
                {
                    EndGameManager.Instance.OnTasksCompleted(RoleManager.PlayerRole.Survivor);
                }
                else
                {
                    Debug.LogError("EndGameManager.Instance is null! Cannot notify task completion.");
                }
            }
        }
        else if (!isSurvivorTask && taskIndex >= 0 && taskIndex < cultistTasks.Count)
        {
            TaskData task = cultistTasks[taskIndex];

            // Accumulate progress instead of setting it
            task.currentProgress = Mathf.Min(task.currentProgress + progressToAdd, task.requiredProgress);
            bool wasCompleted = task.isCompleted;
            task.isCompleted = (task.currentProgress >= task.requiredProgress);

            cultistTasks[taskIndex] = task;

            // Notify all clients about this cultist task update
            UpdateTaskClientRpc(taskIndex, false, task.currentProgress, task.requiredProgress, task.isCompleted);

            Debug.Log($"Updated cultist task {taskIndex}: {task.baseDescription} - Progress: {task.currentProgress}/{task.requiredProgress}, Completed: {task.isCompleted}");

            // Check if all cultist tasks are now complete
            if (!wasCompleted && task.isCompleted && AreAllTasksComplete(false))
            {
                Debug.Log("🎉 ALL CULTIST TASKS COMPLETED! Notifying EndGameManager...");
                if (EndGameManager.Instance != null)
                {
                    EndGameManager.Instance.OnTasksCompleted(RoleManager.PlayerRole.Cultist);
                }
                else
                {
                    Debug.LogError("EndGameManager.Instance is null! Cannot notify task completion.");
                }
            }
        }
        else
        {
            Debug.LogWarning($"Invalid task update - Index: {taskIndex}, IsSurvivor: {isSurvivorTask}");
        }
    }

    [ClientRpc]
    private void UpdateTaskClientRpc(int taskIndex, bool isSurvivorTask, int currentProgress, int requiredProgress, bool isCompleted)
    {
        Debug.Log($"ClientRpc: Task {taskIndex}, isSurvivor: {isSurvivorTask}, progress: {currentProgress}/{requiredProgress}, completed: {isCompleted}");

        if (hudManager != null)
        {
            // Only update if this client's role matches the task type
            if (RoleManager.Instance != null)
            {
                var localPlayerRole = RoleManager.Instance.GetLocalPlayerRole();

                if ((isSurvivorTask && localPlayerRole == RoleManager.PlayerRole.Survivor) ||
                    (!isSurvivorTask && localPlayerRole == RoleManager.PlayerRole.Cultist))
                {
                    string status = isCompleted ? " ✓" : $" ({currentProgress}/{requiredProgress})";
                    string taskDescription = $"- {GetBaseDescriptionForTask(taskIndex, isSurvivorTask)}{status}";

                    hudManager.UpdateTaskProgress(taskIndex, taskDescription);
                    Debug.Log($"Updating HUD for local player - Task: {taskDescription}");
                }
                else
                {
                    Debug.Log($"Skipping HUD update - Local role {localPlayerRole} doesn't match task type {isSurvivorTask}");
                }
            }
        }
    }

    private string GetBaseDescriptionForTask(int taskIndex, bool isSurvivorTask)
    {
        if (isSurvivorTask && taskIndex < survivorTasks.Count)
        {
            return survivorTasks[taskIndex].baseDescription.ToString();
        }
        else if (!isSurvivorTask && taskIndex < cultistTasks.Count)
        {
            return cultistTasks[taskIndex].baseDescription.ToString();
        }
        return "Unknown Task";
    }

    // Check if all tasks for a role are complete
    public bool AreAllTasksComplete(bool isSurvivorTasks)
    {
        var taskList = isSurvivorTasks ? survivorTasks : cultistTasks;

        if (taskList.Count == 0)
        {
            Debug.LogWarning($"Task list for {(isSurvivorTasks ? "Survivor" : "Cultist")} is empty!");
            return false;
        }

        foreach (var task in taskList)
        {
            if (!task.isCompleted)
            {
                Debug.Log($"Task {task.baseDescription} is not complete: {task.currentProgress}/{task.requiredProgress}");
                return false;
            }
        }

        Debug.Log($"All {(isSurvivorTasks ? "Survivor" : "Cultist")} tasks are complete!");
        return true;
    }

    public List<string> GetSurvivorTasksForUI()
    {
        List<string> tasks = new List<string>();
        foreach (var task in survivorTasks)
        {
            string status = task.isCompleted ? " ✓" : $" ({task.currentProgress}/{task.requiredProgress})";
            tasks.Add($"- {task.baseDescription.ToString()}{status}");
        }
        return tasks;
    }

    public List<string> GetCultistTasksForUI()
    {
        List<string> tasks = new List<string>();
        foreach (var task in cultistTasks)
        {
            string status = task.isCompleted ? " ✓" : $" ({task.currentProgress}/{task.requiredProgress})";
            tasks.Add($"- {task.baseDescription.ToString()}{status}");
        }
        return tasks;
    }

    // Test method to simulate task completion
    [ContextMenu("Test Complete First Survivor Task")]
    public void TestCompleteFirstSurvivorTask()
    {
        if (IsServer)
        {
            Debug.Log("TEST: Completing first survivor task");
            UpdateTaskProgressServerRpc(0, true, 3); // Complete first survivor task
        }
    }

    [ContextMenu("Test Complete All Survivor Tasks")]
    public void TestCompleteAllSurvivorTasks()
    {
        if (IsServer)
        {
            Debug.Log("TEST: Completing ALL survivor tasks");
            // Complete all survivor tasks
            for (int i = 0; i < survivorTasks.Count; i++)
            {
                UpdateTaskProgressServerRpc(i, true, 10); // Enough to complete any task
            }
        }
    }

    [ContextMenu("Test Complete First Cultist Task")]
    public void TestCompleteFirstCultistTask()
    {
        if (IsServer)
        {
            Debug.Log("TEST: Completing first cultist task");
            UpdateTaskProgressServerRpc(0, false, 3); // Complete first cultist task
        }
    }

    [ContextMenu("Test Complete All Cultist Tasks")]
    public void TestCompleteAllCultistTasks()
    {
        if (IsServer)
        {
            Debug.Log("TEST: Completing ALL cultist tasks");
            // Complete all cultist tasks
            for (int i = 0; i < cultistTasks.Count; i++)
            {
                UpdateTaskProgressServerRpc(i, false, 10); // Enough to complete any task
            }
        }
    }

    [ContextMenu("Debug Task States")]
    public void DebugTaskStates()
    {
        Debug.Log("=== SURVIVOR TASKS ===");
        for (int i = 0; i < survivorTasks.Count; i++)
        {
            var task = survivorTasks[i];
            Debug.Log($"Task {i}: {task.baseDescription} - Progress: {task.currentProgress}/{task.requiredProgress} - Completed: {task.isCompleted}");
        }

        Debug.Log("=== CULTIST TASKS ===");
        for (int i = 0; i < cultistTasks.Count; i++)
        {
            var task = cultistTasks[i];
            Debug.Log($"Task {i}: {task.baseDescription} - Progress: {task.currentProgress}/{task.requiredProgress} - Completed: {task.isCompleted}");
        }

        Debug.Log($"All survivor tasks complete: {AreAllTasksComplete(true)}");
        Debug.Log($"All cultist tasks complete: {AreAllTasksComplete(false)}");
    }

    public override void OnNetworkDespawn()
    {
        survivorTasks?.Dispose();
        cultistTasks?.Dispose();
    }
}

[System.Serializable]
public struct TaskData : INetworkSerializable, IEquatable<TaskData>
{
    public FixedString32Bytes baseDescription;
    public int currentProgress;
    public int requiredProgress;
    public bool isCompleted;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref baseDescription);
        serializer.SerializeValue(ref currentProgress);
        serializer.SerializeValue(ref requiredProgress);
        serializer.SerializeValue(ref isCompleted);
    }

    public bool Equals(TaskData other)
    {
        return baseDescription.Equals(other.baseDescription) &&
               currentProgress == other.currentProgress &&
               requiredProgress == other.requiredProgress &&
               isCompleted == other.isCompleted;
    }

    public override bool Equals(object obj)
    {
        return obj is TaskData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(baseDescription, currentProgress, requiredProgress, isCompleted);
    }
}