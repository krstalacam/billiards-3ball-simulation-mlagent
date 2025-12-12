using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;

/// <summary>
/// Holds the settings for a billiard game session.
/// This ScriptableObject acts as a central configuration container.
/// </summary>
[CreateAssetMenu(fileName = "GameSettings", menuName = "Billiards/Game Settings", order = 1)]
public class GameSettings : ScriptableObject
{
    [Header("Game Mode Configuration")]
    [Tooltip("Determines the player and agent configuration (e.g., Player vs. AI, AI Only).")]
    [SerializeField] private BilliardGameManager.GameMode _gameMode = BilliardGameManager.GameMode.PlayerVsAi;

    [Tooltip("Determines which balls the cue sticks will target (e.g., everyone hits the main ball).")]
    [SerializeField] private BilliardGameManager.BallMode _ballMode = BilliardGameManager.BallMode.SameBall;

    [Header("AI Configuration")]
    [Tooltip("Enable this for training sessions. The agent's behavior type will be set to Default.")]
    [SerializeField] private bool _isTrainingMode = false;

    [Header("Ball Randomization")]
    [Tooltip("Rastgele top yerleştirme (Training modda otomatik aktif, diğer modlarda manuel kontrol edilebilir)")]
    [SerializeField] private bool _randomizeBallPositions = false;

    [Header("AI Models")]
    [Tooltip("List of available NN Models for the agent.")]
    [SerializeField] private List<NNModel> _availableModels;
    [SerializeField] private int _currentModelIndex = 0;

    public bool IsTrainingMode => _isTrainingMode;
    
    /// <summary>
    /// Training modda otomatik true döner, diğer modlarda kullanıcı ayarını döner
    /// </summary>
    public bool RandomizeBallPositions => _isTrainingMode || _randomizeBallPositions;

    public BilliardGameManager.GameMode CurrentGameMode => _gameMode;
    public BilliardGameManager.BallMode CurrentBallMode => _ballMode;

    public NNModel CurrentModel => 
        (_availableModels != null && _availableModels.Count > 0 && _currentModelIndex >= 0 && _currentModelIndex < _availableModels.Count) 
        ? _availableModels[_currentModelIndex] 
        : null;

    public int CurrentModelIndex => _currentModelIndex;

    public event Action SettingsChanged;

    public void SetGameMode(BilliardGameManager.GameMode newMode)
    {
        if (_gameMode == newMode) return;
        _gameMode = newMode;
        NotifySettingsChanged();
    }

    public void SetBallMode(BilliardGameManager.BallMode newMode)
    {
        if (_ballMode == newMode) return;
        _ballMode = newMode;
        NotifySettingsChanged();
    }

    public void SetTrainingMode(bool isTraining)
    {
        if (_isTrainingMode == isTraining) return;
        _isTrainingMode = isTraining;
        NotifySettingsChanged();
    }

    public void SetModelIndex(int index)
    {
        if (_currentModelIndex == index) return;
        _currentModelIndex = index;
        NotifySettingsChanged();
    }

    public void SetRandomizeBallPositions(bool randomize)
    {
        if (_randomizeBallPositions == randomize) return;
        _randomizeBallPositions = randomize;
        NotifySettingsChanged();
    }

    public List<string> GetModelNames()
    {
        List<string> names = new List<string>();
        if (_availableModels != null)
        {
            foreach(var model in _availableModels)
            {
                names.Add(model != null ? model.name : "None");
            }
        }
        return names;
    }

    private void NotifySettingsChanged()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
        SettingsChanged?.Invoke();
    }
}
