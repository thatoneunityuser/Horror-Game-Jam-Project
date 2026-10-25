using UnityEngine;
using UnityEditor;
using TMPro;

namespace HorrorJam.UI.Editor
{
    public static class FontAssetUtility
    {
        private const string FONTS_DIR = "Assets/FOR GAYJAM/NON/Fonts";

        [MenuItem("Tools/Horror UI/Fix & Refresh All Text", priority = 1)]
        [MenuItem("Tools/Horror UI/Regenerate Font Assets", priority = 5)]
        public static void EnsureAllFontsValid()
        {
            UpdateSceneTextMeshPros();
            AssetDatabase.SaveAssets();
            Debug.Log("[FontAssetUtility] All scene TextMeshPro components verified and refreshed!");
        }

        public static TMP_FontAsset GetOrCreateFontAsset(string fontPath, string targetAssetPath)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
            if (IsValidFontAsset(existing))
            {
                return existing;
            }

            TMP_FontAsset fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (fallback == null) fallback = TMP_Settings.defaultFontAsset;
            return existing != null ? existing : fallback;
        }

        public static bool IsValidFontAsset(TMP_FontAsset asset)
        {
            if (asset == null) return false;
            if (asset.atlasTextures == null || asset.atlasTextures.Length == 0) return false;
            if (asset.atlasTextures[0] == null) return false;
            if (asset.material == null) return false;
            if (asset.characterTable == null || asset.characterTable.Count == 0) return false;
            return true;
        }

        public static void UpdateSceneTextMeshPros()
        {
            TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FONTS_DIR}/Remnant-Regular SDF.asset");
            TMP_FontAsset buttonFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FONTS_DIR}/PixelatedEleganceRegular SDF.asset");
            TMP_FontAsset versionFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FONTS_DIR}/XTypewriter-Bold SDF.asset");
            TMP_FontAsset optionsFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FONTS_DIR}/Coolvetica-Rg SDF.asset");

            TMP_FontAsset fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (fallback == null) fallback = TMP_Settings.defaultFontAsset;

            // Invalid fonts become null so existing text is left untouched instead of being overwritten with the fallback.
            if (!IsValidFontAsset(titleFont)) titleFont = null;
            if (!IsValidFontAsset(buttonFont)) buttonFont = null;
            if (!IsValidFontAsset(versionFont)) versionFont = null;
            if (!IsValidFontAsset(optionsFont)) optionsFont = null;

            TextMeshProUGUI[] tmps = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tmp in tmps)
            {
                TMP_FontAsset targetFont = buttonFont;
                if (tmp.gameObject.name.Contains("Title"))
                    targetFont = titleFont;
                else if (tmp.gameObject.name.Contains("Version"))
                    targetFont = versionFont;
                else if (tmp.gameObject.name.Contains("Label") || tmp.gameObject.name.Contains("Body"))
                    targetFont = optionsFont;

                if (targetFont != null)
                {
                    tmp.font = targetFont;
                    if (targetFont.material != null)
                    {
                        tmp.fontSharedMaterial = targetFont.material;
                    }
                }

                tmp.SetAllDirty();
                EditorUtility.SetDirty(tmp);
            }

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }
    }
}
