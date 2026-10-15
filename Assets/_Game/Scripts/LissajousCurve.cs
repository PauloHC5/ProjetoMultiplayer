/// <summary>
/// This script calculates the position of this gameobject based on a Lissajous Curve formula.
/// You can change the values for the various parameters of the formula to customize the curve.
/// The gameobject should have a Trail Renderer attached to it, so that the trail shows the curve.
/// Read up on Lissajous Curve on Wikipedia to understand the formula, and to also understand 
/// the various ways you can control the curve.
/// NOTE: Each time you wanna change values, exit Play Mode. If you change the values during
/// Play Mode, the design of whatever pattern is currently being drawn will get messed up.
/// </summary>

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Game.Scripts
{
    public enum CurveTransitionMode
    {
        Blend,          // mistura as posições das duas curvas
        ZeroAmplitude   // amplitude da antiga -> 0, depois 0 -> amplitude da nova
    }

    
    [Serializable]
    public class LissajousCurve
    {
        [Header("Lissajous Curve Settings")]
        [SerializeField] private LissajouCurveSettings settings;
        public LissajouCurveSettings Settings => settings;
        public bool HasMovementSettings => settings != null;
        
        public bool interpolateToNewSettings = true;
        public float interpolationDuration = 3.0f;
        public float timeOffset;

        [Header("Debug")]
        public TrailRenderer tr;
        public bool bDebugCurve = false;
        public bool bDebugTime = false;
        public bool bClearTrailOnNewSettings = true;
        public double debugTime = 0;

        struct Params
        {
            public float A, B, C, D, delta, tMul, mult, xo, yo;

            // só a oscilação (sem offset)
            public Vector2 Osc(double time, float deltaAdd, float aScale, float bScale)
            {
                double t = time * tMul;
                return new Vector2(
                    A  * aScale * (float)Math.Sin(C * t + delta + deltaAdd) * mult,
                    B * bScale * (float)Math.Sin(D * t) * mult);
            }

            public Vector2 Offset => new Vector2(xo, yo);
        }

        MonoBehaviour _owner;
        private CurveTransitionMode _transitionMode = CurveTransitionMode.Blend;
        Coroutine _transitionCoroutine;
        Params _from, _to;
        float _progress = 1f;
        bool _bAlredySwitched = false;

        static Params Resolve(LissajouCurveSettings s)
        {
            if(s == null) return new Params { A = 0, B = 0, C = 0, D = 0, delta = 0, tMul = 1f, mult = 1f, xo = 0f, yo = 0f };
            
            return new Params
            {
                A = s.A,
                B = s.B,
                C = s.C_Random ? Random.Range(0f, 10f) : s.C,
                D = s.D_Random ? Random.Range(0f, 10f) : s.D,
                delta = s.delta_Random ? Random.Range(0f, 5f) : s.delta,
            tMul = s.t_multipler, mult = s.multiplier, xo = s.xOffset, yo = s.yOffset
            };
        }
        
        static float Pulse(double t, Vector2 range, float freq, float phaseCycles)
        {
            float s = 0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * (freq * t + phaseCycles));
            return Mathf.Lerp(range.x, range.y, s);
        }
        
        public void OnStart(MonoBehaviour ownerObject)
        {
            _owner = ownerObject;
            CheckTrailRenderer();
            
            _to = _from = Resolve(settings);
            _progress = 1f;
        }

        public void SwitchCurveSettings(LissajouCurveSettings newSettings)
        {
            if (settings != null && newSettings != settings)
            {
                settings.OnChange -= () => SwitchCurveSettings(settings);
                _bAlredySwitched = false;
            }

            if (_transitionCoroutine != null && _owner != null)
            {
                _owner.StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            _from = _to;
            
            _to = Resolve(newSettings);
            
            settings = newSettings;
            if(settings != null)
                _transitionMode = settings.transitionMode;
            
            if(settings != null && !_bAlredySwitched)
                settings.OnChange += () => SwitchCurveSettings(settings);
            
            _bAlredySwitched = true;

            if (interpolateToNewSettings && _owner != null)
                _transitionCoroutine = _owner.StartCoroutine(Transition());
            else
                _progress = 1f;
            
            if(tr == null) return;
            
            CheckTrailRenderer();
            
            if(bClearTrailOnNewSettings)
                tr.Clear();
        }

        IEnumerator Transition()
        {
            float dur = Mathf.Max(0.0001f, interpolationDuration);
            _progress = 0f;
            for (float e = 0f; e < dur; e += Time.deltaTime)
            {
                _progress = e / dur;
                yield return null;
            }
            _progress = 1f;
            _transitionCoroutine = null;
        }

        private float DeltaAnim(double t)
        {
            if (settings.deltaMode == AnimMode.PingPong)
            {
                float s = 0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * settings.deltaFrequency * t);
                return Mathf.Lerp(settings.deltaMin, settings.deltaMax, s);
            }
            // Continuous: o seno é periódico, então o módulo não causa salto
            return (float)((2.0 * Math.PI * settings.deltaFrequency * t) % (2.0 * Math.PI));
        }

        // time = PhotonNetwork.Time (ou Time.timeAsDouble offline)
        public Vector2 Calculate(Transform transform, double time)
        {
            var settingToUse = settings != null ? settings : ScriptableObject.CreateInstance<LissajouCurveSettings>();
            
            double rawTime = time; 
            time += timeOffset;

            float dAdd = settingToUse.animateDelta ? DeltaAnim(rawTime) : 0f;
            float aS = settingToUse.animateA ? Pulse(rawTime, settingToUse.aScaleRange, settingToUse.aFrequency, 0f) : 1f;
            float bS = settingToUse.animateB ? Pulse(rawTime, settingToUse.bScaleRange, settingToUse.bFrequency, settingToUse.bPhase) : 1f;

            Vector2 osc;
            Vector2 offset;

            if (_progress >= 1f)
            {
                osc = _to.Osc(time, dAdd, aS, bS);
                offset = _to.Offset;
            }
            else
            {
                offset = Vector2.Lerp(_from.Offset, _to.Offset, Mathf.SmoothStep(0f, 1f, _progress));

                if (_transitionMode == CurveTransitionMode.Blend)
                {
                    float k = Mathf.SmoothStep(0f, 1f, _progress);
                    osc = Vector2.Lerp(_from.Osc(time, dAdd, aS, bS), _to.Osc(time, dAdd, aS, bS), k);
                }
                else
                {
                    if (_progress < 0.5f)
                        osc = _from.Osc(time, dAdd, aS, bS) * Mathf.SmoothStep(1f, 0f, _progress * 2f);
                    else
                        osc = _to.Osc(time, dAdd, aS, bS) * Mathf.SmoothStep(0f, 1f, (_progress - 0.5f) * 2f);
                }
            }

            Vector3 pos = transform.position;
            pos.x = osc.x + offset.x;
            pos.y = osc.y + offset.y;
            return pos;
        }

        private void CheckTrailRenderer()
        {
            if (tr == null)
            {
                tr = _owner.GetComponent<TrailRenderer>();
                if (tr == null)
                {
                    tr = _owner.gameObject.AddComponent<TrailRenderer>();
                    tr.time = 10f;
                    tr.startWidth = 0.1f;
                    tr.endWidth = 0.1f;
                    tr.material = new Material(Shader.Find("Sprites/Default"));
                    tr.startColor = Color.blue;
                    tr.endColor = Color.deepSkyBlue;
                }
            }
            tr.enabled = bDebugCurve;
        }
    }
}