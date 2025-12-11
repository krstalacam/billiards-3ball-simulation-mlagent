using UnityEngine;

/// <summary>
/// ML-Agents aksiyon değerlerini [-1, 1] aralığından oyun parametrelerine çevirir.
/// Açı ve güç değerlerini normalize eder.
/// </summary>
public class BilliardActionMapper
{
    private readonly Vector2 _angleXLimits;
    private readonly Vector2 _angleYLimits;
    private readonly Vector2 _powerLimits;

    public BilliardActionMapper(Vector2 angleXLimits, Vector2 angleYLimits, Vector2 powerLimits)
    {
        _angleXLimits = angleXLimits;
        _angleYLimits = angleYLimits;
        _powerLimits = powerLimits;
    }

    /// <summary>
    /// ML-Agents aksiyonunu [-1, 1] aralığından belirtilen aralığa çevirir.
    /// </summary>
    public float MapActionToRange(float actionValue, Vector2 range)
    {
        float clamped = Mathf.Clamp(actionValue, -1f, 1f);
        float t = (clamped + 1f) * 0.5f; // [-1,1] -> [0,1]
        return Mathf.Lerp(range.x, range.y, t);
    }

    /// <summary>
    /// Oyun değerini ML-Agents aksiyon değerine [-1, 1] çevirir.
    /// Heuristic modda kullanılabilir.
    /// </summary>
    public float RemapValueToAction(float value, Vector2 range)
    {
        if (Mathf.Approximately(range.y, range.x))
        {
            return 0f;
        }
        float t = Mathf.InverseLerp(range.x, range.y, value);
        return Mathf.Clamp(t * 2f - 1f, -1f, 1f); // [0,1] -> [-1,1]
    }

    /// <summary>
    /// Aksiyon dizisinden oyun parametrelerini çıkarır.
    /// </summary>
    public ShotParameters ExtractShotParameters(Unity.MLAgents.Actuators.ActionBuffers actions)
    {
        var continuousActions = actions.ContinuousActions;
        
        if (continuousActions.Length < 3)
        {
            Debug.LogWarning("[BilliardActionMapper] Expected 3 continuous actions (angleX, angleY, power).");
            return new ShotParameters(0f, 0f, 0f);
        }

        // Log raw model outputs before mapping
        Debug.Log($"[ActionMapper] RAW Model Outputs -> [0]={continuousActions[0]:F6}, [1]={continuousActions[1]:F6}, [2]={continuousActions[2]:F6}");

        float angleX = MapActionToRange(continuousActions[0], _angleXLimits);
        float angleY = MapActionToRange(continuousActions[1], _angleYLimits);
        float power = MapActionToRange(continuousActions[2], _powerLimits);

        return new ShotParameters(angleX, angleY, power);
    }
}

/// <summary>
/// Atış parametrelerini tutan veri yapısı.
/// </summary>
public struct ShotParameters
{
    public float AngleX;
    public float AngleY;
    public float Power;

    public ShotParameters(float angleX, float angleY, float power)
    {
        AngleX = angleX;
        AngleY = angleY;
        Power = power;
    }
}
