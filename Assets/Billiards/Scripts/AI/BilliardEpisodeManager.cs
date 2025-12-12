using System;
using UnityEngine;

/// <summary>
/// Bilardo AI eğitim episode'larını yönetir.
/// Atış takibi, episode başlangıcı ve environment reset işlemlerini koordine eder.
/// Training mode ve play mode (turn-based) destekler.
/// </summary>
public class BilliardEpisodeManager
{
    private readonly BilliardAIEnvironment _environment;
    private readonly BilliardGameManager _gameManager;
    private GameSettings _gameSettings; // GameSettings referansı
    private bool _shotInFlight;
    private float _decisionTimer;
    private int _currentTurnCount = 0; // Tur sayacı
    private bool _shouldRandomizeBallsNextEpisode = false; // Bir sonraki episode'da topları rastgele yerleştir mi?

    // Events
    public event System.Action OnShotCompleted;

    public bool IsShotInFlight => _shotInFlight;
    public float DecisionTimer => _decisionTimer;

    public BilliardEpisodeManager(BilliardAIEnvironment environment)
    {
        if (environment == null)
        {
            Debug.LogError("[BilliardEpisodeManager] Environment cannot be null!");
            return;
        }
        
        _environment = environment;
        // BilliardAIEnvironment holds a serialized reference to the GameManager (may live on a different object)
        _gameManager = environment.GameManager;

        if (_gameManager == null)
        {
            Debug.LogError("[BilliardEpisodeManager] BilliardGameManager component not found on the environment object!");
            return;
        }

        _shotInFlight = false;
        _decisionTimer = 0f;
        _shouldRandomizeBallsNextEpisode = true; // İlk episode'da topları rastgele yerleştir
        
        // GameSettings'i bul
        if (_gameSettings == null)
        {
            var settingsAssets = Resources.FindObjectsOfTypeAll<GameSettings>();
            if (settingsAssets.Length > 0)
            {
                _gameSettings = settingsAssets[0];
                Debug.Log("[BilliardEpisodeManager] GameSettings found and assigned.");
            }
            else
            {
                Debug.LogWarning("[BilliardEpisodeManager] GameSettings not found! Ball randomization will use config only.");
            }
        }
    }

    /// <summary>
    /// Episode başlangıcında çağrılır. Environment'ı resetler.
    /// </summary>
    /// <param name="config">Agent konfigürasyonu</param>
    /// <param name="shouldRandomizeBalls">Topları rastgele yerleştir mi? (Varsayılan: false, sadece max turn'de true olmalı)</param>
    public void BeginEpisode(BilliardAgentConfig config = null, bool shouldRandomizeBalls = false)
    {
        _shotInFlight = false;
        _decisionTimer = 0f;
        
        // Flag'i kontrol et (eğer parametre verilmediyse)
        if (!shouldRandomizeBalls)
        {
            shouldRandomizeBalls = _shouldRandomizeBallsNextEpisode;
            _shouldRandomizeBallsNextEpisode = false; // Flag'i sıfırla
        }
        // NOT: _currentTurnCount burada sıfırlanmıyor! Sadece max turn'e ulaşıldığında sıfırlanacak.
        
        if (_environment != null)
        {
            // GameSettings'ten randomization ayarını kontrol et
            bool shouldRandomizeFromSettings = _gameSettings != null && _gameSettings.RandomizeBallPositions;
            
            // Topları rastgele konumlandır (SADECE shouldRandomizeBalls true ise VE GameSettings izin veriyorsa)
            if (shouldRandomizeBalls && shouldRandomizeFromSettings && config != null && config.randomizeBallPositions && _gameManager != null)
            {
                // SADECE topları rastgele yerleştireceğimiz zaman environment'ı resetle
                _environment.RequestEnvironmentReset();
                RandomizeBallPositions(config);
                Debug.Log($"[BilliardEpisodeManager] Environment reset and balls randomized (Turn: {_currentTurnCount}, GameSettings.RandomizeBallPositions={shouldRandomizeFromSettings}).");
            }
            else
            {
                // Topları rastgele yerleştirmiyorsak, sadece hızlarını sıfırla (pozisyonları koru)
                StopAllBallsWithoutReset();
                
                string reason = !shouldRandomizeBalls ? "shouldRandomizeBalls=false" : 
                               !shouldRandomizeFromSettings ? "GameSettings.RandomizeBallPositions=false" :
                               config == null ? "config=null" :
                               !config.randomizeBallPositions ? "config.randomizeBallPositions=false" :
                               "_gameManager=null";
                Debug.Log($"[BilliardEpisodeManager] Balls NOT randomized - keeping current positions (Turn: {_currentTurnCount}, Reason: {reason}).");
            }
            
            // GameFlowManager now handles the initial turn state based on GameMode.
            // So, we don't need to set it here anymore.
        }
    }

    /// <summary>
    /// Topların hızını sıfırlar ama pozisyonlarını değiştirmez
    /// </summary>
    private void StopAllBallsWithoutReset()
    {
        if (_gameManager == null) return;

        var mainBall = _gameManager.MainBall;
        var targetBall = _gameManager.TargetBall;
        var secondaryBall = _gameManager.SecondaryBall;

        if (mainBall != null)
        {
            var rb = mainBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (targetBall != null)
        {
            var rb = targetBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (secondaryBall != null)
        {
            var rb = secondaryBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        Debug.Log("[BilliardEpisodeManager] All balls stopped (velocities zeroed, positions preserved).");
    }

    /// <summary>
    /// Topları rastgele konumlara yerleştirir (çakışmayı önleyerek)
    /// </summary>
    private void RandomizeBallPositions(BilliardAgentConfig config)
    {
        if (_gameManager == null) return;

        var mainBall = _gameManager.MainBall;
        var targetBall = _gameManager.TargetBall;
        var secondaryBall = _gameManager.SecondaryBall;

        if (mainBall == null || targetBall == null || secondaryBall == null)
        {
            Debug.LogWarning("[BilliardEpisodeManager] Cannot randomize - one or more balls are null!");
            return;
        }

        // Y pozisyonu sabit (masa yüzeyi)
        float yPosition = mainBall.transform.position.y;
        
        // Topları rastgele yerleştir (çakışmayı önle)
        System.Collections.Generic.List<Vector3> positions = new System.Collections.Generic.List<Vector3>();
        
        // Main Ball
        Vector3 mainPos = GetRandomPosition(config, yPosition, positions);
        positions.Add(mainPos);
        mainBall.SetInitialPosition(mainPos);
        
        // Target Ball
        Vector3 targetPos = GetRandomPosition(config, yPosition, positions);
        positions.Add(targetPos);
        targetBall.SetInitialPosition(targetPos);
        
        // Secondary Ball
        Vector3 secondaryPos = GetRandomPosition(config, yPosition, positions);
        positions.Add(secondaryPos);
        secondaryBall.SetInitialPosition(secondaryPos);

        Debug.Log($"[BilliardEpisodeManager] Balls randomized - Main: {mainPos}, Target: {targetPos}, Secondary: {secondaryPos}");
    }

    /// <summary>
    /// Diğer toplarla çakışmayan rastgele bir pozisyon üretir (tableCenterOffset kullanarak)
    /// </summary>
    private Vector3 GetRandomPosition(BilliardAgentConfig config, float yPosition, System.Collections.Generic.List<Vector3> existingPositions)
    {
        // Environment transform'unu al (köşe kontrolü ile aynı sistem)
        Transform tableTransform = _environment?.transform;
        if (tableTransform == null)
        {
            Debug.LogWarning("[BilliardEpisodeManager] Environment transform not found! Using world space origin.");
            tableTransform = _gameManager?.transform; // Fallback
        }

        int maxAttempts = 50;
        for (int i = 0; i < maxAttempts; i++)
        {
            // Local space'de rastgele pozisyon üret (tableCenterOffset ile)
            float localX = UnityEngine.Random.Range(config.randomizationAreaX.x, config.randomizationAreaX.y);
            float localZ = UnityEngine.Random.Range(config.randomizationAreaZ.x, config.randomizationAreaZ.y);
            Vector3 localPos = new Vector3(localX, 0, localZ) + config.tableCenterOffset;
            
            // Local'den world space'e çevir
            Vector3 worldPos = tableTransform != null 
                ? tableTransform.TransformPoint(localPos) 
                : localPos;
            
            // Y pozisyonunu ayarla
            worldPos.y = yPosition;
            
            // Diğer toplarla çakışma kontrolü
            bool tooClose = false;
            foreach (var existingPos in existingPositions)
            {
                float distance = Vector3.Distance(new Vector3(worldPos.x, 0, worldPos.z), 
                                                  new Vector3(existingPos.x, 0, existingPos.z));
                if (distance < config.minDistanceBetweenBalls)
                {
                    tooClose = true;
                    break;
                }
            }
            
            if (!tooClose)
            {
                return worldPos;
            }
        }
        
        // Eğer uygun pozisyon bulunamazsa, rastgele bir pozisyon döndür (son çare)
        Debug.LogWarning("[BilliardEpisodeManager] Could not find non-overlapping position after max attempts!");
        float fallbackLocalX = UnityEngine.Random.Range(config.randomizationAreaX.x, config.randomizationAreaX.y);
        float fallbackLocalZ = UnityEngine.Random.Range(config.randomizationAreaZ.x, config.randomizationAreaZ.y);
        Vector3 fallbackLocal = new Vector3(fallbackLocalX, 0, fallbackLocalZ) + config.tableCenterOffset;
        Vector3 fallbackWorld = tableTransform != null ? tableTransform.TransformPoint(fallbackLocal) : fallbackLocal;
        fallbackWorld.y = yPosition;
        return fallbackWorld;
    }


    /// <summary>
    /// Environment'a event handler bağlar.
    /// </summary>
    public void RegisterEnvironmentEvents()
    {
        if (_environment != null)
        {
            _environment.BallsStopped -= HandleBallsStopped;
            _environment.BallsStopped += HandleBallsStopped;
            
            // The GameFlowManager now handles turn switching.
            // This manager's responsibility is reduced to just managing the episode lifecycle.
        }
    }

    /// <summary>
    /// Environment'tan event handler'ı kaldırır.
    /// </summary>
    public void UnregisterEnvironmentEvents()
    {
        if (_environment != null)
        {
            _environment.BallsStopped -= HandleBallsStopped;
        }
    }

    /// <summary>
    /// Her frame karar zamanlayıcısını günceller.
    /// </summary>
    public void UpdateDecisionTimer(float deltaTime, float decisionInterval)
    {
        // Sadece toplar hareket ediyorsa timer'ı sıfırla
        if (_environment != null && _environment.IsShotInProgress)
        {
            _decisionTimer = 0f;
            return;
        }

        _decisionTimer += deltaTime;
    }

    /// <summary>
    /// Karar zamanı geldi mi kontrol eder.
    /// </summary>
    public bool ShouldRequestDecision(float decisionInterval)
    {
        return _decisionTimer >= Mathf.Max(decisionInterval, 0.01f);
    }

    /// <summary>
    /// Karar zamanlayıcısını resetler.
    /// </summary>
    public void ResetDecisionTimer()
    {
        _decisionTimer = 0f;
    }

    /// <summary>
    /// Atış yapmayı dener. Başarılıysa shotInFlight bayrağını set eder.
    /// </summary>
    public bool TryExecuteShot(ShotParameters shotParams)
    {
        if (_environment == null)
        {
            return false;
        }

        // Toplar hala hareket ediyorsa atış yapma
        if (_environment.IsShotInProgress)
        {
            Debug.Log("[BilliardEpisodeManager] Shot in progress, waiting...");
            return false;
        }

        var shotResult = _environment.TryQueueShot(
            shotParams.AngleX,
            shotParams.AngleY,
            shotParams.Power
        );
        bool shotStarted = shotResult == BilliardAIEnvironment.ShotResult.Success;

        if (shotStarted)
        {
            _shotInFlight = true;
            Debug.Log($"[BilliardEpisodeManager] Shot executed: angleX={shotParams.AngleX:F1}, angleY={shotParams.AngleY:F1}, power={shotParams.Power:F1}");
        }
        else
        {
            Debug.LogWarning("[BilliardEpisodeManager] Failed to execute shot");
        }

        return shotStarted;
    }

    /// <summary>
    /// Toplar durduğunda çağrılır.
    /// </summary>
    private void HandleBallsStopped()
    {
        _shotInFlight = false;
        OnShotCompleted?.Invoke();
        Debug.Log("[BilliardEpisodeManager] Balls stopped, ready for next action");
    }

    // OnBallsStoppedSwitchTurn is removed as GameFlowManager now handles this.

    /// <summary>
    /// Checks if the episode should be forced to end due to turn limit.
    /// Should be called after a turn is completed.
    /// </summary>
    public void RegisterTurnCompletion(BilliardAgentConfig config, BilliardAgent agent)
    {
        _currentTurnCount++;
        
        if (config != null && agent != null && _currentTurnCount >= config.maxTurnsPerEpisode)
        {
             Debug.Log($"[BilliardEpisodeManager] Max turns reached ({_currentTurnCount}/{config.maxTurnsPerEpisode}). Resetting turn counter and randomizing balls.");
             
             // Turn sayacını sıfırla
             _currentTurnCount = 0;
             
             // Bir sonraki episode'da topları rastgele yerleştir
             _shouldRandomizeBallsNextEpisode = true;
             
             // Episode'u bitir ve yeni episode başlat (topları rastgele yerleştirerek)
             agent.EndEpisode();
             // NOT: EndEpisode çağrısı OnEpisodeBegin'i tetikleyecek, orada BeginEpisode(config, shouldRandomizeBalls: true) çağrılmalı
        }
        else
        {
            Debug.Log($"[BilliardEpisodeManager] Turn {_currentTurnCount}/{config.maxTurnsPerEpisode} completed. Continuing with current ball positions.");
        }
    }

    /// <summary>
    /// Environment referansını değiştirir (gerekirse).
    /// </summary>
    public bool IsEnvironmentValid()
    {
        return _environment != null && _gameManager != null;
    }

   
}