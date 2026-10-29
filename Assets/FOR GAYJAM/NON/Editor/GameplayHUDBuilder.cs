using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace HorrorJam.UI.Editor
{
    public static class GameplayHUDBuilder
    {
        private const string PREFAB_PATH = "Assets/FOR GAYJAM/NON/GameplayHUD.prefab";
        private const string FONTS_FOLDER = "Assets/FOR GAYJAM/NON/Font non";

        [MenuItem("Tools/Horror UI/Create or Update Gameplay HUD", priority = 1)]
        [MenuItem("GameObject/UI/Horror Gameplay HUD", priority = 21)]
        public static void CreateGameplayHUDMenuEntry()
        {
            BuildGameplayHUD(true);
        }

        [InitializeOnLoadMethod]
        private static void AutoEnsurePrefabExists()
        {
            // Auto-generates prefab in background if not already present
            EditorApplication.delayCall += () =>
            {
                if (!System.IO.File.Exists(PREFAB_PATH))
                {
                    BuildGameplayHUD(false);
                }
            };
        }

        public static GameObject BuildGameplayHUD(bool instantiateInScene = true)
        {
            TMP_FontAsset fontAsset = LoadHorrorFont();

            // 1. Root Canvas
            GameObject rootGo = new GameObject("Gameplay_HUD_Canvas");
            Canvas canvas = rootGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = rootGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            rootGo.AddComponent<GraphicRaycaster>();
            GameplayHUDManager hudManager = rootGo.AddComponent<GameplayHUDManager>();

            // 2. Room Counter Container (Top-Right: Pure white raw number)
            GameObject roomContainer = new GameObject("RoomCounter_Container");
            roomContainer.transform.SetParent(rootGo.transform, false);

            RectTransform roomRect = roomContainer.AddComponent<RectTransform>();
            roomRect.anchorMin = new Vector2(1, 1);
            roomRect.anchorMax = new Vector2(1, 1);
            roomRect.pivot = new Vector2(1, 1);
            roomRect.anchoredPosition = new Vector2(-40, -40);
            roomRect.sizeDelta = new Vector2(200, 90);

            // Room Number Text (Raw, Plain, Clean White)
            GameObject numberGo = new GameObject("RoomNumber_Text");
            numberGo.transform.SetParent(roomContainer.transform, false);

            RectTransform numRect = numberGo.AddComponent<RectTransform>();
            numRect.anchorMin = Vector2.zero;
            numRect.anchorMax = Vector2.one;
            numRect.pivot = new Vector2(1, 1);
            numRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI numberTmp = numberGo.AddComponent<TextMeshProUGUI>();
            numberTmp.text = "1";
            numberTmp.fontSize = 76;
            numberTmp.fontStyle = FontStyles.Bold;
            numberTmp.alignment = TextAlignmentOptions.TopRight;
            numberTmp.color = Color.white; // สีขาวล้วนตามที่ขอ
            if (fontAsset != null) numberTmp.font = fontAsset;

            // เพิ่ม Outline บางๆ ให้ตัวเลขชัดเจนบนทุกฉาก
            numberTmp.outlineWidth = 0.15f;
            numberTmp.outlineColor = new Color32(0, 0, 0, 200);

            RoomCounterUI roomUI = roomContainer.AddComponent<RoomCounterUI>();
            roomUI.roomNumberText = numberTmp;
            roomUI.defaultColor = Color.white;
            hudManager.roomCounterUI = roomUI;

            // 3. Flashlight Icon (Bottom-Right: Pure Icon-Only Minimalist HUD)
            GameObject flashContainer = new GameObject("Flashlight_Icon");
            flashContainer.transform.SetParent(rootGo.transform, false);

            RectTransform flashRect = flashContainer.AddComponent<RectTransform>();
            flashRect.anchorMin = new Vector2(1, 0);
            flashRect.anchorMax = new Vector2(1, 0);
            flashRect.pivot = new Vector2(1, 0);
            flashRect.anchoredPosition = new Vector2(-50, 50);
            flashRect.sizeDelta = new Vector2(56, 56);

            // Load Flashlight Icon Sprites
            Sprite iconOnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/Flashlight_Icon_On.png");
            Sprite iconOffSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/Flashlight_Icon_Off.png");

            Image iconImg = flashContainer.AddComponent<Image>();
            iconImg.sprite = (iconOnSprite != null) ? iconOnSprite : iconOffSprite;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
            iconImg.raycastTarget = true;

            // Setup FlashlightUI directly on the icon
            FlashlightUI flashlightUI = flashContainer.AddComponent<FlashlightUI>();
            flashlightUI.flashlightIcon = iconImg;
            flashlightUI.iconOnSprite = iconOnSprite;
            flashlightUI.iconOffSprite = iconOffSprite;
            flashlightUI.onColor = Color.white;
            flashlightUI.offColor = new Color(0.6f, 0.6f, 0.65f, 0.35f);
            flashlightUI.toggleKey = KeyCode.T;
            flashlightUI.allowClickOnIcon = true;
            flashlightUI.isFlashlightOn = true;
            hudManager.flashlightUI = flashlightUI;

            // 4. Ghost Warning Indicator System (ลูกศรเตือนผีขอบจอ)
            GameObject warningContainer = new GameObject("GhostWarning_Container");
            warningContainer.transform.SetParent(rootGo.transform, false);

            RectTransform warnRect = warningContainer.AddComponent<RectTransform>();
            warnRect.anchorMin = Vector2.zero;
            warnRect.anchorMax = Vector2.one;
            warnRect.sizeDelta = Vector2.zero;
            warnRect.anchoredPosition = Vector2.zero;

            GhostWarningManager warningManager = rootGo.AddComponent<GhostWarningManager>();
            warningManager.arrowContainer = warnRect;
            warningManager.arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/Non import/Sprites/GhostArrowIndicator.png");
            warningManager.autoTrackRushEntity = true;
            warningManager.defaultArrowColor = new Color(1f, 0.15f, 0.15f, 1f);
            hudManager.ghostWarningManager = warningManager;

            // 5. Stamina Bar (Bottom edge of screen: Pure white bar that fades when not running)
            GameObject staminaGo = new GameObject("StaminaBar_HUD", typeof(RectTransform));
            staminaGo.transform.SetParent(rootGo.transform, false);
            StaminaBarUI staminaUI = staminaGo.AddComponent<StaminaBarUI>();
            staminaUI.EnsureComponents();
            hudManager.staminaBarUI = staminaUI;

            // 6. Horror Warning Sign System (Top Center: 3 warning types)
            GameObject warningSignGo = new GameObject("HorrorWarningSign_HUD", typeof(RectTransform));
            warningSignGo.transform.SetParent(rootGo.transform, false);
            HorrorWarningSignUI warningSignUI = warningSignGo.AddComponent<HorrorWarningSignUI>();
            warningSignUI.EnsureComponents();
            hudManager.horrorWarningSignUI = warningSignUI;

            // 7. Sniff Ghost HUD (SecondEventGhost / Fat Rat / Lockers)
            GameObject sniffGhostGo = new GameObject("SniffGhost_HUD", typeof(RectTransform));
            sniffGhostGo.transform.SetParent(rootGo.transform, false);
            RectTransform sniffRect = sniffGhostGo.GetComponent<RectTransform>();
            StretchFull(sniffRect);
            SniffGhostHUD sniffHUD = sniffGhostGo.AddComponent<SniffGhostHUD>();
            sniffHUD.BuildUIHierarchyIfNeeded();
            hudManager.sniffGhostHUD = sniffHUD;

            // 8. Pause Menu System (ESC Pause Menu matching Main Menu button style)
            BuildPauseMenu(rootGo, hudManager);

            // 8. Save as Prefab to all game HUD prefab paths
            PrefabUtility.SaveAsPrefabAssetAndConnect(rootGo, PREFAB_PATH, InteractionMode.AutomatedAction);
            Debug.Log($"[GameplayHUDBuilder] Saved Prefab successfully to {PREFAB_PATH}!");

            string mewwwPrefabPath = "Assets/FOR GAYJAM/MEWWW/Game UI.prefab";
            PrefabUtility.SaveAsPrefabAsset(rootGo, mewwwPrefabPath);
            Debug.Log($"[GameplayHUDBuilder] Saved Prefab successfully to {mewwwPrefabPath}!");

            string altPrefabPath = "Assets/FOR GAYJAM/NON/UI.prefab";
            PrefabUtility.SaveAsPrefabAsset(rootGo, altPrefabPath);
            Debug.Log($"[GameplayHUDBuilder] Saved Prefab successfully to {altPrefabPath}!");

            if (!instantiateInScene)
            {
                Object.DestroyImmediate(rootGo);
                return null;
            }

            Undo.RegisterCreatedObjectUndo(rootGo, "Create Gameplay HUD");
            Selection.activeGameObject = rootGo;
            return rootGo;
        }

        private static void BuildPauseMenu(GameObject rootGo, GameplayHUDManager hudManager)
        {
            Sprite btnFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/MainMenuUI/Sprites/Button_Frame_9Slice.png");
            Sprite vignetteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FOR GAYJAM/NON/MainMenuUI/Sprites/Vignette_Oval.png");
            AudioClip hoverClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/NON/Audio/UI_Hover.wav");
            AudioClip clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/NON/Audio/UI_Click.wav");

            TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FOR GAYJAM/NON/Fonts/Remnant-Regular SDF.asset");
            TMP_FontAsset buttonFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FOR GAYJAM/NON/Fonts/PixelatedEleganceRegular SDF.asset");
            if (buttonFont == null) buttonFont = LoadHorrorFont();
            if (titleFont == null) titleFont = buttonFont;

            GameObject pauseRoot = new GameObject("Pause_Menu_Root", typeof(RectTransform));
            pauseRoot.transform.SetParent(rootGo.transform, false);

            RectTransform rootRect = pauseRoot.GetComponent<RectTransform>();
            StretchFull(rootRect);

            PauseMenuController pauseCtrl = pauseRoot.AddComponent<PauseMenuController>();
            AudioSource audioSource = pauseRoot.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true;
            pauseCtrl.audioSource = audioSource;
            pauseCtrl.hoverSfx = hoverClip;
            pauseCtrl.clickSfx = clickClip;
            pauseCtrl.mainMenuSceneName = "Menu GayJam";

            // Container overlay for pause visuals
            GameObject pausePanel = new GameObject("Pause_Panel_Overlay", typeof(RectTransform));
            pausePanel.transform.SetParent(pauseRoot.transform, false);
            StretchFull(pausePanel.GetComponent<RectTransform>());

            // 1. Dark Backdrop (Blocks raycasts to underlying gameplay)
            GameObject backdrop = new GameObject("Dark_Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(pausePanel.transform, false);
            StretchFull(backdrop.GetComponent<RectTransform>());
            Image bgImg = backdrop.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.03f, 0.05f, 0.88f);
            bgImg.raycastTarget = true;

            // 2. Vignette Overlay
            GameObject vigObj = new GameObject("Vignette_Overlay", typeof(RectTransform));
            vigObj.transform.SetParent(pausePanel.transform, false);
            StretchFull(vigObj.GetComponent<RectTransform>());
            Image vigImg = vigObj.AddComponent<Image>();
            if (vignetteSprite != null) vigImg.sprite = vignetteSprite;
            vigImg.color = new Color(0f, 0f, 0f, 0.92f);
            vigImg.raycastTarget = false;

            // 3. Pause Title Text
            GameObject titleObj = new GameObject("Pause_Title", typeof(RectTransform));
            titleObj.transform.SetParent(pausePanel.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 130f);
            titleRect.sizeDelta = new Vector2(600f, 100f);

            TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.font = titleFont;
            titleTMP.text = "PAUSED";
            titleTMP.fontSize = 62f;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color = new Color(0.96f, 0.94f, 0.95f, 1f);

            // 4. Buttons Container
            GameObject btnsContainer = new GameObject("Buttons_Container", typeof(RectTransform));
            btnsContainer.transform.SetParent(pausePanel.transform, false);
            RectTransform btnsRect = btnsContainer.GetComponent<RectTransform>();
            btnsRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnsRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnsRect.pivot = new Vector2(0.5f, 0.5f);
            btnsRect.anchoredPosition = new Vector2(0f, -30f);
            btnsRect.sizeDelta = new Vector2(360f, 180f);

            VerticalLayoutGroup vLayout = btnsContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 18f;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = false;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = false;
            vLayout.childForceExpandHeight = false;

            // Resume Button
            Button btnResume = CreateMenuButton("Button_Resume", "Resume", new Vector2(260f, 54f), btnsContainer.transform, btnFrameSprite, buttonFont, 26f);
            // Main Menu Button
            Button btnMainMenu = CreateMenuButton("Button_MainMenu", "Main Menu", new Vector2(260f, 54f), btnsContainer.transform, btnFrameSprite, buttonFont, 26f);

            pauseCtrl.btnResume = btnResume;
            pauseCtrl.btnMainMenu = btnMainMenu;
            pauseCtrl.pausePanel = pausePanel;

            // 5. Screen Fader (Direct child of pauseRoot)
            GameObject faderObj = new GameObject("Screen_Fader", typeof(RectTransform));
            faderObj.transform.SetParent(pauseRoot.transform, false);
            StretchFull(faderObj.GetComponent<RectTransform>());
            CanvasGroup faderGroup = faderObj.AddComponent<CanvasGroup>();
            faderGroup.alpha = 0f;
            faderGroup.blocksRaycasts = false;
            Image faderImg = faderObj.AddComponent<Image>();
            faderImg.color = Color.black;
            faderImg.raycastTarget = false;
            pauseCtrl.screenFader = faderGroup;

            hudManager.pauseMenuController = pauseCtrl;

            // Overlay starts inactive, but pauseRoot controller stays active to listen for ESC
            pausePanel.SetActive(false);
            pauseRoot.SetActive(true);
        }

        private static Button CreateMenuButton(string name, string text, Vector2 size, Transform parent, Sprite frameSprite, TMP_FontAsset font, float fontSize)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            if (frameSprite != null)
            {
                img.sprite = frameSprite;
                img.type = Image.Type.Sliced;
            }
            img.color = new Color(0.9f, 0.9f, 0.92f, 0.85f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            StretchFull(txtObj.GetComponent<RectTransform>());

            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            if (font != null && font.material != null)
            {
                tmp.fontSharedMaterial = font.material;
            }
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.92f, 0.92f, 0.95f, 1f);

            MenuButtonEffects fx = btnObj.AddComponent<MenuButtonEffects>();
            fx.borderImage = img;
            fx.buttonText = tmp;

            return btn;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static TMP_FontAsset LoadHorrorFont()
        {
            // 1. ลองหาฟอนต์ยอดนิยมในโฟลเดอร์ Font non ก่อน
            string[] preferredFonts = new string[]
            {
                $"{FONTS_FOLDER}/XTypewriter-Bold SDF.asset",
                $"{FONTS_FOLDER}/PixelatedEleganceRegular-ovawB SDF.asset",
                $"{FONTS_FOLDER}/Remnant-Regular SDF.asset",
                $"{FONTS_FOLDER}/CREATURE SDF.asset"
            };

            foreach (string path in preferredFonts)
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null) return font;
            }

            // 2. ถ้าไม่เจอ ให้ค้นหาฟอนต์ SDF ใดๆ ในโปรเจกต์
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }

            return null;
        }
    }
}
// Trigger reload
// Rebuild icon-only HUD
