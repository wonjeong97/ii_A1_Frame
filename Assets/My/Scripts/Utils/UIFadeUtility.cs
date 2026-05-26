using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.Utils
{
    /// <summary> 
    /// UI 컴포넌트의 투명도 조절 및 페이드 연출을 중앙 집중화하는 확장(Extension) 유틸리티 클래스. 
    /// </summary>
    public static class UIFadeUtility
    {
        /// <summary> 
        /// CanvasGroup의 알파값을 비동기로 보간합니다. 
        /// 사용 예: await myCanvasGroup.FadeAsync(0f, 1f, 0.5f, ct);
        /// </summary>
        public static async UniTask FadeAsync(this CanvasGroup cg, float start, float end, float duration, CancellationToken cancellationToken = default)
        {
            if (!cg) return;

            // duration이 0 이하일 경우 0 나누기(NaN) 에러 방지 및 즉시 적용
            if (duration <= 0f)
            {
                cg.alpha = end;
                return;
            }

            float timer = 0f;
            cg.alpha = start;
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                cg.alpha = Mathf.Lerp(start, end, t); 
                
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            
            cg.alpha = end;
        }

        /// <summary> 
        /// Graphic(Text, Image, RawImage) 컴포넌트의 알파값을 비동기로 보간합니다.
        /// 사용 예: await myImage.FadeAsync(1f, 0f, 1.0f, ct);
        /// </summary>
        public static async UniTask FadeAsync(this Graphic graphic, float start, float end, float duration, CancellationToken cancellationToken = default)
        {
            if (!graphic) return;

            // duration 0 예외 처리
            if (duration <= 0f)
            {
                graphic.SetAlpha(end);
                return;
            }

            float timer = 0f;
            graphic.SetAlpha(start);
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                
                graphic.SetAlpha(Mathf.Lerp(start, end, t));
                
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            
            graphic.SetAlpha(end);
        }

        /// <summary> 
        /// UI 컴포넌트의 알파값을 즉시 적용합니다.
        /// 사용 예: myText.SetAlpha(0.5f);
        /// </summary>
        public static void SetAlpha(this Graphic graphic, float alpha)
        {
            if (!graphic) return;
            
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}