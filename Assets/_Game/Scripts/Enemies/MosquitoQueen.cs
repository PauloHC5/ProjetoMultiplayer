using System.Collections;
using Photon.Pun;
using UnityEngine;

namespace _Game.Scripts.Enemies
{
    public class MosquitoQueen : MosquitoBase
    {
        [Header("Queen Settings")]
        [SerializeField] private MosquitoZigZagMovementSettings queenZigZagSettings;
        [SerializeField] private MosquitoZigZagMovementSettings queenPrepareToAttackZigZagSettings;
        [SerializeField] private QueenBehaviour[] queenBehaviours;
        [SerializeField] private float minTimeToChangeBehaviour = 5f;
        [SerializeField] private float maxTimeToChangeBehaviour = 10f;
        [SerializeField] private float preparationTime = 3f;
        public bool bEnableBehaviour = true;
        
        private PhotonView _photonView;
        private Coroutine _queenBehaviourCoroutine;
        private int _currentBehaviourIndex = 0;
        private int _previousBehaviourIndex = 0;

        protected override void Awake()
        {
            base.Awake();
            
            _photonView = GetComponent<PhotonView>();
        }

        protected override void Start()
        {
            base.Start();
            
            StartCoroutine(GoToSceneCenter());
        }

        private IEnumerator GoToSceneCenter()
        {
            float now = PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode ? (float)PhotonNetwork.Time : Time.time;

            bool arrivedAtSceneCenter = false;
            GoToNewPosition(Vector2.zero, now, onArrived: () => arrivedAtSceneCenter = true);

            yield return new WaitUntil(() => arrivedAtSceneCenter);

            _queenBehaviourCoroutine = StartCoroutine(QueenBehaviourRoutine());
            
            StartCoroutine(ChangeBehaviour());
        }
        
        private IEnumerator ChangeBehaviour()
        {
            if(!bEnableBehaviour) yield break;
            
            yield return new WaitForSeconds(Random.Range(minTimeToChangeBehaviour, maxTimeToChangeBehaviour));

            while (true)
            {
                if(_queenBehaviourCoroutine != null)
                {
                    StopCoroutine(_queenBehaviourCoroutine);
                }
                
                if (_currentBehaviourIndex == 0)
                {
                    zigZagMovement.SwitchMovementSettings(queenPrepareToAttackZigZagSettings);

                    if (bDebugLogs)
                        Debug.Log("[MosquitoQueen] Preparing to change behaviour to index: " +
                                  queenBehaviours[_currentBehaviourIndex].name);

                    yield return new WaitForSeconds(preparationTime);
                    
                    zigZagMovement.SwitchMovementSettings(null);

                    ChooseNextBehaviour();
                }
                else
                {
                    ReturnToBaseBehaviour();
                    
                    zigZagMovement.SwitchMovementSettings(queenZigZagSettings);
                }
                
                _queenBehaviourCoroutine = StartCoroutine(QueenBehaviourRoutine());

                if(bDebugLogs)
                    Debug.Log("[MosquitoQueen] Changing behaviour to index: " + queenBehaviours[_currentBehaviourIndex].name);
                
                yield return new WaitForSeconds(Random.Range(minTimeToChangeBehaviour, maxTimeToChangeBehaviour));
            }
        }
        
        private void ReturnToBaseBehaviour()
        {
            _currentBehaviourIndex = 0;
        }
        
        private void ChooseNextBehaviour()
        {
            _currentBehaviourIndex = (_previousBehaviourIndex + 1) % queenBehaviours.Length;
            
            if(_currentBehaviourIndex == 0)
            {
                _currentBehaviourIndex = 1; // Skip the base behaviour
            }
            
            _previousBehaviourIndex = _currentBehaviourIndex;
        }
        
        private IEnumerator QueenBehaviourRoutine()
        {
            if(!bEnableBehaviour) yield break;
            
            if(queenBehaviours == null)
            {
                Debug.LogWarning("QueenBehaviour is not assigned.");
                yield break;
            }
            
            if(bDebugLogs)
                Debug.Log("[MosquitoQueen] Starting behaviour routine.");

            while (true)
            {
                int movementSettingsIndex = queenBehaviours[_currentBehaviourIndex].GetNextMovementSettingsIndex(_currentBehaviourIndex);
                
                if(PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
                    _photonView.RPC(nameof(ApplyBehaviourSettings), RpcTarget.All, movementSettingsIndex);
                else
                    ApplyBehaviourSettings(movementSettingsIndex);
                
                
                float timeToChangeBehaviour = queenBehaviours[_currentBehaviourIndex].TimeToChangeBehaviour;
                yield return new WaitForSeconds(timeToChangeBehaviour);
            }
        }
        
        [PunRPC]
        private void ApplyBehaviourSettings(int settingsIndex)
        {
            LissajouCurveSettings newSettings = queenBehaviours[_currentBehaviourIndex].GetMovementSettings(settingsIndex);
            if (newSettings != null)
            {
                lissajousCurve.SwitchCurveSettings(newSettings);
            }
        }
    }
}
