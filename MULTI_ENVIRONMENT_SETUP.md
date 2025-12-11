# Çoklu Environment Kurulumu (Multi-Table Training)

## Sorun Neydi?

Birden fazla masa (environment) oluşturduğunuzda sadece birinin çalışıyordu. Bunun nedeni kodda **`FindFirstObjectByType`** kullanımıydı. Bu metod Unity sahnesindeki **sadece ilk objeyi** bulur, bu yüzden tüm masalar aynı component'lara referans veriyordu.

## Çözüm

Her masa prefab'ı artık **kendi parent/child hiyerarşisindeki** component'ları buluyor:
- `FindFirstObjectByType<T>()` yerine ➔ `GetComponentInChildren<T>()` 
- `FindObjectsByType<T>()` yerine ➔ `GetComponentsInChildren<T>()`

## Unity Prefab Yapısı

Her masa prefab'ı şu yapıda olmalı:

```
TableEnvironment (Parent)
├── BilliardAIEnvironment
├── GameFlowManager
├── BilliardGameManager
├── BilliardScoreManager
├── BilliardAgent
├── BilliardRewardManager
├── BilliardTestController
├── BilliardUIManager
├── BilliardGameMenuUI
├── Table (Mesh)
├── Walls
├── Balls
│   ├── MainBall
│   ├── TargetBall
│   └── SecondaryBall
└── CueSticks
    ├── PlayerCueStick
    └── AgentCueStick
```

## Düzeltilen Dosyalar

1. **GameFlowManager.cs** - `Awake()` metodu
2. **BilliardAIEnvironment.cs** - `AutoResolveReferences()` metodu
3. **BilliardAgent.cs** - `Initialize()` metodu
4. **BilliardRewardManager.cs** - `Awake()` metodu
5. **BilliardTestController.cs** - `Awake()` metodu
6. **BilliardGameManager.cs** - `SetGameMode()` metodu
7. **BilliardUIManager.cs** - `Start()` metodu
8. **BilliardGameMenuUI.cs** - `Awake()` metodu
9. **OutOfBoundsDetector.cs** - `OnTriggerEnter()` metodu
10. **CueStick.cs** - `Start()` metodu

## Çoklu Masa Kurulumu (ML-Agents Training)

### 1. Prefab Oluşturma
- Tek çalışan masanızı seçin
- Project penceresinde sağ tık → "Create Prefab"
- Prefab'ı istediğiniz klasöre kaydedin

### 2. Masaları Çoğaltma
- Sahneye birden fazla prefab instance'ı ekleyin
- Her masa **farklı pozisyonlarda** olmalı (birbirini engellemeyecek şekilde)

### 3. Agent Ayarları
Her masadaki `BilliardAgent` component'ında:
- **Behavior Type:** `Default` (training için)
- **Behavior Name:** Aynı olmalı (ör: `BilliardBehavior`)
- **Team ID:** `0` (tüm masalarda)

### 4. Training Komutu
```bash
mlagents-learn config/MyBehavior.yaml --run-id=multi_table_training
```

### 5. Doğrulama
Training başladığında:
- TensorBoard'da her masanın metriklerini görebilirsiniz
- Console'da her masa için ayrı log mesajları görmelisiniz
- Tüm masalarda toplar hareket etmeli

## Performans İpuçları

### Optimizasyon
- **Time Scale:** `Physics.autoSimulation = false` kullanarak manual simulation yapabilirsiniz
- **Görsellik:** Training sırasında kamera/UI'ları kapatın
- **Masa Sayısı:** 8-16 masa genellikle ideal (donanımınıza bağlı)

### Bellek Kullanımı
Çok fazla masa eklerseniz:
1. **Physics simülasyonu** ağırlaşır
2. **Collision detection** yavaşlar
3. **RAM kullanımı** artar

Çözüm: Unity Editor yerine **Build** kullanın (çok daha hızlı)

## Hata Ayıklama

### Sorun: Sadece bir masa hala çalışıyor
**Çözüm:** Inspector'da her masanın referanslarını kontrol edin:
- `BilliardAIEnvironment` → `_playerCueStick`, `_agentCueStick`, `_mainBall` vb.
- `GameFlowManager` → Tüm referanslar dolu mu?

### Sorun: Agent decision alamıyor
**Çözüm:** 
- `Academy.Instance` başlatıldı mı kontrol edin
- `BehaviorParameters` component var mı?
- Training modda mısınız? (`gameSettings.IsTrainingMode = true`)

### Sorun: Toplar senkronize değil
**Çözüm:**
- `Physics.simulationMode = SimulationMode.Script` kullanın
- `Physics.Simulate(Time.fixedDeltaTime)` manuel çağırın

## ML-Agents Best Practices

### Episode Management
- Her masa bağımsız episode'lara sahip olmalı
- Episode reset'leri diğer masaları etkilememeli
- `agent.EndEpisode()` sadece ilgili masayı resetler

### Reward System
- Her masa kendi reward'larını hesaplar
- Reward'lar ML-Agents tarafından ortalaması alınır
- Tüm masalar aynı reward yapısını kullanmalı

### Observation Space
- Her agent sadece kendi masasındaki topları gözlemler
- Normalize edilmiş değerler kullanın (-1 ile 1 arası)
- Observation sayısı tüm agent'larda aynı olmalı

## Ek Notlar

- Bu düzeltmeler **geriye dönük uyumlu**dur (tek masa da çalışır)
- Prefab hierarchy önemlidir - değiştirmeyin
- Inspector'dan manuel referans atamak her zaman daha güvenlidir
- Training sırasında sahneyi değiştirmeyin

## İletişim

Sorunlarınız için:
1. Console'daki hata mesajlarını kontrol edin
2. Her masanın `BilliardAIEnvironment` component'ını inceleyin
3. Debug logları aktif edin (`_showDebugInfo = true`)
