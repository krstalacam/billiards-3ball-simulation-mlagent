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
    private bool _shotInFlight;
    private float _decisionTimer;

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
        _gameManager = environment.GetComponent<BilliardGameManager>();

        if (_gameManager == null)
        {
            Debug.LogError("[BilliardEpisodeManager] BilliardGameManager component not found on the environment object!");
            return;
        }

        _shotInFlight = false;
        _decisionTimer = 0f;
    }

    /// <summary>
    /// Episode başlangıcında çağrılır. Environment'ı resetler.
    /// </summary>
    public void BeginEpisode()
    {
        _shotInFlight = false;
        _decisionTimer = 0f;
        
        if (_environment != null)
        {
            _environment.RequestEnvironmentReset();
            
            // GameFlowManager now handles the initial turn state based on GameMode.
            // So, we don't need to set it here anymore.
        }
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
    /// Environment referansını değiştirir (gerekirse).
    /// </summary>
    public bool IsEnvironmentValid()
    {
        return _environment != null && _gameManager != null;
    }

   
}
