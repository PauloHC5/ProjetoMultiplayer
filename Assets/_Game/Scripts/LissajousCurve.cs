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
        public LissajouCurveSettings settings;
        public bool interpolateToNewSettings = true;
        public CurveTransitionMode transitionMode = CurveTransitionMode.Blend;
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
            public Vector2 Osc(double time)
            {
                double t = time * tMul;
                return new Vector2(
                    A * (float)Math.Sin(C * t + delta) * mult,
                    B * (float)Math.Sin(D * t) * mult);
            }

            public Vector2 Offset => new Vector2(xo, yo);
        }

        MonoBehaviour _owner;
        Coroutine _transitionCoroutine;
        Params _from, _to;
        float _progress = 1f;

        public void OnStart(MonoBehaviour ownerObject)
        {
            _owner = ownerObject;
            CheckTrailRenderer();
            
            _to = _from = Resolve(settings);
            _progress = 1f;
        }

        static Params Resolve(LissajouCurveSettings s)
        {
            if(s == null) return new Params { A = 0, B = 0, C = 0, D = 0, delta = 0, tMul = 1f, mult = 1f, xo = 0f, yo = 0f };
            
            return new Params
            {
                A = s.A_Random ? Random.Range(0f, 5f) : s.A,
                B = s.B_Random ? Random.Range(0f, 5f) : s.B,
                C = s.C_Random ? Random.Range(0f, 10f) : s.C,
                D = s.D_Random ? Random.Range(0f, 10f) : s.D,
                delta = s.delta_Random ? Random.Range(0f, 5f) : s.delta,
            tMul = s.t_multipler, mult = s.multiplier, xo = s.xOffset, yo = s.yOffset
            };
        }

        public void SwitchCurveSettings(LissajouCurveSettings newSettings)
        {
            if (_transitionCoroutine != null)
            {
                _owner.StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            _from = _to;
            
            _to = Resolve(newSettings);
            settings = newSettings;

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

        // time = PhotonNetwork.Time (ou Time.timeAsDouble offline)
        public Vector3 Calculate(Transform transform, double time)
        {
            Vector2 osc;
            Vector2 offset;
            double t = bDebugTime ? debugTime : time;
            t += timeOffset;

            if (_progress >= 1f)
            {
                osc = _to.Osc(t);
                offset = _to.Offset;
            }
            else
            {
                offset = Vector2.Lerp(_from.Offset, _to.Offset, Mathf.SmoothStep(0f, 1f, _progress));

                if (transitionMode == CurveTransitionMode.Blend)
                {
                    float k = Mathf.SmoothStep(0f, 1f, _progress);
                    osc = Vector2.Lerp(_from.Osc(t), _to.Osc(t), k);
                }
                else // ZeroAmplitude
                {
                    if (_progress < 0.5f)
                    {
                        float scale = Mathf.SmoothStep(1f, 0f, _progress * 2f);
                        osc = _from.Osc(t) * scale;
                    }
                    else
                    {
                        float scale = Mathf.SmoothStep(0f, 1f, (_progress - 0.5f) * 2f);
                        osc = _to.Osc(t) * scale;
                    }
                }
            }
            
            if (settings != null && settings.pulseMultiplier)
            {
                float s = 0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * settings.pulseFrequency * time);
                osc *= Mathf.Lerp(settings.pulseMin, settings.pulseMax, s);
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