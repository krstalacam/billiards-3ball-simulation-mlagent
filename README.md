# 🎱 billiards-3ball-simulation-mlagent  

**A Unity-based three-ball billiards simulator powered by a Machine-Learning (ML) agent.**  

This repository contains a fully-functional Unity project that simulates a **three-ball billiards** game and trains an **ML-Agent** to play it using reinforcement learning. The agent learns to aim, control power, and execute shots to score points according to the game rules.

---

## 📖 Overview  

The project focuses on creating an AI agent capable of playing Three-Ball Billiards. The AI is trained using **Unity ML-Agents Toolkit**.  

The AI learns to:
*   **Aim accurately** and choose optimal shot angles.  
*   **Control shot power** within defined limits.  
*   **Avoid illegal moves** (e.g., fouls, hitting walls excessively without purpose).  
*   **Maximize the score** by pocketing balls or achieving specific collision sequences (Carom).  
*   **Avoid getting stuck** in corners using a dedicated corner-avoidance system.


All configuration parameters (angle limits, power limits, reward values, penalty thresholds) are exposed via `BilliardAgentConfig.cs` for rapid tweaking directly from the Unity Inspector.

---

## 📸 Screenshots

![Gameplay 5](img/Ekran%20görüntüsü%202025-12-11%20220536.png)
![Gameplay 4](img/Ekran%20görüntüsü%202025-12-10%20210505.png)
![Gameplay 3](img/Ekran%20görüntüsü%202025-12-10%20210455.png)
![Gameplay 2](img/Ekran%20görüntüsü%202025-12-10%20145751.png)
![Gameplay 1](img/Ekran%20görüntüsü%202025-12-07%20192213.png)

---

## ✨ Key Features  

| Feature | Description |
|---------|-------------|
| **Three-Ball Rule Set** | Implements the classic "3-ball" billiards variant (cue ball + two object balls). |
| **ML-Agent Integration** | Uses Unity’s ML-Agents Toolkit to train a neural-network policy. |
| **Reward Shaping** | Fully customizable rewards and penalties (wall hits, successful shots, corner stay, etc.) via `BilliardAgentConfig`. |
| **Corner-Avoidance** | Detects and penalizes the agent when it gets stuck in a corner to encourage exploration using `BilliardGameManager` logic. |
| **Dynamic Configuration** | All limits and hyper-parameters are editable in the `BilliardAgentConfig` ScriptableObject asset. |
| **Debug Visualisation** | Gizmos show ball relationships, corner thresholds, and reward zones in the Scene view. |
| **Robust State Management** | `BilliardGameManager` handles turn-based logic, physics settling, and resets. |

---

## 🚀 Getting Started  

### 1️⃣ Prerequisites  

*   **Unity** (2022.3 LTS or newer recommended)
*   **Python** 3.9+ (for training the ML-Agent)
*   **ML-Agents Toolkit** package in Unity (`com.unity.ml-agents`)

### 2️⃣ Clone the Repository  

```bash
git clone https://github.com/krstlcm/billiards-3ball-simulation-mlagent.git
cd billiards-3ball-simulation-mlagent
```

### 3️⃣ Open the Unity Project  

1.  Launch **Unity Hub**.  
2.  Click **Add** → select the folder `three-ball-billiards-agent`.  
3.  Open the project. Unity will resolve dependencies.

### 4️⃣ Setup ML-Agents (Python)

To train the agent, you need to set up the Python environment.

```bash
# From the project root (where the ml-agents folder or config folder resides)
python -m venv .venv
# Activate the environment
# Windows:
.\.venv\Scripts\activate
# Mac/Linux:
source .venv/bin/activate

# Install mlagents
pip install mlagents
```

### 5️⃣ Train the Agent  

Use the provided config file to start training:

```bash
mlagents-learn config/MyBehavior.yaml --run-id=3ball_run_01 --train
```

*   After running the command, press **Play** in the Unity Editor to start the training session.

### 6️⃣ Play with Trained Model  

1.  Locate the trained model file (`.onnx`) in the `results/` folder after training.
2.  Move it to your Unity project (e.g., `Assets/ML-Agents/Models/`).
3.  Assign the model to the **Model** field in the **Behavior Parameters** component of the `BilliardAgent` GameObject.
4.  Press **Play** to watch the AI play!

---

## 🛠️ Project Structure  

```
Assets/
├─ Billiards/
│  ├─ Scripts/
│  │  ├─ AI/
│  │  │  ├─ BilliardAgent.cs          # Core agent logic (Observations, Actions, Rewards)
│  │  │  └─ BilliardAgentConfig.cs    # Configuration Asset (Rewards, Penalties, Settings)
│  │  └─ Core/
│  │     └─ BilliardGameManager.cs    # Game loop, turns, physics checks, reset logic
│  └─ Prefabs/                        # Ball, Table, and Cue Prefabs
config/
└─ MyBehavior.yaml                    # ML-Agents training hyper-parameters
```

### Key Scripts

*   **`BilliardAgentConfig.cs`**:  
    This ScriptableObject holds all the tweaking parameters. You can create different config assets for different training scenarios (e.g., `AggressiveConfig`, `PreciseConfig`).
    *   *Modify:* Rewards for hitting balls, penalties for fouls, corner thresholds.

*   **`BilliardGameManager.cs`**:  
    The brain of the simulation. It handles:
    *   Turn management (Player vs AI, Training Mode).
    *   Physics checks (resetting balls when stopped).
    *   Corner detection logic.

---

## 📚 Documentation & Resources  

*   **Unity ML-Agents Toolkit**: [GitHub](https://github.com/Unity-Technologies/ml-agents)
*   **Three-Ball Billiards Rules**: [Wikipedia](https://en.wikipedia.org/wiki/Three-ball_(billiards))

---

## 📄 License  

This project is licensed under the **MIT License**.
