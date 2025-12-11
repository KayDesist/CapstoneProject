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

    // Called when object spawns on network
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializeTasks();
        }

        hudManager = GameHUDManager.Instance;
    }

    // Initializes tasks
    private void InitializeTasks()
    {
        survivorTasks.Clear();
        cultistTasks.Clear();

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
    }

    // Server RPC to update task progress
    [ServerRpc(RequireOwnership = false)]
    public void UpdateTaskProgressServerRpc(int taskIndex, bool isSurvivorTask, int progressToAdd)
    {
        if (isSurvivorTask && taskIndex >= 0 && taskIndex < survivorTasks.Count)
        {
            TaskData task = survivorTasks[taskIndex];

            task.currentProgress = Mathf.Min(task.currentProgress + progressToAdd, task.requiredProgress);
            bool wasCompleted = task.isCompleted;
            task.isCompleted = (task.currentProgress >= task.requiredProgress);

            survivorTasks[taskIndex] = task;

            UpdateTaskClientRpc(taskIndex, true, task.currentProgress, task.requiredProgress, task.isCompleted);

            if (!wasCompleted && task.isCompleted && AreAllTasksComplete(true))
            {
                if (EndGameManager.Instance != null)
                {
                    EndGameManager.Instance.OnTasksCompleted(RoleManager.PlayerRole.Survivor);
                }
            }
        }
        else if (!isSurvivorTask && taskIndex >= 0 && taskIndex < cultistTasks.Count)
        {
            TaskData task = cultistTasks[taskIndex];

            task.currentProgress = Mathf.Min(task.currentProgress + progressToAdd, task.requiredProgress);
            bool wasCompleted = task.isCompleted;
            task.isCompleted = (task.currentProgress >= task.requiredProgress);

            cultistTasks[taskIndex] = task;

            UpdateTaskClientRpc(taskIndex, false, task.currentProgress, task.requiredProgress, task.isCompleted);

            if (!wasCompleted && task.isCompleted && AreAllTasksComplete(false))
            {
                if (EndGameManager.Instance != null)
                {
                    EndGameManager.Instance.OnTasksCompleted(RoleManager.PlayerRole.Cultist);
                }
            }
        }
    }

    // Client RPC to update task
    [ClientRpc]
    private void UpdateTaskClientRpc(int taskIndex, bool isSurvivorTask, int currentProgress, int requiredProgress, bool isCompleted)
    {
        if (hudManager != null)
        {
            if (RoleManager.Instance != null)
            {
                var localPlayerRole = RoleManager.Instance.GetLocalPlayerRole();

                if ((isSurvivorTask && localPlayerRole == RoleManager.PlayerRole.Survivor) ||
                    (!isSurvivorTask && localPlayerRole == RoleManager.PlayerRole.Cultist))
                {
                    string status = isCompleted ? " ✓" : $" ({currentProgress}/{requiredProgress})";
                    string taskDescription = $"- {GetBaseDescriptionForTask(taskIndex, isSurvivorTask)}{status}";

                    hudManager.UpdateTaskProgress(taskIndex, taskDescription);
                }
            }
        }
    }

    // Gets base description for task
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

    // Checks if all tasks for a role are complete
    public bool AreAllTasksComplete(bool isSurvivorTasks)
    {
        var taskList = isSurvivorTasks ? survivorTasks : cultistTasks;

        if (taskList.Count == 0)
        {
            return false;
        }

        foreach (var task in taskList)
        {
            if (!task.isCompleted)
            {
                return false;
            }
        }

        return true;
    }

    // Gets survivor tasks for UI
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

    // Gets cultist tasks for UI
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

    // Resets static instance
    public static void ResetInstance()
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
        }
    }

    // Test method to complete first survivor task
    [ContextMenu("Test Complete First Survivor Task")]
    public void TestCompleteFirstSurvivorTask()
    {
        if (IsServer)
        {
            UpdateTaskProgressServerRpc(0, true, 3);
        }
    }

    // Test method to complete all survivor tasks
    [ContextMenu("Test Complete All Survivor Tasks")]
    public void TestCompleteAllSurvivorTasks()
    {
        if (IsServer)
        {
            for (int i = 0; i < survivorTasks.Count; i++)
            {
                UpdateTaskProgressServerRpc(i, true, 10);
            }
        }
    }

    // Test method to complete first cultist task
    [ContextMenu("Test Complete First Cultist Task")]
    public void TestCompleteFirstCultistTask()
    {
        if (IsServer)
        {
            UpdateTaskProgressServerRpc(0, false, 3);
        }
    }

    // Test method to complete all cultist tasks
    [ContextMenu("Test Complete All Cultist Tasks")]
    public void TestCompleteAllCultistTasks()
    {
        if (IsServer)
        {
            for (int i = 0; i < cultistTasks.Count; i++)
            {
                UpdateTaskProgressServerRpc(i, false, 10);
            }
        }
    }

    // Debugs task states
    [ContextMenu("Debug Task States")]
    public void DebugTaskStates()
    {
        for (int i = 0; i < survivorTasks.Count; i++)
        {
            var task = survivorTasks[i];
        }

        for (int i = 0; i < cultistTasks.Count; i++)
        {
            var task = cultistTasks[i];
        }
    }

    // Called when object despawns from network
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