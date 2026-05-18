using UnityEngine;

namespace My.Scripts.Utils
{
    /// <summary>
    /// 다이얼 조작 상태를 저장하는 구조체.
    /// 플레이어별 개별 상태 관리를 위해 값 타입으로 정의함.
    /// </summary>
    public struct PlayerWheelState
    {
        public int lastKey;
        public int stepCount;
        public float lastTime;
        public int lastDir;
        public float inputTimer;

        // 정적 읽기 전용 필드로 단 1회만 할당하여 가비지(GC) 완전 제거
        public readonly static PlayerWheelState Default = new PlayerWheelState { lastKey = -1 };
    }

    /// <summary>
    /// 물리 다이얼의 입력 계산을 처리하는 유틸리티 클래스.
    /// 하드웨어 노이즈 필터링, 경계선 모듈로 연산, 상태 갱신을 내부에서 100% 캡슐화하여 처리함.
    /// </summary>
    public static class WheelInputUtility
    {
        private const float FastInputThreshold = 0.2f;
        
        // 캐스팅 오버헤드 제거용 베이스 키값
        private const int Alpha0Int = (int)KeyCode.Alpha0;

        /// <summary>
        /// 지정된 범위의 숫자 키 입력 중 눌린 키의 인덱스를 반환함.
        /// </summary>
        public static int GetPressedKeyIndex(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                if (Input.GetKeyDown((KeyCode)(Alpha0Int + i)))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 현재 입력된 키를 기반으로 회전 방향을 계산하고, 노이즈 필터링 및 상태 갱신을 자동으로 수행함.
        /// </summary>
        /// <param name="currentKey">방금 눌린 키 인덱스</param>
        /// <param name="totalSteps">다이얼의 전체 단계 수 (예: 0~3이면 4)</param>
        /// <param name="state">플레이어의 현재 다이얼 상태 (ref로 자동 갱신됨)</param>
        /// <returns>회전 방향 (-1: 역회전, 0: 무효/정지, 1: 정회전)</returns>
        public static int ResolveDirection(int currentKey, int totalSteps, ref PlayerWheelState state)
        {
            // 최초 입력인 경우 기준점만 잡고 회전(방향)은 무시
            if (state.lastKey == -1)
            {
                state.lastKey = currentKey;
                return 0;
            }

            int rawDiff = currentKey - state.lastKey;
            
            // 모듈로(Modulo) 연산을 통한 경계선(Wrap-around) 음수값 완벽 보정
            // 예: 3번에서 0번으로 갈 때 rawDiff는 -3이지만, (-3 + 4) % 4 = 1 (정회전)로 정상 변환됨.
            int safeDiff = (rawDiff + totalSteps) % totalSteps;

            int direction = 0;
            if (safeDiff == 1) 
            {
                direction = 1;
            }
            else if (safeDiff == totalSteps - 1) // (예: 4스텝일 때 safeDiff가 3이면 역회전)
            {
                direction = -1;
            }

            // 게임 일시정지(TimeScale=0) 시에도 하드웨어 입력은 씹히지 않도록 현실 시간(unscaledTime) 사용
            float now = Time.unscaledTime;

            // 빠른 연타/바운스 방어 로직 (방향이 비정상적으로 튀는 것 방지)
            if (now - state.lastTime < FastInputThreshold && state.lastDir != 0)
            {
                if (safeDiff == 2 || (direction != 0 && direction != state.lastDir))
                {
                    // 바운스/오입력으로 판정되면 이전 방향 유지 (상태값 갱신 없이 즉시 반환)
                    return state.lastDir;
                }
            }

            // 방향이 결정되었을 때 유틸리티 내부에서 상태(State)를 자동 갱신
            if (direction != 0)
            {
                state.lastDir = direction;
                state.lastTime = now;
                state.lastKey = currentKey; // 다음 비교를 위해 현재 키 저장
            }

            return direction;
        }
    }
}