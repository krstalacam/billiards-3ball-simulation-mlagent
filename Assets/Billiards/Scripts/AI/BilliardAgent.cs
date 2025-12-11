using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// ML-Agents ana koordinatör sınıfı. 
/// Sadece ML-Agents lifecycle metodlarını yönetir, iş mantığı alt bileşenlere dağıtılmıştır.
/// </summary>
public class BilliardAgent : Agent
{
    [Header("Environment")]
    [SerializeField] private BilliardAIEnvironment _environment;

    [Header("Configuration")]
    [SerializeField] private BilliardAgentConfig _config;
    [SerializeField] private GameSettings _gameSettings;
    public BilliardAgentConfig Config => _config;
    public BilliardAIEnvironment Environment => _environment;

    [Header("Control Integration")]
    [SerializeField] private BilliardTestController _testController;

    [Header("Flow Control")]
    [SerializeField] private GameFlowManager _flowManager;

    // Modular components
    private BilliardObservationCollector _observationCollector;
    private BilliardActionMapper _actionMapper;
    private BilliardRewardManager _rewardManager;
    private BilliardEpisodeManager _episodeManager;

    // Control state
    private bool _heuristicMode;
    private BehaviorParameters _behaviorParameters;
    private bool _pendingTurnDecision;



    public override void Initialize()
    {
        base.Initialize();

        // Tüm referanslar Inspector'dan atanmalı
        if (_environment == null)
        {
            Debug.LogWarning("[BilliardAgent] BilliardAIEnvironment atanmadı!", this);
        }
        
        if (_flowManager == null)
        {
            Debug.LogWarning("[BilliardAgent] GameFlowManager atanmadı!", this);
        }

        // Create default config if missing
        if (_config == null)
        {
            _config = BilliardAgentConfig.CreateDefault();
            Debug.LogWarning("[BilliardAgent] No config assigned, using default settings.");
        }

        _behaviorParameters = GetComponent<BehaviorParameters>();

        // Log behavior parameters configuration
        if (_behaviorParameters != null)
        {
            Debug.Log($"[BilliardAgent] Behavior Config -> Type={_behaviorParameters.BehaviorType}, " +
                      $"Model={(_behaviorParameters.Model != null ? _behaviorParameters.Model.name : "NULL")}, " +
                      $"InferenceDevice={_behaviorParameters.InferenceDevice}");
        }

        // Initialize modular components
        _observationCollector = new BilliardObservationCollector(_config.tableExtents);
        _actionMapper = new BilliardActionMapper(
            _config.angleXLimits,
            _config.angleYLimits,
            _config.powerLimits
        );
        _episodeManager = new BilliardEpisodeManager(_environment);
        
        _rewardManager = GetComponent<BilliardRewardManager>();
        if (_rewardManager == null)
        {
            // Eğer component yoksa otomatik ekle (veya uyarı ver)
            _rewardManager = gameObject.AddComponent<BilliardRewardManager>();
        }

        // Test controller Inspector'dan atanmalı
        if (_testController == null && _config.toggleTestControllerForHeuristic)
        {
            Debug.LogWarning("[BilliardAgent] BilliardTestController atanmadı!", this);
        }

        ApplyControlMode();
        
        _environment?.RegisterAgent(this);

        if (_gameSettings == null)
        {
            var settings = Resources.FindObjectsOfTypeAll<GameSettings>();
            if (settings.Length > 0) _gameSettings = settings[0];
        }

        if (_gameSettings != null)
        {
            _gameSettings.SettingsChanged += OnSettingsChanged;
            UpdateModelFromSettings();
        }
    }

    private void OnSettingsChanged()
    {
        UpdateModelFromSettings();
    }

    private void UpdateModelFromSettings()
    {
        if (_behaviorParameters != null && _gameSettings != null)
        {
            var newModel = _gameSettings.CurrentModel;
            if (newModel != null && _behaviorParameters.Model != newModel)
            {
                _behaviorParameters.Model = newModel;
                Debug.Log($"[BilliardAgent] Model updated to: {newModel.name}");
            }
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_gameSettings != null)
        {
            _gameSettings.SettingsChanged -= OnSettingsChanged;
        }
    }

    public void BindToEnvironment(BilliardAIEnvironment environment)
    {
        if (_environment != null)
        {
            _environment.BallsStopped -= OnBallsStopped;
            _environment.TurnChanged -= OnTurnChanged;
        }

        _environment = environment;

        if (_environment != null)
        {
            _environment.BallsStopped += OnBallsStopped;
            _environment.TurnChanged += OnTurnChanged;
        }
    }

    private void OnTurnChanged(BilliardAIEnvironment.TurnState newState)
    {
        // GameFlowManager aktifse ve sıra ajana geldiyse karar iste
        if (_flowManager != null && _flowManager.isActiveAndEnabled)
        {
            if (newState == BilliardAIEnvironment.TurnState.Agent)
            {
                CancelInvoke(nameof(RequestDecisionDelayed));

                // Eğer toplar hâlâ hareket ediyorsa, durduktan hemen sonra karar isteyeceğiz
                if (_environment != null && _environment.IsShotInProgress)
                {
                    _pendingTurnDecision = true;
                    Debug.Log("[BilliardAgent] Turn=Agent but balls moving -> deferring decision until stop.");
                    return;
                }

                _pendingTurnDecision = false;
                Debug.Log("[BilliardAgent] Turn changed to Agent -> Requesting Decision...");
                Invoke(nameof(RequestDecisionDelayed), 0.2f);
            }
        }
    }

    private void OnBallsStopped()
    {
        _rewardManager?.OnTurnEnded();
        _episodeManager?.RegisterTurnCompletion(_config, this);

        // Eğer sıra ajanda ve karar isteği toplar durana ertelendiyse, şimdi iste
        if (_pendingTurnDecision && _flowManager != null && _flowManager.isActiveAndEnabled)
        {
            _pendingTurnDecision = false;
            CancelInvoke(nameof(RequestDecisionDelayed));
            Invoke(nameof(RequestDecisionDelayed), 0.1f);
            return;
        }
        
        // Training mode'da bir sonraki atış için hazır ol
        if (_behaviorParameters != null && _behaviorParameters.BehaviorType == BehaviorType.Default)
        {
            // GameFlowManager aktifse kararları o yönetir (TurnChanged eventi ile), biz karışmayalım
            if (_flowManager != null && _flowManager.isActiveAndEnabled) return;

            // Bir süre bekle ve yeni karar iste
            Invoke(nameof(RequestDecisionDelayed), 0.2f);
        }
    }
    
    private void RequestDecisionDelayed()
    {
        RequestDecision();
    }

    public override void OnEpisodeBegin()
    {
        // Her episode başında environment'ı resetleme
        // Çünkü her tur = 1 episode ve oyun devam etmeli
        // Sadece ilk başta veya manuel reset gerektiğinde reset yapılır
        
        Debug.Log("[BilliardAgent] Episode Begin - NOT resetting environment (turn-based learning)");
        _episodeManager?.BeginEpisode();
        
        // Cancel any pending decision requests from the previous episode.
        CancelInvoke(nameof(RequestDecisionDelayed));
        
        // GameFlowManager aktifse state kontrolü yap
        if (_flowManager != null && _flowManager.isActiveAndEnabled)
        {
            // Training modda veya FlowManager AgentDeciding state'indeyse karar iste
            bool isTrainingMode = _environment != null && _environment.CurrentTurn == BilliardAIEnvironment.TurnState.None;
            bool isAgentDeciding = _flowManager.CurrentState == GameFlowManager.GameState.AgentDeciding;
            
            if (isTrainingMode || isAgentDeciding || (_environment != null && _environment.CurrentTurn == BilliardAIEnvironment.TurnState.Agent))
            {
                Debug.Log($"[BilliardAgent] Requesting decision (Training={isTrainingMode}, Deciding={isAgentDeciding})");
                Invoke(nameof(RequestDecisionDelayed), 0.2f);
            }
            else
            {
                Debug.Log($"[BilliardAgent] Skipping decision request (CurrentState={_flowManager.CurrentState}, Turn={_environment?.CurrentTurn})");
            }
            return;
        }
        
        // Bir sonraki karar için hazır ol
        Invoke(nameof(RequestDecisionDelayed), 0.2f);
    }

    private void Update()
    {
        // All decision logic is now handled by GameFlowManager
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        _observationCollector?.CollectObservations(sensor, _environment);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_environment == null || _actionMapper == null)
            return;

        ShotParameters shotParams = _actionMapper.ExtractShotParameters(actions);
        string snapshot = _observationCollector != null ? _observationCollector.LastObservationSnapshot : "<no-observation>";
        Debug.Log(
            $"[DecisionSnapshot] {snapshot} || Action -> AngleX={shotParams.AngleX:F2}, AngleY={shotParams.AngleY:F2}, Power={shotParams.Power:F2}");
        
        // Training mode: by default we do direct shots for faster training, but
        // when a GameFlowManager is present and active we should forward the
        // action so GameFlowManager can apply debug/test shots (e.g. _useTestShot).
        if (_behaviorParameters != null && _behaviorParameters.BehaviorType == BehaviorType.Default)
        {
            if (_flowManager != null && _flowManager.isActiveAndEnabled)
            {
                // Let the GameFlowManager handle the shot (it may apply test shot overrides)
                _rewardManager?.OnTurnStarted();
                _flowManager.OnAgentActionReceived(shotParams);
                return;
            }

            // No GameFlowManager available/active: perform the direct training shot
            var result = _environment.TryQueueShot(shotParams.AngleX, shotParams.AngleY, shotParams.Power);
            
            if (result == BilliardAIEnvironment.ShotResult.Success)
            {
                _rewardManager?.OnTurnStarted();
            }
            else if (result == BilliardAIEnvironment.ShotResult.Blocked)
            {
                // Blocked shot - apply penalty and end episode via RewardManager
                _rewardManager?.OnBlockedShot();
            }
            else
            {
                // Busy, Resetting or Failed
                Debug.LogWarning($"[BilliardAgent] Shot failed in training mode: {result}. Retrying...");
                // Retry after a delay to allow state to clear
                Invoke(nameof(RequestDecisionDelayed), 0.5f);
            }
        }
        else if (_flowManager != null)
        {
            _rewardManager?.OnTurnStarted();
            // Play mode'da GameFlowManager kullan
            _flowManager.OnAgentActionReceived(shotParams);
        }
    }

    private void ApplyControlMode()
    {
        if (_behaviorParameters != null)
        {
            _heuristicMode = _behaviorParameters.BehaviorType == BehaviorType.HeuristicOnly;
        }

        if (_config.toggleTestControllerForHeuristic)
        {
            if (_testController != null)
            {
                _testController.enabled = _heuristicMode;
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Heuristic mode handled by BilliardTestController
    }
}
