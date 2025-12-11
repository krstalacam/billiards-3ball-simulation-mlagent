using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bilardo topu için temel script
/// Her top türü (Main, Target, Secondary) için kullanılır
/// </summary>
public class BilliardBall : MonoBehaviour
{
    public enum BallType
    {
        Main,       // Ana top - isteka ile vuracağımız beyaz top
        Target,     // Hedef top - vurulması gereken top
        Secondary   // İkinci top - diğer top
    }

    [Header("Ball Settings")]
    [SerializeField] private BallType _ballType = BallType.Main;
    
    [Header("Physics Settings")]
    private float _mass = 0.17f; // Gerçek bilardo topu kütlesi (kg)
    private float _drag = 0.2f; // Daha doğal yavaşlama (0.015 -> 0.2)
    private float _angularDrag = 0.8f; // Daha doğal dönme yavaşlaması (1.2 → 0.8)
    
    [Header("Movement Detection")]
    private float _linearStopThreshold = 0.005f; // Topların durdu kabul edileceği hız - azaltıldı (0.03 -> 0.005)
    private float _angularStopThreshold = 0.05f; // Dönme için eşik - azaltıldı (0.2 -> 0.05)
    private float _settleConfirmationTime = 0.15f; // Hareket bitişi teyidi için süre - azaltıldı (0.2 → 0.15)
    
    [Header("Wall Bounce Correction")]
    [SerializeField] private bool _enableWallBounceFix = true; // Aktif - hafif müdahale ile duvara yapışmayı engelle
    private float _minWallBounceNormalSpeed = 0.08f; // Çok düşük - minimal müdahale (0.15 → 0.08)
    private float _wallBounceDamping = 0.99f; // Çok az sönümleme - neredeyse hiç etki yok (0.98 → 0.99)
    private float _minVelocityForWallFix = 0.15f; // Daha düşük eşik - sadece gerçekten yavaş toplar (0.25 → 0.15)

    [Header("Debug")]
    private bool _showDebugInfo = true;
    
    private readonly HashSet<Collider> _activeCollisions = new HashSet<Collider>();
    private Rigidbody _rigidbody;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation; // Tracks spawn rotation so resets stay deterministic
    private bool _isMoving = false;
    private PhysicsMaterialSetup _physicsMaterialSetup;
    private float _settleTimer = 0f;
    
    public BallType Type => _ballType;
    public bool IsMoving => _isMoving;
    public Vector3 Velocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;

    // Çarpışma olaylarını dışarıya bildirmek için event
    public event System.Action<BilliardBall, Collision> OnBallCollision;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        // Physics Material Setup ekle
        _physicsMaterialSetup = GetComponent<PhysicsMaterialSetup>();
        if (_physicsMaterialSetup == null)
        {
            _physicsMaterialSetup = gameObject.AddComponent<PhysicsMaterialSetup>();
            Debug.Log($"✅ [{_ballType}] PhysicsMaterialSetup component added automatically");
        }
        
        // Başlangıç pozisyonunu kaydet
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        
        // Rigidbody ayarları
        _rigidbody.mass = _mass;
        _rigidbody.linearDamping = _drag;
        _rigidbody.angularDamping = _angularDrag;
        _rigidbody.useGravity = true;
        _rigidbody.isKinematic = false; // Kinematic OLMAMALI!
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Hızlı çarpışmaları yakala - penetrasyon engelle
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate; // Smooth hareket
        _rigidbody.solverIterations = 20; // Artırıldı - yavaş hızlarda daha doğru sekme (15 → 20)
        _rigidbody.solverVelocityIterations = 10; // Artırıldı - momentum korunumu daha iyi (8 → 10)
        
        // Rotasyon kısıtlamaları YOK - her eksende dönebilir (gerçek bilardo gibi)
        // Angular drag yüksek olduğu için hızla duracak
        
        // Collider kontrolü
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            Debug.LogError($"❌ [{_ballType}] Ball has NO COLLIDER! Add a Sphere Collider!");
        }
        else
        {
            Debug.Log($"✅ [{_ballType}] Collider OK - Type: {collider.GetType().Name}");
        }
        
        if (_showDebugInfo)
        {
            Debug.Log($"✅ [{_ballType}] Ball initialized - Mass: {_mass}, Kinematic: {_rigidbody.isKinematic}, Gravity: {_rigidbody.useGravity}");
        }
    }

    private void Update()
    {
        RefreshMotionState(true);
        
        // Çok yavaş hareket ediyorsa direkt durdur
        ForceStopIfTooSlow();
    }

    /// <summary>
    /// Topu başlangıç pozisyonuna döndürür
    /// </summary>
    public void ResetPosition(bool syncPhysics = true)
    {
        TeleportTo(_initialPosition, _initialRotation, syncPhysics);
    }

    /// <summary>
    /// Başlangıç pozisyonunu günceller
    /// </summary>
    public void SetInitialPosition(Vector3 position, Quaternion? rotation = null)
    {
        Quaternion desiredRotation = rotation ?? transform.rotation;
        _initialPosition = position;
        _initialRotation = desiredRotation;
        TeleportTo(_initialPosition, _initialRotation, true);
    }

    private void TeleportTo(Vector3 position, Quaternion rotation, bool syncPhysics)
    {
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.position = position;
            _rigidbody.rotation = rotation;
            _rigidbody.Sleep();
        }

        transform.SetPositionAndRotation(position, rotation);

        _isMoving = false;
        _settleTimer = 0f;

        if (syncPhysics)
        {
            Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// Topa kuvvet uygular
    /// </summary>
    public void ApplyForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (_rigidbody != null)
        {
            _rigidbody.AddForce(force, mode);
            _isMoving = true;
            _settleTimer = 0f;
        }
    }

    /// <summary>
    /// Top durduğunda true döner
    /// </summary>
    public bool HasStopped()
    {
        RefreshMotionState(false);
        return !_isMoving;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
        {
            _activeCollisions.Add(collision.collider);
        }

        // Çarpışmayı dinleyenlere bildir (ScoreManager vb.)
        OnBallCollision?.Invoke(this, collision);

        // Çarpışma sesleri veya efektler için kullanılabilir
        if (_showDebugInfo)
        {
            Debug.Log($"[{_ballType}] Ball collided with: {collision.gameObject.name}");
        }

        // Tag kontrolü - daha sonra ödül sistemi için kullanılacak
        if (collision.gameObject.CompareTag("Wall"))
        {
            HandleLowSpeedWallBounce(collision);
        }
        // Not: Bu oyun modunda delik yok, sadece 3 top sistemi
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            HandleLowSpeedWallBounce(collision);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider != null)
        {
            _activeCollisions.Remove(collision.collider);
        }
    }

    private void OnDrawGizmos()
    {
        if (_showDebugInfo && Application.isPlaying && _rigidbody != null)
        {
            // Hız vektörünü göster
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + _rigidbody.linearVelocity);
        }
    }

    private void HandleLowSpeedWallBounce(Collision collision)
    {
        if (!_enableWallBounceFix || _rigidbody == null || collision.contactCount == 0)
        {
            return;
        }

        Vector3 velocity = _rigidbody.linearVelocity;
        if (velocity.sqrMagnitude < _minVelocityForWallFix * _minVelocityForWallFix)
        {
            return;
        }

        ContactPoint contact = collision.GetContact(0);
        Vector3 normal = contact.normal.normalized;
        float normalSpeed = Vector3.Dot(velocity, normal);

        float negativeLimit = -_minWallBounceNormalSpeed * 0.75f;
        if (normalSpeed <= negativeLimit || normalSpeed >= _minWallBounceNormalSpeed)
        {
            return;
        }

        float desiredNormalSpeed = -Mathf.Max(Mathf.Abs(normalSpeed), _minWallBounceNormalSpeed);
        Vector3 tangential = velocity - normal * normalSpeed;
        Vector3 newVelocity = tangential + normal * desiredNormalSpeed;

        float originalSpeed = velocity.magnitude;
        if (newVelocity.sqrMagnitude > originalSpeed * originalSpeed)
        {
            newVelocity = newVelocity.normalized * originalSpeed;
        }

        Vector3 finalVelocity = newVelocity * _wallBounceDamping;
        _rigidbody.linearVelocity = finalVelocity;
        _isMoving = true;
        _settleTimer = 0f;

        if (_showDebugInfo)
        {
            float finalNormalSpeed = Vector3.Dot(finalVelocity, normal);
            //Debug.Log($"[{_ballType}] Low-speed wall bounce adjusted (normal {normalSpeed:F3} → {finalNormalSpeed:F3}).");
        }
    }

    private void RefreshMotionState(bool allowTimerAdvance)
    {
        if (_rigidbody == null)
        {
            _isMoving = false;
            return;
        }

        if (IsBodyMoving(_rigidbody, _linearStopThreshold, _angularStopThreshold))
        {
            _settleTimer = 0f;
            _isMoving = true;
            return;
        }

        if (allowTimerAdvance)
        {
            _settleTimer += Time.deltaTime;
            if (_settleTimer >= _settleConfirmationTime)
            {
                _isMoving = false;
            }
            else
            {
                _isMoving = true;
            }
        }
        else
        {
            _isMoving = _settleTimer < _settleConfirmationTime;
        }

        if (!_isMoving && _rigidbody.angularVelocity.sqrMagnitude > 0f)
        {
            // Hafif dönme kalmışsa sönümlendir
            _rigidbody.angularVelocity = Vector3.Lerp(_rigidbody.angularVelocity, Vector3.zero, Time.deltaTime * 10f);
        }
    }

    public static bool IsBodyMoving(Rigidbody body, float linearThreshold, float angularThreshold)
    {
        if (body == null) return false;
        float linearThresholdSq = linearThreshold * linearThreshold;
        float angularThresholdSq = angularThreshold * angularThreshold;
        return body.linearVelocity.sqrMagnitude > linearThresholdSq || body.angularVelocity.sqrMagnitude > angularThresholdSq;
    }

    /// <summary>
    /// Çok yavaş hareket ediyorsa topu direkt durdur
    /// </summary>
    private void ForceStopIfTooSlow()
    {
        if (_rigidbody == null || !_isMoving) return;
        
        // Çok düşük hız eşikleri - buz pistinde kayma efektini engelle
        float forceStopLinearThreshold = 0.01f; // Sadece gerçekten çok yavaşken durdur (0.04 -> 0.01)
        float forceStopAngularThreshold = 0.1f; // Dönme için de düşük eşik (0.3 -> 0.1)
        
        float linearSpeed = _rigidbody.linearVelocity.magnitude;
        float angularSpeed = _rigidbody.angularVelocity.magnitude;
        
        // Hem linear hem angular çok düşükse direkt durdur
        if (linearSpeed < forceStopLinearThreshold && angularSpeed < forceStopAngularThreshold)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _isMoving = false;
            _settleTimer = _settleConfirmationTime; // Teyit süresini tamamla
            
            if (_showDebugInfo)
            {
                Debug.Log($"[{_ballType}] Force stopped - Linear: {linearSpeed:F4}, Angular: {angularSpeed:F4}");
            }
        }
    }

    public bool TryGetContactWithTags(out Collider collider, params string[] tags)
    {
        collider = null;
        if (_activeCollisions.Count == 0 || tags == null || tags.Length == 0)
        {
            return false;
        }

        foreach (var active in _activeCollisions)
        {
            if (active == null)
            {
                continue;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                if (active.CompareTag(tags[i]))
                {
                    collider = active;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the ball is touching any object with the given tags AND if the given direction points towards it.
    /// Used to validate if a shot is aimed at a wall the ball is touching.
    /// </summary>
    public bool IsTouchingWallInDirection(Vector3 direction, params string[] tags)
    {
        if (_activeCollisions.Count == 0 || tags == null || tags.Length == 0) return false;

        Vector3 checkDir = direction.normalized;
        
        foreach (var col in _activeCollisions)
        {
            if (col == null) continue;
            
            bool tagMatch = false;
            for(int i=0; i<tags.Length; i++) {
                if (col.CompareTag(tags[i])) { tagMatch = true; break; }
            }
            
            if (tagMatch)
            {
                // Raycast logic to check if we are aiming at this wall
                // 1. Move origin back slightly to ensure we are outside the collider (in case of overlap)
                // 2. Align Y to collider center to simulate "infinite height" (ignore vertical miss)
                
                Vector3 rayOrigin = transform.position - (checkDir * 0.05f);
                rayOrigin.y = col.bounds.center.y;
                
                Ray ray = new Ray(rayOrigin, checkDir);
                
                if (col.Raycast(ray, out _, 10f))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
