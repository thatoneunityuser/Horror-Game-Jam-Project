using UnityEngine;

/// <summary>
/// ผู้จัดการระบบ HUD หลักในฉากการเล่น (Gameplay HUD Manager)
/// เชื่อมโยงระบบนับห้อง (RoomCounterUI) และระบบสถานะไฟฉาย (FlashlightUI)
/// เข้าด้วยกัน และมี Static API ให้เรียกใช้ได้สะดวกจากทุกสคริปต์ในเกม
/// </summary>
public class GameplayHUDManager : MonoBehaviour
{
    public static GameplayHUDManager Instance { get; private set; }

    [Header("=== HUD Components ===")]
    public RoomCounterUI roomCounterUI;
    public FlashlightUI flashlightUI;
    public GhostWarningManager ghostWarningManager;
    public StaminaBarUI staminaBarUI;
    public HorrorWarningSignUI horrorWarningSignUI;
    public SniffGhostHUD sniffGhostHUD;

    [Header("=== Pause Menu ===")]
    public HorrorJam.UI.PauseMenuController pauseMenuController;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (roomCounterUI == null) roomCounterUI = GetComponentInChildren<RoomCounterUI>();
        if (flashlightUI == null) flashlightUI = GetComponentInChildren<FlashlightUI>();
        if (ghostWarningManager == null) ghostWarningManager = GetComponentInChildren<GhostWarningManager>();
        if (ghostWarningManager == null) ghostWarningManager = FindObjectOfType<GhostWarningManager>();
        if (staminaBarUI == null) staminaBarUI = GetComponentInChildren<StaminaBarUI>();
        if (staminaBarUI == null) staminaBarUI = FindObjectOfType<StaminaBarUI>();
        if (horrorWarningSignUI == null) horrorWarningSignUI = GetComponentInChildren<HorrorWarningSignUI>();
        if (horrorWarningSignUI == null) horrorWarningSignUI = FindObjectOfType<HorrorWarningSignUI>();
        if (sniffGhostHUD == null) sniffGhostHUD = GetComponentInChildren<SniffGhostHUD>();
        if (sniffGhostHUD == null) sniffGhostHUD = FindObjectOfType<SniffGhostHUD>();
        if (pauseMenuController == null) pauseMenuController = GetComponentInChildren<HorrorJam.UI.PauseMenuController>();
        if (pauseMenuController == null) pauseMenuController = FindObjectOfType<HorrorJam.UI.PauseMenuController>();
    }

    void Update()
    {
        if (HorrorJam.UI.PauseMenuController.WasEscapePressed())
        {
            TogglePauseMenu();
        }
    }

    public static void TogglePauseMenu()
    {
        if (HorrorJam.UI.PauseMenuController.Instance != null)
        {
            HorrorJam.UI.PauseMenuController.Instance.TogglePause();
        }
    }

    #region Static Helper Methods (เข้าถึงง่ายจากทุกที่)

    /// <summary>
    /// สั่งเปลี่ยนสีตัวเลขนับห้อง (เช่น เมื่อเจอห้องพิเศษ, ฉากหลอน หรือบอส)
    /// </summary>
    public static void SetRoomColor(Color color)
    {
        if (Instance != null && Instance.roomCounterUI != null)
        {
            Instance.roomCounterUI.SetNumberColor(color);
        }
        else if (RoomCounterUI.Instance != null)
        {
            RoomCounterUI.Instance.SetNumberColor(color);
        }
    }

    /// <summary>
    /// สั่งรีเซ็ตสีตัวเลขนับห้องกลับเป็นสีขาว
    /// </summary>
    public static void ResetRoomColor()
    {
        if (Instance != null && Instance.roomCounterUI != null)
        {
            Instance.roomCounterUI.ResetColor();
        }
        else if (RoomCounterUI.Instance != null)
        {
            RoomCounterUI.Instance.ResetColor();
        }
    }

    /// <summary>
    /// สั่งเปิดหรือปิดโหมดตัวเลขสีแดง (Red Mode)
    /// </summary>
    public static void SetRoomRedMode(bool isRed)
    {
        if (Instance != null && Instance.roomCounterUI != null)
        {
            Instance.roomCounterUI.SetRedMode(isRed);
        }
        else if (RoomCounterUI.Instance != null)
        {
            RoomCounterUI.Instance.SetRedMode(isRed);
        }
    }

    /// <summary>
    /// สลับสถานะเปิด/ปิดโหมดตัวเลขสีแดง (Red Mode)
    /// </summary>
    public static void ToggleRoomRedMode()
    {
        if (Instance != null && Instance.roomCounterUI != null)
        {
            Instance.roomCounterUI.ToggleRedMode();
        }
        else if (RoomCounterUI.Instance != null)
        {
            RoomCounterUI.Instance.ToggleRedMode();
        }
    }

    /// <summary>
    /// สั่งกะพริบสีตัวเลขนับห้องชั่วคราว
    /// </summary>
    public static void FlashRoomColor(Color color, float duration = 1.0f)
    {
        if (Instance != null && Instance.roomCounterUI != null)
        {
            Instance.roomCounterUI.FlashColor(color, duration);
        }
        else if (RoomCounterUI.Instance != null)
        {
            RoomCounterUI.Instance.FlashColor(color, duration);
        }
    }

    /// <summary>
    /// สลับสถานะไฟฉายเปิด/ปิด
    /// </summary>
    public static void ToggleFlashlight()
    {
        if (Instance != null && Instance.flashlightUI != null)
        {
            Instance.flashlightUI.ToggleFlashlight();
        }
        else if (FlashlightUI.Instance != null)
        {
            FlashlightUI.Instance.ToggleFlashlight();
        }
    }

    /// <summary>
    /// สั่งเปิดหรือปิดไฟฉายโดยตรง
    /// </summary>
    public static void SetFlashlightState(bool on)
    {
        if (Instance != null && Instance.flashlightUI != null)
        {
            Instance.flashlightUI.SetFlashlight(on);
        }
        else if (FlashlightUI.Instance != null)
        {
            FlashlightUI.Instance.SetFlashlight(on);
        }
    }

    #endregion
}
