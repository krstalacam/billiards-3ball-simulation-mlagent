// using UnityEngine;

// /// <summary>
// /// Basit grafik ayarlarını yöneten ve dışarıdan çağrılabilen fonksiyonlar sunan sınıf.
// /// PlayerPrefs kullanılmaz, sadece anlık ayar yapılır.
// /// </summary>
// public class GraphicSettingsManager : MonoBehaviour
// {
//     /// <summary>
//     /// V-Sync aç/kapat (true: açık, false: kapalı)
//     /// </summary>
//     public void SetVSync(bool enabled)
//     {
//         QualitySettings.vSyncCount = enabled ? 1 : 0;
//     }

//     /// <summary>
//     /// Hedef FPS belirle (ör: 60, 120, -1 platform varsayılanı)
//     /// </summary>
//     public void SetTargetFrameRate(int frameRate)
//     {
//         Application.targetFrameRate = frameRate;
//     }

//     /// <summary>
//     /// Ekran çözünürlüğünü ayarla (tam ekran değil, sadece pencere boyutu)
//     /// </summary>
//     public void SetResolution(int width, int height)
//     {
//         Screen.SetResolution(width, height, false);
//     }

//     /// <summary>
//     /// Tam ekran aç/kapat (true: tam ekran, false: pencere)
//     /// </summary>
//     public void SetFullscreen(bool isFullscreen)
//     {
//         Screen.fullScreen = isFullscreen;
//     }

//     /// <summary>
//     /// Mevcut ekran çözünürlüğünü döndürür
//     /// </summary>
//     public Resolution GetCurrentResolution()
//     {
//         return Screen.currentResolution;
//     }

//     /// <summary>
//     /// Desteklenen tüm çözünürlükleri döndürür
//     /// </summary>
//     public Resolution[] GetResolutions()
//     {
//         return Screen.resolutions;
//     }
// }
