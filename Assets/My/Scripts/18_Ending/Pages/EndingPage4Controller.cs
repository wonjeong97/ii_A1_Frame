using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage4Data
    {
        public TextSetting descriptionText;
        public TextSetting allFinishedText;
    }

    /// <summary> 
    /// 엔딩 씬의 마지막 페이지 컨트롤러입니다.
    /// 플레이어에게 최종 메시지를 전달하며, 50% 확률로 '붉은 실(Red Line)' 연출을 포함한 특별 엔딩을 보여줍니다.
    /// </summary>
    public class EndingPage4Controller : GamePage<EndingPage4Data>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image redLineImage;

        private bool _isAllFinished;
        private bool _hasSentEndTime;
        
        protected override void SetupData(EndingPage4Data data)
        {
            // 엔딩의 다양성을 위해 50% 확률로 일반 엔딩과 특별 엔딩(Red Line)을 분기합니다.
            int randomValue = UnityEngine.Random.Range(0, 2);
            TextSetting textToUse = data.descriptionText;

            if (randomValue == 1 && data.allFinishedText != null)
            {
                textToUse = data.allFinishedText;
                _isAllFinished = true;
            }
            else
            {
                textToUse = data.descriptionText;
                _isAllFinished = false;
            }

            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, textToUse);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            SetAlpha(0f);

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
            }

            if (!_hasSentEndTime && GameManager.Instance)
            {
                if (GameManager.Instance.CurrentUserId == 0)
                {
                    Debug.LogWarning("[EndingPage4] CurrentUserId가 없어 API 전송을 보류합니다.");
                }
                else
                {
                    GameManager.Instance.SendTimeUpdateAPI();
                    GameManager.Instance.SendExitRoomAPI();
                    _hasSentEndTime = true;
                }
            }
            StartCoroutine(SequenceRoutine());
        }

        /// <summary> 엔딩 시퀀스 루틴입니다. 분기된 엔딩 타입에 따라 다른 연출과 대기 시간을 가집니다. </summary>
        private IEnumerator SequenceRoutine()
        {   
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            if (_isAllFinished && redLineImage)
            {
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, 2.0f));
                SoundManager.Instance?.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            else
            {   
                yield return CoroutineData.GetWaitForSeconds(2.0f);
                SoundManager.Instance?.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            CompleteStep();
        }

        /// <summary> 이미지의 FillAmount를 조절하여 게이지가 차오르는 듯한 연출을 수행합니다. (붉은 실 연출용) </summary>
        private IEnumerator FillImageRoutine(Image t, float s, float e, float d)
        {
            if (!t) yield break;
            float time = 0f;
            t.fillAmount = s;
            
            while (time < d)
            {
                time += Time.deltaTime;
                t.fillAmount = Mathf.Lerp(s, e, time / d);
                yield return null;
            }

            t.fillAmount = e;
        }
    }
}