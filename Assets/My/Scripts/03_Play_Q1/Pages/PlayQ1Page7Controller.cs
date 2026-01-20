using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._03_Play_Q1.Pages
{
    [Serializable]
    public class PlayQ1Page7Data
    {
        public TextSetting descriptionText;
    }

    public class PlayQ1Page7Controller : PlayQ1PageBase
    {
        [Header("Page 7 UI")]
        [SerializeField] private Text descriptionText;

        public override void SetupData(object data)
        {
            var pageData = data as PlayQ1Page7Data;
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
            // 1. [페이드인 대기] PlayManager의 FadeIn(1.0s) 대기
            yield return new WaitForSeconds(1.0f);

            // 2. [2초 대기] 멘트 노출
            yield return new WaitForSeconds(2.0f);

            // 3. [텍스트 페이드아웃] 0.5초
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
                
                Color finalColor = descriptionText.color;
                finalColor.a = 0f;
                descriptionText.color = finalColor;
            }

            // 4. [완료] Q1 종료 -> 타이틀 등 이동
            CompleteStep();
        }
    }
}