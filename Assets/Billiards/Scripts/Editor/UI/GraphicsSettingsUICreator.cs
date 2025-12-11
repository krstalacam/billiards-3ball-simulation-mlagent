// using UnityEngine;
// using UnityEditor;
// using UnityEngine.UI;
// using TMPro;
// using UnityEditor.Events; // BURASI EKLENDİ: Inspector'daki listeye ekleme yapmak için gerekli

// public class GraphicsSettingsUICreator : MonoBehaviour
// {
//     [MenuItem("Tools/Create Graphics Settings UI Canvas")]
//     public static void CreateGraphicsSettingsUICanvas()
//     {
//         // Ana Canvas oluştur
//         GameObject canvasGO = new GameObject("GraphicsSettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        
//         // GraphicSettingsManager component ekle
//         var gsm = canvasGO.AddComponent<GraphicSettingsManager>();
        
//         Canvas canvas = canvasGO.GetComponent<Canvas>();
//         canvas.renderMode = RenderMode.ScreenSpaceOverlay;
//         CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
//         scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
//         scaler.referenceResolution = new Vector2(1920, 1080);
//         canvasGO.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = true;

//         // EventSystem yoksa ekle
//         if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
//         {
//             GameObject eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
//             Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
//         }

//         // Panel oluştur
//         GameObject panelGO = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
//         panelGO.transform.SetParent(canvasGO.transform, false);
//         RectTransform panelRect = panelGO.GetComponent<RectTransform>();
//         panelRect.sizeDelta = new Vector2(600, 400);
//         panelRect.anchoredPosition = Vector2.zero;
//         Image panelImage = panelGO.GetComponent<Image>();
//         panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

//         // Başlık (TextMeshPro)
//         GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
//         titleGO.transform.SetParent(panelGO.transform, false);
//         RectTransform titleRect = titleGO.GetComponent<RectTransform>();
//         titleRect.anchorMin = new Vector2(0.5f, 1f);
//         titleRect.anchorMax = new Vector2(0.5f, 1f);
//         titleRect.pivot = new Vector2(0.5f, 1f);
//         titleRect.anchoredPosition = new Vector2(0, -30);
//         titleRect.sizeDelta = new Vector2(400, 60);
//         TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
//         titleText.text = "Grafik Ayarları";
//         titleText.fontSize = 36;
//         titleText.alignment = TextAlignmentOptions.Center;

//         float y = -100f;
//         float spacing = -70f;

//         // --- VSYNC TOGGLE ---
//         GameObject vsyncToggleGO = CreateToggle(panelGO.transform, "VSync Açık/Kapalı", new Vector2(0, y));
//         Toggle vsyncToggle = vsyncToggleGO.GetComponent<Toggle>();
        
//         // Örnek: VSync için de Inspector'a eklemek istersen:
//         // UnityEventTools.AddPersistentListener(vsyncToggle.onValueChanged, gsm.SetVSync);
//         // Şimdilik eski yöntemle (gizli) bırakıyorum, istersen yukarıdaki gibi açabilirsin.
//         vsyncToggle.onValueChanged.AddListener((value) => {
//             if (gsm != null) gsm.SetVSync(value);
//         });
//         y += spacing;

//         // --- FPS SLIDER ---
//         GameObject fpsSliderGO = CreateSlider(panelGO.transform, "FPS", 30, 240, 60, new Vector2(0, y));
//         Slider fpsSlider = fpsSliderGO.GetComponent<Slider>();
//         fpsSlider.onValueChanged.AddListener((value) => {
//             if (gsm != null) gsm.SetTargetFrameRate(Mathf.RoundToInt(value));
//         });
//         y += spacing;

//         // --- ÇÖZÜNÜRLÜK BUTONU ---
//         GameObject resButtonGO = CreateButton(panelGO.transform, "Çözünürlük Değiştir", new Vector2(0, y));
//         Button resButton = resButtonGO.GetComponent<Button>();
//         resButton.onClick.AddListener(() => {
//             if (gsm != null)
//             {
//                 var resolutions = gsm.GetResolutions();
//                 if (resolutions != null && resolutions.Length > 0)
//                 {
//                     var res = resolutions[0];
//                     gsm.SetResolution(res.width, res.height);
//                 }
//             }
//             Image buttonImage = resButtonGO.GetComponent<Image>();
//             if (buttonImage != null) buttonImage.color = Color.green;
//         });
//         y += spacing;

//         // --- TAM EKRAN (FULLSCREEN) TOGGLE ---
//         GameObject fullscreenToggleGO = CreateToggle(panelGO.transform, "Tam Ekran", new Vector2(0, y));
//         Toggle fullscreenToggle = fullscreenToggleGO.GetComponent<Toggle>();

//         // -------------------------------------------------------------------------
//         // BURASI DEĞİŞTİ: Inspector'daki listeye (OnValueChanged) ekleme yapıyoruz
//         // -------------------------------------------------------------------------
//         if (gsm != null)
//         {
//             // Bu kod, gsm objesindeki SetFullscreen fonksiyonunu Toggle'ın listesine ekler.
//             // "Dynamic Bool" olarak ekler, yani toggle değiştikçe true/false değeri gider.
//             UnityEventTools.AddPersistentListener(fullscreenToggle.onValueChanged, gsm.SetFullscreen);
//         }

//         // Görsel geri bildirim için (Renk değişimi) hala kod tarafında bir listener tutabiliriz
//         // veya bunu da SetFullscreen içine taşıyabilirsin. Şimdilik renk değişimi burada kalsın:
//         fullscreenToggle.onValueChanged.AddListener((value) => {
//             Image toggleImage = fullscreenToggleGO.GetComponent<Image>();
//             if (toggleImage != null)
//             {
//                 toggleImage.color = value ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.3f, 0.3f);
//             }
//         });

//         // Toggle'un başlangıç değerini ayarla
//         fullscreenToggle.isOn = Screen.fullScreen;
//         y += spacing;

//         // Canvas'ı seçili yap
//         Selection.activeGameObject = canvasGO;

//         // Undo desteği
//         Undo.RegisterCreatedObjectUndo(canvasGO, "Create Graphics Settings Canvas");
//     }

//     // --- Yardımcı UI Fonksiyonları (Değişmedi) ---

//     private static GameObject CreateToggle(Transform parent, string label, Vector2 anchoredPos)
//     {
//         GameObject toggleGO = new GameObject(label + "_Toggle", typeof(RectTransform), typeof(Toggle), typeof(CanvasRenderer));
//         toggleGO.transform.SetParent(parent, false);
//         RectTransform rect = toggleGO.GetComponent<RectTransform>();
//         rect.sizeDelta = new Vector2(300, 40);
//         rect.anchoredPosition = anchoredPos;

//         GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
//         bgGO.transform.SetParent(toggleGO.transform, false);
//         RectTransform bgRect = bgGO.GetComponent<RectTransform>();
//         bgRect.anchorMin = new Vector2(0, 0.25f);
//         bgRect.anchorMax = new Vector2(0, 0.75f);
//         bgRect.sizeDelta = new Vector2(20, 20);
//         bgRect.anchoredPosition = new Vector2(10, 0);
//         Image bgImage = bgGO.GetComponent<Image>();
//         bgImage.color = Color.white * 0.8f;

//         GameObject checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
//         checkGO.transform.SetParent(bgGO.transform, false);
//         RectTransform checkRect = checkGO.GetComponent<RectTransform>();
//         checkRect.sizeDelta = new Vector2(20, 20);
//         checkRect.anchoredPosition = Vector2.zero;
//         Image checkImage = checkGO.GetComponent<Image>();
//         checkImage.color = Color.green;

//         GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
//         labelGO.transform.SetParent(toggleGO.transform, false);
//         RectTransform labelRect = labelGO.GetComponent<RectTransform>();
//         labelRect.anchorMin = new Vector2(0, 0);
//         labelRect.anchorMax = new Vector2(1, 1);
//         labelRect.offsetMin = new Vector2(40, 0);
//         labelRect.offsetMax = new Vector2(0, 0);
//         TextMeshProUGUI labelText = labelGO.GetComponent<TextMeshProUGUI>();
//         labelText.text = label;
//         labelText.fontSize = 24;
//         labelText.alignment = TextAlignmentOptions.Left;

//         Toggle toggle = toggleGO.GetComponent<Toggle>();
//         toggle.targetGraphic = bgImage;
//         toggle.graphic = checkImage;
//         toggle.isOn = false;

//         return toggleGO;
//     }

//     private static GameObject CreateSlider(Transform parent, string label, float min, float max, float value, Vector2 anchoredPos)
//     {
//         GameObject sliderGO = new GameObject(label + "_Slider", typeof(RectTransform), typeof(Slider), typeof(CanvasRenderer));
//         sliderGO.transform.SetParent(parent, false);
//         RectTransform rect = sliderGO.GetComponent<RectTransform>();
//         rect.sizeDelta = new Vector2(300, 40);
//         rect.anchoredPosition = anchoredPos;

//         GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
//         bgGO.transform.SetParent(sliderGO.transform, false);
//         RectTransform bgRect = bgGO.GetComponent<RectTransform>();
//         bgRect.anchorMin = new Vector2(0, 0.25f);
//         bgRect.anchorMax = new Vector2(1, 0.75f);
//         bgRect.sizeDelta = new Vector2(0, 20);
//         bgRect.anchoredPosition = Vector2.zero;
//         Image bgImage = bgGO.GetComponent<Image>();
//         bgImage.color = Color.white * 0.5f;

//         GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
//         fillAreaGO.transform.SetParent(sliderGO.transform, false);
//         RectTransform fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
//         fillAreaRect.anchorMin = new Vector2(0, 0.25f);
//         fillAreaRect.anchorMax = new Vector2(1, 0.75f);
//         fillAreaRect.sizeDelta = new Vector2(-20, 20);
//         fillAreaRect.anchoredPosition = new Vector2(10, 0);

//         GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
//         fillGO.transform.SetParent(fillAreaGO.transform, false);
//         RectTransform fillRect = fillGO.GetComponent<RectTransform>();
//         fillRect.sizeDelta = new Vector2(0, 20);
//         fillRect.anchorMin = new Vector2(0, 0);
//         fillRect.anchorMax = new Vector2(1, 1);
//         fillRect.anchoredPosition = Vector2.zero;
//         Image fillImage = fillGO.GetComponent<Image>();
//         fillImage.color = Color.green;

//         GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
//         handleAreaGO.transform.SetParent(sliderGO.transform, false);
//         RectTransform handleAreaRect = handleAreaGO.GetComponent<RectTransform>();
//         handleAreaRect.anchorMin = new Vector2(0, 0);
//         handleAreaRect.anchorMax = new Vector2(1, 1);
//         handleAreaRect.sizeDelta = new Vector2(0, 0);
//         handleAreaRect.anchoredPosition = Vector2.zero;

//         GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
//         handleGO.transform.SetParent(handleAreaGO.transform, false);
//         RectTransform handleRect = handleGO.GetComponent<RectTransform>();
//         handleRect.sizeDelta = new Vector2(20, 40);
//         handleRect.anchoredPosition = Vector2.zero;
//         Image handleImage = handleGO.GetComponent<Image>();
//         handleImage.color = Color.white;

//         GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
//         labelGO.transform.SetParent(sliderGO.transform, false);
//         RectTransform labelRect = labelGO.GetComponent<RectTransform>();
//         labelRect.anchorMin = new Vector2(1, 0);
//         labelRect.anchorMax = new Vector2(1, 1);
//         labelRect.pivot = new Vector2(0, 0.5f);
//         labelRect.anchoredPosition = new Vector2(30, 0);
//         labelRect.sizeDelta = new Vector2(80, 40);
//         TextMeshProUGUI labelText = labelGO.GetComponent<TextMeshProUGUI>();
//         labelText.text = label;
//         labelText.fontSize = 20;
//         labelText.alignment = TextAlignmentOptions.Left;

//         Slider slider = sliderGO.GetComponent<Slider>();
//         slider.minValue = min;
//         slider.maxValue = max;
//         slider.value = value;
//         slider.fillRect = fillRect;
//         slider.handleRect = handleRect;
//         slider.targetGraphic = handleImage;
//         slider.direction = Slider.Direction.LeftToRight;

//         return sliderGO;
//     }

//     private static GameObject CreateButton(Transform parent, string label, Vector2 anchoredPos)
//     {
//         GameObject buttonGO = new GameObject(label + "_Button", typeof(RectTransform), typeof(Button), typeof(CanvasRenderer), typeof(Image));
//         buttonGO.transform.SetParent(parent, false);
//         RectTransform rect = buttonGO.GetComponent<RectTransform>();
//         rect.sizeDelta = new Vector2(250, 40);
//         rect.anchoredPosition = anchoredPos;
//         Image btnImage = buttonGO.GetComponent<Image>();
//         btnImage.color = new Color(0.2f, 0.5f, 0.8f, 1f);

//         GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
//         labelGO.transform.SetParent(buttonGO.transform, false);
//         RectTransform labelRect = labelGO.GetComponent<RectTransform>();
//         labelRect.anchorMin = new Vector2(0, 0);
//         labelRect.anchorMax = new Vector2(1, 1);
//         labelRect.offsetMin = Vector2.zero;
//         labelRect.offsetMax = Vector2.zero;
//         TextMeshProUGUI labelText = labelGO.GetComponent<TextMeshProUGUI>();
//         labelText.text = label;
//         labelText.fontSize = 22;
//         labelText.alignment = TextAlignmentOptions.Center;

//         Button button = buttonGO.GetComponent<Button>();
//         button.targetGraphic = btnImage;

//         return buttonGO;
//     }
// }