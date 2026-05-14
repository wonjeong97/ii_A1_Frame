using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.Utils
{
    /// <summary> UI 컴포넌트의 투명도 조절 및 페이드 연출을 중앙 집중화하는 유틸리티 클래스. </summary>
    public static class UIFadeUtility
    {
        /// <summary> CanvasGroup의 알파값을 선형 보간함. </summary>
        public static async UniTask FadeCanvasGroupAsync(CanvasGroup cg, float start, float end, float duration, CancellationToken cancellationToken = default)
        {
            if (!cg) return;

            float timer = 0f;
            cg.alpha = start;
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, timer / duration);
                
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            
            cg.alpha = end;
        }

        /// <summary> Graphic(Text, Image, RawImage) 컴포넌트의 알파값을 선형 보간함. </summary>
        public static async UniTask FadeGraphicAsync(Graphic graphic, float start, float end, float duration, CancellationToken cancellationToken = default)
        {
            if (!graphic) return;

            float timer = 0f;
            SetAlpha(graphic, start);
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                SetAlpha(graphic, Mathf.Lerp(start, end, timer / duration));
                
                // 다음 프레임 대기 및 파괴 시 안전한 취소 처리
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            
            SetAlpha(graphic, end);
        }

        /// <summary> UI 컴포넌트의 알파값을 즉시 적용함. </summary>
        public static void SetAlpha(Graphic graphic, float alpha)
        {
            if (!graphic) return;
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}