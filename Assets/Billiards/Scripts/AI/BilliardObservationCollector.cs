using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Bilardo oyunu için gözlem verilerini toplar ve normalleştirir.
/// Top pozisyonlarını ML-Agents için uygun formata çevirir.
/// </summary>
public class BilliardObservationCollector
{
    private readonly Vector2 _tableExtents;
    private string _lastObservationSnapshot = "<no-observation>";

    public string LastObservationSnapshot => _lastObservationSnapshot;

    public BilliardObservationCollector(Vector2 tableExtents)
    {
        _tableExtents = new Vector2(
            Mathf.Max(tableExtents.x, 0.001f),
            Mathf.Max(tableExtents.y, 0.001f)
        );
    }

    /// <summary>
    /// Tüm gözlemleri toplar ve sensöre ekler.
    /// Gözlem sırası her zaman tutarlıdır: [Kontrol Edilen Top, Hedef Top, Diğer Top]
    /// </summary>
    public void CollectObservations(VectorSensor sensor, BilliardAIEnvironment environment)
    {
        if (environment == null)
        {
            AddEmptyObservations(sensor);
            _lastObservationSnapshot = "Environment=null";
            return;
        }

        BilliardBall controlledBall = environment.GetAgentControlledBall();
        BilliardBall mainBall = environment.MainBall;
        BilliardBall targetBall = environment.TargetBall;
        BilliardBall secondaryBall = environment.SecondaryBall;

        // Convert world positions into environment-local coordinates (centered on environment).
        // This avoids huge world offsets (e.g. scene origin far from table) saturating observations.
        Vector3 envOrigin = environment != null ? environment.transform.position : Vector3.zero;

        Vector3 controlledPos = GetBallPosition(controlledBall) - envOrigin;
        Vector3 targetPos;
        Vector3 otherPos;

        // Diğer iki topu belirle ve tutarlı bir sırada ekle.
        // Bu, gözlem uzayının her zaman aynı anlama gelmesini sağlar.
        if (controlledBall == mainBall)
        {
            // Vurduğu top MainBall ise, diğerleri Target ve Secondary'dir.
            targetPos = GetBallPosition(targetBall) - envOrigin;
            otherPos = GetBallPosition(secondaryBall) - envOrigin;
        }
        else // controlledBall'un secondaryBall olduğu varsayılır.
        {
            // Vurduğu top SecondaryBall ise, diğerleri Target ve Main'dir.
            targetPos = GetBallPosition(targetBall) - envOrigin;
            otherPos = GetBallPosition(mainBall) - envOrigin;
        }

        // 1. Gözlem: Her zaman ajanın kontrol ettiği (vuracağı) top.
        // Normalize using the configured table extents (assumed to be half-sizes).
        AddBallPosition(sensor, controlledPos);
        AddBallPosition(sensor, targetPos);
        AddBallPosition(sensor, otherPos);

        // Store a normalized snapshot to help debugging (shows values fed to the model)
        _lastObservationSnapshot =
            $"Controlled={FormatVectorNormalized(controlledPos)} | Target={FormatVectorNormalized(targetPos)} | Other={FormatVectorNormalized(otherPos)}";
    }

    /// <summary>
    /// Bir topun 2D pozisyonunu normalleştirilmiş şekilde ekler (x, z).
    /// </summary>
    private void AddBallPosition(VectorSensor sensor, Vector3 position)
    {
        sensor.AddObservation(Mathf.Clamp(position.x / _tableExtents.x, -1f, 1f)); // Normalized x
        sensor.AddObservation(Mathf.Clamp(position.z / _tableExtents.y, -1f, 1f)); // Normalized z
    }

    private static Vector3 GetBallPosition(BilliardBall ball)
    {
        return ball != null ? ball.transform.position : Vector3.zero;
    }

    private string FormatVectorNormalized(Vector3 localPos)
    {
        // Normalize relative to table extents for concise debugging
        float nx = Mathf.Clamp(localPos.x / _tableExtents.x, -1f, 1f);
        float nz = Mathf.Clamp(localPos.z / _tableExtents.y, -1f, 1f);
        return $"({nx:F3}, {nz:F3})";
    }

    /// <summary>
    /// Environment yoksa boş gözlemler ekler.
    /// </summary>
    private void AddEmptyObservations(VectorSensor sensor)
    {
        for (int i = 0; i < 6; i++)
        {
            sensor.AddObservation(0f);
        }
    }

    /// <summary>
    /// Toplam gözlem sayısını döner (3 top x 2 koordinat = 6).
    /// </summary>
    public static int GetObservationSize() => 6;
}
