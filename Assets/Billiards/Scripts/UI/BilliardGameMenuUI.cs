using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the overlay and settings UI for the billiards experience.
/// Shows victory prompts and exposes buttons/dropdowns for runtime settings.
/// </summary>
public class BilliardGameMenuUI : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private GameSettings _gameSettings;
    [SerializeField] private BilliardGameManager _gameManager;
    [SerializeField] private GameFlowManager _gameFlowManager;
    [SerializeField] private BilliardTestController _testController;

    [Header("Win Prompt")]
    [SerializeField] private GameObject _winPanel;
    [SerializeField] private Text _winMessageText;
    [SerializeField] private Button _playAgainButton;

    [Header("Settings UI")]
    [SerializeField] private Dropdown _gameModeDropdown;
    [SerializeField] private Dropdown _ballModeDropdown;
    [SerializeField] private Dropdown _modelDropdown;
    [SerializeField] private Toggle _trainingModeToggle;
    [SerializeField] private Toggle _randomizeBallPositionsToggle;
    [SerializeField] private Toggle _showAimInGameToggle;
    [SerializeField] private CueStick _cueStick;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Slider _timeScaleSlider;
    [SerializeField] private TextMeshProUGUI _timeScaleValueText;
    [SerializeField] private int _minTimeScaleInt = 0;
    [SerializeField] private int _maxTimeScaleInt = 20;
    [SerializeField] private Button _fullscreenButton;
    private TextMeshProUGUI _fullscreenButtonTMP;
    
    [Header("Settings Panel")]
    [SerializeField] private GameObject _settingsPanel;

    private Action _pendingWinConfirm;

    private void Awake()
    {
        if (_gameSettings == null)
        {
            GameSettings[] settingsAssets = Resources.FindObjectsOfTypeAll<GameSettings>();
            if (settingsAssets.Length > 0)
            {
                _gameSettings = settingsAssets[0];
            }
        }

        // Tüm referanslar Inspector'dan atanmalı
        if (_gameManager == null)
        {
            Debug.LogWarning("[BilliardGameMenuUI] BilliardGameManager atanmadı!", this);
        }

        if (_gameFlowManager == null)
        {
            Debug.LogWarning("[BilliardGameMenuUI] GameFlowManager atanmadı!", this);
        }

        if (_testController == null)
        {
            Debug.LogWarning("[BilliardGameMenuUI] BilliardTestController atanmadı!", this);
        }

        if (_cueStick == null)
        {
            Debug.LogWarning("[BilliardGameMenuUI] CueStick atanmadı! Inspector'dan atayın if you want to control aim visibility.", this);
        }
    }

    private void Start()
    {
        if (_winPanel != null)
        {
            _winPanel.SetActive(false);
        }

        InitializeDropdown(_gameModeDropdown, typeof(BilliardGameManager.GameMode));
        InitializeDropdown(_ballModeDropdown, typeof(BilliardGameManager.BallMode));
        SyncSettingsUI();

        if (_playAgainButton != null)
        {
            _playAgainButton.onClick.AddListener(OnPlayAgainClicked);
        }

        if (_restartButton != null)
        {
            _restartButton.onClick.AddListener(RestartGame);
        }

        if (_gameModeDropdown != null)
        {
            _gameModeDropdown.onValueChanged.AddListener(OnGameModeChanged);
        }

        if (_ballModeDropdown != null)
        {
            _ballModeDropdown.onValueChanged.AddListener(OnBallModeChanged);
        }

        if (_trainingModeToggle != null)
        {
            _trainingModeToggle.onValueChanged.AddListener(OnTrainingModeChanged);
        }

        if (_modelDropdown != null)
        {
            InitializeModelDropdown();
            _modelDropdown.onValueChanged.AddListener(OnModelChanged);
        }

        // Initialize time scale slider (we expect the slider to be integer-stepped)
        if (_timeScaleSlider != null)
        {
            _timeScaleSlider.wholeNumbers = true;
            _timeScaleSlider.minValue = _minTimeScaleInt;
            _timeScaleSlider.maxValue = _maxTimeScaleInt;
            int initial = Mathf.Clamp(Mathf.RoundToInt(Time.timeScale), _minTimeScaleInt, _maxTimeScaleInt);
            _timeScaleSlider.SetValueWithoutNotify(initial);
            _timeScaleSlider.onValueChanged.AddListener(OnTimeScaleSliderChanged);
        }

        // Auto-find child TMP for time scale value text if not assigned
        if (_timeScaleValueText == null && _timeScaleSlider != null)
        {
            _timeScaleValueText = _timeScaleSlider.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_timeScaleValueText != null)
        {
            int timeScaleInt = Mathf.Clamp(Mathf.RoundToInt(Time.timeScale), _minTimeScaleInt, _maxTimeScaleInt);
            UpdateTimeScaleText(timeScaleInt);
        }

        // Ensure the settings panel is closed when the scene starts
        if (_settingsPanel != null)
        {
            _settingsPanel.SetActive(false);
        }

        // Initialize fullscreen button label and auto-find child TMP
        if (_fullscreenButton != null)
        {
            if (_fullscreenButtonTMP == null)
            {
                _fullscreenButtonTMP = _fullscreenButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            _fullscreenButton.onClick.AddListener(OnFullscreenClicked);
            UpdateFullscreenButtonText();
        }

        // Initialize show-aim toggle (allows controlling CueStick's runtime aim visualization)
        if (_showAimInGameToggle != null)
        {
            bool initial = _cueStick != null ? _cueStick.ShowAimInGame : false;
            _showAimInGameToggle.SetIsOnWithoutNotify(initial);
            _showAimInGameToggle.onValueChanged.AddListener(OnShowAimInGameToggleChanged);
        }

        // Initialize randomize ball positions toggle
        if (_randomizeBallPositionsToggle != null)
        {
            bool initialRandomize = _gameSettings != null && _gameSettings.RandomizeBallPositions;
            _randomizeBallPositionsToggle.SetIsOnWithoutNotify(initialRandomize);
            _randomizeBallPositionsToggle.onValueChanged.AddListener(OnRandomizeBallPositionsToggleChanged);
            
            // Training modda toggle'ı devre dışı bırak (otomatik aktif)
            UpdateRandomizeBallPositionsToggleState();
        }
    }

    private void Update()
    {
        if (_settingsPanel == null) return;

        // Ignore ESC if win panel is active
        if (_winPanel != null && _winPanel.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool newState = !_settingsPanel.activeSelf;
            _settingsPanel.SetActive(newState);
        }

        // Keep the slider in sync with Time.timeScale in case other code
        // or the editor changed it at runtime.
        if (_timeScaleSlider != null)
        {
            int sliderInt = Mathf.RoundToInt(_timeScaleSlider.value);
            if (!Mathf.Approximately(Time.timeScale, sliderInt))
            {
                int clamped = Mathf.Clamp(Mathf.RoundToInt(Time.timeScale), _minTimeScaleInt, _maxTimeScaleInt);
                _timeScaleSlider.SetValueWithoutNotify(clamped);
                UpdateTimeScaleText(clamped);
            }
        }

        // Keep fullscreen button label up-to-date if fullscreen was changed externally
        if (_fullscreenButtonTMP != null && _settingsPanel != null && _settingsPanel.activeSelf)
        {
            UpdateFullscreenButtonText();
        }
    }

    /// <summary>
    /// Programmatically toggle settings panel state.
    /// </summary>
    public void ToggleSettingsPanel(bool open)
    {
        if (_settingsPanel == null) return;
        _settingsPanel.SetActive(open);
    }

    private void InitializeDropdown(Dropdown dropdown, Type enumType)
    {
        if (dropdown == null || enumType == null) return;

        dropdown.ClearOptions();
        string[] names = Enum.GetNames(enumType);
        List<string> options = new List<string>(names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            options.Add(names[i]);
        }
        dropdown.AddOptions(options);
    }

    private void SyncSettingsUI()
    {
        if (_gameSettings == null) return;

        if (_gameModeDropdown != null)
        {
            _gameModeDropdown.SetValueWithoutNotify((int)_gameSettings.CurrentGameMode);
        }

        if (_ballModeDropdown != null)
        {
            _ballModeDropdown.SetValueWithoutNotify((int)_gameSettings.CurrentBallMode);
        }

        if (_trainingModeToggle != null)
        {
            _trainingModeToggle.SetIsOnWithoutNotify(_gameSettings.IsTrainingMode);
        }

        if (_modelDropdown != null)
        {
            _modelDropdown.SetValueWithoutNotify(_gameSettings.CurrentModelIndex);
        }

        if (_randomizeBallPositionsToggle != null)
        {
            _randomizeBallPositionsToggle.SetIsOnWithoutNotify(_gameSettings.RandomizeBallPositions);
            UpdateRandomizeBallPositionsToggleState();
        }
    }

    public void ShowWinPrompt(string winnerLabel, Action onConfirmed)
    {
        _pendingWinConfirm = onConfirmed;

        if (_winPanel != null)
        {
            _winPanel.SetActive(true);
        }

        if (_winMessageText != null)
        {
            _winMessageText.text = $"{winnerLabel} kazandı!\nYeniden oynamak ister misiniz?";
        }
    }

    private void OnPlayAgainClicked()
    {
        if (_winPanel != null)
        {
            _winPanel.SetActive(false);
        }

        Action callback = _pendingWinConfirm;
        _pendingWinConfirm = null;
        callback?.Invoke();
    }

    private void RestartGame()
    {
        // Simulate pressing the 'R' key. If a BilliardTestController exists, use its ResetGame
        // method which contains the same logic as the R-key handler (reset game + test controller defaults).
        if (_testController != null)
        {
            _testController.ResetGame();
        }
        
        // Also reset the flow manager to ensure scores are reset
        if (_gameFlowManager != null)
        {
            _gameFlowManager.OnGameModeChanged();
        }
    }

    private void OnGameModeChanged(int optionIndex)
    {
        if (_gameSettings == null) return;
        var modes = Enum.GetValues(typeof(BilliardGameManager.GameMode));
        if (optionIndex < 0 || optionIndex >= modes.Length) return;

        _gameSettings.SetGameMode((BilliardGameManager.GameMode)modes.GetValue(optionIndex));
        
        // Mod değiştiğinde toggle'ı otomatik güncelle
        UpdateAimToggleBasedOnGameMode();
    }
    
    /// <summary>
    /// Oyun moduna göre aim toggle'ı günceller
    /// AI Only modunda toggle otomatik açılır, diğer modlarda kapatılır
    /// </summary>
    private void UpdateAimToggleBasedOnGameMode()
    {
        if (_showAimInGameToggle == null || _cueStick == null) return;
        
        // CueStick'in mevcut ShowAimInGame değerini oku ve toggle'a yansıt
        _showAimInGameToggle.SetIsOnWithoutNotify(_cueStick.ShowAimInGame);
    }

    private void OnBallModeChanged(int optionIndex)
    {
        if (_gameSettings == null) return;
        var modes = Enum.GetValues(typeof(BilliardGameManager.BallMode));
        if (optionIndex < 0 || optionIndex >= modes.Length) return;

        _gameSettings.SetBallMode((BilliardGameManager.BallMode)modes.GetValue(optionIndex));
    }

    private void OnTrainingModeChanged(bool isOn)
    {
        if (_gameSettings == null) return;
        _gameSettings.SetTrainingMode(isOn);
        
        // Training mode değiştiğinde randomize toggle durumunu güncelle
        UpdateRandomizeBallPositionsToggleState();
    }

    private void InitializeModelDropdown()
    {
        if (_modelDropdown == null || _gameSettings == null) return;
        
        _modelDropdown.ClearOptions();
        List<string> names = _gameSettings.GetModelNames();
        _modelDropdown.AddOptions(names);
        _modelDropdown.SetValueWithoutNotify(_gameSettings.CurrentModelIndex);
    }

    private void OnModelChanged(int index)
    {
        if (_gameSettings != null)
        {
            _gameSettings.SetModelIndex(index);
        }
    }

    private void OnShowAimInGameToggleChanged(bool isOn)
    {
        if (_cueStick != null)
        {
            _cueStick.SetShowAimInGame(isOn);
        }
    }

    private void OnRandomizeBallPositionsToggleChanged(bool isOn)
    {
        if (_gameSettings != null)
        {
            _gameSettings.SetRandomizeBallPositions(isOn);
        }
    }

    /// <summary>
    /// Training modda toggle'ı devre dışı bırak (otomatik aktif)
    /// </summary>
    private void UpdateRandomizeBallPositionsToggleState()
    {
        if (_randomizeBallPositionsToggle == null || _gameSettings == null) return;
        
        bool isTraining = _gameSettings.IsTrainingMode;
        
        // Training modda toggle devre dışı ve işaretli
        _randomizeBallPositionsToggle.interactable = !isTraining;
        
        if (isTraining)
        {
            _randomizeBallPositionsToggle.SetIsOnWithoutNotify(true);
        }
        else
        {
            _randomizeBallPositionsToggle.SetIsOnWithoutNotify(_gameSettings.RandomizeBallPositions);
        }
    }

    private void OnTimeScaleSliderChanged(float value)
    {
        // Slider is integer-stepped; use int value for Time.timeScale
        int intVal = Mathf.RoundToInt(value);
        intVal = Mathf.Clamp(intVal, _minTimeScaleInt, _maxTimeScaleInt);
        Time.timeScale = intVal;
        UpdateTimeScaleText(intVal);
    }

    private void UpdateTimeScaleText(int value)
    {
        if (_timeScaleValueText != null)
        {
            _timeScaleValueText.text =  "Time Scale: " +  value.ToString() + "x";
        }
    }

    private void OnFullscreenClicked()
    {
        // Toggle fullscreen/windowed mode
        Screen.fullScreen = !Screen.fullScreen;
        UpdateFullscreenButtonText();
    }

    private void UpdateFullscreenButtonText()
    {
        if (_fullscreenButtonTMP != null)
        {
            _fullscreenButtonTMP.text = Screen.fullScreen ? "Window Mode" : "Full Screen";
        }
    }
}
