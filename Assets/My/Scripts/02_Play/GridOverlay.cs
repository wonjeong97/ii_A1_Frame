using System;
using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts._02_Play
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Image))]
    public class GridOverlay : MonoBehaviour
    {
        [Header("Settings")]
        [Range(1, 50)] public float gridCount = 10; // 10x10
        [Range(0.001f, 0.05f)] public float thickness = 0.005f; // 선 두께
        [Range(10, 100)] public float dashFrequency = 50f; // 점선 촘촘함

        private Image _image;
        private RectTransform _rectTransform;
        private Material _materialInstance;

        // 쉐이더 프로퍼티 ID 캐싱
        private static readonly int AspectRatioID = Shader.PropertyToID("_AspectRatio");
        private static readonly int GridCountID = Shader.PropertyToID("_GridCount");
        private static readonly int ThicknessID = Shader.PropertyToID("_Thickness");
        private static readonly int DashFreqID = Shader.PropertyToID("_DashFreq");

        private void OnEnable()
        {
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            UpdateMaterial();
        }

        private void Update()
        {
            // 에디터에서 값을 조절하거나, 해상도가 바뀔 때마다 갱신
            if (_image != null)
            {
                UpdateMaterial();
            }
        }

        private void UpdateMaterial()
        {
            if (_image.material == null || _image.material.shader.name != "UI/Grid") return;

            // 1. 이미지의 실제 비율 계산 (너비 / 높이)
            float width = _rectTransform.rect.width;
            float height = _rectTransform.rect.height;
            float aspectRatio = (height > 0) ? (width / height) : 1f;

            // 2. 머티리얼 인스턴스 생성 (원본 오염 방지) - 런타임에만
            if (Application.isPlaying && _materialInstance == null)
            {
                _materialInstance = Instantiate(_image.material);
                _image.material = _materialInstance;
            }

            Material mat = Application.isPlaying ? _materialInstance : _image.material;

            // 3. 값 전달
            mat.SetFloat(AspectRatioID, aspectRatio);
            mat.SetFloat(GridCountID, gridCount);
            mat.SetFloat(ThicknessID, thickness);
            mat.SetFloat(DashFreqID, dashFrequency);
        }
        
        // Instantiate로 생성한 머티리얼 OnDestroy에서 명시적으로 해제
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