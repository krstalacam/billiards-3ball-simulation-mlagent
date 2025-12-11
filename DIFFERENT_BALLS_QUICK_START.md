# Farklı Toplara Vurma Sistemi - Hızlı Başlangıç

## ✅ Tamamlandı

Artık **Player** ve **Agent** farklı toplara vurabilir!

---

## 🎯 Nasıl Ayarlanır?

### 1. İki İsteka Oluştur
- Mevcut istekayı **duplicate** et
- İsimlendir: `PlayerCueStick` ve `AgentCueStick`

### 2. Inspector'dan Hedef Topları Ata

**PlayerCueStick** component'i:
```
Owner: Player
Target Ball: Main Ball (sürükle)
```

**AgentCueStick** component'i:
```
Owner: Agent
Target Ball: Secondary Ball (sürükle)  // veya Main Ball (aynı topa vurma için)
```

### 3. Referansları Ata

**BilliardGameManager**:
- `Player Cue Stick` → PlayerCueStick
- `Agent Cue Stick` → AgentCueStick
- `Main Ball` → Main Ball (beyaz top)
- `Target Ball` → Target Ball (kırmızı top)
- `Secondary Ball` → Secondary Ball (sarı top)

**BilliardAIEnvironment**:
- `Player Cue Stick` → PlayerCueStick
- `Agent Cue Stick` → AgentCueStick
- (Toplar aynı)

**BilliardTestController**:
- `Player Cue Stick` → PlayerCueStick

### 4. Oynat!

---

## 🔄 Sıra Sistemi

### Training Mode
- Agent sürekli vuruş yapar
- Turn state = `None`
- Sadece agent isteka aktif

### Play Mode (TwoPlayer)
1. **Player sırası**: 
   - Player isteka aktif, controller açık
   - Player vuruş yapar
   - Toplar durur
   
2. **Sıra değişir → Agent**:
   - Agent isteka **otomatik olarak kendi topuna hizalanır**
   - Player controller kapanır
   - Agent AI vuruş yapar
   - Toplar durur
   
3. **Sıra değişir → Player**:
   - Player isteka **otomatik olarak kendi topuna hizalanır**
   - Player controller açılır
   - Player vuruş yapar
   - Döngü devam eder

---

## 🎮 Özellikler

### ✅ Her İsteka Kendi Topuna Vurur
- Player → Main Ball
- Agent → Secondary Ball (veya Main)
- Inspector'dan değiştirilebilir

### ✅ Otomatik Hizalama
- Toplar durduğunda istekalar kendi toplarına hizalanır
- Sıra değiştiğinde aktif istekanın topu hazırlanır
- Reset sonrası her iki isteka da hizalanır

### ✅ Akıllı Atama
- Inspector'dan atanmışsa **korunur**
- Boşsa otomatik atanır:
  - Player → Her zaman Main Ball
  - Agent → BallMode'a göre (SameBall→Main, DifferentBalls→Secondary)

---

## 🐛 Sorun Giderme

### "İsteka yanlış topa gidiyor"
➡️ CueStick component'inde `Target Ball` doğru atandığını kontrol et

### "İsteka toplar durduktan sonra hizalanmıyor"
➡️ Console'da "cue aligned to" log'larını kontrol et
➡️ BilliardGameManager'da `_playerCueStick` ve `_agentCueStick` atandığından emin ol

### "Agent kendi sırasında vuruş yapmıyor"
➡️ BilliardAIEnvironment'ta `_agentCueStick` atandığından emin ol
➡️ Agent CueStick'in `Target Ball` atandığından emin ol

### "Player vuruş yapamıyor"
➡️ BilliardTestController'da `_playerCueStick` atandığından emin ol
➡️ TurnState = Player olduğunu kontrol et (Console log'ları)

---

## 📋 Kontrol Listesi

- [ ] İki isteka oluşturuldu (PlayerCueStick, AgentCueStick)
- [ ] Her istekada Owner ayarlandı (Player/Agent)
- [ ] Her istekada Target Ball atandı (Inspector'dan)
- [ ] BilliardGameManager'da her iki isteka referansı atandı
- [ ] BilliardAIEnvironment'ta her iki isteka referansı atandı
- [ ] BilliardTestController'da player isteka atandı
- [ ] Toplar atandı (Main, Target, Secondary)

---

## 🎯 Örnek Kurulum

### Klasik Bilardo (Her İkisi Aynı Topa)
```
PlayerCueStick → Target Ball: Main Ball
AgentCueStick → Target Ball: Main Ball
```

### Farklı Toplar (Daha Dinamik)
```
PlayerCueStick → Target Ball: Main Ball
AgentCueStick → Target Ball: Secondary Ball
```

---

## 💡 İpuçları

1. **Test için**: Play mode'da oyuna başla, Player vuruş yap, toplar dursun, otomatik Agent sırasına geçer
2. **Debug**: Console'da turn değişimi ve isteka hizalama log'larını izle
3. **Training**: Training mode'da BallMode önemli değil (sürekli agent vuruş yapar)
4. **Inspector Öncelikli**: Manuel atamalar her zaman otomatik atamaları geçersiz kılar

---

Detaylı açıklama için `DUAL_CUE_STICK_SETUP.md` dosyasına bakın.
