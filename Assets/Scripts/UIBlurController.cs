using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIBlurController : MonoBehaviour
{
    [Tooltip("控制模糊程度，值越大越模糊")]
    [Range(0f, 10f)] // 您可以根据需要调整这个范围
    public float blurSize = 1f;

    private Image targetImage;
    private Material imageMaterial;

    void Awake()
    {
        targetImage = GetComponent<Image>();
        if (targetImage != null)
        {
            // 获取Image组件的材质实例，确保我们修改的是这个Image的材质，而不是共享的材质资源
            imageMaterial = Instantiate(targetImage.material);
            targetImage.material = imageMaterial;
        }
        else
        {
            Debug.LogError("UIBlurController: 找不到Image组件！");
        }
    }

    void OnEnable()
    {
        // 确保材质在对象启用时被正确设置
        if (targetImage != null && imageMaterial == null)
        {
            imageMaterial = Instantiate(targetImage.material);
            targetImage.material = imageMaterial;
        }
        UpdateBlurSize();
    }

    void Update()
    {
        // 每一帧更新模糊程度，如果您希望通过代码在运行时动态修改blurSize并立即看到效果
        // 如果您只希望通过Inspector修改，并在Start时应用一次，可以将UpdateBlurSize()的调用移到Start或Awake
        // 或者提供一个public方法来手动调用
        UpdateBlurSize();
    }

    /// <summary>
    /// 更新模糊程度
    /// </summary>
    public void SetBlurAmount(float amount)
    {
        blurSize = amount;
        UpdateBlurSize();
    }

    private void UpdateBlurSize()
    {
        if (imageMaterial != null)
        {
            imageMaterial.SetFloat("_Size", blurSize);
        }
    }

    // 可选：在编辑器中更改blurSize时实时更新
#if UNITY_EDITOR
    void OnValidate()
    {
        // OnValidate会在脚本加载或Inspector中的值被修改时调用
        // 确保在编辑器模式下，材质也能被正确获取和更新
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetImage != null)
        {
            // 在编辑器模式下，我们可能希望直接修改共享材质的属性以便预览
            // 但为了安全起见，最好还是获取材质实例
            // 如果targetImage.material是null或者不是预期的shader，这里可能会出问题
            // 更稳妥的做法是在运行时才Instantiate
            if (Application.isPlaying)
            {
                 if (imageMaterial == null && targetImage.material != null) {
                    imageMaterial = Instantiate(targetImage.material);
                    targetImage.material = imageMaterial;
                 }
            } else {
                // 编辑器模式下，如果想实时预览，可能需要不同的处理
                // 但直接修改targetImage.material可能影响原始MaterialAsset
                // 这里暂时只在运行时实例化和更新，编辑器预览依赖于材质本身的默认值或手动调整
                // 如果希望编辑器中也能实时通过这个脚本控制，需要更复杂的处理
                // 来确保不会错误地修改Project中的Material Asset
                // 一个简单的做法是，如果 targetImage.material.shader.name 和我们的shader匹配
                // 并且不是一个共享的材质实例（例如，通过name来判断是否是 "高斯模糊shader Instance"）
                // 才去设置。但更推荐的方式是运行时实例化和赋值。
                // 为了简单起见，这里的OnValidate仅在运行时更新，或者提供一个按钮来手动更新。
                 if (imageMaterial != null) // 确保运行时获取的material实例被更新
                 {
                    imageMaterial.SetFloat("_Size", blurSize);
                 } else if (targetImage.material != null && targetImage.material.HasProperty("_Size"))
                 {
                    // 如果还没有运行时实例，尝试直接更新Image上的材质（这可能修改项目资源）
                    // 仅当材质有_Size属性时操作
                    targetImage.material.SetFloat("_Size", blurSize);
                 }
            }
        }
    }
#endif

    void OnDestroy()
    {
        // 清理创建的材质实例，防止内存泄漏
        if (imageMaterial != null)
        {
            Destroy(imageMaterial);
        }
    }
} 