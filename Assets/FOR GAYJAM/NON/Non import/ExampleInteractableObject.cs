using UnityEngine;

/// <summary>
/// ตัวอย่างการเขียนสคริปต์แบบ OOP โดยสืบทอด (Inherit) จาก FloatingInteractPrompt
/// Example showing how to write clean OOP interactable objects by inheriting FloatingInteractPrompt.
/// </summary>
public class ExampleInteractableObject : FloatingInteractPrompt
{
    [Header("=== Custom Object Logic (OOP Example) ===")]
    [Tooltip("ข้อความที่จะพิมพ์ออก Console เมื่อกด")]
    public string messageOnInteract = "Interact success!";

    [Tooltip("ต้องมีไอเทม/สถานะนี้ก่อนถึงจะกดได้หรือไม่")]
    public bool requiresUnlock = false;
    public bool isUnlocked = false;

    private int interactCounter = 0;

    /// <summary>
    /// [OOP Override] กำหนดเงื่อนไขว่าสามารถกดได้หรือไม่
    /// ถ้าคืนค่า false ปุ่มจะไม่โผล่มา หรือไม่สามารถกดได้
    /// </summary>
    public override bool CanInteract()
    {
        // ต้องผ่านเงื่อนไขพื้นฐาน (Cooldown, SingleUse) ก่อน
        if (!base.CanInteract()) return false;

        // เงื่อนไขเพิ่มเติมเฉพาะของวัตถุนี้
        if (requiresUnlock && !isUnlocked)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// [OOP Override] ฟังก์ชันที่จะทำงานเมื่อผู้เล่นกดปุ่ม E
    /// </summary>
    public override void OnInteract()
    {
        base.OnInteract(); // เล่น Squash bounce และ UnityEvent ตามปกติ

        interactCounter++;
        Debug.Log($"[{gameObject.name}] {messageOnInteract} (Interacted {interactCounter} times)");

        // คุณสามารถเขียน Logic ของวัตถุที่นี่ได้ เช่น:
        // - GetComponent<Animator>().SetTrigger("Open");
        // - Inventory.Instance.AddItem(lootItem);
        // - DialogueManager.StartDialogue(npcData);
    }

    /// <summary>
    /// [OOP Override] ทำงานเมื่อกด แต่เงื่อนไข CanInteract() ไม่ผ่าน (เช่น ของถูกล็อค)
    /// </summary>
    public override void OnInteractFailed()
    {
        base.OnInteractFailed();
        Debug.Log($"[{gameObject.name}] Object is locked or on cooldown!");
    }
}
