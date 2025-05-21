using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class 井字棋AI : MonoBehaviour
{
    public boardState board; // 棋盘对象的引用
    // [Range(0,1)] public float mistakeRate = 0.1f; // AI的失误率 (移除，将由CharacterReaction的Sanity控制)
    public boardState.State aiSide = boardState.State.O;   // AI执棋方 (默认 O)
    public boardState.State playerSide = boardState.State.X; // 玩家执棋方 (默认 X)
    public float aiDelay = 0.5f; // AI下棋前的延迟时间（秒）
    private System.Random rand = new System.Random(); // 用于失误时随机选择
    private CharacterReaction characterReaction; // 对CharacterReaction脚本的引用

    // Start is called before the first frame update
    void Start()
    {
        // 尝试获取棋盘对象引用，如果在Inspector中未指定
        if (board == null) board = FindObjectOfType<boardState>(); 
        if (board != null)
        {
            // 订阅玩家下棋事件
            board.OnPlayerMove += OnPlayerMoveHandler;
        }
        else
        {
            Debug.LogError("井字棋AI: 未找到 boardState 对象!");
        }

        // 获取CharacterReaction的引用
        characterReaction = FindObjectOfType<CharacterReaction>();
        if (characterReaction == null)
        {
            Debug.LogError("井字棋AI: 未找到 CharacterReaction 对象! AI失误率将无法与理智值关联。");
        }
    }

    void OnDestroy()
    {
        // 组件销毁时取消事件订阅，防止内存泄漏
        if (board != null)
        {
            board.OnPlayerMove -= OnPlayerMoveHandler;
        }
    }

    // 当玩家下棋后，此方法被调用
    void OnPlayerMoveHandler(int index)
    {
        // 检查游戏是否仍在进行 (没有胜者且棋盘未满)
        if (board.CheckWin(null, false) == boardState.State.Empty && !board.IsFull()) 
        {
            // 如果游戏继续，启动AI的下棋流程 (带延迟)
            StartCoroutine(DelayedAIPlay());
        }
    }

    // 带延迟的AI下棋协程
    IEnumerator DelayedAIPlay()
    {
        yield return new WaitForSeconds(aiDelay);
        PlayAI(); // 执行AI下棋逻辑
    }

    /// <summary>
    /// AI执行下棋的主要逻辑。
    /// 首先判断是否触发失误率，如果触发则随机下一步。
    /// 否则，使用Minimax算法找到最佳落子点。
    /// </summary>
    public void PlayAI()
    {
        // 如果游戏已经结束 (有胜者或棋盘已满)，则AI不行动
        if (board.CheckWin(null, false) != boardState.State.Empty || board.IsFull()) 
        {
            return; 
        }

        float currentMistakeRate = 0.1f; // 默认失误率，以防CharacterReaction未找到
        if (characterReaction != null)
        {
            // 理智值越低，失误率越高。Sanity为0时，mistakeRate为1 (100%失误)
            // Sanity为MAX_SANITY(100)时，mistakeRate为0 (0%失误)
            currentMistakeRate = 1f - (characterReaction.CurrentSanity / CharacterReaction.MAX_SANITY);
            currentMistakeRate = Mathf.Clamp01(currentMistakeRate); // 确保在0-1之间
        }
        else
        {
            Debug.LogWarning("井字棋AI: CharacterReaction 未找到，使用默认失误率: " + currentMistakeRate);
        }

        // 判断是否触发失误
        if (Random.value < currentMistakeRate) // 使用动态计算的失误率
        {
            List<int> emptyIndices = board.GetEmptyIndices(); // 获取所有空格子
            if (emptyIndices.Count > 0)
            {
                int move = emptyIndices[rand.Next(emptyIndices.Count)]; // 随机选择一个空格子
                board.SetState(move, aiSide); // AI落子
                Debug.Log("AI 因失误，随机下在了格子 " + move);
                if (board != null) // 新增：调用boardState显示失误消息
                {
                    board.ShowTemporaryMessage("今汐出现了失误", 2f); // 显示2秒
                }
            }
            // AI失误下棋后，同样检查游戏是否结束
            CheckAndLogGameEndAfterAIMove();
            return;
        }

        // --- Minimax算法寻找最佳落子点开始 ---
        // AI的目标是找到一个落子点，使得在该点落子后，AI的局势评估分数最高。
        int bestMove = -1; // 记录最佳落子点的索引
        int bestScore = int.MinValue; // 记录AI能得到的最高分数 (初始化为负无穷)
        List<int> availableMoves = board.GetEmptyIndices(); // 获取所有可以落子的空位

        // 遍历每一个可以落子的位置
        foreach (int move in availableMoves)
        {
            // 1. 模拟落子：创建一个棋盘的副本，并在该副本上模拟AI在此处落子
            boardState.State[] simulatedBoard = board.GetBoardStateArray(); // 获取当前棋盘状态的副本
            simulatedBoard[move] = aiSide; // 在副本上模拟AI落子
            
            // 2. 局势评估：调用Minimax算法，评估AI在此处落子后，对手（玩家）能得到的最低分数
            //    因为Minimax是递归的，它会模拟双方后续的所有可能走法。
            //    这里传入false表示接下来轮到对手(Minimizer)下棋。
            int score = Minimax(simulatedBoard, false, 0); 
            
            // 3. 选择最佳：如果当前模拟落子得到的分数比已知的最高分还要高，
            //    则更新最高分和最佳落子点。
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }
        // --- Minimax算法寻找最佳落子点结束 ---

        // 如果找到了有效的最佳落子点 (bestMove != -1)
        if (bestMove != -1)
        {
            board.SetState(bestMove, aiSide); // AI在真实棋盘上落子
            // AI下棋后，不再直接处理额外规则，交由boardState.SetState或其后续逻辑统一处理
            // 标准的游戏结束检测仍然需要，以防AI的最后一步按标准规则结束游戏
            CheckAndLogGameEndAfterAIMove();
        }
        else
        {
            // 如果AI未能找到最佳移动
            CheckAndLogGameEndAfterAIMove();
        }
    }
    
    private void CheckAndLogGameEndAfterAIMove()
    {
        // 这个方法只负责标准结局的日志，额外规则的结局由boardState处理
        boardState.State gameWinner = board.CheckWin(null, false); // 检查胜负，但不重复打印日志，因为boardState会处理
        if (gameWinner != boardState.State.Empty)
        {
             // boardState.CheckWin(null, true) // 如果需要AI脚本也打印，可以取消这行注释，但boardState的CheckWin通常已打印
        }
        else if (board.IsFull())
        {
            Debug.Log("平局！(AI检测)"); // AI侧检测到的平局
        }
    }

    /// <summary>
    /// Minimax (极大极小) 算法，用于找到最佳落子点。
    /// 这是一个递归算法，它通过探索所有可能的未来走法来评估当前局势。
    /// </summary>
    /// <param name="currentSimulatedBoard">当前模拟的棋盘状态</param>
    /// <param name="isMaximizingPlayer">当前是否轮到最大化玩家 (AI) 行动</param>
    /// <param name="depth">当前递归深度，用于评估分数 (浅层胜利优于深层胜利)</param>
    /// <returns>当前局势的评估分数</returns>
    int Minimax(boardState.State[] currentSimulatedBoard, bool isMaximizingPlayer, int depth)
    {
        // 1. 检查终止条件 (递归的出口):
        //    a. 是否有胜者?
        //    b. 棋盘是否已满 (平局)?
        boardState.State winner = board.CheckWin(currentSimulatedBoard, false); // 在模拟棋盘上检查胜负，不打印日志

        if (winner == aiSide) return 10 - depth; // AI获胜，返回高分 (减去深度，鼓励快速获胜)
        if (winner == playerSide) return -10 + depth; // 玩家获胜，返回低分 (加上深度，鼓励拖延失败)
        if (board.IsFull(currentSimulatedBoard)) return 0; // 平局，返回0分

        // 2. 递归推演:
        int bestScoreCurrentTurn;
        if (isMaximizingPlayer) // 如果是AI (最大化玩家) 的回合
        {
            bestScoreCurrentTurn = int.MinValue; // AI试图找到使其分数最大化的走法
            List<int> emptyIndices = GetEmptyIndicesFromSimulatedBoard(currentSimulatedBoard);
            foreach (int move in emptyIndices)
            {
                currentSimulatedBoard[move] = aiSide; // AI尝试在此处落子
                // 递归调用Minimax，切换到最小化玩家的回合 (isMaximizingPlayer = false)
                bestScoreCurrentTurn = Mathf.Max(bestScoreCurrentTurn, Minimax(currentSimulatedBoard, false, depth + 1));
                currentSimulatedBoard[move] = boardState.State.Empty; // 撤销落子，回溯，尝试其他可能
            }
        }
        else // 如果是玩家 (最小化玩家) 的回合
        {
            bestScoreCurrentTurn = int.MaxValue; // 玩家试图找到使AI分数最小化的走法
            List<int> emptyIndices = GetEmptyIndicesFromSimulatedBoard(currentSimulatedBoard);
            foreach (int move in emptyIndices)
            {
                currentSimulatedBoard[move] = playerSide; // 玩家尝试在此处落子
                // 递归调用Minimax，切换到最大化玩家的回合 (isMaximizingPlayer = true)
                bestScoreCurrentTurn = Mathf.Min(bestScoreCurrentTurn, Minimax(currentSimulatedBoard, true, depth + 1));
                currentSimulatedBoard[move] = boardState.State.Empty; // 撤销落子，回溯
            }
        }
        return bestScoreCurrentTurn; // 返回当前模拟回合的最佳分数
    }

    // Minimax的辅助方法：从模拟的棋盘状态数组中获取所有空格子的索引
    List<int> GetEmptyIndicesFromSimulatedBoard(boardState.State[] simulatedBoard)
    {
        List<int> emptyIndices = new List<int>();
        for (int i = 0; i < simulatedBoard.Length; i++)
        {
            if (simulatedBoard[i] == boardState.State.Empty)
            {
                emptyIndices.Add(i);
            }
        }
        return emptyIndices;
    }
}
