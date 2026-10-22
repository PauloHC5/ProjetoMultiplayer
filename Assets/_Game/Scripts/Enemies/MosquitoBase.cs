using System;
using Photon.Pun;
using UnityEngine;

namespace _Game.Scripts.Enemies
{
    public abstract class MosquitoBase : MonoBehaviour
    {
        [Header("Settings")]
        public int points;
        [SerializeField] protected MosquitoType type;
        public float positionInterpolationDuration = 0.5f;
        
        [Header("Zig Zag Movement Settings")]
        [SerializeField] protected MosquitoZigZagMovement zigZagMovement;
        
        [Header("Lissajous Curve Settings")]
        [SerializeField] protected LissajousCurve lissajousCurve;
        
        [Header("Debug")]
        public bool bDebugLogs;
        public Vector2 debugGoToPosition;
        public bool moveToHordeCenter = false;
        
        protected Vector2 AnchorPos;
        protected Vector2 MoveStartPos;
        protected Vector2 MoveTargetPos;
        protected double MoveStartTime;
        protected bool IsMoving;
        protected Action MoveArrivedCallback;
        protected Collider2D Collider2D;
        protected int ProjectilesLayer;
        
        public MosquitoType Type => type;
        public MosquitoZigZagMovement MosquitoZigZagMovement => zigZagMovement;
        public LissajousCurve LissajousCurve => lissajousCurve;
        
        public static readonly int MosquitosInstigatorId = 451;

        protected virtual void Awake()
        {
            Collider2D = GetComponent<Collider2D>();
            ProjectilesLayer = LayerMask.NameToLayer("Projectiles");
        }
        
        protected virtual void Start()
        {
            zigZagMovement.OnStart(this);
            lissajousCurve.OnStart(this);
            
            AnchorPos = transform.localPosition;
            MoveStartPos = AnchorPos;
        }

        protected virtual void Update()
        {
            double now = PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode ? PhotonNetwork.Time : Time.timeAsDouble;        
            Action arrivedCallback = null;
        
            if (IsMoving)
            {
                float t = Mathf.Clamp01((float)((now - MoveStartTime) / positionInterpolationDuration));
                AnchorPos = Vector2.Lerp(MoveStartPos, MoveTargetPos, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f)
                {
                    IsMoving = false;
                    arrivedCallback = MoveArrivedCallback;
                    MoveArrivedCallback = null;
                }
            }

            Vector2 zigZagOffset = zigZagMovement.Movement(now);
            Vector2 lissajousOffset = lissajousCurve.Calculate(transform, now);

            if(moveToHordeCenter)
            {
                transform.localPosition = zigZagOffset + lissajousOffset;
            }
            else
            {
                transform.localPosition = AnchorPos + zigZagOffset + lissajousOffset;
            }

            arrivedCallback?.Invoke();
        }
        
        public void GoToNewPosition(Vector2 newPos, Vector2? startPos = null,
            Action onArrived = null)
        {
            MoveStartPos = startPos ?? AnchorPos;
            MoveTargetPos = newPos;
            MoveStartTime = PhotonHandlers.Now() + 0.1f; // Adding a small delay to ensure smooth transition
            MoveArrivedCallback = onArrived;
            IsMoving = true;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            zigZagMovement.OnValidate();
            lissajousCurve.SwitchCurveSettings(lissajousCurve.Settings);
        
            //double now = PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode ? PhotonNetwork.Time : Time.timeAsDouble;
        
            //GoToNewPosition(debugGoToPosition, now);
        }
#endif
    }
}