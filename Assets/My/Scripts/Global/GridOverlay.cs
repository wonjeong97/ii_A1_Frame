using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts._02_Play_Tutorial
{
    /// <summary> 그리드 쉐이더 제어용 UI 오버레이 클래스 </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Image))]
    public class GridOverlay : MonoBehaviour
    {
        [Header("Settings")]
        [Range(1, 50)] public float gridCount = 10; // 그리드 분할 개수 (10x10)
        [Range(0.001f, 0.05f)] public float thickness = 0.005f; // 선 두께
        [Range(10, 100)] public float dashFrequency = 50f; // 점선 빈도

        private Image _image; // 대상 이미지 컴포넌트
        private RectTransform _rectTransform; // RectTransform 참조
        private Material _materialInstance; // 런타임 인스턴스 머티리얼

        // 쉐이더 프로퍼티 ID 캐싱
        private static readonly int AspectRatioID = Shader.PropertyToID("_AspectRatio");
        private static readonly int GridCountID = Shader.PropertyToID("_GridCount");
        private static readonly int ThicknessID = Shader.PropertyToID("_Thickness");
        private static readonly int DashFreqID = Shader.PropertyToID("_DashFreq");

        /// <summary> 컴포넌트 캐싱 및 초기화 </summary>
        private void OnEnable()
        {
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            UpdateMaterial();
        }

        /// <summary> 실시간 갱신 (에디터/런타임) </summary>
        private void Update()
        {
            // 에디터 값 조절 또는 해상도 변경 대응
            if (_image != null)
            {
                UpdateMaterial();
            }
        }

        /// <summary> 쉐이더 파라미터 업데이트 </summary>
        private void UpdateMaterial()
        {
            if (_image.material == null || _image.material.shader.name != "UI/Grid") return;

            // 1. 이미지 실제 비율 계산
            float width = _rectTransform.rect.width;
            float height = _rectTransform.rect.height;
            float aspectRatio = (height > 0) ? (width / height) : 1f;

            // 2. 머티리얼 인스턴스 생성 (런타임 원본 보호)
            if (Application.isPlaying && _materialInstance == null)
            {
                _materialInstance = Instantiate(_image.material);
                _image.material = _materialInstance;
            }

            Material mat = Application.isPlaying ? _materialInstance : _image.material;

            // 3. 쉐이더 값 전달
            mat.SetFloat(AspectRatioID, aspectRatio);
            mat.SetFloat(GridCountID, gridCount);
            mat.SetFloat(ThicknessID, thickness);
            mat.SetFloat(DashFreqID, dashFrequency);
        }
        
        /// <summary> 머티리얼 인스턴스 정리 </summary>
        private void OnDestroy()
        {
            if (_materialInstance != null)
            {
                if (Application.isPlaying) Destroy(_materialInstance);
                else DestroyImmediate(_materialInstance);
            }
        }
    }
}