using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// 确保 EmotionType 枚举在项目中可访问
// 例如，它定义在 CharacterReaction.cs 文件的全局命名空间中
// public enum EmotionType { 普通, 开心, 生气, 骄傲, 调皮, 哭泣 } 

// Helper classes for deserializing emotionDialogues.json
[System.Serializable]
class DialogueDataEntry
{
    public string emotion; // Changed from EmotionType to string
    public List<string> texts;
}

[System.Serializable]
class DialogueDataFile
{
    public List<DialogueDataEntry> allDialogues = new List<DialogueDataEntry>(); // Renamed from dialogues
}

public class EventConfigEditorWindow : EditorWindow
{
    // --- 数据结构 ---
    [System.Serializable]
    public class EmotionChance
    {
        public EmotionType targetEmotion = EmotionType.普通;
        public float weight = 1f; 
        public int selectedDialogueIndex = -1; // -1 for "No Dialogue", 0+ for index in dialogue list for this emotion
    }

    [System.Serializable]
    public class EventActionSetting
    {
        public string eventIdentifier; 
        public float moodEffect = 0f;
        public float sanityEffect = 0f;
        [Range(0f, 1f)]
        public float dialogueTriggerProbability = 1f; // 新增：台词总触发概率
        public List<EmotionChance> emotionChances = new List<EmotionChance>();
        public bool isFoldedOut = true; 
    }

    [System.Serializable]
    public class GameEventSettingsCollection
    {
        public List<EventActionSetting> allEventSettings = new List<EventActionSetting>();
    }

    // --- 编辑器变量 ---
    private GameEventSettingsCollection gameEventSettingsCollection;
    private Vector2 scrollPosition;
    private Dictionary<EmotionType, List<string>> loadedEmotionDialogues = new Dictionary<EmotionType, List<string>>();
    private string emotionDialogueFilePath = ""; 

    private readonly List<string> predefinedEventIdentifiers = new List<string>
    {
        "OnPlayerWinsGame",
        "OnAIWinsGame",
        "OnGameDraw",
        "OnPlayerAlmostWins",
        "OnAIAlmostWins",
        "OnBoardReset",
        "OnPlayerBlockedAIWin"
    };
  
    [MenuItem("工具/事件配置编辑器")]
    public static void ShowWindow()
    {
        GetWindow<EventConfigEditorWindow>("事件配置编辑器");
    }

    private void OnEnable()
    {
        LoadEventSettings();
        LoadEmotionDialogues(); 
    }

    void OnGUI()
    {
        GUILayout.Label("游戏事件反应配置", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (gameEventSettingsCollection == null)
        {
            EditorGUILayout.HelpBox("配置数据未能加载或初始化。", MessageType.Warning);
            if (GUILayout.Button("尝试重新加载/初始化配置"))
            {
                LoadEventSettings();
                LoadEmotionDialogues(); 
            }
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (gameEventSettingsCollection.allEventSettings.Count == 0 && predefinedEventIdentifiers.Count > 0)
        {
             EditorGUILayout.HelpBox("没有找到可配置的事件或配置为空。请确保 predefinedEventIdentifiers 列表正确，并尝试重新加载或保存一次以生成默认配置。", MessageType.Info);
        }

        for (int i = 0; i < gameEventSettingsCollection.allEventSettings.Count; i++)
        {
            EventActionSetting setting = gameEventSettingsCollection.allEventSettings[i];
            
            setting.isFoldedOut = EditorGUILayout.Foldout(setting.isFoldedOut, $"事件: {setting.eventIdentifier}", true, EditorStyles.foldoutHeader);
            if (setting.isFoldedOut)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("直接影响值 (不受权重影响):", EditorStyles.boldLabel);
                setting.moodEffect = EditorGUILayout.FloatField("心情值变化量:", setting.moodEffect);
                setting.sanityEffect = EditorGUILayout.FloatField("理智值变化量:", setting.sanityEffect);
                GUILayout.Space(5);

                setting.dialogueTriggerProbability = EditorGUILayout.Slider("台词总触发概率:", setting.dialogueTriggerProbability, 0f, 1f);
                GUILayout.Space(5);

                EditorGUILayout.LabelField("权重触发表情及台词 (若总概率触发):", EditorStyles.boldLabel);
                if (setting.emotionChances == null) setting.emotionChances = new List<EmotionChance>();

                for (int j = 0; j < setting.emotionChances.Count; j++)
                {
                    EmotionChance chance = setting.emotionChances[j];
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true)); 
                    EmotionType previousEmotion = chance.targetEmotion;
                    chance.targetEmotion = (EmotionType)EditorGUILayout.EnumPopup("表情:", chance.targetEmotion);

                    List<string> dialoguesForCurrentEmotion = new List<string>();
                    if (loadedEmotionDialogues.TryGetValue(chance.targetEmotion, out var foundDialogues))
                    {
                        dialoguesForCurrentEmotion.AddRange(foundDialogues);
                    }

                    if (chance.targetEmotion != previousEmotion || chance.selectedDialogueIndex >= dialoguesForCurrentEmotion.Count)
                    {
                        chance.selectedDialogueIndex = -1; 
                    }

                    string[] dialoguePopupOptions = new string[dialoguesForCurrentEmotion.Count + 1];
                    dialoguePopupOptions[0] = "无台词";
                    for (int k_idx = 0; k_idx < dialoguesForCurrentEmotion.Count; k_idx++)
                    {
                        dialoguePopupOptions[k_idx + 1] = $"[{k_idx}] {TruncateText(dialoguesForCurrentEmotion[k_idx], 30)}";
                    }
                    
                    int currentDialoguePopupSelection = chance.selectedDialogueIndex + 1; 
                    int newDialoguePopupSelection = EditorGUILayout.Popup("台词:", currentDialoguePopupSelection, dialoguePopupOptions);
                    
                    if (newDialoguePopupSelection != currentDialoguePopupSelection)
                    {
                        chance.selectedDialogueIndex = newDialoguePopupSelection - 1; 
                    }
                    EditorGUILayout.EndVertical(); 

                    EditorGUILayout.BeginVertical(GUILayout.Width(100));
                    GUILayout.Label("权重:", GUILayout.Width(40));
                    chance.weight = EditorGUILayout.FloatField(chance.weight, GUILayout.Width(50));
                    EditorGUILayout.EndVertical();
                    
                    if (GUILayout.Button("-", GUILayout.Width(25), GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                    {
                        setting.emotionChances.RemoveAt(j);
                        Repaint();
                        GUI.FocusControl(null); 
                        break; 
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("添加表情权重项", GUILayout.Width(150))) 
                {
                    setting.emotionChances.Add(new EmotionChance { weight = 1f, selectedDialogueIndex = -1 }); 
                    Repaint();
                }
                
                EditorGUI.indentLevel--;
                GUILayout.Space(10);
            }
            if (i < gameEventSettingsCollection.allEventSettings.Count - 1) EditorGUILayout.Separator(); 
        }
        
        EditorGUILayout.EndScrollView();
        GUILayout.Space(20);

        if (GUILayout.Button("保存配置到 JSON"))
        {
            SaveEventSettings();
        }
        if (GUILayout.Button("重新加载配置 (放弃当前更改)"))
        {
            if (EditorUtility.DisplayDialog("重新加载配置", "确定要从文件重新加载配置吗？所有未保存的更改将丢失。", "确定", "取消"))
            {
                LoadEventSettings();
                LoadEmotionDialogues(); 
            }
        }
        if (GUILayout.Button("重置为默认配置"))
        {
            if (EditorUtility.DisplayDialog("重置配置", "确定要重置所有事件配置为默认值吗？当前文件中的设置将丢失，除非先保存。", "确定重置", "取消"))
            {
                InitializeDefaultSettings();
                Repaint(); 
            }
        }
        if (GUILayout.Button("刷新台词数据")) 
        {
            LoadEmotionDialogues();
        }
    }

    private string GetEventSettingsFilePath()
    {
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (!Directory.Exists(resourcesPath)) { Directory.CreateDirectory(resourcesPath); AssetDatabase.Refresh(); }
        string linesFolderPath = Path.Combine(resourcesPath, "lines"); 
        if (!Directory.Exists(linesFolderPath)) { Directory.CreateDirectory(linesFolderPath); AssetDatabase.Refresh(); }
        return Path.Combine(linesFolderPath, "gameEventSettings.json"); 
    }

    private void SaveEventSettings()
    {
        if (gameEventSettingsCollection == null)
        {
            Debug.LogError("无法保存配置：配置数据为空或未初始化。");
            EditorUtility.DisplayDialog("保存失败", "配置数据未初始化，无法保存。请尝试重新加载或重置。", "确定");
            return;
        }
        string filePath = GetEventSettingsFilePath();
        try
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh(); 
                Debug.Log($"已创建目录: {directory}");
            }

            string json = JsonUtility.ToJson(gameEventSettingsCollection, true);
            File.WriteAllText(filePath, json);
            AssetDatabase.Refresh(); 
            EditorUtility.DisplayDialog("保存成功", "事件配置已保存到: " + filePath.Replace(Application.dataPath, "Assets"), "确定");
        }
        catch (System.Exception e)
        {
            Debug.LogError("保存事件配置失败: " + e.Message);
            EditorUtility.DisplayDialog("保存失败", "保存事件配置时发生错误，详情请查看控制台。", "确定");
        }
    }

    private void InitializeDefaultSettings(bool forceRepopulate = true)
    {
        gameEventSettingsCollection = new GameEventSettingsCollection();
        gameEventSettingsCollection.allEventSettings = new List<EventActionSetting>();

        foreach (string eventId in predefinedEventIdentifiers)
        {
            gameEventSettingsCollection.allEventSettings.Add(new EventActionSetting
            {
                eventIdentifier = eventId,
                moodEffect = 0f,
                sanityEffect = 0f,
                dialogueTriggerProbability = 1f,
                emotionChances = new List<EmotionChance> { new EmotionChance { targetEmotion = EmotionType.普通, weight = 1f, selectedDialogueIndex = -1 } }, 
                isFoldedOut = true
            });
        }
        if (forceRepopulate) Repaint(); 
    }

    private void LoadEventSettings()
    {
        string filePath = GetEventSettingsFilePath();
        GameEventSettingsCollection loadedCollection = null;

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    loadedCollection = JsonUtility.FromJson<GameEventSettingsCollection>(json);
                }
            }
            catch (System.Exception e)
            { Debug.LogError($"加载事件配置JSON失败 ({filePath}): {e.Message}\n将使用或生成默认配置。"); }
        }

        if (loadedCollection == null || loadedCollection.allEventSettings == null)
        {
            if (!File.Exists(filePath) || loadedCollection == null) 
            {
                 Debug.Log($"未找到有效的事件配置文件 {filePath}，或文件为空/损坏。将生成并使用默认配置。");
                 InitializeDefaultSettings(false); 
            } else {
                 gameEventSettingsCollection = loadedCollection;
                 if (gameEventSettingsCollection.allEventSettings == null) 
                 {
                    gameEventSettingsCollection.allEventSettings = new List<EventActionSetting>();
                 }
            }
        }
        else
        {
            gameEventSettingsCollection = loadedCollection;
        }
 
        if (gameEventSettingsCollection == null)
        {
            Debug.LogWarning("gameEventSettingsCollection 为 null，将初始化为默认值以避免错误。");
            InitializeDefaultSettings(false);
        }

        bool listModified = false;
        var newSettingsList = new List<EventActionSetting>();
        // Use a dictionary for quick lookup of existing settings to preserve their foldout state and other properties
        Dictionary<string, EventActionSetting> existingSettingsMap = gameEventSettingsCollection.allEventSettings.ToDictionary(s => s.eventIdentifier, s => s);

        foreach (string eventId in predefinedEventIdentifiers)
        {
            if (existingSettingsMap.TryGetValue(eventId, out var existingSetting))
            {
                newSettingsList.Add(existingSetting);
                if (existingSetting.emotionChances == null || existingSetting.emotionChances.Count == 0)
                {
                    existingSetting.emotionChances = new List<EmotionChance> { new EmotionChance { targetEmotion = EmotionType.普通, weight = 1f, selectedDialogueIndex = -1 } };
                    listModified = true;
                }
                 // Ensure all emotion chances have default valid values if loaded from an older version
                foreach(var ec in existingSetting.emotionChances)
                {
                    if (ec.weight == 0 && !Mathf.Approximately(ec.weight, 0f)) // Check for uninitialized weight if necessary
                    {
                        // Potentially set a default weight if it's invalid, e.g. ec.weight = 1f;
                        // This depends on how old versions were structured. For now, assume new fields get default values.
                    }
                }
            }
            else
            {
                newSettingsList.Add(new EventActionSetting
                {
                    eventIdentifier = eventId,
                    moodEffect = 0f,
                    sanityEffect = 0f,
                    dialogueTriggerProbability = 1f,
                    emotionChances = new List<EmotionChance> { new EmotionChance { targetEmotion = EmotionType.普通, weight = 1f, selectedDialogueIndex = -1 } },
                    isFoldedOut = true 
                });
                listModified = true;
            }
        }
        gameEventSettingsCollection.allEventSettings = newSettingsList; 

        int originalCount = gameEventSettingsCollection.allEventSettings.Count;
        gameEventSettingsCollection.allEventSettings.RemoveAll(s => !predefinedEventIdentifiers.Contains(s.eventIdentifier));
        if (gameEventSettingsCollection.allEventSettings.Count != originalCount)
        {
            listModified = true;
        }

        if(listModified) Debug.Log("事件配置已更新，以匹配预定义事件列表，并确保默认值。");
        Repaint(); 
    }

    private void LoadEmotionDialogues()
    {
        loadedEmotionDialogues.Clear();
        emotionDialogueFilePath = Path.Combine(Application.dataPath, "Resources", "lines", "emotionDialogues.json");

        if (File.Exists(emotionDialogueFilePath))
        {
            try
            {
                string json = File.ReadAllText(emotionDialogueFilePath);
                DialogueDataFile fileData = JsonUtility.FromJson<DialogueDataFile>(json);

                if (fileData != null && fileData.allDialogues != null) // Changed from fileData.dialogues
                {
                    foreach (var entry in fileData.allDialogues) // Changed from fileData.dialogues
                    {
                        if (System.Enum.TryParse<EmotionType>(entry.emotion, out EmotionType parsedEmotion))
                        {
                            if (!loadedEmotionDialogues.ContainsKey(parsedEmotion))
                            {
                                loadedEmotionDialogues[parsedEmotion] = new List<string>();
                            }
                            if (entry.texts != null)
                            {
                                loadedEmotionDialogues[parsedEmotion].AddRange(entry.texts.Where(t => !string.IsNullOrEmpty(t)));
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"无法解析台词文件 {emotionDialogueFilePath} 中的情感值 '{entry.emotion}'。该情感未在 EmotionType 枚举中定义。");
                        }
                    }
                    // Debug.Log($"台词数据从 {emotionDialogueFilePath} 加载成功。 {loadedEmotionDialogues.Count} emotions loaded.");
                }
                else
                {
                    Debug.LogWarning($"无法解析台词文件 {emotionDialogueFilePath}。文件内容可能为空或格式不符合预期的 {{ \"allDialogues\": [...] }} 结构。");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载台词文件 {emotionDialogueFilePath} 失败: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"台词文件 {emotionDialogueFilePath} 未找到。台词选择功能将受限。");
        }
        Repaint(); 
    }

    private string TruncateText(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return ""; // Return empty if null or empty
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }
}
