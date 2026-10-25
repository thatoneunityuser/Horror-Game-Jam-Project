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

            // 3. Flashlight Container (Bottom-Right: Minimalist ON / OFF status, NO battery bar)
            GameObject flashContainer = new GameObject("Flashlight_Container");
            flashContainer.transform.SetParent(rootGo.transform, false);

            RectTransform flashRect = flashContainer.AddComponent<RectTransform>();
            flashRect.anchorMin = new Vector2(1, 0);
            flashRect.anchorMax = new Vector2(1, 0);
            flashRect.pivot = new Vector2(1, 0);
            flashRect.anchoredPosition = new Vector2(-40, 40);
            flashRect.sizeDelta = new Vector2(200, 65);

            // Subtle dark background panel for contrast
            Image flashBg = flashContainer.AddComponent<Image>();
            flashBg.color = new Color(0.08f, 0.08f, 0.10f, 0.55f);
            flashBg.raycastTarget = false;

            // Header Label: "FLASHLIGHT"
            GameObject labelGo = new GameObject("Flashlight_Label");
            labelGo.transform.SetParent(flashContainer.transform, false);

            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.55f);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(12, 0);
            labelRect.offsetMax = new Vector2(-12, -4);

            TextMeshProUGUI labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = "FLASHLIGHT";
            labelTmp.fontSize = 15;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.characterSpacing = 8;
            labelTmp.color = new Color(0.7f, 0.72f, 0.78f, 0.75f);
            labelTmp.alignment = TextAlignmentOptions.Left;
            if (fontAsset != null) labelTmp.font = fontAsset;

            // Status Row Container
            GameObject statusRow = new GameObject("Status_Row");
            statusRow.transform.SetParent(flashContainer.transform, false);

            RectTransform rowRect = statusRow.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0, 0);
            rowRect.anchorMax = new Vector2(1, 0.55f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.offsetMin = new Vector2(12, 4);
            rowRect.offsetMax = new Vector2(-12, 0);

            // LED Dot Indicator
            GameObject dotGo = new GameObject("LED_Dot");
            dotGo.transform.SetParent(statusRow.transform, false);

            RectTransform dotRect = dotGo.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0, 0.5f);
            dotRect.anchorMax = new Vector2(0, 0.5f);
            dotRect.pivot = new Vector2(0, 0.5f);
            dotRect.sizeDelta = new Vector2(10, 10);
            dotRect.anchoredPosition = new Vector2(0, 0);

            Image dotImg = dotGo.AddComponent<Image>();
            dotImg.color = new Color(0.3f, 1f, 0.5f, 1f);
            dotImg.raycastTarget = false;

            // Status Text: "ON" / "OFF"
            GameObject statusTextGo = new GameObject("Status_Text");
            statusTextGo.transform.SetParent(statusRow.transform, false);

            RectTransform statRect = statusTextGo.AddComponent<RectTransform>();
            statRect.anchorMin = new Vector2(0, 0);
            statRect.anchorMax = new Vector2(1, 1);
            statRect.pivot = new Vector2(0, 0.5f);
            statRect.offsetMin = new Vector2(20, 0);
            statRect.offsetMax = new Vector2(0, 0);

            TextMeshProUGUI statusTmp = statusTextGo.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "ON";
            statusTmp.fontSize = 24;
            statusTmp.fontStyle = FontStyles.Bold;
            statusTmp.alignment = TextAlignmentOptions.Left;
            statusTmp.color = new Color(0.95f, 0.98f, 1f, 1f);
            if (fontAsset != null) statusTmp.font = fontAsset;

            // Setup FlashlightUI
            FlashlightUI flashlightUI = flashContainer.AddComponent<FlashlightUI>();
            flashlightUI.statusText = statusTmp;
            flashlightUI.labelText = labelTmp;
            flashlightUI.indicatorDot = dotImg;
            flashlightUI.toggleKey = KeyCode.T;
            flashlightUI.isFlashlightOn = true;
            hudManager.flashlightUI = flashlightUI;

            // 4. Save as Prefab
            PrefabUtility.SaveAsPrefabAssetAndConnect(rootGo, PREFAB_PATH, InteractionMode.AutomatedAction);
            Debug.Log($"[GameplayHUDBuilder] Saved Prefab successfully to {PREFAB_PATH}!");

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
