using UnityEngine;

public class OutOfBoundsDetector : MonoBehaviour
{
    // Her top kendi başlangıç pozisyonunu bildiği için harici referanslara gerek yok.
    // Bu sayede prefab içinde her masa kendi toplarını yönetebilir.
    [Header("Manager References")]
    [Tooltip("Bu masanın GameFlowManager'ı - Faul durumunda bilgilendirilir")]
    [SerializeField] private GameFlowManager _gameFlowManager;
    
    [Tooltip("Bu masanın ScoreManager'ı - Faul kaydı için kullanılır")]
    [SerializeField] private BilliardScoreManager _scoreManager;
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Debug.Log($"[OutOfBoundsDetector] Top sınır dışına çıktı: {other.name}. FAUL!");
            

                // Immediately reset the out-of-bounds ball to its initial position so
                // the scene doesn't keep an off-table ball. Use the ball's ResetPosition
                // so we don't trigger a full table reset here (that is handled by
                // GameFlowManager after balls have stopped).
                var ballComp = other.GetComponent<BilliardBall>();
                if (ballComp != null)
                {
                    // true => sync physics transforms after teleport
                    ballComp.ResetPosition(true);
                    Debug.Log($"[OutOfBoundsDetector] Resetting {other.name} to its spawn position.");
                }

                // ScoreManager'a out of bounds faul bildir
                if (_scoreManager != null)
                {
                    _scoreManager.RegisterOutOfBoundsFoul();
                }
            
            // GameFlowManager'a faul durumunu bildir
            // GameFlowManager kendi masası için reset ve sıra değişimi yapacak
            if (_gameFlowManager != null)
            {
                _gameFlowManager.OnOutOfBoundsFoul();
            }
            else
            {
                Debug.LogError("[OutOfBoundsDetector] GameFlowManager referansı atanmamış! Inspector'dan atayın.");
            }
        }
    }
}
