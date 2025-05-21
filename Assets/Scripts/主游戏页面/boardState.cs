using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class boardState : MonoBehaviour
{
    public enum State
    {
        X,
        O,
        Empty
    }

    public Image[] boardImages;
    public Button[] boardButtons;
    public event System.Action<int> OnPlayerMove;
    public event System.Action<int> OnAIMove;

    // 新增游戏状态事件
    public event System.Action OnPlayerWinsGame;
    public event System.Action OnAIWinsGame;
    public event System.Action OnGameDraw;
    public event System.Action OnPlayerAlmostWins;
    public event System.Action OnAIAlmostWins;
    public event System.Action OnBoardReset;
    public event System.Action OnPlayerBlockedAIWin; // 新增事件：玩家阻止AI获胜时触发
    public event System.Action<State> OnMatchEnd; // 新增事件：当一方分数达到获胜条件时触发

    private State[] board = new State[9];
    public Sprite spriteX;
    public Sprite spriteO;
    public Sprite spriteEmpty;
    public State currentPlayer = State.X; // 调试时默认X
    public Dropdown RuleSelect;
    public ExtraRule extraRuleProcessor; // Handles extra rule logic
    public Text Player1Score;
    public Text Player2Score;
    public int[] Score = new int[2]; // 0: Player1 (X), 1: Player2 (O)
    private const int TARGET_SCORE_TO_WIN_GAME = 5;
    private bool isMatchOver = false;
    public Text resettingBoardStatusText; // 新增：用于显示棋盘重置状态的文本
    private Coroutine ellipsisAnimationCoroutine; // 用于存储省略号动画协程的引用
    private Coroutine temporaryMessageCoroutine; // 新增：用于临时消息显示的协程引用
    private Coroutine _delayedResetCoroutine; // Added to manage the auto-reset coroutine

    public bool IsCurrentlyResettingAfterRoundEnd { get; private set; } = false; // New flag
    public bool WasBoardFullJustBeforeThisReset { get; private set; } = false; // New flag for board fullness check

    [Header("Audio Settings")]
    [Tooltip("用于播放游戏音效的AudioSource组件")]
    public AudioSource gameAudioSource;
    [Tooltip("棋盘按钮点击音效")]
    public AudioClip boardClickSound;
    [Tooltip("获胜音效")]
    public AudioClip winSound;
    [Tooltip("平局音效")]
    public AudioClip drawSound;
    [Tooltip("重置操作音效（棋盘/分数）")]
    public AudioClip resetActionSound;

    public CharacterReaction Character;

    private List<int> winningLineIndexes = new List<int>(); // 用于存储获胜棋子的索引
    public Color winningLineColor = Color.red; // 获胜连线的颜色，可在Inspector中修改
    public Color defaultXColor = Color.white; // X棋子的默认颜色
    public Color defaultOColor = Color.white; // O棋子的默认颜色

    void Start()
    {
        extraRuleProcessor = new ExtraRule(this);

        boardButtons = GetComponentsInChildren<Button>();
        boardImages = new Image[boardButtons.Length];
        for (int i = 0; i < boardButtons.Length; i++)
        {
            boardImages[i] = boardButtons[i].GetComponent<Image>();
            int index = i;
            boardButtons[i].onClick.AddListener(() => OnBoardButtonClick(index));
        }
        if (resettingBoardStatusText != null) // 新增：启动时隐藏重置文本
        {
            resettingBoardStatusText.gameObject.SetActive(false);
        }
        ResetBoard();
        ResetScores(); // 初始化时重置并更新分数
    }

    public void ResetBoard()
    {
        // Stop the delayed auto-reset coroutine if it's running
        if (_delayedResetCoroutine != null)
        {
            StopCoroutine(_delayedResetCoroutine);
            _delayedResetCoroutine = null;
            Debug.Log("手动重置棋盘，已取消自动延时重置。");

            if (ellipsisAnimationCoroutine != null)
            {
                // StopCoroutine(ellipsisAnimationCoroutine); // This will be stopped by stopping _delayedResetCoroutine if it was its child
                // ellipsisAnimationCoroutine = null;
            }
            if (resettingBoardStatusText != null && resettingBoardStatusText.gameObject.activeSelf)
            {
                resettingBoardStatusText.gameObject.SetActive(false);
            }
        }

        // Check and store if the board is full BEFORE clearing it
        WasBoardFullJustBeforeThisReset = IsFull(); 

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayResetSound();
        }
        winningLineIndexes.Clear(); // 清除上一局的获胜连线记录
        for (int i = 0; i < board.Length; i++)
        {
            board[i] = State.Empty;
        }
        SetBox();
        currentPlayer = State.X;
        Debug.Log("棋盘已重置，轮到 " + currentPlayer + " 下棋");
        OnBoardReset?.Invoke();
    }

    public void ResetScores()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayResetSound();
        }
        Score[0] = 0;
        Score[1] = 0;
        UpdateScoreUI();
        isMatchOver = false;
        Debug.Log("积分已重置，比赛可以重新开始");
        Character.ResetStats();
    }

    private void UpdateScoreUI()
    {
        if (Player1Score != null) Player1Score.text = Score[0].ToString();
        if (Player2Score != null) Player2Score.text = Score[1].ToString();
    }

    private void AwardPoint(State winner)
    {
        if (isMatchOver) return;

        HighlightWinningLine(winner); // 新增调用

        if (winner == State.X)
        {
            Score[0]++;
            Debug.Log("玩家X 得1分");
            AudioManager.Instance.PlayWinSound();
            OnPlayerWinsGame?.Invoke();
        }
        else if (winner == State.O)
        {
            Score[1]++;
            Debug.Log("玩家O 得1分");
            AudioManager.Instance.PlayWinSound();
            OnAIWinsGame?.Invoke();
        }
        UpdateScoreUI();

        // 检查是否有一方达到最终获胜分数
        if (Score[0] >= TARGET_SCORE_TO_WIN_GAME)
        {
            isMatchOver = true;
            Debug.Log("玩家X 赢得整场比赛！");
            OnMatchEnd?.Invoke(State.X);
            EndingCheckResult endingResult = PlayerProcess.Instance.CheckAndUnlockEnding((int)Character.CurrentMood, true);
            Debug.Log($"比赛结束，玩家X获胜。结局状态: {endingResult.Type}, 结局ID: {endingResult.EndingId?.ToString() ?? "N/A"}");
            StartCoroutine(ShowEndingStatusPopup(endingResult));
        }
        else if (Score[1] >= TARGET_SCORE_TO_WIN_GAME)
        {
            isMatchOver = true;
            Debug.Log("玩家O 赢得整场比赛！");
            OnMatchEnd?.Invoke(State.O);
            EndingCheckResult endingResult = PlayerProcess.Instance.CheckAndUnlockEnding((int)Character.CurrentMood, false);
            Debug.Log($"比赛结束，玩家O获胜。结局状态: {endingResult.Type}, 结局ID: {endingResult.EndingId?.ToString() ?? "N/A"}");
            StartCoroutine(ShowEndingStatusPopup(endingResult));
        }
        else // 如果比赛未结束 (即 !isMatchOver)
        {
            // 如果比赛未结束，则延迟2秒后自动重置棋盘准备下一局
            if (_delayedResetCoroutine != null)
            {
                StopCoroutine(_delayedResetCoroutine);
            }
            _delayedResetCoroutine = StartCoroutine(DelayedResetBoardAfterRoundWin());
        }

        // 如果比赛未结束，则准备下一局 (通常 ResetBoard 会在这里或由外部逻辑调用)
        // if (!isMatchOver) ResetBoard();  // 这行现在由协程处理，可以删除或保留注释
    }

    private IEnumerator ShowEndingStatusPopup(EndingCheckResult endingResult) // <--- 修改参数类型
    {
        yield return new WaitForSeconds(1f); // <--- 添加1秒延迟

        string popupMessage = "";
        string buttonText = "确定";
        Action buttonAction = null;
        int? endingIdToShow = endingResult.EndingId; // 保存ID供后续使用

        // 定义CGViewer关闭后的通用回调
        Action cgViewerClosedAction = () => {
            Debug.Log("CGViewer closed callback: Attempting to show main menu and play main BGM.");
            if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu(); // 或者其他适当的界面
            // 主BGM的播放现在由UIManager.ShowMainMenu()更可靠地处理，此处不再重复调用，以避免潜在的两次播放或冲突。
            // if (AudioManager.Instance != null) AudioManager.Instance.PlayMainBGM(1f);
        };

        switch (endingResult.Type) // <--- 使用 result.Type
        {
            case EndingType.newEnding:
                popupMessage = "恭喜！您解锁了一个新的结局！";
                buttonText = "进入结局";
                buttonAction = () => {
                    if (endingIdToShow.HasValue)
                    {
                        Debug.Log($"尝试进入新结局 ID: {endingIdToShow.Value}");
                        UIManager.Instance.ShowCGViewer(endingIdToShow.Value, cgViewerClosedAction); // 使用通用回调
                    }
                    else
                    {
                        Debug.LogError("新结局ID无效，无法显示CG。检查PlayerProcess.CheckAndUnlockEnding逻辑。");
                        cgViewerClosedAction(); // 如果无法显示CG，也执行关闭后的操作（如返回主菜单）
                    }
                };
                break;
            case EndingType.oldEnding:
                popupMessage = "您已达成过这个结局。";
                buttonText = "查看结局"; // 或者 "重新体验"
                buttonAction = () => {
                    if (endingIdToShow.HasValue)
                    {
                        Debug.Log($"尝试查看旧结局 ID: {endingIdToShow.Value}");
                        UIManager.Instance.ShowCGViewer(endingIdToShow.Value, cgViewerClosedAction); // 使用通用回调
                    }
                    else
                    {
                        Debug.LogError("旧结局ID无效，无法显示CG。检查PlayerProcess.CheckAndUnlockEnding逻辑。");
                        cgViewerClosedAction(); // 如果无法显示CG，也执行关闭后的操作
                    }
                };
                break;
            case EndingType.lockedEnding:
                popupMessage = "很遗憾，未能达成任何结局。";
                buttonText = "确定";
                buttonAction = () => {
                    Debug.Log("结局未达成，返回或重试... (具体逻辑待实现)");
                    // 例如: 返回主菜单或重置游戏
                    cgViewerClosedAction(); // 未达成结局时，也执行关闭后的操作（返回主菜单）
                };
                break;
            default:
                Debug.LogWarning($"未知的 EndingType: {endingResult.Type}，无法生成提示信息。");
                yield break; // <--- 修改为 yield break
        }

        // GameObject popupGO = Instantiate(endingPopupPrefab); // 移除手动实例化
        // PopupWindows popupInstance = popupGO.GetComponent<PopupWindows>(); // 移除 GetComponent

        // popupInstance 将从 UIManager 获取
        PopupWindows popupInstance = null; 

        Action finalButtonAction = () => {
            buttonAction?.Invoke(); // 执行特定于结局类型的动作
            if (popupInstance != null && popupInstance.gameObject != null && popupInstance.gameObject.activeInHierarchy) 
            { 
                popupInstance.ClosePopup(); // 关闭弹窗
            }
        };

        // 调用 UIManager 来显示单页弹窗
        popupInstance = UIManager.Instance.ShowSinglePagePopup(
            message: popupMessage,
            button1Text: buttonText,
            button1Action: finalButtonAction
            // 其他参数使用默认值
        );

        if (popupInstance == null) // 检查 UIManager 是否成功创建了弹窗
        {
            Debug.LogError("无法通过 UIManager 创建结局状态弹窗！");
            // if (popupGO != null) Destroy(popupGO); // popupGO 不再存在
        }
        // popupInstance.Show(); // UIManager 的方法内部会调用 Show()
    }

    private IEnumerator DelayedResetBoardAfterRoundWin()
    {
        try
        {
            IsCurrentlyResettingAfterRoundEnd = true; // Set flag indicating context

            // 停止任何正在显示的临时消息，因为棋盘重置消息优先级更高
            if (temporaryMessageCoroutine != null)
            {
                StopCoroutine(temporaryMessageCoroutine);
                temporaryMessageCoroutine = null;
            }
            // 确保之前的省略号动画（如果因某种原因未停止）也被停止
            if (ellipsisAnimationCoroutine != null)
            {
                StopCoroutine(ellipsisAnimationCoroutine);
            }

            if (resettingBoardStatusText != null)
            {
                resettingBoardStatusText.gameObject.SetActive(true);
                if (ellipsisAnimationCoroutine != null)
                {
                    StopCoroutine(ellipsisAnimationCoroutine);
                }
                ellipsisAnimationCoroutine = StartCoroutine(AnimateResettingText("正在清理棋盘"));
            }

            yield return new WaitForSeconds(2f);

            if (ellipsisAnimationCoroutine != null)
            {
                StopCoroutine(ellipsisAnimationCoroutine);
                ellipsisAnimationCoroutine = null;
            }
            if (resettingBoardStatusText != null)
            {
                resettingBoardStatusText.gameObject.SetActive(false);
            }

            if (!isMatchOver) // 再次检查，以防在这2秒内状态改变
            {
                Debug.Log("单局结束，2秒后自动重置棋盘。");
                ResetBoard();
            }
        }
        finally
        {
            IsCurrentlyResettingAfterRoundEnd = false; // Reset flag in all cases
            // If the coroutine completes naturally, nullify the reference if it's this instance.
            // This check helps if the coroutine could be restarted very quickly, though
            // current logic stops the old one before starting a new one.
            if (_delayedResetCoroutine == this._delayedResetCoroutine) // Check if it's THIS coroutine instance
            {
               // _delayedResetCoroutine = null; // Managed by the caller or manual reset now
            }    
        }
        _delayedResetCoroutine = null; // ensure it's nulled when coroutine ends
    }

    private IEnumerator AnimateResettingText(string baseMessage)
    {
        int dotCount = 1;
        int maxDots = 6;
        while (true)
        {
            string dots = new string('.', dotCount);
            if (resettingBoardStatusText != null)
            {
                resettingBoardStatusText.text = baseMessage + dots;
            }
            dotCount++;
            if (dotCount > maxDots)
            {
                dotCount = 1;
            }
            yield return new WaitForSeconds(0.2f); // 每0.3秒更新一次点的数量
        }
    }

    public void SetState(int index, State pieceToPlace)
    {
        if (index >= 0 && index < board.Length)
        {
            if (board[index] == State.Empty) // Only allow setting if the cell is empty
            {
                bool aiCouldHaveWonHere = false;
                if (pieceToPlace == State.X) // 仅当玩家(X)落子时，检查是否阻止了AI(O)
                {
                    State originalState = board[index]; // 保存原始状态 (应该是Empty)
                    board[index] = State.O;           // 假设AI在此落子
                    if (CheckWin(this.board, false) == State.O) // 检查AI是否会赢 (不输出日志)
                    {
                        aiCouldHaveWonHere = true;
                    }
                    board[index] = originalState;     // 恢复棋盘到玩家落子前的状态
                }

                // 玩家或AI实际落子
                board[index] = pieceToPlace;
                SetBox(); // Update UI for this primary move

                // 如果玩家的这一步阻止了AI的胜利，则触发事件
                if (aiCouldHaveWonHere)
                {
                    Debug.Log($"玩家 X 在格子 {index} 阻止了 AI O 的获胜机会!");
                    OnPlayerBlockedAIWin?.Invoke();
                }

                // **统一处理落子后的逻辑，包括额外规则和标准结局判断**
                HandlePostMoveLogic(index, pieceToPlace);
            }
        }
        else
        {
            Debug.LogError("SetState: Index out of bounds: " + index);
        }
    }

    public void SetState(int x, int y, State pieceToPlace)
    {
        int index = x + y * 3;
        SetState(index, pieceToPlace); // This will call the main SetState with HandlePostMoveLogic
    }

    void SetBox()
    {
        for (int i = 0; i < board.Length; i++)
        {
            Color pieceColor = Color.white; 

            switch (board[i])
            {
                case State.X:
                    boardImages[i].sprite = spriteX;
                    pieceColor = defaultXColor;
                    break;
                case State.O:
                    boardImages[i].sprite = spriteO;
                    pieceColor = defaultOColor;
                    break;
                case State.Empty:
                    boardImages[i].sprite = spriteEmpty;
                    break;
            }
            
            if (winningLineIndexes.Contains(i))
            {
                boardImages[i].color = winningLineColor;
            }
            else
            {
                boardImages[i].color = pieceColor;
            }
        }
        // CheckWin(null, true); // Consider where to call this if it should log to console after UI update
    }

    public State GetState(int index)
    {
        if (index >= 0 && index < board.Length)
        {
            return board[index];
        }
        Debug.LogError("GetState: Index out of bounds: " + index);
        return State.Empty;
    }

    public List<int> GetEmptyIndices()
    {
        List<int> emptyIndices = new List<int>();
        for (int i = 0; i < board.Length; i++)
        {
            if (board[i] == State.Empty)
            {
                emptyIndices.Add(i);
            }
        }
        return emptyIndices;
    }

    public State[] GetBoardStateArray()
    {
        State[] boardCopy = new State[board.Length];
        System.Array.Copy(board, boardCopy, board.Length);
        return boardCopy;
    }

    public bool IsFull(State[] stateToCheck = null)
    {
        State[] currentBoard = stateToCheck ?? board;
        foreach (var s in currentBoard)
        {
            if (s == State.Empty) return false;
        }
        return true;
    }

    // Centralized logic after any piece (player or AI) is placed
    private void HandlePostMoveLogic(int moveIndex, State playerWhoMoved)
    {
        if (isMatchOver) return;
        State standardWinner = CheckWin(null, true);
        if (standardWinner != State.Empty)
        {
            AwardPoint(standardWinner);
            return;
        }
        else
        {
            if (IsFull(null))
            {
                Debug.Log("平局！(棋盘检测)");
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayDrawSound();
                }
                OnGameDraw?.Invoke();
                if (!isMatchOver) // 新增：平局且比赛未结束时，也延迟重置棋盘
                {
                    if (_delayedResetCoroutine != null)
                    {
                        StopCoroutine(_delayedResetCoroutine);
                    }
                    _delayedResetCoroutine = StartCoroutine(DelayedResetBoardAfterRoundWin());
                }
                return;
            }
            else
            {
                LogAlmostWinOpportunities();

                if (playerWhoMoved == State.X)
                {
                    currentPlayer = State.O;
                    OnPlayerMove?.Invoke(moveIndex);
                }
                else if (playerWhoMoved == State.O)
                {
                    OnAIMove?.Invoke(moveIndex);
                    currentPlayer = State.X;
                }
            }
        }
    }

    // Handles button clicks from the player
    void OnBoardButtonClick(int index)
    {
        if (isMatchOver) 
        {
            Debug.LogWarning("比赛已结束，无法落子。");
            return;
        }

        if (currentPlayer != State.X) 
        {
            Debug.LogWarning("现在不是玩家X的回合，点击无效。");
            return;
        }
        
        if (GetState(index) == State.Empty && CheckWin(null, false) == State.Empty && !IsFull(null)) 
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBoardClickSound();
            }
            SetState(index, State.X);
        }
        else
        {
            Debug.LogWarning("无效落子：格子非空或游戏已结束。");
        }
    }

    /// <summary>
    /// 判断当前棋盘是否有胜者。
    /// 原理：遍历所有可能的连线（横、竖、斜），
    /// 如果某条连线上的三个格子都属于同一方且不为空，则该方获胜。
    /// 没有连线则返回Empty。
    /// </summary>
    /// <param name="state">要判断的棋盘状态数组，默认用当前棋盘</param>
    /// <param name="log">是否输出Debug信息</param>
    /// <returns>胜者（X、O或Empty）</returns>
    public State CheckWin(State[] state = null, bool log = true)
    {
        State[] currentBoard = state ?? this.board;
        int[,] winPatterns = GetWinPatterns(); 
        for (int i = 0; i < winPatterns.GetLength(0); i++)
        {
            int a = winPatterns[i,0];
            int b = winPatterns[i,1];
            int c = winPatterns[i,2];
            if (currentBoard[a] != State.Empty && currentBoard[a] == currentBoard[b] && currentBoard[b] == currentBoard[c])
            {
                if (log) Debug.Log($"{currentBoard[a]} 获胜！");
                return currentBoard[a];
            }
        }
        return State.Empty;
    }

    // Public method to set the current player, e.g., by an extra rule
    public void SetCurrentPlayer(State player)
    {
        currentPlayer = player;
        // Potentially add UI feedback for whose turn it is here
    }

    /// <summary>
    /// 记录棋盘上所有"即将胜利"（两子一线，末端有空位）的情况。
    /// </summary>
    private void LogAlmostWinOpportunities()
    {
        State[] currentBoard = this.board;
        int[,] winPatterns = new int[,]
        {
            {0,1,2}, {3,4,5}, {6,7,8}, // 横
            {0,3,6}, {1,4,7}, {2,5,8}, // 竖
            {0,4,8}, {2,4,6}           // 斜
        };

        for (int i = 0; i < winPatterns.GetLength(0); i++)
        {
            int p1 = winPatterns[i,0];
            int p2 = winPatterns[i,1];
            int p3 = winPatterns[i,2];

            // 检查玩家 X 的机会
            CheckAndLogOpportunity(currentBoard, p1, p2, p3, State.X);
            // 检查玩家 O (AI) 的机会
            CheckAndLogOpportunity(currentBoard, p1, p2, p3, State.O);
        }
    }

    private void CheckAndLogOpportunity(State[] board, int c1, int c2, int c3, State player)
    {
        string playerName = player == State.X ? "玩家 X" : "AI O";
        string lineInfo = $"格子 [{c1},{c2},{c3}]";
        bool opportunityFound = false;

        // 模式 1: P, P, Empty
        if (board[c1] == player && board[c2] == player && board[c3] == State.Empty)
        {
            Debug.Log($"{playerName} 即将在 {lineInfo} 形成三连 (当前: {player},{player},空)。空位在: {c3}");
            opportunityFound = true;
        }
        // 模式 2: P, Empty, P
        else if (board[c1] == player && board[c3] == player && board[c2] == State.Empty)
        {
            Debug.Log($"{playerName} 即将在 {lineInfo} 形成三连 (当前: {player},空,{player})。空位在: {c2}");
            opportunityFound = true;
        }
        // 模式 3: Empty, P, P
        else if (board[c2] == player && board[c3] == player && board[c1] == State.Empty)
        {
            Debug.Log($"{playerName} 即将在 {lineInfo} 形成三连 (当前: 空,{player},{player})。空位在: {c1}");
            opportunityFound = true;
        }

        if (opportunityFound)
        {
            if (player == State.X) OnPlayerAlmostWins?.Invoke();
            else if (player == State.O) OnAIAlmostWins?.Invoke();
        }
    }

    // 新增方法：高亮获胜的连线
    private void HighlightWinningLine(State winner)
    {
        winningLineIndexes.Clear(); // 先清除

        int[,] winPatterns = GetWinPatterns(); 
        for (int i = 0; i < winPatterns.GetLength(0); i++)
        {
            int a = winPatterns[i, 0];
            int b = winPatterns[i, 1];
            int c = winPatterns[i, 2];

            if (board[a] == winner && board[a] == board[b] && board[b] == board[c])
            {
                winningLineIndexes.Add(a);
                winningLineIndexes.Add(b);
                winningLineIndexes.Add(c);
                // 更新这些格子的颜色，因为SetBox可能在AwardPoint之后才被调用（如果开始新一局）
                // 或者在AwardPoint时尚未开始新一局，颜色需要立即改变
                boardImages[a].color = winningLineColor;
                boardImages[b].color = winningLineColor;
                boardImages[c].color = winningLineColor;
                break; 
            }
        }
    }

    // 辅助方法，返回胜利模式数组
    private int[,] GetWinPatterns()
    {
        return new int[,]
        {
            {0,1,2}, {3,4,5}, {6,7,8}, // 横
            {0,3,6}, {1,4,7}, {2,5,8}, // 竖
            {0,4,8}, {2,4,6}           // 斜
        };
    }

    // 新增：显示临时状态消息的方法
    public void ShowTemporaryMessage(string message, float duration)
    {
        if (resettingBoardStatusText == null) return;

        // 如果"正在清理棋盘"的动画正在运行，则停止它
        if (ellipsisAnimationCoroutine != null)
        {
            StopCoroutine(ellipsisAnimationCoroutine);
            ellipsisAnimationCoroutine = null; 
        }

        // 如果有其他的临时消息正在显示，也停止它
        if (temporaryMessageCoroutine != null)
        {
            StopCoroutine(temporaryMessageCoroutine);
        }

        resettingBoardStatusText.text = message;
        resettingBoardStatusText.gameObject.SetActive(true);
        temporaryMessageCoroutine = StartCoroutine(DisplayTemporaryMessageCoroutine(duration));
    }

    // 新增：处理临时消息显示的协程
    private IEnumerator DisplayTemporaryMessageCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        // 再次检查，确保 text 组件仍然存在，并且此协程未被新的消息覆盖
        if (resettingBoardStatusText != null && temporaryMessageCoroutine == this.temporaryMessageCoroutine) 
        {
            resettingBoardStatusText.gameObject.SetActive(false);
        }
        // 只有当这个协程确实是最后一个启动的临时消息协程时，才将引用置空
        if (temporaryMessageCoroutine == this.temporaryMessageCoroutine) 
        { 
            temporaryMessageCoroutine = null;
        }
    }
}
