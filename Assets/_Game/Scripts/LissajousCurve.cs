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
    [Serializable]
    public class LissajousCurve
    {
        [Header("Lissajous Curve Settings")]
        public LissajouCurveSettings settings;
        public TrailRenderer tr;
        
        [Header("Debug")]
        public bool bDebugCurve = false;
        
        [HideInInspector]
        public float t = 0f;
        
        private GameObject ownerObject;

        public void OnStart(GameObject ownerObject)
        {
            this.ownerObject = ownerObject;
            
            // Checks if there is a Trail Renderer component attached to this gameobject.
            // If not, it will attach one, and set values for the relevant fields. But generally
            // it's best to set up the Trail Renderer component yourself from the inspector.
            CheckTrailRenderer();

            settings.A = settings.A_Random ? Random.Range(0f, 5f) : settings.A;
            settings.B = settings.B_Random ? Random.Range(0f, 5f) : settings.B;
            settings.C = settings.C_Random ? Random.Range(0f, 10f) : settings.C;
            settings.D = settings.D_Random ? Random.Range(0f, 10f) : settings.D;
            settings.delta = settings.delta_Random ? Random.Range(0f, 5f) : settings.delta;
        }
        
        public Vector3 Calculate(Transform transform)
        {
            t = Time.time * settings.t_multipler;
            Vector3 finalPos = transform.position;

            finalPos.x = (settings.A * Mathf.Sin((settings.C * t) + settings.delta)) * settings.multiplier;
            finalPos.y = (settings.B * Mathf.Sin(settings.D * t)) * settings.multiplier;

            finalPos.x += settings.xOffset;
            finalPos.y += settings.yOffset;
            
            return finalPos;
        }
        
        public void CheckTrailRenderer()
        {
            if (tr == null)
            {
                tr = ownerObject.GetComponent<TrailRenderer>();
                if (tr == null)
                {
                    tr = ownerObject.AddComponent<TrailRenderer>();
                    tr.enabled = false;
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