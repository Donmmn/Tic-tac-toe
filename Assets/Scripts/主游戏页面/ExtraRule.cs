using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExtraRule
{
    private boardState boardController; // 对棋盘控制脚本的引用
    private boardState.State aiPlayerSide = boardState.State.O; // 假设AI是O，可以做得更通用

    // 你之后会手动填充这个数组
    private static readonly string[] ruleDisplayNames = new string[] 
    {
        "默认规则", // 对应 RuleType 0
        "规则1: 占据中心特殊效果", // 对应 RuleType 1
        // "规则2: ..." // 对应 RuleType 2, 以此类推
    };

    // 构造函数，用于从boardState获取引用
    public ExtraRule(boardState controller)
    {
        this.boardController = controller;
        // 如果需要，可以从boardController或AI脚本获取AI是哪一方
        // this.aiPlayerSide = controller.GetAISideSomehow(); 
    }

    // 辅助方法，用于外部（如boardState）判断一个State是否是AI方
    public boardState.State GetAISide() 
    {
        return aiPlayerSide; 
    }

    public static string[] GetRuleDisplayNames()
    {
        return ruleDisplayNames;
    }

    // 返回实际的规则类型值，因为数组索引从0开始，而你的RuleType 0是默认规则
    // Dropdown的选项索引会从0开始对应ruleDisplayNames的每一项
    // 这个方法将Dropdown的选项索引转换为实际的RuleType值
    public static int GetRuleTypeFromDropdownIndex(int dropdownIndex)
    {
        // 假设 ruleDisplayNames[0] 对应 RuleType 0 (默认规则)
        // ruleDisplayNames[1] 对应 RuleType 1 (第一个额外规则)
        // ...以此类推
        return dropdownIndex; 
    }

    /// <summary>
    /// 处理任何一方（玩家或AI）行动后的额外规则。
    /// </summary>
    /// <param name="moveIndex">落子的位置索引</param>
    /// <param name="playerWhoMoved">执行操作的一方（X或O）</param>
    /// <param name="ruleType">当前启用的规则类型</param>
    /// <returns>如果额外规则导致了明确的胜者，则返回胜者State (X或O)。如果额外规则被触发但未决出胜负（例如只是改变状态或回合），或者规则未被触发，则返回State.Empty，让标准逻辑继续。</returns>
    public boardState.State ProcessAction(int moveIndex, boardState.State playerWhoMoved, int ruleType)
    {
        if (ruleType == 0) return boardState.State.Empty; // 没有额外规则
        if (ruleType >= ruleDisplayNames.Length || ruleType < 0) {
             Debug.LogWarning($"请求了无效的规则类型: {ruleType}，将使用默认规则。");
             return boardState.State.Empty;
        }

        Debug.Log($"额外规则检测：{playerWhoMoved} 在格子 {moveIndex} 落子，规则类型: {ruleType} ({ruleDisplayNames[ruleType]})");

        switch (ruleType)
        {
            case 1:
                // 示例规则1：如果任何一方在中间格子(4)落子
                if (moveIndex == 4) 
                {
                    Debug.Log($"规则 {ruleDisplayNames[ruleType]} 触发：{playerWhoMoved} 占据中心。");
                    // 示例：如果规则是占据中心直接获胜
                    // return playerWhoMoved; 
                }
                return boardState.State.Empty; // 此示例规则触发了，但没决出胜负，让标准流程继续
            
            // case 2: // 另一个规则示例：如果X下三子直接获胜（无论是否连线）
            //     if (playerWhoMoved == boardState.State.X) {
            //         int countX = 0;
            //         for(int i=0; i<9; i++) {
            //             if(boardController.GetState(i) == boardState.State.X) countX++;
            //         }
            //         if (countX >= 3) { // 假设规则是下满3个X就赢
            //              Debug.Log($"规则 {ruleType} 触发：玩家X 通过放置三个棋子获胜！");
            //              return boardState.State.X;
            //         }
            //     }
            //     return boardState.State.Empty; // 如果条件未满足

            default:
                // 对于RuleType 0 (默认规则) 已在方法开头处理，这里应该不会执行到 ruleType 0
                Debug.LogWarning($"尝试处理规则类型 {ruleType} ({ruleDisplayNames[ruleType]}) 但未在switch中定义具体行为。");
                return boardState.State.Empty; // 未知的规则类型，标准流程继续
        }
    }
    // 原 Start 和 Update 方法可以移除，因为这个类不再是MonoBehaviour
}
