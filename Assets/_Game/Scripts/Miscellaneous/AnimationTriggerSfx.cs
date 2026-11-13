using UnityEngine;

public class AnimationTriggerSfx : MonoBehaviour
{
    public AudioCue sfxToPlay;
    
    public void PlaySfx()
    {
        if (sfxToPlay != null)
        {
            GameManager.Instance.AudioManager.PlaySfx(sfxToPlay);
        }
    }
}
