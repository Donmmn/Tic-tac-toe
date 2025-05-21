using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[System.Serializable]
public class TutorialListWrapper
{
    public List<Tutorial> Tutorials;

    public TutorialListWrapper(List<Tutorial> tutorials)
    {
        Tutorials = tutorials;
    }
}

public class TutorialEditorWindow : EditorWindow
{
    private List<Tutorial> tutorials = new List<Tutorial>();
    private Vector2 scrollPosition;
    private string tutorialsFilePath;

    private Dictionary<Tutorial, List<string>> tempTutorialTexts = new Dictionary<Tutorial, List<string>>();
    private Dictionary<Tutorial, List<string>> tempImageNames = new Dictionary<Tutorial, List<string>>();
    private Dictionary<Tutorial, List<Texture2D>> tempImageTextures = new Dictionary<Tutorial, List<Texture2D>>();

    private const float ImagePreviewWidth = 150f; 
    private const float ImagePreviewHeight = 100f; 
    private const float FixedTextAreaWidth = 1000f; // 新增：文本区域固定宽度

    [MenuItem("工具/教程编辑器")] 
    public static void ShowWindow()
    {
        GetWindow<TutorialEditorWindow>("教程编辑器"); 
    }

    private void OnEnable()
    {
        tutorialsFilePath = Path.Combine(Application.dataPath, "Resources", "lines", "tutorials.json");
        LoadTutorials();
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }
    
    private void OnUndoRedo()
    {
        SynchronizeTempListsFromTutorials();
        Repaint();
    }

    private void SynchronizeTempListsFromTutorials()
    {
        tempTutorialTexts.Clear();
        tempImageNames.Clear();
        tempImageTextures.Clear();

        foreach (var tutorial in tutorials)
        {
            var texts = new List<string>(tutorial.TutorialText ?? new string[0]);
            var imgNames = new List<string>(tutorial.ImageNames ?? new string[0]);
            var textures = new List<Texture2D>();

            while (imgNames.Count < texts.Count)
            {
                imgNames.Add("");
            }
            if (imgNames.Count > texts.Count) {
                imgNames.RemoveRange(texts.Count, imgNames.Count - texts.Count);
            }

            tempTutorialTexts[tutorial] = texts;
            tempImageNames[tutorial] = imgNames;

            for(int i = 0; i < imgNames.Count; i++)
            {
                textures.Add(LoadTextureFromResources(imgNames[i]));
            }
            tempImageTextures[tutorial] = textures;
        }
    }

    private void ApplyTempListsToTutorial(Tutorial tutorial)
    {
        if (tempTutorialTexts.ContainsKey(tutorial))
        {
            tutorial.TutorialText = tempTutorialTexts[tutorial].ToArray();
        }
        if (tempImageNames.ContainsKey(tutorial) && tempTutorialTexts.ContainsKey(tutorial))
        {
            List<string> texts = tempTutorialTexts[tutorial];
            List<string> imgNames = tempImageNames[tutorial];
            
            while (imgNames.Count < texts.Count)
            {
                imgNames.Add("");
            }
            if (imgNames.Count > texts.Count)
            {
                imgNames.RemoveRange(texts.Count, imgNames.Count - texts.Count);
            }
            tutorial.ImageNames = imgNames.ToArray();
        }
        else if (tempTutorialTexts.ContainsKey(tutorial))
        {
             List<string> texts = tempTutorialTexts[tutorial];
             string[] emptyImageNames = new string[texts.Count];
             for(int i=0; i < texts.Count; i++) emptyImageNames[i] = "";
             tutorial.ImageNames = emptyImageNames;
        }
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("教程编辑器", EditorStyles.boldLabel); 
        EditorGUILayout.Space();

        if (GUILayout.Button("保存教程")) 
        {
            SaveTutorials();
        }

        if (GUILayout.Button("加载教程")) 
        {
            if (EditorUtility.DisplayDialog("确认加载", 
                "确定要加载教程吗？所有未保存的更改将会丢失。", "加载", "取消")) 
            {
                LoadTutorials();
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("添加新教程")) 
        {
            Undo.RecordObject(this, "Add Tutorial");
            Tutorial newTutorial = new Tutorial
            {
                Id = tutorials.Count > 0 ? tutorials.Max(t => t.Id) + 1 : 1,
                TutorialText = new string[0],
                ImageNames = new string[0]
            };
            tutorials.Add(newTutorial);
            SynchronizeTempListsForTutorial(newTutorial); 
            EditorUtility.SetDirty(this);
        }

        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < tutorials.Count; i++)
        {
            Tutorial tutorial = tutorials[i];
            if (tutorial == null) continue;

            if (!tempTutorialTexts.ContainsKey(tutorial)) SynchronizeTempListsForTutorial(tutorial);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"教程条目 {i + 1} (编号: {tutorial.Id})", EditorStyles.boldLabel); 
            
            EditorGUI.BeginChangeCheck();
            int newId = EditorGUILayout.IntField("编号", tutorial.Id); 
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Change Tutorial ID");
                tutorial.Id = newId;
                EditorUtility.SetDirty(this);
            }
            
            EditorGUILayout.LabelField("教程步骤", EditorStyles.miniBoldLabel); 
            List<string> currentTexts = tempTutorialTexts[tutorial];
            List<string> currentImageNames = tempImageNames[tutorial];
            List<Texture2D> currentTextures = tempImageTextures[tutorial];

            for (int j = 0; j < currentTexts.Count; j++)
            {
                float singleLineHeight = EditorGUIUtility.singleLineHeight;
                float standardVerticalSpacing = EditorGUIUtility.standardVerticalSpacing;
                float rightBlockTotalHeight = ImagePreviewHeight + singleLineHeight + standardVerticalSpacing;

                EditorGUILayout.BeginHorizontal();

                // 左侧: 教程文本区域 (固定宽度)
                EditorGUILayout.BeginVertical(GUILayout.Width(FixedTextAreaWidth)); 
                EditorGUI.BeginChangeCheck();
                string textEntry = EditorGUILayout.TextArea(currentTexts[j], GUILayout.Height(rightBlockTotalHeight), GUILayout.Width(FixedTextAreaWidth));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Edit Tutorial Text");
                    currentTexts[j] = textEntry;
                    EditorUtility.SetDirty(this);
                }
                EditorGUILayout.EndVertical(); 

                GUILayout.FlexibleSpace(); 

                // 右侧: 图片信息区域 (固定宽度)
                EditorGUILayout.BeginVertical(GUILayout.Width(ImagePreviewWidth));
                EditorGUI.BeginChangeCheck();
                string imgName = EditorGUILayout.TextField(currentImageNames[j]); 
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Edit Image Name");
                    currentImageNames[j] = imgName;
                    currentTextures[j] = LoadTextureFromResources(imgName);
                    EditorUtility.SetDirty(this);
                }
                
                EditorGUI.BeginChangeCheck();
                Texture2D pickedTexture = (Texture2D)EditorGUILayout.ObjectField(currentTextures[j], typeof(Texture2D), false, GUILayout.Height(ImagePreviewHeight));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Pick Image");
                    string resourcePath = GetTextureResourcePath(pickedTexture);
                    if (!string.IsNullOrEmpty(resourcePath) || pickedTexture == null)
                    {
                        currentImageNames[j] = resourcePath;
                        currentTextures[j] = pickedTexture; 
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("图片错误", "选择的图片必须位于 'Resources' 文件夹中，或清空选择。", "好的"); 
                    }
                    EditorUtility.SetDirty(this);
                }
                EditorGUILayout.EndVertical(); 

                // 删除步骤按钮 (与右侧区域高度一致)
                if (GUILayout.Button("-", GUILayout.Width(25), GUILayout.Height(rightBlockTotalHeight)))
                {
                    Undo.RecordObject(this, "Remove Tutorial Step");
                    currentTexts.RemoveAt(j);
                    currentImageNames.RemoveAt(j);
                    currentTextures.RemoveAt(j);
                    EditorUtility.SetDirty(this);
                    GUI.FocusControl(null); 
                    break; 
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5); 
            }

            if (GUILayout.Button("添加步骤")) 
            {
                Undo.RecordObject(this, "Add Tutorial Step");
                currentTexts.Add("");
                currentImageNames.Add("");
                currentTextures.Add(null);
                EditorUtility.SetDirty(this);
            }
            
            EditorGUILayout.Space(10);
            if (GUILayout.Button("删除此教程", GUILayout.Height(25))) 
            {
                if (EditorUtility.DisplayDialog("确认删除", 
                    $"确定要删除编号为 {tutorial.Id} 的教程吗？", "删除", "取消")) 
                {
                    Undo.RecordObject(this, "Remove Tutorial");
                    tutorials.RemoveAt(i);
                    tempTutorialTexts.Remove(tutorial); 
                    tempImageNames.Remove(tutorial);
                    tempImageTextures.Remove(tutorial);
                    EditorUtility.SetDirty(this);
                    GUI.FocusControl(null);
                    break; 
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(15); 
        }

        EditorGUILayout.EndScrollView();
    }

    private void SynchronizeTempListsForTutorial(Tutorial tutorial)
    {
        var texts = new List<string>(tutorial.TutorialText ?? new string[0]);
        var imgNames = new List<string>(tutorial.ImageNames ?? new string[0]);
        var textures = new List<Texture2D>();

        while (imgNames.Count < texts.Count)
        {
            imgNames.Add("");
        }
        if (imgNames.Count > texts.Count) 
        {
            imgNames.RemoveRange(texts.Count, imgNames.Count - texts.Count);
        }

        tempTutorialTexts[tutorial] = texts;
        tempImageNames[tutorial] = imgNames;

        for(int i=0; i < imgNames.Count; ++i)
        {
            textures.Add(LoadTextureFromResources(imgNames[i]));
        }
        while (textures.Count < texts.Count)
        {
            textures.Add(null);
        }
        if (textures.Count > texts.Count)
        {
            textures.RemoveRange(texts.Count, textures.Count - texts.Count);
        }
        tempImageTextures[tutorial] = textures;
    }

    private void SaveTutorials()
    {
        foreach (var tutorial in tutorials)
        {
            ApplyTempListsToTutorial(tutorial);
        }

        string directoryPath = Path.GetDirectoryName(tutorialsFilePath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        TutorialListWrapper wrapper = new TutorialListWrapper(tutorials);
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(tutorialsFilePath, json);
        AssetDatabase.Refresh(); 
        Debug.Log("教程已保存到: " + tutorialsFilePath); 
        EditorUtility.SetDirty(this);
    }

    private void LoadTutorials()
    {
        if (File.Exists(tutorialsFilePath))
        {
            try
            {
                string json = File.ReadAllText(tutorialsFilePath);
                TutorialListWrapper wrapper = JsonUtility.FromJson<TutorialListWrapper>(json);
                if (wrapper != null && wrapper.Tutorials != null)
                {
                    Undo.RecordObject(this, "Load Tutorials");
                    tutorials = wrapper.Tutorials;
                    SynchronizeTempListsFromTutorials(); 
                }
                else
                {
                    tutorials = new List<Tutorial>();
                    SynchronizeTempListsFromTutorials();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("加载教程失败: " + ex.Message); 
                tutorials = new List<Tutorial>();
                SynchronizeTempListsFromTutorials();
            }
        }
        else
        {
            tutorials = new List<Tutorial>();
            SynchronizeTempListsFromTutorials();
            Debug.Log("未找到教程文件。将以空列表开始。"); 
        }
        EditorUtility.SetDirty(this);
        Repaint();
    }

    private string GetTextureResourcePath(Texture2D texture)
    {
        if (texture == null) return "";
        string assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(assetPath)) return "";
        int resourcesIndex = assetPath.IndexOf("/Resources/");
        if (resourcesIndex == -1)
        {
            return null; 
        }
        string relativePath = assetPath.Substring(resourcesIndex + "/Resources/".Length);
        int extensionIndex = relativePath.LastIndexOf('.');
        if (extensionIndex != -1)
        {
            relativePath = relativePath.Substring(0, extensionIndex);
        }
        return relativePath;
    }
    
    private Texture2D LoadTextureFromResources(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;
        return Resources.Load<Texture2D>(resourcePath);
    }
} 