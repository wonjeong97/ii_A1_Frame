using My.Scripts.Core;
using UnityEngine;

namespace My.Scripts.Global
{
    // 카트리지(A~D)와 관계(1~6)의 조합으로 24가지 경우의 수 생성
    public enum UserType
    {
        A1, A2, A3, A4, A5, A6,
        B1, B2, B3, B4, B5, B6,
        C1, C2, C3, C4, C5, C6,
        D1, D2, D3, D4, D5, D6
    }

    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        public int CurrentUserId { get; set; } 
        public string PlayerAUid { get; set; } = string.Empty;
        public string PlayerBUid { get; set; } = string.Empty;
        public string CurrentLanguage { get; set; } = "ko";
        public string BlockCode { get; set; } = string.Empty;
        
        
        public string PlayerAFirstName { get; set; } = "NoNameA";
        public string PlayerBFirstName { get; set; } = "NoNameB";
        
        public ColorData PlayerAColor { get; set; } = ColorData.NotSet;
        public ColorData PlayerBColor { get; set; } = ColorData.NotSet;
        
        public UserType CurrentUserType { get; set; } = UserType.A1;
        public string CurrentModuleCode { get; set; } = GameConstants.Module.Code;
        public string Cartridge { get; set; } = string.Empty;
        
        public bool IsOtherCartridgeContentsCleared { get; set; } = false;
        public int ClearedEndCount { get; set; } = 0; 

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
                if (string.IsNullOrEmpty(BlockCode)) 
                {
                    return 0;
                }

                int sum = 0;
                string[] blocks = BlockCode.Split(',');

                foreach (string b in blocks)
                {
                    string block = b.Trim().ToUpper();
            
                    // 현재 진행 중인 모듈은 합산에서 제외 (엔딩에서 보상으로 따로 더해짐)
                    if (block == CurrentModuleCode.ToUpper()) 
                    {
                        continue;
                    }

                    switch (block)
                    {
                        case "A1": sum += PieceA1; break;
                        case "A2": sum += PieceA2; break;
                        case "A3": sum += PieceA3; break;
                        case "B1": sum += PieceB1; break;
                        case "B2": sum += PieceB2; break;
                        case "B3": sum += PieceB3; break;
                        case "C1": sum += PieceC1; break;
                        case "C2": sum += PieceC2; break;
                        case "C3": sum += PieceC3; break;
                        case "D1": sum += PieceD1; break;
                        case "D2": sum += PieceD2; break;
                        case "D3": sum += PieceD3; break;
                    }
                }
                return sum;
            }
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