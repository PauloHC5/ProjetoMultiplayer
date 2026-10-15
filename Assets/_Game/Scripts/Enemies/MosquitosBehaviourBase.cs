using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;

[Serializable]
public class MosquitosBehaviourBase
{
    [SerializeField] protected LissajouCurveSettings[] curveMovementSettings;
    [SerializeField] protected float behaviourStartDelay = 5f;
    [SerializeField] protected float behaviourDuration = 3f;
    [SerializeField] protected float speedMultiplier = 2f;
    
    public bool bDegugLog = false;
    
    protected Mosquito[] mosquitos;
    protected MosquitosHorde mosquitosHorde;
    
    public void SetupBehaviour(Mosquito[] inMosquitos, MosquitosHorde inMosquitosHorde)
    {
        if(bDegugLog)
            Debug.Log("MosquitosBehaviourBase: SetupBehaviour called.");
        
        if(curveMovementSettings == null)
        {
            Debug.LogWarning("Curve Movement Settings is not assigned in MosquitosBehaviourBase.");
            return;
        }
        
        this.mosquitos = inMosquitos;
        this.mosquitosHorde = inMosquitosHorde;
        
        if(curveMovementSettings == null)
        {
            Debug.LogWarning("Curve Movement Settings is not assigned in MosquitosBehaviourBase.");
            return;
        }
        
        if(mosquitos == null || mosquitos.Length == 0)
        {
            Debug.LogWarning("No mosquitos assigned to MosquitosBehaviourBase.");
            return;
        }
        
        mosquitosHorde.StartCoroutine(StartBehaviour());
    }
    
    private IEnumerator StartBehaviour()
    {
        yield return new WaitForSeconds(behaviourStartDelay);
        
        if(bDegugLog)
            Debug.Log("MosquitosBehaviourBase: Behaviour started.");
        
        LissajouCurveSettings randomMovementSettings = this.curveMovementSettings[UnityEngine.Random.Range(0, this.curveMovementSettings.Length)];
        
        for (int i = 0; i < mosquitos.Length; i++)
        {
            if (mosquitos[i] != null && mosquitos[i].gameObject.activeInHierarchy)
            {
                mosquitos[i].GoToNewPosition(Vector2.zero, PhotonNetwork.Time + 0.1f);
                mosquitos[i].LissajousCurve.SwitchCurveSettings(randomMovementSettings);
            }
        }
        
        mosquitosHorde.speedBuff = speedMultiplier;
        
        yield return new WaitForSeconds(behaviourDuration);
        
        mosquitosHorde.StartCoroutine(RemoveBehaviour());
    }
    
    private IEnumerator RemoveBehaviour()
    {
        if(bDegugLog)
            Debug.Log("MosquitosBehaviourBase: Behaviour ended. Removing curve settings from mosquitos.");
        
        for (int i = 0; i < mosquitos.Length; i++)
        {
            if (mosquitos[i] != null && mosquitos[i].gameObject.activeInHierarchy)
            {
                mosquitos[i].LissajousCurve.SwitchCurveSettings(null);
            }
        }
        
        yield return new WaitForSeconds(0.5f);
        
        if(bDegugLog)
            Debug.Log("MosquitosBehaviourBase: Sending mosquitos back to their positions.");
        
        mosquitosHorde.SendMosquitosBackToTheirPositions();
        
        mosquitosHorde.speedBuff = 1f;
        
        if(bDegugLog)
            Debug.Log("MosquitosBehaviourBase: Restarting behaviour.");
        
        mosquitosHorde.StartCoroutine(StartBehaviour());
    }
}
