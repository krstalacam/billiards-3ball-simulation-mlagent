using UnityEngine;

/// <summary>
/// Toplar için Physics Material oluşturur ve uygular
/// Sekmeli (bouncy) çarpışmalar için gerekli
/// </summary>
public class PhysicsMaterialSetup : MonoBehaviour
{
    [Header("Material Settings")]
    [SerializeField] private float _bounciness = 0.85f; // Artırıldı - momentum korunsun (0.75 → 0.85)
    [SerializeField] private float _dynamicFriction = 0.05f; // Daha da azaltıldı - açı korunsun (0.1 → 0.05)
    [SerializeField] private float _staticFriction = 0.05f; // Daha da azaltıldı
    [SerializeField] private PhysicsMaterialCombine _bounceCombine = PhysicsMaterialCombine.Maximum; // Maximum - minimum bounciness garantisi
    [SerializeField] private PhysicsMaterialCombine _frictionCombine = PhysicsMaterialCombine.Minimum; // Minimum - gerçek bilardo gibi
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;
    
    private PhysicsMaterial _ballPhysicsMaterial;

    private void Awake()
    {
        CreateAndApplyPhysicsMaterial();
    }

    /// <summary>
    /// Physics Material oluşturur ve collider'a uygular
    /// </summary>
    private void CreateAndApplyPhysicsMaterial()
    {
        // Physics Material oluştur
        _ballPhysicsMaterial = new PhysicsMaterial("BallPhysicsMaterial");
        _ballPhysicsMaterial.bounciness = _bounciness;
        _ballPhysicsMaterial.dynamicFriction = _dynamicFriction;
        _ballPhysicsMaterial.staticFriction = _staticFriction;
        _ballPhysicsMaterial.bounceCombine = _bounceCombine;
        _ballPhysicsMaterial.frictionCombine = _frictionCombine;
        
        // Collider'a uygula
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.material = _ballPhysicsMaterial;
            
            if (_showDebugInfo)
            {
                Debug.Log($"✅ [{gameObject.name}] Physics Material applied - Bounciness: {_bounciness}, Friction: {_dynamicFriction}");
            }
        }
        else
        {
            Debug.LogError($"❌ [{gameObject.name}] No Collider found! Cannot apply Physics Material!");
        }
    }

    /// <summary>
    /// Runtime'da bounciness değiştir
    /// </summary>
    public void SetBounciness(float value)
    {
        _bounciness = Mathf.Clamp01(value);
        if (_ballPhysicsMaterial != null)
        {
            _ballPhysicsMaterial.bounciness = _bounciness;
        }
    }

    /// <summary>
    /// Runtime'da sürtünme değiştir
    /// </summary>
    public void SetFriction(float dynamic, float staticFriction)
    {
        _dynamicFriction = Mathf.Clamp01(dynamic);
        _staticFriction = Mathf.Clamp01(staticFriction);
        
        if (_ballPhysicsMaterial != null)
        {
            _ballPhysicsMaterial.dynamicFriction = _dynamicFriction;
            _ballPhysicsMaterial.staticFriction = _staticFriction;
        }
    }
}
