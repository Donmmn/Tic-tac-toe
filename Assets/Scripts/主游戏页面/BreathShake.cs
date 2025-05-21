using UnityEngine;
using System.Collections;

public class BreathShake : MonoBehaviour
{
    [Tooltip("图片浮动的最大半径")]
    public float floatRadius = 5f;

    [Tooltip("图片浮动的速度，值越大，过渡到新目标点越快")]
    public float moveSpeed = 1f;

    [Tooltip("改变浮动目标点的时间间隔（秒）")]
    public float changeTargetInterval = 2f;

    private RectTransform rectTransform;
    private Vector2 initialAnchoredPosition;
    private Vector2 currentTargetOffset;
    private Vector2 currentVelocity = Vector2.zero; // 用于 SmoothDamp
    private float timeSinceLastTargetChange = 0f;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("BreathShake: RectTransform component not found on this GameObject. Disabling script.", this);
            enabled = false;
            return;
        }
    }

    void Start()
    {
        if (!enabled) return; // 如果在Awake中被禁用

        initialAnchoredPosition = rectTransform.anchoredPosition;
        SetNewRandomTargetOffset();
        timeSinceLastTargetChange = Random.Range(0, changeTargetInterval); // 初始随机化计时器，避免所有物体同步运动
    }

    void Update()
    {
        if (!enabled) return;

        timeSinceLastTargetChange += Time.deltaTime;

        if (timeSinceLastTargetChange >= changeTargetInterval)
        {
            SetNewRandomTargetOffset();
            timeSinceLastTargetChange = 0f;
        }

        // 计算实际的目标位置（初始位置 + 当前随机偏移）
        Vector2 actualTargetPosition = initialAnchoredPosition + currentTargetOffset;

        // 使用SmoothDamp平滑地移动到目标位置
        // smoothTime参数与moveSpeed反相关，moveSpeed越大，smoothTime越小，移动越快
        float smoothTime = 1f / Mathf.Max(0.001f, moveSpeed); // 防止moveSpeed为0导致除零
        rectTransform.anchoredPosition = Vector2.SmoothDamp(
            rectTransform.anchoredPosition, 
            actualTargetPosition, 
            ref currentVelocity, 
            smoothTime
        );
    }

    /// <summary>
    /// 在以initialAnchoredPosition为中心的、floatRadius为半径的圆内设置一个新的随机目标偏移量。
    /// </summary>
    void SetNewRandomTargetOffset()
    {
        currentTargetOffset = Random.insideUnitCircle * floatRadius;
    }
}
