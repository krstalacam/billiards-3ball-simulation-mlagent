using UnityEngine;

/// <summary>
/// BilliardScoreManager'dan gelen olayları dinler ve BilliardAgent'a ödül/ceza verir.
/// Şekillendirilmiş ödül (Shaped Reward) mantığını uygular.
/// </summary>
public class BilliardRewardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BilliardAgent _agent;
    [SerializeField] private BilliardScoreManager _scoreManager;
    [SerializeField] private BilliardAgentConfig _config;

  
    // Tur içi durum takibi
    private int _lastWallCount = 0;
    private int _lastBallCount = 0;
    private bool _turnActive = false;
    private bool _scoreWasCommitted = false; // Sayı yapıldı mı?
    private bool _outOfBoundsPenaltyApplied = false; // Track immediate OOB penalty to avoid double-apply
    private int _consecutiveCornerTurns = 0; // Köşede kalma sayacı

    [Header("Settings")]
    [SerializeField] private GameSettings _gameSettings;

    private void Awake()
    {
        if (_agent == null) _agent = GetComponent<BilliardAgent>();
        
        // GameSettings'i bulmaya çalış
        if (_gameSettings == null)
        {
            var settingsAssets = Resources.FindObjectsOfTypeAll<GameSettings>();
            if (settingsAssets.Length > 0)
            {
                _gameSettings = settingsAssets[0];
            }
            else
            {
                // Fallback: Try to find checking if GameManager has it, or just log warning
                Debug.LogWarning("[BilliardRewardManager] GameSettings not assigned and not found in resources. Corner check might fail to detect training mode.");
            }
        }

        // ScoreManager ve Config Inspector'dan atanmalı - atanmadıysa hata ver
        if (_scoreManager == null)
        {
            // Fallback: Try to get from Agent's Environment if available
            if (_agent != null && _agent.Environment != null)
            {
                _scoreManager = _agent.Environment.ScoreManager;
                if (_scoreManager != null)
                {
                    Debug.Log("[BilliardRewardManager] Auto-resolved ScoreManager from Environment.");
                }
            }

            if (_scoreManager == null)
            {
                Debug.LogError("[BilliardRewardManager] BilliardScoreManager is NOT assigned in Inspector! Please assign it.", this);
            }
        }
        
        // Config agent üzerinde varsa oradan al, yoksa inspector'dan bekle
        if (_config == null && _agent != null)
        {
            _config = _agent.Config; // Agent'ın config'ine erişim
        }

        if (_config == null)
        {
            Debug.LogError("[BilliardRewardManager] Config is missing! Please assign it in the inspector or ensure the agent has a config.", this);
        }
        
        if (_agent == null)
        {
            Debug.LogError("[BilliardRewardManager] BilliardAgent not found! Reward system will not work.", this);
        }
    }

    // ... (Other methods remain unchanged until IsTrainingMode)

    private bool IsTrainingMode()
    {
        // Öncelik GameSettings'te
        if (_gameSettings != null)
        {
            return _gameSettings.IsTrainingMode;
        }

        // Fallback: Environment üzerinden kontrol (Eski yöntem)
        return _agent != null &&
               _agent.Environment != null &&
               _agent.Environment.CurrentTurn == BilliardAIEnvironment.TurnState.None;
    }

    private void OnEnable()
    {
        if (_scoreManager != null)
        {
            _scoreManager.OnScoreUpdate += HandleScoreUpdate;
            _scoreManager.OnScoreCommitted += OnScoreScored;
            _scoreManager.OnOutOfBoundsRegistered += HandleOutOfBoundsRegistered;
        }
    }

    private void OnDisable()
    {
        if (_scoreManager != null)
        {
            _scoreManager.OnScoreUpdate -= HandleScoreUpdate;
            _scoreManager.OnScoreCommitted -= OnScoreScored;
            _scoreManager.OnOutOfBoundsRegistered -= HandleOutOfBoundsRegistered;
        }
    }

    /// <summary>
    /// Agent yeni bir atışa başladığında çağrılmalı.
    /// </summary>
    public void OnTurnStarted()
    {
        _lastWallCount = 0;
        _lastBallCount = 0;
        _turnActive = true;
        _scoreWasCommitted = false;
        Debug.Log("[Reward] ========== NEW TURN STARTED ==========");
    }

    /// <summary>
    /// Engellenmiş atış durumunda çağrılmalı (fiziksel olarak imkansız açı seçildi).
    /// </summary>
    public void OnBlockedShot()
    {
        if (_agent != null && _config != null)
        {
            float penalty = _config.blockedShotPenalty;
            AddReward(penalty);
            Debug.Log($"[Reward] 🚫 BLOCKED SHOT (Invalid Action)! | Penalty: {penalty} | Total Reward: {GetTotalReward()}");
        }

        CompleteEpisode("BlockedShot");
        _turnActive = false;
    }

    /// <summary>
    /// ScoreManager reported an out-of-bounds instantly. Apply penalty and end episode now.
    /// </summary>
    private void HandleOutOfBoundsRegistered()
    {
        if (!_turnActive || _agent == null || _config == null) return;

        // Apply immediate penalty so the agent receives negative feedback right away,
        // but DO NOT end the ML-Agents episode here. Ending the episode causes
        // OnEpisodeBegin/EndEpisode side-effects that make the agent request new
        // decisions while physics callbacks are still being processed and leads
        // to incorrect reward/state sequencing.
        float penalty = _config.outOfBoundsPenalty;
        AddReward(penalty);
        _outOfBoundsPenaltyApplied = true;
        Debug.Log($"[Reward] ❌ OUT OF BOUNDS (Immediate)! Penalty: {penalty} | Total Reward: {GetTotalReward()} (episode kept open)");

        // Keep _turnActive true here so OnTurnEnded still runs its normal checks when
        // balls stop. OnTurnEnded will see HasOutOfBoundsFoul() and will NOT double-apply
        // the penalty because of the _outOfBoundsPenaltyApplied flag.
    }

    /// <summary>
    /// Tur bittiğinde (toplar durduğunda) çağrılmalı.
    /// </summary>
    public void OnTurnEnded()
    {
        Debug.Log($"[Reward] ========== TURN ENDED (Walls: {_lastWallCount}, Balls: {_lastBallCount}) ==========");
        if (!_turnActive)
        {
            Debug.Log("[Reward] Turn already inactive, skipping.");
            return;
        }

        // Eğer skor zaten commit edildiyse (OnScoreScored çağrıldı), 
        // burada tekrar episode bitirmeye gerek yok
        if (_scoreWasCommitted)
        {
            Debug.Log("[Reward] Turn ended but score was already committed. Checking game win condition.");
            _turnActive = false;
            
            // Check if game is won
            if (_scoreManager.IsGameWon(out int winner))
            {
                 Debug.Log($"[Reward] 🏆 Game Won by Player {winner}! | Total Reward: {GetTotalReward()}");
            }
            else
            {
                 // Game continues
                 Debug.Log($"[Reward] Score made, game continuing. | Total Reward: {GetTotalReward()}");
            }

            // Köşe kontrolünü başarılı skor durumunda da yap
            CheckCornerStuckState();
            
            CompleteEpisode("PostCommitTurnEnd");
            return;
        }

        bool successAchieved = _scoreManager.IsTurnActive && !_scoreManager.HasFoul() && _scoreManager.CheckScoreCondition();
        Debug.Log($"[Reward] Checking success: TurnActive={_scoreManager.IsTurnActive}, HasFoul={_scoreManager.HasFoul()}, ScoreCondition={_scoreManager.CheckScoreCondition()}");
        if (successAchieved)
        {
            Debug.Log("[Reward] SUCCESS ACHIEVED! Handling successful turn.");
            // Köşe kontrolünü başarılı tur durumunda da yap
            CheckCornerStuckState();
            HandleSuccessfulTurn();
            return;
        }

        // Sadece top dışarı çıktığında veya oyun bittiğinde episode'u bitir.
        // Diğer durumlarda oyun devam etmeli.
        if (_scoreManager.HasOutOfBoundsFoul())
        {
            // If we already applied the immediate penalty when the foul was detected,
            // don't apply it again here. Otherwise, apply it now.
            if (!_outOfBoundsPenaltyApplied)
            {
                float penalty = _config.outOfBoundsPenalty;
                AddReward(penalty);
                Debug.Log($"[Reward] ❌ OUT OF BOUNDS! Penalty: {penalty} | Total Reward: {GetTotalReward()}");
            }
            else
            {
                Debug.Log($"[Reward] OUT OF BOUNDS previously applied. Skipping duplicate penalty. Total Reward: {GetTotalReward()}");
            }
        }
        // Diğer fauller
        else if (_scoreManager.HasFoul())
        {
            float penalty = _config.unsuccessfulShotPenalty;
            AddReward(penalty);
            Debug.Log($"[Reward] ⚠️ FOUL! Penalty: {penalty} | Total Reward: {GetTotalReward()}");
        }
        // 3. Hiçbir şeye değmeme (Boş vuruş)
        else if (_lastWallCount == 0 && _lastBallCount == 0)
        {
            float penalty = _config.noContactPenalty;
            AddReward(penalty);
            Debug.Log($"[Reward] ⚠️ NO CONTACT (Boş Vuruş)! Penalty: {penalty} | Total Reward: {GetTotalReward()}");
        }
        // 4. Başarısız atış (temas var ama sayı yok)
        else
        {
            float penalty = _config.unsuccessfulShotPenalty;
            AddReward(penalty);
            Debug.Log($"[Reward] ⚠️ UNSUCCESSFUL SHOT (Walls: {_lastWallCount}, Balls: {_lastBallCount}) | Penalty: {penalty} | Total Reward: {GetTotalReward()}");
        }

        // Her tur sonunda köşe kontrolü yap
        CheckCornerStuckState();

        _turnActive = false;
        // Clear OOB flag for next turn
        _outOfBoundsPenaltyApplied = false;
        Debug.Log($"[Reward] ✅ Turn completed. | Total Reward: {GetTotalReward()}");

        CompleteEpisode("TurnEndedGeneric");
    }

    /// <summary>
    /// ScoreManager'dan gelen güncellemeleri işler.
    /// </summary>
    private void HandleScoreUpdate(int wallCount, int ballCount, int p1Score, int p2Score)
    {
        if (!_turnActive || _agent == null || _config == null || _scoreManager == null) return;

        // 1. Bant Teması Ödülü (Incremental)
        if (wallCount > _lastWallCount)
        {
            int diff = wallCount - _lastWallCount;

            // Only reward up to 3 wall-hits per turn to avoid jitter/exploit behavior.
            const int maxRewardedWallHits = 3;

            if (_lastWallCount >= maxRewardedWallHits)
            {
                // Already reached cap; update local counter but give no further reward.
                _lastWallCount = wallCount;
            }
            else
            {
                int allowed = Mathf.Max(0, maxRewardedWallHits - _lastWallCount);
                int rewardedHits = Mathf.Min(diff, allowed);
                float reward = rewardedHits * _config.wallHitReward;

                if (rewardedHits > 0f && reward != 0f)
                {
                    AddReward(reward);
                    Debug.Log($"[Reward] 🎯 WALL HIT +{rewardedHits} (Total: {wallCount}) | Reward: +{reward} | Total Reward: {GetTotalReward()}");
                }

                // Update last wall count to reflect current state regardless of reward given.
                _lastWallCount = wallCount;
            }
        }

        // 2. Top Teması Ödülü (Incremental)
        if (ballCount > _lastBallCount)
        {
            // Hangi topa vurduğunu (1. mi 2. mi) anlamak için sayıya bakıyoruz
            float reward = 0f;
            string ballName = "";
            
            if (ballCount == 1)
            {
                reward = _config.firstBallHitReward;
                ballName = "FIRST BALL";
            }
            else if (ballCount == 2)
            {
                reward = _config.secondBallHitReward;
                ballName = "SECOND BALL";
            }

            AddReward(reward);
            Debug.Log($"[Reward] ⚽ {ballName} HIT! | Reward: +{reward} | Total Reward: {GetTotalReward()}");
            _lastBallCount = ballCount;
        }

        // 3. Başarılı Sayı Ödülü
        // ScoreManager sayı olduğunda CommitScore yapar ve puan artar.
        // Ancak biz burada anlık olayları takip ediyoruz.
        // Eğer sayı alma şartları oluştuysa (3 bant + 2 top), ScoreManager bunu zaten yönetir.
        // Biz burada sadece "Sayı oldu mu?" kontrolünü yapamayız çünkü ScoreManager puanı tur sonunda işliyor olabilir.
        // Ancak ScoreManager'da "CommitScore" olduğunda puan artışı olur.
        // Puan artışını kontrol etmek için önceki puanları saklamamız gerekirdi ama
        // ScoreManager yapısı gereği, sayı olduğunda CommitScore çağrılır ve tur biter.
        // Biz burada basitçe şunu kontrol edebiliriz:
        // Eğer bu güncelleme bir "Sayı" sonucu geldiyse (bunu parametrelerden anlamak zor olabilir),
        // Alternatif: ScoreManager sayı olduğunda özel bir event fırlatabilir veya biz şartları kontrol ederiz.
        
        // Şimdilik basit mantık: Eğer şartlar sağlandıysa büyük ödülü ver.
        // Not: ScoreManager.CheckScoreCondition() public ise kullanabiliriz ama o anlık durumu verir.
    }

    /// <summary>
    /// ScoreManager sayı kaydettiğinde çağrılır (Bunu BilliardGameManager veya ScoreManager tetiklemeli).
    /// </summary>
    public void OnScoreScored()
    {
        if (_agent != null && _config != null && _turnActive)
        {
            _scoreWasCommitted = true; // Skorun commit edildiğini işaretle
            float reward = _config.successfulScoreReward;
            AddReward(reward);
            Debug.Log($"[Reward] ✅✅✅ SUCCESSFUL SCORE! ✅✅✅ | Reward: +{reward} | Total Reward: {GetTotalReward()}");
            
            // Episode sonlandırması OnTurnEnded içinde ele alınıyor
        }
    }

    private void AddReward(float value)
    {
        if (_agent != null)
        {
            _agent.AddReward(value);
        }
    }

    private void HandleSuccessfulTurn()
    {
        bool committed = false;
        if (IsTrainingMode() && _scoreManager != null && _scoreManager.IsTurnActive)
        {
            // Agent is always treated as player index 1 during training.
            _scoreManager.CommitScore(1);
            committed = true;
            Debug.Log("[Reward] Score committed in training mode.");
        }

        if (!committed)
        {
            _scoreWasCommitted = true;
            float reward = _config.successfulScoreReward;
            AddReward(reward);
            Debug.Log($"[Reward] ✅ SUCCESS! | Reward: +{reward} | Total Reward: {GetTotalReward()}");
        }

        _turnActive = false;
        
        // Check if game is won
        if (_scoreManager.IsGameWon(out int winner))
        {
             Debug.Log($"[Reward] 🏆 GAME WON by Player {winner}! | Total Reward: {GetTotalReward()}");
        }
        else
        {
             Debug.Log($"[Reward] ✅ Successful turn, game continuing. | Total Reward: {GetTotalReward()}");
        }

        CompleteEpisode("SuccessfulTurn");
    }

    private void CompleteEpisode(string sourceLabel)
    {
        Debug.Log($"[Reward] 🔴 EPISODE ENDED - Rewards processed | Final Reward: {GetTotalReward()}");
        if (_agent == null) return;

        Debug.Log($"[RewardManager] {sourceLabel} Calling EndEpisode()");
        _agent.EndEpisode();
    }

    private void CheckCornerStuckState()
    {
        // Config veya Environment yoksa işlem yapma
        if (_agent == null || _agent.Environment == null || _config == null) 
        {
            Debug.Log("[Reward] CheckCornerStuckState: Missing agent, environment or config - skipping.");
            return;
        }

        // Köşe cezası sistemi kapalıysa çık
        if (!_config.enableCornerPenalty) 
        {
            Debug.Log("[Reward] CheckCornerStuckState: Corner penalty system is DISABLED in config.");
            return;
        }

        // Sadece training modunda bu kontrolü yap (Play modda oyuncuyu resetlemek istemeyiz)
        if (!IsTrainingMode()) 
        {
            Debug.Log("[Reward] CheckCornerStuckState: NOT in training mode - skipping corner check.");
            // Play mode'dayken sayacı sıfırla
            if (_consecutiveCornerTurns > 0)
            {
                Debug.Log($"[Reward] Resetting corner counter (was {_consecutiveCornerTurns}) because not in training mode.");
                _consecutiveCornerTurns = 0;
            }
            return;
        }

        var ball = _agent.Environment.GetAgentControlledBall();
        if (ball == null) 
        {
            Debug.Log("[Reward] CheckCornerStuckState: Agent controlled ball is NULL - skipping.");
            return;
        }

        // DOĞRU POZİSYON HESAPLAMA:
        // Topun world pozisyonunu alıp, Environment (Masa) referans sistemine çeviriyoruz.
        // Bu, topun hiyerarşideki yerinden bağımsız olarak masaya göre tam konumunu verir.
        Vector3 ballWorldPos = ball.transform.position;
        Vector3 tableLocalPos = _agent.Environment.transform.InverseTransformPoint(ballWorldPos);

        // Varsa pivot offset düzeltmesi (pivot tam merkezde değilse)
        Vector3 pos = tableLocalPos - _config.tableCenterOffset;
        
        // Masa boyutları (yarı uzunluklar)
        float xMax = _config.tableExtents.x;
        float zMax = _config.tableExtents.y;

        // Topun mutlak koordinatları (simetri olduğu için)
        float xAbs = Mathf.Abs(pos.x);
        float zAbs = Mathf.Abs(pos.z);

        // En yakın köşeye olan mesafe
        float distToCorner = Vector2.Distance(new Vector2(xAbs, zAbs), new Vector2(xMax, zMax));

        // Detaylı Debug Log: Hangi değerlerin kullanıldığını tam olarak görelim
        Debug.Log($"[Reward] 🔍 DETAILED CHECK:\n" +
                  $"Ball World: {ballWorldPos}\n" +
                  $"Table Local: {pos} (xAbs={xAbs:F2}, zAbs={zAbs:F2})\n" +
                  $"Table Max: ({xMax:F2}, {zMax:F2})\n" +
                  $"DistToCorner: {distToCorner:F2} (Threshold: {_config.cornerThreshold:F2})\n" +
                  $"Counter: {_consecutiveCornerTurns}/{_config.maxConsecutiveCornerTurns}");

        if (distToCorner < _config.cornerThreshold)
        {
             _consecutiveCornerTurns++;
             Debug.Log($"[Reward] 🔴 Ball IS IN CORNER! Counter INCREASED: {_consecutiveCornerTurns}/{_config.maxConsecutiveCornerTurns} | Dist: {distToCorner:F2} | [TRAINING MODE]");
             
             // Köşede kalma cezası
             AddReward(_config.cornerStayPenalty);
             Debug.Log($"[Reward] Applied corner penalty: {_config.cornerStayPenalty} | Total Reward: {GetTotalReward()}");
             
             if (_consecutiveCornerTurns >= _config.maxConsecutiveCornerTurns)
             {
                 Debug.Log($"[Reward] 🛑 STUCK IN CORNER LIMIT REACHED ({_consecutiveCornerTurns}/{_config.maxConsecutiveCornerTurns}) -> Forcing Reset! [TRAINING MODE]");
                 
                 // Environment reset iste
                 _agent.Environment.RequestEnvironmentReset();
                 
                 _consecutiveCornerTurns = 0;
             }
        }
        else
        {
            // Köşeden çıktıysa sayacı sıfırla
            if (_consecutiveCornerTurns > 0)
            {
                Debug.Log($"[Reward] ✅ Ball LEFT corner. Counter RESET from {_consecutiveCornerTurns} to 0. (Dist: {distToCorner:F2})");
                _consecutiveCornerTurns = 0;
            }
            else
            {
                Debug.Log($"[Reward] ✅ Ball NOT in corner. Counter remains 0. (Dist: {distToCorner:F2})");
            }
        }
    }



    private float GetTotalReward()
    {
        return _agent != null ? _agent.GetCumulativeReward() : 0f;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Köşe bölgelerini Scene view'da görselleştirir.
    /// </summary>
    private void OnDrawGizmos()
    {
        // Script aktif değilse veya gerekli referanslar yoksa çizim yapma
        if (!isActiveAndEnabled || _config == null || _agent == null || _agent.Environment == null) return;

        // Masa boyutları
        float xMax = _config.tableExtents.x;
        float zMax = _config.tableExtents.y;
        float cornerRadius = _config.cornerThreshold;

        // Masanın 4 köşesi (local space'de + offset)
        Vector3[] corners = new Vector3[]
        {
            new Vector3(xMax, 0, zMax) + _config.tableCenterOffset,      // Sağ üst
            new Vector3(-xMax, 0, zMax) + _config.tableCenterOffset,     // Sol üst
            new Vector3(xMax, 0, -zMax) + _config.tableCenterOffset,     // Sağ alt
            new Vector3(-xMax, 0, -zMax) + _config.tableCenterOffset     // Sol alt
        };

        // Köşe bölgelerini kırmızı renkle çiz
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Kırmızı, yarı saydam
        
        Transform tableTransform = _agent.Environment.transform;
        
        foreach (Vector3 corner in corners)
        {
            Vector3 worldPos = tableTransform.TransformPoint(corner);
            Gizmos.DrawSphere(worldPos, cornerRadius);
            
            // Köşe sınırını daha net göstermek için wire sphere
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Gizmos.DrawWireSphere(worldPos, cornerRadius);
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        }

        // Eğer top köşedeyse, topun pozisyonunu sarı renkle vurgula
        var ball = _agent.Environment.GetAgentControlledBall();
        if (ball != null)
        {
            // DOĞRU POZİSYON HESAPLAMA (CheckCornerStuckState ile birebir aynı)
            Vector3 ballWorldPos = ball.transform.position;
            Vector3 tableLocalPos = tableTransform.InverseTransformPoint(ballWorldPos);
            Vector3 pos = tableLocalPos - _config.tableCenterOffset;
            
            float xAbs = Mathf.Abs(pos.x);
            float zAbs = Mathf.Abs(pos.z);
            float distToCorner = Vector2.Distance(new Vector2(xAbs, zAbs), new Vector2(xMax, zMax));

            if (distToCorner < cornerRadius)
            {
                // Top köşede - sarı renkle göster
                Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
                Gizmos.DrawSphere(ballWorldPos, 0.1f);
                
                // Köşeye olan mesafeyi ve detaylı bilgileri göster
                // Objeyi tanıyalım (Clone mu, asıl mı?) ve Training modunu görelim.
                string statusInfo = $"[{gameObject.name}] {(IsTrainingMode() ? "TRAIN" : "PLAY")}\n" +
                                    $"CORNER: {_consecutiveCornerTurns}/{_config.maxConsecutiveCornerTurns}\n" +
                                    $"Dist: {distToCorner:F2}m";
                
                // Birden fazla ajan varsa yazılar üst üste binmesin diye objenin ID'sine göre ofset veriyoruz
                // Basit bir hash/mod mantığı ile dikey pozisyonu kaydır
                float dynamicHeightOffset = 0.4f + (Mathf.Abs(gameObject.GetInstanceID()) % 5) * 0.25f;
                                    
                UnityEditor.Handles.Label(ballWorldPos + Vector3.up * dynamicHeightOffset, statusInfo);
            }
            else
            {
                // Top köşede değil - yeşil renkle göster
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                Gizmos.DrawSphere(ballWorldPos, 0.08f);
            }
        }

        // Masa sınırlarını mavi çizgilerle göster (referans için)
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
        Vector3[] tableBounds = new Vector3[]
        {
            tableTransform.TransformPoint(new Vector3(xMax, 0, zMax) + _config.tableCenterOffset),
            tableTransform.TransformPoint(new Vector3(-xMax, 0, zMax) + _config.tableCenterOffset),
            tableTransform.TransformPoint(new Vector3(-xMax, 0, -zMax) + _config.tableCenterOffset),
            tableTransform.TransformPoint(new Vector3(xMax, 0, -zMax) + _config.tableCenterOffset)
        };
        
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(tableBounds[i], tableBounds[(i + 1) % 4]);
        }
    }
#endif
}
