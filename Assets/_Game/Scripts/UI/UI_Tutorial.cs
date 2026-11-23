using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_Tutorial : MonoBehaviour
{
    [Header("References")]
    public GameObject tutorialWindow;
    public Button tutorialButton;

    public Sprite buttonOnImage;
    public Sprite buttonOffImage;
    
    private void Awake()
    {
        if(GameManager.AlreadyShownTutorial)
        {
            HideTutorial();
        }
        else
        {
            ShowTutorial();
            GameManager.AlreadyShownTutorial = true;
        }
    }
    
    public void ShowTutorial()
    {
        tutorialWindow.gameObject.SetActive(true);
        tutorialButton.image.sprite = buttonOffImage;
        tutorialButton.interactable = false;
    }
    
    public void HideTutorial()
    {
        tutorialWindow.gameObject.SetActive(false);
        tutorialButton.image.sprite = buttonOnImage;
        tutorialButton.interactable = true;
    }
}
