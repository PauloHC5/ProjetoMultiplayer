
using System;
using System.Collections.Generic;
using _Game.Scripts;
using Photon.Pun;
using UnityEngine;

public class LissajousCurveTestScript : MonoBehaviour
{
    public LissajousCurve lissajousCurve;
    
    private PhotonView _photonView;

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();
    }

    void Start()
    {
        lissajousCurve.OnStart(this);
    }

    void FixedUpdate()
    {
        transform.localPosition = lissajousCurve.Calculate(transform, GetTime());
    }
    
    private double GetTime()
    {
        return PhotonNetwork.IsConnected ? PhotonNetwork.Time : Time.timeAsDouble;
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
        {
            if (_photonView == null || !_photonView.IsMine) return;

            _photonView.RPC(nameof(RpcValuesChanged), RpcTarget.AllBuffered, lissajousCurve.Settings.name);
        }
        else
        {
            ValuesChanged(lissajousCurve.Settings);
        }
    }
#endif
    
    [PunRPC]
    private void RpcValuesChanged(string curveSettingsName)
    {
        LissajouCurveSettings newSettings = curveSettingsName != string.Empty ? Resources.Load<LissajouCurveSettings>("LissajousSettings/" + curveSettingsName) : null;
        
        lissajousCurve.SwitchCurveSettings(newSettings);
    }

    private void ValuesChanged(LissajouCurveSettings newSettings)
    {
        lissajousCurve.SwitchCurveSettings(newSettings);
    }
}
