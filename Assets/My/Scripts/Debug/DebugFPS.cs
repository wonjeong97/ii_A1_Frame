using System;
using UnityEngine;

namespace My.Scripts.Utils
{
    /// <summary> 
    /// 개발 빌드(Development Build) 및 에디터에서만 FPS를 표시하는 디버그 툴 
    /// </summary>
    public class DebugFPS : MonoBehaviour
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        
        [Header("Settings")]
        [SerializeField] private Color textColor = Color.green;
        [SerializeField] [Range(10, 100)] private int fontSize = 30;
        
        private float _deltaTime = 0.0f;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // 프레임 시간 갱신 (부드럽게 보정)
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        }

        private void OnGUI()
        {
            int w = Screen.width, h = Screen.height;

            GUIStyle style = new GUIStyle();

            // 좌측 상단에 표시
            Rect rect = new Rect(20, 20, w, h * 2 / 100);
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = fontSize;
            style.normal.textColor = textColor;

            // 계산: 밀리초(ms) 및 초당 프레임(FPS)
            float msec = _deltaTime * 1000.0f;
            float fps = 1.0f / _deltaTime;
            
            string text = string.Format("{0:0.0} ms ({1:0.} FPS)", msec, fps);
            
            GUI.Label(rect, text, style);
        }
#endif
    }
}