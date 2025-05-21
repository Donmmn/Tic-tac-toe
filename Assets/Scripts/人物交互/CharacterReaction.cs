using System.Collections;
using System.Collections.Generic;
using System.Linq; // 用于 Enum.GetNames().ToList()
using UnityEngine;
using UnityEngine.UI; // 确保这行存在
using System.IO; 

public enum EmotionType
{
    普通,
    开心,
    生气,
    骄傲,
    调皮,
    哭泣,
    思考 // 新增思考表情
}

[System.Serializable]
public class DialogueEntry 
{
    public string emotion;
    public List<string> texts = new List<string>(); // 匹配编辑器的结构
}

[System.Serializable]
public class DialogueCollection
{
    public List<DialogueEntry> allDialogues = new List<DialogueEntry>();
}

// --- Structures for Game Event Settings (mirroring editor's for JSON deserialization) ---
[System.Serializable]
public class EmotionChanceRuntime
{
    public EmotionType targetEmotion;
    public float weight;
    public int selectedDialogueIndex;
}

[System.Serializable]
public class EventActionSettingRuntime
{
    public string eventIdentifier;
    public float moodEffect;
    public float sanityEffect;
    public float dialogueTriggerProbability;
    public List<EmotionChanceRuntime> emotionChances = new List<EmotionChanceRuntime>();
    // isFoldedOut is editor-only, not needed here
}

[System.Serializable]
public class GameEventSettingsCollectionRuntime
{
    public List<EventActionSettingRuntime> allEventSettings = new List<EventActionSettingRuntime>();
}
// --- End Structures for Game Event Settings ---

public class CharacterReaction : MonoBehaviour
{
    [Header("角色表情精灵")]
    [SerializeField] [Tooltip("普通表情的图片")] private Sprite normalSprite;
    [SerializeField] [Tooltip("开心表情的图片")] private Sprite happySprite;
    [SerializeField] [Tooltip("生气表情的图片")] private Sprite angrySprite;
    [SerializeField] [Tooltip("骄傲表情的图片")] private Sprite proudSprite;
    [SerializeField] [Tooltip("调皮表情的图片")] private Sprite naughtySprite;
    [SerializeField] [Tooltip("哭泣表情的图片")] private Sprite cryingSprite;
    [SerializeField] [Tooltip("思考表情的图片")] private Sprite thinkingSprite; // 新增思考Sprite

    [Header("UI组件引用")]
    public Image emotionImage; 
    public Dropdown Changeem;
    public Text dialogueDisplayText;
    public Image dialogBubble;
    public Scrollbar moodScrollbar; // 心情值滚动条 (改名并使用)
    public Scrollbar sanityScrollbar; // 理智值滚动条 (改名并使用)

    [Header("AI下棋引用")]
    public 井字棋AI AI; // AI脚本引用 (由用户添加)

    [Header("表情抖动设置")]
    [Tooltip("勾选在切换到这些表情时触发抖动效果")]
    public List<EmotionType> emotionsToShake = new List<EmotionType>();
    [Tooltip("抖动幅度（像素）")]
    public float shakeMagnitude = 5f;
    [Tooltip("抖动次数")]
    public int shakeCount = 3;
    [Tooltip("单次抖动的持续时间（秒）")]
    public float shakeDurationPerCycle = 0.05f;

    [Header("角色状态值")]
    [SerializeField] private float _currentMood;
    public float CurrentMood
    {
        get { return _currentMood; }
        set
        {
            _currentMood = Mathf.Clamp(value, 0, MAX_MOOD);
            if (moodScrollbar != null)
            {
                moodScrollbar.size = Mathf.Clamp01(_currentMood / MAX_MOOD);
            }
        }
    }

    [SerializeField] private float _currentSanity;
    public float CurrentSanity
    {
        get { return _currentSanity; }
        set
        {
            _currentSanity = Mathf.Clamp(value, 0, MAX_SANITY);
            if (sanityScrollbar != null)
            {
                sanityScrollbar.size = Mathf.Clamp01(_currentSanity / MAX_SANITY);
            }
        }
    }
    public const float MAX_MOOD = 100f;
    public const float MAX_SANITY = 100f;

    public boardState Board; // 棋局状态引用 (由用户添加)
  
    private Dictionary<EmotionType, List<string>> loadedDialogues = new Dictionary<EmotionType, List<string>>();
    private Dictionary<string, EventActionSettingRuntime> gameEventConfig = new Dictionary<string, EventActionSettingRuntime>(); // For loaded event settings
    private Coroutine activeRevertCoroutine; // 用于跟踪恢复表情的协程
    private Coroutine activeShakeCoroutine; // 用于跟踪抖动协程
    private HashSet<EmotionType> emotionsToShakeSet; // 用于快速查找是否需要抖动
    private Vector2 emotionImageInitialAnchoredPosition; // 记录表情图片的初始位置
    private System.Random rand = new System.Random(); // 随机使用表情

    void Awake() // 改为 Awake 以确保事件注册在 Board 的 Start 之前或同时发生
    {
        if (Board == null) Board = FindObjectOfType<boardState>();
        if (AI == null) AI = FindObjectOfType<井字棋AI>();

        if (Board != null)
        {
            // Register game events to a common handler
            Board.OnGameDraw += () => HandleConfiguredEvent("OnGameDraw");
            Board.OnAIWinsGame += () => HandleConfiguredEvent("OnAIWinsGame");
            Board.OnPlayerWinsGame += () => HandleConfiguredEvent("OnPlayerWinsGame");
            Board.OnAIAlmostWins += () => HandleConfiguredEvent("OnAIAlmostWins");
            Board.OnPlayerAlmostWins += () => HandleConfiguredEvent("OnPlayerAlmostWins");
            Board.OnBoardReset += () => HandleConfiguredEvent("OnBoardReset");
            Board.OnPlayerBlockedAIWin += () => HandleConfiguredEvent("OnPlayerBlockedAIWin");
            Board.OnMatchEnd += HandleMatchEnd; // 新增：订阅比赛结束事件
        }
        else
        {
            Debug.LogError("CharacterReaction: 无法找到 boardState 对象以注册事件!");
        }
    }

    void Start()
    {
        if (Board == null || AI == null) // 再次检查，因为 Awake 可能失败
        {
            Debug.LogError("CharacterReaction: 依赖的 boardState 或 井字棋AI 对象缺失!");
            enabled = false; // 禁用此脚本以避免后续错误
            return;
        }
        
        if (emotionImage != null)
        {
            emotionImageInitialAnchoredPosition = emotionImage.rectTransform.anchoredPosition;
        }
        else
        {
            Debug.LogError("CharacterReaction: emotionImage 未在 Inspector 中分配！抖动和位置恢复可能无法正常工作。");
        }

        CurrentMood = MAX_MOOD; // 设置初始值，将通过setter更新UI
        CurrentSanity = MAX_SANITY; // 设置初始值，将通过setter更新UI
        
        // 设置滚动条不可交互，并确保Handle可见
        //sif (moodScrollbar != null)
        //s{
        //s    moodScrollbar.interactable = false;
        //s    // 如果Scrollbar的Handle (targetGraphic) 在interactable=false时Alpha降低，则恢复它
        //s    if (moodScrollbar.targetGraphic != null)
        //s    {
        //s        Color handleColor = moodScrollbar.targetGraphic.color;
        //s        handleColor.a = 1f; // 强制Alpha为1，使其完全可见
        //s        moodScrollbar.targetGraphic.color = handleColor;
        //s    }
        //s}
        //sif (sanityScrollbar != null)
        //s{
        //s    sanityScrollbar.interactable = false;
        //s    if (sanityScrollbar.targetGraphic != null)
        //s    {
        //s        Color handleColor = sanityScrollbar.targetGraphic.color;
        //s        handleColor.a = 1f; // 强制Alpha为1
        //s        sanityScrollbar.targetGraphic.color = handleColor;
        //s    }
        //s}

        LoadDialoguesFromFile(); 
        LoadGameEventSettings(); // Load event configurations
        InitializeEmotionDropdown();
        InitializeShakeSettings();
    }

    void OnDestroy() // 当此对象销毁时，取消注册事件
    {
        if (Board != null)
        {
            // Unsubscribe from all events
            Board.OnGameDraw -= () => HandleConfiguredEvent("OnGameDraw"); 
            Board.OnAIWinsGame -= () => HandleConfiguredEvent("OnAIWinsGame"); 
            Board.OnPlayerWinsGame -= () => HandleConfiguredEvent("OnPlayerWinsGame"); 
            Board.OnAIAlmostWins -= () => HandleConfiguredEvent("OnAIAlmostWins");
            Board.OnPlayerAlmostWins -= () => HandleConfiguredEvent("OnPlayerAlmostWins");
            Board.OnBoardReset -= () => HandleConfiguredEvent("OnBoardReset");
            Board.OnPlayerBlockedAIWin -= () => HandleConfiguredEvent("OnPlayerBlockedAIWin");
            Board.OnMatchEnd -= HandleMatchEnd; // 新增：取消订阅比赛结束事件
        }
    }

    // --- Initialize Shake Settings ---
    private void InitializeShakeSettings()
    {
        emotionsToShakeSet = new HashSet<EmotionType>(emotionsToShake);
    }

    // --- Game Event Settings Loading ---
    private void LoadGameEventSettings()
    {
        string filePath = "lines/gameEventSettings"; // Path relative to Resources folder
        TextAsset jsonFile = Resources.Load<TextAsset>(filePath);

        if (jsonFile == null)
        {
            Debug.LogError($"CharacterReaction: 事件配置文件未在 Resources/{filePath}.json 找到。事件反应将不可用。");
            return;
        }

        try
        {
            GameEventSettingsCollectionRuntime loadedCollection = JsonUtility.FromJson<GameEventSettingsCollectionRuntime>(jsonFile.text);
            if (loadedCollection != null && loadedCollection.allEventSettings != null)
            {
                gameEventConfig.Clear();
                foreach (var setting in loadedCollection.allEventSettings)
                {
                    if (!string.IsNullOrEmpty(setting.eventIdentifier))
                    {
                        gameEventConfig[setting.eventIdentifier] = setting;
                    }
                }
                Debug.Log($"CharacterReaction: 事件配置已从 {filePath}.json 加载成功。 加载了 {gameEventConfig.Count} 个事件配置。");
            }
            else
            {
                Debug.LogError($"CharacterReaction: 解析事件配置文件 {filePath}.json 失败。文件可能为空或格式不正确。");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CharacterReaction: 加载或解析事件配置文件 {filePath}.json 时发生错误: {e.Message}");
        }
    }

    // --- Generic Event Handler ---
    private void HandleConfiguredEvent(string eventIdentifier)
    {
        if (!gameEventConfig.TryGetValue(eventIdentifier, out EventActionSettingRuntime setting))
        {
            return;
        }

        Debug.Log($"CharacterReaction: 处理事件 '{eventIdentifier}'。心情变化: {setting.moodEffect}, 理智变化: {setting.sanityEffect}");
        CurrentMood += setting.moodEffect;
        CurrentSanity += setting.sanityEffect;

        if (Random.value < setting.dialogueTriggerProbability)
        {
            if (setting.emotionChances == null || setting.emotionChances.Count == 0)
            {
                return;
            }

            float totalWeight = 0f;
            foreach (var chance in setting.emotionChances)
            {
                if (chance.weight > 0) 
                {
                    totalWeight += chance.weight;
                }
            }

            if (totalWeight <= 0)
            {
                return; 
            }

            float randomPick = Random.Range(0f, totalWeight);
            EmotionChanceRuntime chosenEmotionRuntime = null;
            float currentWeightSum = 0f;

            foreach (var chance in setting.emotionChances)
            {
                if (chance.weight <= 0) continue;

                currentWeightSum += chance.weight;
                if (randomPick <= currentWeightSum)
                {
                    chosenEmotionRuntime = chance;
                    break;
                }
            }
            
            if (chosenEmotionRuntime == null && setting.emotionChances.Count > 0)
            {
                for(int i = setting.emotionChances.Count -1; i >= 0; i--) {
                    if(setting.emotionChances[i].weight > 0) {
                        chosenEmotionRuntime = setting.emotionChances[i];
                        break;
                    }
                }
            }

            if (chosenEmotionRuntime != null)
            {
                Debug.Log($"CharacterReaction: 事件 '{eventIdentifier}' 触发表情 '{chosenEmotionRuntime.targetEmotion}' (台词索引: {chosenEmotionRuntime.selectedDialogueIndex}, 权重: {chosenEmotionRuntime.weight}).");
                ChangeEmotion(chosenEmotionRuntime.targetEmotion, chosenEmotionRuntime.selectedDialogueIndex);
            }
            else
            {
                 Debug.LogWarning($"CharacterReaction: 事件 '{eventIdentifier}' 台词已触发，但未能根据权重选择表情。总权重: {totalWeight}");
            }
        }
    }

    private void LoadDialoguesFromFile()
    {
        string filePath = "lines/emotionDialogues"; 
        TextAsset jsonFile = Resources.Load<TextAsset>(filePath);
        if (jsonFile == null)
        {
            Debug.LogError("无法在 Resources/" + filePath + " 中找到台词文件。");
            return;
        }
        try
        {
            DialogueCollection collection = JsonUtility.FromJson<DialogueCollection>(jsonFile.text);
            if (collection != null && collection.allDialogues != null)
            {
                loadedDialogues.Clear();
                foreach (var entry in collection.allDialogues)
                {
                    if (System.Enum.TryParse<EmotionType>(entry.emotion, out EmotionType type))
                    {
                        if (entry.texts != null && entry.texts.Count > 0)
                        {
                            loadedDialogues[type] = new List<string>(entry.texts);
                        }
                        else
                        {
                            loadedDialogues[type] = new List<string> { "" }; 
                        }
                    }
                    else
                    {
                        Debug.LogWarning("JSON文件中发现未知或格式错误的表情类型: " + entry.emotion);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("加载或解析台词JSON文件失败: " + filePath + "\n错误: " + e.Message);
        }
    }

    private void InitializeEmotionDropdown()
    {
        if (Changeem == null) return;
        Changeem.ClearOptions();
        List<string> emotionDisplayNames = System.Enum.GetNames(typeof(EmotionType)).ToList();
        Changeem.AddOptions(emotionDisplayNames);
        Changeem.onValueChanged.RemoveAllListeners(); 
        Changeem.onValueChanged.AddListener(OnEmotionDropdownChanged);
        if (emotionDisplayNames.Count > 0)
        {
            Changeem.value = 0;
            ChangeEmotion((EmotionType)0, -1); 
        }
    }

    public void ChangeEmotion(EmotionType selectedEmotion, int specificDialogueIndex = -1)
    {
        if (emotionImage == null) return;

        if (activeRevertCoroutine != null)
        {
            StopCoroutine(activeRevertCoroutine);
            activeRevertCoroutine = null;
        }

        // 停止任何正在进行的抖动
        if (activeShakeCoroutine != null)
        {
            StopCoroutine(activeShakeCoroutine);
            // 确保图片回到原位
            if (emotionImage != null) emotionImage.rectTransform.anchoredPosition = emotionImageInitialAnchoredPosition;
            activeShakeCoroutine = null;
        }

        switch (selectedEmotion) 
        {
            case EmotionType.普通: emotionImage.sprite = normalSprite; break;
            case EmotionType.开心: emotionImage.sprite = happySprite; break;
            case EmotionType.生气: emotionImage.sprite = angrySprite; break;
            case EmotionType.骄傲: emotionImage.sprite = proudSprite; break;
            case EmotionType.调皮: emotionImage.sprite = naughtySprite; break;
            case EmotionType.哭泣: emotionImage.sprite = cryingSprite; break; 
            case EmotionType.思考: emotionImage.sprite = thinkingSprite; break; // 处理思考表情
            default: emotionImage.sprite = normalSprite; break;
        }
        UpdateDialogueText(selectedEmotion, specificDialogueIndex);

        // 检查是否需要抖动
        if (emotionsToShakeSet != null && emotionsToShakeSet.Contains(selectedEmotion) && emotionImage.gameObject.activeInHierarchy)
        {
            activeShakeCoroutine = StartCoroutine(ShakeEmotionImage());
        }

        if (selectedEmotion != EmotionType.普通)
        {
            activeRevertCoroutine = StartCoroutine(RevertToNormalAfterDelay());
        }
    }

    private IEnumerator RevertToNormalAfterDelay()
    {
        float delay = Random.Range(3f, 5f); 
        yield return new WaitForSeconds(delay);
        
        // 在恢复普通表情前，确保停止可能正在进行的抖动并将图片归位
        if (activeShakeCoroutine != null)
        {
            StopCoroutine(activeShakeCoroutine);
            if (emotionImage != null) emotionImage.rectTransform.anchoredPosition = emotionImageInitialAnchoredPosition;
            activeShakeCoroutine = null;
        }

        ChangeEmotion(EmotionType.普通, -1); 
        activeRevertCoroutine = null; 
    }

    private Vector2 GetOriginalEmotionImagePosition()
    {
        return emotionImageInitialAnchoredPosition;
    }

    private IEnumerator ShakeEmotionImage()
    {
        if (emotionImage == null || !emotionImage.gameObject.activeInHierarchy) yield break;

        // 使用记录的初始位置作为抖动的基准
        Vector2 basePosition = emotionImageInitialAnchoredPosition; 

        try
        {
            for (int i = 0; i < shakeCount; i++)
            {
                float offsetX = Random.Range(-shakeMagnitude, shakeMagnitude);
                float offsetY = Random.Range(-shakeMagnitude, shakeMagnitude);
                emotionImage.rectTransform.anchoredPosition = basePosition + new Vector2(offsetX, offsetY);
                yield return new WaitForSeconds(shakeDurationPerCycle);
            }
        }
        finally
        {
            // 无论协程如何结束（正常完成或被StopCoroutine打断），都恢复到原始位置
            if (emotionImage != null) emotionImage.rectTransform.anchoredPosition = basePosition;
            activeShakeCoroutine = null; // 清理引用
        }
    }

    private void UpdateDialogueText(EmotionType emotion, int specificDialogueIndex = -1)
    {
        if (dialogueDisplayText == null) 
        {
            if (dialogBubble != null) dialogBubble.gameObject.SetActive(false);
            return;
        }
        
        string oldText = dialogueDisplayText.text;
        string newDialogueToDisplay = "";

        List<string> dialoguesForEmotion;
        if (loadedDialogues.TryGetValue(emotion, out dialoguesForEmotion))
        {
            if (dialoguesForEmotion != null && dialoguesForEmotion.Count > 0)
            {
                if (specificDialogueIndex >= 0 && specificDialogueIndex < dialoguesForEmotion.Count)
                {
                    newDialogueToDisplay = dialoguesForEmotion[specificDialogueIndex];
                }
                else if (dialoguesForEmotion.Count > 0) 
                {
                    newDialogueToDisplay = dialoguesForEmotion[0];
                }
            }
        }

        string processedDialogue = ProcessAutomaticLineBreaks(newDialogueToDisplay, 11);
        if (processedDialogue.Length > 60)
        {
            int endIndex = Mathf.Min(processedDialogue.Length, 57);
            processedDialogue = processedDialogue.Substring(0, endIndex) + "...";
        }
        dialogueDisplayText.text = processedDialogue;

        bool shouldShowBubble = !string.IsNullOrEmpty(dialogueDisplayText.text);
        if (dialogBubble != null)
        {
            dialogBubble.gameObject.SetActive(shouldShowBubble);
        }

        if (shouldShowBubble && dialogueDisplayText.text != oldText && emotion != EmotionType.普通)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCharacterSpeechSound();
            }
            else
            {
                Debug.LogWarning("AudioManager.Instance 为空，无法播放角色音效");
            }
        }
    }

    private string ProcessAutomaticLineBreaks(string text, int maxCharsPerLine)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int currentLineCharCount = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                sb.Append(c);
                currentLineCharCount = 0;
                continue;
            }
            if (currentLineCharCount >= maxCharsPerLine && currentLineCharCount > 0)
            {
                if (!char.IsWhiteSpace(c)) 
                {
                    sb.Append('\n');
                    currentLineCharCount = 0;
                }
            }
            sb.Append(c);
            currentLineCharCount++;
        }
        return sb.ToString();
    }

    public void OnEmotionDropdownChanged(int selectedIndex)
    {
        ChangeEmotion((EmotionType)selectedIndex, -1); 
    }

    public void OnPlayerWin()
    {
        Debug.Log("事件：玩家获胜！");
    }

    public void OnPlayerLose()
    {
        Debug.Log("事件：玩家失败！");
    }

    public void OnPlayerConnectTwo()
    {
        Debug.Log("事件：玩家两子连线！");
    }

    public void OnAIConnectTwo()
    {
        Debug.Log("事件：AI两子连线！");
    }

    public void AdjustMood(float amount)
    {
        CurrentMood += amount;
    }

    public void AdjustSanity(float amount)
    {
        CurrentSanity += amount;
    }

    // --- Match End Handler ---
    private void HandleMatchEnd(boardState.State matchWinner)
    {
        string winnerString = matchWinner == boardState.State.X ? "玩家 (X)" : "AI (O)";
        Debug.Log($"比赛结束！胜者: {winnerString}. 当前心情: {CurrentMood}, 当前理智: {CurrentSanity}");

        // 根据心情值触发不同结局的逻辑
        if (CurrentMood > 70)
        {
            Debug.Log("结局触发：好结局！角色心情愉快地接受了比赛结果。");
            // 你之后会在这里实现好结局的弹窗
        }
        else if (CurrentMood > 30)
        {
            Debug.Log("结局触发：普通结局。角色平静地结束了比赛。");
            // 你之后会在这里实现普通结局的弹窗
        }
        else
        {
            Debug.Log("结局触发：坏结局！角色因心情低落而对结果感到沮丧。");
            // 你之后会在这里实现坏结局的弹窗
        }

        // 可以在此禁用进一步的游戏交互，等待弹窗后的操作
        //例如 Time.timeScale = 0; (如果弹窗不是基于Time.timeScale的话)
    }

    // 新增：用于重置角色状态的方法
    public void ResetStats()
    {
        CurrentMood = MAX_MOOD; // 或者使用一个特定的 initialMood 变量
        CurrentSanity = MAX_SANITY; // 或者使用一个特定的 initialSanity 变量
        Debug.Log($"CharacterReaction: Stats reset. Mood: {CurrentMood}, Sanity: {CurrentSanity}");

        // 新增：重置角色表情为"普通"并清除/设置默认对话
        ChangeEmotion(EmotionType.普通, -1); 
        Debug.Log("CharacterReaction: Emotion reset to Normal.");

        // 注意：如果UI滚动条不是通过CurrentMood/CurrentSanity的setter自动更新的，
        // 你可能需要在这里手动更新它们，例如：
        // if (moodScrollbar != null) moodScrollbar.size = Mathf.Clamp01(CurrentMood / MAX_MOOD);
        // if (sanityScrollbar != null) sanityScrollbar.size = Mathf.Clamp01(CurrentSanity / MAX_SANITY);
        // 但根据你当前的setter实现，这应该是自动的。
    }
}
