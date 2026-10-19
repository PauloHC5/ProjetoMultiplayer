using UnityEngine;

[CreateAssetMenu(fileName = "QueenBehaviour", menuName = "Scriptable Objects/QueenBehaviour")]
public class QueenBehaviour : ScriptableObject
{
    [Header("Behaviour Settings")]
    [SerializeField] private LissajouCurveSettings[] curveMovementSettings;
    [SerializeField] private float minTimeToChangeMovement = 5f;
    [SerializeField] private float maxTimeToChangeMovement = 10f;
    
    private int _previousMovementSettingsIndex = -1;
    
    public float TimeToChangeBehaviour => Random.Range(minTimeToChangeMovement, maxTimeToChangeMovement);
    
    public LissajouCurveSettings GetMovementSettings(int index)
    {
        if (curveMovementSettings == null || curveMovementSettings.Length == 0)
        {
            Debug.LogWarning("Curve movement settings array is empty.");
            return null;
        }

        if (index < 0 || index >= curveMovementSettings.Length)
        {
            Debug.LogWarning($"Index {index} is out of bounds for curve movement settings array.");
            return null;
        }

        return curveMovementSettings[index];
    }

    public int GetNextMovementSettingsIndex(int currentBehaviourIndex)
    {
        if (curveMovementSettings == null || curveMovementSettings.Length == 0)
        {
            Debug.LogWarning("Curve movement settings array is empty.");
            return -1;
        }

        int newIndex = (currentBehaviourIndex + 1) % curveMovementSettings.Length;

        // Ensure the new index is not the same as the previous one
        if (newIndex == _previousMovementSettingsIndex)
        {
            newIndex = (newIndex + 1) % curveMovementSettings.Length;
        }

        _previousMovementSettingsIndex = newIndex;
        return newIndex;
    }
    
    public int GetRandomMovementSettingsIndex()
    {
        if (curveMovementSettings == null || curveMovementSettings.Length == 0)
        {
            Debug.LogWarning("Curve movement settings array is empty.");
            return -1;
        }

        int newIndex;
        
        if(curveMovementSettings.Length == 1)
        {
            return 0; // Only one option available
        }
        
        do
        {
            newIndex = Random.Range(0, curveMovementSettings.Length);
        } while (newIndex == _previousMovementSettingsIndex);

        _previousMovementSettingsIndex = newIndex;
        return newIndex;
    }
}
