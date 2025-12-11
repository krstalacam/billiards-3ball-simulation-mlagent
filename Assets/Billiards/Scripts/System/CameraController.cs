using UnityEngine;
using UnityEngine.UI;

namespace Billiards.System
{
    /// <summary>
    /// 3 kamera arasında geçiş yapmayı sağlayan kontrol sistemi.
    /// Butona her basıldığında sırayla bir sonraki kameraya geçer.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Kamera Ayarları")]
        [Tooltip("Sahne üzerindeki kamera listesi. Inspector'dan sürükleyip bırakarak ekleyin.")]
        public Camera[] cameras;

        [Header("Buton Ayarları")]
        [Tooltip("Kamera değiştirmek için kullanılacak buton. Inspector'dan sürükleyip bırakın.")]
        public Button switchCameraButton;

        // Mevcut aktif kamera indeksi
        private int currentCameraIndex = 0;

        void Start()
        {
            // İlk kurulum kontrolleri
            if (cameras == null || cameras.Length == 0)
            {
                Debug.LogError("CameraController: Hiç kamera atanmamış! Lütfen Inspector'dan kamera ekleyin.");
                return;
            }

            if (switchCameraButton == null)
            {
                Debug.LogError("CameraController: Buton atanmamış! Lütfen Inspector'dan buton ekleyin.");
                return;
            }

            // Butona tıklama event'ini bağla
            switchCameraButton.onClick.AddListener(SwitchToNextCamera);

            // İlk kamera hariç diğerlerini kapat
            InitializeCameras();
        }

        /// <summary>
        /// Başlangıçta sadece ilk kamerayı aktif eder, diğerlerini kapatır.
        /// </summary>
        private void InitializeCameras()
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                {
                    cameras[i].enabled = (i == 0);
                }
            }

            currentCameraIndex = 0;
            Debug.Log($"CameraController: {cameras[0].name} kamerası aktif edildi.");
        }

        /// <summary>
        /// Sıradaki kameraya geçiş yapar. Son kameradan sonra ilk kameraya döner.
        /// </summary>
        public void SwitchToNextCamera()
        {
            if (cameras == null || cameras.Length == 0)
            {
                Debug.LogWarning("CameraController: Kamera listesi boş!");
                return;
            }

            // Mevcut kamerayı kapat
            if (cameras[currentCameraIndex] != null)
            {
                cameras[currentCameraIndex].enabled = false;
            }

            // Sıradaki indekse geç (son kameradan sonra başa dön)
            currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;

            // Yeni kamerayı aç
            if (cameras[currentCameraIndex] != null)
            {
                cameras[currentCameraIndex].enabled = true;
                Debug.Log($"CameraController: {cameras[currentCameraIndex].name} kamerasına geçildi.");
            }
            else
            {
                Debug.LogWarning($"CameraController: {currentCameraIndex}. indeksteki kamera null!");
            }
        }

        /// <summary>
        /// Belirli bir indeksteki kameraya doğrudan geçiş yapar.
        /// </summary>
        /// <param name="index">Aktif edilecek kamera indeksi (0-2 arası)</param>
        public void SwitchToCamera(int index)
        {
            if (cameras == null || cameras.Length == 0)
            {
                Debug.LogWarning("CameraController: Kamera listesi boş!");
                return;
            }

            if (index < 0 || index >= cameras.Length)
            {
                Debug.LogWarning($"CameraController: Geçersiz kamera indeksi: {index}");
                return;
            }

            // Mevcut kamerayı kapat
            if (cameras[currentCameraIndex] != null)
            {
                cameras[currentCameraIndex].enabled = false;
            }

            // Yeni kamerayı aç
            currentCameraIndex = index;
            if (cameras[currentCameraIndex] != null)
            {
                cameras[currentCameraIndex].enabled = true;
                Debug.Log($"CameraController: {cameras[currentCameraIndex].name} kamerasına geçildi.");
            }
        }

        void OnDestroy()
        {
            // Event listener'ı temizle
            if (switchCameraButton != null)
            {
                switchCameraButton.onClick.RemoveListener(SwitchToNextCamera);
            }
        }
    }
}
