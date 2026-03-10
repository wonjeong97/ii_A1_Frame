using My.Scripts.Core;
using UnityEngine;

namespace My.Scripts.Global
{
    public enum UserType
    {
        A, B, C, D, E, F
    }

    /// <summary>
    /// 현재 플레이 중인 사용자의 세션 데이터(이름, UID, 점수 등)를 전담하여 보관하는 매니저입니다.
    /// GameManager에서 분리되어 단일 책임 원칙(SRP)을 준수합니다.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        public int CurrentUserId { get; set; } 
        public string PlayerAUid { get; set; } = string.Empty;
        public string PlayerBUid { get; set; } = string.Empty;
        public string CurrentLanguage { get; set; } = "ko";
        
        public string PlayerALastName { get; set; } = "NoNameA";
        public string PlayerBLastName { get; set; } = "NoNameB";
        
        public ColorData PlayerAColor { get; set; } = ColorData.NotSet;
        public ColorData PlayerBColor { get; set; } = ColorData.NotSet;

        public UserType CurrentUserType { get; set; } = UserType.A;
        public string CurrentModuleCode { get; set; } = GameConstants.Module.Code;
        public string Cartridge { get; set; } = string.Empty;
        
        public bool IsOtherCartridgeContentsCleared { get; set; } = false;

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
        
        public int TotalPieces => PieceA1 + PieceA2 + PieceA3 + 
                                  PieceB1 + PieceB2 + PieceB3 + 
                                  PieceC1 + PieceC2 + PieceC3 + 
                                  PieceD1 + PieceD2 + PieceD3;

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
            CurrentLanguage = "ko";
            
            PlayerALastName = "NoNameA";
            PlayerBLastName = "NoNameB";
            
            PlayerAColor = ColorData.NotSet;
            PlayerBColor = ColorData.NotSet;

            CurrentUserType = UserType.A;
            CurrentModuleCode = GameConstants.Module.Code;
            Cartridge = string.Empty;
            
            IsOtherCartridgeContentsCleared = false;

            PieceA1 = 0; PieceA2 = 0; PieceA3 = 0;
            PieceB1 = 0; PieceB2 = 0; PieceB3 = 0;
            PieceC1 = 0; PieceC2 = 0; PieceC3 = 0;
            PieceD1 = 0; PieceD2 = 0; PieceD3 = 0;
        }
    }
}