using UnityEngine;
using System.IO;

public class AudioManager : MonoBehaviourSingleton<AudioManager>
{
    [Header("音量设置")]
    [Range(0f, 1f)]
    public float BGMVolume = 1f;
    [Range(0f, 1f)]
    public float SFXVolume = 1f;

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

    public void PlayBGM(AudioClip clip)
    {
        if (bgmSource != null && clip != null)
        {
            bgmSource.clip = clip;
            bgmSource.Play();
        }
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