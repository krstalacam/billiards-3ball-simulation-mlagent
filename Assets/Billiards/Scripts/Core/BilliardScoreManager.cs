using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 3 Top Bilardo oyun kurallarını ve skor takibini yönetir.
/// Topların çarpışmalarını dinler ve puan durumunu hesaplar.
/// </summary>
public class BilliardScoreManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Min(1)] private int _winningScore = 10;

    [Header("Debug")]
    [SerializeField] private bool _showDebugLogs = true;

    // State
    private BilliardBall _currentCueBall;
    private int _wallHitCount;
    private HashSet<BilliardBall> _hitBalls = new HashSet<BilliardBall>();
    private bool _turnInProgress;
    
    // Player Scores
    public int Player1Score { get; private set; }
    public int Player2Score { get; private set; }
    public int WinningScore => Mathf.Max(1, _winningScore);
    public bool IsTurnActive => _turnInProgress;
    
    // Faul türlerini ayır
    private bool _outOfBoundsFoul = false;  // Top dışarı çıktı
    private bool _otherFoul = false;         // Diğer faul türleri

    // Events
    // int: wallCount, int: ballCount, int: p1Score, int: p2Score
    public event System.Action<int, int, int, int> OnScoreUpdate;
    public event System.Action OnScoreCommitted; // Yeni event: Sayı kazanıldığında tetiklenir
    // Yeni event: Top dışarı çıktığında anında bildirim (detektörden sonra çağrılır)
    public event System.Action OnOutOfBoundsRegistered;

    /// <summary>
    /// Yeni bir vuruş başladığında çağrılır.
    /// Takip edilecek topu (vuran top) ayarlar ve sayaçları sıfırlar.
    /// Her çağrıda sayaçlar kesinlikle sıfırlanır.
    /// UI'da vuruş yapılmadan önce 0 gösterilir.
    /// </summary>
    public void StartTurn(BilliardBall cueBall, CueStick activeCue)
    {
        // Önceki toptan aboneliği kaldır
        if (_currentCueBall != null)
        {
            _currentCueBall.OnBallCollision -= HandleBallCollision;
        }

        _currentCueBall = cueBall;
        ResetTurnStats(); // Her çağrıda sayaçları sıfırla ve UI'yı 0 olarak göster
        RegisterInitialWallContact(activeCue); // İlk duvar temasını kaydet (internal, UI'da görünmez)
        _turnInProgress = true;

        // Yeni topa abone ol
        if (_currentCueBall != null)
        {
            _currentCueBall.OnBallCollision += HandleBallCollision;
        }
        
        if (_showDebugLogs) Debug.Log($"[ScoreManager] Turn Started for {_currentCueBall.name}. Stats reset. Internal walls: {_wallHitCount}, UI shows: 0");
    }

    /// <summary>
    /// Tur istatistiklerini sıfırlar.
    /// UI'yı 0 olarak günceller (vuruş yapılmadan önce).
    /// </summary>
    private void ResetTurnStats()
    {
        _wallHitCount = 0;
        _hitBalls.Clear();
        _outOfBoundsFoul = false;
        _otherFoul = false;
        // UI'yı 0 olarak göster
        OnScoreUpdate?.Invoke(0, 0, Player1Score, Player2Score);
    }

    public void RegisterOutOfBoundsFoul()
    {
        _outOfBoundsFoul = true;
        if (_showDebugLogs) Debug.Log("[ScoreManager] Out of Bounds Foul Registered!");
        // Immediately notify listeners (UI / reward manager) about updated score state
        NotifyScoreUpdate();
        OnOutOfBoundsRegistered?.Invoke();
    }

    public void RegisterFoul()
    {
        _otherFoul = true;
        if (_showDebugLogs) Debug.Log("[ScoreManager] Foul Registered!");
    }

    public bool HasFoul() => _outOfBoundsFoul || _otherFoul;
    public bool HasOutOfBoundsFoul() => _outOfBoundsFoul;

    public void CommitScore(int playerIndex)
    {
        if (playerIndex == 0) Player1Score++;
        else Player2Score++;
        
        if (_showDebugLogs)
        {
            Debug.Log($"[ScoreManager] Score Committed! P1: {Player1Score}, P2: {Player2Score}");
            Debug.Log("[ScoreManager] CommitScore called at: " + DateTime.Now.ToString("HH:mm:ss.fff") + "\nStack:\n" + Environment.StackTrace);
        }
        
        OnScoreCommitted?.Invoke(); // Eventi tetikle

        // Reset stats immediately
        _wallHitCount = 0;
        _hitBalls.Clear();
        _outOfBoundsFoul = false;
        _otherFoul = false;
        _turnInProgress = false;
        
        NotifyScoreUpdate();
    }

    public bool IsGameWon(out int winnerIndex)
    {
        if (Player1Score >= WinningScore) { winnerIndex = 0; return true; }
        if (Player2Score >= WinningScore) { winnerIndex = 1; return true; }
        winnerIndex = -1;
        return false;
    }

    public void ResetScores()
    {
        if (_showDebugLogs)
        {
            Debug.LogWarning("[ScoreManager] ResetScores called! Resetting player scores to 0.");
            Debug.LogWarning("[ScoreManager] ResetScores stack trace:\n" + Environment.StackTrace);
        }

        Player1Score = 0;
        Player2Score = 0;
        _turnInProgress = false;
        NotifyScoreUpdate();
    }

    /// <summary>
    /// Top çarpışmalarını işler.
    /// </summary>
    private void HandleBallCollision(BilliardBall ball, Collision collision)
    {
        // Sadece takip ettiğimiz topun çarpışmalarıyla ilgileniyoruz
        if (ball != _currentCueBall) return;

        GameObject hitObj = collision.gameObject;

        // 1. Duvar (Bant) Kontrolü
        // Not: Unity'de bantların tag'i "Wall" olmalı.
        if (hitObj.CompareTag("Wall"))
        {
            _wallHitCount++;
            if (_showDebugLogs) Debug.Log($"[ScoreManager] Wall Hit! Total: {_wallHitCount}");
            NotifyScoreUpdate();
        }
        // 2. Top Kontrolü
        else if (hitObj.TryGetComponent<BilliardBall>(out var hitBall))
        {
            // Kendi kendine çarpma durumu olmaz ama yine de kontrol edelim.
            // Daha önce vurulmamış bir topsa listeye ekle.
            if (hitBall != _currentCueBall && !_hitBalls.Contains(hitBall))
            {
                _hitBalls.Add(hitBall);
                if (_showDebugLogs) Debug.Log($"[ScoreManager] Ball Hit: {hitBall.name}! Total Unique Balls: {_hitBalls.Count}");
                NotifyScoreUpdate();
            }
        }
    }

    /// <summary>
    /// Puan alma şartlarının sağlanıp sağlanmadığını kontrol eder.
    /// Kural: En az 3 bant + 2 farklı topa temas.
    /// </summary>
    public bool CheckScoreCondition()
    {
        bool success = _wallHitCount >= 3 && _hitBalls.Count >= 2;
        
        if (_showDebugLogs)
        {
            string status = success ? "SUCCESS" : "FAIL";
            Debug.Log($"[ScoreManager] Check Condition: {status} (Walls: {_wallHitCount}/3, Balls: {_hitBalls.Count}/2)");
        }
        
        return success;
    }

    /// <summary>
    /// Tur başlarken top zaten duvara temas ediyorsa ilk duvar vuruşu sayar.
    /// Ancak vuruşun duvara doğru yapılıp yapılmadığını kontrol eder.
    /// NOT: Bu internal sayaçtır, UI'da görünmez. Vuruş yapılınca UI güncellenir.
    /// </summary>
    private void RegisterInitialWallContact(CueStick activeCue)
    {
        if (_currentCueBall == null) return;

        // If no cue stick provided, fallback to simple contact check
        if (activeCue == null)
        {
            if (_currentCueBall.TryGetContactWithTags(out _, "Wall"))
            {
                _wallHitCount = Mathf.Max(_wallHitCount, 1);
                if (_showDebugLogs) Debug.Log("[ScoreManager] Cue ball started turn touching a wall (No Cue Check). Counting as initial hit (internal, not shown in UI yet).");
                // UI güncellemesi yok - vuruş yapılınca gösterilecek
            }
            return;
        }

        // With cue stick, check direction
        Vector3 shotDir = activeCue.transform.forward;
        shotDir.y = 0; // Flatten for 2D check

        if (shotDir.sqrMagnitude > 0.001f)
        {
            if (_currentCueBall.IsTouchingWallInDirection(shotDir, "Wall"))
            {
                _wallHitCount = Mathf.Max(_wallHitCount, 1);
                if (_showDebugLogs) Debug.Log("[ScoreManager] Cue ball started turn touching a wall AND aiming at it. Counting as initial hit (internal, not shown in UI yet).");
                // UI güncellemesi yok - vuruş yapılınca gösterilecek
            }
            else if (_showDebugLogs)
            {
                // Debug logging for "Touching but aiming away"
                if (_currentCueBall.TryGetContactWithTags(out var wall, "Wall"))
                {
                    Debug.Log($"[ScoreManager] Ball touching wall {wall.name}, but shot aimed away. Not counting initial hit.");
                }
            }
        }
    }

    /// <summary>
    /// UI ve diğer sistemleri bilgilendirir.
    /// </summary>
    private void NotifyScoreUpdate()
    {
        OnScoreUpdate?.Invoke(_wallHitCount, _hitBalls.Count, Player1Score, Player2Score);
    }

    public void PrepareForNextTurnVisuals()
    {
        if (_turnInProgress) return;

        _wallHitCount = 0;
        _hitBalls.Clear();
        _outOfBoundsFoul = false;
        _otherFoul = false;
        NotifyScoreUpdate();
    }

    public void FinalizeTurnTracking()
    {
        if (_currentCueBall != null)
        {
            _currentCueBall.OnBallCollision -= HandleBallCollision;
            _currentCueBall = null;
        }

        _turnInProgress = false;
    }

    /// <summary>
    /// Vuruş yapıldığında çağrılır. UI'yı gerçek değerlerle günceller.
    /// Başlangıçta duvara temas varsa artık UI'da gösterilir.
    /// </summary>
    public void OnShotExecuted()
    {
        if (_showDebugLogs) Debug.Log($"[ScoreManager] Shot executed! Updating UI with current stats: Walls={_wallHitCount}, Balls={_hitBalls.Count}");
        NotifyScoreUpdate();
    }
    
    private void OnDestroy()
    {
        // Temizlik
        if (_currentCueBall != null)
        {
            _currentCueBall.OnBallCollision -= HandleBallCollision;
        }
    }
}
