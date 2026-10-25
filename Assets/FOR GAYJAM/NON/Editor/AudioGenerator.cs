using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace HorrorJam.UI.Editor
{
    public static class AudioGenerator
    {
        private const string AUDIO_DIR = "Assets/FOR GAYJAM/NON/Audio";

        [MenuItem("Tools/Horror UI/Generate UI Audio Files", priority = 2)]
        public static void GenerateAudioFiles()
        {
            if (!Directory.Exists(AUDIO_DIR))
            {
                Directory.CreateDirectory(AUDIO_DIR);
            }

            string hoverPath = $"{AUDIO_DIR}/UI_Hover.wav";
            string clickPath = $"{AUDIO_DIR}/UI_Click.wav";
            string flickerPath = $"{AUDIO_DIR}/Light_Flicker.wav";

            CreateHoverWav(hoverPath);
            CreateClickWav(clickPath);
            CreateFlickerWav(flickerPath);

            AssetDatabase.Refresh();

            AssignToScene();
            Debug.Log("[AudioGenerator] UI_Hover.wav, UI_Click.wav, and Light_Flicker.wav created and refreshed in AssetDatabase!");
        }

        private static void CreateHoverWav(string path)
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 40f);
                float freq = Mathf.Lerp(260f, 130f, t / duration);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                samples[i] = wave * env * 0.6f;
            }

            WriteWavFile(path, samples, sampleRate);
        }

        private static void CreateClickWav(string path)
        {
            int sampleRate = 44100;
            float duration = 0.07f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];

            System.Random rand = new System.Random(42);
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 55f);
                float freq = Mathf.Lerp(520f, 150f, t / duration);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 100f) * 0.35f;
                samples[i] = (wave * 0.7f + noise) * env * 0.85f;
            }

            WriteWavFile(path, samples, sampleRate);
        }

        private static void WriteWavFile(string path, float[] samples, int sampleRate)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int numSamples = samples.Length;
                int dataSize = numSamples * 2;
                int totalSize = 36 + dataSize;

                // RIFF
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(totalSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // fmt
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // subchunk1 size
                writer.Write((short)1); // PCM
                writer.Write((short)1); // Mono
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2); // Byte rate
                writer.Write((short)2); // Block align
                writer.Write((short)16); // Bits per sample

                // data
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                for (int i = 0; i < numSamples; i++)
                {
                    float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                    short val = (short)(clamped * 32767f);
                    writer.Write(val);
                }
            }
        }

        public static void AssignToScene()
        {
            AudioClip hover = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AUDIO_DIR}/UI_Hover.wav");
            AudioClip click = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AUDIO_DIR}/UI_Click.wav");
            AudioClip flicker = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AUDIO_DIR}/Light_Flicker.wav");

            MainMenuController controller = Object.FindFirstObjectByType<MainMenuController>();
            if (controller != null)
            {
                if (controller.hoverSfx == null) controller.hoverSfx = hover;
                if (controller.clickSfx == null) controller.clickSfx = click;
                if (controller.audioSource == null)
                {
                    controller.audioSource = controller.GetComponent<AudioSource>();
                    if (controller.audioSource == null)
                        controller.audioSource = controller.gameObject.AddComponent<AudioSource>();
                }
                controller.audioSource.playOnAwake = false;
                EditorUtility.SetDirty(controller);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                Debug.Log("[AudioGenerator] Successfully assigned hover and click SFX to MainMenuController in the active scene!");
            }

            FlashlightController flashlight = Object.FindFirstObjectByType<FlashlightController>();
            if (flashlight != null)
            {
                if (flashlight.flickerSfx == null) flashlight.flickerSfx = flicker;
                EditorUtility.SetDirty(flashlight);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(flashlight.gameObject.scene);
                Debug.Log("[AudioGenerator] Successfully assigned flicker SFX to FlashlightController!");
            }
        }

        private static void CreateFlickerWav(string path)
        {
            int sampleRate = 44100;
            float duration = 1.3f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            System.Random rand = new System.Random(99);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.3f;

                float burst = 0f;
                if (t >= 0.04f && t < 0.14f) burst = 0.7f;
                else if (t >= 0.20f && t < 0.34f) burst = 0.8f;
                else if (t >= 0.40f && t < 1.0f) burst = 0.95f;
                else if (t >= 1.0f && t < 1.2f) burst = 0.3f * Mathf.Exp(-(t - 1.0f) * 12f);

                float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.25f * burst;
                float spark = 0f;
                if ((t >= 0.04f && t < 0.07f) || (t >= 0.20f && t < 0.23f) || (t >= 0.40f && t < 0.44f))
                {
                    spark = ((float)rand.NextDouble() * 2f - 1f) * 0.7f;
                }

                samples[i] = Mathf.Clamp((hum * burst * 0.6f + noise + spark) * 0.85f, -1f, 1f);
            }

            WriteWavFile(path, samples, sampleRate);
        }
    }
}
