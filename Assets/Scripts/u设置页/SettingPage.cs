using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingPage : MonoBehaviour
{
    [SerializeField] Button ResetData;
    [SerializeField] Button BackToMainMenu;
    [SerializeField] Button ResetTutorial;
    [SerializeField] Button UnlockAllCG;

    [SerializeField] Slider BGMv;
    [SerializeField] Slider SoundEfv;

    [SerializeField] InputField UnlockCode;
    [SerializeField] Button UnlockByCodeButton;

    void Start()
    {
        // 初始化按钮监听
        if (ResetData != null)
            ResetData.onClick.AddListener(OnResetDataClick);
        
        if (BackToMainMenu != null)
            BackToMainMenu.onClick.AddListener(OnBackToMainMenuClick);
        
        if (ResetTutorial != null)
            ResetTutorial.onClick.AddListener(OnResetTutorialClick);
        
        if (UnlockAllCG != null)
            UnlockAllCG.onClick.AddListener(OnUnlockAllCGClick);

        // 初始化音量滑块
        if (BGMv != null)
        {
            BGMv.value = AudioManager.Instance.BGMVolume;
            BGMv.onValueChanged.AddListener(OnBGMVolumeChanged);
        }
        
        if (SoundEfv != null)
        {
            SoundEfv.value = AudioManager.Instance.SFXVolume;
            SoundEfv.onValueChanged.AddListener(OnSoundEffectVolumeChanged);
        }

        // 新增：为解锁回忆按钮添加监听
        if (UnlockByCodeButton != null)
            UnlockByCodeButton.onClick.AddListener(OnUnlockByCodeClick);
    }

    void OnDestroy()
    {
        // 移除按钮监听
        if (ResetData != null)
            ResetData.onClick.RemoveListener(OnResetDataClick);
        
        if (BackToMainMenu != null)
            BackToMainMenu.onClick.RemoveListener(OnBackToMainMenuClick);
        
        if (ResetTutorial != null)
            ResetTutorial.onClick.RemoveListener(OnResetTutorialClick);
        
        if (UnlockAllCG != null)
            UnlockAllCG.onClick.RemoveListener(OnUnlockAllCGClick);

        // 移除音量滑块监听
        if (BGMv != null)
            BGMv.onValueChanged.RemoveListener(OnBGMVolumeChanged);
        
        if (SoundEfv != null)
            SoundEfv.onValueChanged.RemoveListener(OnSoundEffectVolumeChanged);
        
        // 新增：移除解锁回忆按钮的监听
        if (UnlockByCodeButton != null)
            UnlockByCodeButton.onClick.RemoveListener(OnUnlockByCodeClick);
    }

    private void OnResetDataClick()
    {
        // 显示确认弹窗
        UIManager.Instance.ShowSinglePagePopup(
            message: "确定要重置所有数据吗？\n此操作将清空所有结局解锁状态和教程完成状态，且不可恢复！",
            button1Text: "确定",
            button1Action: () => {
                if (PlayerProcess.Instance != null)
                {
                    PlayerProcess.Instance.ResetAllData();
                    Debug.Log("设置页面：已重置所有数据");
                }
                else
                {
                    Debug.LogError("设置页面：PlayerProcess.Instance 为空，无法重置数据");
                }
            },
            button2Text: "取消"
        );
    }

    private void OnBackToMainMenuClick()
    {
        UIManager.Instance.ShowMainMenu();
    }

    private void OnResetTutorialClick()
    {
        if (PlayerProcess.Instance != null)
        {
            PlayerProcess.Instance.ResetTutorialStatus();
            Debug.Log("设置页面：已重置教程状态");
        }
        else
        {
            Debug.LogError("设置页面：PlayerProcess.Instance 为空，无法重置教程状态");
        }
    }

    private void OnUnlockAllCGClick()
    {
        if (PlayerProcess.Instance != null)
        {
            PlayerProcess.Instance.UnlockAllEndings();
            Debug.Log("设置页面：已解锁所有CG");
        }
        else
        {
            Debug.LogError("设置页面：PlayerProcess.Instance 为空，无法解锁CG");
        }
    }

    private void OnUnlockByCodeClick()
    {
        if (UnlockCode == null || string.IsNullOrEmpty(UnlockCode.text))
        {
            UIManager.Instance.ShowSinglePagePopup("请输入解锁码。", "确定");
            return;
        }

        if (PlayerProcess.Instance != null)
        {
            bool success = PlayerProcess.Instance.VerifyAndUnlockEndingsByCode(UnlockCode.text);
            if (success)
            {
                UIManager.Instance.ShowSinglePagePopup("解锁码正确！所有回忆已解锁。", "太棒了！");
                UnlockCode.text = ""; // 清空输入框
            }
            else
            {
                UIManager.Instance.ShowSinglePagePopup("解锁码错误或未设置。", "知道了");
            }
        }
        else
        {
            Debug.LogError("设置页面：PlayerProcess.Instance 为空，无法通过代码解锁CG");
            UIManager.Instance.ShowSinglePagePopup("发生错误，无法处理解锁请求。", "确定");
        }
    }

    private void OnBGMVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(value);
            Debug.Log($"BGM音量已调整为: {value}");
        }
        else
        {
            Debug.LogError("设置页面：AudioManager.Instance 为空，无法调整BGM音量");
        }
    }

    private void OnSoundEffectVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
            Debug.Log($"音效音量已调整为: {value}");
        }
        else
        {
            Debug.LogError("设置页面：AudioManager.Instance 为空，无法调整音效音量");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
