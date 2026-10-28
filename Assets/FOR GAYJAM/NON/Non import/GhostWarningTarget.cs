using UnityEngine;

/// <summary>
/// สคริปต์สำหรับติดที่พรีแฟบผี (Ghost / Entity) เพื่อให้มีลูกศรแจ้งเตือนขอบจอ
/// Attach this component to any Ghost / Entity prefab to show a warning arrow on screen edges.
/// </summary>
[DisallowMultipleComponent]
public class GhostWarningTarget : MonoBehaviour
{
    [Header("=== Arrow Appearance (รูปลักษณ์ลูกศร) ===")]
    [Tooltip("Color of the warning arrow for this ghost (default red)")]
    public Color arrowColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("Custom arrow sprite (leave empty to use default triangle arrow)")]
    public Sprite customArrowSprite;

    [Tooltip("Icon scale multiplier")]
    public Vector2 arrowScale = Vector2.one;

    [Header("=== Detection & Distance (ระยะการแจ้งเตือน) ===")]
    [Tooltip("Max distance to player to show arrow (0 = unlimited / always show across the whole map)")]
    public float maxWarningDistance = 0f;

    [Tooltip("If true, hide arrow when ghost is fully visible inside screen view")]
    public bool hideWhenOnScreen = false;

    [Header("=== Movement & Animation Override ===")]
    [Tooltip("Custom left-right bobbing speed (-1 to use Manager default)")]
    public float customBobSpeed = -1f;

    [Tooltip("Custom left-right bobbing distance in pixels (-1 to use Manager default)")]
    public float customBobDistance = -1f;

    [Header("=== Sound Alert (เสียงแจ้งเตือนเสริม) ===")]
    [Tooltip("Optional sound effect to play when warning first triggers")]
    public AudioClip warningSfx;
    [Range(0f, 1f)] public float warningSfxVolume = 0.8f;

    private bool isRegistered = false;

    void OnEnable()
    {
        Register();
    }

    void Start()
    {
        if (!isRegistered)
        {
            Register();
        }
    }

    void OnDisable()
    {
        Unregister();
    }

    void OnDestroy()
    {
        Unregister();
    }

    public void Register()
    {
        if (GhostWarningManager.Instance != null)
        {
            GhostWarningManager.Instance.RegisterTarget(this);
            isRegistered = true;
        }
    }

    public void Unregister()
    {
        if (GhostWarningManager.Instance != null && isRegistered)
        {
            GhostWarningManager.Instance.UnregisterTarget(this);
            isRegistered = false;
        }
    }
}
