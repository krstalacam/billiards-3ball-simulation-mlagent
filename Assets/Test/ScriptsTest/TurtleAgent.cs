using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// This is the main agent script for the Turtle.
/// It learns how to reach the goal while avoiding walls.
/// It gives visual feedback by flashing the ground:
///     - Green if it reached the goal
///     - Red if it failed
/// </summary>
public class TurtleAgent : Agent
{
    // The goal object that the turtle is trying to reach
    [SerializeField] private Transform _goal;

    // The ground object � we use its material to flash red or green
    [SerializeField] private Renderer _groundRenderer;

    // Movement and rotation speeds for the turtle
    [SerializeField] private float _moveSpeed = 1.5f;
    [SerializeField] private float _rotationSpeed = 180f;

    // Reference to the turtle's Renderer � we use it to change color on wall collision
    private Renderer _renderer;

    // Ground's original color (so we can fade back to it after flashing red or green)
    private Color _defaultGroundColor;

    // Tracks whether the agent reached the goal in the last episode
    private bool _reachedGoalLastEpisode = false;

    // Handle to the coroutine that flashes the ground � we store it so we can stop it early
    private Coroutine _flashGroundCoroutine;

    // Tracks how many episodes have been completed (useful for debugging GUI)
    [HideInInspector] public int _currentEposide = 0;

    // Optional: Shows current reward value (useful for GUI debug info)
    [HideInInspector] public float _cumulativeReward = 0f;

    // Training performance tracking
    private static int _totalEpisodes = 0;
    private static int _successfulEpisodes = 0;
    private static int _recentSuccesses = 0;
    private static int _recentEpisodes = 0;
    private const int EVALUATION_WINDOW = 200; // Son 200 episode'u değerlendir
    private const float SUCCESS_THRESHOLD = 0.98f; // %98 başarı oranı (daha yüksek)
    private const int MIN_EPISODES_FOR_EVALUATION = 2000; // Minimum 2000 episode
    private const int CONSECUTIVE_SUCCESS_REQUIRED = 3; // Üst üste 3 kez başarılı olmalı
    private static int _consecutiveSuccessCount = 0;
    private static bool _trainingComplete = false;

    /// <summary>
    /// Called once when the agent is first initialized
    /// </summary>
    public override void Initialize()
    {
        // Cache the turtle's renderer so we can change its color on wall hit
        _renderer = GetComponent<Renderer>();

        // Store the ground's original color so we can fade back to it
        if (_groundRenderer != null)
        {
            _defaultGroundColor = _groundRenderer.material.color;
        }

        _currentEposide = 0;
        _cumulativeReward = 0f;
    }

    /// <summary>
    /// Eğitimin tamamlanıp tamamlanmadığını kontrol eder
    /// </summary>
    private void CheckTrainingCompletion()
    {
        _totalEpisodes++;
        _recentEpisodes++;

        // Minimum episode sayısına ulaşmadıysak değerlendirme yapma
        if (_totalEpisodes < MIN_EPISODES_FOR_EVALUATION)
        {
            return;
        }

        // Son N episode için başarı oranını hesapla
        if (_recentEpisodes >= EVALUATION_WINDOW)
        {
            float successRate = (float)_recentSuccesses / (float)_recentEpisodes;
            
            Debug.Log($"[Training Monitor] Episode: {_totalEpisodes}, Success Rate (Last {EVALUATION_WINDOW}): {successRate:P2} ({_recentSuccesses}/{_recentEpisodes}), Consecutive: {_consecutiveSuccessCount}/{CONSECUTIVE_SUCCESS_REQUIRED}");

            // Başarı oranı threshold'u geçtiyse sayacı artır
            if (successRate >= SUCCESS_THRESHOLD)
            {
                _consecutiveSuccessCount++;
                Debug.Log($"<color=yellow>⭐ High success rate detected! ({_consecutiveSuccessCount}/{CONSECUTIVE_SUCCESS_REQUIRED})</color>");
                
                // Üst üste yeterli sayıda başarılı olursa eğitimi durdur
                if (_consecutiveSuccessCount >= CONSECUTIVE_SUCCESS_REQUIRED && !_trainingComplete)
                {
                    _trainingComplete = true;
                    Debug.Log($"<color=green>🎉 TRAINING COMPLETE! 🎉</color>");
                    Debug.Log($"<color=green>Success rate {successRate:P2} maintained for {CONSECUTIVE_SUCCESS_REQUIRED} consecutive evaluations!</color>");
                    Debug.Log($"<color=green>Total episodes: {_totalEpisodes}</color>");
                    Debug.Log($"<color=green>Stopping simulation...</color>");

                    // Unity Editor'ü durdur
                    #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                    #else
                    Application.Quit();
                    #endif
                }
            }
            else
            {
                // Başarı oranı düştüyse sayacı sıfırla
                if (_consecutiveSuccessCount > 0)
                {
                    Debug.Log($"<color=red>⚠ Success rate dropped below threshold. Resetting counter.</color>");
                    _consecutiveSuccessCount = 0;
                }
            }

            // Pencereyi sıfırla
            _recentSuccesses = 0;
            _recentEpisodes = 0;
        }
    }

    /// <summary>
    /// Called automatically at the start of each new episode
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Eğitim tamamlandıysa yeni episode başlatma
        if (_trainingComplete)
        {
            return;
        }

        // Flash ground based on whether the goal was reached last episode
        if (_groundRenderer != null)
        {
            // Choose green for success or red for failure
            Color flashColor = _reachedGoalLastEpisode ? Color.green : Color.red;

            // Stop any previous flashing effect
            if (_flashGroundCoroutine != null)
            {
                StopCoroutine(_flashGroundCoroutine);
            }

            // Start the ground flashing for visual feedback
            _flashGroundCoroutine = StartCoroutine(FlashGround(flashColor, 3.0f));
        }

        // Başarı takibi için istatistik güncelle
        if (_reachedGoalLastEpisode)
        {
            _successfulEpisodes++;
            _recentSuccesses++;
        }

        // Eğitim durumunu kontrol et
        CheckTrainingCompletion();

        // Reset this flag for the next episode
        _reachedGoalLastEpisode = false;

        // Reset counters
        _currentEposide++;
        _cumulativeReward = 0f;

        // Reset the turtle's visual color
        _renderer.material.color = Color.blue;

        // Randomize the goal and turtle positions
        SpawnObjects();
    }

    /// <summary>
    /// Coroutine to flash the ground a color and fade it back to original
    /// </summary>
    private IEnumerator FlashGround(Color targetColor, float duration)
    {
        float elapsedTime = 0f;

        // Start with red or green
        _groundRenderer.material.color = targetColor;

        // Gradually fade back to the original ground color over time
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            _groundRenderer.material.color = Color.Lerp(targetColor, _defaultGroundColor, elapsedTime / duration);
            yield return null;
        }
    }

    /// <summary>
    /// Spawns the turtle and goal at new random positions
    /// </summary>
    private void SpawnObjects()
    {
        // Reset the turtle's position and rotation
        transform.localRotation = Quaternion.identity;
        transform.localPosition = new Vector3(0f, 0.15f, 0f);

        // Random direction and distance for the goal
        float randomAngle = Random.Range(0f, 360f);
        Vector3 randomDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
        float randomDistance = Random.Range(1f, 2.5f);
        Vector3 goalPosition = transform.position + randomDirection * randomDistance;

        // Set goal's new position
        _goal.transform.position = new Vector3(goalPosition.x, 0.3f, goalPosition.z);
    }

    /// <summary>
    /// Collects information (observations) that the neural network uses to learn
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        // Normalize all positions to stay between -1 and 1 for better learning

        float goalX = _goal.localPosition.x / 5f;
        float goalZ = _goal.localPosition.z / 5f;

        float agentX = transform.localPosition.x / 5f;
        float agentZ = transform.localPosition.z / 5f;

        float agentRotY = (transform.localRotation.eulerAngles.y / 360f) * 2f - 1f;

        sensor.AddObservation(goalX);
        sensor.AddObservation(goalZ);
        sensor.AddObservation(agentX);
        sensor.AddObservation(agentZ);
        sensor.AddObservation(agentRotY);
    }

    /// <summary>
    /// Allows keyboard control for testing the agent manually
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0; // Default: no action

        if (Input.GetKey(KeyCode.UpArrow)) discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.LeftArrow)) discreteActions[0] = 2;
        else if (Input.GetKey(KeyCode.RightArrow)) discreteActions[0] = 3;
    }

    /// <summary>
    /// Called every time the agent takes an action
    /// </summary>
    public override void OnActionReceived(ActionBuffers actions)
    {
        MoveAgent(actions.DiscreteActions);

        // Small time penalty to encourage quicker completion
        AddReward(-0.2f / MaxStep);

        // Optional: Track cumulative reward for debugging
        _cumulativeReward = GetCumulativeReward();
    }

    /// <summary>
    /// Applies movement or rotation based on neural network's chosen action
    /// </summary>
    private void MoveAgent(ActionSegment<int> act)
    {
        int action = act[0];

        switch (action)
        {
            case 1:
                transform.position += transform.forward * _moveSpeed * Time.deltaTime;
                break;
            case 2:
                transform.Rotate(0f, -_rotationSpeed * Time.deltaTime, 0f);
                break;
            case 3:
                transform.Rotate(0f, _rotationSpeed * Time.deltaTime, 0f);
                break;
        }
    }

    /// <summary>
    /// Called when the agent touches something with a trigger (like the goal)
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Goal"))
        {
            GoalReached();
        }
    }

    /// <summary>
    /// Handles the logic when the goal is reached
    /// </summary>
    private void GoalReached()
    {
        // Give large positive reward for success
        AddReward(1f);

        // Mark that this episode was successful
        _reachedGoalLastEpisode = true;

        // End the current episode
        EndEpisode();
    }

    /// <summary>
    /// Called when the agent bumps into a wall
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Small penalty for hitting walls
            AddReward(-0.05f);

            // Turn turtle red as visual feedback
            if (_renderer != null)
            {
                _renderer.material.color = Color.red;
            }
        }
    }

    /// <summary>
    /// Called while the agent is still in contact with a wall
    /// </summary>
    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Continuous tiny penalty while touching wall
            AddReward(-0.01f * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Called when the agent stops touching a wall
    /// </summary>
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Reset color to blue
            if (_renderer != null)
            {
                _renderer.material.color = Color.blue;
            }
        }
    }
}
