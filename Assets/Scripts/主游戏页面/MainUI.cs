using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Required for Button

public class MainUI : MonoBehaviour
{
    [Header("UI Pages")]
    [Tooltip("拖拽登录页面的GameObject到这里")]
    public GameObject loginPage;

    [Header("Navigation Buttons")] // 新增
    [Tooltip("从当前UI返回到主菜单(frontpage)的按钮")] // 新增
    [SerializeField] private Button backToFrontPageButton; // 新增

    // Start is called before the first frame update
    void Start()
    {
        // 确保登录页面初始状态根据需要设置，例如初始隐藏
        // if (loginPage != null && loginPage.activeSelf)
        // {
        //     loginPage.SetActive(false);
        // }

        // 为返回主菜单按钮添加监听器
        if (backToFrontPageButton != null)
        {
            backToFrontPageButton.onClick.AddListener(GoToFrontPageViaManager);
        }
        else
        {
            // 如果你的MainUI设计中不一定总有这个按钮，可以将此改为Debug.Log
            Debug.LogWarning("MainUI: backToFrontPageButton 未在 Inspector 中分配。"); 
        }
    }

    // 新增：通过UIManager返回主菜单(frontpage)的方法
    public void GoToFrontPageViaManager()
    {
        Debug.Log("MainUI: Attempting to return to front page (main menu) via UIManager.");
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMainMenu();
        }
        else
        {
            Debug.LogError("MainUI: UIManager.Instance not found! Cannot return to front page.");
        }
    }

    // 新增：在销毁时移除监听器
    void OnDestroy()
    {
        if (backToFrontPageButton != null)
        {
            backToFrontPageButton.onClick.RemoveListener(GoToFrontPageViaManager);
        }
    }
}
