"""
ML-Agents Training Script with Auto-Stop
Eğitim tamamlandığında veya hedef reward'a ulaşıldığında otomatik durdurur
"""
import subprocess
import sys
import time
import os
import re

def monitor_training(config_path, run_id, target_reward=0.95, check_interval=30):
    """
    ML-Agents eğitimini başlatır ve logları izler.
    Hedef reward'a ulaşıldığında veya 'Not Training' görüldüğünde otomatik durdurur.
    
    Args:
        config_path: YAML config dosyasının yolu
        run_id: Eğitim run ID'si
        target_reward: Hedef ortalama reward (opsiyonel)
        check_interval: Log kontrol aralığı (saniye)
    """
    
    # ML-Agents eğitim komutunu başlat
    cmd = ["mlagents-learn", config_path, f"--run-id={run_id}"]
    print(f"Starting training: {' '.join(cmd)}")
    print(f"Target reward: {target_reward}")
    print(f"Monitoring logs every {check_interval} seconds...")
    print("-" * 60)
    
    # Process'i başlat
    process = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
        universal_newlines=True
    )
    
    training_completed = False
    last_mean_reward = 0.0
    consecutive_not_training = 0
    
    try:
        # Log satırlarını oku
        for line in iter(process.stdout.readline, ''):
            if line:
                print(line, end='')  # Logu ekrana yazdır
                
                # "Not Training" kontrolü
                if "Not Training" in line:
                    consecutive_not_training += 1
                    if consecutive_not_training >= 3:
                        print("\n" + "=" * 60)
                        print("⚠️  'Not Training' detected 3 times consecutively!")
                        print("Training has completed. Stopping process...")
                        print("=" * 60)
                        training_completed = True
                        break
                else:
                    consecutive_not_training = 0
                
                # Mean Reward kontrolü
                match = re.search(r'Mean Reward:\s*([-\d.]+)', line)
                if match:
                    last_mean_reward = float(match.group(1))
                    if last_mean_reward >= target_reward:
                        print("\n" + "=" * 60)
                        print(f"🎉 Target reward {target_reward} reached!")
                        print(f"Current Mean Reward: {last_mean_reward}")
                        print("Stopping training...")
                        print("=" * 60)
                        training_completed = True
                        break
                
                # "Learning was interrupted" kontrolü
                if "Learning was interrupted" in line or "Exported" in line and "final" in line.lower():
                    print("\n" + "=" * 60)
                    print("✅ Training completed successfully!")
                    print("=" * 60)
                    training_completed = True
                    break
        
        # Process'i bekle
        if training_completed:
            print("Terminating ML-Agents process...")
            process.terminate()
            try:
                process.wait(timeout=10)
                print("✅ Process terminated successfully.")
            except subprocess.TimeoutExpired:
                print("⚠️  Process didn't terminate gracefully, forcing...")
                process.kill()
                process.wait()
                print("✅ Process killed.")
        else:
            # Normal bitiş
            process.wait()
            
    except KeyboardInterrupt:
        print("\n" + "=" * 60)
        print("⚠️  Training interrupted by user (Ctrl+C)")
        print("=" * 60)
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
    
    print("\n" + "=" * 60)
    print("Training session ended.")
    print(f"Final Mean Reward: {last_mean_reward}")
    print("=" * 60)
    
    return process.returncode

if __name__ == "__main__":
    # Parametreleri al
    if len(sys.argv) < 3:
        print("Usage: python train_with_auto_stop.py <config_path> <run_id> [target_reward]")
        print("Example: python train_with_auto_stop.py config/Turtle.yaml turtle_agent_test6 0.95")
        sys.exit(1)
    
    config_path = sys.argv[1]
    run_id = sys.argv[2]
    target_reward = float(sys.argv[3]) if len(sys.argv) > 3 else 0.95
    
    # Config dosyasının varlığını kontrol et
    if not os.path.exists(config_path):
        print(f"❌ Config file not found: {config_path}")
        sys.exit(1)
    
    # Eğitimi başlat ve izle
    exit_code = monitor_training(config_path, run_id, target_reward)
    sys.exit(exit_code)
