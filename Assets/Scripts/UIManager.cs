using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI; // Required for Sprite if passed as argument

public class UIManager : MonoSingleton<UIManager>
{
    [Header("Core UI Canvases")]
    [Tooltip("主菜单界面的Canvas")][SerializeField] private GameObject mainMenuCanvas;
    [Tooltip("游戏棋盘界面的Canvas")][SerializeField] private GameObject gameplayCanvas;
    [Tooltip("结局CG播放界面的预制件")][SerializeField] private GameObject cgViewerCanvasPrefab;
    [Tooltip("设置界面的Canvas")][SerializeField] private GameObject settingsCanvas; // 新增设置页面引用
    [Tooltip("回忆收集页面Canvas")][SerializeField] private GameObject cgCollectPageCanvas; // 新增

    private GameObject currentCGViewerInstance;

    [Header("Popup System Settings")] // 新增：弹窗系统相关设置
    [Tooltip("用于所有弹窗的PopupWindow预制件")][SerializeField] private GameObject popupWindowPrefab;

    protected void Awake()
    {
        // 启动时默认显示主菜单，隐藏其他 (确保场景初始状态符合预期)
        // 如果场景中已手动设置好初始状态，可以注释掉下面这行，或根据需要调整
        // ShowMainMenu(); 
    }

    private void HideAllCoreCanvases()
    {
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);
        if (gameplayCanvas != null) gameplayCanvas.SetActive(false);
        if (currentCGViewerInstance != null)
        {
            Destroy(currentCGViewerInstance);
            currentCGViewerInstance = null;
        }
        if (settingsCanvas != null) settingsCanvas.SetActive(false); // 新增：隐藏设置页面
        if (cgCollectPageCanvas != null) cgCollectPageCanvas.SetActive(false); // 新增
    }

    public void ShowMainMenu()
    {     
        // 新增：尝试重置棋盘和分数
        boardState gameBoard = gameplayCanvas.GetComponentInChildren<boardState>();
        if (gameBoard != null)
        {
            Debug.Log("UIManager: Resetting board and scores before showing main menu.");
            gameBoard.ResetBoard();
            gameBoard.ResetScores();
        }
        else
        {
            Debug.LogWarning("UIManager: boardState instance not found. Cannot reset board and scores. This might be normal if game scene is not loaded.");
        }
        HideAllCoreCanvases();

        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
        else Debug.LogError("UIManager: MainMenuCanvas is not assigned!");
    }

    public void ShowGameplay()
    {
        HideAllCoreCanvases();
        if (gameplayCanvas != null) gameplayCanvas.SetActive(true);
        else Debug.LogError("UIManager: GameplayCanvas is not assigned!");
    }

    public void ShowCGViewer(int endingId, Action onViewerClosedCallback = null)
    {
        HideAllCoreCanvases();

        if (cgViewerCanvasPrefab == null)
        {
            Debug.LogError("UIManager: cgViewerCanvasPrefab is not assigned! Cannot show CG viewer.");
            // 如果有回调，也应该执行，或者决定在这种错误情况下如何处理回调
            onViewerClosedCallback?.Invoke(); 
            // 考虑是否应该总是尝试显示主菜单，或者如果回调存在则依赖回调处理后续
            // ShowMainMenu(); 
            return;
        }

        currentCGViewerInstance = Instantiate(cgViewerCanvasPrefab);
        if (currentCGViewerInstance != null)
        {
            CGviewer cgViewerComponent = currentCGViewerInstance.GetComponent<CGviewer>();
            if (cgViewerComponent != null)
            {
                cgViewerComponent.DisplayEnding(endingId);
                if (onViewerClosedCallback != null)
                {
                    cgViewerComponent.SetOnCloseCallback(onViewerClosedCallback); 
                }
            }
            else
            {
                Debug.LogError("UIManager: cgViewerCanvasPrefab is missing CGviewer component! Cannot display ending CG.");
                Destroy(currentCGViewerInstance);
                currentCGViewerInstance = null;
                onViewerClosedCallback?.Invoke(); // 同样，处理回调
                // ShowMainMenu(); 
            }
        }
        else
        {
            Debug.LogError("UIManager: Failed to instantiate cgViewerCanvasPrefab!");
            onViewerClosedCallback?.Invoke(); // 处理回调
            // ShowMainMenu(); 
        }
    }

    public void ShowSettings()
    {
        HideAllCoreCanvases();
        if (settingsCanvas != null) settingsCanvas.SetActive(true);
        else Debug.LogError("UIManager: SettingsCanvas is not assigned!");
    }

    public void ShowCGCollectPage()
    {
        HideAllCoreCanvases();
        if (cgCollectPageCanvas != null) cgCollectPageCanvas.SetActive(true);
        else Debug.LogError("UIManager: CGCollectPageCanvas is not assigned!");
    }

    // 可以在游戏启动时由一个初始化脚本调用，以确保UI处于正确的初始状态
    public void InitializeDefaultUIState()
    {
        ShowMainMenu();
    }

    // --- 新增：弹窗显示方法 ---

    /// <summary>
    /// 显示一个单页弹窗。
    /// </summary>
    /// <returns>创建的 PopupWindows 实例，如果创建失败则返回null。</returns>
    public PopupWindows ShowSinglePagePopup(
        string message,
        string button1Text = null, Action button1Action = null,
        string button2Text = null, Action button2Action = null,
        Sprite imageSprite = null, 
        bool isIgnorable = false, Action onIgnoreAction = null, string ignoreButtonText = "不再提示")
    {
        if (popupWindowPrefab == null)
        {
            Debug.LogError("UIManager: popupWindowPrefab is not assigned! Cannot show single page popup.");
            return null;
        }

        GameObject popupGO = Instantiate(popupWindowPrefab);
        PopupWindows popupInstance = popupGO.GetComponent<PopupWindows>();

        if (popupInstance != null)
        {
            popupInstance.Initialize(
                message,
                imageSprite,
                button1Text, button1Action,
                button2Text, button2Action,
                isIgnorable, onIgnoreAction, ignoreButtonText
            );
            popupInstance.Show();
            return popupInstance;
        }
        else
        {
            Debug.LogError("UIManager: Instantiated popupWindowPrefab is missing PopupWindows component!");
            if (popupGO != null) Destroy(popupGO);
            return null;
        }
    }

    /// <summary>
    /// 显示一个多页图文弹窗（例如教程）。
    /// </summary>
    /// <returns>创建的 PopupWindows 实例，如果创建失败则返回null。</returns>
    public PopupWindows ShowMultiPagePopup(
        string[] messages,
        string[] imageNames = null,
        bool isIgnorableOnFinalPage = false, 
        Action onIgnoredCallback = null, 
        string customIgnoreButtonText = "不再提示")
    {
        if (popupWindowPrefab == null)
        {
            Debug.LogError("UIManager: popupWindowPrefab is not assigned! Cannot show multi-page popup.");
            return null;
        }

        GameObject popupGO = Instantiate(popupWindowPrefab);
        PopupWindows popupInstance = popupGO.GetComponent<PopupWindows>();

        if (popupInstance != null)
        {
            popupInstance.Initialize(
                messages,
                imageNames,
                isIgnorableOnFinalPage,
                onIgnoredCallback,
                customIgnoreButtonText
            );
            popupInstance.Show();
            return popupInstance;
        }
        else
        {
            Debug.LogError("UIManager: Instantiated popupWindowPrefab is missing PopupWindows component!");
            if (popupGO != null) Destroy(popupGO);
            return null;
        }
    }
}
