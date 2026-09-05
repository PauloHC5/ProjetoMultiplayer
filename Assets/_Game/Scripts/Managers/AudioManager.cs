using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public delegate void AudioStop();

[Serializable]
public class AudioManager
{
    [SerializeField]
    private AudioMixer _audioMixer;

    [SerializeField]
    private AudioSource _musicSource;

    [SerializeField]
    private AudioSource _sfxSource;

    [SerializeField]
    private int _sfxPoolSize = 5;

    private List<AudioSource> _sfxSourcePool;
    private GameObject _audioSourceInstance;    

    public void InitializePool()
    {
        if (!ValidateMixerAndSources()) return;
        
        _sfxSourcePool = new List<AudioSource>(_sfxPoolSize);
        GameObject audioSourceInstance = new GameObject("AudioSourceInstance");
        audioSourceInstance.transform.SetParent(GameManager.Instance.transform);        
        for (int i = 0; i < _sfxPoolSize; i++)
        {
            AudioSource audioSource = audioSourceInstance.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.outputAudioMixerGroup = _audioMixer.FindMatchingGroups("SFX")[0];
            _sfxSourcePool.Add(audioSource);
        }

    }
    
    public bool ValidateMixerAndSources()
    {
        if (_audioMixer == null)
        {
            Debug.LogError("AudioMixer is not assigned in AudioManager.");
            return false;
        }

        if (_musicSource == null)
        {
            Debug.LogError("Music AudioSource is not assigned in AudioManager.");
            return false;
        }

        if (_sfxSource == null)
        {
            Debug.LogError("SFX AudioSource is not assigned in AudioManager.");
            return false;
        }

        return true;
    }

    public void PlayMusic(AudioClip music, bool bLoop = false)
    {
        _musicSource.Stop();
        _musicSource.loop = bLoop;
        _musicSource.volume = 1f;
        _musicSource.clip = music;
        _musicSource.Play();
    }

    public void StopMusic()
    {
        if (_musicSource.isPlaying)          
            _musicSource.Stop();         
    }

    public void MusicPause(bool bIsPause = true)
    {
        if (bIsPause)
            _musicSource.Pause();
        else
            _musicSource.UnPause();
    }    

    public bool IsMusicPlaying()
    {
        return _musicSource.isPlaying;
    }

    public void MusicFadeIn(AudioClip music, float time, bool bLoop = true)
    {
        if (_musicSource.isPlaying)
            _musicSource.Stop();

        _musicSource.loop = bLoop;
        _musicSource.volume = 0f;
        _musicSource.clip = music;
        _musicSource.Play();

        GameManager.Instance.StartCoroutine(InternalFadeMusic(true, time));
    }    

    public void MusicFadeOut(float time)
    {
        if (_musicSource.isPlaying)
            GameManager.Instance.StartCoroutine(InternalFadeMusic(false, time));
    }

    public void SetMixerMusicVolume(float volume)
    {
        float dbVolume = Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
        _audioMixer.SetFloat("MusicVolume", dbVolume);
    }

    private IEnumerator InternalFadeMusic(bool bIsFadeIn, float time)
    {
        float currentVolume = _musicSource.volume;
        float deltaTime = 0.0f;
        float targetVolume = bIsFadeIn ? 1f : 0f;

        while (deltaTime < time)
        {
            deltaTime += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(currentVolume, targetVolume, deltaTime / time);
            yield return null;
        }        

        _musicSource.volume = targetVolume;
    }

    public void PlaySfx(AudioClip clip, float pitch, float volume)
    {
        InternalPlaySfx(clip, pitch, volume);
    }    

    public void PlaySfx(AudioCue sfx)
    {
        InternalPlaySfx(sfx.GetSample(), sfx.GetPitch(), sfx.GetVolume());
    }

    public AudioStop PlaySfxInLoop(AudioCue sfx)
    {
        return InternalPlaySfx(sfx.GetSample(), sfx.GetPitch(), sfx.GetVolume(), true);        
    }

    public void StopSfx()
    {
        if (_sfxSource.isPlaying)
            _sfxSource.Stop();
    }

    public void PauseSfx(bool bIsPause = true)
    {
        if (bIsPause)
            _sfxSource.Pause();
        else
            _sfxSource.UnPause();
    }

    public AudioStop InternalPlaySfx(AudioClip clip, float pitch, float volume, bool bLoop = false)
    {
        int index = -1;
        if(_sfxSource.isPlaying)
        {
            for (int i = 0; i < _sfxSourcePool.Count; i++)
            {
                if (!_sfxSourcePool[i].isPlaying)
                {
                    index = i;
                    break;
                }
            }
            if (index == -1)
            {
                // Preciso expandir o pool
                index = CreateAudioSource();

            }
        }

        AudioSource audioSource = index == -1 ? _sfxSource : _sfxSourcePool[index];

        audioSource.pitch = pitch;
        audioSource.volume = volume;
        audioSource.loop = bLoop;

        if(bLoop)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
        else
        {
            audioSource.PlayOneShot(clip);
        }

        AudioStop result = () =>
        {
            _sfxSourcePool[index].Stop();
        };


        return result;
    }    

    private int CreateAudioSource()
    {
        AudioSource audioSource = _audioSourceInstance.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.outputAudioMixerGroup = _audioMixer.FindMatchingGroups("SFX")[0];
        _sfxSourcePool.Add(audioSource);
        return _sfxSourcePool.Count - 1;
    }
}
