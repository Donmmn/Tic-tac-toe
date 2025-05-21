using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 如果直接传递Sprite，或者为了PopupWindows内部类型，则需要此项
using System.Linq; // For Linq operations like FirstOrDefault
using System.IO; // For Path operations if saving outside Resources (not needed for Resources.Load)

[System.Serializable]
public class Tutorial
{
    public int Id;
    public string[] TutorialText;
    public string[] ImageNames;
}

// This wrapper is needed for JsonUtility to parse a list from the root of a JSON file.
// If this is already defined in a runtime-accessible script, this duplicate can be removed.
[System.Serializable]
public class TutorialListWrapper 
{
    public List<Tutorial> Tutorials;
}

// 新增：用于JsonUtility序列化结局列表的包装类
[System.Serializable]
public class EndingListWrapper
{
    public List<Ending> Endings;
}

// 新增：用于JsonUtility序列化解锁码的包装类
[System.Serializable]
public class UnlockCodeData
{
    public string Code;
}

[System.Serializable]
public class Ending
{
    public int Id;
    public int MinScore;      // 新增：最低解锁分数
    public int MaxScore;      // 新增：最高解锁分数
    public bool PlayerWin;
    public string title;
    public string[] EndingText;
    public string[] ImagesName;
    public string BackgroundImagePath; // 新增：结局背景图片路径
    public string BGMusicPath;         // 新增：结局背景音乐路径
}
    
// 新增：用于保存和加载玩家进度的包装类
[System.Serializable]
public class PlayerProgressData
{
    public int[] UnlockedEndingIds; // 为了清晰，重命名以表明是ID数组
    public int[] CompletedTutorialIds;

    public PlayerProgressData()
    {
        UnlockedEndingIds = new int[0];
        CompletedTutorialIds = new int[0];
    }
}
public enum EndingType
{
    newEnding,
    oldEnding,
    lockedEnding,
}

// 新增：用于封装结局检查结果的结构体
public struct EndingCheckResult
{
    public EndingType Type;
    public int? EndingId; // Nullable int，因为 lockedEnding 可能没有关联的ID

    public EndingCheckResult(EndingType type, int? endingId)
    {
        Type = type;
        EndingId = endingId;
    }
}

public class PlayerProcess : MonoBehaviourSingleton<PlayerProcess>
{
    public int[] UnlockedEndings = new int[0];
    public int[] CompletedTutorials = new int[0];
    
    private List<Tutorial> allTutorials = new List<Tutorial>();
    private bool tutorialsLoaded = false;
    private const string TutorialsJsonPath = "lines/tutorials"; // Path relative to Resources folder
    
    // 新增：结局数据相关
    private List<Ending> allEndings = new List<Ending>();
    private bool endingsLoaded = false;
    private const string EndingsJsonPath = "lines/endings"; // 相对于 Resources 文件夹的路径

    private const string PlayerProgressJsonPath = "Config/player_progress"; // Resources下的玩家进度JSON路径
    private const string UnlockCodeJsonPath = "Config/unlock_code"; // 新增：解锁码JSON路径

    private string currentUnlockCode = ""; // 新增：存储当前解锁码

    // Awake 通常用于自身的初始化，尤其是单例模式的Instance设置。
    // 如果 Instance 在 Awake 中设置，其他脚本在 Awake 中可能还无法访问 PlayerProcess.Instance。
    // Start 会在所有 Awake 完成后执行，是进行依赖其他单例或加载数据的一个好时机。
    // 或者使用一个初始化的 public 方法由游戏管理器调用。
    // 我们先尝试在 Awake 中加载，如果遇到时序问题再调整。
    protected override void Awake()
    {
        base.Awake(); // 如果 MonoBehaviourSingleton 有自己的 Awake 逻辑
        LoadAllTutorialsFromJson(); // 加载所有教程定义
        LoadAllEndingsFromJson();   // 新增：加载所有结局定义
        LoadPlayerProgress();      // 加载玩家进度
        LoadUnlockCodeFromJson();  // 新增：加载解锁码
    }

    private void LoadAllTutorialsFromJson()
    {
        if (tutorialsLoaded) return;

        TextAsset tutorialsJsonAsset = Resources.Load<TextAsset>(TutorialsJsonPath);
        if (tutorialsJsonAsset == null)
        {
            Debug.LogError($"玩家处理：从 Resources/{TutorialsJsonPath}.json 加载教程JSON文件失败");
            allTutorials = new List<Tutorial>(); 
            tutorialsLoaded = true; 
            return;
        }

        try
        {
            TutorialListWrapper wrapper = JsonUtility.FromJson<TutorialListWrapper>(tutorialsJsonAsset.text);
            if (wrapper != null && wrapper.Tutorials != null)
            {
                allTutorials = wrapper.Tutorials;
                Debug.Log($"玩家处理：已成功从JSON加载 {allTutorials.Count} 个教程。");
            }
            else
            {
                Debug.LogError("玩家处理：从JSON解析教程失败。包装器或教程列表为null。");
                allTutorials = new List<Tutorial>();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"玩家处理：解析教程JSON时发生异常: {ex.Message}");
            allTutorials = new List<Tutorial>();
        }
        tutorialsLoaded = true;
    }

    // 新增：从JSON加载所有结局定义的方法
    private void LoadAllEndingsFromJson()
    {
        if (endingsLoaded) return;

        TextAsset endingsJsonAsset = Resources.Load<TextAsset>(EndingsJsonPath);
        if (endingsJsonAsset == null)
        {
            Debug.LogWarning($"玩家处理：从 Resources/{EndingsJsonPath}.json 加载结局JSON文件失败。将使用空列表。");
            allEndings = new List<Ending>();
            endingsLoaded = true;
            return;
        }

        try
        {
            EndingListWrapper wrapper = JsonUtility.FromJson<EndingListWrapper>(endingsJsonAsset.text);
            if (wrapper != null && wrapper.Endings != null)
            {
                allEndings = wrapper.Endings;
                Debug.Log($"玩家处理：已成功从JSON加载 {allEndings.Count} 个结局。");
            }
            else
            {
                Debug.LogError("玩家处理：从JSON解析结局失败。包装器或结局列表为null。");
                allEndings = new List<Ending>();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"玩家处理：解析结局JSON时发生异常: {ex.Message}");
            allEndings = new List<Ending>();
        }
        endingsLoaded = true;
    }

    // 新增：从JSON加载解锁码的方法
    private void LoadUnlockCodeFromJson()
    {
        TextAsset unlockCodeJsonAsset = Resources.Load<TextAsset>(UnlockCodeJsonPath);
        if (unlockCodeJsonAsset == null)
        {
            Debug.LogWarning($"玩家处理：从 Resources/{UnlockCodeJsonPath}.json 加载解锁码JSON文件失败。解锁码功能可能不可用。");
            currentUnlockCode = ""; // 或者设置为一个不可能被猜到的默认值，以防文件不存在时仍能通过空码解锁
            return;
        }

        try
        {
            UnlockCodeData data = JsonUtility.FromJson<UnlockCodeData>(unlockCodeJsonAsset.text);
            if (data != null && !string.IsNullOrEmpty(data.Code))
            {
                currentUnlockCode = data.Code;
                Debug.Log("玩家处理：已成功加载解锁码。");
            }
            else
            {
                Debug.LogError("玩家处理：从JSON解析解锁码失败，或解锁码为空。");
                currentUnlockCode = "";
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"玩家处理：解析解锁码JSON时发生异常: {ex.Message}");
            currentUnlockCode = "";
        }
    }

    /// <summary>
    /// Gets a tutorial by its ID. Loads tutorials from JSON if not already loaded.
    /// </summary>
    /// <param name="id">The ID of the tutorial to retrieve.</param>
    /// <returns>The Tutorial object if found; otherwise, null.</returns>
    public Tutorial GetTutorialById(int id)
    {
        if (!tutorialsLoaded)
        {
            LoadAllTutorialsFromJson();
        }

        Tutorial tutorial = allTutorials.FirstOrDefault(t => t.Id == id);
        if (tutorial == null)
        {
            Debug.LogWarning($"玩家处理：未找到ID为 {id} 的教程。");
        }
        return tutorial;
    }

    // 新增：根据ID获取结局的方法
    /// <summary>
    /// 根据ID获取结局。如果尚未加载，则从JSON加载结局。
    /// </summary>
    /// <param name="id">要检索的结局ID。</param>
    /// <returns>如果找到则返回 Ending 对象；否则返回 null。</returns>
    public Ending GetEndingById(int id)
    {
        if (!endingsLoaded)
        {
            LoadAllEndingsFromJson();
        }

        Ending ending = allEndings.FirstOrDefault(e => e.Id == id);
        if (ending == null)
        {
            Debug.LogWarning($"玩家处理：未找到ID为 {id} 的结局。");
        }
        return ending;
    }

    /// <summary>
    /// 加载玩家进度（已解锁结局和已完成教程）。
    /// </summary>
    public void LoadPlayerProgress()
    {
        TextAsset progressJsonAsset = Resources.Load<TextAsset>(PlayerProgressJsonPath);
        if (progressJsonAsset != null)
        {
            try
            {
                PlayerProgressData data = JsonUtility.FromJson<PlayerProgressData>(progressJsonAsset.text);
                if (data != null)
                {
                    UnlockedEndings = data.UnlockedEndingIds ?? new int[0];
                    CompletedTutorials = data.CompletedTutorialIds ?? new int[0];
                    Debug.Log("玩家处理：玩家进度已成功加载。");
                }
                else
                {
                    Debug.LogWarning("玩家处理：未能从JSON解析玩家进度数据，将使用默认空进度。");
                    InitializeEmptyProgress();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"玩家处理：解析玩家进度JSON时发生异常: {ex.Message}。将使用默认空进度。");
                InitializeEmptyProgress();
            }
        }
        else
        {
            Debug.LogWarning($"玩家处理：未在 Resources/{PlayerProgressJsonPath}.json 找到玩家进度文件。将创建并使用新的空进度。");
            InitializeEmptyProgress();
            SavePlayerProgress(); // 如果文件不存在，则创建一个新的空进度文件
        }
    }

    private void InitializeEmptyProgress()
    {
        UnlockedEndings = new int[0];
        CompletedTutorials = new int[0];
    }

    /// <summary>
    /// 保存玩家进度（已解锁结局和已完成教程）。
    /// 注意：运行时直接写入 Resources 文件夹通常不可行或不被推荐。
    /// 此方法主要用于编辑器中测试，或用于保存到 Application.persistentDataPath。
    /// 如果要在编辑器内更新Resources文件，需要使用UnityEditor命名空间下的方法。
    /// </summary>
    public void SavePlayerProgress()
    {
        PlayerProgressData data = new PlayerProgressData
        {
            UnlockedEndingIds = this.UnlockedEndings,
            CompletedTutorialIds = this.CompletedTutorials
        };

        string json = JsonUtility.ToJson(data, true);
        
        // **重要说明关于保存路径**
        // 1. 在编辑器中直接写入 Application.dataPath + "/Resources/..." 可以工作，但需要 AssetDatabase.Refresh()。
        // 2. 在构建的游戏中，Resources 文件夹是只读的。不能直接写入。
        // 3. 对于运行时可读写的数据，应使用 Application.persistentDataPath。

        // 此处为了与Resources加载逻辑对应，我们假设这是在编辑器环境下用于生成默认配置文件，
        // 或者游戏设计允许通过特定方式（如调试菜单）触发此保存到 Resources （需要特殊处理）。
        // 对于常规游戏存档，请改用 Application.persistentDataPath。

        string resourcesFolderPath = Path.Combine(Application.dataPath, "Resources");
        string targetDirectoryPath = Path.Combine(resourcesFolderPath, "Config"); // 保存到 Resources/Config
        string filePath = Path.Combine(targetDirectoryPath, "player_progress.json");

        try
        {
            if (!Directory.Exists(targetDirectoryPath)) // 检查并创建 Resources/Config 文件夹
            {
                if (!Directory.Exists(resourcesFolderPath))
                {
                    Directory.CreateDirectory(resourcesFolderPath); // 如果Resources文件夹也不存在，则创建它
                }
                Directory.CreateDirectory(targetDirectoryPath);
            }
            File.WriteAllText(filePath, json);
            Debug.Log($"玩家处理：玩家进度已保存到 {filePath}。如果在编辑器中，请刷新资源数据库以在项目中查看。");

            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            #endif
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"玩家处理：保存玩家进度到 {filePath} 时发生错误: {ex.Message}");
        }
    }

    // 示例：当玩家完成一个教程时调用
    public void MarkTutorialAsCompleted(int tutorialId)
    {
        if (!CompletedTutorials.Contains(tutorialId))
        {
            List<int> tempList = CompletedTutorials.ToList();
            tempList.Add(tutorialId);
            CompletedTutorials = tempList.ToArray();
            Debug.Log($"教程 {tutorialId} 已标记为完成。");
            // SavePlayerProgress(); // 可选择在每次更新后保存
        }
    }

    // 示例：当玩家解锁一个结局时调用
    public void UnlockEnding(int endingId)
    {
        if (!UnlockedEndings.Contains(endingId))
        {
            List<int> tempList = UnlockedEndings.ToList();
            tempList.Add(endingId);
            UnlockedEndings = tempList.ToArray();
            Debug.Log($"结局 {endingId} 已解锁。");
            // SavePlayerProgress(); // 可选择在每次更新后保存
        }
    }

    // [Header("教程系统引用")] // 移除此Header
    // public GameObject tutorialPopupPrefab; // 移除此字段，教程弹窗由UIManager处理
    //private PopupWindows activeTutorialPopupInstance; // 当前活动的教程弹窗实例
    //private int currentTutorialIdForPopup = -1; // 用于记录当前正在显示的教程ID

    /// <summary>
    /// 根据玩家得分和胜利状态检查并尝试解锁结局。
    /// </summary>
    /// <param name="playerScore">玩家当前得分。</param>
    /// <param name="playerHasWon">玩家是否胜利。</param>
    /// <returns>一个 EndingCheckResult 结构，包含结局的类型和ID。</returns>
    public EndingCheckResult CheckAndUnlockEnding(int playerScore, bool playerHasWon)
    {
        if (!endingsLoaded)
        {
            LoadAllEndingsFromJson(); // 确保结局已加载
        }

        Ending firstMetNewEnding = null;
        Ending firstMetOldEnding = null;

        // 遍历所有结局，优先查找可解锁的新结局，然后是符合条件的旧结局
        // 可以考虑对 allEndings 进行排序以实现特定的解锁优先级，例如 OrderBy(e => e.Id) 或 OrderByDescending(e => e.RequiredScore)
        foreach (Ending ending in allEndings.OrderBy(e => e.Id)) // 按ID排序以保证确定性
        {
            if (playerScore >= ending.MinScore && playerScore <= ending.MaxScore && playerHasWon == ending.PlayerWin)
            {
                if (!UnlockedEndings.Contains(ending.Id)) // 条件满足，且是新结局
                {
                    if (firstMetNewEnding == null) // 只记录第一个满足条件的新结局
                    {
                        firstMetNewEnding = ending;
                    }
                }
                else // 条件满足，但是是已解锁的旧结局
                {
                    if (firstMetOldEnding == null) // 只记录第一个满足条件的旧结局
                    {
                        firstMetOldEnding = ending;
                    }
                }
            }
        }

        // 根据查找结果决定返回类型
        if (firstMetNewEnding != null)
        {
            List<int> tempList = UnlockedEndings.ToList();
            tempList.Add(firstMetNewEnding.Id);
            UnlockedEndings = tempList.ToArray();
            SavePlayerProgress(); // 保存进度
            Debug.Log($"玩家处理：新结局 ID: {firstMetNewEnding.Id} ({firstMetNewEnding.title}) 已解锁!");
            return new EndingCheckResult(EndingType.newEnding, firstMetNewEnding.Id);
        }
        else if (firstMetOldEnding != null)
        {
            Debug.Log($"玩家处理：满足已解锁的结局 ID: {firstMetOldEnding.Id} ({firstMetOldEnding.title}) 的条件。");
            return new EndingCheckResult(EndingType.oldEnding, firstMetOldEnding.Id);
        }
        else
        {
            Debug.Log("玩家处理：没有满足解锁条件的结局。");
            return new EndingCheckResult(EndingType.lockedEnding, null);
        }
    }

    /// <summary>
    /// 重置所有数据，包括结局解锁状态和教程完成状态
    /// </summary>
    public void ResetAllData()
    {
        // 清空已解锁的结局
        UnlockedEndings = new int[0];
        
        // 清空已完成的教程
        CompletedTutorials = new int[0];
        
        // 保存更改
        SavePlayerProgress();
        
        Debug.Log("PlayerProcess: 已重置所有数据");
    }

    /// <summary>
    /// 仅重置教程完成状态
    /// </summary>
    public void ResetTutorialStatus()
    {
        // 只清空已完成的教程
        CompletedTutorials = new int[0];
        
        // 保存更改
        SavePlayerProgress();
        
        Debug.Log("PlayerProcess: 已重置教程状态");
    }

    /// <summary>
    /// 解锁所有结局
    /// </summary>
    public void UnlockAllEndings()
    {
        // 获取所有结局的ID
        List<int> allEndingIds = new List<int>();
        foreach (Ending ending in allEndings)
        {
            allEndingIds.Add(ending.Id);
        }
        
        // 更新已解锁的结局列表
        UnlockedEndings = allEndingIds.ToArray();
        
        // 保存更改
        SavePlayerProgress();
        
        Debug.Log("PlayerProcess: 已解锁所有结局");
    }

    /// <summary>
    /// 验证解锁码并解锁所有结局（如果正确）。
    /// </summary>
    /// <param name="inputCode">用户输入的解锁码。</param>
    /// <returns>如果解锁码正确则返回true，否则返回false。</returns>
    public bool VerifyAndUnlockEndingsByCode(string inputCode)
    {
        if (string.IsNullOrEmpty(currentUnlockCode))
        {
            Debug.LogWarning("玩家处理：解锁码未加载或为空，无法验证。");
            return false;
        }

        if (inputCode == currentUnlockCode)
        {
            UnlockAllEndings();
            Debug.Log("玩家处理：解锁码正确，已解锁所有结局。");
            return true;
        }
        else
        {
            Debug.LogWarning("玩家处理：输入的解锁码不正确。");
            return false;
        }
    }
}
