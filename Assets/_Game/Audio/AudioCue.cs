using UnityEngine;

[CreateAssetMenu(fileName = "AudioCue", menuName = "Game Audio/AudioCue")]
public class AudioCue : ScriptableObject
{
    [SerializeField]
    private AudioClip[] _samples;

    [Space]

    [Header("Procedural Params")]

    [SerializeField]
    [Range(0f, 3f)]
    private float _minPitch;

    [SerializeField]
    [Range(0f, 3f)] 
    private float _Maxpitch;

    [SerializeField]
    [Range(0f, 1f)]
    private float _minVolume;

    [SerializeField]
    [Range(0f, 1f)]
    private float _maxVolume;

    public AudioClip GetSample()
    {
        return _samples[Random.Range(0, _samples.Length)];
    }

    public float GetPitch()
    {
        return Random.Range(_minPitch, _Maxpitch);
    }

    public float GetVolume()
    {
        return Random.Range(_minVolume, _maxVolume);
    }
}