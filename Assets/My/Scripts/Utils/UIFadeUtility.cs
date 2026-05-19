using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.Utils
{
    /// <summary> 
    /// UI 컴포넌트의 투명도 조절 및 페이드 연출을 중앙 집중화하는 확장(Extension) 유틸리티 클래스. 
    /// Divide-by-Zero 방어 및 Clamp 안전망이 적용되었습니다.
    /// </summary>
    public static class UIFadeUtility
    {
        /// <summary> 
        /// CanvasGroup의 알파값을 비동기로 보간합니다. (확장 메서드 적용)
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
                
                // [안정성 & 퀄리티] 비율이 1.0을 초과하지 않도록 Clamp01 적용
                float t = Mathf.Clamp01(timer / duration);
                
                // Tip: 더 부드러운 고급 연출을 원한다면 Mathf.SmoothStep(start, end, t) 사용 권장
                cg.alpha = Mathf.Lerp(start, end, t); 
                
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            
            cg.alpha = end;
        }

        /// <summary> 
        /// Graphic(Text, Image, RawImage) 컴포넌트의 알파값을 비동기로 보간합니다. (확장 메서드 적용)
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
        /// UI 컴포넌트의 알파값을 즉시 적용합니다. (확장 메서드 적용)
        /// 사용 예: myText.SetAlpha(0.5f);
        /// </summary>
        public static void SetAlpha(this Graphic graphic, float alpha)
        {
            if (!graphic) return;
            
            // 기존 color 구조체를 복사 후 알파값만 변경하여 재할당 (가비지 0)
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}