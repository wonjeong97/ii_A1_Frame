using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage6Data
    {
        [Header("Player A")]
        public TextSetting txtA_Start; 
        public TextSetting txtA_Info;  

        [Header("Player B")]
        public TextSetting txtB_Start; 
        public TextSetting txtB_Info;  
        
        public string warningMessage; 
        public string resetMessage;   
    }

    public class TutorialPage6Controller : PopupGamePage<TutorialPage6Data>
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image imageFocus;

        [Header("Settings")]
        [SerializeField] private float stepDistance = 50f; 
        [SerializeField] private float smoothTime = 0.1f;  
        [SerializeField] private float minX = -400;
        [SerializeField] private float maxX = 400f;
        [SerializeField] private float minY = -200f;
        [SerializeField] private float maxY = 250f;
        
        private readonly float fadeDuration = 1.0f;

        private Vector2 _initialPos;
        private Vector2 _targetPos; 
        private Vector2 _currentVelocity; 

        private bool _isInitialized;
        private bool _hasStarted;
        private bool _isInputBlocked;
        private int _currentStage; 

        private TutorialPage6Data _data; // 데이터를 보관
        private Coroutine _stageSequenceRoutine;

        private int _lastP1Key = -1;
        private int _p1StepCount = 0; 
        private float _p1LastTime;
        private int _p1LastDir;

        private int _lastP2Key = -1;
        private int _p2StepCount = 0; 
        private float _p2LastTime;
        private int _p2LastDir;

        private const int StepsForFullRotation = 3; 
        private const float FastInputThreshold = 0.2f; 

        protected override void SetupData(TutorialPage6Data data)
        {
            _data = data;
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.txtA_Start);
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            if (_data != null && descriptionText)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.txtA_Start);
                ApplyDynamicNames(descriptionText);
            }
            
            if (!_isInitialized && imageFocus)
            {
                _initialPos = imageFocus.rectTransform.anchoredPosition;
                _isInitialized = true;
            }

            _hasStarted = false;
            _isInputBlocked = false;
            _currentStage = 0; 
            _stageSequenceRoutine = null; 
            
            _lastP1Key = -1; _p1StepCount = 0; _p1LastDir = 0; _p1LastTime = 0f;
            _lastP2Key = -1; _p2StepCount = 0; _p2LastDir = 0; _p2LastTime = 0f;
            
            ResetIdleState(true);
            
            if (imageFocus) 
            {
                imageFocus.rectTransform.anchoredPosition = _initialPos;
                _targetPos = _initialPos; 
                _currentVelocity = Vector2.zero;
            }
            SetAlpha(1f);
            SetTextAlpha(1f);
        }

        // 텍스트 안에 있는 {nameA}, {nameB}를 현재 이름으로 변경하는 헬퍼 함수
        private void ApplyDynamicNames(Text txt)
        {
            if (txt && GameManager.Instance)
            {
                txt.text = txt.text.Replace("{nameA}", GameManager.Instance.PlayerALastName)
                                   .Replace("{nameB}", GameManager.Instance.PlayerBLastName);
            }
        }
        
        public override void OnExit()
        {
            if (_stageSequenceRoutine != null)
            {
                StopCoroutine(_stageSequenceRoutine);
                _stageSequenceRoutine = null;
            }
            base.OnExit();
        }
        
        private void Update()
        {
            if (!_isInputBlocked)
            {
                HandleWheelInput();
            }

            if (imageFocus)
            {
                imageFocus.rectTransform.anchoredPosition = Vector2.SmoothDamp(
                    imageFocus.rectTransform.anchoredPosition, 
                    _targetPos, 
                    ref _currentVelocity, 
                    smoothTime
                );
            }

            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);
            }
            else
            {
                UpdateInactivity(_isInputBlocked);
            }
        }

        private void HandleWheelInput()
        {
            if (!imageFocus) return;

            int direction = 0; 
            float now = Time.time;

            if (_currentStage == 0)
            {
                int currentKey = GetPressedKeyIndex(1, 4);
                if (currentKey != -1)
                {
                    if (_lastP1Key != -1)
                    {
                        int diff = (currentKey - _lastP1Key + 4) % 4;
                        int dir = 0;

                        if (diff == 1) dir = 1;       
                        else if (diff == 3) dir = -1; 

                        if (now - _p1LastTime < FastInputThreshold && _p1LastDir != 0)
                        {
                            if (diff == 2 || (dir != 0 && dir != _p1LastDir))
                            {
                                dir = _p1LastDir;
                            }
                        }

                        if (dir != 0)
                        {
                            direction = dir; 
                            
                            if (dir == _p1LastDir) _p1StepCount++;
                            else _p1StepCount = 1;

                            _p1LastDir = dir;
                            _p1LastTime = now;

                            if (_p1StepCount >= StepsForFullRotation)
                            {
                                SoundManager.Instance?.PlaySFX("카메라_1");
                                _p1StepCount = 0; 
                            }
                        }
                    }
                    _lastP1Key = currentKey;
                }
            }
            else
            {
                int currentKey = GetPressedKeyIndex(5, 8);
                if (currentKey != -1)
                {
                    if (_lastP2Key != -1)
                    {
                        int currIdx = currentKey - 5;
                        int lastIdx = _lastP2Key - 5;
                        int diff = (currIdx - lastIdx + 4) % 4;
                        int dir = 0;
                        
                        if (diff == 1) dir = 1;       
                        else if (diff == 3) dir = -1; 

                        if (now - _p2LastTime < FastInputThreshold && _p2LastDir != 0)
                        {
                            if (diff == 2 || (dir != 0 && dir != _p2LastDir))
                            {
                                dir = _p2LastDir;
                            }
                        }

                        if (dir != 0)
                        {
                            direction = dir; 

                            if (dir == _p2LastDir) _p2StepCount++;
                            else _p2StepCount = 1;

                            _p2LastDir = dir;
                            _p2LastTime = now;

                            if (_p2StepCount >= StepsForFullRotation)
                            {
                                SoundManager.Instance?.PlaySFX("카메라_1");
                                _p2StepCount = 0; 
                            }
                        }
                    }
                    _lastP2Key = currentKey;
                }
            }

            if (direction != 0)
            {
                if (!_hasStarted)
                {
                    _hasStarted = true;
                    _stageSequenceRoutine = StartCoroutine(ProcessStageSequence());
                }

                if (_currentStage == 0) 
                {
                    float moveY = (direction == 1) ? -stepDistance : stepDistance;
                    _targetPos.y += moveY;
                    _targetPos.y = Mathf.Clamp(_targetPos.y, _initialPos.y + minY, _initialPos.y + maxY);
                    _targetPos.x = _initialPos.x; 
                }
                else 
                {
                    float moveX = (direction == 1) ? stepDistance : -stepDistance;
                    _targetPos.x += moveX;
                    _targetPos.x = Mathf.Clamp(_targetPos.x, _initialPos.x + minX, _initialPos.x + maxX);
                    _targetPos.y = _initialPos.y; 
                }
            }
        }

        private int GetPressedKeyIndex(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                KeyCode key = (KeyCode)((int)KeyCode.Alpha0 + i);
                if (Input.GetKeyDown(key)) return i;
            }
            return -1;
        }

        private IEnumerator ProcessStageSequence()
        {
            if (_data == null)
            {
                Debug.LogError("[TutorialPage6] 데이터가 없습니다.");
                _stageSequenceRoutine = null;
                yield break;
            }
            
            yield return CoroutineData.GetWaitForSeconds(5.0f); 

            _isInputBlocked = true; 
            MoveFocusToCenter(); 

            if (_currentStage == 0)
            {
                yield return StartCoroutine(TextChangeSequence(_data.txtA_Info));
                yield return CoroutineData.GetWaitForSeconds(4.0f);
                yield return StartCoroutine(TextChangeSequence(_data.txtB_Start));

                _currentStage = 1;
                _hasStarted = false;
                _isInputBlocked = false;
                _stageSequenceRoutine = null;
                
                _lastP2Key = -1;
                _p2StepCount = 0;
                _p2LastDir = 0;
                _p2LastTime = 0f;
            }
            else
            {
                yield return StartCoroutine(TextChangeSequence(_data.txtB_Info));
                yield return CoroutineData.GetWaitForSeconds(4.0f);
                CompleteStep(); 
                _stageSequenceRoutine = null;
            }
        }

        private void MoveFocusToCenter()
        {
            if (!imageFocus) return;
            _targetPos = _initialPos; 
        }
        
        private IEnumerator TextChangeSequence(TextSetting newTextData)
        {
            yield return StartCoroutine(FadeTextRoutine(1f, 0f));
            if (newTextData != null && descriptionText)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, newTextData);
                // 텍스트가 바뀔 때마다 이름 동적 적용
                ApplyDynamicNames(descriptionText);
            }
            yield return StartCoroutine(FadeTextRoutine(0f, 1f));
        }

        private IEnumerator FadeTextRoutine(float startAlpha, float endAlpha)
        {
            if (!descriptionText) yield break;
            float timer = 0f;
            SetTextAlpha(startAlpha);
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / fadeDuration;
                SetTextAlpha(Mathf.Lerp(startAlpha, endAlpha, progress));
                yield return null;
            }
            SetTextAlpha(endAlpha);
        }

        private void SetTextAlpha(float alpha)
        {
            if (!descriptionText) return;
            Color c = descriptionText.color;
            c.a = alpha;
            descriptionText.color = c;
        }
    }
}