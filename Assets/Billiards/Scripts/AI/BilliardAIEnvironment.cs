using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Bridge between the traditional billiard gameplay scripts and ML-Agents.
/// Centralises references, exposes game state to agents, and coordinates shots/resets.
/// </summary>
public class BilliardAIEnvironment : MonoBehaviour
{
    // Son hizalanan topları takip için alanlar
    private BilliardBall _lastPlayerCueTarget = null;
    private BilliardBall _lastAgentCueTarget = null;
    // Agent turn event
    public event System.Action OnAgentTurn;
    public enum TurnState
    {
        Player,     // Oyuncunun sırası
        Agent,      // Ajanın sırası
        None        // Eğitim modu (sıra yok, sürekli ajan)
    }
    [Header("Core References")]
    [SerializeField] private BilliardGameManager _gameManager;
    [SerializeField] private BilliardScoreManager _scoreManager;
    public BilliardScoreManager ScoreManager => _scoreManager;

    [SerializeField] private CueStick _playerCueStick;
    [SerializeField] private CueStick _agentCueStick;
    [SerializeField] private BilliardBall _mainBall;
    [SerializeField] private BilliardBall _targetBall;
    [SerializeField] private BilliardBall _secondaryBall;

    [Header("Agent Hook (Optional)")]
    [SerializeField] private BilliardAgent _agent;

    [Header("Turn Management")]
    private TurnState _currentTurn = TurnState.None;

    private bool _resetRequested;

    [Header("Safety Watchdogs")]
    [Tooltip("Toplar hareketsiz kaldığında masayı kaç saniye sonra sıfırlayalım? (Config'den otomatik ayarlanır)")]
    [SerializeField] private float _maxIdleSeconds = 10f;
    [Tooltip("İsteka animasyonu takılı kalırsa masayı kaç saniye sonra sıfırlayalım? (Config'den otomatik ayarlanır)")]
    [SerializeField] private float _maxCueStuckSeconds = 5f;
    
    [Header("Configuration")]
    [SerializeField] private BilliardAgentConfig _config;
    [SerializeField] private GameSettings _gameSettings;

    private float _idleTimer = 0f;
    private float _cueStuckTimer = 0f;

    /// <summary>
    /// Raised after all balls have stopped moving.
    /// </summary>
    public event Action BallsStopped;

    /// <summary>
    /// Raised when turn changes (only in play mode, not training)
    /// </summary>
    public event Action<TurnState> TurnChanged;

    public BilliardBall MainBall => _mainBall;
    public BilliardBall TargetBall => _targetBall;
    public BilliardBall SecondaryBall => _secondaryBall;
    // Expose the serialized game manager so other helper classes can access it
    public BilliardGameManager GameManager => _gameManager;
    public bool IsShotInProgress => _gameManager != null && _gameManager.AreBallsMoving();
    public TurnState CurrentTurn => _currentTurn;

    private void Awake()
    {
        AutoResolveReferences();
        ConfigureWatchdogTimeouts();
    }

    /// <summary>
    /// Training/Play mode'a göre watchdog sürelerini ayarlar.
    /// </summary>
    private void ConfigureWatchdogTimeouts()
    {
        if (_config == null || _gameSettings == null)
        {
            Debug.LogWarning("[BilliardAIEnvironment] Config or GameSettings not assigned, using default watchdog timeouts.");
            return;
        }

        if (_gameSettings.IsTrainingMode)
        {
            // Training mode: Uzun süre ver ki ajan ne yapacağını öğrenebilsin
            _maxIdleSeconds = _config.trainingModeWatchdogTimeout;
            _maxCueStuckSeconds = _config.trainingModeWatchdogTimeout * 0.5f;
        }
        else
        {
            // Play mode: Hızlı reset için kısa süre
            _maxIdleSeconds = _config.playModeWatchdogTimeout;
            _maxCueStuckSeconds = _config.playModeWatchdogTimeout * 0.5f;
        }
    }

    private void OnEnable()
    {
        if (_gameManager != null)
        {
            _gameManager.BallsStopped += HandleBallsStopped;
        }

        if (_agent != null)
        {
            _agent.BindToEnvironment(this);
        }
    }

    private void OnDisable()
    {
        if (_gameManager != null)
        {
            _gameManager.BallsStopped -= HandleBallsStopped;
        }
    }

    private void Update()
    {
        // Watchdog artık sadece agent deciding durumunda çalışır
        // GameFlowManager kontrolü yapıyor
    }

    /// <summary>
    /// Allows an agent to register at runtime (useful when spawned dynamically in a prefab).
    /// </summary>
    public void RegisterAgent(BilliardAgent agent)
    {
        _agent = agent;
        _agent?.BindToEnvironment(this);
    }

    /// <summary>
    /// Attempts to reset the underlying billiard game.
    /// </summary>
    public void RequestEnvironmentReset()
    {
        if (_gameManager == null)
        {
            Debug.LogWarning("[BilliardAIEnvironment] GameManager is missing – cannot reset.");
            return;
        }

        if (_resetRequested)
        {
            return;
        }

        _resetRequested = true;
        StartCoroutine(ResetRoutine());
    }

    public enum ShotResult
    {
        Success,
        Busy,
        Blocked,
        Resetting,
        Failed
    }

    /// <summary>
    /// Queues a shot if the cue is idle and all balls have stopped.
    /// Sıraya göre doğru istekanın kullanıldığından emin olur.
    /// ÖNEMLİ: Verilen açıyı HEMEN kullanır, önceki açıları değil!
    /// </summary>
    public ShotResult TryQueueShot(float angleX, float angleY, float power)
    {
        if (_gameManager == null)
        {
            Debug.LogWarning("[BilliardAIEnvironment] Missing game manager reference.");
            return ShotResult.Failed;
        }

        if (_gameManager.AreBallsMoving())
        {
            return ShotResult.Busy;
        }

        if (_resetRequested || _gameManager.IsResetting)
        {
            Debug.LogWarning("[BilliardAIEnvironment] Cannot queue shot - reset requested or in progress.");
            return ShotResult.Resetting;
        }

        // Sıraya göre doğru istekanın kullanıldığından emin ol
        CueStick cueToUse = GetCueStickForCurrentTurn();
        if (cueToUse == null)
        {
            Debug.LogWarning("[BilliardAIEnvironment] No cue stick available for current turn.");
            return ShotResult.Failed;
        }

        // ÖNEMLİ: İstekanın pozisyonunu ve açısını ANINDA güncelle
        // Önce bu açının fiziksel olarak geçerli olup olmadığını kontrol et.
        if (cueToUse.IsBlockedForAngles(angleX, angleY))
        {
            Debug.LogWarning($"[BilliardAIEnvironment] Blocked shot detected. AngleX={angleX:F2}, AngleY={angleY:F2}");
            return ShotResult.Blocked; // Atışı gerçekleştirme - ceza RewardManager'da
        }

        // İstekayı pozisyona getir ve atışı uygula
        //cueToUse.ForceAlignWithBall(angleX, angleY, power);

        Debug.Log($"[BilliardAIEnvironment] Shot with NEW angles: AngleX={angleX:F2}, AngleY={angleY:F2}, Power={power:F2}");

        // Training mode'da veya Agent atışında ScoreManager'ı başlat
        // GameFlowManager da çağırıyor olabilir ama burada garantiye alıyoruz.
        // StartTurn çağrısı istatistikleri sıfırlar, bu yüzden her atışta temiz bir sayfa açılır.
        if (cueToUse.TargetBall != null && _scoreManager != null)
        {
            _scoreManager.StartTurn(cueToUse.TargetBall, cueToUse);
        }

        // Artık Shoot() metodu güncel pozisyonu kullanacak
        bool shotStarted = cueToUse.Shoot(angleX, angleY, power);
        return shotStarted ? ShotResult.Success : ShotResult.Busy;
    }

    /// <summary>
    /// Mevcut sıraya göre hangi istekanın kullanılacağını döner.
    /// </summary>
    private CueStick GetCueStickForCurrentTurn()
    {
        // Training mode'da veya Agent sırasında -> Agent isteka kullan
        if (_currentTurn == TurnState.None || _currentTurn == TurnState.Agent)
        {
            return _agentCueStick;
        }
        // Player sırasında -> Player isteka kullan
        else if (_currentTurn == TurnState.Player)
        {
            return _playerCueStick;
        }
        
        return null;
    }

    public Vector3 GetBallPosition(BilliardBall ball)
    {
        return ball != null ? ball.transform.position : Vector3.zero;
    }

    public Vector3 GetBallVelocity(BilliardBall ball)
    {
        return ball != null ? ball.Velocity : Vector3.zero;
    }

    /// <summary>
    /// Agent'ın kontrol ettiği topu döner (hangi topa vurduğu)
    /// </summary>
    public BilliardBall GetAgentControlledBall()
    {
        if (_agentCueStick != null && _agentCueStick.TargetBall != null)
        {
            return _agentCueStick.TargetBall;
        }
        // Fallback: Main ball
        return _mainBall;
    }

    private IEnumerator ResetRoutine()
    {
        _gameManager.ResetGame();
        // Allow one frame so physics settle before clearing the flag
        yield return null;
        _resetRequested = false;

        // Clear watchdog timers and active flags so watchdog doesn't immediately trigger after reset
        ResetWatchdogTimers();

        // Immediately set turn to Agent and request a decision so the agent will act after reset
        SetTurnState(TurnState.Agent);
        if (_agent != null)
        {
            try
            {
                _agent.RequestDecision();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BilliardAIEnvironment] Failed to request decision from agent after reset: " + ex.Message);
            }
        }
    }

    private void HandleBallsStopped()
    {
        BallsStopped?.Invoke();
    }

    /// <summary>
    /// Sıra durumunu ayarlar (training mode için None, play mode için Player/Agent)
    /// </summary>
    public void SetTurnState(TurnState newState)
    {
        if (_currentTurn != newState)
        {
            _currentTurn = newState;
            TurnChanged?.Invoke(_currentTurn);
            // Sıra değiştiğinde o sıranın istekasını hizala
            AlignCueStickForCurrentTurn();
            // Controller state is now managed by GameFlowManager
            // UpdateTestControllerState();
            // Eğer sıra ajana geçtiyse event tetikle
            if (_currentTurn == TurnState.Agent)
            {
                OnAgentTurn?.Invoke();
            }
        }
    }

    /// <summary>
    /// Mevcut sıranın istekasını zorla hizalar.
    /// </summary>
    public void ForceAlignCurrentCue()
    {
        AlignCueStickForCurrentTurn();
    }

    /// <summary>
    /// Mevcut sıraya göre istekanın kendi topuna hizalanmasını sağlar
    /// </summary>
    private void AlignCueStickForCurrentTurn()
    {
        if (_currentTurn == TurnState.Player && _playerCueStick != null)
        {
            // Player için varsayılan hizalama (önceki açı korunabilir veya resetlenebilir)
            _playerCueStick.ForceAlignWithBall(null, 60f, null);
            if (_playerCueStick.TargetBall != null && (_lastPlayerCueTarget != _playerCueStick.TargetBall))
            {
                _lastPlayerCueTarget = _playerCueStick.TargetBall;
            }
        }
        else if (_currentTurn == TurnState.Agent && _agentCueStick != null)
        {
            // Agent için GÜVENLİ hizalama: Y açısını 60 dereceye sabitle
            // X açısı (yatay) önceki değerde kalabilir, ama Y=60 (yukarıdan bakış) duvar içine girmeyi engeller.
            _agentCueStick.ForceAlignWithBall(null, 60f, null);
            
            if (_agentCueStick.TargetBall != null && (_lastAgentCueTarget != _agentCueStick.TargetBall))
            {
                _lastAgentCueTarget = _agentCueStick.TargetBall;
            }
        }
    }

    /// <summary>
    /// Sırayı değiştirir (Player -> Agent veya Agent -> Player)
    /// Training mode'da çağrılmamalı
    /// </summary>
    public void SwitchTurn()
    {
        if (_currentTurn == TurnState.None)
        {
            Debug.LogWarning("[BilliardAIEnvironment] Cannot switch turn in training mode (TurnState.None)");
            return;
        }

        TurnState newTurn = _currentTurn == TurnState.Player ? TurnState.Agent : TurnState.Player;
        SetTurnState(newTurn);
    }

    /// <summary>
    /// Ajan sırası mı kontrol eder
    /// </summary>
    public bool IsAgentTurn()
    {
        return _currentTurn == TurnState.Agent || _currentTurn == TurnState.None;
    }

    private void AutoResolveReferences()
    {
        // GetComponent kullanımı kaldırıldı. Referanslar Inspector'dan atanmalı.
        if (_gameManager == null) Debug.LogWarning($"[BilliardAIEnvironment] BilliardGameManager atanmadı! ({gameObject.name})", this);
        if (_scoreManager == null) Debug.LogWarning($"[BilliardAIEnvironment] BilliardScoreManager atanmadı! ({gameObject.name})", this);
        if (_playerCueStick == null) Debug.LogWarning($"[BilliardAIEnvironment] Player CueStick atanmadı! ({gameObject.name})", this);
        if (_agentCueStick == null) Debug.LogWarning($"[BilliardAIEnvironment] Agent CueStick atanmadı! ({gameObject.name})", this);
        if (_mainBall == null) Debug.LogWarning($"[BilliardAIEnvironment] MainBall atanmadı! ({gameObject.name})", this);
        if (_targetBall == null) Debug.LogWarning($"[BilliardAIEnvironment] TargetBall atanmadı! ({gameObject.name})", this);
        if (_secondaryBall == null) Debug.LogWarning($"[BilliardAIEnvironment] SecondaryBall atanmadı! ({gameObject.name})", this);
        
        // Config ve GameSettings'i otomatik bul
        if (_config == null)
        {
            if (_agent != null) _config = _agent.Config;
            if (_config == null) _config = Resources.FindObjectsOfTypeAll<BilliardAgentConfig>()[0];
            if (_config != null) Debug.Log($"[BilliardAIEnvironment] Config auto-resolved: {_config.name}");
            else Debug.LogError("[BilliardAIEnvironment] ⚠️ Config NOT FOUND! Watchdog timeouts will use defaults.", this);
        }
        
        if (_gameSettings == null)
        {
            _gameSettings = Resources.FindObjectsOfTypeAll<GameSettings>()[0];
            if (_gameSettings != null) Debug.Log($"[BilliardAIEnvironment] GameSettings auto-resolved: Training={_gameSettings.IsTrainingMode}");
            else Debug.LogError("[BilliardAIEnvironment] ⚠️ GameSettings NOT FOUND! Watchdog timeouts will use defaults.", this);
        }
    }

    /// <summary>
    /// İsteka veya toplar beklenenden uzun süre hareketsiz kaldığında masayı kurtarmak için watchdog.
    /// SADECE Agent Deciding durumunda aktif olur (GameFlowManager kontrolü yapar).
    /// </summary>
    // Watchdog events: expose progress and triggers so UI can display countdowns
    public event Action OnWatchdogIdleStarted;
    public event Action<float, float> OnWatchdogIdleUpdated; // current, max
    public event Action OnWatchdogIdleCancelled;

    public event Action OnWatchdogCueStuckStarted;
    public event Action<float, float> OnWatchdogCueStuckUpdated; // current, max
    public event Action OnWatchdogCueStuckCancelled;

    public event Action<string> OnWatchdogTriggered; // reason

    private bool _idleWatchdogActive = false;
    private bool _cueWatchdogActive = false;

    /// <summary>
    /// Watchdog'u dışarıdan (GameFlowManager) kontrol etmek için public metod.
    /// Sadece AgentDeciding durumunda çağrılmalı.
    /// </summary>
    public void MonitorEnvironmentWatchdogs()
    {
        if (_gameManager == null)
        {
            return;
        }
        
        // Her check'te watchdog sürelerini güncelle (runtime'da training mode değişebilir)
        ConfigureWatchdogTimeouts();

        if (_resetRequested || _gameManager.IsResetting)
        {
            _idleTimer = 0f;
            _cueStuckTimer = 0f;
            if (_idleWatchdogActive)
            {
                _idleWatchdogActive = false;
                OnWatchdogIdleCancelled?.Invoke();
            }
            if (_cueWatchdogActive)
            {
                _cueWatchdogActive = false;
                OnWatchdogCueStuckCancelled?.Invoke();
            }
            return;
        }

        // Bu metod artık sadece GameFlowManager tarafından AgentDeciding durumunda çağrılır
        // Player turn veya balls moving durumlarında çağrılmamalı

        bool ballsMoving = _gameManager.AreBallsMoving();

        if (!ballsMoving)
        {
            _idleTimer += Time.deltaTime;
            if (!_idleWatchdogActive && _idleTimer > 0f)
            {
                _idleWatchdogActive = true;
                OnWatchdogIdleStarted?.Invoke();
            }
            OnWatchdogIdleUpdated?.Invoke(_idleTimer, _maxIdleSeconds);

            if (_idleTimer >= _maxIdleSeconds)
            {
                OnWatchdogTriggered?.Invoke("Balls stayed idle too long.");
                ForceEnvironmentRecovery("Balls stayed idle too long.");
                _idleTimer = 0f;
                _cueStuckTimer = 0f;
                _idleWatchdogActive = false;
                _cueWatchdogActive = false;
                return;
            }
        }
        else
        {
            if (_idleWatchdogActive)
            {
                _idleWatchdogActive = false;
                OnWatchdogIdleCancelled?.Invoke();
            }
            _idleTimer = 0f;
        }

        bool cueShooting = _agentCueStick != null && _agentCueStick.IsShooting;
        if (cueShooting && !ballsMoving)
        {
            _cueStuckTimer += Time.deltaTime;
            if (!_cueWatchdogActive && _cueStuckTimer > 0f)
            {
                _cueWatchdogActive = true;
                OnWatchdogCueStuckStarted?.Invoke();
            }
            OnWatchdogCueStuckUpdated?.Invoke(_cueStuckTimer, _maxCueStuckSeconds);

            if (_cueStuckTimer >= _maxCueStuckSeconds)
            {
                OnWatchdogTriggered?.Invoke("Cue stick animation stuck.");
                ForceEnvironmentRecovery("Cue stick animation stuck.");
                _cueStuckTimer = 0f;
                _cueWatchdogActive = false;
            }
        }
        else
        {
            if (_cueWatchdogActive)
            {
                _cueWatchdogActive = false;
                OnWatchdogCueStuckCancelled?.Invoke();
            }
            _cueStuckTimer = 0f;
        }
    }

    private void ForceEnvironmentRecovery(string reason)
    {
        if (_resetRequested)
        {
            return;
        }

        Debug.LogWarning($"[BilliardAIEnvironment] {reason} Forcing hard reset.");
        _scoreManager?.FinalizeTurnTracking();
        // Notify listeners that watchdog forced a reset
        OnWatchdogTriggered?.Invoke(reason);

        // ensure any active watchdog state is cleared and listeners know
        ResetWatchdogTimers();

        // Ajanın mevcut bölümünü sonlandırarak yeni bir bölüme başlamasını sağla
        _agent?.EndEpisode();

        RequestEnvironmentReset();
    }

    /// <summary>
    /// Watchdog timer'larını ve bayraklarını sıfırlar.
    /// GameFlowManager state değişikliklerinde çağrılmalı.
    /// </summary>
    public void ResetWatchdogTimers()
    {
        _idleTimer = 0f;
        _cueStuckTimer = 0f;
        if (_idleWatchdogActive)
        {
            _idleWatchdogActive = false;
            OnWatchdogIdleCancelled?.Invoke();
        }
        if (_cueWatchdogActive)
        {
            _cueWatchdogActive = false;
            OnWatchdogCueStuckCancelled?.Invoke();
        }
    }

    private void OnDrawGizmos()
    {
        // Rastgele top pozisyonları alanını görselleştir
        if (_config != null && _config.randomizeBallPositions)
        {
            // Dikdörtgen alanın köşelerini hesapla (local space + offset)
            // Köşe kontrolü ile aynı koordinat sistemini kullan
            Vector3 localMin = new Vector3(_config.randomizationAreaX.x, 0, _config.randomizationAreaZ.x) + _config.tableCenterOffset;
            Vector3 localMax = new Vector3(_config.randomizationAreaX.y, 0, _config.randomizationAreaZ.y) + _config.tableCenterOffset;
            
            // Dikdörtgenin 4 köşesi (local space)
            Vector3 corner1Local = new Vector3(localMin.x, 0, localMin.z);
            Vector3 corner2Local = new Vector3(localMax.x, 0, localMin.z);
            Vector3 corner3Local = new Vector3(localMax.x, 0, localMax.z);
            Vector3 corner4Local = new Vector3(localMin.x, 0, localMax.z);
            
            // Local'den world space'e çevir (Environment transform kullanarak)
            Transform tableTransform = this.transform;
            Vector3 corner1 = tableTransform.TransformPoint(corner1Local);
            Vector3 corner2 = tableTransform.TransformPoint(corner2Local);
            Vector3 corner3 = tableTransform.TransformPoint(corner3Local);
            Vector3 corner4 = tableTransform.TransformPoint(corner4Local);
            
            // Y pozisyonunu masa yüzeyine ayarla
            float yPos = _mainBall != null ? _mainBall.transform.position.y : 0.5f;
            corner1.y = yPos;
            corner2.y = yPos;
            corner3.y = yPos;
            corner4.y = yPos;
            
            // Yeşil renkte dikdörtgen çiz
            Gizmos.color = new Color(0f, 1f, 0f, 0.7f);
            Gizmos.DrawLine(corner1, corner2);
            Gizmos.DrawLine(corner2, corner3);
            Gizmos.DrawLine(corner3, corner4);
            Gizmos.DrawLine(corner4, corner1);
            
            // Çapraz çizgiler (merkezi göster)
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawLine(corner1, corner3);
            Gizmos.DrawLine(corner2, corner4);
            
            // Merkez noktayı göster
            Vector3 centerLocal = (_config.tableCenterOffset);
            Vector3 centerWorld = tableTransform.TransformPoint(centerLocal);
            centerWorld.y = yPos;
            Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
            Gizmos.DrawSphere(centerWorld, 0.05f);
        }
    }

}
