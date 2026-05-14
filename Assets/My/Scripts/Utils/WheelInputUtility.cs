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

        public static PlayerWheelState Default => new PlayerWheelState { lastKey = -1 };
    }

    /// <summary>
    /// 물리 다이얼의 입력 계산을 처리하는 유틸리티 클래스.
    /// 여러 컨트롤러에서 중복되는 방향 판별 및 키 입력 감지 로직을 중앙화하기 위함.
    /// </summary>
    public static class WheelInputUtility
    {
        private const float FastInputThreshold = 0.2f;

        /// <summary>
        /// 지정된 범위의 숫자 키 입력 중 눌린 키의 인덱스를 반환함.
        /// 하드웨어 다이얼 회전 신호를 키보드 입력으로 매핑하여 감지하기 위함.
        /// </summary>
        public static int GetPressedKeyIndex(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                KeyCode key = (KeyCode)((int)KeyCode.Alpha0 + i);
                if (Input.GetKeyDown(key))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 이전 키와 현재 키의 차이를 비교하여 회전 방향을 결정함.
        /// 다이얼의 물리적 바운스나 의도치 않은 역회전 입력을 필터링하기 위함.
        /// </summary>
        public static int ResolveDirection(int diff, float now, ref PlayerWheelState state)
        {
            int direction = 0;
            
            if (diff == 1)
            {
                direction = 1;
            }
            else if (diff == 3)
            {
                direction = -1;
            }

            if (now - state.lastTime < FastInputThreshold && state.lastDir != 0)
            {
                if (diff == 2 || (direction != 0 && direction != state.lastDir))
                {
                    return state.lastDir;
                }
            }

            return direction;
        }
    }
}