using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._02_Play_Tutorial.Pages
{
    [Serializable]
    public class PlayTutorialPage6Data
    {
        public TextSetting descriptionText;
    }

    public class PlayTutorialPage6Controller : PlayTutorialPageBase
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text descriptionText;

        public override void SetupData(object data)
        {
            var pageData = data as PlayTutorialPage6Data;
            if (pageData == null) return;

            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
                
                // 텍스트가 보이도록 알파값 초기화
                Color c = descriptionText.color;
                c.a = 1f;
                descriptionText.color = c;
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            
            StartCoroutine(EndSequence());
        }

        private IEnumerator EndSequence()
        {
            // 1. [페이드인 대기]
            yield return new WaitForSeconds(1.0f);

            // 2. [2초 대기] 
            yield return new WaitForSeconds(2.0f);

            // 3. [텍스트 페이드아웃] 
            // 0.5초 동안 텍스트 알파값을 1 -> 0으로 변경
            if (descriptionText != null)
            {
                float timer = 0f;
                float duration = 0.5f;
                Color startColor = descriptionText.color;
                
                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    float alpha = Mathf.Lerp(1f, 0f, timer / duration);
                    
                    Color c = descriptionText.color;
                    c.a = alpha;
                    descriptionText.color = c;
                    
                    yield return null;
                }
                
                // 확실하게 0으로 설정
                Color finalColor = descriptionText.color;
                finalColor.a = 0f;
                descriptionText.color = finalColor;
            }

            // 4. [완료] 다음 씬 로드
            CompleteStep();
        }
    }
}