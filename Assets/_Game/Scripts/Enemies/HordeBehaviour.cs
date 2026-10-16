using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

[CreateAssetMenu(fileName = "HordeBehaviour", menuName = "Scriptable Objects/HordeBehaviour")]
public class HordeBehaviour : ScriptableObject
{
    protected enum BehaviourType
    {
        AllHordeMosquitos,
        RandomRowOrColumnMosquitos,
        OnlyInfectedMosquitos,
    }
    
    [Header("Behaviour Settings")]
    [SerializeField] private LissajouCurveSettings[] curveMovementSettings;
    [SerializeField] private BehaviourType behaviourType;
    [SerializeField] private float minBehaviourStartDelay = 5f;
    [SerializeField] private float maxBehaviourStartDelay = 5f;
    [SerializeField] private float minBehaviourDuration = 3f;
    [SerializeField] private float maxBehaviourDuration = 3f;
    [SerializeField] private float timeToReorder = 0.5f;

    [Header("Behaviour Buffs")] 
    [SerializeField] private bool bRapidFire = false;
    [SerializeField] private float rapidFireTime = 1.5f;
    
    public float BehaviourStartDelay => UnityEngine.Random.Range(minBehaviourStartDelay, maxBehaviourStartDelay);
    public float BehaviourDuration => UnityEngine.Random.Range(minBehaviourDuration, maxBehaviourDuration);
    public float TimeToReorder => timeToReorder;
    public bool IsRapidFire => bRapidFire;
    public float RapidFireTime => rapidFireTime;


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
    

    public void ChoseMosquitoBehaviour(int hordeColumns, int hordeRows, Dictionary<MosquitosHorde.HordePositionKey, Mosquito> mosquitosDictionary,
        out int movementSettingsIndex, out MosquitosHorde.HordePositionKey[] hordeMosquitosChoosen)
    {
        // Verificar se o dicionário de mosquitos está vazio
        if (mosquitosDictionary.Count == 0)
        {
            movementSettingsIndex = -1;
            hordeMosquitosChoosen = null;
            return;
        }

        // Escolher aleatoriamente um índice de configuração de movimento
        movementSettingsIndex = UnityEngine.Random.Range(0, curveMovementSettings.Length);
        
        hordeMosquitosChoosen = null;

        switch (behaviourType)
        {
            case BehaviourType.AllHordeMosquitos:
                hordeMosquitosChoosen = GetAllMosquitosFromHorde(mosquitosDictionary);
                break;
            case BehaviourType.OnlyInfectedMosquitos:
                hordeMosquitosChoosen = GetOnlyInfectedMosquitos(mosquitosDictionary);
                break;
            case BehaviourType.RandomRowOrColumnMosquitos:
                hordeMosquitosChoosen = GetMosquitosFromRandomRowOrColumn(mosquitosDictionary, hordeRows, hordeColumns);
                break;
        }
    }
    
    private MosquitosHorde.HordePositionKey[] GetAllMosquitosFromHorde(Dictionary<MosquitosHorde.HordePositionKey, Mosquito> mosquitosDictionary)
    {
        return mosquitosDictionary.Keys.ToArray();
    }
    
    private MosquitosHorde.HordePositionKey[] GetOnlyInfectedMosquitos(Dictionary<MosquitosHorde.HordePositionKey, Mosquito> mosquitosDictionary)
    {
        return mosquitosDictionary
            .Where(kvp => kvp.Value.Type == MosquitoType.Infected)
            .Select(kvp => kvp.Key)
            .ToArray();
    }
    
    
    private MosquitosHorde.HordePositionKey[] GetMosquitosFromRandomRowOrColumn(Dictionary<MosquitosHorde.HordePositionKey, Mosquito> mosquitosDictionary, int hordeRows, int hordeColumns)
    {
        var allKeys = mosquitosDictionary.Keys.ToList();
        
        var randomChoice = UnityEngine.Random.Range(0, 2); // 0 for row, 1 for column

        MosquitosHorde.HordePositionKey[] selectedMosquitos = Array.Empty<MosquitosHorde.HordePositionKey>();
        
        if(randomChoice == 0) // Random Row
        {
            int randomRow = UnityEngine.Random.Range(0, hordeRows);
            selectedMosquitos = allKeys.Where(key => key.row == randomRow).ToArray();
            return selectedMosquitos;
        }

        // Random Column
        int randomColumn = UnityEngine.Random.Range(0, hordeColumns);
        
        selectedMosquitos = allKeys.Where(key => key.col == randomColumn).ToArray();

        return selectedMosquitos;
    }
}
