using UnityEngine;

public class IPersistentSinglenton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _uniqueInstance;
    private static bool bIsRunning = true;

    public static T Instance {

        get
        {
            if (_uniqueInstance == null && bIsRunning)
            {
                _uniqueInstance = FindAnyObjectByType<T>();
                if (_uniqueInstance == null)
                {
                    GameObject SingletonPrefab = Resources.Load<GameObject>(typeof(T).Name);
                    if (SingletonPrefab != null)
                    {
                        GameObject singletonGameObject = Instantiate<GameObject>(SingletonPrefab);

                        if (singletonGameObject != null)
                            _uniqueInstance = singletonGameObject.GetComponent<T>();
                    }
                    
                    if(_uniqueInstance == null)
                    {
                        _uniqueInstance = new GameObject($"[Singleton] {typeof(T).Name}").AddComponent<T>();
                    }
                }
            }
            return _uniqueInstance;
        }


        private set 
        {
            if (_uniqueInstance == null)
            {
                _uniqueInstance = value;
                DontDestroyOnLoad(_uniqueInstance.gameObject);
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

    private void OnDestroy()
    {
        if(_uniqueInstance == this)
        {
            _uniqueInstance = null;
        }
    }

    private void OnApplicationQuit()
    {
        bIsRunning = false;
    }
}
