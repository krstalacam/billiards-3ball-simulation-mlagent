using UnityEngine;
using Unity.MLAgents;

/// <summary>
/// Manages the step-by-step flow of the billiard game for debugging and controlled execution.
/// Allows manual progression through game states via an inspector checkbox.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public enum GameState
    {
        Idle,
        PlayerTurn,
        WaitingForPlayerShot,
        AgentTurn,
        AgentDeciding,
        AgentShooting,
        BallsMoving
    }

    [Header("State Control")]
    [SerializeField] private GameState _currentState = GameState.Idle;
    [Tooltip("Check this to advance to the next state.")]
    [SerializeField] private bool _proceedToNextStep = false;

    [Header("Core References")]
    [SerializeField] private GameSettings _gameSettings;
    [SerializeField] private BilliardAIEnvironment _environment;
    [SerializeField] private BilliardAgent _agent;
    [SerializeField] private BilliardTestController _testController;
    [SerializeField] private BilliardGameManager _gameManager;
    [SerializeField] private BilliardScoreManager _scoreManager;
    [SerializeField] private BilliardUIManager _uiManager;
    [SerializeField] private BilliardGameMenuUI _gameMenuUI;

    [Header("Test Settings")]
    [SerializeField] private bool _useTestShot = false;
    [SerializeField] private float _testAngleX = 45f;
    [SerializeField] private float _testAngleY = 0f;
    [SerializeField] private float _testPower = 5f;

    [Header("Automation")]
    [Tooltip("Enable this to automatically proceed to the next state after each state completes.")]
    [SerializeField] private bool _automaticProgression = false;
    [SerializeField] private bool _showDebugInfo = true;

    private bool _isStateProcessing = false;
    private GameState _lastStateBeforeMoving; // Track the state before balls start moving
    private int _currentPlayerIndex = 0; // 0 for Player 1, 1 for Player 2
    private float _ballsMovingEnteredTime = -1f;
    private const float _forcedStopGraceSeconds = 0.3f;
    private bool _lastTrainingMode; // For tracking runtime changes
    private bool _isFoulReset = false; // Faul resetinden sonra BallsStopped event'ini engeller
    private bool _pendingFoulReset = false; // Defer the actual reset until listeners (RewardManager) process the foul
    private GameState _pendingNextState = GameState.Idle;

    public GameState CurrentState => _currentState;

    private void Awake()
    {
        // Her zaman Player 1'den başla
        _currentPlayerIndex = 0;
        
        // Tüm referanslar Inspector'dan atanmalı
        if (_environment == null) Debug.LogWarning("[GameFlowManager] BilliardAIEnvironment atanmadı!", this);
        if (_agent == null) Debug.LogWarning("[GameFlowManager] BilliardAgent atanmadı!", this);
        if (_testController == null) Debug.LogWarning("[GameFlowManager] BilliardTestController atanmadı!", this);
        if (_gameManager == null) Debug.LogWarning("[GameFlowManager] BilliardGameManager atanmadı!", this);
        if (_scoreManager == null) Debug.LogWarning("[GameFlowManager] BilliardScoreManager atanmadı!", this);
        if (_uiManager == null) Debug.LogWarning("[GameFlowManager] BilliardUIManager atanmadı!", this);
        if (_gameMenuUI == null) Debug.LogWarning("[GameFlowManager] BilliardGameMenuUI atanmadı!", this);

        if (_gameSettings == null)
        {
            // Attempt to find a GameSettings asset in the project
            var settingsAssets = Resources.FindObjectsOfTypeAll<GameSettings>();
            if (settingsAssets.Length > 0)
            {
                _gameSettings = settingsAssets[0];
                Debug.Log("[GameFlowManager] Found and assigned GameSettings asset.");
            }
            else
            {
                Debug.LogWarning("[GameFlowManager] No GameSettings asset found. AI behavior type will not be configured automatically.");
            }
        }
    }

    private void Start()
    {
        // In training mode we keep GameFlowManager enabled so inspector test shots work.
        // Previously this component was disabled in training which prevented _useTestShot
        // from being applied. Keep it active and let the agent forward actions here when
        // the GameFlowManager is active.
        if (_gameSettings != null && _gameSettings.IsTrainingMode)
        {
            Debug.Log("[GameFlowManager] Training mode detected - keeping GameFlowManager enabled so test shots work.");
            // Do NOT disable this component here; allow GameFlowManager to handle agent actions
            // when present so debug/test shots (_useTestShot) function during training.
        }
        
        if (_gameManager != null)
        {
            _gameManager.BallsStopped += OnBallsStopped;
            _gameManager.OnGameReset += HandleGameResetEvent;
        }
        else
        {
            Debug.LogError("[GameFlowManager] BilliardGameManager not found!");
            enabled = false;
            return;
        }

        // Set initial state based on GameMode
        InitializeGameState();
        ConfigureAgentBehavior();
        UpdateTimeScale();
    }

    private void ConfigureAgentBehavior()
    {
        if (_agent == null || _gameSettings == null) return;

        var behaviorParameters = _agent.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        if (behaviorParameters != null)
        {
            if (_gameSettings.IsTrainingMode)
            {
                behaviorParameters.BehaviorType = Unity.MLAgents.Policies.BehaviorType.Default;
                Debug.Log("[GameFlowManager] Training mode is ON. Agent behavior set to Default.");
            }
            else
            {
                behaviorParameters.BehaviorType = Unity.MLAgents.Policies.BehaviorType.InferenceOnly;
                Debug.Log("[GameFlowManager] Training mode is OFF. Agent behavior set to InferenceOnly.");
            }
        }
    }

    private void InitializeGameState()
    {
        _environment.RequestEnvironmentReset(); // Reset first
        _currentPlayerIndex = 0; // Start with Player 1

        switch (_gameManager.CurrentGameMode)
        {
            case BilliardGameManager.GameMode.SinglePlayer:
            case BilliardGameManager.GameMode.TwoPlayer:
            case BilliardGameManager.GameMode.PlayerVsAi:
                ChangeState(GameState.PlayerTurn);
                break;
            case BilliardGameManager.GameMode.AiOnly:
                ChangeState(GameState.AgentTurn);
                // In training/AiOnly mode, automatically progress to AgentDeciding
                _isStateProcessing = false;
                // Invoke(nameof(ProgressToAgentDeciding), 0.1f); // Handled in ChangeState
                break;
            default:
                ChangeState(GameState.Idle);
                break;
        }
        Debug.Log($"[GameFlowManager] Game starting in {_gameManager.CurrentGameMode} mode. Initial state: {_currentState}");
    }
    
    private void ProgressToAgentDeciding()
    {
        if (_currentState == GameState.AgentTurn)
        {
            ChangeState(GameState.AgentDeciding);
        }
    }

    private void OnDestroy()
    {
        if (_gameManager != null)
        {
            _gameManager.BallsStopped -= OnBallsStopped;
            _gameManager.OnGameReset -= HandleGameResetEvent;
        }
    }

    /// <summary>
    /// Handles a reset that occurred in the game manager. By default we set the
    /// current player to Player 1 and switch to PlayerTurn, except for AiOnly mode
    /// and when a foul reset was in progress (which already chooses its own state).
    /// </summary>
    private void HandleGameResetEvent()
    {
        // If this reset was caused by a foul handler, don't override the state
        if (_isFoulReset)
        {
            if (_showDebugInfo) Debug.Log("[GameFlowManager] Game reset occurred after foul reset - skipping default turn assignment and score reset.");
            _isFoulReset = false; // Reset the flag for next time
            return;
        }

        // Check if someone has won the game (reached final score)
        bool gameWasWon = _scoreManager != null && _scoreManager.IsGameWon(out int _);

        // Eğer oyun kazanılmışsa ve yeniden başlıyorsa, timeScale'i normale döndür
        if (gameWasWon && _gameSettings != null && !_gameSettings.IsTrainingMode)
        {
            Time.timeScale = 2f;
            if (_showDebugInfo) Debug.Log("[GameFlowManager] Game restarting after win. TimeScale restored to 2.");
        }

        if (_scoreManager != null)
        {
            if (gameWasWon)
            {
                // Someone reached the final score - reset scores for a fresh match
                if (_showDebugInfo) Debug.Log("[GameFlowManager] Game reset after win - resetting scores for new match.");
                _scoreManager.ResetScores();
            }
            else
            {
                // Normal reset - preserve player scores, only clear turn tracking and visuals
                if (_showDebugInfo) Debug.Log("[GameFlowManager] Game reset performed - preserving player scores.");
            }

            // Always finalize turn tracking and prepare visuals for next turn
            _scoreManager.FinalizeTurnTracking();
            _scoreManager.PrepareForNextTurnVisuals();
        }

        // Do not change turn for AiOnly mode
        if (_gameManager != null && _gameManager.CurrentGameMode == BilliardGameManager.GameMode.AiOnly)
        {
                if (_showDebugInfo) Debug.Log("[GameFlowManager] Game reset occurred in AiOnly mode - preserving player scores and restarting agent loop.");
            
            // FIX: Always restart the agent loop after a reset in AiOnly mode.
            // This handles cases where we were stuck in AgentShooting due to a race condition with reset,
            // or if we were in BallsMoving and balls were forced stopped.
            ChangeState(GameState.AgentTurn);
            
            // Cancel any pending invokes to avoid double calls or race conditions
            CancelInvoke(nameof(ProgressToAgentDeciding));
            Invoke(nameof(ProgressToAgentDeciding), 0.1f);
            
            return;
        }

        // Reinitialize the game state, starting at Player 1 (if that mode applies)
        InitializeGameState();
    }

    /// <summary>
    /// GameMode değiştiğinde çağrılır. Oyun durumunu yeniden başlatır.
    /// </summary>
    public void OnGameModeChanged()
    {
        if (_showDebugInfo)
        {
            Debug.Log($"[GameFlowManager] GameMode changed to '{_gameManager.CurrentGameMode}'. Reinitializing game state.");
        }
        
        // Mevcut durumu temizle
        _isStateProcessing = false;
        _currentPlayerIndex = 0;
        _ballsMovingEnteredTime = -1f;
        
        // Game mode değişince skorları sıfırla (yeni bir oyun başlıyor)
        if (_scoreManager != null)
        {
            if (_showDebugInfo) Debug.Log("[GameFlowManager] GameMode changed - resetting scores for new game setup.");
            _scoreManager.ResetScores();
            _scoreManager.FinalizeTurnTracking();
            _scoreManager.PrepareForNextTurnVisuals();
        }
        
        // Yeni moda göre başlangıç durumunu ayarla
        InitializeGameState();
    }

    private void UpdateTimeScale()
    {
        if (_gameSettings != null)
        {
            if (_gameSettings.IsTrainingMode)
            {
                Time.timeScale = 5f;
                if (_showDebugInfo) Debug.Log("[GameFlowManager] Training Mode active. TimeScale set to 5.");
            }
            else
            {
                // Eğitim modunda değilsek ve oyun kazanıldıysa timeScale'i 0 yap
                if (_scoreManager != null && _scoreManager.IsGameWon(out int _))
                {
                    Time.timeScale = 0f;
                    if (_showDebugInfo) Debug.Log("[GameFlowManager] Game Won! TimeScale set to 0 (paused).");
                }
                else
                {
                    Time.timeScale = 2f;
                    if (_showDebugInfo) Debug.Log("[GameFlowManager] Normal Mode active. TimeScale set to 2.");
                }
            }
        }
    }

    private void CheckForGameSettingsChanges()
    {
        if (_gameSettings != null && _gameSettings.IsTrainingMode != _lastTrainingMode)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[GameFlowManager] GameSettings changed. TrainingMode changed from '{_lastTrainingMode}' to '{_gameSettings.IsTrainingMode}'. Re-configuring agent.");
            }
            _lastTrainingMode = _gameSettings.IsTrainingMode;
            ConfigureAgentBehavior();
            UpdateTimeScale();
        }
    }

    private void Update()
    {
        CheckForGameSettingsChanges();

        // Watchdog'u sadece agent deciding durumunda çalıştır
        if (_currentState == GameState.AgentDeciding && _environment != null)
        {
            _environment.MonitorEnvironmentWatchdogs();
        }
        else if (_environment != null)
        {
            // Diğer durumlarda watchdog timer'larını sıfırla
            _environment.ResetWatchdogTimers();
        }

        if (_automaticProgression && !_isStateProcessing && CanProceedAutomatically())
        {
            _proceedToNextStep = true;
            _isStateProcessing = true;
            ProceedState();
        }
        else if (_proceedToNextStep && !_isStateProcessing)
        {
            _proceedToNextStep = false;
            _isStateProcessing = true;
            ProceedState();
        }

        if (_currentState == GameState.BallsMoving &&
            _ballsMovingEnteredTime > 0f &&
            Time.time - _ballsMovingEnteredTime >= _forcedStopGraceSeconds &&
            _gameManager != null &&
            !_gameManager.AreBallsMoving() &&
            !IsAnyCueShooting())
        {
            HandleBallsStoppedInternal(true);
        }
    }

    private bool IsAnyCueShooting()
    {
        bool primaryShooting = _gameManager?.PrimaryCueStick != null && _gameManager.PrimaryCueStick.IsShooting;
        bool secondaryShooting = _gameManager?.SecondaryCueStick != null && _gameManager.SecondaryCueStick.IsShooting;
        return primaryShooting || secondaryShooting;
    }

    private bool CanProceedAutomatically()
    {
        switch (_currentState)
        {
            case GameState.WaitingForPlayerShot:
            case GameState.AgentDeciding:
            case GameState.AgentShooting:
            case GameState.BallsMoving:
                return false; // These states require external triggers to proceed
            default:
                return true;
        }
    }

    /// <summary>
    /// Advances the game to the next logical state.
    /// </summary>
    private void ProceedState()
    {
        if (_automaticProgression)
        {
            switch (_currentState)
            {
                case GameState.Idle:
                    ChangeState(GameState.PlayerTurn);
                    break;
                case GameState.PlayerTurn:
                    ChangeState(GameState.WaitingForPlayerShot);
                    break;
                case GameState.WaitingForPlayerShot:
                    Debug.LogWarning("[GameFlowManager] In 'WaitingForPlayerShot'. Player must shoot to continue.");
                    _isStateProcessing = false;
                    break;
                case GameState.AgentTurn:
                    ChangeState(GameState.AgentDeciding);
                    break;
                case GameState.AgentDeciding:
                    Debug.LogWarning("[GameFlowManager] In 'AgentDeciding'. Agent must provide an action to continue.");
                    _isStateProcessing = false;
                    break;
                case GameState.AgentShooting:
                    Debug.LogWarning("[GameFlowManager] In 'AgentShooting'. Waiting for shot to complete.");
                    _isStateProcessing = false;
                    break;
                case GameState.BallsMoving:
                    Debug.LogWarning("[GameFlowManager] In 'BallsMoving'. Waiting for balls to stop.");
                    _isStateProcessing = false;
                    break;
            }
        }
        else
        {
            switch (_currentState)
            {
                case GameState.Idle:
                    ChangeState(GameState.PlayerTurn);
                    break;
                case GameState.PlayerTurn:
                    ChangeState(GameState.WaitingForPlayerShot);
                    break;
                case GameState.WaitingForPlayerShot:
                    Debug.LogWarning("[GameFlowManager] In 'WaitingForPlayerShot'. Player must shoot to continue. You cannot proceed until the player shoots.");
                    _isStateProcessing = false;
                    break;
                case GameState.AgentTurn:
                    ChangeState(GameState.AgentDeciding);
                    break;
                case GameState.AgentDeciding:
                    Debug.LogWarning("[GameFlowManager] In 'AgentDeciding'. Agent must provide an action to continue. You cannot proceed until the agent acts.");
                    _isStateProcessing = false;
                    break;
                case GameState.AgentShooting:
                    Debug.LogWarning("[GameFlowManager] In 'AgentShooting'. Waiting for shot to complete. You cannot proceed until the shot is done.");
                    _isStateProcessing = false;
                    break;
                case GameState.BallsMoving:
                    Debug.LogWarning("[GameFlowManager] In 'BallsMoving'. Waiting for balls to stop. You cannot proceed until balls stop.");
                    _isStateProcessing = false;
                    break;
            }
        }
    }

    private void ChangeState(GameState newState)
    {
        if (_currentState == newState) return;

        // Store the previous state if we are about to start moving balls
        if (newState == GameState.BallsMoving)
        {
            _lastStateBeforeMoving = _currentState;
            _ballsMovingEnteredTime = Time.time;
        }
        else if (_currentState == GameState.BallsMoving)
        {
            _ballsMovingEnteredTime = -1f;
        }

        _currentState = newState;
        Debug.Log($"[GameFlowManager] New State: {newState}");

        // Add debug log to confirm state transitions
        Debug.Log($"[GameFlowManager] Transitioning to state: {_currentState}");

        // Manage test controller and agent activity based on state
        if (_testController != null)
        {
            bool isPlayerInvolved = _gameManager.CurrentGameMode == BilliardGameManager.GameMode.SinglePlayer ||
                                    _gameManager.CurrentGameMode == BilliardGameManager.GameMode.TwoPlayer ||
                                    _gameManager.CurrentGameMode == BilliardGameManager.GameMode.PlayerVsAi;

            _testController.enabled = isPlayerInvolved && newState == GameState.WaitingForPlayerShot;
        }

        if (_agent != null)
        {
            bool isAgentInvolved = _gameManager.CurrentGameMode == BilliardGameManager.GameMode.PlayerVsAi ||
                                   _gameManager.CurrentGameMode == BilliardGameManager.GameMode.AiOnly;
            
            // Enable/disable agent based on whether it's involved in the current game mode
            if (_agent.gameObject.activeSelf != isAgentInvolved)
            {
                _agent.gameObject.SetActive(isAgentInvolved);
                Debug.Log($"[GameFlowManager] Agent game object {(isAgentInvolved ? "activated" : "deactivated")}.");
            }
        }

        switch (newState)
        {
            case GameState.Idle:
                // This state is now largely bypassed by InitializeGameState, but kept for safety.
                Debug.Log("[GameFlowManager] Game is idle. Waiting to start.");
                break;

            case GameState.PlayerTurn:
                _environment.SetTurnState(BilliardAIEnvironment.TurnState.Player);
                
                CueStick activeCue = null;
                if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.TwoPlayer)
                {
                    activeCue = _currentPlayerIndex == 0 ? _gameManager.PrimaryCueStick : _gameManager.SecondaryCueStick;
                    Debug.Log($"[GameFlowManager] Player {(_currentPlayerIndex + 1)}'s turn. Setting up for shot...");
                }
                else // SinglePlayer or PlayerVsAi
                {
                    _currentPlayerIndex = 0;
                    activeCue = _gameManager.PrimaryCueStick;
                    Debug.Log("[GameFlowManager] Player's turn. Setting up for shot...");
                }

                if (activeCue != null)
                {
                    _gameManager.SetActiveCueStick(activeCue);
                }

                if (_testController != null && activeCue != null)
                {
                    _testController.SetActiveCueStick(activeCue);
                    activeCue.ForceAlignWithBall(); // Align the active cue stick
                }

                if (_uiManager != null)
                {
                    string turnLabel = _gameManager.CurrentGameMode == BilliardGameManager.GameMode.TwoPlayer
                        ? $"Oyuncu {(_currentPlayerIndex + 1)}"
                        : "Oyuncu";
                    _uiManager.UpdateTurnInfo(turnLabel);
                }

                _scoreManager?.PrepareForNextTurnVisuals();
                break;

            case GameState.WaitingForPlayerShot:
                if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.TwoPlayer)
                    Debug.Log($"[GameFlowManager] Waiting for Player {(_currentPlayerIndex + 1)} to shoot (Press Space).");
                else
                    Debug.Log("[GameFlowManager] Waiting for player to shoot (Press Space).");
                
                // Aktif istekayı al ve ScoreManager'da turu başlat
                var waitingActiveCue = _gameManager.CurrentGameMode == BilliardGameManager.GameMode.TwoPlayer
                    ? (_currentPlayerIndex == 0 ? _gameManager.PrimaryCueStick : _gameManager.SecondaryCueStick)
                    : _gameManager.PrimaryCueStick;
                
                if (_scoreManager != null && waitingActiveCue != null && waitingActiveCue.TargetBall != null)
                {
                    _scoreManager.StartTurn(waitingActiveCue.TargetBall, waitingActiveCue);
                    if (_showDebugInfo) Debug.Log($"[GameFlowManager] Score tracking started for {waitingActiveCue.Owner}'s turn.");
                }
                break;

            case GameState.AgentTurn:
                Debug.Log("[GameFlowManager] Agent's turn. Check '_proceedToNextStep' to request a decision from the agent.");
                _environment.SetTurnState(BilliardAIEnvironment.TurnState.Agent);

                // Force alignment of the cue stick. This is critical for "AiOnly" mode where
                // the turn state might not change (Agent -> Agent), so SetTurnState wouldn't trigger alignment.
                _environment.ForceAlignCurrentCue();

                if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.PlayerVsAi ||
                    _gameManager.CurrentGameMode == BilliardGameManager.GameMode.AiOnly)
                {
                    _gameManager.SetActiveCueStick(_gameManager.SecondaryCueStick);
                }

                if (_uiManager != null)
                {
                    string turnLabel;
                    if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.TwoPlayer)
                    {
                        turnLabel = $"Oyuncu {(_currentPlayerIndex + 1)}";
                    }
                    else
                    {
                        turnLabel = "AI";
                    }

                    _uiManager.UpdateTurnInfo(turnLabel);
                }

                _scoreManager?.PrepareForNextTurnVisuals();

                // Auto-progress to decision for AI modes
                if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.AiOnly || 
                    _gameManager.CurrentGameMode == BilliardGameManager.GameMode.PlayerVsAi)
                {
                    // Automatically request decision after a short delay
                    Invoke(nameof(ProgressToAgentDeciding), 0.1f);
                }
                break;

            case GameState.AgentDeciding:
                // Agent'ın kullanacağı istekayı belirle ve ScoreManager'da turu başlat
                var agentCue = _gameManager.SecondaryCueStick;
                if (_scoreManager != null && agentCue != null && agentCue.TargetBall != null)
                {
                    _scoreManager.StartTurn(agentCue.TargetBall, agentCue);
                    if (_showDebugInfo) Debug.Log($"[GameFlowManager] Score tracking started for Agent's turn.");
                }
                
                if (_agent != null && _agent.gameObject.activeSelf)
                {
                    Debug.Log("[GameFlowManager] Waiting for agent decision (Agent triggered via TurnChanged)...");
                    // _agent.RequestDecision(); // REMOVED: Agent decides when to request decision via TurnChanged event
                    
                    // Fallback: Eğer agent 2 saniye içinde karar vermezse random shot at
                    // StartCoroutine(WaitForAgentDecisionWithTimeout(2f)); // REMOVED: Let agent decide fully
                }
                else
                {
                    Debug.LogError("[GameFlowManager] Cannot request decision - Agent is null or inactive!");
                }
                break;

            case GameState.AgentShooting:
                Debug.Log("[GameFlowManager] Agent is preparing to shoot.");
                break;

            case GameState.BallsMoving:
                Debug.Log("[GameFlowManager] Balls are moving. Waiting for them to stop.");
                break;
        }
        _isStateProcessing = false;
    }

    /// <summary>
    /// Called by the agent when an action is received.
    /// </summary>
    public void OnAgentActionReceived(ShotParameters shotParams)
    {
        // FIX: Prevent agent from acting if it's not the agent's turn.
        // This fixes the issue where AI shoots with player's cue stick in PlayerVsAi mode.
        if (_currentState == GameState.PlayerTurn || _currentState == GameState.WaitingForPlayerShot)
        {
             if (_showDebugInfo) Debug.LogWarning($"[GameFlowManager] Ignored agent action received during {_currentState}.");
             return;
        }

        // Don't reject actions based on state - let the blocked shot handler deal with invalid attempts.
        // This prevents race conditions when episode ends and immediately restarts (OnEpisodeBegin).
        // if (_currentState != GameState.AgentDeciding) return;  // REMOVED

        ChangeState(GameState.AgentShooting);
        
        // Test modu kontrolü
        ShotParameters finalShot;
        if (_useTestShot)
        {
            finalShot = new ShotParameters(_testAngleX, _testAngleY, _testPower);
            Debug.LogWarning($"[GameFlowManager] TEST MODE: Using test shot instead of agent decision!");
            Debug.Log($"[GameFlowManager] Test Shot: AngleX={finalShot.AngleX:F2}, AngleY={finalShot.AngleY:F2}, Power={finalShot.Power:F2}");
            Debug.Log($"[GameFlowManager] Agent's original decision: AngleX={shotParams.AngleX:F2}, AngleY={shotParams.AngleY:F2}, Power={shotParams.Power:F2}");
        }
        else
        {
            finalShot = shotParams;
            Debug.Log($"[GameFlowManager] Agent decided: AngleX={shotParams.AngleX:F2}, AngleY={shotParams.AngleY:F2}, Power={shotParams.Power:F2}");
        }

        // Score tracking is already started in AgentDeciding state, no need to call StartTurn again here

        // Show the agent's decision visually on the agent cue (red line) so every decision is visible
        if (_gameManager != null)
        {
            var agentCue = _gameManager.SecondaryCueStick;
            if (agentCue != null)
            {
                agentCue.ShowDecisionLine(finalShot.AngleX, finalShot.AngleY);
            }
        }

        var result = _environment.TryQueueShot(finalShot.AngleX, finalShot.AngleY, finalShot.Power);
        if (result == BilliardAIEnvironment.ShotResult.Success)
        {
            // Vuruş yapıldı, UI'yı güncelle
            if (_scoreManager != null)
            {
                _scoreManager.OnShotExecuted();
            }
            ChangeState(GameState.BallsMoving);
        }
        else if (result == BilliardAIEnvironment.ShotResult.Blocked)
        {
            Debug.LogWarning("[GameFlowManager] Agent shot BLOCKED. Requesting new decision.");
            OnAgentInvalidAction();
        }
        else if (result == BilliardAIEnvironment.ShotResult.Busy)
        {
            Debug.LogWarning("[GameFlowManager] Agent shot failed to execute (cue busy). Waiting before retry...");
            StartCoroutine(RetryAgentShotAfterDelay());
        }
        else
        {
            Debug.LogWarning($"[GameFlowManager] Agent shot failed with result: {result}");
            // If resetting or failed, maybe go back to deciding or wait?
            // For now, let's retry if it's not blocked
            if (result == BilliardAIEnvironment.ShotResult.Resetting)
            {
                 Debug.Log("[GameFlowManager] Shot rejected due to reset. Retrying decision in 0.2s...");
                 StartCoroutine(RetryDecisionAfterDelay(0.2f));
            }
            else
            {
                 ChangeState(GameState.AgentDeciding);
                 _agent.RequestDecision();
            }
        }
    }

    private System.Collections.IEnumerator RetryDecisionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // Ensure we are still in a state where we want a decision
        // We check AgentShooting because OnAgentActionReceived sets it to AgentShooting before calling TryQueueShot
        if (_currentState == GameState.AgentShooting || _currentState == GameState.AgentDeciding || _currentState == GameState.AgentTurn)
        {
             ChangeState(GameState.AgentDeciding);
             if (_agent != null && _agent.gameObject.activeSelf)
             {
                 _agent.RequestDecision();
             }
        }
    }

    /// <summary>
    /// Waits for the cue stick to finish its current animation, then returns to AgentTurn.
    /// This prevents infinite retry loops when the cue is stuck in shooting state.
    /// </summary>
    private System.Collections.IEnumerator RetryAgentShotAfterDelay()
    {
        // Wait until the cue stick finishes its animation
        float timeout = 0f;
        float maxWaitTime = 2f; // Maximum 2 seconds wait
        
        while (IsAnyCueShooting() && timeout < maxWaitTime)
        {
            yield return null;
            timeout += Time.deltaTime;
        }
        
        // // If we timed out, force reset the cue stick state
        // if (timeout >= maxWaitTime)
        // {
        //     Debug.LogError("[GameFlowManager] Cue stick stuck! Force resetting the game...");
        //     if (_gameManager != null)
        //     {
        //         _gameManager.ResetGame();
        //     }
        //     yield break;
        // }
        
        // Add a small buffer delay to ensure the cue is fully ready
        yield return new WaitForSeconds(0.1f);
        
        Debug.Log("[GameFlowManager] Cue ready. Returning to AgentTurn.");
        ChangeState(GameState.AgentTurn);
    }

    /// <summary>
    /// Waits for agent to make a decision. If agent doesn't respond within timeout,
    /// uses a random shot as fallback.
    /// </summary>
    private System.Collections.IEnumerator WaitForAgentDecisionWithTimeout(float timeout)
    {
        float elapsed = 0f;
        GameState startingState = _currentState;
        
        while (elapsed < timeout && _currentState == GameState.AgentDeciding)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        
        // If still in AgentDeciding state after timeout, use random shot
        if (_currentState == GameState.AgentDeciding)
        {
            Debug.LogWarning("[GameFlowManager] Agent decision timeout! Using random shot as fallback.");
            
            // Generate random shot parameters
            float randomAngleX = UnityEngine.Random.Range(-180f, 180f);
            float randomAngleY = UnityEngine.Random.Range(0f, 60f);
            float randomPower = UnityEngine.Random.Range(3f, 7f);
            
            ShotParameters randomShot = new ShotParameters(randomAngleX, randomAngleY, randomPower);
            OnAgentActionReceived(randomShot);
        }
    }

    /// <summary>
    /// Called by the agent when it produced an invalid action that was blocked
    /// (e.g. angle would go through wall). This forces the flow back to
    /// decision state so the agent can pick a new action.
    /// </summary>
    public void OnAgentInvalidAction()
    {
        Debug.Log("[GameFlowManager] Agent provided invalid action — applying penalty and ending episode (table position unchanged).");
        
        // Apply penalty through RewardManager and end episode
        var rewardManager = _agent != null ? _agent.GetComponent<BilliardRewardManager>() : null;
        if (rewardManager != null)
        {
            rewardManager.OnBlockedShot(); // This applies penalty and ends episode
            // Note: We do NOT reset the table - agent continues from the same position
            Debug.Log("[GameFlowManager] Episode ended. Agent will try again with same table configuration.");
            
            // IMPORTANT: Change state to AgentDeciding so the next action from OnEpisodeBegin
            // will be accepted. Without this, the agent gets stuck in AgentShooting state
            // and all subsequent actions are rejected as "not in AgentDeciding state".
            ChangeState(GameState.AgentDeciding);
            
            // Clear processing flag so the next decision can proceed
            _isStateProcessing = false;
            return;
        }
        else
        {
            Debug.LogWarning("[GameFlowManager] RewardManager not found! Cannot apply blocked shot penalty.");
            // Fallback: just request a new decision (old behavior)
            ChangeState(GameState.AgentDeciding);
            _agent.RequestDecision();
        }
    }

    /// <summary>
    /// Called by the environment when the player shoots.
    /// </summary>
    public void OnPlayerShot()
    {
        if (_currentState != GameState.WaitingForPlayerShot) return;

        // Vuruş yapıldı, UI'yı gerçek değerlerle güncelle
        // (StartTurn zaten WaitingForPlayerShot state'inde çağrıldı)
        if (_scoreManager != null)
        {
            _scoreManager.OnShotExecuted();
        }

        Debug.Log("[GameFlowManager] Player has shot.");
        _lastStateBeforeMoving = _currentState; // WaitingForPlayerShot'ı kaydet
        ChangeState(GameState.BallsMoving);
    }

    /// <summary>
    /// Callback for when all balls have stopped moving.
    /// </summary>
    private void OnBallsStopped()
    {
        // Start a coroutine to wait for the cue stick to finish its animation
        // before proceeding to the next state. This prevents race conditions.
        StartCoroutine(OnBallsStoppedRoutine());
    }

    private System.Collections.IEnumerator OnBallsStoppedRoutine()
    {
        // Wait until any active cue stick has finished its shooting animation.
        while (IsAnyCueShooting())
        {
            yield return null; // Wait for the next frame
        }
        
        HandleBallsStoppedInternal(false);
    }

    private void HandleBallsStoppedInternal(bool wasForced)
    {
        // FIX: Ignore BallsStopped event if we are not in BallsMoving state.
        // This prevents spurious events (e.g. at startup or after reset) from messing up the turn logic.
        if (_currentState != GameState.BallsMoving && !wasForced)
        {
            if (_showDebugInfo) Debug.Log($"[GameFlowManager] Balls stopped event received in state {_currentState}. Ignoring.");
            return;
        }

        // FIX: If a foul reset is pending (e.g., out of bounds), skip turn evaluation entirely.
        // OnOutOfBoundsFoul has already determined the next player, and we should just wait
        // for PerformPendingFoulReset to execute at the end of this method.
        if (_pendingFoulReset)
        {
            if (_showDebugInfo) Debug.Log("[GameFlowManager] Pending foul reset detected - skipping turn evaluation, will reset directly.");
            _scoreManager?.FinalizeTurnTracking();
            _scoreManager?.PrepareForNextTurnVisuals();
            _isStateProcessing = false;
            // Jump directly to the reset logic at the end
            PerformPendingFoulReset();
            return;
        }

        // Eğer faul reset'inden geliyorsak, bu eventi atla
        if (_isFoulReset)
        {
            if (_showDebugInfo) Debug.Log("[GameFlowManager] Balls stopped after foul reset. Skipping HandleBallsStoppedInternal.");
            _isFoulReset = false;
            _isStateProcessing = false;
            
            // Sayaçları sıfırla (faul resetinden sonra yeni tura hazır olmalıyız)
            if (_scoreManager != null)
            {
                _scoreManager.PrepareForNextTurnVisuals();
            }
            return;
        }
        
        bool canEvaluateScore = _scoreManager != null && _scoreManager.IsTurnActive;
        if (!canEvaluateScore)
        {
            if (_showDebugInfo)
            {
                Debug.LogWarning("[GameFlowManager] Balls stopped but ScoreManager had no active turn. Forcing flow to advance as a missed shot.");
            }

            // Ensure we do not keep stale subscriptions or state when score tracking was skipped
            _scoreManager?.FinalizeTurnTracking();
        }

        if (_showDebugInfo) Debug.Log("[GameFlowManager] All balls have stopped.");

        // Check for foul first
        bool hasFoul = canEvaluateScore && _scoreManager.HasFoul();
        bool scoreConditionsMet = canEvaluateScore && _scoreManager.CheckScoreCondition();
        bool scoreMade = canEvaluateScore && scoreConditionsMet && !hasFoul;
        
        _isStateProcessing = false;

        if (_uiManager != null)
        {
            if (hasFoul)
                _uiManager.ShowResult("FAUL!", false);
            else if (scoreMade)
                _uiManager.ShowResult("Sayı!", true);
            else
                _uiManager.ShowResult("Sıra Geçti", false);
        }

        // Commit score if made
        if (scoreMade)
        {
            int scorerIndex = 0;
            if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.TwoPlayer)
            {
                scorerIndex = _currentPlayerIndex;
            }
            else if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.PlayerVsAi)
            {
                scorerIndex = (_lastStateBeforeMoving == GameState.WaitingForPlayerShot) ? 0 : 1;
            }
            else if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.AiOnly)
            {
                scorerIndex = 1;
            }
            
            _scoreManager.CommitScore(scorerIndex);

            // Check Win
            if (_scoreManager.IsGameWon(out int winnerIndex))
            {
                string winnerName = winnerIndex == 0 ? "Oyuncu 1" : "Oyuncu 2 / AI";
                if (_uiManager != null) _uiManager.ShowResult($"{winnerName} KAZANDI!", true);
                Debug.Log($"GAME OVER! Winner: {winnerName}");

                // Eğitim modunda değilsek oyunu durdur (timeScale = 0)
                if (_gameSettings != null && !_gameSettings.IsTrainingMode)
                {
                    Time.timeScale = 0f;
                    if (_showDebugInfo) Debug.Log("[GameFlowManager] Game Won! TimeScale set to 0 (paused).");
                }

                if (_gameMenuUI != null)
                {
                    _isStateProcessing = true;
                    _gameMenuUI.ShowWinPrompt(winnerName, () =>
                    {
                        // Butona basıldığında timeScale'i normale döndür
                        if (_gameSettings != null && !_gameSettings.IsTrainingMode)
                        {
                            Time.timeScale = 2f;
                            if (_showDebugInfo) Debug.Log("[GameFlowManager] Win button pressed. TimeScale restored to 2.");
                        }
                        
                        // ResetGame will trigger HandleGameResetEvent which resets scores
                        _gameManager.ResetGame();
                        _isStateProcessing = false;
                    });
                }
                else
                {
                    // Butona basıldığında timeScale'i normale döndür
                    if (_gameSettings != null && !_gameSettings.IsTrainingMode)
                    {
                        Time.timeScale = 2f;
                        if (_showDebugInfo) Debug.Log("[GameFlowManager] Game restarting. TimeScale restored to 2.");
                    }
                    
                    // ResetGame will trigger HandleGameResetEvent which resets scores
                    _gameManager.ResetGame();
                }
                return;
            }
        }

        // Determine next state based on game mode and score
        GameState nextState;

        if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.TwoPlayer)
        {
            // In Two Player mode: if score was made, same player continues; otherwise switch player
            if (scoreMade)
            {
                if (_showDebugInfo) Debug.Log($"[GameFlowManager] Score made. Player {(_currentPlayerIndex + 1)} plays again.");
            }
            else
            {
                // No score made (miss or foul) - switch to other player
                _currentPlayerIndex = (_currentPlayerIndex + 1) % 2;
                if (_showDebugInfo) Debug.Log($"[GameFlowManager] Turn ended without score. Switching to Player {(_currentPlayerIndex + 1)}.");
            }
            nextState = GameState.PlayerTurn;
        }
        else if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.PlayerVsAi)
        {
            // In PlayerVsAi, switch between Player and Agent
            if (_lastStateBeforeMoving == GameState.WaitingForPlayerShot)
            {
                nextState = scoreMade ? GameState.PlayerTurn : GameState.AgentTurn;
            }
            else // Agent just shot
            {
                nextState = scoreMade ? GameState.AgentTurn : GameState.PlayerTurn;
            }
        }
        else if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.SinglePlayer)
        {
            // In Single Player, it's always the player's turn
            nextState = GameState.PlayerTurn;
        }
        else // AiOnly
        {
        // In AiOnly mode, it's always the agent's turn
            nextState = GameState.AgentTurn;
        }

        // If a score was made, the current player/agent continues.
        // If no score was made or there was a foul, the turn is switched.
        // The board is NOT reset unless a ball goes out of bounds.
        _scoreManager?.FinalizeTurnTracking();
        
        // Bir sonraki tur için UI sayaçlarını hazırla (bant ve top sayaçlarını sıfırla)
        _scoreManager?.PrepareForNextTurnVisuals();

        // If a foul reset was requested earlier, perform it now — after balls have stopped
        // and after reward handlers had chance to process the event. This prevents
        // premature resets that would start a new turn while physics callbacks are
        // still being processed.
        if (_pendingFoulReset)
        {
            if (_showDebugInfo) Debug.Log("[GameFlowManager] Pending foul reset detected - performing reset now after balls stopped.");
            PerformPendingFoulReset();
            return;
        }

        ChangeState(nextState);
    }

    /// <summary>
    /// Top sınır dışına çıktığında çağrılır. Faul olarak kabul edilir ve sıra karşı tarafa geçer.
    /// Skorlar korunur, sadece sıra değişir.
    /// </summary>
    public void OnOutOfBoundsFoul()
    {
        // Prevent double handling if multiple balls go out or reset is already in progress
        if (_isFoulReset || (_gameManager != null && _gameManager.IsResetting))
        {
            return;
        }

        if (_showDebugInfo) Debug.Log("[GameFlowManager] Out of bounds FOUL detected. Deferring reset so reward handlers can process the foul.");

        // Do NOT finalize or clear ScoreManager state here. Keep current turn tracking
        // intact so reward handlers and listeners can inspect the real turn stats
        // when they receive the OutOfBounds notification. Finalization/visual
        // preparation will happen after balls stop in HandleBallsStoppedInternal.

        // If we've already scheduled a pending foul reset, skip duplicate scheduling
        if (_pendingFoulReset || (_gameManager != null && _gameManager.IsResetting))
        {
            return;
        }

        // Compute next state now and defer the actual reset/state-change slightly so
        // subscribed listeners (RewardManager) receive the score update and can
        // end the episode before we reset the table and start the next turn.
        GameState nextState;
        
        if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.TwoPlayer)
        {
            // İki oyunculu modda sırayı değiştir
            _currentPlayerIndex = (_currentPlayerIndex + 1) % 2;
            if (_showDebugInfo) Debug.Log($"[GameFlowManager] Foul. Switching to Player {(_currentPlayerIndex + 1)}.");
            nextState = GameState.PlayerTurn;
        }
        else if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.PlayerVsAi)
        {
            // Oyuncu vs AI modda sırayı değiştir
            if (_lastStateBeforeMoving == GameState.WaitingForPlayerShot || _currentState == GameState.WaitingForPlayerShot)
            {
                // Oyuncu vuruş yaptıysa sıra AI'ye geçer
                nextState = GameState.AgentTurn;
            }
            else
            {
                // AI vuruş yaptıysa sıra oyuncuya geçer
                nextState = GameState.PlayerTurn;
            }
        }
        else if (_gameManager.CurrentGameMode == BilliardGameManager.GameMode.SinglePlayer)
        {
            // Tek oyunculu modda oyuncunun sırası devam eder
            nextState = GameState.PlayerTurn;
        }
        else // AiOnly
        {
            // AI Only modda AI'nin sırası devam eder
            nextState = GameState.AgentTurn;
        }
        
        // Store pending reset information and schedule the reset shortly.
        _pendingNextState = nextState;
        _pendingFoulReset = true;

        // Show FAUL immediately on UI so player sees it.
        if (_uiManager != null)
        {
            _uiManager.ShowResult("FAUL", false);
        }

        // Clear watchdog timers so watchdog doesn't trigger during the brief defer window
        if (_environment != null)
        {
            var envType = _environment.GetType();
            var idleTimerField = envType.GetField("_idleTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cueTimerField = envType.GetField("_cueStuckTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var idleActiveField = envType.GetField("_idleWatchdogActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cueActiveField = envType.GetField("_cueWatchdogActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (idleTimerField != null) idleTimerField.SetValue(_environment, 0f);
            if (cueTimerField != null) cueTimerField.SetValue(_environment, 0f);
            if (idleActiveField != null) idleActiveField.SetValue(_environment, false);
            if (cueActiveField != null) cueActiveField.SetValue(_environment, false);
        }

        // Do NOT schedule an immediate reset here. The pending foul reset is
        // intentionally deferred until HandleBallsStoppedInternal so the full
        // turn (physics + reward processing) completes before we reset the table.
    }

    private void PerformPendingFoulReset()
    {
        if (!_pendingFoulReset) return;

        if (_showDebugInfo) Debug.Log("[GameFlowManager] Performing deferred foul reset now.");

        // Mark that a foul reset is happening so HandleGameResetEvent knows to preserve scores
        _isFoulReset = true;

        // Perform the actual reset
        if (_gameManager != null)
        {
            _gameManager.ResetGame();
        }

        // Apply the stored next state
        ChangeState(_pendingNextState);

        // If next turn is agent, request a decision
        if ((_pendingNextState == GameState.AgentTurn || _pendingNextState == GameState.AgentDeciding) && _agent != null)
        {
            _agent.RequestDecision();
        }

        _pendingFoulReset = false;
        _pendingNextState = GameState.Idle;
    }

}
