using System;
using System.Collections;
using UnityEngine;

namespace _Game.Scripts.Enemies
{
    [Serializable]
    public class MosquitoMovement
    {
        [Header("Movement Settings")]
        [SerializeField] private MosquitoMovementSettings movementSettings;
        public bool bInterpolateMovement = true;
        public float interpolationDuration = 3.0f;
        
        [Header("Debug")]
        [SerializeField] private bool debugMovement = false;
        [SerializeField] private double debugTime = 0f;
        
        // Current per-instance zigzag values (we keep these separate from the ScriptableObject so
        // each Mosquito can smoothly interpolate independently)
        private float _currentHorizontalFrequency;
        private float _currentHorizontalAmplitude;
        private float _currentVerticalFrequency;
        private float _currentVerticalAmplitude;
        
        private float _phaseOffset;
        private Mosquito _mosquitoOwner;
        private Coroutine _interpolationCoroutine;
        
        public void OnStart(float phaseOffset, Mosquito mosquitoOwner)
        {
            _phaseOffset = phaseOffset;
            _mosquitoOwner = mosquitoOwner;

            _currentHorizontalFrequency = movementSettings.HorizontalZigzagFrequency;
            _currentHorizontalAmplitude = movementSettings.HorizontalZigzagAmplitude;
            _currentVerticalFrequency = movementSettings.VerticalZigzagFrequency;
            _currentVerticalAmplitude = movementSettings.VerticalZigzagAmplitude;
        }

        public Vector2 Movement(double time)
        {
            float x = 0;
            float y = 0;
            double t = debugMovement ? debugTime : time;

            if(bInterpolateMovement)
            {
                x = (float)(Math.Sin(t * _currentHorizontalFrequency + _phaseOffset)) * _currentHorizontalAmplitude;
                y = (float)(Math.Cos(t * _currentVerticalFrequency + _phaseOffset)) * _currentVerticalAmplitude;
            }
            else
            {
                x = (float)(Math.Sin(t * movementSettings.HorizontalZigzagFrequency + _phaseOffset)) * movementSettings.HorizontalZigzagAmplitude;
                y = (float)(Math.Cos(t * movementSettings.VerticalZigzagFrequency + _phaseOffset)) * movementSettings.VerticalZigzagAmplitude;
            }

            return new Vector2(x, y);
        }
        
        public void SwitchMovementSettings(MosquitoMovementSettings newSettings)
        {
            if(bInterpolateMovement)
            {
                if(_interpolationCoroutine != null)
                {
                    _mosquitoOwner.StopCoroutine(_interpolationCoroutine);
                }
                _interpolationCoroutine = _mosquitoOwner.StartCoroutine(InterpolateToNewZigZagSettings(newSettings));
            }
            else
            {
                movementSettings = newSettings;
            }
        }
        
        private IEnumerator InterpolateToNewZigZagSettings(MosquitoMovementSettings newSettings)
        {
            // Start from the current per-instance values and interpolate towards the target settings
            float initialHorizontalAmplitude = _currentHorizontalAmplitude;
            float initialVerticalAmplitude = _currentVerticalAmplitude;

            float targetHorizontalAmplitude = newSettings.HorizontalZigzagAmplitude;
            float targetVerticalAmplitude = newSettings.VerticalZigzagAmplitude;

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
            _currentHorizontalFrequency = newSettings.HorizontalZigzagFrequency;
            _currentVerticalFrequency = newSettings.VerticalZigzagFrequency;
        
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
            
            movementSettings = newSettings; // Update the ScriptableObject reference to the new settings
            
            _interpolationCoroutine = null; // Clear the coroutine reference
        }
    }
}