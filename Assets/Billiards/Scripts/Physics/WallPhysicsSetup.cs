using UnityEngine;

/// <summary>
/// Duvarlar (masanın kenarları) için Physics Material
/// Topların duvara çarpınca sekmesini sağlar
/// </summary>
public class WallPhysicsSetup : MonoBehaviour
{
    private float _bounciness = 0.75f; // Dengeli sekme - çok fazla momentum kaybetmesin (0.95 → 0.75)
    private float _dynamicFriction = 0.01f; // Neredeyse sıfır - yansıma açısı bozulmasın (0.1 → 0.01)
    private float _staticFriction = 0.01f;
    private PhysicsMaterialCombine _bounceCombine = PhysicsMaterialCombine.Maximum; // Maximum - minimum bounciness garantisi
    private PhysicsMaterialCombine _frictionCombine = PhysicsMaterialCombine.Minimum; // Minimum - az sürtünme
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;
    
    private PhysicsMaterial _wallPhysicsMaterial;

    private void Awake()
    {
        CreateAndApplyPhysicsMaterial();
    }

    private void CreateAndApplyPhysicsMaterial()
    {
        // Physics Material oluştur
        _wallPhysicsMaterial = new PhysicsMaterial("WallPhysicsMaterial");
        _wallPhysicsMaterial.bounciness = _bounciness;
        _wallPhysicsMaterial.dynamicFriction = _dynamicFriction;
        _wallPhysicsMaterial.staticFriction = _staticFriction;
        _wallPhysicsMaterial.bounceCombine = _bounceCombine;
        _wallPhysicsMaterial.frictionCombine = _frictionCombine;
        
        // Tüm child collider'lara uygula
        Collider[] colliders = GetComponentsInChildren<Collider>();
        
        if (colliders.Length == 0)
        {
            // Parent'ta collider var mı?
            Collider parentCollider = GetComponent<Collider>();
            if (parentCollider != null)
            {
                parentCollider.material = _wallPhysicsMaterial;
                if (_showDebugInfo)
                {
                    Debug.Log($"✅ [{gameObject.name}] Wall Physics Material applied to parent");
                }
            }
            else
            {
                Debug.LogError($"❌ [{gameObject.name}] No Collider found on Wall!");
            }
        }
        else
        {
            foreach (Collider collider in colliders)
            {
                collider.material = _wallPhysicsMaterial;
            }
            
            if (_showDebugInfo)
            {
                Debug.Log($"✅ [{gameObject.name}] Wall Physics Material applied to {colliders.Length} colliders - Bounciness: {_bounciness}");
            }
        }
    }
}
