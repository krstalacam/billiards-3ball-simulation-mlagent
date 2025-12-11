using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Bilardo oyunu için genel yönetici
/// Topların durumunu, vuruş sırasını ve oyun kurallarını yönetir
/// </summary>
public class BilliardGameManager : MonoBehaviour
{
    public enum GameMode
    {
        SinglePlayer,       // Sadece Player, Agent yok
        TwoPlayer,          // Sırayla sadece Player oynar
        PlayerVsAi,         // Player vs Agent
        AiOnly              // Sadece Agent (eğitim/izleme)
    }

    public enum BallMode
    {
        SameBall,      // Herkes aynı topa vurur (Main Ball)
        DifferentBalls // Player Main'e, Agent Secondary'e vurur
    }

    [Header("Settings")]
    [SerializeField] private GameSettings _gameSettings;

    public GameMode CurrentGameMode => _gameSettings != null ? _gameSettings.CurrentGameMode : GameMode.PlayerVsAi;
    public BallMode CurrentBallMode => _gameSettings != null ? _gameSettings.CurrentBallMode : BallMode.SameBall;

    [Header("Ball References")]
    [SerializeField] private BilliardBall _mainBall;
    [SerializeField] private BilliardBall _targetBall;
    [SerializeField] private BilliardBall _secondaryBall;
    
    [Header("Cue Sticks")]
    [SerializeField] private CueStick _primaryCueStick;
    [SerializeField] private CueStick _secondaryCueStick;
    
    public CueStick PrimaryCueStick => _primaryCueStick;
    public CueStick SecondaryCueStick => _secondaryCueStick;
    
    [Header("Game Settings")]
    [SerializeField] private float _stopVelocityThreshold = 0.2f; // Tüm toplar durdu kabul edilmeden önceki bekleme süresi
    [SerializeField] private float _maxWaitTime = 15f; // Topların durması için maksimum bekleme süresi (8 -> 15)
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;
    
    private bool _ballsAreMoving = false;
    private float _waitTimer = 0f;
    private bool _resetInProgress = false;
    private BallMode _lastBallMode; // For tracking runtime changes
    private GameMode _lastGameMode; // For tracking runtime changes

    public bool IsResetting => _resetInProgress;

    public event Action BallsStopped;
    /// <summary>
    /// Invoked when a reset routine completes. Allows flow managers to react
    /// to a user/test/AI triggered reset.
    /// </summary>
    public event Action OnGameReset;

    private void Awake()
    {
        ValidateReferences();
        AssignCueStickOwners();
        UpdateCueStickVisibility(); // AI Only modunda Player isteka gizlenir
        AssignCueStickTargetBalls();
        _lastBallMode = CurrentBallMode; // Store the initial mode
        _lastGameMode = CurrentGameMode; // Store the initial mode
        AlignCueSticksToTheirBalls();
    }

    /// <summary>
    /// Oyun moduna göre CueStick'lerin Owner durumunu ayarlar
    /// </summary>
    private void AssignCueStickOwners()
    {
        // Primary CueStick genellikle Player, ama AiOnly modunda Agent olabilir
        if (_primaryCueStick != null)
        {
            CueStick.CueOwner primaryOwner = CurrentGameMode == GameMode.AiOnly 
                ? CueStick.CueOwner.Agent 
                : CueStick.CueOwner.Player;
            
            SetCueStickOwner(_primaryCueStick, primaryOwner);
            Debug.Log($"[BilliardGameManager] Primary CueStick owner set to: {primaryOwner}");
        }
        
        // Secondary CueStick - TwoPlayer'da Player, diğerlerinde Agent
        if (_secondaryCueStick != null)
        {
            CueStick.CueOwner secondaryOwner = CurrentGameMode == GameMode.TwoPlayer 
                ? CueStick.CueOwner.Player 
                : CueStick.CueOwner.Agent;
            
            SetCueStickOwner(_secondaryCueStick, secondaryOwner);
            Debug.Log($"[BilliardGameManager] Secondary CueStick owner set to: {secondaryOwner}");
        }
    }

    /// <summary>
    /// Inspector'dan atanmamış istekalar için otomatik target ball atar
    /// BallMode'a göre atamaları günceller
    /// </summary>
    private void AssignCueStickTargetBalls()
    {
        // BallMode'a göre atamaları belirle
        if (CurrentBallMode == BallMode.SameBall)
        {
            // Her iki isteka da Main Ball'a vurur
            if (_primaryCueStick != null)
            {
                SetCueStickTarget(_primaryCueStick, _mainBall);
                Debug.Log("[BilliardGameManager] Primary CueStick assigned to Main Ball (SameBall mode)");
            }
            
            if (_secondaryCueStick != null)
            {
                SetCueStickTarget(_secondaryCueStick, _mainBall);
                Debug.Log("[BilliardGameManager] Secondary CueStick assigned to Main Ball (SameBall mode)");
            }
        }
        else if (CurrentBallMode == BallMode.DifferentBalls)
        {
            // Primary CueStick -> Main Ball
            if (_primaryCueStick != null)
            {
                SetCueStickTarget(_primaryCueStick, _mainBall);
                Debug.Log("[BilliardGameManager] Primary CueStick assigned to Main Ball (DifferentBalls mode)");
            }
            
            // Secondary CueStick -> Secondary Ball
            if (_secondaryCueStick != null)
            {
                SetCueStickTarget(_secondaryCueStick, _secondaryBall);
                Debug.Log("[BilliardGameManager] Secondary CueStick assigned to Secondary Ball (DifferentBalls mode)");
            }
        }
    }

    /// <summary>
    /// CueStick'in owner durumunu reflection ile atar (SerializeField olduğu için)
    /// </summary>
    private void SetCueStickOwner(CueStick cueStick, CueStick.CueOwner owner)
    {
        if (cueStick == null) return;

        var field = typeof(CueStick).GetField("_owner", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            field.SetValue(cueStick, owner);
        }
        else
        {
            Debug.LogError("[BilliardGameManager] Could not find _owner field in CueStick!");
        }
    }

    /// <summary>
    /// CueStick'e target ball'ı reflection ile atar (SerializeField olduğu için)
    /// </summary>
    private void SetCueStickTarget(CueStick cueStick, BilliardBall targetBall)
    {
        if (cueStick == null || targetBall == null) return;

        var field = typeof(CueStick).GetField("_targetBall", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            field.SetValue(cueStick, targetBall);
            Debug.Log($"[BilliardGameManager] {cueStick.Owner} CueStick -> {targetBall.Type} Ball");
        }
        else
        {
            Debug.LogError($"[BilliardGameManager] Could not find 'targetBall' field on {cueStick.name}.");
        }
    }

    private void CheckForGameSettingsChanges()
    {
        if (_gameSettings == null) return;

        // Check for BallMode change
        if (CurrentBallMode != _lastBallMode)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[BilliardGameManager] GameSettings changed. BallMode changed from '{_lastBallMode}' to '{CurrentBallMode}'. Re-assigning targets.");
            }
            _lastBallMode = CurrentBallMode;
            AssignCueStickTargetBalls();
            AlignCueSticksToTheirBalls();

            // Apply a full reset when BallMode changes so the new target
            // assignments and cue alignment take effect immediately.
            if (_showDebugInfo) Debug.Log("[BilliardGameManager] BallMode changed - resetting game to apply new ball assignments.");
            ResetGame();
        }

        // Check for GameMode change
        if (CurrentGameMode != _lastGameMode)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[BilliardGameManager] GameSettings changed. GameMode changed from '{_lastGameMode}' to '{CurrentGameMode}'. Resetting game.");
            }
            _lastGameMode = CurrentGameMode;
            AssignCueStickOwners();
            UpdateCueStickVisibility();
            
            // GameMode değiştiğinde oyunu resetle
            ResetGame();
            
            // GameFlowManager'a yeni moda göre state'i başlat
            // Not: GameFlowManager referansını Inspector'dan GameFlowManager'a atayın
            // veya bu satırı yorum dışı bırakıp _flowManager field'ı ekleyin
        }
    }

    private void Update()
    {
        CheckForGameSettingsChanges();
        CheckBallsMovement();
        
        // R tuşu artık sadece BilliardTestController'da kullanılıyor
        // Burada duplicate reset'i kaldırdık
        
        if (_showDebugInfo && Input.GetKeyDown(KeyCode.I))
        {
            PrintGameInfo();
        }

        // Oyun modu değişirse isteka görünürlüğünü güncelle (gelişmiş: Inspector'dan runtime değişim için)
        // UpdateCueStickVisibility(); // Performans ve turn-based görünürlük için Update'den kaldırıldı
    }

    /// <summary>
    /// Sets the active cue stick and hides the others.
    /// </summary>
    public void SetActiveCueStick(CueStick activeCue)
    {
        if (_primaryCueStick != null)
        {
            _primaryCueStick.gameObject.SetActive(_primaryCueStick == activeCue);
        }
        
        if (_secondaryCueStick != null)
        {
            _secondaryCueStick.gameObject.SetActive(_secondaryCueStick == activeCue);
        }
    }

    /// <summary>
    /// Referansları kontrol eder
    /// </summary>
    private void ValidateReferences()
    {
        if (_mainBall == null)
            Debug.LogError("BilliardGameManager: Main Ball reference is missing!");
        
        if (_targetBall == null)
            Debug.LogWarning("BilliardGameManager: Target Ball reference is missing!");
        
        if (_secondaryBall == null)
            Debug.LogWarning("BilliardGameManager: Secondary Ball reference is missing!");
        
        if ((CurrentGameMode == GameMode.SinglePlayer || CurrentGameMode == GameMode.PlayerVsAi) && _primaryCueStick == null)
            Debug.LogError("BilliardGameManager: Player 1 CueStick is missing for the current game mode!");
        
        if (CurrentGameMode == GameMode.TwoPlayer && (_primaryCueStick == null || _secondaryCueStick == null))
            Debug.LogError("BilliardGameManager: One or both Player CueSticks are missing for TwoPlayer mode!");

        if ((CurrentGameMode == GameMode.PlayerVsAi || CurrentGameMode == GameMode.AiOnly) && _secondaryCueStick == null)
            Debug.LogError("BilliardGameManager: Agent CueStick reference is missing for the current game mode!");
    }

    private float _physicsSettleTimer = 0f;

    /// <summary>
    /// Topların hareket edip etmediğini kontrol eder
    /// </summary>
    private void CheckBallsMovement()
    {
        // FIX: Physics Settle Timer - Reset sonrası fizik motorunun oturması için bekle
        if (_physicsSettleTimer > 0f)
        {
            _physicsSettleTimer -= Time.deltaTime;
            _ballsAreMoving = false;
            _waitTimer = 0f;
            return;
        }

        bool anyBallMoving = IsBallStillMoving(_mainBall) || IsBallStillMoving(_targetBall) || IsBallStillMoving(_secondaryBall);
        
        if (anyBallMoving)
        {
            _ballsAreMoving = true;
            _waitTimer = 0f;
        }
        else if (_ballsAreMoving)
        {
            _waitTimer += Time.deltaTime;
            
            // Toplar durdu
            float confirmationDelay = Mathf.Max(0.05f, _stopVelocityThreshold);
            if (_waitTimer >= confirmationDelay || _waitTimer >= _maxWaitTime)
            {
                // FIX: Eğer herhangi bir isteka vuruş yapıyorsa (henüz topa değmemiş olabilir),
                // topların durduğunu ilan etme. Bu, fizik jitter'ı veya vuruş öncesi bekleme
                // durumlarında "Boş Vuruş" cezasını engeller.
                // NOT: Eğer isteka zaten "topların durmasını bekliyorsa" (IsWaitingForBalls),
                // o zaman bu kontrolü pas geçmeliyiz ki deadlock olmasın.
                bool isShooting = (_primaryCueStick != null && _primaryCueStick.IsShooting && !_primaryCueStick.IsWaitingForBalls) || 
                                  (_secondaryCueStick != null && _secondaryCueStick.IsShooting && !_secondaryCueStick.IsWaitingForBalls);

                if (isShooting)
                {
                    // Vuruş devam ediyor, beklemeye devam et
                    return;
                }

                if (_showDebugInfo && _ballsAreMoving)
                {
                    Debug.Log($"[BilliardGameManager] Balls stopped detection trigger. WaitTimer: {_waitTimer:F2}, MaxWait: {_maxWaitTime}, IsResetting: {_resetInProgress}");
                }

                _ballsAreMoving = false;
                
                // FIX: Reset sırasında OnAllBallsStopped çağrılmamalı
                // Reset kendi physics sync'ini yaptıktan sonra tamamlanacak
                // Bu sayede reset sonrası "boş vuruş" cezası uygulanmaz
                if (!_resetInProgress)
                {
                    OnAllBallsStopped();
                }
                else if (_showDebugInfo)
                {
                    Debug.Log("[BilliardGameManager] Balls stopped during reset - skipping OnAllBallsStopped event.");
                }
            }
        }
    }

    /// <summary>
    /// Tüm toplar durduğunda çağrılır
    /// </summary>
    private void OnAllBallsStopped()
    {
        if (_showDebugInfo)
        {
            Debug.Log("[BilliardGameManager] All balls stopped!");
        }

        // İstekaları hizalama sorumluluğu BilliardAIEnvironment'a devredildi.
        // AlignCueSticksToTheirBalls();

        NotifyCueSticksBallsStopped();
        BallsStopped?.Invoke();
        
        // Burada ödül hesaplama, AI için bir sonraki hamle hazırlığı vs. yapılabilir
    }

    private void NotifyCueSticksBallsStopped()
    {
        _primaryCueStick?.NotifyAllBallsStopped();
        _secondaryCueStick?.NotifyAllBallsStopped();
        
        // Ayrıca OnBallsStopped metodunu da çağır (frozen modunu kaldırmak için)
        _primaryCueStick?.OnBallsStopped();
        _secondaryCueStick?.OnBallsStopped();
    }

    /// <summary>
    /// Her istekanın kendi target ball'ına hizalanmasını sağlar
    /// </summary>
    private void AlignCueSticksToTheirBalls()
    {
        if (_primaryCueStick != null)
        {
            _primaryCueStick.ForceAlignWithBall();
            if (_showDebugInfo)
            {
                Debug.Log($"[BilliardGameManager] Player cue aligned to {_primaryCueStick.TargetBall?.Type}");
            }
        }
        
        if (_secondaryCueStick != null)
        {
            _secondaryCueStick.ForceAlignWithBall();
            if (_showDebugInfo)
            {
                Debug.Log($"[BilliardGameManager] Agent cue aligned to {_secondaryCueStick.TargetBall?.Type}");
            }
        }
    }

    /// <summary>
    /// Oyunu sıfırlar
    /// </summary>
    public void ResetGame()
    {
        if (_resetInProgress) return;
        StartCoroutine(ResetRoutine());
    }

    /// <summary>
    /// Manuel test için - isteka ile vuruş yap
    /// </summary>
    /// <param name="cueStick">Kullanılacak isteka</param>
    /// <param name="angleX">Yatay açı (-180 to 180)</param>
    /// <param name="angleY">Dikey açı (-30 to 30)</param>
    /// <param name="power">Vuruş gücü (0 to maxPower)</param>
    public void MakeShot(CueStick cueStick, float angleX, float angleY, float power)
    {
        if (cueStick == null || _ballsAreMoving)
        {
            Debug.LogWarning("Cannot make shot: CueStick missing or balls are moving!");
            return;
        }
        
        cueStick.Shoot(angleX, angleY, power);
    }

    /// <summary>
    /// Oyun durumu bilgilerini yazdırır
    /// </summary>
    private void PrintGameInfo()
    {
        Debug.Log("=== BILLIARD GAME INFO ===");
        Debug.Log($"Balls Moving: {_ballsAreMoving}");
        
        if (_mainBall != null)
            Debug.Log($"Main Ball - Pos: {_mainBall.transform.position}, Vel: {_mainBall.Velocity.magnitude:F2}");
        
        if (_targetBall != null)
            Debug.Log($"Target Ball - Pos: {_targetBall.transform.position}, Vel: {_targetBall.Velocity.magnitude:F2}");
        
        if (_secondaryBall != null)
            Debug.Log($"Secondary Ball - Pos: {_secondaryBall.transform.position}, Vel: {_secondaryBall.Velocity.magnitude:F2}");
    }

    /// <summary>
    /// Oyun moduna göre kullanılmayan isteka GameObject'ini gizler/gösterir
    /// AI Only modunda agent aim'i otomatik açar, diğer modlarda kapatır
    /// </summary>
    private void UpdateCueStickVisibility()
    {
        // SinglePlayer: Sadece Player isteka aktif, Agent isteka gizli
        if (CurrentGameMode == GameMode.SinglePlayer)
        {
            if (_primaryCueStick != null) _primaryCueStick.gameObject.SetActive(true);
            if (_secondaryCueStick != null) _secondaryCueStick.gameObject.SetActive(false);
        }
        // AiOnly: Sadece Agent isteka aktif, Player isteka gizli
        else if (CurrentGameMode == GameMode.AiOnly)
        {
            if (_primaryCueStick != null) _primaryCueStick.gameObject.SetActive(false);
            if (_secondaryCueStick != null) 
            {
                _secondaryCueStick.gameObject.SetActive(true);
                _secondaryCueStick.SetShowAimInGame(true); // AI Only modunda agent aim otomatik açık
            }
        }
        // TwoPlayer veya PlayerVsAi: İstekalar aktif (gizleme yok)
        else
        {
            if (_primaryCueStick != null) _primaryCueStick.gameObject.SetActive(true);
            if (_secondaryCueStick != null) 
            {
                _secondaryCueStick.gameObject.SetActive(true);
                _secondaryCueStick.SetShowAimInGame(false); // Diğer modlarda agent aim otomatik kapalı
            }
        }
    }

    /// <summary>
    /// Topların hareketli olup olmadığını döner
    /// </summary>
    public bool AreBallsMoving()
    {
        return _ballsAreMoving;
    }

    private static bool IsBallStillMoving(BilliardBall ball)
    {
        return ball != null && !ball.HasStopped();
    }

    private static void StopBall(BilliardBall ball)
    {
        if (ball == null) return;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
    }

    private IEnumerator ResetRoutine()
    {
        _resetInProgress = true;

        StopBall(_mainBall);
        StopBall(_targetBall);
        StopBall(_secondaryBall);

        yield return new WaitForFixedUpdate(); // Fizik kararı tamamlanmadan pozisyonlama yapma

        if (_mainBall != null) _mainBall.ResetPosition(false);
        if (_targetBall != null) _targetBall.ResetPosition(false);
        if (_secondaryBall != null) _secondaryBall.ResetPosition(false);

        Physics.SyncTransforms();

        if (_primaryCueStick != null)
        {
            _primaryCueStick.ForceAlignWithBall();
        }
        
        if (_secondaryCueStick != null)
        {
            _secondaryCueStick.ForceAlignWithBall();
        }

        _ballsAreMoving = false;
        _waitTimer = 0f;
        _physicsSettleTimer = 1.0f; // FIX: Reset sonrası 1 saniye boyunca fizik hareketlerini yoksay

        Debug.Log("[BilliardGameManager] Game reset!");

        // Notify any listeners that a reset has completed
        OnGameReset?.Invoke();

        // Reset sonrası OnAllBallsStopped çağrılmıyor
        // Çünkü bu BallsStopped event'ini tetikler ve oyuncu sırasını değiştirebilir
        // Reset sadece pozisyonları sıfırlar, oyun akışı devam eder

        _resetInProgress = false;
    }

    private void OnDrawGizmos()
    {
        if (_showDebugInfo && _mainBall != null && _targetBall != null)
        {
            // Ana top ile hedef top arasında çizgi
            Gizmos.color = Color.green;
            Gizmos.DrawLine(_mainBall.transform.position, _targetBall.transform.position);
            
            if (_secondaryBall != null)
            {
                // Ana top ile ikinci top arasında çizgi
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_mainBall.transform.position, _secondaryBall.transform.position);
            }
        }
    }
}
