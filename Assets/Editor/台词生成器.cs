using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq; // 用于 Enum.GetNames().ToList()

// 如果 EmotionType 定义在 CharacterReaction.cs 中且没有特定命名空间，则这里不需要额外 using
// 如果 EmotionType 在命名空间内，则需要 using NamespaceName;

public class DialogueEditorWindow : EditorWindow
{
    // 从 EmotionType enum 获取表情名称列表
    private List<string> emotionDisplayNames;
    private List<List<string>> dialoguesForEachEmotion; // Changed: List of lists of strings

    private CharacterReaction targetReactionScript; // 目标 CharacterReaction 脚本实例
    private Vector2 scrollPosition; // 用于当内容过多时滚动显示

    // 用于JSON序列化的数据结构
    [System.Serializable]
    private class DialogueEntry
    {
        public string emotion; // 将存储 EmotionType enum 成员的字符串名称
        public List<string> texts = new List<string>(); // Changed: stores multiple texts
    }

    [System.Serializable]
    private class DialogueCollection
    {
        public List<DialogueEntry> allDialogues = new List<DialogueEntry>();
    }

    [MenuItem("工具/台词编辑器")]
    public static void ShowWindow()
    {
        GetWindow<DialogueEditorWindow>("台词编辑器");
    }

    private void OnEnable()
    {
        // 从 EmotionType enum 初始化表情名称列表
        emotionDisplayNames = System.Enum.GetNames(typeof(EmotionType)).ToList();

        // 初始化台词列表以匹配表情数量
        dialoguesForEachEmotion = new List<List<string>>();
        for (int i = 0; i < emotionDisplayNames.Count; i++)
        {
            dialoguesForEachEmotion.Add(new List<string> { "" }); // Initialize each emotion with one empty dialogue line
        }
        LoadDialogues(); // 尝试加载已保存的台词
    }

    void OnGUI()
    {
        GUILayout.Label("台词编辑器", EditorStyles.boldLabel);
        GUILayout.Space(10);

        targetReactionScript = (CharacterReaction)EditorGUILayout.ObjectField("表情控制脚本 (可选)", targetReactionScript, typeof(CharacterReaction), true);
        if (targetReactionScript == null)
        {
            if (GUILayout.Button("自动查找表情控制脚本"))
            {
                 targetReactionScript = FindObjectOfType<CharacterReaction>();
                 if (targetReactionScript == null) {
                    EditorUtility.DisplayDialog("未找到", "场景中未找到 CharacterReaction 脚本的实例。", "确定");
                 }
            }
             EditorGUILayout.HelpBox("请指定一个场景中的 CharacterReaction 脚本实例，或尝试自动查找。这对未来扩展可能有用。", MessageType.Info);
        } 
        else 
        {   
            EditorGUILayout.HelpBox("已关联到: " + targetReactionScript.gameObject.name, MessageType.Info);
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("为每个表情输入对应的台词（可有多条）：", MessageType.Info);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < emotionDisplayNames.Count; i++)
        {
            GUILayout.Label(emotionDisplayNames[i] + " 表情台词:", EditorStyles.boldLabel);
            List<string> currentEmotionDialogues = dialoguesForEachEmotion[i];
            for (int j = 0; j < currentEmotionDialogues.Count; j++)
            {
                currentEmotionDialogues[j] = EditorGUILayout.TextArea(currentEmotionDialogues[j], GUILayout.Height(40));
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加台词", GUILayout.Width(100)))
            {
                currentEmotionDialogues.Add(""); // Add a new empty line
                Repaint(); // Repaint to show the new text area
            }
            if (currentEmotionDialogues.Count > 1 && GUILayout.Button("移除最后一条", GUILayout.Width(100)))
            {
                currentEmotionDialogues.RemoveAt(currentEmotionDialogues.Count - 1);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
        }
        EditorGUILayout.EndScrollView();
        GUILayout.Space(10);

        if (GUILayout.Button("保存台词到 JSON"))
        {
            SaveDialogues();
        }
        GUILayout.Space(5);
        if (GUILayout.Button("加载台词从 JSON"))
        {
            LoadDialogues();
        }
    }

    private string GetDialogueFilePath()
    {
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        string linesFolderPath = Path.Combine(resourcesPath, "lines");

        // 确保 Resources 文件夹存在
        if (!Directory.Exists(resourcesPath))
        {
            Directory.CreateDirectory(resourcesPath);
            // AssetDatabase.Refresh() 可能在这里不是必需的，因为我们还会检查子文件夹
        }

        // 确保 Resources/lines 文件夹存在
        if (!Directory.Exists(linesFolderPath))
        {
            Directory.CreateDirectory(linesFolderPath);
            AssetDatabase.Refresh(); // 刷新以便Unity编辑器识别新创建的文件夹
        }
        return Path.Combine(linesFolderPath, "emotionDialogues.json");
    }

    private void SaveDialogues()
    {
        DialogueCollection collection = new DialogueCollection();
        for (int i = 0; i < emotionDisplayNames.Count; i++)
        {
            collection.allDialogues.Add(new DialogueEntry { emotion = emotionDisplayNames[i], texts = new List<string>(dialoguesForEachEmotion[i]) });
        }

        string json = JsonUtility.ToJson(collection, true);
        string filePath = GetDialogueFilePath();
        File.WriteAllText(filePath, json);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("保存成功", "台词已保存到: " + filePath.Replace(Application.dataPath, "Assets"), "确定");
    }

    private void LoadDialogues()
    {
        string filePath = GetDialogueFilePath();
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            try
            {
                DialogueCollection collection = JsonUtility.FromJson<DialogueCollection>(json);

                if (collection != null && collection.allDialogues != null)
                {
                    // Reinitialize dialoguesForEachEmotion to match the structure of emotionDisplayNames
                    dialoguesForEachEmotion = new List<List<string>>();
                    for(int i=0; i < emotionDisplayNames.Count; i++)
                    {
                        dialoguesForEachEmotion.Add(new List<string>()); // Add empty list first
                    }

                    foreach (var entry in collection.allDialogues)
                    {
                        int index = emotionDisplayNames.IndexOf(entry.emotion);
                        if (index != -1)
                        {
                            // Assign the loaded texts, or ensure at least one empty string if texts list is null or empty from JSON
                            dialoguesForEachEmotion[index] = (entry.texts != null && entry.texts.Count > 0) ? new List<string>(entry.texts) : new List<string> { "" };
                        }
                    }
                    // Ensure any emotions in emotionDisplayNames not found in JSON still have an initialized list
                    for(int i=0; i < dialoguesForEachEmotion.Count; i++)
                    {
                        if(dialoguesForEachEmotion[i] == null || dialoguesForEachEmotion[i].Count == 0)
                        {
                             dialoguesForEachEmotion[i] = new List<string> { "" };
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("无法解析台词文件或文件内容为空: " + filePath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("加载台词文件失败: " + filePath + "\n错误: " + e.Message);
            }
        }
        
        // 再次确保 dialoguesForEachEmotion 列表总是被初始化且大小正确，即使加载失败或文件不存在
        if (dialoguesForEachEmotion.Count != emotionDisplayNames.Count)
        {
            dialoguesForEachEmotion = new List<List<string>>();
            for (int i = 0; i < emotionDisplayNames.Count; i++)
            {
                dialoguesForEachEmotion.Add(new List<string> { "" });
            }
        }
        Repaint();
    }
}
