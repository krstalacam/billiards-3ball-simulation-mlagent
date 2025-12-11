using UnityEngine;

/// <summary>
/// Masanın yüzeyi (yer) için Physics Material
/// Topların masada hareket ederken sürtünmesi için gerekli
/// Duvarlardan daha az zıplayıcı, daha fazla sürtünme
/// </summary>
public class TableSurfacePhysicsSetup : MonoBehaviour
{
    private float _bounciness = 0.1f; // Masada minimal sıçrama
    private float _dynamicFriction = 0.35f; // Daha doğal - yavaşlama kademelik olsun (0.55 → 0.35)
    private float _staticFriction = 0.35f; // Daha doğal
    private PhysicsMaterialCombine _bounceCombine = PhysicsMaterialCombine.Minimum; // Minimum - en az sekme
    private PhysicsMaterialCombine _frictionCombine = PhysicsMaterialCombine.Maximum; // Maximum - sürtünme hem hareketi hem dönmeyi etkilesin
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;
    
    private PhysicsMaterial _surfacePhysicsMaterial;

    private void Awake()
    {
        CreateAndApplyPhysicsMaterial();
    }

    private void CreateAndApplyPhysicsMaterial()
    {
        // Physics Material oluştur
        _surfacePhysicsMaterial = new PhysicsMaterial("TableSurfacePhysicsMaterial");
        _surfacePhysicsMaterial.bounciness = _bounciness;
        _surfacePhysicsMaterial.dynamicFriction = _dynamicFriction;
        _surfacePhysicsMaterial.staticFriction = _staticFriction;
        _surfacePhysicsMaterial.bounceCombine = _bounceCombine;
        _surfacePhysicsMaterial.frictionCombine = _frictionCombine;
        
        // Tüm child collider'lara uygula
        Collider[] colliders = GetComponentsInChildren<Collider>();
        
        if (colliders.Length == 0)
        {
            // Parent'ta collider var mı?
            Collider parentCollider = GetComponent<Collider>();
            if (parentCollider != null)
            {
                parentCollider.material = _surfacePhysicsMaterial;
                if (_showDebugInfo)
                {
                    Debug.Log($"✅ [{gameObject.name}] Table Surface Physics Material applied to parent");
                }
            }
            else
            {
                Debug.LogError($"❌ [{gameObject.name}] No Collider found on Table Surface!");
            }
        }
        else
        {
            foreach (Collider collider in colliders)
            {
                collider.material = _surfacePhysicsMaterial;
            }
            
            if (_showDebugInfo)
            {
                Debug.Log($"✅ [{gameObject.name}] Table Surface Physics Material applied to {colliders.Length} colliders - Bounciness: {_bounciness}, Friction: {_dynamicFriction}");
            }
        }
    }

    /// <summary>
    /// Runtime'da bounciness değiştir
    /// </summary>
    public void SetBounciness(float value)
    {
        _bounciness = Mathf.Clamp01(value);
        if (_surfacePhysicsMaterial != null)
        {
            _surfacePhysicsMaterial.bounciness = _bounciness;
        }
    }

    /// <summary>
    /// Runtime'da sürtünme değiştir
    /// </summary>
    public void SetFriction(float dynamic, float staticFriction)
    {
        _dynamicFriction = Mathf.Clamp01(dynamic);
        _staticFriction = Mathf.Clamp01(staticFriction);
        
        if (_surfacePhysicsMaterial != null)
        {
            _surfacePhysicsMaterial.dynamicFriction = _dynamicFriction;
            _surfacePhysicsMaterial.staticFriction = _staticFriction;
        }
    }
}
