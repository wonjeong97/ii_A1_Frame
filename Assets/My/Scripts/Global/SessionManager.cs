using System;
using My.Scripts.Core;
using UnityEngine;

namespace My.Scripts.Global
{
    // 카트리지(A~D)와 관계(1~5)의 조합으로 20가지 경우의 수 생성
    public enum UserType
    {
        A1, A2, A3, A4, A5, 
        B1, B2, B3, B4, B5,
        C1, C2, C3, C4, C5,
        D1, D2, D3, D4, D5
    }

    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }
        
        public event Action<string> OnLanguageChanged;

        public int CurrentUserId { get; set; } 
        public string PlayerAUid { get; set; } = string.Empty;
        public string PlayerBUid { get; set; } = string.Empty;
        
        private string _currentLanguage = "ko";
        public string CurrentLanguage 
        { 
            get => _currentLanguage; 
            set 
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnLanguageChanged?.Invoke(_currentLanguage);
                }
            } 
        }
        public string BlockCode { get; set; } = string.Empty;
        
        public string PlayerAFirstName { get; set; } = "NoNameA";
        public string PlayerBFirstName { get; set; } = "NoNameB";
        
        public ColorData PlayerAColor { get; set; } = ColorData.NotSet;
        public ColorData PlayerBColor { get; set; } = ColorData.NotSet;
        
        public UserType CurrentUserType { get; set; } = UserType.A1;
        public string CurrentModuleCode { get; set; } = GameConstants.Module.Code;
        public string Cartridge { get; set; } = string.Empty;
        
        public bool IsOtherCartridgeContentsCleared { get; set; }
        public int ClearedEndCount { get; set; } 

        public int PieceA1 { get; set; }
        public int PieceA2 { get; set; }
        public int PieceA3 { get; set; }
        public int PieceB1 { get; set; }
        public int PieceB2 { get; set; }
        public int PieceB3 { get; set; }
        public int PieceC1 { get; set; }
        public int PieceC2 { get; set; }
        public int PieceC3 { get; set; }
        public int PieceD1 { get; set; }
        public int PieceD2 { get; set; }
        public int PieceD3 { get; set; }
        
        public int TotalPieces
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BlockCode)) 
                {
                    return GetDefaultTotalPieces();
                }

                return CalculatePiecesFromBlockCode();
            }
        }
        
        /// <summary>
        /// 세션 데이터 누락 시 안전망(Fallback)으로 동작함.
        /// A1은 현재 진행 컨텐츠이므로 합산에서 제외함.
        /// </summary>
        private int GetDefaultTotalPieces()
        {
            return PieceA2 + PieceA3 +
                   PieceB1 + PieceB2 + PieceB3 +
                   PieceC1 + PieceC2 + PieceC3 +
                   PieceD1 + PieceD2 + PieceD3;
        }
        
        /// <summary>
        /// 획득한 블록 코드를 문자열 할당(GC) 없이 인덱스 기반으로 순회하며 조각 개수를 합산함.
        /// 흐름 제어, 모듈 식별, 문자 분석의 책임을 분리하여 복잡도를 낮춤.
        /// </summary>
        private int CalculatePiecesFromBlockCode()
        {
            if (string.IsNullOrEmpty(BlockCode))
            {
                return 0;
            }

            (char currentMod1, char currentMod2) = GetCurrentModuleChars();

            return SumParsedBlocks(BlockCode, currentMod1, currentMod2);
        }
        
        /// <summary>
        /// 기준이 되는 현재 모듈 코드를 두 개의 문자로 분리하여 캐싱 반환함.
        /// </summary>
        private (char, char) GetCurrentModuleChars()
        {
            if (string.IsNullOrEmpty(CurrentModuleCode) || CurrentModuleCode.Length < 2)
            {
                return ('\0', '\0');
            }

            return (char.ToUpperInvariant(CurrentModuleCode[0]), char.ToUpperInvariant(CurrentModuleCode[1]));
        }
        
        /// <summary>
        /// 전체 블록 코드 문자열을 순회하며 개별 문자를 처리 및 누적 합산함.
        /// </summary>
        private int SumParsedBlocks(string blocks, char currentMod1, char currentMod2)
        {
            int sum = 0;
            char parsedMod1 = '\0';
            char parsedMod2 = '\0';
            int length = blocks.Length;

            for (int i = 0; i < length; i++)
            {
                ProcessBlockChar(blocks[i], ref parsedMod1, ref parsedMod2, ref sum, currentMod1, currentMod2);
            }

            // 루프 종료 후 마지막에 남은 잔여 파싱 블록 처리
            sum += EvaluateAndGetPieceCount(parsedMod1, parsedMod2, currentMod1, currentMod2);
            return sum;
        }
        
        /// <summary>
        /// 단일 문자를 평가하여 임시 변수에 캐싱하거나, 구분자(,)를 만나면 합산 후 변수를 리셋함.
        /// </summary>
        private void ProcessBlockChar(char c, ref char parsedMod1, ref char parsedMod2, ref int sum, char currentMod1, char currentMod2)
        {
            if (char.IsWhiteSpace(c))
            {
                return;
            }

            if (c == ',')
            {
                sum += EvaluateAndGetPieceCount(parsedMod1, parsedMod2, currentMod1, currentMod2);
                parsedMod1 = '\0';
                parsedMod2 = '\0';
                return;
            }

            // Split 없이 쉼표 이전의 유효 알파벳 및 숫자를 순차적으로 기록함
            if (parsedMod1 == '\0')
            {
                parsedMod1 = char.ToUpperInvariant(c);
            }
            else if (parsedMod2 == '\0')
            {
                parsedMod2 = char.ToUpperInvariant(c);
            }
        }

        /// <summary>
        /// 파싱된 콘텐츠 코드가 유효한지, 그리고 현재 진행 중인 모듈이 아닌지 검증한 후 조각 개수를 반환함.
        /// </summary>
        private int EvaluateAndGetPieceCount(char parsed1, char parsed2, char current1, char current2)
        {
            if (parsed1 == '\0' || parsed2 == '\0')
            {
                return 0;
            }

            if (parsed1 == current1 && parsed2 == current2)
            {
                return 0;
            }

            return GetPieceCount(parsed1, parsed2);
        }

        /// <summary>
        /// 콘텐츠 식별 문자를 조합하여 실제 조각 개수를 반환함.
        /// 거대한 switch 문을 그룹별 하위 메서드로 나누어 코드 복잡도를 낮춤.
        /// </summary>
        private int GetPieceCount(char mod1, char mod2)
        {
            switch (mod1)
            {
                case 'A': return GetPieceGroupA(mod2);
                case 'B': return GetPieceGroupB(mod2);
                case 'C': return GetPieceGroupC(mod2);
                case 'D': return GetPieceGroupD(mod2);
                default: return 0;
            }
        }

        private int GetPieceGroupA(char mod2)
        {
            if (mod2 == '1') return PieceA1;
            if (mod2 == '2') return PieceA2;
            if (mod2 == '3') return PieceA3;
            return 0;
        }

        private int GetPieceGroupB(char mod2)
        {
            if (mod2 == '1') return PieceB1;
            if (mod2 == '2') return PieceB2;
            if (mod2 == '3') return PieceB3;
            return 0;
        }

        private int GetPieceGroupC(char mod2)
        {
            if (mod2 == '1') return PieceC1;
            if (mod2 == '2') return PieceC2;
            if (mod2 == '3') return PieceC3;
            return 0;
        }

        private int GetPieceGroupD(char mod2)
        {
            if (mod2 == '1') return PieceD1;
            if (mod2 == '2') return PieceD2;
            if (mod2 == '3') return PieceD3;
            return 0;
        }

        /// <summary>
        /// 개별 블록 코드 문자열에 매핑되는 실제 조각 개수를 반환함.
        /// 타 클래스에서 문자열로 조회할 경우를 대비한 오버로딩 헬퍼.
        /// </summary>
        private int GetPieceCount(string blockCode)
        {
            if (string.IsNullOrEmpty(blockCode) || blockCode.Length < 2)
            {
                return 0;
            }
            
            char mod1 = char.ToUpperInvariant(blockCode[0]);
            char mod2 = char.ToUpperInvariant(blockCode[1]);
            
            return GetPieceCount(mod1, mod2);
        }

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void ClearSession()
        {
            CurrentUserId = 0;
            PlayerAUid = string.Empty;
            PlayerBUid = string.Empty;
            BlockCode = string.Empty;
            CurrentLanguage = "ko";
            
            PlayerAFirstName = "NoNameA";
            PlayerBFirstName = "NoNameB";
            
            PlayerAColor = ColorData.NotSet;
            PlayerBColor = ColorData.NotSet;

            CurrentUserType = UserType.A1;
            CurrentModuleCode = GameConstants.Module.Code;
            Cartridge = string.Empty;
            
            IsOtherCartridgeContentsCleared = false;
            ClearedEndCount = 0; 

            PieceA1 = 0; PieceA2 = 0; PieceA3 = 0;
            PieceB1 = 0; PieceB2 = 0; PieceB3 = 0;
            PieceC1 = 0; PieceC2 = 0; PieceC3 = 0;
            PieceD1 = 0; PieceD2 = 0; PieceD3 = 0;
        }
    }
}