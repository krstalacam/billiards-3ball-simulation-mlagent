using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manuel test için UI kontrol scripti
/// Klavye ve UI ile isteka kontrolü sağlar
/// </summary>
public class BilliardTestController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BilliardGameManager _gameManager;
    
    [Header("Control Settings")]
    [SerializeField] private float _angleSpeed = 90f; // Açı değiştirme hızı (derece/saniye)
    [SerializeField] private float _powerSpeed = 5f;  // Güç değiştirme hızı (birim/saniye)
    [Header("Acceleration Settings")]
    [SerializeField] private float _accelerationDelay = 0.3f; // Hızlanmanın başlaması için gerekli süre
    [SerializeField] private float _maxSpeedMultiplier = 3f; // Maksimum hız çarpanı
    [SerializeField] private float _accelerationRate = 2f; // Hızlanma oranı
    private const float _angleXMin = -180f;
    private const float _angleXMax = 180f;
    private const float _angleXRange = _angleXMax - _angleXMin;
    private float _angleYMin = 0f;
    private float _angleYMax = 60f;
    private float _powerMin = 0.1f;
    private float _powerMax = 10f; // Daha düşük maksimum güç
    
    // Tuş basılı tutma süreleri
    private float _leftKeyHoldTime = 0f;
    private float _rightKeyHoldTime = 0f;
    private float _upKeyHoldTime = 0f;
    private float _downKeyHoldTime = 0f;
    private float _qKeyHoldTime = 0f;
    private float _eKeyHoldTime = 0f;
    
    [Header("Current Values")]
    [SerializeField] private float _currentAngleX = 0f;
    [SerializeField] private float _currentAngleY = 60f;
    [SerializeField] private float _currentPower = 3f; 
    
    [Header("UI References (Optional)")]
    [SerializeField] private Text _angleXText;
    [SerializeField] private Text _angleYText;
    [SerializeField] private Text _powerText;

    [SerializeField] private GameFlowManager _flowManager;
    [SerializeField] private BilliardScoreManager _scoreManager;
    private bool _inputLocked = false;
    private CueStick _activeCueStick;

    private void Awake()
    {
        // Auto-find ScoreManager if not assigned
        if (_scoreManager == null)
        {
            if (_scoreManager == null)
            {
                Debug.LogWarning("[BilliardTestController] BilliardScoreManager not found in scene!");
            }
        }

        if (_flowManager == null)
        {
            if (_flowManager == null)
            {
                Debug.LogWarning("[BilliardTestController] GameFlowManager not found - will use fallback score tracking!");
            }
        }
    }


    private void Start()
    {
        // Başlangıçta UI text'lerini beyaz, bold ve net yap
        ApplyUnifiedTextStyle();
    }

    private void Update()
    {
        TryReleaseInputLock();
        if (!_inputLocked)
        {
            HandleKeyboardInput();
            UpdateCuePosition();
        }
        UpdateUI();
    }

    /// <summary>
    /// UI text elementlerine tutarlı stil uygular (beyaz, bold, net)
    /// </summary>
    private void ApplyUnifiedTextStyle()
    {
        Text[] texts = new Text[] { _angleXText, _angleYText, _powerText };

        foreach (var t in texts)
        {
            if (t == null) continue;
            t.color = Color.white;
            t.fontStyle = FontStyle.Bold;
            t.resizeTextForBestFit = false;
            
            var rt = t.GetComponent<RectTransform>();
            if (rt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        // Canvas'ı güncelle
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Klavye girişlerini işler
    /// </summary>
    private void HandleKeyboardInput()
    {
        if (_inputLocked || _activeCueStick == null) return;
        
        // Yatay açı kontrolü (A/D veya Left/Right) - DÖNGÜSEL (wrap around)
        // SAĞ tuş ile açı AZALIR, SOL tuş ile açı ARTAR (düz mantık)
        bool leftPressed = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool rightPressed = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        
        if (leftPressed)
        {
            _leftKeyHoldTime += Time.deltaTime;
            _rightKeyHoldTime = 0f;
            float speedMultiplier = CalculateSpeedMultiplier(_leftKeyHoldTime);
            float newAngleX = _currentAngleX + _angleSpeed * speedMultiplier * Time.deltaTime;
            if (newAngleX > _angleXMax) newAngleX -= _angleXRange;
            if (_activeCueStick.CanUpdateAim(newAngleX, _currentAngleY))
            {
                SetShotParameters(newAngleX, _currentAngleY, _currentPower);
            }
        }
        else if (rightPressed)
        {
            _rightKeyHoldTime += Time.deltaTime;
            _leftKeyHoldTime = 0f;
            float speedMultiplier = CalculateSpeedMultiplier(_rightKeyHoldTime);
            float newAngleX = _currentAngleX - _angleSpeed * speedMultiplier * Time.deltaTime;
            if (newAngleX < _angleXMin) newAngleX += _angleXRange;
            if (_activeCueStick.CanUpdateAim(newAngleX, _currentAngleY))
            {
                SetShotParameters(newAngleX, _currentAngleY, _currentPower);
            }
        }
        else
        {
            _leftKeyHoldTime = 0f;
            _rightKeyHoldTime = 0f;
        }
        
        // Dikey açı kontrolü (W/S veya Up/Down) - SINIRLI (clamp)
        bool upPressed = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool downPressed = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        
        if (upPressed)
        {
            _upKeyHoldTime += Time.deltaTime;
            _downKeyHoldTime = 0f;
            float speedMultiplier = CalculateSpeedMultiplier(_upKeyHoldTime);
            float newAngleY = Mathf.Clamp(_currentAngleY + (_angleSpeed / 2f) * speedMultiplier * Time.deltaTime, _angleYMin, _angleYMax);
            if (_activeCueStick.CanUpdateAim(_currentAngleX, newAngleY))
            {
                SetShotParameters(_currentAngleX, newAngleY, _currentPower);
            }
        }
        else if (downPressed)
        {
            _downKeyHoldTime += Time.deltaTime;
            _upKeyHoldTime = 0f;
            float speedMultiplier = CalculateSpeedMultiplier(_downKeyHoldTime);
            float newAngleY = Mathf.Clamp(_currentAngleY - (_angleSpeed / 2f) * speedMultiplier * Time.deltaTime, _angleYMin, _angleYMax);
            if (_activeCueStick.CanUpdateAim(_currentAngleX, newAngleY))
            {
                SetShotParameters(_currentAngleX, newAngleY, _currentPower);
            }
        }
        else
        {
            _upKeyHoldTime = 0f;
            _downKeyHoldTime = 0f;
        }
        
        // Güç kontrolü (Q/E)
        bool qPressed = Input.GetKey(KeyCode.Q);
        bool ePressed = Input.GetKey(KeyCode.E);
        
        if (qPressed)
        {
            _qKeyHoldTime += Time.deltaTime;
            _eKeyHoldTime = 0f;
            float speedMultiplier = CalculateSpeedMultiplier(_qKeyHoldTime);
            float newPower = Mathf.Clamp(_currentPower - _powerSpeed * speedMultiplier * Time.deltaTime, _powerMin, _powerMax);
            SetShotParameters(_currentAngleX, _currentAngleY, newPower);
        }
        else if (ePressed)
        {
            _eKeyHoldTime += Time.deltaTime;
            _qKeyHoldTime = 0f;
            float speedMultiplier = CalculateSpeedMultiplier(_eKeyHoldTime);
            float newPower = Mathf.Clamp(_currentPower + _powerSpeed * speedMultiplier * Time.deltaTime, _powerMin, _powerMax);
            SetShotParameters(_currentAngleX, _currentAngleY, newPower);
        }
        else
        {
            _qKeyHoldTime = 0f;
            _eKeyHoldTime = 0f;
        }
        
        // Vuruş (Space)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Shoot();
        }
        
        // Reset (R)
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetGame();
        }
    }

    /// <summary>
    /// Tuş basılı tutma süresine göre hız çarpanı hesaplar
    /// </summary>
    private float CalculateSpeedMultiplier(float holdTime)
    {
        if (holdTime < _accelerationDelay)
        {
            // İlk gecikme süresi boyunca normal hız
            return 1f;
        }
        
        // Gecikme sonrası hızlanma
        float accelerationTime = holdTime - _accelerationDelay;
        float multiplier = 1f + (accelerationTime * _accelerationRate);
        
        // Maksimum hızla sınırla
        return Mathf.Min(multiplier, _maxSpeedMultiplier);
    }

    /// <summary>
    /// İstekanın pozisyonunu günceller
    /// </summary>
    private void UpdateCuePosition()
    {
        if (_inputLocked || _activeCueStick == null) return;
        
        if (_gameManager != null)
        {
            // Sadece toplar duruyor ise pozisyon güncelle
            if (!_gameManager.AreBallsMoving())
            {
                _activeCueStick.UpdateCuePosition(_currentAngleX, _currentAngleY, _currentPower);
            }
        }
    }

    /// <summary>
    /// Vuruş yapar
    /// </summary>
    public void Shoot()
    {
        if (_inputLocked)
        {
            Debug.LogWarning("[BilliardTestController] Input is locked, cannot shoot.");
            return;
        }

        if (_activeCueStick != null && _gameManager != null)
        {
            if (!_gameManager.AreBallsMoving())
            {
                _activeCueStick.Shoot(_currentAngleX, _currentAngleY, _currentPower);
                _inputLocked = true; // Lock input after shooting
                
                // FlowManager'a vuruş yapıldığını bildir (BallsMoving state'ine geçmesi için)
                if (_flowManager != null)
                {
                    _flowManager.OnPlayerShot();
                }
            }
            else
            {
                Debug.LogWarning("[BilliardTestController] Cannot shoot while balls are moving.");
            }
        }
    }

    /// <summary>
    /// Oyunu sıfırlar
    /// </summary>
    public void ResetGame()
    {
        if (_gameManager != null)
        {
            _gameManager.ResetGame();
            float defaultY = _activeCueStick != null ? _activeCueStick.DefaultVerticalAngle : 60f;
            SetShotParameters(0f, defaultY, 1.5f); // Daha düşük başlangıç gücü
            _inputLocked = false;
            Debug.Log("[Test] Game reset!");
        }
    }

    // NotifyPlayerShot metodu kaldırıldı - GameFlowManager artık bu işi WaitingForPlayerShot state'inde yapıyor

    private void TryReleaseInputLock()
    {
        if (!_inputLocked) return;
        if (_activeCueStick == null || _gameManager == null)
        {
            _inputLocked = false;
            return;
        }

        // Toplar durduktan ve istekâ animasyonu bittiğinde tekrar girişe izin ver
        if (!_gameManager.AreBallsMoving() && !_activeCueStick.IsShooting)
        {
            _inputLocked = false;
            // Force snap to the corrected angle (Y=60) immediately to avoid getting stuck
            float targetAngleY = _activeCueStick.DefaultVerticalAngle;
            _activeCueStick.ForceAlignWithBall(_currentAngleX, targetAngleY, _currentPower);
            SetShotParameters(_currentAngleX, targetAngleY, _currentPower);
        }
    }

    /// <summary>
    /// Sets the currently controlled cue stick.
    /// </summary>
    /// <param name="cueStick">The cue stick to control.</param>
    public void SetActiveCueStick(CueStick cueStick)
    {
        _activeCueStick = cueStick;
        if (_activeCueStick != null)
        {
            Debug.Log($"[BilliardTestController] Active cue stick set to: {_activeCueStick.Owner}");
        }
    }

    /// <summary>
    /// Locks or unlocks user input for the test controller.
    /// </summary>
    /// <param name="locked">True to lock input, false to unlock.</param>
    public void SetInputLock(bool locked)
    {
        _inputLocked = locked;
    }

    /// <summary>
    /// UI'yi günceller
    /// </summary>
    private void UpdateUI()
    {
        if (_angleXText != null)
        {
            _angleXText.text = $"X: {_currentAngleX:F1}°";
            _angleXText.color = Color.white;
            _angleXText.fontStyle = FontStyle.Bold;
            _angleXText.resizeTextForBestFit = false;
        }
        
        if (_angleYText != null)
        {
            _angleYText.text = $"Y: {_currentAngleY:F1}°";
            _angleYText.color = Color.white;
            _angleYText.fontStyle = FontStyle.Bold;
            _angleYText.resizeTextForBestFit = false;
        }
        
        if (_powerText != null)
        {
            _powerText.text = $"Power: {_currentPower:F1}";
            _powerText.color = Color.white;
            _powerText.fontStyle = FontStyle.Bold;
            _powerText.resizeTextForBestFit = false;
        }
    }

    /// <summary>
    /// Ortak olarak açı ve güç parametrelerini ayarlar.
    /// </summary>
    private void SetShotParameters(float angleX, float angleY, float power)
    {
        _currentAngleX = angleX;
        _currentAngleY = angleY;
        _currentPower = power;
    }

    /// <summary>
    /// Gets the current shot parameters from the test controller.
    /// </summary>
    /// <returns>A tuple containing the current X angle, Y angle, and power.</returns>
    public (float, float, float) GetCurrentShotParameters()
    {
        return (_currentAngleX, _currentAngleY, _currentPower);
    }

    /// <summary>
    /// Ajanın vuruş parametrelerini ayarlar, böylece sıra oyuncuya geçtiğinde isteka doğru pozisyonda olur.
    /// </summary>
    public void SetCurrentShotParameters(float angleX, float angleY, float power)
    {
        SetShotParameters(angleX, angleY, power);
        
        // UI'ı hemen güncelle
        UpdateUI();
        // Isteka pozisyonunu hemen güncelle
        UpdateCuePosition();
    }

    /// <summary>
    /// Ajanın kararını UI'da gösterir.
    /// </summary>
    public void DisplayAction(ShotParameters shotParams)
    {
        // Bu metod, ajanın kararını anlık olarak UI'da göstermek için kullanılabilir.
        // Örneğin, bir text alanına "Agent decided: AngleX=..., Power=..." yazdırılabilir.
        Debug.Log($"[BilliardTestController] Agent Action: AngleX={shotParams.AngleX:F1}, AngleY={shotParams.AngleY:F1}, Power={shotParams.Power:F1}");
    }

    /// <summary>
    /// Ajan tarafından verilen parametrelerle UI'ı günceller.
    /// </summary>
    public void UpdateUIFromShot(ShotParameters shotParams)
    {
        SetShotParameters(shotParams.AngleX, shotParams.AngleY, shotParams.Power);
        UpdateUI();
    }
}
