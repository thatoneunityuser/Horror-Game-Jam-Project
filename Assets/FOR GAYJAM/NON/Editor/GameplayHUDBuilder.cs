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

            // 7. Save as Prefab to all game HUD prefab paths
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
