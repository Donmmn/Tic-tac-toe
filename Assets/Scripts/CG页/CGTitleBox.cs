using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System; // Required for Action

public class CGTitleBox : MonoBehaviour
{
    [SerializeField] Image ImageShow;
    [SerializeField] Image Locker;
    [SerializeField] Text Title;
    [SerializeField] Button clickButton; // Button to make the whole box clickable

    public Ending AssociatedEnding { get; private set; }
    private bool isUnlocked;

    public event Action<Ending, bool> OnClicked;

    void Awake()
    {
        if (clickButton == null)
        {
            clickButton = GetComponent<Button>();
            if (clickButton == null)
            {
                clickButton = gameObject.AddComponent<Button>(); // Add button if not present
                // Optional: Customize the added button's transition, e.g., set to None
                 if (clickButton.targetGraphic == null) clickButton.targetGraphic = ImageShow; // Optional: assign a graphic for visual feedback
            }
        }
        clickButton.onClick.AddListener(HandleClick);
    }

    public void Initialized(string imgPath, bool unlockStatus, string titleText, Ending endingData)
    {
        AssociatedEnding = endingData;
        isUnlocked = unlockStatus;

        Title.text = titleText;
        Locker.gameObject.SetActive(!isUnlocked);

        if (ImageShow != null)
            {
            if (!string.IsNullOrEmpty(imgPath) && isUnlocked)
            {
                Sprite sprite = Resources.Load<Sprite>(imgPath);
                if (sprite != null)
                {
                    ImageShow.sprite = sprite;
                    ImageShow.color = Color.white; // Ensure full visibility for unlocked
                    ImageShow.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"CGTitleBox: Thumbnail not found at Resources path: {imgPath} for title: {titleText}");
                    ImageShow.gameObject.SetActive(false);
                }
            }
            else if (!isUnlocked)
            {
                ImageShow.color = new Color(0.5f, 0.5f, 0.5f, 1); // Dim if locked
                if (!string.IsNullOrEmpty(imgPath)) // Still try to load image for dimmed effect if path exists
                {
                     Sprite sprite = Resources.Load<Sprite>(imgPath);
                     if (sprite != null) ImageShow.sprite = sprite;
                     else ImageShow.gameObject.SetActive(false); 
                }
                else { ImageShow.gameObject.SetActive(false); }
                ImageShow.gameObject.SetActive(true); // Show dimmed image
            }
            else
            {
                ImageShow.gameObject.SetActive(false);
            }
        }
    }

    private void HandleClick()
    {
        if (AssociatedEnding != null)
        {
            OnClicked?.Invoke(AssociatedEnding, isUnlocked);
        }
        else
        {
            Debug.LogError("CGTitleBox: AssociatedEnding is null. Cannot invoke OnClicked event.");
        }
    }

    void OnDestroy()
    {
        if (clickButton != null)
        {
            clickButton.onClick.RemoveListener(HandleClick);
        }
    }   
}