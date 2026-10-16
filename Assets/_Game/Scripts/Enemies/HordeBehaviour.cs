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
        RandomMosquitos,
        RandomRowMosquitos,
        RandomColumnMosquitos
    }
    
    [SerializeField] protected LissajouCurveSettings[] curveMovementSettings;
    [SerializeField] protected BehaviourType behaviourType;
    [SerializeField] protected float behaviourStartDelay = 5f;
    [SerializeField] protected float behaviourDuration = 3f;
    [SerializeField] protected float timeToReorder = 0.5f;
    
    public float BehaviourStartDelay => behaviourStartDelay;
    public float BehaviourDuration => behaviourDuration;
    public float TimeToReorder => timeToReorder;


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
            case BehaviourType.RandomMosquitos:
                hordeMosquitosChoosen = null; // Implement this case
                break;
            case BehaviourType.RandomRowMosquitos:
                hordeMosquitosChoosen = GetMosquitosFromRandomRow(mosquitosDictionary, hordeRows);
                break;
            case BehaviourType.RandomColumnMosquitos:
                hordeMosquitosChoosen = GetMosquitosFromRandomColumn(mosquitosDictionary, hordeColumns);
                break;
        }
    }
    
    private MosquitosHorde.HordePositionKey[] GetAllMosquitosFromHorde(Dictionary<MosquitosHorde.HordePositionKey, Mosquito> mosquitosDictionary)
    {
        return mosquitosDictionary.Keys.ToArray();
    }
    
    private MosquitosHorde.HordePositionKey[] GetMosquitosFromRandomRow(Dictionary<MosquitosHorde.HordePositionKey, Mosquito> mosquitosDictionary, int hordeRows)
    {
        var allKeys = mosquitosDictionary.Keys.ToList();

        int randomRow = UnityEngine.Random.Range(0, hordeRows);
        
        MosquitosHorde.HordePositionKey[] selectedMosquitos = allKeys.Where(key => key.row == randomRow).ToArray();

        return selectedMosquitos;
    }
    
    private MosquitosHorde.HordePositionKey[] GetMosquitosFromRandomColumn(Dictionary<MosquitosHorde.HordePositionKey, Mosquito> mosquitosDictionary, int hordeColumns)
    {
        var allKeys = mosquitosDictionary.Keys.ToList();

        int randomColumn = UnityEngine.Random.Range(0, hordeColumns);
        
        MosquitosHorde.HordePositionKey[] selectedMosquitos = allKeys.Where(key => key.col == randomColumn).ToArray();

        return selectedMosquitos;
    }
}
