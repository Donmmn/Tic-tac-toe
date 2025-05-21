using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 添加对 UI 系统的引用
using System; // 添加对 Action 的引用

public class PopupWindows : MonoBehaviour
{
    [Header("UI 元素")]
    [SerializeField] private Text messageText; // 用于显示文本 (修改为 Text)
    [SerializeField] private Image displayImage;       // 用于显示图片
    [SerializeField] private Image BlurImage;          // 新增：用于背景模糊的Image
    [SerializeField] private Button closeButton;        // 关闭按钮
    [SerializeField] private Button functionButton1;    // 功能按钮1
    [SerializeField] private Text functionButton1Text; // 功能按钮1的文本
    [SerializeField] private Button functionButton2;    // 功能按钮2
    [SerializeField] private Text functionButton2Text; // 功能按钮2的文本
    [SerializeField] private Button ignoreButton; // 新增：忽略按钮 (之前可能是 functionButton3)
    [SerializeField] private Text ignoreButtonText; // 新增：忽略按钮的文本

    [Header("Scaling Settings")]
    [Tooltip("设计弹窗UI时的参考屏幕高度。弹窗将根据当前屏幕高度与此值的比例进行缩放。")]
    [SerializeField] private float referenceScreenHeight = 1080f;

    private Action onFunctionButton1Click;
    private Action onFunctionButton2Click;
    private Action onIgnoreButtonClick; // 新增：忽略按钮的回调

    // 字段用于多页弹窗
    private string[] currentMessages;
    private string[] currentImageNames;
    private int currentPageIndex;
    private bool isMultiPagePopup = false;
    private bool isMultiPageIgnorable;
    private Action onMultiPageIgnoreAction;
    private string multiPageIgnoreButtonTextContent = "不再提示";

    void OnEnable() // 新增 OnEnable 方法
    {
        if (BlurImage != null)
        {
            bool assignMaterial = false;
            if (BlurImage.material == null)
            {
                assignMaterial = true;
                Debug.Log("PopupWindows: BlurImage material is null, attempting to load BlurEffect.");
            }
            // 如果材质名不是 "BlurEffect" (注意，实例化的材质名后面可能会有 " (Instance)")
            // 为了确保我们总是使用项目中的原始共享材质，或者如果当前材质就是默认UI材质，我们也需要设置它。
            // 直接比较材质名称 "BlurEffect" 来决定是否加载。
            else if (BlurImage.material.name != "BlurEffect")
            {
                assignMaterial = true;
                Debug.Log($"PopupWindows: BlurImage material is '{BlurImage.material.name}', attempting to load BlurEffect.");
            }

            if (assignMaterial)
            {
                Material blurEffectMaterial = Resources.Load<Material>("Material/BlurEffect");
                if (blurEffectMaterial != null)
                {
                    BlurImage.material = blurEffectMaterial;
                    Debug.Log("PopupWindows: BlurImage material successfully set to BlurEffect.");
                }
                else
                {
                    Debug.LogError("PopupWindows: Failed to load Material 'Material/BlurEffect' from Resources. Please ensure it exists at the correct path and is not corrupted.");
                }
            }
            // 如果 assignMaterial 为 false，则表示材质已经是 BlurEffect，无需操作。
        }
        // else
        // {
        //     Debug.LogWarning("PopupWindows: BlurImage is not assigned in the Inspector. Cannot apply blur effect material logic.");
        // }
    }

    void Awake() // 使用 Awake 确保在 Start 之前初始化
    {
        // 为按钮添加监听器
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (functionButton1 != null)
        {
            functionButton1.onClick.AddListener(OnFunctionButton1Clicked);
            functionButton1.onClick.AddListener(ClosePopup); // 默认加上关闭
        }

        if (functionButton2 != null)
        {
            functionButton2.onClick.AddListener(OnFunctionButton2Clicked);
            functionButton2.onClick.AddListener(ClosePopup); // 默认加上关闭
        }

        if (ignoreButton != null) // 新增
        {
            ignoreButton.onClick.AddListener(OnIgnoreButtonClicked);
        }
    }
    private void SetMessageAndImage(string textContent, string imageName = null)
    {
        if (messageText != null)
        {
            messageText.text = textContent;
        }

        if (displayImage != null)
        {
            if (!string.IsNullOrEmpty(imageName))
            {
                Sprite newSprite = Resources.Load<Sprite>(imageName);
                if (newSprite != null)
                {
                    displayImage.sprite = newSprite;
                    displayImage.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"SetMessageAndImage: 无法从 Resources/{imageName} 加载图片。图片将不会显示。");
                    displayImage.sprite = null;
                    displayImage.gameObject.SetActive(false);
                }
            }
            else
            {
                displayImage.sprite = null;
                displayImage.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 初始化弹窗内容 (单页)
    /// </summary>
    /// <param name="message">要显示的文本信息</param>
    /// <param name="imageSprite">要显示的图片 (可选)</param>
    /// <param name="button1Text">功能按钮1的文本 (可选, 为 null 则隐藏按钮)</param>
    /// <param name="button1Action">功能按钮1点击时执行的动作 (可选)</param>
    /// <param name="button2Text">功能按钮2的文本 (可选, 为 null 则隐藏按钮)</param>
    /// <param name="button2Action">功能按钮2点击时执行的动作 (可选)</param>
    /// <param name="isIgnorable">此弹窗是否可被用户忽略 (可选, 默认为 false)</param>
    /// <param name="onIgnoreAction">当忽略按钮被点击时执行的动作 (可选)</param>
    /// <param name="ignoreButtonTextContent">忽略按钮上显示的文本 (可选, 默认为 "不再提示")</param>
    public void Initialize(
        string message,
        Sprite imageSprite = null,
        string button1Text = null,
        Action button1Action = null,
        string button2Text = null,
        Action button2Action = null,
        bool isIgnorable = false,         // 新增参数
        Action onIgnoreAction = null,     // 新增参数
        string ignoreButtonTextContent = "不再提示" // 新增参数
        )
    {
        this.isMultiPagePopup = false; // 标记为单页弹窗

        SetMessageAndImage(message, null); // 先用 SetMessageAndImage 处理文本和 imageName (sprite 由外部传入)
        if (displayImage != null && imageSprite != null) // 单独处理 Sprite
            {
                displayImage.sprite = imageSprite;
                displayImage.gameObject.SetActive(true);
        } else if (displayImage != null)
            {
                displayImage.gameObject.SetActive(false);
        }

        // 设置功能按钮1
        this.onFunctionButton1Click = button1Action;
        if (functionButton1 != null)
        {
            if (!string.IsNullOrEmpty(button1Text))
            {
                if(functionButton1Text != null) functionButton1Text.text = button1Text;
                functionButton1.gameObject.SetActive(true);
            }
            else
            {
                functionButton1.gameObject.SetActive(false);
            }
        }

        // 设置功能按钮2
        this.onFunctionButton2Click = button2Action;
        if (functionButton2 != null)
        {
            if (!string.IsNullOrEmpty(button2Text))
            {
                if(functionButton2Text != null) functionButton2Text.text = button2Text;
                functionButton2.gameObject.SetActive(true);
            }
            else
            {
                functionButton2.gameObject.SetActive(false);
            }
        }

        // 设置忽略按钮 (新增)
        this.onIgnoreButtonClick = onIgnoreAction;
        if (ignoreButton != null) 
        {
            if (isIgnorable)
            {
                if(ignoreButtonText != null) ignoreButtonText.text = ignoreButtonTextContent;
                ignoreButton.gameObject.SetActive(true);
            }
            else
            {
                ignoreButton.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 初始化弹窗内容 (多页)
    /// </summary>
    /// <param name="messages">要显示的文本信息数组</param>
    /// <param name="imageNames">要显示的图片名称数组 (在 Resources/Images/ 目录下，可选，可为null或长度与messages不同)</param>
    /// <param name="isIgnorable">此弹窗是否可被用户忽略 (可选, 默认为 false)</param>
    /// <param name="onIgnoreAction">当忽略按钮被点击时执行的动作 (可选, 仅在最后一页显示时有效)</param>
    /// <param name="customIgnoreButtonText">忽略按钮上显示的文本 (可选, 默认为 "不再提示")</param>
    public void Initialize(
        string[] messages,
        string[] imageNames = null,
        bool isIgnorable = false,
        Action onIgnoreAction = null,
        string customIgnoreButtonText = "不再提示")
    {
        if (messages == null || messages.Length == 0)
        {
            Debug.LogError("Initialize (multi-page): messages array cannot be null or empty.");
            return;
        }

        this.isMultiPagePopup = true;
        this.currentMessages = messages;
        this.currentImageNames = imageNames;
        this.currentPageIndex = 0;
        this.isMultiPageIgnorable = isIgnorable;
        this.onMultiPageIgnoreAction = onIgnoreAction;
        this.multiPageIgnoreButtonTextContent = customIgnoreButtonText;

        // 清除单页按钮的特定回调，因为多页模式将控制它们
        this.onFunctionButton1Click = GoToPreviousPage;
        this.onFunctionButton2Click = GoToNextPage;
        // onIgnoreButtonClick 会在 ShowPage 中根据当前页和 isMultiPageIgnorable 条件设置

        // 重点：移除自动关闭监听
        if (functionButton1 != null)
            functionButton1.onClick.RemoveListener(ClosePopup);
        if (functionButton2 != null)
            functionButton2.onClick.RemoveListener(ClosePopup);

        ShowPage(this.currentPageIndex);
    }

    private void ShowPage(int index)
    {
        if (!isMultiPagePopup || currentMessages == null || index < 0 || index >= currentMessages.Length)
        {
            //Debug.LogError($"ShowPage: Invalid page index {index} or not in multi-page mode.");
            return;
        }
        currentPageIndex = index;

        string msg = currentMessages[currentPageIndex];
        string imgName = (currentImageNames != null && currentPageIndex < currentImageNames.Length) ? currentImageNames[currentPageIndex] : null;

        SetMessageAndImage(msg, imgName);

        // 配置导航按钮
        if (functionButton1 != null)
        {
            bool showPrev = currentPageIndex > 0;
            functionButton1.gameObject.SetActive(showPrev);
            if (showPrev && functionButton1Text != null)
            {
                functionButton1Text.text = "上一页";
            }
        }

        if (functionButton2 != null)
        {
            bool showNext = currentPageIndex < currentMessages.Length - 1;
            functionButton2.gameObject.SetActive(showNext);
            if (showNext && functionButton2Text != null)
            {
                functionButton2Text.text = "下一页";
            }
        }
        
        // 配置忽略按钮
        if (ignoreButton != null)
        {
            bool showIgnoreThisPage = isMultiPageIgnorable && currentPageIndex == currentMessages.Length - 1;
            ignoreButton.gameObject.SetActive(showIgnoreThisPage);
            if (showIgnoreThisPage)
            {
                if (ignoreButtonText != null) ignoreButtonText.text = multiPageIgnoreButtonTextContent;
                this.onIgnoreButtonClick = this.onMultiPageIgnoreAction;
            }
            else
            {
                 // 在多页模式下，如果不是最后一页的可忽略状态，则清除忽略按钮的回调
                this.onIgnoreButtonClick = null;
            }
        }
    }

    private void GoToPreviousPage()
    {
        if (isMultiPagePopup && currentPageIndex > 0)
        {
            ShowPage(currentPageIndex - 1);
        }
    }

    private void GoToNextPage()
    {
        if (isMultiPagePopup && currentMessages != null && currentPageIndex < currentMessages.Length - 1)
        {
            ShowPage(currentPageIndex + 1);
        }
    }

    /// <summary>
    /// 刷新弹窗内容
    /// </summary>
    /// <param name="newMessage">新的文本信息，不能为空</param>
    /// <param name="imageName">新的图片名称 (在 Resources/Images/ 目录下，可选)</param>
    public void RefreshContent(string newMessage, string imageName = null)
    {
        if (string.IsNullOrEmpty(newMessage))
        {
            Debug.LogError("RefreshContent: newMessage 不能为空。");
            return;
        }
        SetMessageAndImage(newMessage, imageName);
        // 注意: 此刷新不改变按钮配置。
        // 如果在多页弹窗上调用，它仅更改当前视图，
        // 导航离开再返回将恢复页面的原始内容。
    }

    /// <summary>
    /// 显示弹窗
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 关闭/隐藏弹窗
    /// </summary>
    public void ClosePopup()
    {
        // 尝试保存玩家进度
        if (PlayerProcess.Instance != null)
        {
            PlayerProcess.Instance.SavePlayerProgress();
            Debug.Log("PopupWindows: 尝试在弹窗关闭时保存玩家进度。");
        }
        else
        {
            Debug.LogWarning("PopupWindows: PlayerProcess.Instance 未找到，无法在弹窗关闭时保存进度。");
        }

        //gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnFunctionButton1Clicked()
    {
        onFunctionButton1Click?.Invoke();
    }

    private void OnFunctionButton2Clicked()
    {
        onFunctionButton2Click?.Invoke();
    }

    private void OnIgnoreButtonClicked() // 新增
    {
        onIgnoreButtonClick?.Invoke();
        ClosePopup();
    }

    void OnDestroy() // 清理监听器，防止内存泄漏
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
        }
        if (functionButton1 != null)
        {
            functionButton1.onClick.RemoveListener(OnFunctionButton1Clicked);
        }
        if (functionButton2 != null)
        {
            functionButton2.onClick.RemoveListener(OnFunctionButton2Clicked);
        }
        if (ignoreButton != null) // 新增
        {
            ignoreButton.onClick.RemoveListener(OnIgnoreButtonClicked);
        }
    }
}
