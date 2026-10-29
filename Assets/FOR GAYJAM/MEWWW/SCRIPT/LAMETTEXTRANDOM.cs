using System.Collections;
using UnityEngine;
using TMPro;

public class LAMETTEXTRANDOM : MonoBehaviour
{
    public static LAMETTEXTRANDOM instance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    [Header("On / Off")]
    public bool isActive = true;

    [Header("Text Settings")]
    [TextArea(3, 10)]
    public string[] textPool = new string[]
    {
        "ARAYA",
        "DARKNESS",
        "SOUL",
        "BEYOND",
        "ETERNAL",
        "VOID",
        "LAMENT",
        "SHADOW"
    };

    [Header("Typewriter Settings")]
    public float typewriterSpeed = 0.05f;   // seconds per character

    [Header("Position & Rotation Settings")]
    public Vector2 positionRangeX = new Vector2(-400f, 400f);
    public Vector2 positionRangeY = new Vector2(-200f, 200f);
    public Vector2 rotationRange  = new Vector2(-30f,   30f);

    [Header("Shaking Settings")]
    public bool enableShaking = true;
    public float shakeMagnitude = 5f;
    [Range(0.01f, 0.2f)]
    public float shakeSpeed = 0.05f;

    [Header("References")]
    public TextMeshProUGUI tmpText;   // Assign your TMP Text component in Inspector

    // --- Internal State ---
    private Coroutine _cycle;
    private Vector2 baseAnchoredPosition;

    // Per-character shake state
    private Vector2[] _charOffsets = new Vector2[0];   // current offset per char
    private Vector2[] _charDirs    = new Vector2[0];   // direction per char
    private float     _shakeTimer  = 0f;

    void Start()
    {
        if (tmpText == null)
            tmpText = GetComponent<TextMeshProUGUI>();

        if (tmpText == null)
        {
            Debug.LogError("[LAMETTEXTRANDOM] No TextMeshProUGUI component found!", this);
            return;
        }

        baseAnchoredPosition = tmpText.rectTransform.anchoredPosition;
        tmpText.text = "";

        if (isActive)
            _cycle = StartCoroutine(TextCycle());
    }

    void Update()
    {
        if (isActive && _cycle == null)
        {
            tmpText.text = "";
            _cycle = StartCoroutine(TextCycle());
        }
        else if (!isActive && _cycle != null)
        {
            StopCoroutine(_cycle);
            _cycle = null;
            tmpText.text = "";
        }

        if (isActive && enableShaking && tmpText != null && !string.IsNullOrEmpty(tmpText.text))
        {
            _shakeTimer += Time.deltaTime;

            if (_shakeTimer >= shakeSpeed)
            {
                _shakeTimer = 0f;
                AssignNewCharDirections();
            }

            ApplyPerCharShake();
        }
    }

    // Assign a fresh random direction for each visible character
    private void AssignNewCharDirections()
    {
        tmpText.ForceMeshUpdate();
        int charCount = tmpText.textInfo.characterCount;

        if (_charDirs.Length != charCount)
        {
            _charDirs    = new Vector2[charCount];
            _charOffsets = new Vector2[charCount];
        }

        for (int i = 0; i < charCount; i++)
        {
            float angle     = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float magnitude = Random.Range(shakeMagnitude * 0.4f, shakeMagnitude);
            _charDirs[i]    = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude;
            _charOffsets[i] = _charDirs[i];
        }
    }

    // Push per-character offsets into the TMP vertex buffer
    private void ApplyPerCharShake()
    {
        tmpText.ForceMeshUpdate();
        TMP_TextInfo textInfo = tmpText.textInfo;

        int charCount = textInfo.characterCount;
        if (charCount == 0) return;

        if (_charOffsets.Length != charCount)
        {
            _charDirs    = new Vector2[charCount];
            _charOffsets = new Vector2[charCount];
        }

        for (int i = 0; i < charCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int matIndex  = charInfo.materialReferenceIndex;
            int vertIndex = charInfo.vertexIndex;

            Vector3[] verts = textInfo.meshInfo[matIndex].vertices;

            Vector3 offset = new Vector3(_charOffsets[i].x, _charOffsets[i].y, 0f);
            verts[vertIndex + 0] += offset;
            verts[vertIndex + 1] += offset;
            verts[vertIndex + 2] += offset;
            verts[vertIndex + 3] += offset;
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            tmpText.UpdateGeometry(meshInfo.mesh, i);
        }
    }

    private IEnumerator TextCycle()
    {
        while (isActive)
        {
            string chosen = textPool[Random.Range(0, textPool.Length)];
            RandomisePlacement();
            yield return StartCoroutine(TypewriterEffect(chosen));
            yield return new WaitForSeconds(2f);

            tmpText.text  = "";
            _charOffsets  = new Vector2[0];
            _charDirs     = new Vector2[0];

            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator TypewriterEffect(string fullText)
    {
        tmpText.text = "";
        foreach (char c in fullText)
        {
            tmpText.text += c;

            tmpText.ForceMeshUpdate();
            int count = tmpText.textInfo.characterCount;
            if (_charDirs.Length != count)
            {
                System.Array.Resize(ref _charDirs,    count);
                System.Array.Resize(ref _charOffsets, count);
            }
            int idx   = count - 1;
            float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float mag = Random.Range(shakeMagnitude * 0.4f, shakeMagnitude);
            _charDirs[idx]    = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * mag;
            _charOffsets[idx] = _charDirs[idx];

            yield return new WaitForSeconds(typewriterSpeed);
        }
    }

    private void RandomisePlacement()
    {
        RectTransform rt = tmpText.rectTransform;

        float x = Random.Range(positionRangeX.x, positionRangeX.y);
        float y = Random.Range(positionRangeY.x, positionRangeY.y);
        baseAnchoredPosition = new Vector2(x, y);
        rt.anchoredPosition  = baseAnchoredPosition;

        float z = Random.Range(rotationRange.x, rotationRange.y);
        rt.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    void OnDestroy()
    {
        if (_cycle != null)
            StopCoroutine(_cycle);
    }
}
