using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic; // 需要 List (虽然当前未使用，但通常有用)
using System;

public class CGviewer : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImageComponent; // 新增：用于显示结局背景图
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Image displayImage;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button skipTextButton; // 用于快进文本的按钮 (例如屏幕中央的透明按钮)
    [SerializeField] private Button toggleSubtitlesButton; // 新增：用于切换字幕可见性的按钮
    [SerializeField] private Text toggleSubtitlesButtonText; // 新增：切换字幕按钮上的文本

    [Header("Settings")]
    [SerializeField] private float typewriterDelay = 0.05f;
    [SerializeField] private float titleDisplayDuration = 2.5f;

    private Ending currentEnding;
    private int currentPageIndex = 0;
    private Coroutine typewriterCoroutine;
    private Coroutine titleDisplayCoroutine;
    private bool fullTextDisplayedForPage = false;
    private string currentFullPageText = "";
    private bool areSubtitlesHidden = false; // 新增：字幕是否隐藏的状态

    private Action customOnCloseCallback; // 新增：用于自定义关闭行为的回调

    void Awake()
    {
        if (nextButton != null) nextButton.onClick.AddListener(OnNextButtonClicked);
        if (prevButton != null) prevButton.onClick.AddListener(OnPrevButtonClicked);
        if (exitButton != null) exitButton.onClick.AddListener(ExitToMainMenu);
        if (skipTextButton != null) skipTextButton.onClick.AddListener(OnSkipTextButtonClicked);
        if (toggleSubtitlesButton != null) toggleSubtitlesButton.onClick.AddListener(ToggleSubtitles); // 新增

        // 添加点击事件监听
        if (gameObject.GetComponent<Button>() == null)
        {
            Button clickButton = gameObject.AddComponent<Button>();
            clickButton.onClick.AddListener(OnScreenClicked);
        }

        gameObject.SetActive(false); // CG查看器默认不显示

        // 初始化字幕按钮文本
        UpdateSubtitleButtonText(); // 新增
    }

    public void SetOnCloseCallback(Action callback)
    {
        customOnCloseCallback = callback;
    }

    private void OnScreenClicked()
    {
        // 如果正在显示标题，则跳过标题显示
        if (titleDisplayCoroutine != null)
        {
            StopCoroutine(titleDisplayCoroutine);
            titleDisplayCoroutine = null;
            if (titleText != null)
            {
                titleText.gameObject.SetActive(false);
            }
            // 直接加载第一页内容 (此时背景和BGM应已设置)
            LoadPageContent(currentPageIndex);
        }
    }

    public void DisplayEnding(int endingId)
    {
        if (PlayerProcess.Instance == null)
        {
            Debug.LogError("CGviewer: PlayerProcess.Instance is null. Cannot load ending.");
            gameObject.SetActive(false); // 确保CG查看器被隐藏
            return;
        }
        currentEnding = PlayerProcess.Instance.GetEndingById(endingId);

        if (currentEnding == null)
        {
            Debug.LogError($"CGviewer: Ending with ID {endingId} not found.");
            gameObject.SetActive(false);
            return;
        }
        
        gameObject.SetActive(true);
        currentPageIndex = 0;

        // 设置结局背景图片
        if (backgroundImageComponent != null)
        {
            if (!string.IsNullOrEmpty(currentEnding.BackgroundImagePath))
            {
                Sprite bgSprite = Resources.Load<Sprite>(currentEnding.BackgroundImagePath);
                if (bgSprite != null)
                {
                    backgroundImageComponent.sprite = bgSprite;
                    backgroundImageComponent.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"CGviewer: Could not load background image '{currentEnding.BackgroundImagePath}' from Resources.");
                    backgroundImageComponent.gameObject.SetActive(false);
                }
            }
            else
            {
                backgroundImageComponent.gameObject.SetActive(false); // 如果没有配置背景图，则隐藏
            }
        }

        // 播放结局BGM
        if (AudioManager.Instance != null && !string.IsNullOrEmpty(currentEnding.BGMusicPath))
        {
            AudioClip endingMusic = Resources.Load<AudioClip>(currentEnding.BGMusicPath);
            if (endingMusic != null)
            {
                AudioManager.Instance.PlayBGM(endingMusic, true, 1f); // 使用1秒渐变，可调整
            }
            else
            {
                Debug.LogWarning($"CGviewer: Could not load BGM '{currentEnding.BGMusicPath}' from Resources. Main BGM might still be playing or it might be silent.");
                 // 可选：如果结局BGM加载失败，明确停止当前BGM或播放默认静音，而不是让主BGM继续
                // AudioManager.Instance.PlayBGM(null, true, 0.5f); // 渐隐当前BGM
            }
        }
        // else if (AudioManager.Instance != null) {
            // AudioManager.Instance.PlayBGM(null, true, 0.5f); // 如果没有配置结局BGM，渐隐当前BGM
        // }

        StartCoroutine(ShowTitleAndFirstPageSequence());
    }

    private IEnumerator ShowTitleAndFirstPageSequence()
    {
        // 1. 显示标题和第一页图片 (如果存在)
        if (titleText != null)
        {
            titleText.text = currentEnding.title;
            titleText.gameObject.SetActive(true);
        }

        if (currentEnding.ImagesName != null && currentEnding.ImagesName.Length > 0 && !string.IsNullOrEmpty(currentEnding.ImagesName[0]))
        {
            LoadImageForPage(0); // 加载第一页图片
        }
        else if (displayImage != null)
        {
            displayImage.gameObject.SetActive(false); // 如果没图片则隐藏
        }
        
        if (bodyText != null) bodyText.text = ""; // 清空正文，等待标题结束后显示

        // 2. 等待标题显示时间，但允许点击跳过
        titleDisplayCoroutine = StartCoroutine(WaitForTitleDisplay());
        yield return titleDisplayCoroutine;

        // 3. 隐藏标题
        if (titleText != null)
        {
            titleText.gameObject.SetActive(false);
        }

        // 4. 加载第一页的文本内容和对应的导航按钮状态
        LoadPageContent(currentPageIndex);
    }

    private IEnumerator WaitForTitleDisplay()
    {
        float elapsedTime = 0f;
        while (elapsedTime < titleDisplayDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    private void LoadPageContent(int pageIndex)
    {
        // 确保标题被隐藏
        if (titleText != null)
        {
            titleText.gameObject.SetActive(false);
        }
        if (titleDisplayCoroutine != null)
        {
            StopCoroutine(titleDisplayCoroutine);
            titleDisplayCoroutine = null;
        }

        if (currentEnding == null || currentEnding.EndingText == null || pageIndex < 0)
        {
            Debug.LogWarning("CGviewer: Invalid page index or missing ending data.");
            UpdateNavigationButtons(); // 即使内容无效，也更新按钮状态
            return;
        }
        
        // 处理文本数组越界或空数组的情况
        if (pageIndex >= currentEnding.EndingText.Length)
        {
             // 如果页码超出文本数组，可能意味着这是纯图片页或者数据有问题
             // 为简化，我们假设文本和图片数组长度应该对应，或者文本主导页数
            Debug.LogWarning($"CGviewer: Page index {pageIndex} is out of bounds for EndingText (length {currentEnding.EndingText.Length}).");
            currentFullPageText = ""; // 显示空文本
            if (bodyText != null) bodyText.text = "";
        }
        else
        {
            currentFullPageText = currentEnding.EndingText[pageIndex];
        }

        fullTextDisplayedForPage = false;

        if (bodyText != null)
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }
            if (!string.IsNullOrEmpty(currentFullPageText))
            {
                 typewriterCoroutine = StartCoroutine(TypewriterEffect(currentFullPageText));
            }
            else // 如果当前页文本为空，则直接标记为已完成
            {
                bodyText.text = "";
                fullTextDisplayedForPage = true;
                typewriterCoroutine = null;
            }
            bodyText.gameObject.SetActive(!areSubtitlesHidden); // 新增：根据状态设置文本可见性
        }
        
        LoadImageForPage(pageIndex);
        UpdateNavigationButtons();
    }
    
    private void LoadImageForPage(int pageIndex)
    {
        if (displayImage == null) return;

        if (currentEnding.ImagesName != null && pageIndex < currentEnding.ImagesName.Length && !string.IsNullOrEmpty(currentEnding.ImagesName[pageIndex]))
        {
            Sprite sprite = Resources.Load<Sprite>(currentEnding.ImagesName[pageIndex]);
            if (sprite != null)
            {
                displayImage.sprite = sprite;
                displayImage.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"CGviewer: Could not load image '{currentEnding.ImagesName[pageIndex]}' from Resources.");
                displayImage.gameObject.SetActive(false);
            }
        }
        else
        {
            displayImage.gameObject.SetActive(false); // 如果该页没有图片名或超出数组范围，则隐藏图片
        }
    }


    private IEnumerator TypewriterEffect(string textToType)
    {
        if (bodyText == null) yield break;
        bodyText.text = ""; // 即使隐藏也要先清空，以便显示时从头开始
        fullTextDisplayedForPage = false; // 重置状态
        foreach (char letter in textToType.ToCharArray())
        {
            if (!areSubtitlesHidden) // 新增：仅在字幕可见时更新文本
            {
                bodyText.text += letter;
            }
            yield return new WaitForSeconds(typewriterDelay);
        }
        // 打字结束后，即使是隐藏的，也更新完整文本，以便切换回显示状态时能看到完整内容
        if (areSubtitlesHidden && bodyText != null)
        {
            bodyText.text = textToType;
        }
        typewriterCoroutine = null;
        fullTextDisplayedForPage = true;
    }
    private void SkipTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        if (bodyText != null && !fullTextDisplayedForPage)
        {
            bodyText.text = currentFullPageText; // 无论是否隐藏，都填充完整文本
            fullTextDisplayedForPage = true;
            // 字幕的可见性由 areSubtitlesHidden 和 bodyText.gameObject.SetActive 控制
        }
    }

    private void OnNextButtonClicked()
    {
        if (currentEnding == null || currentEnding.EndingText == null) return;

        if (typewriterCoroutine != null && !fullTextDisplayedForPage)
        {
            SkipTypewriter();
            // 允许再次点击以翻页，或者在此处直接翻页（当前为前者）
        }
        else if (currentPageIndex < currentEnding.EndingText.Length - 1)
        {
            currentPageIndex++;
            LoadPageContent(currentPageIndex);
        }
    }

    private void OnPrevButtonClicked()
    {
        if (currentEnding == null) return;

        if (typewriterCoroutine != null && !fullTextDisplayedForPage)
        {
            SkipTypewriter();
             // 允许再次点击以翻页
        }
        else if (currentPageIndex > 0)
        {
            currentPageIndex--;
            LoadPageContent(currentPageIndex);
        }
    }

    private void OnSkipTextButtonClicked()
    {
        // 优先处理：如果标题正在显示，则跳过标题并直接显示第一页内容
        if (titleDisplayCoroutine != null)
        {
            StopCoroutine(titleDisplayCoroutine);
            titleDisplayCoroutine = null;
            if (titleText != null)
            {
                titleText.gameObject.SetActive(false);
            }
            // currentPageIndex 此时应为 0 （由 ShowTitleAndFirstPageSequence 设置）
            LoadPageContent(currentPageIndex); 
            return; // 操作完成，退出
        }

        // 如果文字正在打字，则快进显示完整文字
        if (typewriterCoroutine != null && !fullTextDisplayedForPage)
        {
            SkipTypewriter();
            // 快进后，按钮状态将由Update()重新评估，再次点击可能会翻页或显示弹窗
            return; 
        }

        // 如果标题和打字都已结束（或当前页没有文字可打）
        bool hasText = currentEnding != null && currentEnding.EndingText != null && currentEnding.EndingText.Length > 0;

        if (hasText)
        {
            // 如果有文本内容，并且不是最后一页，则翻到下一页
            if (currentPageIndex < currentEnding.EndingText.Length - 1)
            {
                currentPageIndex++;
                LoadPageContent(currentPageIndex);
            }
            else // 是最后一页文本（或唯一的文本页），并且文本已完全显示
            {
                ShowEndingPopup();
            }
        }
        else // 没有文本内容 (currentEnding.EndingText 为 null 或为空)
        {
            // 如果标题刚刚被跳过（或正常结束），并且没有任何文本，则直接显示结局弹窗
            ShowEndingPopup();
        }
    }

    private void ShowEndingPopup()
    {
        if (UIManager.Instance != null)
        {
            string button2Text = "确认返回";
            Action button2Action = () => {
                // 默认返回主页
                ExitToMainMenu();
            };

            if (customOnCloseCallback != null)
            {
                button2Text = "返回列表"; // 或者其他合适的文本
                button2Action = () => {
                    customOnCloseCallback?.Invoke();
                    // 调用回调后，通常也需要关闭CGViewer自身
                    // UIManager 在 HideAllCoreCanvases 中会处理 Destroy(currentCGViewerInstance)
                    // 所以这里不需要显式销毁，但要确保回调会切换Canvas
                };
            }

            PopupWindows popup = UIManager.Instance.ShowSinglePagePopup(
                "剧情已结束", // 消息简化
                "从头播放",
                () => {
                    // 从头播放 (此时结局的背景和BGM应该已经是当前结局的，所以直接重置页面和标题序列)
                    currentPageIndex = 0;
                    StartCoroutine(ShowTitleAndFirstPageSequence());
                },
                button2Text, // 使用上面决定的文本
                button2Action  // 使用上面决定的动作
            );

            // 弹窗的关闭逻辑已移至 PopupWindows 脚本自身处理（如果按钮有回调）
        }
        else
        {
            Debug.LogError("CGviewer: UIManager.Instance is null. Cannot show ending popup.");
            // 如果无法显示弹窗，直接返回主页
            ExitToMainMenu();
        }
    }

    private void UpdateNavigationButtons()
    {
        bool hasTextContent = currentEnding != null && currentEnding.EndingText != null && currentEnding.EndingText.Length > 0;

        if (prevButton != null)
        {
            prevButton.gameObject.SetActive(hasTextContent && currentPageIndex > 0);
        }
        if (nextButton != null)
        {
            // 恢复下一页按钮的显示
            nextButton.gameObject.SetActive(hasTextContent && currentPageIndex < currentEnding.EndingText.Length - 1);
        }
        // skipTextButton 的显隐逻辑已移至 Update() 方法
    }

    private void ExitToMainMenu()
    {
        Debug.Log("CGViewer: ExitToMainMenu called.");

        // 此方法现在主要作为没有自定义回调时的后备
        // 如果有自定义回调，则不应直接调用此方法，而是调用回调
        if (customOnCloseCallback != null)
        {
            Debug.LogWarning("CGViewer: ExitToMainMenu called, but a customOnCloseCallback exists. The callback should have been called instead.");
            // 理论上，如果 customOnCloseCallback 存在，ShowEndingPopup 应该调用它而不是 ExitToMainMenu
            // 但作为安全措施，如果意外调用到这里，我们也优先执行自定义回调
            customOnCloseCallback.Invoke(); // 这里不直接return，因为customOnCloseCallback可能不包含恢复BGM的逻辑
            // return; // Previous version had a return here.
        }

        // 恢复主BGM的逻辑移至UIManager.ShowMainMenu或自定义回调中更合适，
        // 因为CGViewer自身关闭时，AudioManager实例可能仍然存在。
        // 此处调用会确保在直接通过此方法退出时BGM被重置。
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMainBGM(1f); // 使用1秒渐变切回主BGM
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMainMenu();
        }
        else
        {
            Debug.LogError("CGViewer: UIManager.Instance is null. Cannot switch to Main Menu.");
            // 作为后备，如果UIManager找不到，尝试直接禁用自身，但这可能不是最佳方案
            // gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // 更新 skipTextButton (中央跳过/快进按钮) 的可见性
        if (skipTextButton == null) return;

        if (currentEnding == null)
        {
            skipTextButton.gameObject.SetActive(false);
            return;
        }

        bool hasText = currentEnding.EndingText != null && currentEnding.EndingText.Length > 0;
        bool shouldBeActive = false;

        if (titleDisplayCoroutine != null) // 1. 标题正在显示
        {
            shouldBeActive = true;
        }
        else if (typewriterCoroutine != null && !fullTextDisplayedForPage) // 2. 文本正在打字 (意味着 hasText 为 true)
        {
            shouldBeActive = true;
        }
        else // 标题已结束，且当前页的文本 (如果有) 已完全显示
        {
            if (hasText)
            {
                bool isLastTextPage = currentPageIndex >= currentEnding.EndingText.Length - 1;
                if (!isLastTextPage) // 可以前进到下一文本页
                {
                    shouldBeActive = true;
                }
                else // 在最后一页文本 (或唯一的文本页) 且文本已完全显示
                {
                    shouldBeActive = true; // 可以显示结局弹窗
                }
            }
            else // 根本没有文本内容，且标题已结束
            {
                // 允许点击以显示结局弹窗
                shouldBeActive = true;
            }
        }
        skipTextButton.gameObject.SetActive(shouldBeActive);
    }

    void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveAllListeners();
        if (prevButton != null) prevButton.onClick.RemoveAllListeners();
        if (exitButton != null) exitButton.onClick.RemoveAllListeners();
        if (skipTextButton != null) skipTextButton.onClick.RemoveAllListeners();
        if (toggleSubtitlesButton != null) toggleSubtitlesButton.onClick.RemoveListener(ToggleSubtitles); // 新增
    }

    // 新增方法：切换字幕可见性
    private void ToggleSubtitles()
    {
        areSubtitlesHidden = !areSubtitlesHidden;
        UpdateSubtitleButtonText();

        if (bodyText != null)
        {
            bodyText.gameObject.SetActive(!areSubtitlesHidden);
            if (!areSubtitlesHidden)
            {
                // 如果是重新显示字幕
                if (typewriterCoroutine != null)
                {
                    // 如果打字机还在运行（理论上不应该，因为隐藏时打字机不直接更新UI text但会跑完）
                    // 为了保险，直接显示完整文本
                    StopCoroutine(typewriterCoroutine);
                    typewriterCoroutine = null;
                    bodyText.text = currentFullPageText;
                    fullTextDisplayedForPage = true;
                }
                else if (!string.IsNullOrEmpty(currentFullPageText))
                {
                    // 如果打字机已结束或从未开始，显示当前页的完整文本
                    bodyText.text = currentFullPageText;
                }
                // 如果 currentFullPageText 为空，则 bodyText.text 已经通过 LoadPageContent 或 TypewriterEffect 初始化为空字符串
            }
        }
    }

    // 新增方法：更新字幕切换按钮的文本
    private void UpdateSubtitleButtonText()
    {
        if (toggleSubtitlesButtonText != null)
        {
            toggleSubtitlesButtonText.text = areSubtitlesHidden ? "显示字幕" : "隐藏字幕";
        }
    }
}
