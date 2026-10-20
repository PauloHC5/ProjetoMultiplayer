using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _Game.Scripts.Enemies
{
    [Serializable]
    public class MosquitoZigZagMovement
    {
        [Header("ZigZag Movement Settings")]
        [SerializeField] private MosquitoZigZagMovementSettings zigZagMovementSettings;
        public bool bInterpolateMovement = true;
        public float interpolationDuration = 3.0f;
        
        [Header("Debug")]
        [SerializeField] private bool debugZigZagMovement = false;
        [SerializeField] private double debugTime = 0f;
        
        // Current per-instance zigzag values (we keep these separate from the ScriptableObject so
        // each Mosquito can smoothly interpolate independently)
        private float _currentHorizontalFrequency;
        private float _currentHorizontalAmplitude;
        private float _currentVerticalFrequency;
        private float _currentVerticalAmplitude;
        
        public float phaseOffset;
        
        private MosquitoBase _mosquitoOwner;
        private Coroutine _interpolationCoroutine;
        private MosquitoZigZagMovementSettings _targetSettings;
        
        public void OnStart(MosquitoBase mosquitoOwner)
        {
            if(zigZagMovementSettings == null) return;
            
            _mosquitoOwner = mosquitoOwner;

            _currentHorizontalFrequency = zigZagMovementSettings.HorizontalZigzagFrequency;
            _currentHorizontalAmplitude = zigZagMovementSettings.HorizontalZigzagAmplitude;
            _currentVerticalFrequency = zigZagMovementSettings.VerticalZigzagFrequency;
            _currentVerticalAmplitude = zigZagMovementSettings.VerticalZigzagAmplitude;
        }

        public Vector2 Movement(double time)
        {
            if(zigZagMovementSettings == null) return Vector2.zero;
            
            float x = 0;
            float y = 0;
            double t = debugZigZagMovement ? debugTime : time;

            if(bInterpolateMovement)
            {
                x = (float)(Math.Sin(t * _currentHorizontalFrequency + phaseOffset)) * _currentHorizontalAmplitude;
                y = (float)(Math.Cos(t * _currentVerticalFrequency + phaseOffset)) * _currentVerticalAmplitude;
            }
            else
            {
                x = (float)(Math.Sin(t * zigZagMovementSettings.HorizontalZigzagFrequency + phaseOffset)) * zigZagMovementSettings.HorizontalZigzagAmplitude;
                y = (float)(Math.Cos(t * zigZagMovementSettings.VerticalZigzagFrequency + phaseOffset)) * zigZagMovementSettings.VerticalZigzagAmplitude;
            }

            return new Vector2(x, y);
        }
        
        public void SwitchMovementSettings(MosquitoZigZagMovementSettings newSettings)
        {
            if(newSettings == zigZagMovementSettings) return;
            
            _targetSettings = newSettings;
            
            if(bInterpolateMovement && _mosquitoOwner != null)
            {
                if(_interpolationCoroutine != null)
                {
                    _mosquitoOwner.StopCoroutine(_interpolationCoroutine);
                }
                _interpolationCoroutine = _mosquitoOwner.StartCoroutine(InterpolateToNewZigZagSettings(newSettings));
            }
            else
            {
                zigZagMovementSettings = newSettings;
            }
        }
        
        private IEnumerator InterpolateToNewZigZagSettings(MosquitoZigZagMovementSettings newSettings)
        {
            // Start from the current per-instance values and interpolate towards the target settings
            float initialHorizontalAmplitude = _currentHorizontalAmplitude;
            float initialVerticalAmplitude = _currentVerticalAmplitude;

            float targetHorizontalAmplitude = newSettings != null ? newSettings.HorizontalZigzagAmplitude : 0f;
            float targetVerticalAmplitude = newSettings != null ? newSettings.VerticalZigzagAmplitude : 0f;

            float elapsedTime = 0f;

            // Interpolate amplitude to 0
            while (elapsedTime < interpolationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / interpolationDuration);
            
                // Lerp per-instance current values towards 0
                _currentHorizontalAmplitude = Mathf.Lerp(initialHorizontalAmplitude, 0f, t);
                _currentVerticalAmplitude = Mathf.Lerp(initialVerticalAmplitude, 0f, t);
            
                yield return null;
            }
        
            // change the frequency to the new settings
            _currentHorizontalFrequency = newSettings != null ? newSettings.HorizontalZigzagFrequency : 0f;
            _currentVerticalFrequency = newSettings != null ? newSettings.VerticalZigzagFrequency : 0f;
        
            elapsedTime = 0f; // Reset elapsed time for the next interpolation

            // Interpolate amplitudes to the new settings
            while (elapsedTime < interpolationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / interpolationDuration);

                // Lerp per-instance current values
                _currentHorizontalAmplitude = Mathf.Lerp(0f, targetHorizontalAmplitude, t);
                _currentVerticalAmplitude = Mathf.Lerp(0f, targetVerticalAmplitude, t);

                yield return null;
            }

            // Ensure the final values are set
            // Ensure the final per-instance values are set to the targets
            _currentHorizontalAmplitude = targetHorizontalAmplitude;
            _currentVerticalAmplitude = targetVerticalAmplitude;
            
            zigZagMovementSettings = newSettings; // Update the ScriptableObject reference to the new settings
            
            _interpolationCoroutine = null; // Clear the coroutine reference
        }
        
        public void OnValidate()
        {
            // Só faz sentido em Play Mode, depois do OnStart
            if (!Application.isPlaying || _mosquitoOwner == null || zigZagMovementSettings == null) return;

            // O Inspector trocou o asset: dispara a interpolação para o novo
            if (zigZagMovementSettings != _targetSettings)
            {
                SwitchMovementSettings(zigZagMovementSettings);
            }
        }
    }
}