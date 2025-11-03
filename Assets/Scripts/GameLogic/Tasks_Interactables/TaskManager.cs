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
    }

    private void InitializeTasks()
    {
        // Initialize survivor tasks
        survivorTasks.Add(new TaskData
        {
            description = "Repair Generator",
            currentProgress = 0,
            requiredProgress = 3,
            isCompleted = false
        });
        survivorTasks.Add(new TaskData
        {
            description = "Collect Firewood",
            currentProgress = 0,
            requiredProgress = 5,
            isCompleted = false
        });
        survivorTasks.Add(new TaskData
        {
            description = "Find Car Keys",
            currentProgress = 0,
            requiredProgress = 1,
            isCompleted = false
        });

        // Initialize cultist tasks
        cultistTasks.Add(new TaskData
        {
            description = "Place Ritual Candles",
            currentProgress = 0,
            requiredProgress = 3,
            isCompleted = false
        });
        cultistTasks.Add(new TaskData
        {
            description = "Collect Sacrificial Items",
            currentProgress = 0,
            requiredProgress = 2,
            isCompleted = false
        });
        cultistTasks.Add(new TaskData
        {
            description = "Activate Altars",
            currentProgress = 0,
            requiredProgress = 2,
            isCompleted = false
        });
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateTaskProgressServerRpc(int taskIndex, bool isSurvivorTask, int progress)
    {
        if (isSurvivorTask && taskIndex >= 0 && taskIndex < survivorTasks.Count)
        {
            TaskData task = survivorTasks[taskIndex];
            task.currentProgress = progress;
            task.isCompleted = (task.currentProgress >= task.requiredProgress);

            string status = task.isCompleted ? " ?" : $" ({task.currentProgress}/{task.requiredProgress})";
            task.description = $"{task.description.ToString().Split('(')[0].Trim()}{status}";

            survivorTasks[taskIndex] = task;
            UpdateTaskClientRpc(taskIndex, isSurvivorTask, task.description.ToString());
        }
        else if (!isSurvivorTask && taskIndex >= 0 && taskIndex < cultistTasks.Count)
        {
            TaskData task = cultistTasks[taskIndex];
            task.currentProgress = progress;
            task.isCompleted = (task.currentProgress >= task.requiredProgress);

            string status = task.isCompleted ? " ?" : $" ({task.currentProgress}/{task.requiredProgress})";
            task.description = $"{task.description.ToString().Split('(')[0].Trim()}{status}";

            cultistTasks[taskIndex] = task;
            UpdateTaskClientRpc(taskIndex, isSurvivorTask, task.description.ToString());
        }
    }

    [ClientRpc]
    private void UpdateTaskClientRpc(int taskIndex, bool isSurvivorTask, string taskDescription)
    {
        if (hudManager != null)
        {
            hudManager.UpdateTaskProgress(taskIndex, taskDescription);
        }
    }

    public List<string> GetSurvivorTasksForUI()
    {
        List<string> tasks = new List<string>();
        foreach (var task in survivorTasks)
        {
            string status = task.isCompleted ? " ?" : $" ({task.currentProgress}/{task.requiredProgress})";
            tasks.Add($"- {task.description.ToString().Split('(')[0].Trim()}{status}");
        }
        return tasks;
    }

    public List<string> GetCultistTasksForUI()
    {
        List<string> tasks = new List<string>();
        foreach (var task in cultistTasks)
        {
            string status = task.isCompleted ? " ?" : $" ({task.currentProgress}/{task.requiredProgress})";
            tasks.Add($"- {task.description.ToString().Split('(')[0].Trim()}{status}");
        }
        return tasks;
    }

    // Test method to simulate task completion
    [ContextMenu("Test Complete First Task")]
    public void TestCompleteFirstTask()
    {
        if (IsServer)
        {
            UpdateTaskProgressServerRpc(0, true, 3); // Complete first survivor task
        }
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
    public FixedString32Bytes description;
    public int currentProgress;
    public int requiredProgress;
    public bool isCompleted;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref description);
        serializer.SerializeValue(ref currentProgress);
        serializer.SerializeValue(ref requiredProgress);
        serializer.SerializeValue(ref isCompleted);
    }

    public bool Equals(TaskData other)
    {
        return description.Equals(other.description) &&
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
        return HashCode.Combine(description, currentProgress, requiredProgress, isCompleted);
    }
}