using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DialogAndTypewirtting : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Dialogue Content")]
    [SerializeField] private List<string> dialogueLines = new List<string>();

    [Header("Settings")]
    [SerializeField] private float typeSpeed = 0.05f;
    [SerializeField] private float autoAdvanceDelay = 15f;
    [SerializeField] private bool useMouseClickToAdvance = true;

    [Header("End Trigger GameObjects")]
    [SerializeField] private GameObject gameObjectToAppear;
    [SerializeField] private GameObject gameObjectToDisappear;

    private int currentLineIndex = 0;
    private bool isTyping = false;
    private float timer = 0f;
    private Coroutine typewriteCoroutine;
    private bool hasTriggeredEnd = false;

    private void Start()
    {
        if (dialogueLines == null || dialogueLines.Count == 0)
        {
            Debug.LogWarning("No dialogue lines defined in DialogAndTypewirtting!");
            return;
        }

        if (dialogueText == null)
        {
            dialogueText = GetComponent<TextMeshProUGUI>();
            if (dialogueText == null)
            {
                Debug.LogError("DialogueText reference is missing on DialogAndTypewirtting!");
                return;
            }
        }

        currentLineIndex = 0;
        StartDialogueLine(currentLineIndex);
    }

    private void Update()
    {
        // Handle automatic transition timer
        timer += Time.deltaTime;
        if (timer >= autoAdvanceDelay)
        {
            AdvanceDialog();
        }

        // Handle click/tap to advance or skip typewriter
        if (useMouseClickToAdvance && Input.GetMouseButtonDown(0))
        {
            OnDialogClicked();
        }
    }

    public void OnDialogClicked()
    {
        // If we have already finished all dialogues and triggered the end, ignore further clicks
        if (hasTriggeredEnd) return;

        if (isTyping)
        {
            // Click 1: Skip typing process
            SkipTypewriter();
        }
        else
        {
            // Click 2 (or click when not typing): Advance to next dialog
            AdvanceDialog();
        }
    }

    private void StartDialogueLine(int index)
    {
        if (typewriteCoroutine != null)
        {
            StopCoroutine(typewriteCoroutine);
        }
        typewriteCoroutine = StartCoroutine(TypeText(dialogueLines[index]));
    }

    private IEnumerator TypeText(string text)
    {
        isTyping = true;
        timer = 0f; // Reset 15s timer when starting typing a new line
        dialogueText.text = text;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int totalVisibleCharacters = dialogueText.textInfo.characterCount;
        int counter = 0;

        while (counter <= totalVisibleCharacters)
        {
            dialogueText.maxVisibleCharacters = counter;
            counter++;
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
    }

    private void SkipTypewriter()
    {
        if (typewriteCoroutine != null)
        {
            StopCoroutine(typewriteCoroutine);
        }
        dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
        isTyping = false;
        
        // Reset timer when skipping so user has the full time to read the text
        timer = 0f;
    }

    private void AdvanceDialog()
    {
        currentLineIndex++;
        timer = 0f; // Reset 15s timer when advancing

        if (currentLineIndex < dialogueLines.Count)
        {
            StartDialogueLine(currentLineIndex);
        }
        else
        {
            TriggerEndingEffects();
        }
    }

    private void TriggerEndingEffects()
    {
        if (hasTriggeredEnd) return;
        hasTriggeredEnd = true;

        if (gameObjectToAppear != null)
        {
            gameObjectToAppear.SetActive(true);
        }

        if (gameObjectToDisappear != null)
        {
            gameObjectToDisappear.SetActive(false);
        }

        // Stop typewriter/cleanup text representation if desired
        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
    }
}
