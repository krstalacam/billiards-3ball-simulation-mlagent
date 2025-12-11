using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bilardo skor ve durum bilgilerini ekranda gösterir.
/// BilliardScoreManager'dan gelen eventleri dinler.
/// </summary>
public class BilliardUIManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private BilliardScoreManager _scoreManager;

    [Header("UI Elements")]
    [SerializeField] private Text _wallHitText; // Örn: "Bant: 0/3"
    [SerializeField] private Text _ballHitText; // Örn: "Top: 0/2"
    [SerializeField] private Text _turnText;    // Örn: "Sıra: Oyuncu 1"
    [SerializeField] private Text _resultText;  // Örn: "Sayı!" veya "Faul"
    [SerializeField] private Text _player1ScoreText;
    [SerializeField] private Text _player2ScoreText;
    [SerializeField] private Text _watchdogText; // gösterilecek uyarı/geri sayım
    [SerializeField] private BilliardAIEnvironment _aiEnvironment; 

    [Header("Visual Settings")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _successColor = Color.green;
    [SerializeField] private Color _failColor = Color.red;

    private void Start()
    {
        if (_scoreManager == null)
        {
            Debug.LogWarning("[BilliardUIManager] BilliardScoreManager atanmadı! Inspector'dan atayın.", this);
        }

        if (_scoreManager != null)
        {
            _scoreManager.OnScoreUpdate += UpdateScoreUI;
        }
        else
        {
            Debug.LogWarning("[BilliardUIManager] ScoreManager bulunamadı!");
        }

        // Başlangıç durumu: hemen ekranda gösterilecek metinleri ayarla
        UpdateScoreUI(0, 0, 0, 0);

        // Make sure result text is visible (but empty) so UI elements are present from frame 1
        if (_resultText != null)
        {
            _resultText.text = "";
            _resultText.color = _normalColor;
        }

        // Hide watchdog text initially (it will be shown when necessary)
        if (_watchdogText != null)
        {
            _watchdogText.gameObject.SetActive(false);
            _watchdogText.text = "";
            _watchdogText.color = _normalColor;
        }

        // Ensure all UI Texts share the same visual style (bold + same base color)
        ApplyUnifiedTextStyle();

        // Auto-find AI environment if not assigned
        if (_aiEnvironment == null)
        {
           Debug.Log("[BilliardUIManager] AI Environment not assigned, searching in scene...");
        }

        if (_aiEnvironment != null)
        {
            _aiEnvironment.OnWatchdogIdleStarted += HandleWatchdogIdleStarted;
            _aiEnvironment.OnWatchdogIdleUpdated += HandleWatchdogIdleUpdated;
            _aiEnvironment.OnWatchdogIdleCancelled += HandleWatchdogCancelled;

            _aiEnvironment.OnWatchdogCueStuckStarted += HandleWatchdogCueStarted;
            _aiEnvironment.OnWatchdogCueStuckUpdated += HandleWatchdogCueUpdated;
            _aiEnvironment.OnWatchdogCueStuckCancelled += HandleWatchdogCancelled;

            _aiEnvironment.OnWatchdogTriggered += HandleWatchdogTriggered;
        }
    }

    /// <summary>
    /// Applies unified visual styling to all known Text fields so they appear
    /// immediately and consistently (bold + base color).
    /// </summary>
    private void ApplyUnifiedTextStyle()
    {
        Text[] texts = new Text[]
        {
            _wallHitText,
            _ballHitText,
            _turnText,
            _resultText,
            _player1ScoreText,
            _player2ScoreText,
            _watchdogText
        };

        foreach (var t in texts)
        {
            if (t == null) continue;
            t.color = _normalColor;
            t.fontStyle = FontStyle.Bold;
            // Disable Best Fit which can make text look blurry at startup due to runtime resizing
            t.resizeTextForBestFit = false;
            // Ensure the transform/layout is rebuilt so Unity rasterizes the text at correct size
            var rt = t.GetComponent<RectTransform>();
            if (rt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        // Ensure turn text has a sensible default immediately
        if (_turnText != null && string.IsNullOrEmpty(_turnText.text))
        {
            _turnText.text = "Sıra: -";
        }

        // Force a canvas update so fonts/materials are initialized and render cleanly from frame 1
        Canvas.ForceUpdateCanvases();
    }

    private void OnDestroy()
    {
        if (_scoreManager != null)
        {
            _scoreManager.OnScoreUpdate -= UpdateScoreUI;
        }

        if (_aiEnvironment != null)
        {
            _aiEnvironment.OnWatchdogIdleStarted -= HandleWatchdogIdleStarted;
            _aiEnvironment.OnWatchdogIdleUpdated -= HandleWatchdogIdleUpdated;
            _aiEnvironment.OnWatchdogIdleCancelled -= HandleWatchdogCancelled;

            _aiEnvironment.OnWatchdogCueStuckStarted -= HandleWatchdogCueStarted;
            _aiEnvironment.OnWatchdogCueStuckUpdated -= HandleWatchdogCueUpdated;
            _aiEnvironment.OnWatchdogCueStuckCancelled -= HandleWatchdogCancelled;

            _aiEnvironment.OnWatchdogTriggered -= HandleWatchdogTriggered;
        }
    }

    // Watchdog UI handlers
    private void HandleWatchdogIdleStarted()
    {
        if (_watchdogText == null) return;
        _watchdogText.gameObject.SetActive(true);
        _watchdogText.color = _failColor;
        _watchdogText.text = "Idle Watchdog başladı...";
    }

    private void HandleWatchdogIdleUpdated(float current, float max)
    {
        if (_watchdogText == null) return;
        float remaining = Mathf.Max(0f, max - current);
        _watchdogText.gameObject.SetActive(true);
        _watchdogText.color = _failColor;
        _watchdogText.text = $"⏱ {remaining:F1}s";
    }

    private void HandleWatchdogCueStarted()
    {
        if (_watchdogText == null) return;
        _watchdogText.gameObject.SetActive(true);
        _watchdogText.color = _failColor;
        _watchdogText.text = "İsteka takıldı!";
    }

    private void HandleWatchdogCueUpdated(float current, float max)
    {
        if (_watchdogText == null) return;
        float remaining = Mathf.Max(0f, max - current);
        _watchdogText.gameObject.SetActive(true);
        _watchdogText.color = _failColor;
        _watchdogText.text = $"⏱ {remaining:F1}s";
    }

    private void HandleWatchdogCancelled()
    {
        if (_watchdogText == null) return;
        _watchdogText.gameObject.SetActive(false);
        _watchdogText.text = "";
    }

    private void HandleWatchdogTriggered(string reason)
    {
        if (_watchdogText == null) return;
        _watchdogText.gameObject.SetActive(true);
        _watchdogText.color = _failColor;
        _watchdogText.text = "Watchdog triggered: resetting...";
        // hide after a short delay
        CancelInvoke(nameof(HideWatchdogDelayed));
        Invoke(nameof(HideWatchdogDelayed), 2f);
    }

    private void HideWatchdogDelayed()
    {
        if (_watchdogText == null) return;
        _watchdogText.gameObject.SetActive(false);
        _watchdogText.text = "";
    }

    /// <summary>
    /// Skor bilgilerini günceller (Event tarafından çağrılır)
    /// </summary>
    private void UpdateScoreUI(int wallCount, int ballCount, int p1Score, int p2Score)
    {
        int winningScore = _scoreManager != null ? _scoreManager.WinningScore : 0;

        if (_wallHitText != null)
        {
            _wallHitText.text = $"Bant: {wallCount}/3";
            _wallHitText.color = wallCount >= 3 ? _successColor : _normalColor;
        }

        if (_ballHitText != null)
        {
            _ballHitText.text = $"Top: {ballCount}/2";
            _ballHitText.color = ballCount >= 2 ? _successColor : _normalColor;
        }

        if (_player1ScoreText != null)
        {
            _player1ScoreText.text = winningScore > 0 ? $"P1: {p1Score}/{winningScore}" : $"P1: {p1Score}";
        }

        if (_player2ScoreText != null)
        {
            _player2ScoreText.text = winningScore > 0 ? $"P2: {p2Score}/{winningScore}" : $"P2: {p2Score}";
        }
    }

    /// <summary>
    /// Sıra bilgisini günceller
    /// </summary>
    public void UpdateTurnInfo(string playerName)
    {
        if (_turnText != null)
        {
            _turnText.text = $"{playerName}";
        }
    }

    /// <summary>
    /// Atış sonucunu ekranda gösterir
    /// </summary>
    public void ShowResult(string message, bool isSuccess)
    {
        if (_resultText != null)
        {
            _resultText.text = message;
            _resultText.color = isSuccess ? _successColor : _failColor;
            
            // Mesajı 2 saniye sonra temizle
            CancelInvoke(nameof(ClearResult));
            Invoke(nameof(ClearResult), 2f);
        }
    }

    private void ClearResult()
    {
        if (_resultText != null) _resultText.text = "";
    }
}
