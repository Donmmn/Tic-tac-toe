using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System; // Required for Action

public class CGCollectPage : MonoBehaviour
{
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private Transform cgListParent; // 用于放置CGTitleBox的父物体
    [SerializeField] private GameObject cgTitleBoxPrefab; // CGTitleBox的预制体

    [Header("Preview UI Elements")]
    [SerializeField] private Image cgImage; // Renamed from previewImage
    [SerializeField] private Text cgTitle;   // Renamed from previewTitleText
    [SerializeField] private Text Unlockcase; // Renamed from previewUnlockConditionText
    [SerializeField] private Button OpenCG;     // Renamed from playEndingButton
    [SerializeField] private GameObject previewPanelContainer; // Optional: A parent GameObject for all preview UI elements to easily show/hide

    private List<CGTitleBox> cgBoxes = new List<CGTitleBox>();
    private Ending currentSelectedEnding;
    private bool currentSelectedEndingUnlocked;

    // Start is called before the first frame update
    void Start()
    {
        if (backToMainMenuButton != null)
            backToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        else
            Debug.LogError("CGCollectPage: backToMainMenuButton 未分配！");

        if (OpenCG != null)
            OpenCG.onClick.AddListener(OnOpenCGButtonClicked);
        else
            Debug.LogError("CGCollectPage: OpenCG button 未分配！");

        // Initialize preview area (hide it initially)
        if (previewPanelContainer != null)
        {
            previewPanelContainer.SetActive(false);
        }
        else // Fallback if no container is assigned: manage individual elements
        {
            if (cgImage != null) cgImage.gameObject.SetActive(false);
            if (cgTitle != null) cgTitle.text = "";
            if (Unlockcase != null) Unlockcase.text = "";
            if (OpenCG != null) OpenCG.interactable = false;
        }
    }

    void OnEnable()
    {
        LoadCGList();
    }

    private void LoadCGList()
    {
        // Clear existing boxes and unsubscribe
        foreach (var box in cgBoxes)
        {
            if (box != null)
            {
                box.OnClicked -= OnCGBoxSelected; // Unsubscribe before destroying
                Destroy(box.gameObject);
            }
        }
        cgBoxes.Clear();

        // Fallback if the old destroy logic was preferred for some reason (e.g. other children in parent)
        // List<GameObject> toDelete = new List<GameObject>();
        // foreach (Transform child in cgListParent)
        // {
        //     CGTitleBox boxComponent = child.GetComponent<CGTitleBox>();
        //     if (boxComponent != null)
        //     {
        //         boxComponent.OnClicked -= OnCGBoxSelected;
        //         toDelete.Add(child.gameObject);
        //     }
        // }
        // foreach (var go in toDelete) Destroy(go);

        var playerProcess = PlayerProcess.Instance;
        if (playerProcess == null)
        {
            Debug.LogError("CGCollectPage: PlayerProcess.Instance is null!");
            return;
        }
        var unlockedIds = new HashSet<int>(playerProcess.UnlockedEndings);
        var allEndingsField = typeof(PlayerProcess).GetField("allEndings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var allEndings = allEndingsField?.GetValue(playerProcess) as List<Ending>;

        if (allEndings == null)
        {
            Debug.LogError("CGCollectPage: 无法获取结局数据 (allEndings is null).");
            return;
        }

        foreach (var ending in allEndings)
        {
            var boxObj = Instantiate(cgTitleBoxPrefab, cgListParent);
            var box = boxObj.GetComponent<CGTitleBox>();
            if (box != null)
            {
                bool unlocked = unlockedIds.Contains(ending.Id);
                string imgPath = (ending.ImagesName != null && ending.ImagesName.Length > 0) ? ending.ImagesName[0] : "";
                string title = unlocked ? ending.title : "???";
                box.Initialized(imgPath, unlocked, title, ending); // Pass the whole Ending object
                box.OnClicked += OnCGBoxSelected; // Subscribe to the click event
                cgBoxes.Add(box);
            }
            else
            {
                Debug.LogError("CGCollectPage: Instantiated CGTitleBox prefab is missing CGTitleBox component!");
                Destroy(boxObj); // Clean up unusable instance
            }
        }
    }

    private void OnCGBoxSelected(Ending selectedEnding, bool isUnlocked)
    {
        currentSelectedEnding = selectedEnding;
        currentSelectedEndingUnlocked = isUnlocked;

        if (previewPanelContainer != null)
        {
            previewPanelContainer.SetActive(true);
        }

        // 1. Set preview image
        if (cgImage != null)
        {
            if (isUnlocked && selectedEnding.ImagesName != null && selectedEnding.ImagesName.Length > 0 && !string.IsNullOrEmpty(selectedEnding.ImagesName[0]))
            {
                Sprite endingSprite = Resources.Load<Sprite>(selectedEnding.ImagesName[0]);
                if (endingSprite != null)
                {
                    cgImage.sprite = endingSprite;
                    cgImage.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"CGCollectPage: 无法从 Resources 加载图片: {selectedEnding.ImagesName[0]} for ending ID: {selectedEnding.Id}");
                    cgImage.gameObject.SetActive(false);
                }
            }
            else
            {
                cgImage.gameObject.SetActive(false); // Hide if locked or no image path
            }
        }

        // 2. Set title
        if (cgTitle != null)
        {
            cgTitle.text = isUnlocked ? selectedEnding.title : "???";
        }

        // 3. Set unlock conditions
        if (Unlockcase != null)
        {
            if (isUnlocked)
            {
                string winConditionText = selectedEnding.PlayerWin ? "玩家获胜" : "AI获胜"; // 您可以根据实际情况调整"AI获胜"的文本
                Unlockcase.text = $"解锁条件：\n心情值: {selectedEnding.MinScore} ~ {selectedEnding.MaxScore}\n要求: {winConditionText}";
            }
            else
            {
                Unlockcase.text = "解锁条件：？？？";
            }
        }

        // 4. Set "Open CG" button state
        if (OpenCG != null)
        {
            OpenCG.interactable = isUnlocked;
            // Ensure button is visible if a container isn't used
            if (previewPanelContainer == null) OpenCG.gameObject.SetActive(true);
        }
         // Ensure other text elements are visible if a container isn't used
        if (previewPanelContainer == null)
        {
            if(cgTitle != null) cgTitle.gameObject.SetActive(true); 
            if(Unlockcase != null) Unlockcase.gameObject.SetActive(true);
        }
    }

    private void OnOpenCGButtonClicked()
    {
        if (currentSelectedEnding != null && currentSelectedEndingUnlocked)
        {
            if (UIManager.Instance != null)
            {
                Debug.Log($"CGCollectPage: Requesting UIManager to play ending ID: {currentSelectedEnding.Id}");
                
                Action callbackToReopenCollectPage = () => {
                    if (this != null && gameObject != null) // 确保 CGCollectPage 实例仍然有效
                    {
                        // UIManager.HideAllCoreCanvases() 应该已被CGViewer的关闭流程或UIManager的ShowCGViewer的开头调用
                        // 我们只需要重新激活 CGCollectPage 的 Canvas
                        // UIManager.Instance.ShowCGCollectPage(); // 这个方法会隐藏其他所有Canvas，正是我们想要的
                        // 然而，如果 CGViewer 的关闭没有正确调用 HideAllCoreCanvases (例如，如果回调直接在 CGViewer 内部触发关闭)，
                        // 并且CGViewer实例被销毁了，直接调用ShowCGCollectPage是安全的。

                        Debug.Log("CGCollectPage: Callback executed. Re-showing CG Collect Page.");
                        if (UIManager.Instance != null) 
                        {
                            UIManager.Instance.ShowCGCollectPage();
                        }
                        else
                        {
                             Debug.LogError("CGCollectPage Callback: UIManager instance is null when trying to re-show collect page.");
                             // 作为后备，可以尝试手动激活，但这不是理想状态
                             // gameObject.SetActive(true); 
                        }
                    }
                    else
                    {
                        Debug.LogWarning("CGCollectPage: Callback to reopen collect page was called, but the page instance is no longer valid.");
                    }
                };

                UIManager.Instance.ShowCGViewer(currentSelectedEnding.Id, callbackToReopenCollectPage);
            }
            else
            {
                Debug.LogError("CGCollectPage: UIManager.Instance is null! Cannot play CG.");
            }
        }
        else if (currentSelectedEnding == null)
        {
            Debug.LogWarning("CGCollectPage: No ending selected to play.");
        }
        else if (!currentSelectedEndingUnlocked)
        {
            Debug.LogWarning("CGCollectPage: Selected ending is locked. Cannot play CG.");
        }
    }

    private void ReturnToMainMenu()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMainMenu();
        else
            Debug.LogError("CGCollectPage: UIManager.Instance 未找到！");
    }

    void OnDestroy()
    {
        if (backToMainMenuButton != null)
            backToMainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        if (OpenCG != null)
            OpenCG.onClick.RemoveListener(OnOpenCGButtonClicked);

        foreach (var box in cgBoxes)
        {
            if (box != null) // Ensure box instance is still valid
            {
                box.OnClicked -= OnCGBoxSelected; // Unsubscribe
            }
        }
        // cgBoxes list will be cleared if this object is destroyed and re-enabled, 
        // but good practice to clear if managing manually elsewhere.
    }
}
