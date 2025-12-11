using UnityEngine;

/// <summary>
/// Bilardo AI agent'ının tüm konfigürasyon ayarlarını tutar.
/// Inspector'dan kolayca düzenlenebilir.
/// </summary>
[CreateAssetMenu(fileName = "BilliardAgentConfig", menuName = "Billiards/Agent Configuration")]
public class BilliardAgentConfig : ScriptableObject
{
    [Header("Action Mapping")]
    [Tooltip("Yatay açı limitleri (örn: -180 ile 180 derece)")]
    public Vector2 angleXLimits = new Vector2(-180f, 180f);

    [Tooltip("Dikey açı limitleri (örn: 0 ile 45 derece)")]
    public Vector2 angleYLimits = new Vector2(0f, 45f);

    [Tooltip("Güç limitleri")]
    public Vector2 powerLimits = new Vector2(6f, 10f);

    [Header("Observation Normalization")]
    [Tooltip("Masa boyutu (X ve Z eksenleri için yarı uzunluklar)")]
    public Vector2 tableExtents = new Vector2(2.25f, 4.45f);

    [Tooltip("Masa merkezi offset'i (Local space'de köşe hesaplamaları için düzeltme)")]
    public Vector3 tableCenterOffset = Vector3.zero;

    [Header("Decision Settings")]
    [Tooltip("Otomatik karar isteme aktif mi?")]
    public bool autoRequestDecisions = true;

    [Tooltip("Kararlar arası minimum süre (saniye)")]
    public float decisionInterval = 0.05f; // bilardo sira tabanli oldugu icin bu ne kadar onemli bilmiyorum.

    [Header("Rewards")]
    [Tooltip("Karar verdiği için anlık ödül (Genelde 0 olmalı)")]
    public float decisionReward = 0f;

    [Tooltip("Atışı başarıyla uyguladığı için ödül (Genelde 0 olmalı)")]
    public float shotExecutionReward = 0f;

    [Header("Gameplay Rewards")]
    [Tooltip("Her bant teması için ödül - Agent'ı banda vurmaya teşvik eder")]
    public float wallHitReward = 0.00f;  // 0.02 -> 0.00 (Farming engellendi. Sadece sayı için araç olmalı.)

    [Tooltip("İlk topa temas ödülü - Herhangi bir topa vurması önemli bir ilerleme")]
    public float firstBallHitReward = 3.0f;  // 1.0 → 3.0 (Cezalardan çok daha yüksek olmalı)

    [Tooltip("İkinci topa temas ödülü - İki topa birden vurması çok iyi bir gelişme")]
    public float secondBallHitReward = 10.0f;  // 2.0 → 10.0 (Büyük hedef)

    [Tooltip("Başarılı sayı ödülü (3 bant + 2 top) - EN BÜYÜK HEDEF")]
    public float successfulScoreReward = 25.0f;  // 5.0 → 25.0 (Jackpot)

    [Tooltip("Atış gücüne bağlı ceza çarpanı. Yüksek güçleri cezalandırmak için kullanılır.")]
    public float powerPenaltyMultiplier = 0f; // bunu kullanmıcaz. 0 kalsın.

    [Tooltip("Faul cezası (Top dışarı çıkması gibi ciddi hatalar) - Ağır ceza gerekli")]
    public float outOfBoundsPenalty = -1.0f;  // -3 → -1.0 (Daha az korkutucu)

    [Tooltip("Başarısız atış cezası (Temas var ama sayı olmadı) - HAFİF CEZA, öğrenmeye izin ver")]
    public float unsuccessfulShotPenalty = -0.1f;  // -0.5 → -0.1 (Denemek neredeyse bedava)

    [Tooltip("Hiçbir şeye değmeme cezası (Boş vuruş) - Orta şiddette ceza")]
    public float noContactPenalty = -0.5f;  // -1.0 -> -0.5 (Cezalar azaltıldı)

    [Tooltip("Sadece duvara değme cezası (Topa değmedi) - Boş vuruşa yakın olmalı ki topa değmeye çalışsın")]
    public float wallOnlyPenalty = -0.3f; // -0.8 -> -0.3 (Cezalar azaltıldı)

    [Tooltip("Engellenmiş atış cezası (Fiziksel olarak imkansız açı seçimi) - Orta ceza")]
    public float blockedShotPenalty = -2f;  // -5 → -2 (imkansız açıları öğrenmeli ama çok ağır olmasın)

    [Header("Corner Avoidance")]
    [Tooltip("Köşe cezası sistemini aktif et (Sadece training modda çalışır)")]
    public bool enableCornerPenalty = true;
    
    [Tooltip("Köşede kalma cezası (Sıkışmayı önlemek için)")]
    public float cornerStayPenalty = -1.0f;
    
    [Tooltip("Top kaç tur üst üste köşede kalırsa ceza/reset uygulansın?")]
    public int maxConsecutiveCornerTurns = 3;

    [Tooltip("Köşe bölgesi yarıçapı (metre cinsi)")]
    public float cornerThreshold = 0.5f;

    [Header("Watchdog Settings")]
    [Tooltip("Play mode'da watchdog timeout süresi (saniye)")]
    public float playModeWatchdogTimeout = 5f;

    [Tooltip("Training mode'da watchdog timeout süresi (saniye) - Ajana öğrenme zamanı vermek için uzun tutulmalı")]
    public float trainingModeWatchdogTimeout = 10f;

    [Tooltip("Bir episode'un sürebileceği maksimum tur sayısı (Sıkışmayı önlemek için)")]
    public int maxTurnsPerEpisode = 50;

    [Header("Heuristic Mode Settings")]
    [Tooltip("Heuristic modda test controller'ı otomatik aç/kapat?")]
    public bool toggleTestControllerForHeuristic = true;

    /// <summary>
    /// Varsayılan config oluşturur.
    /// </summary>
    public static BilliardAgentConfig CreateDefault()
    {
        var config = CreateInstance<BilliardAgentConfig>();
        config.angleXLimits = new Vector2(-180f, 180f);
        config.angleYLimits = new Vector2(0f, 60f);
        config.powerLimits = new Vector2(0f, 10f);
        config.tableExtents = new Vector2(2.25f, 4.45f);
        config.autoRequestDecisions = true;
        config.decisionInterval = 0.05f;
        config.decisionReward = -0.001f;
        config.shotExecutionReward = 0.1f;
        config.blockedShotPenalty = -0.1f;
        config.toggleTestControllerForHeuristic = true;
        return config;
    }

    /// <summary>
    /// Ayarların geçerli olup olmadığını kontrol eder.
    /// </summary>
    public bool ValidateSettings()
    {
        if (tableExtents.x <= 0f || tableExtents.y <= 0f)
        {
            Debug.LogWarning("[BilliardAgentConfig] tableExtents must be greater than 0");
            return false;
        }

        if (decisionInterval < 0f)
        {
            Debug.LogWarning("[BilliardAgentConfig] decisionInterval cannot be negative");
            return false;
        }

        return true;
    }
}
