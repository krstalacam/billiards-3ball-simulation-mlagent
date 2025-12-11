# İki İsteka Sistemi (Dual Cue Stick System)

## Özet

Oyun artık **iki ayrı isteka** desteği sunuyor:
- **Player Cue Stick**: Oyuncunun kullandığı isteka
- **Agent Cue Stick**: AI agent'ın kullandığı isteka

Her oyuncu kendi istekasını kullanır ve sistem otomatik olarak sıra kontrolü yapar.

---

## Yapılan Değişiklikler

### 1. **BilliardGameManager.cs**
- ✅ `GameMode` enum'ı eklendi:
  - `SinglePlayer`: Sadece oyuncu (agent yok)
  - `TwoPlayer`: Player vs Agent
- ✅ Tek `_cueStick` yerine **iki ayrı referans**:
  - `_playerCueStick`
  - `_agentCueStick`
- ✅ `MakeShot()` metodu güncellendi: Artık hangi istekanın kullanılacağını parametre olarak alıyor
- ✅ Reset ve balls stopped event'lerinde her iki isteka da yönetiliyor

### 2. **BilliardAIEnvironment.cs**
- ✅ Tek `_cueStick` yerine **iki ayrı referans**:
  - `_playerCueStick`
  - `_agentCueStick`
- ✅ `GetCueStickForCurrentTurn()` metodu eklendi:
  - **Training mode** veya **Agent sırası**: Agent istekası kullanılır
  - **Player sırası**: Player istekası kullanılır
- ✅ `TryQueueShot()` otomatik olarak doğru istekayı seçer

### 3. **BilliardTestController.cs**
- ✅ `_cueStick` → `_playerCueStick` olarak değiştirildi
- ✅ Sadece **player istekasını** kontrol eder
- ✅ Player sırasında aktif, agent sırasında devre dışı

### 4. **CueStick.cs**
- ✅ `CueOwner` enum'ı eklendi:
  - `Player`: Oyuncu istekası
  - `Agent`: AI agent istekası
- ✅ `Owner` property ile isteka sahibi belirlenir
- ✅ Inspector'dan her isteka için owner seçilebilir

### 5. **BilliardAgent.cs**
- ✅ Environment üzerinden `TryQueueShot()` çağrısı yapıyor
- ✅ Environment otomatik olarak agent istekasını seçiyor
- ✅ Açıklayıcı yorum eklendi

---

## Unity Editor'da Kurulum

### Adım 1: İki İsteka Oluştur
1. Sahnede mevcut istekayı **duplicate** et
2. İsimlerini değiştir:
   - `PlayerCueStick`
   - `AgentCueStick`

### Adım 2: İsteka Sahiplerini ve Hedef Toparını Ayarla
Her isteka için **CueStick** component'ini aç:
- **PlayerCueStick**:
  - `Owner` = **Player**
  - `Target Ball` = **Main Ball** (sürükle)
- **AgentCueStick**:
  - `Owner` = **Agent**
  - `Target Ball` = **Main** veya **Secondary** (hangisine vuracaksa)

**ÖNEMLİ**: 
- Inspector'dan manuel atama **ÖNCELİKLİDİR**
- Boş bırakırsanız otomatik atanır (Player→Main, Agent→BallMode'a göre)

### Adım 3: Oyun Modunu Seç (Opsiyonel)

#### BilliardGameManager'da:
- **Ball Mode** = Sadece otomatik atama için kullanılır (manuel atamayı ezmez)

### Adım 4: Referansları Ata

#### BilliardGameManager:
- `Game Mode`: `TwoPlayer` seç
- `Player Cue Stick`: PlayerCueStick'i sürükle
- `Agent Cue Stick`: AgentCueStick'i sürükle

#### BilliardAIEnvironment:
- `Player Cue Stick`: PlayerCueStick'i sürükle
- `Agent Cue Stick`: AgentCueStick'i sürükle

#### BilliardTestController:
- `Player Cue Stick`: PlayerCueStick'i sürükle (sadece bu!)

### Adım 4: Test Et
- **Play Mode**: Player sırasında PlayerCueStick aktif, Agent sırasında AgentCueStick aktif
- **Training Mode**: Sadece AgentCueStick aktif

---

## Nasıl Çalışır?

### Sıra Kontrolü (Turn Management)

```csharp
// Training Mode (TurnState.None)
// -> Sürekli Agent isteka kullanır

// Play Mode - Player Sırası (TurnState.Player)
// -> PlayerCueStick aktif
// -> BilliardTestController aktif (klavye kontrolü)

// Play Mode - Agent Sırası (TurnState.Agent)
// -> AgentCueStick aktif
// -> BilliardTestController devre dışı
```

### Otomatik İsteka Seçimi

`BilliardAIEnvironment.GetCueStickForCurrentTurn()` metodu:
- **Training Mode**: Agent isteka
- **Agent Turn**: Agent isteka
- **Player Turn**: Player isteka

### Toplar Durduğunda
- Her iki isteka da reset edilir
- Sıra değiştirilir (play mode'da)
- Doğru controller aktif/deaktif edilir

---

## Top Modları (Ball Modes)

### SameBall Mode (Klasik Bilardo)
- Her iki oyuncu da **aynı topa** (Main Ball) vurur
- Sırayla hamle yaparlar
- Klasik bilardo kuralları

### DifferentBalls Mode (Farklı Toplar)
- **Player**: Main Ball'a vurur
- **Agent**: Secondary Ball'a vurur
- Her oyuncu kendi topunu kontrol eder
- Daha dinamik oyun mekaniği
- ML-Agents eğitimi için farklı stratejiler

**Önemli**: BallMode değiştiğinde, BilliardGameManager otomatik olarak her istekanın hedef topunu atar.

---

## Tek Oyuncu Modu (SinglePlayer)

`BilliardGameManager.GameMode = SinglePlayer` seçilirse:
- Sadece `_playerCueStick` kullanılır
- `_agentCueStick` opsiyoneldir (atanmayabilir)
- Agent sistemi çalışmaz, sadece oyuncu kontrol eder

---

## Örnek Kullanım

### Kod'da Atış Yapmak

```csharp
// Player istekası ile atış
gameManager.MakeShot(playerCueStick, angleX, angleY, power);

// Agent istekası ile atış
gameManager.MakeShot(agentCueStick, angleX, angleY, power);
```

### Environment Üzerinden (Otomatik)

```csharp
// Mevcut sıraya göre otomatik isteka seçimi
environment.TryQueueShot(angleX, angleY, power);
```

---

## Önemli Notlar

⚠️ **Manuel Referans Atama Gerekli**
- İstekalar artık otomatik olarak bulunamaz (hangisinin player/agent olduğu bilinemez)
- Her component'te manuel olarak atanmalıdır

⚠️ **Owner Ayarı**
- Her CueStick'te `Owner` property'sini doğru ayarlayın
- Bu sadece organizasyon için, sistem buna göre seçim yapmıyor (referanslar üzerinden)

⚠️ **GameManager.MakeShot() API Değişti**
- Artık hangi istekanın kullanılacağını parametre olarak alıyor
- Eski kodlar `gameManager.MakeShot(cueStick, x, y, p)` şeklinde güncellenmeli

✅ **Geriye Uyumluluk**
- Tüm mevcut ML-Agents eğitim kodu aynı şekilde çalışır
- Environment otomatik olarak doğru istekayı seçer
- Test Controller sadece player istekasını kullanır

---

## Sorun Giderme

### "Missing cue stick reference"
➡️ BilliardGameManager ve BilliardAIEnvironment'ta her iki isteka referansını da atayın

### "TestController çalışmıyor"
➡️ `_playerCueStick` atandığından ve Controller aktif olduğundan emin olun

### "Agent vuruş yapmıyor"
➡️ BilliardAIEnvironment'ta `_agentCueStick` atandığından emin olun

### "Yanlış isteka kullanılıyor"
➡️ TurnState kontrolünü ve `GetCueStickForCurrentTurn()` mantığını kontrol edin

### "İsteka yanlış topa vuruyor"
➡️ BilliardGameManager'da `Ball Mode` ayarını kontrol edin
➡️ Console'da "BallMode:" log mesajını kontrol edin (hangi isteka hangi topa atandığını gösterir)

### "Agent observation'ları yanlış"
➡️ Agent artık kendi kontrol ettiği topu gözlemler (Main veya Secondary)
➡️ BallMode = DifferentBalls ise agent Secondary ball'ı gözlemler
