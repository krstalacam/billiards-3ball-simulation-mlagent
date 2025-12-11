using UnityEngine;

/// <summary>
/// İsteka kontrolü için script
/// Manuel ve AI kontrolü için destek sağlar
/// </summary>
public class CueStick : MonoBehaviour
{
    public enum CueOwner
    {
        Player, // Oyuncu istekası
        Agent   // AI agent istekası
    }

    [Header("Owner")]
    [SerializeField] private CueOwner _owner = CueOwner.Player;

    [Header("References")]
    [SerializeField] private Transform _cueTip;
    [SerializeField] private BilliardBall _targetBall;
    [SerializeField] private BilliardGameManager _gameManager;

    public CueOwner Owner => _owner;
    public BilliardBall TargetBall => _targetBall;

    [Header("Position Settings")]
    [SerializeField] private float _minDistanceFromBall = 0.125f;
    [SerializeField] private float _defaultVerticalAngle = 60f;
    [SerializeField] private Vector2 _aimOffsetRatio = Vector2.zero;
    [SerializeField] private float _maxAimOffsetRatio = 0.8f;

    public float DefaultVerticalAngle => _defaultVerticalAngle;

    [Header("Shot Settings")]
    [SerializeField] private float _maxPower = 100f;
    [SerializeField] private float _maxPullbackDistance = 1.5f;
    [SerializeField] private float _forceMultiplier = 0.4f;

    [Header("Physics")]
    [SerializeField] private float _cueSpeed = 15f;

    [Header("Animation Settings")]
    [SerializeField] private bool _enableAnimation = true;
    [SerializeField] private float _upwardHeight = 0.5f;
    [SerializeField] private float _upwardDuration = 0.3f;

    [Header("Smooth Movement Settings")]
    [SerializeField] private float _positionSmoothSpeed = 8f;
    [SerializeField] private float _rotationSmoothSpeed = 10f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;
    [SerializeField] private bool _showTrajectoryLine = true;

    [Header("Runtime Debug")]
    [Tooltip("Show aiming ray in the Game view / build. Uses a LineRenderer so it works at runtime when enabled.")]
    [SerializeField] private bool _showAimInGame = false;
    [SerializeField] private Color _aimBlockedColor = Color.red;
    [SerializeField] private float _aimLineWidth = 0.02f;

    [Header("Raycast Settings")]
    [SerializeField] private LayerMask _collisionLayers;
    [SerializeField] private float _rayLength = 5f;

    // Runtime line renderer used to show aim in Game view / builds when enabled
    private LineRenderer _aimLineRenderer;
    // Decision line renderer to show each agent decision (red) briefly
    private LineRenderer _decisionLineRenderer;
    [SerializeField] private float _decisionLineDuration = 1.2f;

    private Rigidbody _rigidbody;
    private bool _isShoting = false;
    private float _currentPower = 2f;
    private bool _hasAppliedHit = false;
    private bool _waitingForAllBallsStop = false;
    private bool _ballsStoppedSignalReceived = false;
    private bool _frozenAfterShot = false; // Vuruş sonrası topu takip etmeyi durdur

    private float _currentAngleX = 0f;
    private float _shotAngleX = 0f;
    private float _shotAngleY = 60f;

    private Quaternion _targetRotation;
    private bool _hasTargetSet = false;

    public bool IsShooting => _isShoting;
    public bool IsWaitingForBalls => _waitingForAllBallsStop; // Fix for deadlock
    public Vector2 AimOffset => _aimOffsetRatio;

    /// <summary>
    /// Bir sonraki atış için parametreleri ayarlar
    /// </summary>
    public void SetNextShotParameters(ShotParameters shotParams)
    {
        UpdateAim(shotParams.AngleX, shotParams.AngleY);
    }

    /// <summary>
    /// İstekanın görsel hedefini günceller
    /// </summary>
    public void UpdateAim(float angleX, float angleY)
    {
        if (_isShoting || _targetBall == null) return;
        
        _currentAngleX = angleX;
    }

    /// <summary>
    /// Vuruş ofsetini ayarlar (-1 ila 1 arası)
    /// </summary>
    public void SetAimOffset(Vector2 offset)
    {
        _aimOffsetRatio.x = Mathf.Clamp(offset.x, -_maxAimOffsetRatio, _maxAimOffsetRatio);
        _aimOffsetRatio.y = Mathf.Clamp(offset.y, -_maxAimOffsetRatio, _maxAimOffsetRatio);
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = true;
    }

    private void Start()
    {
        if (_targetBall == null)
        {
            Debug.LogError($"[CueStick-{_owner}] Target ball atanmadı! Inspector'dan atayın.", this);
            return;
        }

        ValidateBallRigidbody();
        ValidateCueTip();

        // Create runtime aim line renderer if requested
        if (_showAimInGame)
        {
            EnsureAimLine();
        }
    }

    private void EnsureAimLine()
    {
        if (!_showAimInGame)
        {
            if (_aimLineRenderer != null)
            {
                Destroy(_aimLineRenderer.gameObject);
                _aimLineRenderer = null;
            }
            return;
        }

        if (_aimLineRenderer == null)
        {
            GameObject go = new GameObject("AimLineRenderer");
            go.transform.SetParent(transform, false);
            _aimLineRenderer = go.AddComponent<LineRenderer>();
            _aimLineRenderer.positionCount = 2;
            _aimLineRenderer.useWorldSpace = true;
            _aimLineRenderer.widthMultiplier = Mathf.Max(0.0001f, _aimLineWidth);
            var mat = new Material(Shader.Find("Sprites/Default"));
            _aimLineRenderer.material = mat;
            _aimLineRenderer.numCapVertices = 2;
            _aimLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aimLineRenderer.receiveShadows = false;
            _aimLineRenderer.enabled = true;
        }
    }

    private void EnsureDecisionLine()
    {
        if (_decisionLineRenderer == null)
        {
            GameObject go = new GameObject("DecisionLineRenderer");
            go.transform.SetParent(transform, false);
            _decisionLineRenderer = go.AddComponent<LineRenderer>();
            _decisionLineRenderer.positionCount = 2;
            _decisionLineRenderer.useWorldSpace = true;
            _decisionLineRenderer.widthMultiplier = Mathf.Max(0.0001f, _aimLineWidth);
            var mat = new Material(Shader.Find("Sprites/Default"));
            _decisionLineRenderer.material = mat;
            _decisionLineRenderer.startColor = _aimBlockedColor;
            _decisionLineRenderer.endColor = _aimBlockedColor;
            _decisionLineRenderer.numCapVertices = 2;
            _decisionLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _decisionLineRenderer.receiveShadows = false;
            _decisionLineRenderer.enabled = false;
        }
    }

    private void UpdateDecisionLine(Vector3 start, Vector3 end)
    {
        EnsureDecisionLine();
        if (_decisionLineRenderer == null) return;
        _decisionLineRenderer.enabled = true;
        _decisionLineRenderer.SetPosition(0, start);
        _decisionLineRenderer.SetPosition(1, end);
        
        // ShowAimInGame false ise alpha 0, true ise tam görünür
        Color lineColor = _aimBlockedColor;
        lineColor.a = _showAimInGame ? 1f : 0f;
        
        _decisionLineRenderer.startColor = lineColor;
        _decisionLineRenderer.endColor = lineColor;
        _decisionLineRenderer.widthMultiplier = Mathf.Max(0.0001f, _aimLineWidth);
    }

    private void ClearDecisionLine()
    {
        if (_decisionLineRenderer != null)
        {
            _decisionLineRenderer.enabled = false;
        }
    }

    private System.Collections.IEnumerator DecisionLineRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }
        ClearDecisionLine();
    }

    /// <summary>
    /// Shows the agent's decision as a red line for a short duration.
    /// </summary>
    public void ShowDecisionLine(float angleX, float angleY, float duration = -1f)
    {
        if (_targetBall == null) return;
        if (duration <= 0f) duration = _decisionLineDuration;

        Quaternion rot = Quaternion.Euler(angleY, angleX, 0f);
        Vector3 forwardDir = rot * Vector3.forward;
        Vector3 tipPosition = _targetBall.transform.position - forwardDir * _minDistanceFromBall;
        Vector3 endPosition = tipPosition - forwardDir * _rayLength;

        UpdateDecisionLine(tipPosition, endPosition);
        StopCoroutine("DecisionLineRoutine");
        StartCoroutine(DecisionLineRoutine(duration));
    }

    private void UpdateAimLine(Vector3 start, Vector3 end, Color col)
    {
        if (_aimLineRenderer == null) return;
        _aimLineRenderer.enabled = true;
        _aimLineRenderer.SetPosition(0, start);
        _aimLineRenderer.SetPosition(1, end);
        _aimLineRenderer.startColor = col;
        _aimLineRenderer.endColor = col;
        _aimLineRenderer.widthMultiplier = Mathf.Max(0.0001f, _aimLineWidth);
    }

    private void ClearAimLine()
    {
        if (_aimLineRenderer != null)
        {
            _aimLineRenderer.enabled = false;
        }
    }

    private void ValidateBallRigidbody()
    {
        Rigidbody ballRb = _targetBall.GetComponent<Rigidbody>();
        if (ballRb == null)
        {
            Debug.LogError($"❌ CueStick-{_owner}: Target ball ({_targetBall.Type}) has NO RIGIDBODY!");
        }
        else if (ballRb.isKinematic)
        {
            Debug.LogError($"❌ CueStick-{_owner}: Target ball ({_targetBall.Type}) Rigidbody is KINEMATIC!");
        }
        else
        {
            Debug.Log($"✅ CueStick-{_owner}: Target ball ({_targetBall.Type}) Rigidbody OK (Mass: {ballRb.mass})");
        }
    }

    private void ValidateCueTip()
    {
        if (_cueTip == null)
        {
            _cueTip = transform.Find("CueTip");
            if (_cueTip == null)
            {
                Debug.LogWarning("⚠ CueStick: Cue tip not found. Using parent transform.");
                _cueTip = transform;
                return;
            }
        }

        Collider tipCollider = _cueTip.GetComponent<Collider>();
        if (tipCollider == null)
        {
            Debug.LogError("❌ CueStick: CueTip has NO COLLIDER!");
        }
        else if (!tipCollider.isTrigger)
        {
            Debug.LogError("❌ CueStick: CueTip Collider is NOT a trigger!");
        }
        else
        {
            Debug.Log("✅ CueStick: CueTip collider OK");
        }
    }

    private void Update()
    {
        // Vuruş sırasında veya vuruş sonrası frozen durumdaysa topu takip etme
        if (_isShoting || _frozenAfterShot || !_hasTargetSet) return;
        
        // Toplar hareket ediyorsa isteka hareketsiz kalmalı
        if (_gameManager != null && _gameManager.AreBallsMoving())
        {
            return;
        }

        // Hedef rotasyonu hesapla
        float rotT = 1f - Mathf.Exp(-_rotationSmoothSpeed * Time.deltaTime);
        Quaternion nextRotation = Quaternion.Slerp(transform.rotation, _targetRotation, rotT);

        // Engel kontrolü - engel varsa dönme
        if (!CheckIfBlocked(nextRotation))
        {
            transform.rotation = nextRotation;
        }

        // Pozisyon sabitlemesi - her zaman toptan minimum mesafede
        Vector3 ballPos = _targetBall.transform.position;
        Vector3 tipOffset = GetTipOffset();
        Vector3 fixedPosition = ballPos - (transform.forward * _minDistanceFromBall) - tipOffset;

        float posT = 1f - Mathf.Exp(-_positionSmoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, fixedPosition, posT);
    }

    /// <summary>
    /// İstekanın pozisyonunu ve rotasyonunu günceller
    /// </summary>
    public void UpdatePositionAndRotation(float angleX, float angleY)
    {
        if (_isShoting || _targetBall == null) return;
        
        UpdateCuePosition(angleX, angleY, _currentPower);
    }

    /// <summary>
    /// İstekanın pozisyonunu ve rotasyonunu ayarlar
    /// </summary>
    public void UpdateCuePosition(float angleX, float angleY, float power)
    {
        if (_targetBall == null) return;

        // Vuruş sırasında açı sabit kalmalı
        if (_isShoting)
        {
            ApplyCuePose(_shotAngleX, _shotAngleY, power);
            return;
        }

        // Normal durumda açıyı güncelle ve smooth hareketi başlat
        ApplyCuePose(angleX, angleY, power);
    }

    /// <summary>
    /// Reset sonrasında isteka animasyonunu iptal ederek topa hizalar
    /// </summary>
    public void ForceAlignWithBall(float? angleXOverride = null, float? angleYOverride = null, float? powerOverride = null)
    {
        if (_targetBall == null) return;

        StopAllCoroutines();
        _isShoting = false;
        _waitingForAllBallsStop = false;
        _hasAppliedHit = false;
        _frozenAfterShot = false;

        float targetAngleX = angleXOverride ?? _currentAngleX;
        float targetAngleY = angleYOverride ?? _defaultVerticalAngle;
        float targetPower = powerOverride ?? _currentPower;

        ApplyCuePoseImmediate(targetAngleX, targetAngleY, targetPower);
    }

    /// <summary>
    /// Smooth geçiş olmadan anında pozisyon ayarla
    /// </summary>
    private void ApplyCuePoseImmediate(float angleX, float angleY, float power)
    {
        _currentAngleX = angleX;
        _currentPower = Mathf.Clamp(power, 0f, _maxPower);

        Vector3 ballCenter = _targetBall.transform.position;
        Quaternion angleRotation = Quaternion.Euler(angleY, angleX, 0f);
        Vector3 direction = (angleRotation * Vector3.forward).normalized;

        Quaternion cueRotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 hitPointOnBall = GetHitPointOnBall(ballCenter, cueRotation);
        Vector3 tipLocalOffset = GetTipLocalOffset();

        Vector3 tipRestPosition = hitPointOnBall - direction * _minDistanceFromBall;
        Vector3 cueRestPosition = tipRestPosition - (cueRotation * tipLocalOffset);

        transform.position = cueRestPosition;
        transform.rotation = cueRotation;
    }

    private void ApplyCuePose(float angleX, float angleY, float power)
    {
        _currentAngleX = angleX;
        _currentPower = Mathf.Clamp(power, 0f, _maxPower);

        Vector3 ballCenter = _targetBall.transform.position;
        Quaternion angleRotation = Quaternion.Euler(angleY, angleX, 0f);
        Vector3 direction = (angleRotation * Vector3.forward).normalized;

        Quaternion cueRotation = Quaternion.LookRotation(direction, Vector3.up);

        _targetRotation = cueRotation;
        _hasTargetSet = true;

        if (_showDebugInfo)
        {
            Debug.DrawLine(ballCenter, transform.position, Color.yellow);
            Debug.DrawLine(ballCenter, ballCenter + direction * 2f, Color.red);
            DrawCueTipDebug(ballCenter);
        }
    }

    private void DrawCueTipDebug(Vector3 ballCenter)
    {
        if (_cueTip != null)
        {
            Debug.DrawLine(_cueTip.position, ballCenter, Color.green);
            Debug.DrawLine(_cueTip.position + Vector3.up * 0.1f, _cueTip.position - Vector3.up * 0.1f, Color.cyan);
            Debug.DrawLine(_cueTip.position + Vector3.left * 0.1f, _cueTip.position + Vector3.right * 0.1f, Color.cyan);
        }
    }

    /// <summary>
    /// Vuruş yapar
    /// </summary>
    public bool Shoot(float angleX, float angleY, float power)
    {
        if (_isShoting)
        {
            Debug.LogWarning($"[CueStick-{_owner}] Cannot shoot - already shooting!");
            return false;
        }
        
        if (_targetBall == null)
        {
            Debug.LogError($"[CueStick-{_owner}] Cannot shoot - no target ball!");
            return false;
        }

        Debug.Log($"[CueStick-{_owner}] Shoot called - AngleX:{angleX:F2}, AngleY:{angleY:F2}, Power:{power:F2}");

        // FIX: Force align the cue stick to the decided angles immediately before shooting.
        // This ensures the shot is executed with the current decision, not the previous one.
        ApplyCuePoseImmediate(angleX, angleY, power);

        _shotAngleX = angleX;
        _shotAngleY = angleY;
        // _currentAngleX and _currentPower are updated in ApplyCuePoseImmediate
        _hasAppliedHit = false;
        _isShoting = true; // FIX: Set flag immediately
        Debug.Log($"[CueStick-{_owner}] _isShoting flag set to TRUE.");

        StartCoroutine(ExecuteShot(power));
        return true;
    }

    private System.Collections.IEnumerator ExecuteShot(float power)
    {
        // _isShoting is already set to true in Shoot()
        _hasAppliedHit = false;
        _waitingForAllBallsStop = false;
        _ballsStoppedSignalReceived = false;

        Quaternion shotRotation = transform.rotation;
        Vector3 restPosition = transform.position;
        Vector3 direction = (shotRotation * Vector3.forward).normalized;

        float pullbackDistance = Mathf.Lerp(0.2f, _maxPullbackDistance, power / _maxPower);
        Vector3 pullbackPosition = restPosition + (-direction * pullbackDistance);

        if (_showDebugInfo)
        {
            Debug.Log($"[CueStick] Starting shot - Power: {power:F2}, Pullback: {pullbackDistance:F2}");
        }

        // ADIM 1: Geri çekil
        yield return MoveToPosition(restPosition, pullbackPosition, shotRotation, 0.3f);
        yield return new WaitForSeconds(0.1f);

        // ADIM 2: İleri vur
        Vector3 contactPosition = CalculateContactPosition(direction, shotRotation);
        float strikeDistance = Vector3.Distance(pullbackPosition, contactPosition);
        float strikeDuration = strikeDistance / _cueSpeed;
        
        yield return MoveToPosition(pullbackPosition, contactPosition, shotRotation, strikeDuration);
        yield return new WaitForSeconds(0.05f);

        // ADIM 3-5: Animasyon
        if (_enableAnimation)
        {
            yield return AnimateUpwardAndWait(contactPosition, shotRotation);
            ApplyCuePoseImmediate(_currentAngleX, _shotAngleY, _currentPower);
        }
        else
        {
            yield return MoveToPosition(contactPosition, restPosition, shotRotation, 0.4f);
        }

        _isShoting = false;
        _waitingForAllBallsStop = false;
        _frozenAfterShot = true; // Toplar duruncaya kadar pozisyonu sabitle

        if (_showDebugInfo)
        {
            Debug.Log($"[CueStick-{_owner}] Shot complete! Cue frozen until balls stop.");
        }
    }

    private System.Collections.IEnumerator MoveToPosition(Vector3 from, Vector3 to, Quaternion rotation, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.position = Vector3.Lerp(from, to, t);
            transform.rotation = rotation;
            yield return null;
        }
        transform.position = to;
        transform.rotation = rotation;
    }

    private System.Collections.IEnumerator AnimateUpwardAndWait(Vector3 contactPosition, Quaternion shotRotation)
    {
        Vector3 upPosition = contactPosition + Vector3.up * _upwardHeight;
        yield return MoveToPosition(contactPosition, upPosition, shotRotation, _upwardDuration);

        if (!_ballsStoppedSignalReceived)
        {
            _waitingForAllBallsStop = true;
            while (_waitingForAllBallsStop)
            {
                yield return null;
            }
        }
    }

    private Vector3 CalculateContactPosition(Vector3 direction, Quaternion shotRotation)
    {
        Vector3 ballCenter = _targetBall.transform.position;
        float ballRadius = GetBallRadius();
        Vector3 tipLocalOffset = GetTipLocalOffset();
        
        Vector3 tipContactPosition = ballCenter - direction * ballRadius;
        return tipContactPosition - (shotRotation * tipLocalOffset);
    }

    private Vector3 GetHitPointOnBall(Vector3 ballCenter, Quaternion cueRotation)
    {
        Vector3 right = cueRotation * Vector3.right;
        Vector3 up = cueRotation * Vector3.up;
        float ballRadius = GetBallRadius();
        Vector3 offset = right * _aimOffsetRatio.x * ballRadius + up * _aimOffsetRatio.y * ballRadius;
        return ballCenter + offset;
    }

    private float GetBallRadius()
    {
        SphereCollider ballCollider = _targetBall.GetComponent<SphereCollider>();
        return ballCollider != null ? ballCollider.radius * _targetBall.transform.lossyScale.x : 0.05f;
    }

    private Vector3 GetTipLocalOffset()
    {
        return (_cueTip != null && _cueTip != transform) ? _cueTip.localPosition : Vector3.zero;
    }

    private Vector3 GetTipOffset()
    {
        Vector3 tipOffset = GetTipLocalOffset();
        return transform.rotation * tipOffset;
    }

    public void NotifyAllBallsStopped()
    {
        _ballsStoppedSignalReceived = true;
        if (_waitingForAllBallsStop)
        {
            _waitingForAllBallsStop = false;
        }
    }

    /// <summary>
    /// Toplar durduğunda çağrılır - freeze modunu kaldırır
    /// </summary>
    public void OnBallsStopped()
    {
        if (_frozenAfterShot)
        {
            _frozenAfterShot = false;
            if (_showDebugInfo)
            {
                Debug.Log($"[CueStick-{_owner}] Balls stopped - unfreezing cue stick.");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_showDebugInfo)
        {
            Debug.Log($"[CueStick-{_owner}] OnTriggerEnter with {other.name}, _isShoting:{_isShoting}, _hasAppliedHit:{_hasAppliedHit}");
        }
        
        if (!_isShoting || _hasAppliedHit) return;

        BilliardBall ball = other.GetComponent<BilliardBall>();
        if (ball != null && ball == _targetBall)
        {
            Vector3 hitDirection = transform.forward.normalized;
            float force = _currentPower * _forceMultiplier;

            ball.ApplyForce(hitDirection * force, ForceMode.Impulse);
            _hasAppliedHit = true;

            if (_showDebugInfo)
            {
                Debug.Log($"[CueStick] ✅ HIT! Power: {_currentPower:F2}, Force: {force:F2}");
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (_showTrajectoryLine && _targetBall != null && !_isShoting)
        {
            Vector3 ballPos = _targetBall.transform.position;
            Vector3 direction = transform.forward;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(ballPos, ballPos + direction * 3f);

            Gizmos.color = Color.Lerp(Color.green, Color.red, _currentPower / _maxPower);
            Gizmos.DrawWireSphere(transform.position, 0.1f);

            DrawCapsuleGizmo();
        }
    }

    private void DrawCapsuleGizmo()
    {
        float stickThickness = 0.04f;
        Vector3 forwardDir = transform.forward;
        Vector3 tipPosition = _cueTip != null ? _cueTip.position : transform.position;
        Vector3 endPosition = tipPosition - forwardDir * _rayLength;

        bool isBlocked = CheckIfBlocked(transform.rotation);
        Gizmos.color = isBlocked ? Color.red : Color.magenta;

        Gizmos.DrawWireSphere(tipPosition, stickThickness);
        Gizmos.DrawWireSphere(endPosition, stickThickness);
        Gizmos.DrawLine(tipPosition + transform.up * stickThickness, endPosition + transform.up * stickThickness);
        Gizmos.DrawLine(tipPosition - transform.up * stickThickness, endPosition - transform.up * stickThickness);
        Gizmos.DrawLine(tipPosition + transform.right * stickThickness, endPosition + transform.right * stickThickness);
        Gizmos.DrawLine(tipPosition - transform.right * stickThickness, endPosition - transform.right * stickThickness);
    }

    public void AlignToShotParameters(float angleX, float angleY)
    {
        transform.rotation = Quaternion.Euler(angleY, angleX, 0);
        Debug.Log($"[CueStick] Aligned to angles: AngleX = {angleX}, AngleY = {angleY}");
    }

    public void AlignToShotParameters(float angleX, float angleY, float power)
    {
        _currentAngleX = angleX;
        _currentPower = power;
        Debug.Log($"[CueStick] Aligned to shot parameters: AngleX = {angleX}, AngleY = {angleY}, Power = {power}");
    }

    public void MakeShot()
    {
        Debug.Log($"[CueStick] Making shot with parameters: AngleX = {_currentAngleX}, Power = {_currentPower}");
        Shoot(_currentAngleX, _defaultVerticalAngle, _currentPower);
    }

    public bool CanUpdateAim(float angleX, float angleY)
    {
        Quaternion potentialRotation = Quaternion.Euler(angleY, angleX, 0f);
        return !CheckIfBlocked(potentialRotation);
    }

    /// <summary>
    /// Public accessor for whether runtime aim visualization is enabled.
    /// </summary>
    public bool ShowAimInGame => _showAimInGame;

    /// <summary>
    /// Enable/disable runtime aim visualization. Only Agent-owned cue sticks can enable the aim.
    /// If this cue stick is not owned by an Agent, the value will be forced to false.
    /// </summary>
    public void SetShowAimInGame(bool show)
    {
        if (Owner != CueOwner.Agent)
        {
            show = false;
        }

        _showAimInGame = show;
        
        // Decision line'ın alpha değerini güncelle
        if (_decisionLineRenderer != null)
        {
            Color lineColor = _decisionLineRenderer.startColor;
            lineColor.a = _showAimInGame ? 1f : 0f;
            _decisionLineRenderer.startColor = lineColor;
            _decisionLineRenderer.endColor = lineColor;
        }
    }

    private bool CheckIfBlocked(Quaternion checkRotation)
    {
        if (_targetBall == null) return false;

        float stickThickness = 0.04f;
        float checkDistance = _rayLength;

        Vector3 forwardDir = checkRotation * Vector3.forward;
        Vector3 tipPosition = _targetBall.transform.position - forwardDir * _minDistanceFromBall;
        Vector3 endPosition = tipPosition - forwardDir * checkDistance;

        Collider[] colliders = Physics.OverlapCapsule(tipPosition, endPosition, stickThickness, _collisionLayers);

        foreach (var hitCollider in colliders)
        {
            if (hitCollider.gameObject == _targetBall.gameObject) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;

            if (_showDebugInfo) Debug.DrawLine(tipPosition, endPosition, Color.red, 0.1f);
            return true;
        }

        if (_showDebugInfo) Debug.DrawLine(tipPosition, endPosition, Color.green, 0.1f);
        return false;
    }

    public bool IsBlockedForAngles(float angleX, float angleY)
    {
        Quaternion checkRotation = Quaternion.Euler(angleY, angleX, 0f);
        return CheckIfBlocked(checkRotation);
    }
}