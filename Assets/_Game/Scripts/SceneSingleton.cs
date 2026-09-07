using System;
using Photon.Pun;
using UnityEngine;

namespace _Game.Scripts
{
    public abstract class SceneSingleton<T> : MonoBehaviourPun where T : MonoBehaviourPun
    {
        private static T _uniqueInstance;
        private static bool bIsRunning = true;

        public static T Instance
        {
            get
            {
                if (_uniqueInstance == null && bIsRunning)
                {
                    _uniqueInstance = FindAnyObjectByType<T>();
                    if (_uniqueInstance == null)
                    {
                        Debug.LogWarning($"No instance of singleton {typeof(T).Name} found in the scene!");
                    }
                }
                return _uniqueInstance;
            }
            
            private set 
            {
                if (_uniqueInstance == null)
                {
                    _uniqueInstance = value;
                }
                else if(_uniqueInstance != value)
                {
#if UNITY_EDITOR
                    Debug.LogWarning($"Trying to set a second instance of singleton {typeof(T).Name}! \n");
#endif
                    DestroyImmediate(value.gameObject);
                }
            }
        }
        
        

        protected virtual void Awake()
        {
            Instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (_uniqueInstance == this)
                _uniqueInstance = null;
        }

        private void OnApplicationQuit()
        {
            bIsRunning = false;
        }
    }
}