using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events; // Required for UnityEvents

public class UISwipeInteraction : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Swipe Settings")]
    [Tooltip("Minimum distance (in screen pixels) for a swipe to be registered.")]
    public float swipeThreshold = 50f;
    [Tooltip("Should the swipe be registered continuously during drag, or only once on PointerUp?")]
    public bool continuousSwipeDetection = false;
    [Tooltip("需要多少次完整的来回滑动循环才能触发 OnSwipeBackAndForth 事件。")]
    public int requiredBackAndForthCycles = 2; // 新增：需要多少次循环

    [Header("Events")]
    [Tooltip("Event triggered when a left swipe is detected.")]
    public UnityEvent OnSwipeLeft;
    [Tooltip("Event triggered when a right swipe is detected.")]
    public UnityEvent OnSwipeRight;
    [Tooltip("Event triggered when the pointer is pressed down on this element.")]
    public UnityEvent OnPointerPressed;
     [Tooltip("Event triggered when the pointer is released from this element after a press/drag.")]
    public UnityEvent OnPointerReleased;
    [Tooltip("当检测到一次完整的来回滑动（例如左滑->右滑，或右滑->左滑）时触发。")]
    public UnityEvent OnSwipeBackAndForth;


    private bool isDragging = false;
    private Vector2 pressStartPosition;
    private Vector2 lastDragPosition;

    private bool swipeDetectedThisDrag = false; // To ensure event fires once per drag if continuousSwipeDetection is false
    // 新增：用于检测来回滑动的状态变量
    private int detectedSwipeDirectionInDrag = 0; // 0: none, 1: first swipe left, 2: first swipe right
    private bool backAndForthPatternTriggeredThisDrag = false;
    private int currentBackAndForthCycleCount = 0; // 新增：当前已完成的来回循环次数

    public void OnPointerDown(PointerEventData eventData)
    {
        // Check if the pointer is actually over this UI element.
        // This is important if the script is on a parent that might receive events for children.
        if (!RectTransformUtility.RectangleContainsScreenPoint(GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera))
        {
            return;
        }

        isDragging = true;
        swipeDetectedThisDrag = false;
        pressStartPosition = eventData.position;
        lastDragPosition = eventData.position;
        OnPointerPressed?.Invoke();
        Debug.Log("UISwipeInteraction: Pointer Down at " + eventData.position);

        // 重置来回滑动检测的状态
        detectedSwipeDirectionInDrag = 0;
        backAndForthPatternTriggeredThisDrag = false;
        currentBackAndForthCycleCount = 0; // 新增：重置循环计数
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        Vector2 currentDragPosition = eventData.position;
        float dragDeltaX = currentDragPosition.x - lastDragPosition.x; // More responsive for continuous
        float totalSwipeDistanceX = currentDragPosition.x - pressStartPosition.x;

        // --- 事件触发标志 ---
        bool fireStandardRightSwipeEvent = false;
        bool fireStandardLeftSwipeEvent = false;

        // --- 保留原有的标准左右滑动事件触发逻辑 ---
        if (continuousSwipeDetection || !swipeDetectedThisDrag)
        {
            // 条件A: 当前拖拽段本身构成一次显著滑动
            bool segmentIsRightSwipe = dragDeltaX > swipeThreshold;
            bool segmentIsLeftSwipe = dragDeltaX < -swipeThreshold;

            // 条件B: 连续检测模式下，当前拖拽段可能不显著，但累积位移显著
            bool cumulativeRightSwipeContinuous = continuousSwipeDetection && dragDeltaX > 0 && Mathf.Abs(totalSwipeDistanceX) > swipeThreshold;
            bool cumulativeLeftSwipeContinuous = continuousSwipeDetection && dragDeltaX < 0 && Mathf.Abs(totalSwipeDistanceX) > swipeThreshold;

            if (segmentIsRightSwipe || cumulativeRightSwipeContinuous)
            {
                // 原始代码中的内部 'skip' 条件: !(continuousSwipeDetection && Mathf.Abs(dragDeltaX) < swipeThreshold && !swipeDetectedThisDrag)
                // 如果要精确复刻，这个skip条件应该在这里判断。
                // 为了简化和清晰，我们假设如果上面的条件满足，就准备触发。
                // 如果 segmentIsRightSwipe 为真，则 Mathf.Abs(dragDeltaX) < swipeThreshold 必为假，原始skip条件不满足，事件会触发。
                // 如果 segmentIsRightSwipe 为假但 cumulativeRightSwipeContinuous 为真，此时 dragDeltaX 可能较小。
                // 原始skip: continuousSwipeDetection && (dragDeltaX < swipeThreshold) && !swipeDetectedThisDrag
                // 若为真则跳过。这意味着，如果本段不强，且是连续模式的第一次累积触发，则跳过。这部分逻辑比较费解。
                // 当前简化：如果外部条件满足，就认为可以触发。
                fireStandardRightSwipeEvent = true;
            }
            else if (segmentIsLeftSwipe || cumulativeLeftSwipeContinuous)
            {
                fireStandardLeftSwipeEvent = true;
            }
        }

        if (fireStandardRightSwipeEvent)
        {
            Debug.Log("UISwipeInteraction: Swipe Right Detected. DeltaX: " + dragDeltaX + ", TotalX: " + totalSwipeDistanceX);
            OnSwipeRight?.Invoke();
            if(!continuousSwipeDetection) swipeDetectedThisDrag = true;
        }
        if (fireStandardLeftSwipeEvent) // 使用 if 而非 else if，因为理论上极快切换方向可能都为false，或避免逻辑错误
        {
            Debug.Log("UISwipeInteraction: Swipe Left Detected. DeltaX: " + dragDeltaX + ", TotalX: " + totalSwipeDistanceX);
            OnSwipeLeft?.Invoke();
            if(!continuousSwipeDetection) swipeDetectedThisDrag = true;
        }

        // --- 新增：检测来回滑动模式 ---
        // 使用更简单的、基于当前拖拽段是否超过阈值的判断，使模式检测更清晰
        bool currentSegmentIsActuallySwipeRight = dragDeltaX > swipeThreshold;
        bool currentSegmentIsActuallySwipeLeft = dragDeltaX < -swipeThreshold;

        if (!backAndForthPatternTriggeredThisDrag) // 如果“多次循环”事件尚未在本轮拖拽中触发
        {
            if (detectedSwipeDirectionInDrag == 0) // 状态0：等待当前循环的第一次滑动
            {
                if (currentSegmentIsActuallySwipeRight)
                {
                    detectedSwipeDirectionInDrag = 2; // 记录第一次滑动为向右
                    // Debug.Log("UISwipeInteraction (Cycle Detection): Part 1 of a cycle: RIGHT swipe.");
                }
                else if (currentSegmentIsActuallySwipeLeft)
                {
                    detectedSwipeDirectionInDrag = 1; // 记录第一次滑动为向左
                    // Debug.Log("UISwipeInteraction (Cycle Detection): Part 1 of a cycle: LEFT swipe.");
                }
            }
            else // 状态1或2：已记录当前循环的第一次滑动，等待相反方向的第二次滑动
            {
                bool cycleCompletedThisSegment = false;
                if (detectedSwipeDirectionInDrag == 1 && currentSegmentIsActuallySwipeRight) // 第一次是左，当前是右 (L-R cycle part completed)
                {
                    // Debug.Log("UISwipeInteraction (Cycle Detection): L-R part of a cycle completed.");
                    cycleCompletedThisSegment = true;
                }
                else if (detectedSwipeDirectionInDrag == 2 && currentSegmentIsActuallySwipeLeft) // 第一次是右，当前是左 (R-L cycle part completed)
                {
                    // Debug.Log("UISwipeInteraction (Cycle Detection): R-L part of a cycle completed.");
                    cycleCompletedThisSegment = true;
                }

                if (cycleCompletedThisSegment)
                {
                    currentBackAndForthCycleCount++; // 增加已完成的循环次数
                    Debug.Log($"UISwipeInteraction: Completed cycle {currentBackAndForthCycleCount} of {requiredBackAndForthCycles}.");
                    detectedSwipeDirectionInDrag = 0; // 重置，以便检测下一个循环的开始

                    if (currentBackAndForthCycleCount >= requiredBackAndForthCycles)
                    {
                        Debug.Log("UISwipeInteraction: Required number of back-and-forth cycles reached!");
                        OnSwipeBackAndForth?.Invoke();
                        backAndForthPatternTriggeredThisDrag = true; // 标记"多次循环"事件已触发，防止重复
                    }
                }
            }
        }
        // --- 来回滑动模式检测结束 ---

        lastDragPosition = currentDragPosition;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        Debug.Log("UISwipeInteraction: Pointer Up at " + eventData.position);
        OnPointerReleased?.Invoke();

        if (!continuousSwipeDetection && !swipeDetectedThisDrag) // Only evaluate on Up if not continuous and no swipe during drag
        {
            float totalSwipeDistanceX = eventData.position.x - pressStartPosition.x;

            if (totalSwipeDistanceX > swipeThreshold)
            {
                Debug.Log("UISwipeInteraction: Swipe Right Confirmed on PointerUp. TotalX: " + totalSwipeDistanceX);
                OnSwipeRight?.Invoke();
            }
            else if (totalSwipeDistanceX < -swipeThreshold)
            {
                Debug.Log("UISwipeInteraction: Swipe Left Confirmed on PointerUp. TotalX: " + totalSwipeDistanceX);
                OnSwipeLeft?.Invoke();
            }
        }
        
        isDragging = false;
        swipeDetectedThisDrag = false; // Reset for next interaction
    }

    void OnValidate()
    {
        // Ensure threshold is not negative
        if (swipeThreshold < 0)
        {
            swipeThreshold = 0;
        }
    }
} 