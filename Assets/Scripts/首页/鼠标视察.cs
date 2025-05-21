using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 需要访问Image组件

public class 鼠标视察 : MonoBehaviour
{
    public enum MovementType
    {
        Linear,
        Quadratic,
        Inverse,
        Logarithmic
    }

    [Tooltip("选择运动曲线类型")]
    public MovementType selectedMovementType = MovementType.Linear;

    [Tooltip("视差效果的强度")]
    public float parallaxIntensity = 10f;

    [Tooltip("运动平滑度，值越小越平滑")]
    public float smoothTime = 0.1f;

    private RectTransform imageRectTransform;
    private Vector2 initialAnchoredPosition;
    private Vector2 currentVelocity = Vector2.zero; // 用于SmoothDamp

    // 屏幕尺寸
    private Vector2 screenSize;

    // 图片的原始尺寸和当前有效尺寸 (考虑缩放)
    private Vector2 imageOriginalSize;
    private Vector2 imageEffectiveSize;

    private bool useGyro = false;


    void Start()
    {
        imageRectTransform = GetComponent<RectTransform>();
        if (imageRectTransform == null)
        {
            Debug.LogError("鼠标视察: RectTransform component not found on this GameObject. Disabling script.", this);
            enabled = false;
            return;
        }

        // 尝试获取Image组件以获取原始尺寸，如果不是Image，则使用RectTransform的尺寸
        Image imageComponent = GetComponent<Image>();
        if (imageComponent != null && imageComponent.sprite != null)
        {
            imageOriginalSize = imageComponent.sprite.rect.size;
        }
        else
        {
            imageOriginalSize = imageRectTransform.rect.size;
        }
        
        initialAnchoredPosition = imageRectTransform.anchoredPosition;
        screenSize = new Vector2(Screen.width, Screen.height);

#if UNITY_ANDROID || UNITY_IOS
        if (Application.isMobilePlatform)
        {
            if (SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true;
                useGyro = true;
                Debug.Log("鼠标视察: Gyroscope enabled for parallax.");
            }
            else
            {
                Debug.LogWarning("鼠标视察: Gyroscope not supported on this mobile device. Parallax will use mouse/touch if available, or be static.", this);
            }
        }
#endif

        // 监听屏幕分辨率变化事件，以便动态更新
        // 在Unity 2019.2+可以考虑使用 Screen.safeAreaChanged 事件，但为了兼容性，我们可以在Update中检查
    }

    void Update()
    {
        if (!enabled) return;

        // 检查屏幕尺寸是否变化
        if (screenSize.x != Screen.width || screenSize.y != Screen.height)
        {
            screenSize = new Vector2(Screen.width, Screen.height);
            // 如果屏幕尺寸变化，可能需要重新计算某些依赖于屏幕尺寸的参数
        }

        // 更新图片有效尺寸 (考虑当前缩放)
        imageEffectiveSize = new Vector2(imageOriginalSize.x * imageRectTransform.localScale.x, imageOriginalSize.y * imageRectTransform.localScale.y);

        Vector2 normalizedInputPosition;

#if UNITY_ANDROID || UNITY_IOS
        if (useGyro && Input.gyro.enabled)
        {
            // 使用陀螺仪的重力向量作为输入
            // Input.gyro.gravity x 和 y 通常在 [-1, 1] 范围内，对应屏幕的倾斜
            // tilting device right -> gravity.x positive
            // tilting device display up (bottom edge towards user) -> gravity.y positive
            // 这与鼠标标准化后的方向一致
            normalizedInputPosition = new Vector2(Input.gyro.gravity.x, Input.gyro.gravity.y);
        }
        else
        {
            // 移动平台但陀螺仪不可用或未启用，回退到鼠标/触摸输入
            // 或者如果Input.touchCount > 0，也可以使用触摸输入，但这里保持与原逻辑一致用鼠标
            Vector2 mousePosition = Input.mousePosition;
            normalizedInputPosition = new Vector2(
                (mousePosition.x / screenSize.x - 0.5f) * 2f,
                (mousePosition.y / screenSize.y - 0.5f) * 2f
            );
        }
#else
        // 非移动平台，使用鼠标输入
        Vector2 mousePosition = Input.mousePosition;
        normalizedInputPosition = new Vector2(
            (mousePosition.x / screenSize.x - 0.5f) * 2f,
            (mousePosition.y / screenSize.y - 0.5f) * 2f
        );
#endif

        // 3. 根据选择的运动曲线调整标准化后的输入位置
        Vector2 curvedInputPosition = ApplyMovementCurve(normalizedInputPosition);

        // 4. 计算目标偏移量
        // 输入向右 (normalized.x 为正), 图片向左 (offset.x 为负)
        Vector2 targetOffset = new Vector2(
            -curvedInputPosition.x * parallaxIntensity,
            -curvedInputPosition.y * parallaxIntensity
        );

        // 5. 计算最大允许偏移量以防止间隙
        // 图片超出屏幕的部分 / 2 就是最大允许的单向移动距离
        float maxOffsetX = Mathf.Max(0, (imageEffectiveSize.x - screenSize.x) / 2f);
        float maxOffsetY = Mathf.Max(0, (imageEffectiveSize.y - screenSize.y) / 2f);
        
        // 如果图片比屏幕小，则不允许移动，否则会出现空隙
        if (imageEffectiveSize.x <= screenSize.x) maxOffsetX = 0;
        if (imageEffectiveSize.y <= screenSize.y) maxOffsetY = 0;
        
        // 应用视差强度到最大偏移量上，确保 intensity 不会导致图片移出过多
        // 这一步的逻辑需要仔细考虑：我们是希望intensity直接控制像素位移，
        // 还是希望intensity是一个乘数，作用于normalizedMousePosition，然后结果被钳制在maxOffset内。
        // 目前的设计是intensity直接影响目标偏移，然后clamp。
        // parallaxIntensity 应该影响的是"移动范围"相对于"鼠标移动范围"的比例。
        // 因此，最大偏移也应受 parallaxIntensity 影响，但不应超过计算出的 maxOffsetX/Y。
        // 我们将 targetOffset 钳制在 [-maxOffsetX, maxOffsetX] 和 [-maxOffsetY, maxOffsetY]
        
        targetOffset.x = Mathf.Clamp(targetOffset.x, -maxOffsetX, maxOffsetX);
        targetOffset.y = Mathf.Clamp(targetOffset.y, -maxOffsetY, maxOffsetY);

        // 6. 计算最终的目标锚点位置
        Vector2 finalTargetPosition = initialAnchoredPosition + targetOffset;

        // 7. 平滑移动到目标位置
        imageRectTransform.anchoredPosition = Vector2.SmoothDamp(
            imageRectTransform.anchoredPosition,
            finalTargetPosition,
            ref currentVelocity,
            smoothTime
        );
    }

    private Vector2 ApplyMovementCurve(Vector2 normalizedPosition)
    {
        float x = normalizedPosition.x;
        float y = normalizedPosition.y;

        switch (selectedMovementType)
        {
            case MovementType.Linear:
                // 已经是线性的，无需更改
                break;
            case MovementType.Quadratic:
                // y = x^2 * sign(x) 或者 y = x * |x|
                x = x * Mathf.Abs(x);
                y = y * Mathf.Abs(y);
                break;
            case MovementType.Inverse: // 反比例函数 (y = k/x)，这里更像是一种减速曲线
                // 对于 [-1, 1] 的输入，直接用反比例不太合适，通常用于 (0, inf)
                // 我们可以实现一个在边缘移动慢，中间移动快的或者相反的效果
                // 例如: sign(x) * (1 - sqrt(1 - |x|^2)) (easeOutCirc)
                // 或者 sign(x) * (|x| ^ (1/k)) for k > 1 (power curve, decelerate)
                // 为了简单，我们用一个简单的减速: x^3, y^3
                x = Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 3);
                y = Mathf.Sign(y) * Mathf.Pow(Mathf.Abs(y), 3);
                break;
            case MovementType.Logarithmic: // 对数函数 (y = log(x))
                // 对数函数在0附近未定义或趋向负无穷，对于[-1,1]输入不直接适用
                // 我们可以用 sign(x) * log(1 + |x|* (e-1)) / log(e) 类似的形式，使其在0处为0，在1处为1
                // 或者更简单的，类似上面的power curve，但指数小于1 (accelerate)
                // 例如: sign(x) * (|x| ^ 0.5)
                x = Mathf.Sign(x) * Mathf.Sqrt(Mathf.Abs(x));
                y = Mathf.Sign(y) * Mathf.Sqrt(Mathf.Abs(y));
                break;
        }
        return new Vector2(x, y);
    }

    void OnDisable()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (useGyro && SystemInfo.supportsGyroscope) // 确保是由于此脚本启用的
        {
            Input.gyro.enabled = false;
            useGyro = false;
            Debug.Log("鼠标视察: Gyroscope disabled.");
        }
#endif
    }

    void OnDestroy() // 以防万一对象在未被disable时直接销毁
    {
#if UNITY_ANDROID || UNITY_IOS
        if (Input.gyro.enabled && useGyro) // 检查是否由此脚本启用并仍然是useGyro状态
        {
            Input.gyro.enabled = false;
            Debug.Log("鼠标视察: Gyroscope explicitly disabled on destroy in case OnDisable wasn't called.");
        }
#endif
    }
}
