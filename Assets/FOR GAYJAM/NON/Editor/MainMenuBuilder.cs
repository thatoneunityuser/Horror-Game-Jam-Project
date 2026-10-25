using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace HorrorJam.UI.Editor
{
    public static class MainMenuBuilder
    {
        private const string SPRITES_DIR = "Assets/FOR GAYJAM/NON/MainMenuUI/Sprites";
        private const string FONTS_DIR = "Assets/FOR GAYJAM/NON/Fonts";
        private const string PREFAB_PATH = "Assets/FOR GAYJAM/NON/MainMenuUI/MainMenu.prefab";

        [MenuItem("Tools/Horror UI/Create Main Menu", priority = 10)]
        [MenuItem("GameObject/UI/Horror Main Menu", priority = 20)]
        public static void CreateMainMenuMenuEntry()
        {
            BuildMainMenu(true);
        }

        [MenuItem("Tools/Horror UI/Add Flashlight Effect to Current Menu", priority = 3)]
        public static void AddFlashlightToCurrentScene()
        {
            GameObject bgArt = GameObject.Find("Background_Art");
            if (bgArt == null)
            {
                MainMenuController mm = Object.FindFirstObjectByType<MainMenuController>();
                if (mm != null)
                {
                    Transform t = mm.transform.Find("Background_Art");
                    if (t != null) bgArt = t.gameObject;
                }
            }

            if (bgArt != null)
            {
                FlashlightController fc = bgArt.GetComponent<FlashlightController>();
                if (fc == null)
                {
                    fc = Undo.AddComponent<FlashlightController>(bgArt);
                }
                fc.diameter = 560f;
                fc.softness = 90f;
                fc.ambientDarkness = 0.0f;
                fc.enableFullScreenFlicker = true;
                if (fc.flickerSfx == null)
                {
                    fc.flickerSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/NON/Audio/Light_Flicker.wav");
                }
                EditorUtility.SetDirty(bgArt);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bgArt.scene);
                Debug.Log("[Horror UI] FlashlightController successfully added to Background_Art (560px diameter + Full Screen Flicker)!");
            }
            else
            {
                Debug.LogWarning("[Horror UI] Could not find Background_Art in the current scene.");
            }
        }

        public static GameObject BuildMainMenu(bool savePrefab)
        {
            // 1. Ensure EventSystem exists
            EnsureEventSystem();

            // 2. Ensure Canvas exists
            Canvas canvas = EnsureCanvas();

            // Check if existing menu exists under canvas
            Transform existing = canvas.transform.Find("MainMenu_Root");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            // 3. Load Resources
            Sprite bgArtSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Background_Art.png");
            Sprite vignetteSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Vignette_Oval.png");
            Sprite btnFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Button_Frame_9Slice.png");
            Sprite iconSmileySprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Icon_Smiley.png");
            Sprite iconSlidersSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Icon_Sliders.png");
            Sprite iconPowerSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Icon_Power.png");
            Sprite panelBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Panel_Background.png");

            TMP_FontAsset titleFont = FontAssetUtility.GetOrCreateFontAsset($"{FONTS_DIR}/Remnant-Regular.ttf", $"{FONTS_DIR}/Remnant-Regular SDF.asset");
            TMP_FontAsset buttonFont = FontAssetUtility.GetOrCreateFontAsset($"{FONTS_DIR}/PixelatedEleganceRegular.ttf", $"{FONTS_DIR}/PixelatedEleganceRegular SDF.asset");
            TMP_FontAsset versionFont = FontAssetUtility.GetOrCreateFontAsset($"{FONTS_DIR}/XTypewriter-Bold.ttf", $"{FONTS_DIR}/XTypewriter-Bold SDF.asset");
            TMP_FontAsset optionsFont = FontAssetUtility.GetOrCreateFontAsset($"{FONTS_DIR}/Coolvetica-Rg.otf", $"{FONTS_DIR}/Coolvetica-Rg SDF.asset");

            // Fallback font if any font failed to load
            TMP_FontAsset defaultTMPFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (defaultTMPFont == null) defaultTMPFont = TMP_Settings.defaultFontAsset;
            if (!FontAssetUtility.IsValidFontAsset(titleFont)) titleFont = defaultTMPFont;
            if (!FontAssetUtility.IsValidFontAsset(buttonFont)) buttonFont = defaultTMPFont;
            if (!FontAssetUtility.IsValidFontAsset(versionFont)) versionFont = defaultTMPFont;
            if (!FontAssetUtility.IsValidFontAsset(optionsFont)) optionsFont = defaultTMPFont;

            // 4. Create Root UI Object
            GameObject root = new GameObject("MainMenu_Root", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create Horror Main Menu");
            root.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchFull(rootRect);

            MainMenuController controller = root.AddComponent<MainMenuController>();
            AudioSource audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            controller.audioSource = audioSource;
            controller.hoverSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/NON/Audio/UI_Hover.wav");
            controller.clickSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FOR GAYJAM/NON/Audio/UI_Click.wav");

            // 5. Background Black Fill
            GameObject bgBlack = CreateUIObject("Background_Black", root.transform);
            Image imgBlack = bgBlack.AddComponent<Image>();
            imgBlack.color = new Color(0.04f, 0.03f, 0.05f, 1f);
            StretchFull(bgBlack.GetComponent<RectTransform>());

            // 6. Background Illustration Art
            GameObject bgArt = CreateUIObject("Background_Art", root.transform);
            Image imgArt = bgArt.AddComponent<Image>();
            if (bgArtSprite != null)
            {
                imgArt.sprite = bgArtSprite;
                imgArt.preserveAspect = true;
            }
            imgArt.color = Color.white;
            StretchFull(bgArt.GetComponent<RectTransform>());

            FlashlightController fc = bgArt.AddComponent<FlashlightController>();
            fc.diameter = 560f;
            fc.softness = 90f;
            fc.ambientDarkness = 0.0f;

            // 7. Vignette Overlay (Dark radial horror mask)
            GameObject vignette = CreateUIObject("Vignette_Overlay", root.transform);
            Image imgVig = vignette.AddComponent<Image>();
            if (vignetteSprite != null) imgVig.sprite = vignetteSprite;
            imgVig.color = new Color(0f, 0f, 0f, 0.92f);
            imgVig.raycastTarget = false;
            StretchFull(vignette.GetComponent<RectTransform>());

            // 8. Title Text (LOOKING UP I SEE ONLY A CEILING)
            GameObject titleObj = CreateUIObject("Title_Text", root.transform);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -80f);
            titleRect.sizeDelta = new Vector2(900f, 260f);

            TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.font = titleFont;
            titleTMP.text = "LOOKING UP I\nSEE ONLY A\nCEILING";
            titleTMP.fontSize = 72f;
            titleTMP.lineSpacing = -12f;
            titleTMP.alignment = TextAlignmentOptions.TopGeoAligned;
            titleTMP.color = new Color(0.96f, 0.94f, 0.95f, 1f);
            titleTMP.enableVertexGradient = false;

            // 9. Buttons Container (Centered in lower half matching reference)
            GameObject buttonsContainer = CreateUIObject("Buttons_Container", root.transform);
            RectTransform btnsRect = buttonsContainer.GetComponent<RectTransform>();
            btnsRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnsRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnsRect.pivot = new Vector2(0.5f, 0.5f);
            btnsRect.anchoredPosition = new Vector2(0f, -70f);
            btnsRect.sizeDelta = new Vector2(480f, 240f);

            VerticalLayoutGroup vLayout = buttonsContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 16f;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = false;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = false;
            vLayout.childForceExpandHeight = false;

            // Main Buttons: Load Checkpoint, Explore, Restart
            Button btnLoad = CreateMenuButton("Button_LoadCheckpoint", "Load Checkpoint", new Vector2(380f, 54f), buttonsContainer.transform, btnFrameSprite, buttonFont, 26f);
            Button btnExplore = CreateMenuButton("Button_Explore", "Explore", new Vector2(250f, 54f), buttonsContainer.transform, btnFrameSprite, buttonFont, 26f);
            Button btnRestart = CreateMenuButton("Button_Restart", "Restart", new Vector2(250f, 54f), buttonsContainer.transform, btnFrameSprite, buttonFont, 26f);

            controller.btnLoadCheckpoint = btnLoad;
            controller.btnExplore = btnExplore;
            controller.btnRestart = btnRestart;

            // 10. Bottom Bar (Version text & Icons)
            GameObject bottomBar = CreateUIObject("Bottom_Bar", root.transform);
            RectTransform bbRect = bottomBar.GetComponent<RectTransform>();
            bbRect.anchorMin = new Vector2(0f, 0f);
            bbRect.anchorMax = new Vector2(1f, 0f);
            bbRect.pivot = new Vector2(0.5f, 0f);
            bbRect.anchoredPosition = Vector2.zero;
            bbRect.sizeDelta = new Vector2(0f, 90f);

            // Version Text (1.0)
            GameObject verObj = CreateUIObject("Version_Text", bottomBar.transform);
            RectTransform verRect = verObj.GetComponent<RectTransform>();
            verRect.anchorMin = new Vector2(0.5f, 0.5f);
            verRect.anchorMax = new Vector2(0.5f, 0.5f);
            verRect.pivot = new Vector2(1f, 0.5f);
            verRect.anchoredPosition = new Vector2(-220f, 0f);
            verRect.sizeDelta = new Vector2(100f, 40f);

            TextMeshProUGUI verTMP = verObj.AddComponent<TextMeshProUGUI>();
            verTMP.font = versionFont;
            verTMP.text = "1.0";
            verTMP.fontSize = 28f;
            verTMP.alignment = TextAlignmentOptions.MidlineRight;
            verTMP.color = new Color(0.85f, 0.85f, 0.9f, 0.9f);

            // Icon Buttons Container
            GameObject iconGroup = CreateUIObject("Icon_Group", bottomBar.transform);
            RectTransform iconGroupRect = iconGroup.GetComponent<RectTransform>();
            iconGroupRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconGroupRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconGroupRect.pivot = new Vector2(0f, 0.5f);
            iconGroupRect.anchoredPosition = new Vector2(60f, 0f);
            iconGroupRect.sizeDelta = new Vector2(200f, 60f);

            HorizontalLayoutGroup hLayout = iconGroup.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 10f;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            Button btnSmiley = CreateIconButton("IconButton_Smiley", iconSmileySprite, new Vector2(50f, 50f), iconGroup.transform);
            Button btnSliders = CreateIconButton("IconButton_Settings", iconSlidersSprite, new Vector2(50f, 50f), iconGroup.transform);
            Button btnPower = CreateIconButton("IconButton_Power", iconPowerSprite, new Vector2(50f, 50f), iconGroup.transform);

            controller.btnSmiley = btnSmiley;
            controller.btnSettings = btnSliders;
            controller.btnQuit = btnPower;

            // 11. Settings Panel Popup
            GameObject settingsPanel = CreateSettingsPanel(root.transform, panelBgSprite, btnFrameSprite, buttonFont, optionsFont, controller);
            controller.settingsPanel = settingsPanel;

            // 12. Credits Panel Popup
            GameObject creditsPanel = CreateCreditsPanel(root.transform, panelBgSprite, btnFrameSprite, buttonFont, optionsFont, controller);
            controller.creditsPanel = creditsPanel;

            // 13. Quit Confirmation Dialog
            GameObject quitDialog = CreateQuitDialog(root.transform, panelBgSprite, btnFrameSprite, buttonFont, controller);
            controller.quitDialog = quitDialog;

            // 14. Screen Fader Overlay
            GameObject faderObj = CreateUIObject("Screen_Fader", root.transform);
            CanvasGroup faderGroup = faderObj.AddComponent<CanvasGroup>();
            faderGroup.alpha = 0f;
            faderGroup.blocksRaycasts = false;
            Image faderImg = faderObj.AddComponent<Image>();
            faderImg.color = Color.black;
            StretchFull(faderObj.GetComponent<RectTransform>());
            controller.screenFader = faderGroup;

            // 15. Save as Prefab if requested
            if (savePrefab)
            {
                string dir = System.IO.Path.GetDirectoryName(PREFAB_PATH);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PREFAB_PATH, InteractionMode.AutomatedAction);
                Debug.Log($"[Horror UI] Main menu prefab saved to: {PREFAB_PATH}");
            }

            // Mark active scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            Debug.Log("[Horror UI] Main Menu created successfully!");
            return root;
        }

        private static Button CreateMenuButton(string name, string text, Vector2 size, Transform parent, Sprite frameSprite, TMP_FontAsset font, float fontSize)
        {
            GameObject btnObj = CreateUIObject(name, parent);
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

            // Button Text
            GameObject txtObj = CreateUIObject("Text", btnObj.transform);
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

            // Add MenuButtonEffects
            MenuButtonEffects fx = btnObj.AddComponent<MenuButtonEffects>();
            fx.borderImage = img;
            fx.buttonText = tmp;

            return btn;
        }

        private static Button CreateIconButton(string name, Sprite iconSprite, Vector2 size, Transform parent)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            if (iconSprite != null)
            {
                img.sprite = iconSprite;
                img.preserveAspect = true;
            }
            img.color = new Color(0.9f, 0.9f, 0.92f, 0.9f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            MenuButtonEffects fx = btnObj.AddComponent<MenuButtonEffects>();
            fx.borderImage = img;
            fx.hoverScale = 1.08f;
            fx.enableProgressiveRed = false;

            return btn;
        }

        private static GameObject CreateSettingsPanel(Transform parent, Sprite panelBg, Sprite btnFrame, TMP_FontAsset btnFont, TMP_FontAsset optFont, MainMenuController controller)
        {
            GameObject panel = CreateUIObject("Settings_Panel", parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(580f, 440f);

            Image bg = panel.AddComponent<Image>();
            if (panelBg != null) { bg.sprite = panelBg; bg.type = Image.Type.Sliced; }
            bg.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);

            // Title
            GameObject title = CreateUIObject("Header", panel.transform);
            RectTransform tRect = title.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 1f);
            tRect.anchorMax = new Vector2(0.5f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -24f);
            tRect.sizeDelta = new Vector2(400f, 50f);
            TextMeshProUGUI tTMP = title.AddComponent<TextMeshProUGUI>();
            tTMP.font = btnFont;
            tTMP.text = "SETTINGS";
            tTMP.fontSize = 32f;
            tTMP.alignment = TextAlignmentOptions.Center;

            // Volume Row
            GameObject volLabel = CreateUIObject("Vol_Label", panel.transform);
            RectTransform vlRect = volLabel.GetComponent<RectTransform>();
            vlRect.anchoredPosition = new Vector2(-120f, 60f);
            vlRect.sizeDelta = new Vector2(240f, 30f);
            TextMeshProUGUI vlTMP = volLabel.AddComponent<TextMeshProUGUI>();
            vlTMP.font = optFont;
            vlTMP.text = "Master Volume";
            vlTMP.fontSize = 22f;
            vlTMP.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject sliderObj = CreateUIObject("Volume_Slider", panel.transform);
            RectTransform slRect = sliderObj.GetComponent<RectTransform>();
            slRect.anchoredPosition = new Vector2(80f, 60f);
            slRect.sizeDelta = new Vector2(200f, 24f);
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            // Slider Background
            GameObject slBg = CreateUIObject("Background", sliderObj.transform);
            Image slBgImg = slBg.AddComponent<Image>();
            slBgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            StretchFull(slBg.GetComponent<RectTransform>());

            // Slider Fill Area
            GameObject fillArea = CreateUIObject("Fill Area", sliderObj.transform);
            RectTransform faRect = fillArea.GetComponent<RectTransform>();
            StretchFull(faRect);
            faRect.offsetMin = new Vector2(5f, 0f);
            faRect.offsetMax = new Vector2(-5f, 0f);

            GameObject fill = CreateUIObject("Fill", fillArea.transform);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            StretchFull(fill.GetComponent<RectTransform>());
            slider.fillRect = fill.GetComponent<RectTransform>();

            // Fullscreen Toggle Row
            GameObject fsLabel = CreateUIObject("FS_Label", panel.transform);
            RectTransform fslRect = fsLabel.GetComponent<RectTransform>();
            fslRect.anchoredPosition = new Vector2(-120f, 0f);
            fslRect.sizeDelta = new Vector2(240f, 30f);
            TextMeshProUGUI fslTMP = fsLabel.AddComponent<TextMeshProUGUI>();
            fslTMP.font = optFont;
            fslTMP.text = "Fullscreen";
            fslTMP.fontSize = 22f;
            fslTMP.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject toggleObj = CreateUIObject("Fullscreen_Toggle", panel.transform);
            RectTransform tgRect = toggleObj.GetComponent<RectTransform>();
            tgRect.anchoredPosition = new Vector2(80f, 0f);
            tgRect.sizeDelta = new Vector2(30f, 30f);
            Toggle toggle = toggleObj.AddComponent<Toggle>();
            Image tgBg = toggleObj.AddComponent<Image>();
            tgBg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            toggle.targetGraphic = tgBg;

            GameObject checkObj = CreateUIObject("Checkmark", toggleObj.transform);
            Image checkImg = checkObj.AddComponent<Image>();
            checkImg.color = Color.white;
            StretchFull(checkObj.GetComponent<RectTransform>());
            checkObj.GetComponent<RectTransform>().offsetMin = new Vector2(4f, 4f);
            checkObj.GetComponent<RectTransform>().offsetMax = new Vector2(-4f, -4f);
            toggle.graphic = checkImg;

            // Close Button
            Button btnBack = CreateMenuButton("Btn_Back", "BACK", new Vector2(160f, 48f), panel.transform, btnFrame, btnFont, 22f);
            RectTransform backRect = btnBack.GetComponent<RectTransform>();
            backRect.anchoredPosition = new Vector2(0f, -150f);

            controller.volumeSlider = slider;
            controller.fullscreenToggle = toggle;
            controller.btnCloseSettings = btnBack;

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateCreditsPanel(Transform parent, Sprite panelBg, Sprite btnFrame, TMP_FontAsset btnFont, TMP_FontAsset optFont, MainMenuController controller)
        {
            GameObject panel = CreateUIObject("Credits_Panel", parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(580f, 400f);

            Image bg = panel.AddComponent<Image>();
            if (panelBg != null) { bg.sprite = panelBg; bg.type = Image.Type.Sliced; }
            bg.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);

            // Title
            GameObject title = CreateUIObject("Header", panel.transform);
            RectTransform tRect = title.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 1f);
            tRect.anchorMax = new Vector2(0.5f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -24f);
            tRect.sizeDelta = new Vector2(400f, 50f);
            TextMeshProUGUI tTMP = title.AddComponent<TextMeshProUGUI>();
            tTMP.font = btnFont;
            tTMP.text = "ABOUT";
            tTMP.fontSize = 32f;
            tTMP.alignment = TextAlignmentOptions.Center;

            // Body
            GameObject body = CreateUIObject("BodyText", panel.transform);
            RectTransform bRect = body.GetComponent<RectTransform>();
            bRect.anchoredPosition = new Vector2(0f, 15f);
            bRect.sizeDelta = new Vector2(480f, 180f);
            TextMeshProUGUI bTMP = body.AddComponent<TextMeshProUGUI>();
            bTMP.font = optFont;
            bTMP.text = "CREATED FOR HORROR GAME JAM\n\n- Template UI System -\nChange titles, buttons, art, and icons as you wish.\n\nThank you for playing!";
            bTMP.fontSize = 20f;
            bTMP.alignment = TextAlignmentOptions.Center;
            bTMP.lineSpacing = 10f;
            bTMP.color = new Color(0.85f, 0.85f, 0.9f, 0.95f);

            // Close Button
            Button btnBack = CreateMenuButton("Btn_Back", "BACK", new Vector2(160f, 48f), panel.transform, btnFrame, btnFont, 22f);
            RectTransform backRect = btnBack.GetComponent<RectTransform>();
            backRect.anchoredPosition = new Vector2(0f, -140f);

            controller.btnCloseCredits = btnBack;

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateQuitDialog(Transform parent, Sprite panelBg, Sprite btnFrame, TMP_FontAsset btnFont, MainMenuController controller)
        {
            GameObject panel = CreateUIObject("Quit_Dialog", parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(480f, 260f);

            Image bg = panel.AddComponent<Image>();
            if (panelBg != null) { bg.sprite = panelBg; bg.type = Image.Type.Sliced; }
            bg.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);

            // Title
            GameObject title = CreateUIObject("Header", panel.transform);
            RectTransform tRect = title.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 1f);
            tRect.anchorMax = new Vector2(0.5f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -30f);
            tRect.sizeDelta = new Vector2(360f, 50f);
            TextMeshProUGUI tTMP = title.AddComponent<TextMeshProUGUI>();
            tTMP.font = btnFont;
            tTMP.text = "QUIT GAME?";
            tTMP.fontSize = 28f;
            tTMP.alignment = TextAlignmentOptions.Center;

            // Buttons: Yes & No
            Button btnYes = CreateMenuButton("Btn_Yes", "YES", new Vector2(130f, 46f), panel.transform, btnFrame, btnFont, 20f);
            RectTransform yr = btnYes.GetComponent<RectTransform>();
            yr.anchoredPosition = new Vector2(-80f, -40f);

            Button btnNo = CreateMenuButton("Btn_No", "NO", new Vector2(130f, 46f), panel.transform, btnFrame, btnFont, 20f);
            RectTransform nr = btnNo.GetComponent<RectTransform>();
            nr.anchoredPosition = new Vector2(80f, -40f);

            controller.btnConfirmQuit = btnYes;
            controller.btnCancelQuit = btnNo;

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Canvas EnsureCanvas()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            }
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
            }
        }
    }
}
