using UnityEngine;
using System.IO;
using System.Collections; // Needed for Coroutines

public class AudioManager : MonoBehaviourSingleton<AudioManager>
{
    [Header("音量设置")]
    [Range(0f, 1f)]
    public float BGMVolume = 1f;
    [Range(0f, 1f)]
    public float SFXVolume = 1f;

    [Header("主BGM设置")]
    [Tooltip("直接在此处设置主BGM的AudioClip")][SerializeField] private AudioClip mainBGMClipAsset; // 新增：在Inspector中设置的主BGM
    private AudioClip mainBGMClip; // 内部使用的实际播放的clip
    private Coroutine bgmFadeCoroutine; // 用于控制BGM渐变

    [Header("音效设置")]
    [Tooltip("按钮点击音效")]
    public AudioClip buttonClickSound;
    [Tooltip("棋盘点击音效")]
    public AudioClip boardClickSound;
    [Tooltip("获胜音效")]
    public AudioClip winSound;
    [Tooltip("平局音效")]
    public AudioClip drawSound;
    [Tooltip("重置操作音效")]
    public AudioClip resetActionSound;
    [Tooltip("角色说话音效")]
    public AudioClip characterSpeechSound;

    private const string VolumeConfigPath = "Config/volume_config";
    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private AudioSource characterSource;

    protected override void Awake()
    {
        base.Awake();
        LoadVolumeSettings();
        InitializeAudioSources();
    }

    private void InitializeAudioSources()
    {
        // 创建BGM音源
        GameObject bgmObj = new GameObject("BGM Source");
        bgmObj.transform.SetParent(transform);
        bgmSource = bgmObj.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = BGMVolume;

        // 尝试加载并播放主BGM
        if (mainBGMClipAsset != null)
        {
            mainBGMClip = mainBGMClipAsset; // 将Inspector中设置的clip赋给内部变量
            PlayBGM(mainBGMClip, true, 0f); // 初始播放，可无渐变
            Debug.Log($"主BGM '{mainBGMClip.name}' 已通过Inspector设置并开始播放。");
        }
        else
        {
            Debug.LogWarning("主BGM AudioClip 未在 AudioManager Inspector 中设置。");
        }

        // 创建音效音源
        GameObject sfxObj = new GameObject("SFX Source");
        sfxObj.transform.SetParent(transform);
        sfxSource = sfxObj.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = SFXVolume;

        // 创建角色音效音源
        GameObject characterObj = new GameObject("Character Source");
        characterObj.transform.SetParent(transform);
        characterSource = characterObj.AddComponent<AudioSource>();
        characterSource.loop = false;
        characterSource.playOnAwake = false;
        characterSource.volume = SFXVolume;
    }

    public void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
        {
            bgmSource.volume = BGMVolume;
        }
        SaveVolumeSettings();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = SFXVolume;
        }
        if (characterSource != null)
        {
            characterSource.volume = SFXVolume;
        }
        SaveVolumeSettings();
    }

    public void PlayBGM(AudioClip clip, bool loop = true, float fadeDuration = 1f)
    {
        if (bgmSource == null || clip == null) return;

        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
        }
        bgmFadeCoroutine = StartCoroutine(FadeSwitchBGM(clip, loop, fadeDuration));
    }

    private IEnumerator FadeSwitchBGM(AudioClip newClip, bool loop, float duration)
    {
        float startVolume = bgmSource.volume; // 当前实际音量，可能已被渐变影响
        float targetOverallVolume = BGMVolume; // 全局BGM目标音量

        // 渐隐当前BGM (如果正在播放且有音频片段)
        if (bgmSource.isPlaying && bgmSource.clip != null && duration > 0.01f) // 只有当duration足够大时才执行渐变
        {
            float fadeOutTime = duration / 2f;
            float timer = 0f;
            while (timer < fadeOutTime)
            {
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / fadeOutTime);
                timer += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime 以避免受 Time.timeScale 影响
                yield return null;
            }
            bgmSource.Stop();
            bgmSource.volume = 0; // 确保完全静音
        }
        else if (bgmSource.isPlaying) // 如果duration太短，直接停止
        {
            bgmSource.Stop();
        }

        // 切换到新的BGM并设置循环
        bgmSource.clip = newClip;
        bgmSource.loop = loop;
        bgmSource.Play();

        // 渐显新的BGM
        if (duration > 0.01f)
        {
            float fadeInTime = duration / 2f;
            float timer = 0f;
            while (timer < fadeInTime)
            {
                bgmSource.volume = Mathf.Lerp(0f, targetOverallVolume, timer / fadeInTime);
                timer += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        bgmSource.volume = targetOverallVolume; // 确保达到目标音量
        bgmFadeCoroutine = null;
    }

    // 新增：播放主BGM的方法
    public void PlayMainBGM(float fadeDuration = 1f)
    {
        if (mainBGMClip != null)
        {
            // 只有当当前播放的不是主BGM，或者BGM没有在播放时，才切换
            if (bgmSource.clip != mainBGMClip || !bgmSource.isPlaying)
            {
                PlayBGM(mainBGMClip, true, fadeDuration);
                Debug.Log("开始播放主BGM。");
            }
            else
            {
                Debug.Log("主BGM已在播放，无需切换。");
            }
        }
        else
        {
            Debug.LogWarning("主BGM片段 (mainBGMClip) 未加载，无法播放主BGM。");
        }
    }

    // 新增：检查当前是否正在播放主BGM
    public bool IsPlayingMainBGM()
    {
        return bgmSource != null && bgmSource.clip == mainBGMClip && bgmSource.isPlaying;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, SFXVolume);
        }
    }

    // 新增：播放角色音效
    public void PlayCharacterSound(AudioClip clip)
    {
        if (characterSource != null && clip != null)
        {
            characterSource.PlayOneShot(clip, SFXVolume);
        }
    }

    // 新增：播放按钮点击音效
    public void PlayButtonClickSound()
    {
        PlaySFX(buttonClickSound);
    }

    // 新增：播放棋盘点击音效
    public void PlayBoardClickSound()
    {
        PlaySFX(boardClickSound);
    }

    // 新增：播放获胜音效
    public void PlayWinSound()
    {
        PlaySFX(winSound);
    }

    // 新增：播放平局音效
    public void PlayDrawSound()
    {
        PlaySFX(drawSound);
    }

    // 新增：播放重置音效
    public void PlayResetSound()
    {
        PlaySFX(resetActionSound);
    }

    public void PlayCharacterSpeechSound()
    {
        PlayCharacterSound(characterSpeechSound);
    }

    private void SaveVolumeSettings()
    {
        VolumeSettings settings = new VolumeSettings
        {
            BGMVolume = this.BGMVolume,
            SFXVolume = this.SFXVolume
        };

        string json = JsonUtility.ToJson(settings, true);
        string resourcesFolderPath = Path.Combine(Application.dataPath, "Resources");
        string targetDirectoryPath = Path.Combine(resourcesFolderPath, "Config");
        string filePath = Path.Combine(targetDirectoryPath, "volume_config.json");

        try
        {
            if (!Directory.Exists(targetDirectoryPath))
            {
                if (!Directory.Exists(resourcesFolderPath))
                {
                    Directory.CreateDirectory(resourcesFolderPath);
                }
                Directory.CreateDirectory(targetDirectoryPath);
            }
            File.WriteAllText(filePath, json);
            Debug.Log($"音量设置已保存到 {filePath}");

            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            #endif
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"保存音量设置时发生错误: {ex.Message}");
        }
    }

    private void LoadVolumeSettings()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(VolumeConfigPath);
        if (configAsset != null)
        {
            try
            {
                VolumeSettings settings = JsonUtility.FromJson<VolumeSettings>(configAsset.text);
                if (settings != null)
                {
                    BGMVolume = settings.BGMVolume;
                    SFXVolume = settings.SFXVolume;
                    Debug.Log("音量设置已加载");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"加载音量设置时发生错误: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("未找到音量配置文件，使用默认设置");
            // 使用默认值
            BGMVolume = 1f;
            SFXVolume = 1f;
            SaveVolumeSettings(); // 创建默认配置文件
        }
    }
}

[System.Serializable]
public class VolumeSettings
{
    public float BGMVolume;
    public float SFXVolume;
} 