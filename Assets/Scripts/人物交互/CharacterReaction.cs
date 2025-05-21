using System.Collections;
using System.Collections.Generic;
using System.Linq; // 用于 Enum.GetNames().ToList()
using UnityEngine;
using UnityEngine.UI; // 确保引用UI命名空间
using System.IO; 

public enum EmotionType
{
    普通,
    开心,
    生气,
    骄傲,
    调皮,
    哭泣,
    思考, // 新增思考表情
    PattedHead, // 新增：被摸头
    PattedChest // 新增：被摸胸/身体
}

[System.Serializable]
public class DialogueEntry 
{
    public string emotion;
    public List<string> texts = new List<string>(); // 对话列表
}

[System.Serializable]
public class DialogueCollection
{
    public List<DialogueEntry> allDialogues = new List<DialogueEntry>();
}

// --- 游戏事件设置结构体 (用于JSON反序列化) ---
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
    // isFoldedOut 仅编辑器使用，此处不需要
}

[System.Serializable]
public class GameEventSettingsCollectionRuntime
{
    public List<EventActionSettingRuntime> allEventSettings = new List<EventActionSettingRuntime>();
}
// --- 游戏事件设置结构体结束 ---

public class CharacterReaction : MonoBehaviour
{
    [Header("角色表情精灵")]
    [SerializeField] [Tooltip("普通表情的图片")] private Sprite normalSprite;
    [SerializeField] [Tooltip("开心表情的图片")] private Sprite happySprite;
    [SerializeField] [Tooltip("生气表情的图片")] private Sprite angrySprite;
    [SerializeField] [Tooltip("骄傲表情的图片")] private Sprite proudSprite;
    [SerializeField] [Tooltip("调皮表情的图片")] private Sprite naughtySprite;
    [SerializeField] [Tooltip("哭泣表情的图片")] private Sprite cryingSprite;
    [SerializeField] [Tooltip("思考表情的图片")] private Sprite thinkingSprite; // 思考表情Sprite
    [SerializeField] [Tooltip("被摸头表情的图片")] private Sprite pattedHeadSprite; // 新增
    [SerializeField] [Tooltip("被摸胸/身体表情的图片")] private Sprite pattedChestSprite; // 新增

    [Header("UI组件引用")]
    public Image emotionImage; 
    public Dropdown Changeem;
    public Text dialogueDisplayText;
    public Image dialogBubble;
    public Scrollbar moodScrollbar; // 心情值滚动条
    public Scrollbar sanityScrollbar; // 理智值滚动条

    [Header("AI下棋引用")]
    public 井字棋AI AI; // AI脚本引用

    [Header("交互区域指定 (UISwipeInteraction)")] // 新增Header
    [Tooltip("指定用于检测头部触摸的UISwipeInteraction组件")]
    public UISwipeInteraction headTouchDetector; // 新增字段
    [Tooltip("指定用于检测胸部触摸的UISwipeInteraction组件")]
    public UISwipeInteraction chestTouchDetector; // 新增字段

    [Header("交互事件CD设置 (秒)")] // 新增Header
    [Tooltip("摸头事件的短CD (完全阻止事件)")]
    public float headPatShortCooldown = 10f;
    [Tooltip("摸头事件的长CD (仅表情/对话，无数值变化)")]
    public float headPatLongCooldown = 60f;
    [Tooltip("摸胸事件的短CD (完全阻止事件)")]
    public float chestPatShortCooldown = 10f;
    [Tooltip("摸胸事件的长CD (仅表情/对话，无数值变化)")]
    public float chestPatLongCooldown = 60f;

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

    public boardState Board; // 棋局状态引用
  
    private Dictionary<EmotionType, List<string>> loadedDialogues = new Dictionary<EmotionType, List<string>>();
    private Dictionary<string, EventActionSettingRuntime> gameEventConfig = new Dictionary<string, EventActionSettingRuntime>(); // 加载的事件配置
    private Coroutine activeRevertCoroutine; // 跟踪恢复表情的协程
    private Coroutine activeShakeCoroutine; // 跟踪抖动协程
    private HashSet<EmotionType> emotionsToShakeSet; // 用于快速查找是否需要抖动
    private Vector2 emotionImageInitialAnchoredPosition; // 表情图片的初始位置
    private System.Random rand = new System.Random(); // 随机数生成器

    // --- CD 计时器 ---
    private float headPatShortCdEndTime = 0f;
    private float headPatLongCdEndTime = 0f;
    private float chestPatShortCdEndTime = 0f;
    private float chestPatLongCdEndTime = 0f;

    [Header("游戏逻辑引用")] // 新增Header
    [Tooltip("对boardState脚本实例的引用，用于显示冷却提示信息")]
    public boardState gameBoardState; // 新增：对boardState的引用

    void Awake() // 改为Awake以确保事件注册在Board的Start之前或同时
    {
        if (Board == null) Board = FindObjectOfType<boardState>();
        if (AI == null) AI = FindObjectOfType<井字棋AI>();

        // 新增：确保 gameBoardState 也被赋值，如果未在Inspector中设置，尝试查找
        if (gameBoardState == null)
        {
            gameBoardState = FindObjectOfType<boardState>();
            if (gameBoardState == null)
            {
                Debug.LogError("CharacterReaction: boardState (gameBoardState) instance not found and not assigned in Inspector. Cannot show cooldown messages.");
            }
        }

        if (Board != null)
        {
            // 注册游戏事件到通用处理器
            Board.OnGameDraw += () => HandleConfiguredEvent("OnGameDraw");
            Board.OnAIWinsGame += () => HandleConfiguredEvent("OnAIWinsGame");
            Board.OnPlayerWinsGame += () => HandleConfiguredEvent("OnPlayerWinsGame");
            Board.OnAIAlmostWins += () => HandleConfiguredEvent("OnAIAlmostWins");
            Board.OnPlayerAlmostWins += () => HandleConfiguredEvent("OnPlayerAlmostWins");
            Board.OnBoardReset += () => HandleConfiguredEvent("OnBoardReset");
            Board.OnPlayerBlockedAIWin += () => HandleConfiguredEvent("OnPlayerBlockedAIWin");
            Board.OnMatchEnd += HandleMatchEnd; // 订阅比赛结束事件
        }
        else
        {
            Debug.LogError("CharacterReaction: 未找到boardState对象以注册事件!");
        }
    }

    void Start()
    {
        if (Board == null || AI == null) // 再次检查依赖项
        {
            Debug.LogError("CharacterReaction: 依赖的boardState或井字棋AI对象缺失!");
            enabled = false; // 禁用此脚本
            return;
        }
        
        if (emotionImage != null)
        {
            emotionImageInitialAnchoredPosition = emotionImage.rectTransform.anchoredPosition;
        }
        else
        {
            Debug.LogError("CharacterReaction: emotionImage未在Inspector中分配！抖动和位置恢复可能异常。");
        }

        CurrentMood = MAX_MOOD; // 初始化心情值，通过setter更新UI
        CurrentSanity = MAX_SANITY; // 初始化理智值，通过setter更新UI
        
        // 示例：设置滚动条不可交互，并确保Handle可见 (此段逻辑已被注释掉，如需使用请取消注释并调整)
        // if (moodScrollbar != null)
        // {
        //     moodScrollbar.interactable = false;
        //     if (moodScrollbar.targetGraphic != null)
        //     {
        //         Color handleColor = moodScrollbar.targetGraphic.color;
        //         handleColor.a = 1f; // 强制Alpha为1
        //         moodScrollbar.targetGraphic.color = handleColor;
        //     }
        // }
        // if (sanityScrollbar != null)
        // {
        //     sanityScrollbar.interactable = false;
        //     if (sanityScrollbar.targetGraphic != null)
        //     {
        //         Color handleColor = sanityScrollbar.targetGraphic.color;
        //         handleColor.a = 1f; // 强制Alpha为1
        //         sanityScrollbar.targetGraphic.color = handleColor;
        //     }
        // }

        LoadDialoguesFromFile(); 
        LoadGameEventSettings(); // 加载事件配置
        InitializeEmotionDropdown();
        InitializeShakeSettings();

        // 新增：动态注册来自UISwipeInteraction的事件
        if (headTouchDetector != null)
        {
            // 默认监听按下事件作为"触摸"
            // 如果希望监听"来回滑动"，可以使用 headTouchDetector.OnSwipeBackAndForth.AddListener(ReactToHeadTouch);
            headTouchDetector.OnSwipeBackAndForth.AddListener(ReactToHeadTouch); // 改为监听来回滑动
            Debug.Log("CharacterReaction: Successfully subscribed to headTouchDetector.OnSwipeBackAndForth");
        }
        else
        {
            Debug.LogWarning("CharacterReaction: headTouchDetector is not assigned in the Inspector.");
        }

        if (chestTouchDetector != null)
        {
            // chestTouchDetector.OnPointerPressed.AddListener(ReactToChestTouch);
            chestTouchDetector.OnSwipeBackAndForth.AddListener(ReactToChestTouch); // 改为监听来回滑动
            Debug.Log("CharacterReaction: Successfully subscribed to chestTouchDetector.OnSwipeBackAndForth");
        }
        else
        {
            Debug.LogWarning("CharacterReaction: chestTouchDetector is not assigned in the Inspector.");
        }
    }

    void OnDestroy() // 对象销毁时取消事件注册
    {
        if (Board != null)
        {
            // 取消所有事件的订阅
            Board.OnGameDraw -= () => HandleConfiguredEvent("OnGameDraw"); 
            Board.OnAIWinsGame -= () => HandleConfiguredEvent("OnAIWinsGame"); 
            Board.OnPlayerWinsGame -= () => HandleConfiguredEvent("OnPlayerWinsGame"); 
            Board.OnAIAlmostWins -= () => HandleConfiguredEvent("OnAIAlmostWins");
            Board.OnPlayerAlmostWins -= () => HandleConfiguredEvent("OnPlayerAlmostWins");
            Board.OnBoardReset -= () => HandleConfiguredEvent("OnBoardReset");
            Board.OnPlayerBlockedAIWin -= () => HandleConfiguredEvent("OnPlayerBlockedAIWin");
            Board.OnMatchEnd -= HandleMatchEnd; // 取消订阅比赛结束事件
        }

        // 新增：移除 UISwipeInteraction 事件监听
        if (headTouchDetector != null)
        {
            // headTouchDetector.OnPointerPressed.RemoveListener(ReactToHeadTouch);
            // 如果之前监听了其他事件，如 OnSwipeBackAndForth，也需要在此移除
            headTouchDetector.OnSwipeBackAndForth.RemoveListener(ReactToHeadTouch); // 确保移除对应的监听
        }
        if (chestTouchDetector != null)
        {
            // chestTouchDetector.OnPointerPressed.RemoveListener(ReactToChestTouch);
            chestTouchDetector.OnSwipeBackAndForth.RemoveListener(ReactToChestTouch); // 确保移除对应的监听
        }
    }

    // --- 初始化抖动设置 ---
    private void InitializeShakeSettings()
    {
        emotionsToShakeSet = new HashSet<EmotionType>(emotionsToShake);
    }

    // --- 加载游戏事件配置 ---
    private void LoadGameEventSettings()
    {
        string filePath = "lines/gameEventSettings"; // Resources文件夹下的相对路径
        TextAsset jsonFile = Resources.Load<TextAsset>(filePath);

        if (jsonFile == null)
        {
            Debug.LogError($"CharacterReaction: 事件配置文件 Resources/{filePath}.json 未找到。");
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
                Debug.Log($"CharacterReaction: 事件配置从 {filePath}.json 加载成功，共 {gameEventConfig.Count} 条。");
            }
            else
            {
                Debug.LogError($"CharacterReaction: 解析事件配置文件 {filePath}.json 失败，文件可能为空或格式错误。");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CharacterReaction: 加载或解析事件配置文件 {filePath}.json 时出错: {e.Message}");
        }
    }

    // --- 通用事件处理器 ---
    private void HandleConfiguredEvent(string eventIdentifier, bool bypassValueChanges = false)
    {
        if (!gameEventConfig.TryGetValue(eventIdentifier, out EventActionSettingRuntime setting))
        {
            // 如果事件未在配置中找到，可以选择执行默认操作或静默返回。
            // 当前：未配置则不执行任何操作。
            // Debug.LogWarning($"CharacterReaction: 事件 '{eventIdentifier}' 未在配置中找到.");
            return;
        }

        // 根据上下文特殊处理OnBoardReset事件
        if (eventIdentifier == "OnBoardReset")
        {
            bool isAutomaticResetAfterRound = Board != null && Board.IsCurrentlyResettingAfterRoundEnd;
            bool wasBoardActuallyFull = Board != null && Board.WasBoardFullJustBeforeThisReset; // 检查棋盘是否在重置前已满

            // 仅当是棋局结束后自动重置，并且重置前棋盘已满时，才应用OnBoardReset配置的效果
            if (isAutomaticResetAfterRound && wasBoardActuallyFull)
            {
                Debug.Log($"CharacterReaction: 事件 '{eventIdentifier}' 继续执行，因为是满棋盘后的自动重置。");
                // 继续执行OnBoardReset的配置效果
            }
            else
            {
                string reason = "手动重置";
                if (isAutomaticResetAfterRound && !wasBoardActuallyFull) reason = "自动重置但棋盘未满";
                else if (!isAutomaticResetAfterRound) reason = "非局后自动重置";
                
                Debug.Log($"CharacterReaction: 事件 '{eventIdentifier}' 的效果（心情/理智/对话）已跳过。原因: {reason}.");
                // 跳过OnBoardReset配置的心情/理智变化及对话。
                // 如果需要，仍可在此处触发一个非常通用的、不影响数值的对话/表情。
                return; 
            }
        }

        Debug.Log($"CharacterReaction: 处理事件 '{eventIdentifier}'. 心情变化: {setting.moodEffect}, 理智变化: {setting.sanityEffect}, 跳过数值变化: {bypassValueChanges}");
        if (!bypassValueChanges)
        {
            CurrentMood += setting.moodEffect;
            CurrentSanity += setting.sanityEffect;
        }

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
            
            if (chosenEmotionRuntime == null && setting.emotionChances.Count > 0) // 如果权重选择失败，则选择最后一个有效项作为后备
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
        string filePath = "lines/emotionDialogues"; // Resources文件夹下的相对路径
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
                            loadedDialogues[type] = new List<string> { "" }; // 如果没有台词，添加一个空字符串作为默认
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
            ChangeEmotion((EmotionType)0, -1); // 默认显示第一个表情
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
            case EmotionType.PattedHead: emotionImage.sprite = pattedHeadSprite; break; // 新增
            case EmotionType.PattedChest: emotionImage.sprite = pattedChestSprite; break; // 新增
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

    private Vector2 GetOriginalEmotionImagePosition() // 此方法当前未被使用，可以考虑移除或按需使用
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
            if (dialogBubble != null) dialogBubble.gameObject.SetActive(false); // 如果对话文本组件为空，也隐藏气泡
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
                else if (dialoguesForEmotion.Count > 0) // 如果未指定索引或索引无效，使用第一个台词
                {
                    newDialogueToDisplay = dialoguesForEmotion[0];
                }
            }
        }

        string processedDialogue = ProcessAutomaticLineBreaks(newDialogueToDisplay, 11); // 11为每行最大字符数
        if (processedDialogue.Length > 60) // 对话最大长度限制
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

        // 如果对话文本有变化且不是普通表情，播放语音
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
            if (c == '\n') // 处理预设的换行符
            {
                sb.Append(c);
                currentLineCharCount = 0;
                continue;
            }
            if (currentLineCharCount >= maxCharsPerLine && currentLineCharCount > 0) // 超出单行字符数限制
            {
                if (!char.IsWhiteSpace(c)) // 避免在空格处换行后产生额外空行
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

    public void OnPlayerWin() // 事件：玩家胜利（此方法当前未被boardState直接调用，可按需连接或移除）
    {
        Debug.Log("事件：玩家获胜！");
    }

    public void OnPlayerLose() // 事件：玩家失败（此方法当前未被boardState直接调用，可按需连接或移除）
    {
        Debug.Log("事件：玩家失败！");
    }

    public void OnPlayerConnectTwo() // 事件：玩家两子连线（此方法当前未被boardState直接调用，可按需连接或移除）
    {
        Debug.Log("事件：玩家两子连线！");
    }

    public void OnAIConnectTwo() // 事件：AI两子连线（此方法当前未被boardState直接调用，可按需连接或移除）
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

    // --- 比赛结束处理器 ---
    private void HandleMatchEnd(boardState.State matchWinner)
    {
        string winnerString = matchWinner == boardState.State.X ? "玩家 (X)" : "AI (O)";
        Debug.Log($"比赛结束！胜者: {winnerString}. 当前心情: {CurrentMood}, 当前理智: {CurrentSanity}");

        // 此处是旧的结局触发逻辑，实际结局触发已移至boardState.AwardPoint中的PlayerProcess.CheckAndUnlockEnding
        // 可以保留此处的Debug信息或根据新的结局显示流程调整
        if (CurrentMood > 70)
        {
            Debug.Log("HandleMatchEnd: 好结局条件（心情 > 70）可能已满足。实际结局由PlayerProcess处理。");
        }
        else if (CurrentMood > 30)
        {
            Debug.Log("HandleMatchEnd: 普通结局条件（心情 > 30）可能已满足。实际结局由PlayerProcess处理。");
        }
        else
        {
            Debug.Log("HandleMatchEnd: 坏结局条件（心情 <= 30）可能已满足。实际结局由PlayerProcess处理。");
        }

        // 可在此处禁用游戏交互，等待弹窗操作，例如 Time.timeScale = 0;
    }

    // 重置角色状态
    public void ResetStats()
    {
        CurrentMood = MAX_MOOD; // 或使用特定的初始心情值
        CurrentSanity = MAX_SANITY; // 或使用特定的初始理智值
        Debug.Log($"CharacterReaction: 状态已重置。 心情: {CurrentMood}, 理智: {CurrentSanity}");

        // 重置角色表情为"普通"并清除/设置默认对话
        ChangeEmotion(EmotionType.普通, -1); 
        Debug.Log("CharacterReaction: 表情已重置为普通。");

        // 注意：如果UI滚动条不是通过CurrentMood/CurrentSanity的setter自动更新的，
        // 可能需要在此处手动更新，但根据当前setter实现，应为自动更新。
    }

    // --- 新增：特定区域触摸的反应方法 ---
    public void ReactToHeadTouch()
    {
        Debug.Log("CharacterReaction: Head was touched attempt.");

        if (Time.time < headPatShortCdEndTime)
        {
            Debug.Log("CharacterReaction: Head pat on short cooldown. Displaying warning.");
            if (gameBoardState != null)
            {
                gameBoardState.ShowTemporaryMessage("要被摸坏了！", 1.5f); // 显示1.5秒
            }
            else
            {
                Debug.LogWarning("CharacterReaction: gameBoardState is null, cannot display '要被摸坏了！'");
            }
            return; // 短CD期间，显示提示后忽略其他动作
        }

        // 只要事件能进行到这里（短CD已过），就意味着会有表情/对话反应
        // 因此，短CD在本次交互成功后总是应该被重置

        bool isLongCdActive = Time.time < headPatLongCdEndTime;
        
        if (isLongCdActive)
        {
            Debug.Log("CharacterReaction: Head pat on long cooldown. Triggering with no value changes.");
        }
        else
        {
            Debug.Log("CharacterReaction: Head pat cooldowns clear for full event. Resetting long CD.");
            // 只有在长CD也结束时（即完整事件触发），才重置长CD的计时器
            headPatLongCdEndTime = Time.time + headPatLongCooldown;
        }
        
        // 执行事件（可能只包含表情/对话，如果长CD激活）
        HandleConfiguredEvent("OnHeadPatted", bypassValueChanges: isLongCdActive);

        // 无论长CD状态如何，只要成功触发了表情/对话，就重置短CD
        Debug.Log("CharacterReaction: Head pat action performed. Resetting short CD.");
        headPatShortCdEndTime = Time.time + headPatShortCooldown;
    }

    public void ReactToChestTouch()
    {
        Debug.Log("CharacterReaction: Chest was touched attempt.");

        if (Time.time < chestPatShortCdEndTime)
        {
            Debug.Log("CharacterReaction: Chest pat on short cooldown. Displaying warning.");
            if (gameBoardState != null)
            {
                gameBoardState.ShowTemporaryMessage("要被摸坏了！", 1.5f); // 显示1.5秒
            }
            else
            {
                Debug.LogWarning("CharacterReaction: gameBoardState is null, cannot display '要被摸坏了！'");
            }
            return; // 短CD期间，显示提示后忽略其他动作
        }

        bool isLongCdActive = Time.time < chestPatLongCdEndTime;

        if (isLongCdActive)
        {
            Debug.Log("CharacterReaction: Chest pat on long cooldown. Triggering with no value changes.");
        }
        else
        {
            Debug.Log("CharacterReaction: Chest pat cooldowns clear for full event. Resetting long CD.");
            chestPatLongCdEndTime = Time.time + chestPatLongCooldown;
        }

        HandleConfiguredEvent("OnChestPatted", bypassValueChanges: isLongCdActive);

        Debug.Log("CharacterReaction: Chest pat action performed. Resetting short CD.");
        chestPatShortCdEndTime = Time.time + chestPatShortCooldown;
    }
}
