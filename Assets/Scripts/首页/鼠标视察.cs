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

    private Canvas canvas;
    private float lastCanvasScaleFactor = 0f;
    private Vector2 lastScreenSizeInPixels = Vector2.zero;
    private Vector2 screenSizeInCanvasUnits = Vector2.zero;
    private Vector2 imageEffectiveSizeInCanvasUnits = Vector2.zero;
    private Vector2 imageRectBaseSize = Vector2.zero; // Store imageRectTransform.rect.size

    private bool useGyro = false;
    private bool isMobilePlatform = false; // 缓存是否为移动平台

    void Start()
    {
        imageRectTransform = GetComponent<RectTransform>();
        if (imageRectTransform == null)
        {
            Debug.LogError("鼠标视察 Start: RectTransform component not found. Disabling script.", this);
            enabled = false;
            return;
        }

        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("鼠标视察 Start: Canvas component not found in parent hierarchy of the RectTransform. Disabling script.", this);
            enabled = false;
            return;
        }
        
        // Get imageOriginalSize (sprite's pixel size) mainly for reference or if Image component exists
        // The core logic will use imageRectTransform.rect.size for canvas unit calculations.
        Vector2 imageOriginalSpritePixelSize = Vector2.zero;
        Image imageComponent = GetComponent<Image>();
        if (imageComponent != null && imageComponent.sprite != null)
        {
            imageOriginalSpritePixelSize = imageComponent.sprite.rect.size;
        }
        else
        {
            // If no sprite, imageOriginalSpritePixelSize remains zero, which is fine.
            // We will rely on imageRectTransform.rect.size.
            Debug.LogWarning("鼠标视察 Start: Image component or sprite not found. Parallax calculations will rely solely on RectTransform.rect.size and localScale, assuming it's correctly set up by UI layout/CanvasScaler.", this);
        }
        
        initialAnchoredPosition = imageRectTransform.anchoredPosition;
        
        // Initial calculation of sizes
        UpdateScreenAndImageSizes(); 

        isMobilePlatform = Application.isMobilePlatform; 

        Debug.Log($"鼠标视察 Start: Initial AnchoredPos: {initialAnchoredPosition}, Canvas ScaleFactor: {lastCanvasScaleFactor}, ScreenInCanvasUnits: {screenSizeInCanvasUnits}, ImageEffectiveInCanvasUnits: {imageEffectiveSizeInCanvasUnits}, SpritePixelSize (ref): {imageOriginalSpritePixelSize}, isMobilePlatform = {isMobilePlatform}");

#if UNITY_ANDROID || UNITY_IOS
        if (isMobilePlatform)
        {
            if (SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true; 
                if (Input.gyro.enabled) 
                {
                    useGyro = true;
                    Debug.Log("鼠标视察 Start: Gyroscope enabled for parallax.");
                }
                else
                {
                    Debug.LogWarning("鼠标视察 Start: Failed to enable Gyroscope even if supported. Will use touch/mouse.", this);
                }
            }
            else
            {
                Debug.LogWarning("鼠标视察 Start: Gyroscope not supported. Will use touch/mouse.", this);
            }
        }
#else
        if (!isMobilePlatform) Debug.Log("鼠标视察 Start: Not a mobile platform (Android/iOS defined). Using mouse input for parallax.");
#endif
    }

    void UpdateScreenAndImageSizes()
    {
        if (canvas == null || imageRectTransform == null)
        {
            Debug.LogError("鼠标视察 UpdateScreenAndImageSizes: Canvas or RectTransform is null. Cannot update sizes.", this);
            enabled = false; // Critical components missing
            return;
        }

        lastCanvasScaleFactor = canvas.scaleFactor;
        if (lastCanvasScaleFactor <= 0)
        {
            Debug.LogError($"鼠标视察 UpdateScreenAndImageSizes: Invalid canvas scale factor ({lastCanvasScaleFactor}). Disabling script to prevent division by zero or incorrect behavior.", this);
            enabled = false;
            return;
        }

        lastScreenSizeInPixels = new Vector2(Screen.width, Screen.height);
        screenSizeInCanvasUnits = lastScreenSizeInPixels / lastCanvasScaleFactor;

        imageRectBaseSize = imageRectTransform.rect.size; // This is in canvas units, before localScale
        if (imageRectBaseSize.x <= 0 || imageRectBaseSize.y <= 0)
        {
            Debug.LogError($"鼠标视察 UpdateScreenAndImageSizes: imageRectTransform.rect.size ({imageRectBaseSize}) is zero or negative. Parallax cannot work correctly. Ensure UI element has valid dimensions. Disabling script.", this);
            enabled = false;
            return;
        }
        
        Vector2 currentLocalScale = imageRectTransform.localScale;
        imageEffectiveSizeInCanvasUnits = new Vector2(imageRectBaseSize.x * currentLocalScale.x, imageRectBaseSize.y * currentLocalScale.y);
        
        // This log can be very verbose, consider moving it to where screenSizeChangedThisFrame is true or to a less frequent log.
        // Debug.Log($"鼠标视察 UpdateScreenAndImageSizes: Updated sizes. ScaleFactor: {lastCanvasScaleFactor}, ScreenPixels: {lastScreenSizeInPixels}, ScreenCanvasUnits: {screenSizeInCanvasUnits}, ImageRectBase: {imageRectBaseSize}, ImageEffectiveCanvasUnits: {imageEffectiveSizeInCanvasUnits}");
    }

    void Update()
    {
        if (!enabled || canvas == null) return; // Ensure canvas is still valid and script is enabled

        bool screenSizeChangedThisFrame = false;
        if (lastScreenSizeInPixels.x != Screen.width || lastScreenSizeInPixels.y != Screen.height || (canvas != null && lastCanvasScaleFactor != canvas.scaleFactor) )
        {
            UpdateScreenAndImageSizes();
            if (!enabled) return; // UpdateScreenAndImageSizes might disable the script
            screenSizeChangedThisFrame = true;
        }

        // imageEffectiveSize = new Vector2(imageOriginalSize.x * imageRectTransform.localScale.x, imageOriginalSize.y * imageRectTransform.localScale.y); // OLD LOGIC

        Vector2 inputPosition = Vector2.zero; 
        bool inputAvailable = false;
        Vector2 normalizedInputPosition = Vector2.zero; 

        if (isMobilePlatform)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (useGyro && Input.gyro.enabled)
            {
                normalizedInputPosition = new Vector2(Input.gyro.gravity.x, Input.gyro.gravity.y);
                inputAvailable = true;
            }
            else if (Input.touchCount > 0)
            {
                inputPosition = Input.GetTouch(0).position;
                inputAvailable = true;
                if (lastScreenSizeInPixels.x > 0 && lastScreenSizeInPixels.y > 0) { // Use pixel screen size for normalizing raw touch/mouse
                    normalizedInputPosition = new Vector2(
                        (inputPosition.x / lastScreenSizeInPixels.x - 0.5f) * 2f,
                        (inputPosition.y / lastScreenSizeInPixels.y - 0.5f) * 2f
                    );
                }
            }
            else
            {
                inputPosition = Input.mousePosition; 
                inputAvailable = true; 
                if (lastScreenSizeInPixels.x > 0 && lastScreenSizeInPixels.y > 0) { // Use pixel screen size
                    normalizedInputPosition = new Vector2(
                        (inputPosition.x / lastScreenSizeInPixels.x - 0.5f) * 2f,
                        (inputPosition.y / lastScreenSizeInPixels.y - 0.5f) * 2f
                    );
                }
            }
#else
            inputPosition = Input.mousePosition;
            inputAvailable = true;
            if (lastScreenSizeInPixels.x > 0 && lastScreenSizeInPixels.y > 0) { // Use pixel screen size
                 normalizedInputPosition = new Vector2(
                    (inputPosition.x / lastScreenSizeInPixels.x - 0.5f) * 2f,
                    (inputPosition.y / lastScreenSizeInPixels.y - 0.5f) * 2f
                );
            }
#endif
        }
        else 
        {
            inputPosition = Input.mousePosition;
            inputAvailable = true;
            if (lastScreenSizeInPixels.x > 0 && lastScreenSizeInPixels.y > 0) { // Use pixel screen size
                normalizedInputPosition = new Vector2(
                    (inputPosition.x / lastScreenSizeInPixels.x - 0.5f) * 2f,
                    (inputPosition.y / lastScreenSizeInPixels.y - 0.5f) * 2f
                );
            }
        }
        
        Vector2 curvedInputPosition = ApplyMovementCurve(normalizedInputPosition);

        // parallaxIntensity is now in Canvas Units
        Vector2 targetOffsetUnclamped = new Vector2(
            -curvedInputPosition.x * parallaxIntensity, 
            -curvedInputPosition.y * parallaxIntensity
        );

        // Calculations are now in Canvas Units
        float maxOffsetX = Mathf.Max(0, (imageEffectiveSizeInCanvasUnits.x - screenSizeInCanvasUnits.x) / 2f);
        float maxOffsetY = Mathf.Max(0, (imageEffectiveSizeInCanvasUnits.y - screenSizeInCanvasUnits.y) / 2f);
        
        // These explicit checks are redundant if Mathf.Max(0, ...) is used, but harmless.
        // if (imageEffectiveSizeInCanvasUnits.x <= screenSizeInCanvasUnits.x) maxOffsetX = 0;
        // if (imageEffectiveSizeInCanvasUnits.y <= screenSizeInCanvasUnits.y) maxOffsetY = 0;
        
        Vector2 targetOffset = new Vector2(
            Mathf.Clamp(targetOffsetUnclamped.x, -maxOffsetX, maxOffsetX),
            Mathf.Clamp(targetOffsetUnclamped.y, -maxOffsetY, maxOffsetY)
        );

        // initialAnchoredPosition and targetOffset are both in Canvas Units
        Vector2 finalTargetPosition = initialAnchoredPosition + targetOffset;

        imageRectTransform.anchoredPosition = Vector2.SmoothDamp(
            imageRectTransform.anchoredPosition,
            finalTargetPosition,
            ref currentVelocity,
            smoothTime
        );

        if (Time.frameCount % 60 == 0) 
        {
            if (screenSizeChangedThisFrame) Debug.Log($"鼠标视察 Update: Screen/Canvas size CHANGED. ScaleFactor: {lastCanvasScaleFactor}, ScreenPixels: {lastScreenSizeInPixels}, ScreenCanvasUnits: {screenSizeInCanvasUnits}, ImageEffectiveCanvasUnits: {imageEffectiveSizeInCanvasUnits}");
            Debug.Log($"鼠标视察 Update: ImageEffectiveInCU = {imageEffectiveSizeInCanvasUnits}, ScreenInCU = {screenSizeInCanvasUnits}, InputAvailable = {inputAvailable}");
            if(isMobilePlatform && useGyro && Input.gyro.enabled) Debug.Log($"鼠标视察 Update (Gyro): GyroData(gravity) = {Input.gyro.gravity}, normalizedInput = {normalizedInputPosition}");
            else Debug.Log($"鼠标视察 Update (Mouse/Touch): rawInputPos = {inputPosition} (pixels), normalizedInput = {normalizedInputPosition}");
            Debug.Log($"鼠标视察 Update: curvedInput = {curvedInputPosition}, targetOffsetUnclampedInCU = {targetOffsetUnclamped}");
            Debug.Log($"鼠标视察 Update: maxOffsetInCU = ({maxOffsetX}, {maxOffsetY}), targetOffsetClampedInCU = {targetOffset}");
            Debug.Log($"鼠标视察 Update: finalTargetPosInCU = {finalTargetPosition}, currentAnchorPosInCU = {imageRectTransform.anchoredPosition}");
        }
    }

    private Vector2 ApplyMovementCurve(Vector2 normalizedPosition)
    {
        float x = normalizedPosition.x;
        float y = normalizedPosition.y;

        x = Mathf.Clamp(x, -1f, 1f);
        y = Mathf.Clamp(y, -1f, 1f);

        switch (selectedMovementType)
        {
            case MovementType.Linear:
                break;
            case MovementType.Quadratic:
                x = x * Mathf.Abs(x);
                y = y * Mathf.Abs(y);
                break;
            case MovementType.Inverse: 
                x = Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 3); 
                y = Mathf.Sign(y) * Mathf.Pow(Mathf.Abs(y), 3);
                break;
            case MovementType.Logarithmic: 
                x = Mathf.Sign(x) * Mathf.Sqrt(Mathf.Abs(x)); 
                y = Mathf.Sign(y) * Mathf.Sqrt(Mathf.Abs(y));
                break;
        }
        return new Vector2(x, y);
    }

    void OnDisable()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (isMobilePlatform && useGyro && SystemInfo.supportsGyroscope)
        {
            Input.gyro.enabled = false;
            Debug.Log("鼠标视察 OnDisable: Gyroscope disabled via OnDisable.");
        }
#endif
    }

    void OnDestroy() 
    {
#if UNITY_ANDROID || UNITY_IOS
        if (isMobilePlatform && useGyro && Input.gyro.enabled) 
        {
            Input.gyro.enabled = false;
            Debug.Log("鼠标视察 OnDestroy: Gyroscope explicitly disabled on destroy.");
        }
#endif
    }
}
