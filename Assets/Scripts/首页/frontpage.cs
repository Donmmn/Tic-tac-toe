using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 引用UI命名空间
using System;
using System.Linq; // 确保 Linq 在作用域内

public class frontpage : MonoBehaviour
{
    [Header("UI Buttons")]
    public Button startGameButton;
    public Button settingsButton;
    public Button exitGameButton;
    public Button viewCGButton; // 新增：查看回忆按钮

    [Header("Canvas to Hide")]
    [Tooltip("将此脚本所在的Canvas拖拽到这里")]
    public GameObject mainMenuCanvas; // 用于隐藏主菜单的画布

    private const int MainTutorialId = 1; // 定义主教程ID

    void Start()
    {
        // 为按钮添加监听器
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(StartGame);
        }
        else
        {
            Debug.LogError("StartGameButton 未在 Inspector 中分配！");
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OpenSettings);
        }
        else
        {
            Debug.LogError("SettingsButton 未在 Inspector 中分配！");
        }

        if (exitGameButton != null)
        {
            exitGameButton.onClick.AddListener(ExitGame);
        }
        else
        {
            Debug.LogError("ExitGameButton 未在 Inspector 中分配！");
        }

        if (viewCGButton != null)
            viewCGButton.onClick.AddListener(OpenCGCollectPage);
        else
            Debug.LogError("viewCGButton 未在 Inspector 中分配！");

        // 确保画布在开始时是可见的（如果它在编辑器中被意外隐藏）
        if (mainMenuCanvas != null && !mainMenuCanvas.activeSelf)
        {
            // mainMenuCanvas.SetActive(true); // 根据需要取消注释，如果希望脚本控制初始状态
        }
        else if (mainMenuCanvas == null)
        {
             Debug.LogError("MainMenuCanvas 未在 Inspector 中分配！隐藏功能将无法工作。");
        }
    }

    public void StartGame()
    {
        PlayButtonClickSound();
        ShowMainTutorial();
    }

    void ShowMainTutorial()
    {
        // 检查此教程是否已完成
        if (PlayerProcess.Instance != null && System.Linq.Enumerable.Contains(PlayerProcess.Instance.CompletedTutorials, MainTutorialId))
        {
            Debug.Log($"教程 {MainTutorialId} 已完成，直接开始游戏。");
            ProceedToGame();
            return;
        }
        Tutorial tutorialData = PlayerProcess.Instance.GetTutorialById(MainTutorialId);

        if (tutorialData == null)
        {
            Debug.LogWarning($"未能获取ID为 {MainTutorialId} 的教程数据，直接开始游戏。");
            ProceedToGame();
            return;
        }

        Action onTutorialCompleteOrSkipped = () => {
            if (PlayerProcess.Instance != null) 
            {
                PlayerProcess.Instance.MarkTutorialAsCompleted(MainTutorialId);
            }
            ProceedToGame();
        };
        
        PopupWindows popupInstance = UIManager.Instance.ShowMultiPagePopup(
            messages: tutorialData.TutorialText,
            imageNames: tutorialData.ImageNames,
            isIgnorableOnFinalPage: true, 
            onIgnoredCallback: onTutorialCompleteOrSkipped, 
            customIgnoreButtonText: "继续"
        );

        if (popupInstance == null)
        {
            Debug.LogError("无法通过 UIManager 创建教程弹窗！直接开始游戏。");
            ProceedToGame();
        }
    }

    private void ProceedToGame()
    {
        Debug.Log("开始游戏逻辑执行！"); // 更新日志信息
        
        // 通过 UIManager 显示游戏界面
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameplay();
        }
        else
        {
            Debug.LogError("UIManager.Instance 为空，无法切换到游戏界面！请确保UIManager场景中存在且已初始化。");
            // 作为后备，可以尝试直接隐藏当前 mainMenuCanvas，但这不是理想做法
            if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);
        }

        // UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene"); // 如果需要加载新场景
    }

    public void OpenSettings()
    {
        PlayButtonClickSound();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowSettings();
            Debug.Log("已打开设置页面");
        }
        else
        {
            Debug.LogError("UIManager.Instance 为空，无法打开设置页面！请确保UIManager场景中存在且已初始化。");
        }
    }

    public void ExitGame()
    {
        PlayButtonClickSound();
        Debug.Log("退出游戏按钮被按下！");
        // 如果在Unity编辑器中运行
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 如果是构建后的游戏
        Application.Quit();
#endif
    }

    public void OpenCGCollectPage()
    {
        PlayButtonClickSound();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowCGCollectPage();
            Debug.Log("已打开回忆收集页面");
        }
        else
        {
            Debug.LogError("UIManager.Instance 为空，无法打开回忆收集页面！");
        }
    }

    private void PlayButtonClickSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClickSound();
        }
        else
        {
            Debug.LogWarning("AudioManager.Instance 为空，无法播放按钮音效");
        }
    }

    void OnDestroy()
    {
        // 移除监听器以防止内存泄漏
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(StartGame);
        }
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
        }
        if (exitGameButton != null)
        {
            exitGameButton.onClick.RemoveListener(ExitGame);
        }
        if (viewCGButton != null)
        {
            viewCGButton.onClick.RemoveListener(OpenCGCollectPage);
        }
    }
}
