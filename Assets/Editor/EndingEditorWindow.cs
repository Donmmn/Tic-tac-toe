using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq; // 需要 Linq 来获取 Max ID

public class EndingEditorWindow : EditorWindow
{
    private List<Ending> endingsList = new List<Ending>();
    private Vector2 scrollPosition;
    private const string EndingsJsonFileName = "endings.json";
    private const string ResourcesLinesPath = "Assets/Resources/lines"; // 包含 Assets/Resources
    private const string EndingsJsonPathInResources = "lines/" + EndingsJsonFileName; // 用于 Resources.Load

    // 布局常量调整
    private const float StepTextAreaWidth = 280f; // 文本区域宽度，略微减小
    private const float RightPanelImageControlsWidth = 170f; // 右侧图片控件区域宽度，调整以适应ObjectField和预览
    private const float ImagePreviewHeight = 100f; // 图片预览区域的高度
    private const float StepControlsHeight = 125f; // 步骤控件总高度 (TextArea 和右侧图片控件区域的统一高度)
    // private const float ImageNameFieldWidth = 150f; // 旧的，移除
    // private const float ImagePreviewWidth = 100f; // 旧的，移除

    [MenuItem("工具/结局编辑器")]
    public static void ShowWindow()
    {
        GetWindow<EndingEditorWindow>("结局编辑器");
    }

    private void OnEnable()
    {
        LoadEndings();
    }

    private void OnGUI()
    {
        GUILayout.Label("结局编辑器", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("添加新结局", GUILayout.Width(150)))
        {
            AddNewEnding();
        }
        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < endingsList.Count; i++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Ending currentEnding = endingsList[i];

            EditorGUILayout.BeginHorizontal();
            currentEnding.Id = EditorGUILayout.IntField("结局 ID", currentEnding.Id, GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("删除此结局", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("确认删除", $"确定要删除结局 ID: {currentEnding.Id} ({currentEnding.title})吗？", "删除", "取消"))
                {
                    endingsList.RemoveAt(i);
                    GUI.FocusControl(null); 
                    Repaint();
                    return; 
                }
            }
            EditorGUILayout.EndHorizontal();

            currentEnding.title = EditorGUILayout.TextField("结局标题", currentEnding.title);
            currentEnding.MinScore = EditorGUILayout.IntField("最低解锁分数", currentEnding.MinScore);
            currentEnding.MaxScore = EditorGUILayout.IntField("最高解锁分数", currentEnding.MaxScore);
            if (currentEnding.MinScore > currentEnding.MaxScore)
            {
                currentEnding.MaxScore = currentEnding.MinScore;
            }
            currentEnding.PlayerWin = EditorGUILayout.Toggle("是否玩家获胜结局", currentEnding.PlayerWin); 

            // 新增：结局专属背景图片和BGM
            EditorGUILayout.Space();
            GUILayout.Label("结局专属资源:", EditorStyles.boldLabel);

            // 背景图片选择
            EditorGUILayout.BeginHorizontal();
            currentEnding.BackgroundImagePath = EditorGUILayout.TextField("背景图路径:", currentEnding.BackgroundImagePath, GUILayout.Width(StepTextAreaWidth + 50)); // 稍微加宽
            Texture2D backgroundImage = null;
            if (!string.IsNullOrEmpty(currentEnding.BackgroundImagePath))
            {
                backgroundImage = Resources.Load<Texture2D>(currentEnding.BackgroundImagePath);
            }
            Texture2D selectedBgTexture = (Texture2D)EditorGUILayout.ObjectField(backgroundImage, typeof(Texture2D), false, GUILayout.Height(40), GUILayout.Width(RightPanelImageControlsWidth - 60)); // 调整宽度
            if (selectedBgTexture != backgroundImage)
            {
                if (selectedBgTexture != null)
                {
                    string bgPath = GetTextureResourcePath(selectedBgTexture);
                    if (!string.IsNullOrEmpty(bgPath)) currentEnding.BackgroundImagePath = bgPath;
                    else Debug.LogWarning("选择的背景图片不在 Resources 文件夹内。");
                }
                else currentEnding.BackgroundImagePath = string.Empty;
                GUI.FocusControl(null); Repaint();
            }
            EditorGUILayout.EndHorizontal();

            // BGM选择
            EditorGUILayout.BeginHorizontal();
            currentEnding.BGMusicPath = EditorGUILayout.TextField("BGM路径:", currentEnding.BGMusicPath, GUILayout.Width(StepTextAreaWidth + 50));
            AudioClip bgMusic = null;
            if (!string.IsNullOrEmpty(currentEnding.BGMusicPath))
            {
                bgMusic = Resources.Load<AudioClip>(currentEnding.BGMusicPath);
            }
            AudioClip selectedBgMusic = (AudioClip)EditorGUILayout.ObjectField(bgMusic, typeof(AudioClip), false, GUILayout.Height(40), GUILayout.Width(RightPanelImageControlsWidth - 60));
            if (selectedBgMusic != bgMusic)
            {
                if (selectedBgMusic != null)
                {
                    string musicPath = GetAudioClipResourcePath(selectedBgMusic); // 需要一个新的辅助方法
                    if (!string.IsNullOrEmpty(musicPath)) currentEnding.BGMusicPath = musicPath;
                    else Debug.LogWarning("选择的BGM不在 Resources 文件夹内。");
                }
                else currentEnding.BGMusicPath = string.Empty;
                GUI.FocusControl(null); Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            GUILayout.Label("结局内容 (文本和图片):", EditorStyles.boldLabel);

            if (currentEnding.EndingText == null) currentEnding.EndingText = new string[0];
            if (currentEnding.ImagesName == null) currentEnding.ImagesName = new string[0];

            if (currentEnding.ImagesName.Length < currentEnding.EndingText.Length)
            {
                List<string> tempImages = currentEnding.ImagesName.ToList();
                while (tempImages.Count < currentEnding.EndingText.Length)
                {
                    tempImages.Add(string.Empty);
                }
                currentEnding.ImagesName = tempImages.ToArray();
            }
            
            for (int j = 0; j < currentEnding.EndingText.Length; j++)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical(GUILayout.Width(StepTextAreaWidth));
                GUILayout.Label($"步骤 {j + 1} 文本:");
                currentEnding.EndingText[j] = EditorGUILayout.TextArea(currentEnding.EndingText[j], GUILayout.Height(StepControlsHeight), GUILayout.ExpandWidth(true));
                EditorGUILayout.EndVertical();

                // 右侧：图片选择和预览区域
                EditorGUILayout.BeginVertical(GUILayout.Width(RightPanelImageControlsWidth));
                // GUILayout.Label("图片名称 (Resources):"); // 移除此标签

                // --- 新增：图片路径文本框 ---
                EditorGUI.BeginChangeCheck();
                string imageNameInput = EditorGUILayout.TextField("图片路径:", currentEnding.ImagesName[j]);
                if (EditorGUI.EndChangeCheck())
                {
                    currentEnding.ImagesName[j] = imageNameInput;
                    GUI.FocusControl(null); 
                    Repaint(); // 确保ObjectField和预览更新
                }
                // --- 结束：图片路径文本框 ---

                Texture2D currentTexture = null;
                if (!string.IsNullOrEmpty(currentEnding.ImagesName[j]))
                {
                    currentTexture = Resources.Load<Texture2D>(currentEnding.ImagesName[j]);
                }

                // --- 修改：将标签与ObjectField分开 --- 
                EditorGUILayout.LabelField("图像选择:"); // 单独的标签
                EditorGUI.BeginChangeCheck();
                Texture2D selectedTexture = (Texture2D)EditorGUILayout.ObjectField(
                    // "图像选择:", // 不再在此处传递标签
                    currentTexture, 
                    typeof(Texture2D), 
                    false, 
                    GUILayout.Height(ImagePreviewHeight), 
                    GUILayout.Width(RightPanelImageControlsWidth - 10) 
                );

                if (EditorGUI.EndChangeCheck())
                {
                    if (selectedTexture != null)
                    {
                        string resourcePath = GetTextureResourcePath(selectedTexture);
                        if (!string.IsNullOrEmpty(resourcePath))
                        {
                            currentEnding.ImagesName[j] = resourcePath;
                        }
                        else
                        {
                            Debug.LogWarning($"选择的图片 '{AssetDatabase.GetAssetPath(selectedTexture)}' 不在 Resources 文件夹内或其子文件夹内，无法在运行时通过 Resources.Load 加载。请将其移动到 Resources 文件夹下。");
                            currentEnding.ImagesName[j] = ""; 
                        }
                    }
                    else
                    {
                        currentEnding.ImagesName[j] = ""; 
                    }
                    // 路径已更新，currentTexture 将在下一次OnGUI或Repaint时从新路径加载
                    GUI.FocusControl(null); 
                    Repaint(); 
                }
                
                EditorGUILayout.EndVertical(); // 结束右侧图片控件区域
                
                GUILayout.FlexibleSpace(); 

                if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(StepControlsHeight)))
                {
                    RemoveEndingStep(currentEnding, j);
                    GUI.FocusControl(null);
                    Repaint();
                    EditorGUILayout.EndHorizontal(); 
                    EditorGUILayout.EndVertical(); 
                    EditorGUILayout.EndScrollView(); 
                    return; 
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("添加结局步骤", GUILayout.Width(150)))
            {
                AddEndingStep(currentEnding);
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("保存所有结局"))
        {
            SaveEndings();
        }
        if (GUILayout.Button("重新加载结局"))
        {
            if (EditorUtility.DisplayDialog("确认重新加载", "将从文件重新加载结局数据，未保存的更改将丢失。", "重新加载", "取消"))
            {
                LoadEndings();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void AddNewEnding()
    {
        Ending newEnding = new Ending();
        newEnding.Id = endingsList.Count > 0 ? endingsList.Max(e => e.Id) + 1 : 1; 
        newEnding.title = "新结局";
        newEnding.MinScore = 0;
        newEnding.MaxScore = 0;
        newEnding.PlayerWin = false; 
        newEnding.EndingText = new string[] { "结局步骤 1 的默认文本。" }; 
        newEnding.ImagesName = new string[] { "" }; 
        newEnding.BackgroundImagePath = ""; // 初始化新字段
        newEnding.BGMusicPath = "";         // 初始化新字段
        endingsList.Add(newEnding);
        GUI.FocusControl(null); 
    }

    private void AddEndingStep(Ending ending)
    {
        List<string> texts = ending.EndingText.ToList();
        List<string> images = ending.ImagesName.ToList();
        texts.Add("新步骤文本。");
        images.Add(""); 
        ending.EndingText = texts.ToArray();
        ending.ImagesName = images.ToArray();
    }

    private void RemoveEndingStep(Ending ending, int stepIndex)
    {
        List<string> texts = ending.EndingText.ToList();
        List<string> images = ending.ImagesName.ToList();
        if (stepIndex >= 0 && stepIndex < texts.Count)
        {
            texts.RemoveAt(stepIndex);
            if (stepIndex < images.Count)
            {
                images.RemoveAt(stepIndex);
            }
        }
        ending.EndingText = texts.ToArray();
        ending.ImagesName = images.ToArray();
    }

    private void SaveEndings()
    {
        if (!Directory.Exists(ResourcesLinesPath))
        {
            Directory.CreateDirectory(ResourcesLinesPath);
        }
        
        string filePath = Path.Combine(ResourcesLinesPath, EndingsJsonFileName);
        EndingListWrapper wrapper = new EndingListWrapper { Endings = endingsList };
        string json = JsonUtility.ToJson(wrapper, true);

        try
        {
            File.WriteAllText(filePath, json);
            EditorUtility.DisplayDialog("保存成功", $"结局数据已保存到: {filePath}", "确定");
            AssetDatabase.Refresh(); 
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("保存失败", $"保存结局数据时发生错误: {ex.Message}", "确定");
            Debug.LogError($"保存结局数据失败: {ex.Message}");
        }
    }

    private void LoadEndings()
    {
        TextAsset jsonData = Resources.Load<TextAsset>(EndingsJsonPathInResources.Replace(".json", "")); 
        if (jsonData != null)
        {
            try
            {
                EndingListWrapper wrapper = JsonUtility.FromJson<EndingListWrapper>(jsonData.text);
                if (wrapper != null && wrapper.Endings != null)
                {
                    endingsList = wrapper.Endings;
                    Debug.Log($"成功从 {EndingsJsonPathInResources} 加载 {endingsList.Count} 个结局。");
                }
                else
                {
                    endingsList = new List<Ending>();
                    Debug.LogWarning($"从 {EndingsJsonPathInResources} 加载结局：JSON内容为空或格式不正确。初始化为空列表。");
                }
            }
            catch (System.Exception ex)
            {
                endingsList = new List<Ending>();
                Debug.LogError($"加载结局JSON时发生错误 ({EndingsJsonPathInResources}): {ex.Message}。初始化为空列表。");
            }
        }
        else
        {
            endingsList = new List<Ending>();
            Debug.Log($"未找到结局JSON文件: {EndingsJsonPathInResources}。将使用空列表。");
        }
        Repaint(); 
    }
    
    private string GetTextureResourcePath(Texture2D texture)
    {
        if (texture == null) return string.Empty;

        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path)) return string.Empty;

        if (path.StartsWith("Assets/Resources/"))
        {
            string resourcesRelativePath = path.Substring("Assets/Resources/".Length);
            int dotIndex = resourcesRelativePath.LastIndexOf('.');
            if (dotIndex > 0)
            {
                return resourcesRelativePath.Substring(0, dotIndex);
            }
            return resourcesRelativePath; 
        }
        else
        {
            return string.Empty; 
        }
    }

    // 新增：获取AudioClip在Resources中的路径
    private string GetAudioClipResourcePath(AudioClip audioClip)
    {
        if (audioClip == null) return string.Empty;

        string path = AssetDatabase.GetAssetPath(audioClip);
        if (string.IsNullOrEmpty(path)) return string.Empty;

        if (path.StartsWith("Assets/Resources/"))
        {
            string resourcesRelativePath = path.Substring("Assets/Resources/".Length);
            // 移除文件扩展名
            int dotIndex = resourcesRelativePath.LastIndexOf('.');
            if (dotIndex > 0)
            {
                return resourcesRelativePath.Substring(0, dotIndex);
            }
            return resourcesRelativePath; // 如果没有扩展名（不太可能，但作为后备）
        }
        else
        {
            return string.Empty; // 不在Resources文件夹中
        }
    }
} 