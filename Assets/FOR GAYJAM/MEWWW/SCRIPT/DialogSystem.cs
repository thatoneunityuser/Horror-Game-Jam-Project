using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

[System.Serializable]
public class ChoiceData
{
    [Header("Choice")]
    public string choiceText;
    [Tooltip("Lines after pick this choice.")]
    public dialog[] choiceDialogue;
    [Tooltip("On = back to main talk. Off = end talk.")]
    public bool returnToMainDialogue = true;
}

[System.Serializable]
public class dialog
{
    [Header("Line")]
    public string name;
    [TextArea(3, 5)]
    public string dialogue;
    public bool CharAppear;
    public Image CharImage;
    public SpriteRenderer CharSprite;
    public Sprite characterSprite;

    [Header("Audio")]
    public bool HasSound;
    public bool HasToPlayEveryTextAppear;
    public AudioClip audioClip;

    [Header("Choice")]
    public bool NeedToMakeChoiceBeforeNextLine;
    public ChoiceData[] choices;

    [Header("Minigame")]
    [Tooltip("If on, the minigame must finish before the next line.")]
    public bool needMiniGameBeforeNextLine;
    public GameObject miniGame;

    [Header("Event")]
    public bool TriggerTheEvent;
    public UnityEvent onLineEvent;
}

[System.Serializable]
public class ButtonForMAkeChoice
{
    [Header("Button")]
    public Button button;
    public TextMeshProUGUI buttonTextUI;
    public string buttonText;
    public string buttonAction;
}

[System.Serializable]
public class dialogfrombutton
{
    public string name;
    [TextArea(3, 5)]
    public string dialogue;
    public bool CharAppear;
    public Image CharImage;
    public SpriteRenderer CharSprite;
    public Sprite characterSprite;
    public bool HasSound;
    public AudioClip audioClip;
}

public class DialogSystem : MonoBehaviour
{
    #region Inspector
    [Header("Dialogue Data")]
    public dialog[] dialog;
    public int currentIndex = 0;

    [Header("UI")]
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI nameText;
    public GameObject NameTag;
    public GameObject[] character;
    public Image characterImage;
    public Canvas canvas;

    [Header("Typewriter")]
    [Tooltip("If true, dialogue text types out character by character.")]
    public bool useTypewriter = true;
    [Tooltip("Delay in seconds between characters.")]
    [Min(0.001f)]
    public float typingSpeed = 0.03f;
    [Tooltip("Play character sound every N characters (1 = every character).")]
    [Min(1)]
    public int soundEveryNCharacters = 1;
    [Tooltip("Optional subtle pitch variation for typing sound.")]
    public bool randomizeTypingPitch = false;

    [Header("Audio")]
    [Tooltip("AudioSource for dialogue lines. Auto found or added if empty.")]
    public AudioSource audioSource;
    [Tooltip("Sound played when player clicks to advance dialogue or select choice.")]
    public AudioClip clickSound;

    [Header("Choice Buttons")]
    public ButtonForMAkeChoice[] buttonForMAkeChoice;

    [Header("Run")]
    [Tooltip("If on, start talk in Start().")]
    public bool SystemWork = false;

    [Header("When End")]
    public bool IsDialogeEnd;
    public bool IsRepetable;
    public dialog[] UnrepeatableDialoge;
    public bool TriggerTheEvent;
    public UnityEvent onDialogueEnd;


    #endregion
    #region ForLateGame
    [Header("Rally Characters")]
    public bool NeedToRally;
    public GameObject[] Characters;
    [Tooltip("One target position for each character, by index.")]
    public Transform[] positions;

    [Header("Screen Fill")]
    [Tooltip("Filled Image for transition before dialogue.")]
    public Image FillImage;
    [Min(0.01f)]
    public float transitionTime = 1f;
    #endregion

    #region Runtime
    private dialog[] activeDialogueArray;
    private int activeIndex;
    private bool isBranchActive;
    private int mainDialogueReturnIndex;
    private bool isWaitingForChoice;
    private bool choiceButtonsActive;
    private bool waitingForMiniGame;
    private GameObject activeMiniGame;
    private bool isRallyTransitioning;
    private Coroutine rallyRoutine;
    private Coroutine typingRoutine;

    public bool isTyping { get; private set; }
    public static AudioSource ActiveDialogueAudioSource;
    #endregion

    #region Unity
    void Start()
    {
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }
        }



        ResolveAudioSource();

        if (FillImage == null)
        {
            FillImage = ResolveFillImage();
        }

        if (FillImage != null)
        {
            FillImage.gameObject.SetActive(false);
        }

        if (dialogueText != null)
        {
#if UNITY_2023_1_OR_NEWER
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
#else
            dialogueText.enableWordWrapping = true;
#endif
            if (dialogueText.overflowMode == TextOverflowModes.Page)
            {
                dialogueText.overflowMode = TextOverflowModes.Overflow;
            }
        }

        HideChoiceButtons();

        if (SystemWork && dialog != null && dialog.Length > 0)
        {
            StartTheSystem();
        }
    }

    void OnDisable()
    {
        StopAudio();
        StopTyping();
    }

    void Update()
    {
        if (!SystemWork || isRallyTransitioning)
        {
            return;
        }

        if (isWaitingForChoice && !choiceButtonsActive)
        {
            if (!isTyping)
            {
                ShowChoiceButtons();
            }
        }

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
        {
            AdvanceDialogue();
        }
    }
    #endregion

    #region Public Functions
    public void StartTheSystem()
    {
        if (isRallyTransitioning)
        {
            return;
        }

        StopAllDialogueAudio();
        StopAudio();
        StopTyping();

        SystemWork = true;
        IsDialogeEnd = false;
        isBranchActive = false;
        isWaitingForChoice = false;
        choiceButtonsActive = false;
        StopActiveMiniGame();



        if (dialog != null && dialog.Length > 0)
        {
            activeDialogueArray = dialog;
            activeIndex = currentIndex;
        }
        else
        {
            activeDialogueArray = null;
            activeIndex = 0;
        }

        if (NeedToRally)
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }

            if (rallyRoutine != null)
            {
                StopCoroutine(rallyRoutine);
            }
            rallyRoutine = StartCoroutine(RallyAndTransitionRoutine());
            return;
        }

        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);
        }

        if (activeDialogueArray != null && activeDialogueArray.Length > 0)
        {
            DisplayLine(activeIndex);
        }
    }

    public void StartTheSyetem() => StartTheSystem();

    public void StopTheSystem()
    {
        StopAudio();
        StopTyping();

        if (rallyRoutine != null)
        {
            StopCoroutine(rallyRoutine);
            rallyRoutine = null;
        }

        isRallyTransitioning = false;
        SetFill(0f);
        Image fill = ResolveFillImage();
        if (fill != null)
        {
            fill.gameObject.SetActive(false);
        }

        SystemWork = false;
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }



        HideChoiceButtons();
        StopActiveMiniGame();
    }

    public void StopTheSyetem() => StopTheSystem();

    public void AddChoiceButton(Button button)
    {
        AddChoiceButton(button, null);
    }

    public void AddChoiceButton(Button button, TextMeshProUGUI textUI)
    {
        if (button == null)
        {
            return;
        }

        if (IndexOfChoiceButton(button) >= 0)
        {
            return;
        }

        ButtonForMAkeChoice slot = new ButtonForMAkeChoice();
        slot.button = button;
        slot.buttonTextUI = textUI != null ? textUI : button.GetComponentInChildren<TextMeshProUGUI>(true);
        AppendChoiceSlot(slot);
    }

    public void AddChoiceButton(ButtonForMAkeChoice slot)
    {
        if (slot == null || slot.button == null)
        {
            return;
        }

        if (IndexOfChoiceButton(slot.button) >= 0)
        {
            return;
        }

        if (slot.buttonTextUI == null)
        {
            slot.buttonTextUI = slot.button.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        AppendChoiceSlot(slot);
    }

    public void RemoveChoiceButton(Button button)
    {
        int idx = IndexOfChoiceButton(button);
        if (idx < 0)
        {
            return;
        }

        if (button != null)
        {
            button.gameObject.SetActive(false);
        }

        int newLen = buttonForMAkeChoice.Length - 1;
        ButtonForMAkeChoice[] next = new ButtonForMAkeChoice[newLen];
        int write = 0;
        for (int i = 0; i < buttonForMAkeChoice.Length; i++)
        {
            if (i != idx)
            {
                next[write++] = buttonForMAkeChoice[i];
            }
        }

        buttonForMAkeChoice = next;

        if (choiceButtonsActive)
        {
            ShowChoiceButtons();
        }
    }

    public void ClearChoiceButtons()
    {
        if (buttonForMAkeChoice != null)
        {
            for (int i = 0; i < buttonForMAkeChoice.Length; i++)
            {
                if (buttonForMAkeChoice[i] != null && buttonForMAkeChoice[i].button != null)
                {
                    buttonForMAkeChoice[i].button.gameObject.SetActive(false);
                }
            }
        }

        buttonForMAkeChoice = new ButtonForMAkeChoice[0];
    }

    public void AdvanceDialogue()
    {
        if (activeDialogueArray == null || activeIndex < 0 || activeIndex >= activeDialogueArray.Length)
        {
            return;
        }

        if (waitingForMiniGame || (isWaitingForChoice && choiceButtonsActive))
        {
            return;
        }

        PlayClickSound();

        // If typewriter is still typing, clicking instantly reveals full text
        if (isTyping)
        {
            SkipTyping();
            return;
        }

        if (activeDialogueArray[activeIndex].needMiniGameBeforeNextLine)
        {
            StartMiniGame();
            return;
        }

        MoveToNextLine();
    }

    public void StartMiniGame()
    {
        if (activeDialogueArray == null || activeIndex < 0 || activeIndex >= activeDialogueArray.Length || waitingForMiniGame)
        {
            return;
        }

        dialog currentLine = activeDialogueArray[activeIndex];
        if (!currentLine.needMiniGameBeforeNextLine)
        {
            return;
        }

        
        waitingForMiniGame = true;
        activeMiniGame = currentLine.miniGame;
        if (activeMiniGame != null)
        {
            activeMiniGame.SetActive(true);
        }
    }

    // Call this from the minigame when the player wins / finishes.
    public void FinishedMiniGame()
    {
        if (!waitingForMiniGame)
        {
            return;
        }

        StopAudio();

        if (activeMiniGame != null)
        {
            activeMiniGame.SetActive(false);
        }

        activeMiniGame = null;
        waitingForMiniGame = false;
        MoveToNextLine();
    }

    void MoveToNextLine()
    {
        activeIndex++;
        if (activeIndex < activeDialogueArray.Length)
        {
            DisplayLine(activeIndex);
        }
        else if (isBranchActive)
        {
            isBranchActive = false;
            activeDialogueArray = dialog;
            activeIndex = mainDialogueReturnIndex;

            if (activeIndex < activeDialogueArray.Length)
            {
                DisplayLine(activeIndex);
            }
            else
            {
                EndDialogue();
            }
        }
        else
        {
            EndDialogue();
        }
    }

    public void DisplayLine(int index)
    {
        if (activeDialogueArray == null || index < 0 || index >= activeDialogueArray.Length)
        {
            return;
        }

        HideChoiceButtons();

        dialog currentLine = activeDialogueArray[index];
        PlayLineAudio(currentLine);

        if (dialogueText != null)
        {
#if UNITY_2023_1_OR_NEWER
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
#else
            dialogueText.enableWordWrapping = true;
#endif
            if (dialogueText.overflowMode == TextOverflowModes.Page)
            {
                dialogueText.overflowMode = TextOverflowModes.Overflow;
            }
        }

        if (currentLine.TriggerTheEvent || (currentLine.onLineEvent != null && currentLine.onLineEvent.GetPersistentEventCount() > 0))
        {
            currentLine.onLineEvent?.Invoke();
        }

        if (nameText != null)
        {
            nameText.text = ThaiFontFixer.Adjust(currentLine.name);
        }

        ApplyCharacterVisuals(currentLine, index);

        isWaitingForChoice = currentLine.NeedToMakeChoiceBeforeNextLine && currentLine.choices != null && currentLine.choices.Length > 0;

        if (dialogueText != null)
        {
            StartTyping(currentLine.dialogue, currentLine);
        }

        // Show minigame instantly if this dialogue line requires a minigame
        if (currentLine.needMiniGameBeforeNextLine && currentLine.miniGame != null)
        {
            StartMiniGame();
        }
    }

    public void ShowChoiceButtons()
    {
        choiceButtonsActive = true;
        if (activeDialogueArray == null || activeIndex < 0 || activeIndex >= activeDialogueArray.Length)
        {
            return;
        }

        dialog currentLine = activeDialogueArray[activeIndex];
        if (buttonForMAkeChoice == null || currentLine.choices == null)
        {
            return;
        }

        for (int i = 0; i < buttonForMAkeChoice.Length; i++)
        {
            ButtonForMAkeChoice slot = buttonForMAkeChoice[i];
            if (slot == null || slot.button == null)
            {
                continue;
            }

            if (i < currentLine.choices.Length)
            {
                ChoiceData choice = currentLine.choices[i];
                slot.button.gameObject.SetActive(true);
                SetButtonLabel(slot, choice.choiceText);

                int choiceIndex = i;
                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() => OnChoiceSelected(choiceIndex));
            }
            else
            {
                slot.button.gameObject.SetActive(false);
            }
        }
    }

    public void HideChoiceButtons()
    {
        choiceButtonsActive = false;
        isWaitingForChoice = false;

        if (buttonForMAkeChoice == null)
        {
            return;
        }

        for (int i = 0; i < buttonForMAkeChoice.Length; i++)
        {
            if (buttonForMAkeChoice[i] != null && buttonForMAkeChoice[i].button != null)
            {
                buttonForMAkeChoice[i].button.gameObject.SetActive(false);
            }
        }
    }

    public void OnChoiceSelected(int choiceIndex)
    {
        if (activeDialogueArray == null || activeIndex < 0 || activeIndex >= activeDialogueArray.Length)
        {
            return;
        }

        dialog currentLine = activeDialogueArray[activeIndex];
        if (currentLine.choices == null || choiceIndex < 0 || choiceIndex >= currentLine.choices.Length)
        {
            return;
        }

        ChoiceData choice = currentLine.choices[choiceIndex];
        HideChoiceButtons();
        StopAudio();
        PlayClickSound();

        if (choice.choiceDialogue != null && choice.choiceDialogue.Length > 0)
        {
            mainDialogueReturnIndex = choice.returnToMainDialogue ? activeIndex + 1 : dialog.Length;
            isBranchActive = true;
            activeDialogueArray = choice.choiceDialogue;
            activeIndex = 0;
            DisplayLine(activeIndex);
        }
        else if (choice.returnToMainDialogue)
        {
            activeIndex++;
            if (activeIndex < activeDialogueArray.Length)
            {
                DisplayLine(activeIndex);
            }
            else
            {
                EndDialogue();
            }
        }
        else
        {
            EndDialogue();
        }
    }

    public void EndDialogue()
    {
        StopAudio();
        StopTyping();

        IsDialogeEnd = true;

        if (TriggerTheEvent || (onDialogueEnd != null && onDialogueEnd.GetPersistentEventCount() > 0))
        {
            onDialogueEnd?.Invoke();
        }

        HideChoiceButtons();
        StopActiveMiniGame();


        if (NameTag != null)
        {
            NameTag.SetActive(false);
        }

        if (character != null)
        {
            for (int i = 0; i < character.Length; i++)
            {
                if (character[i] != null)
                {
                    character[i].SetActive(false);
                }
            }
        }

        if (characterImage != null)
        {
            characterImage.gameObject.SetActive(false);
        }

        HidePortraitsInArray(dialog);
        HidePortraitsInArray(activeDialogueArray);

        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }

        SystemWork = false;

        if (IsRepetable)
        {
            activeIndex = 0;
        }
        else if (UnrepeatableDialoge != null && UnrepeatableDialoge.Length > 0)
        {
            dialog = UnrepeatableDialoge;
            activeIndex = 0;
        }
    }
#endregion

    public void RallyCharacters()
    {
        if (Characters == null || positions == null)
        {
            return;
        }

        int count = Mathf.Min(Characters.Length, positions.Length);
        for (int i = 0; i < count; i++)
        {
            if (Characters[i] == null || positions[i] == null)
            {
                continue;
            }

            if (Characters[i].TryGetComponent(out Rigidbody2D rb))
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector2.zero;
#else
                rb.velocity = Vector2.zero;
#endif
                rb.position = positions[i].position;
            }

            Characters[i].transform.position = positions[i].position;
        }
    }

    IEnumerator RallyAndTransitionRoutine()
    {
        isRallyTransitioning = true;
        StopAudio();

     

        Image fill = ResolveFillImage();
        if (fill != null)
        {
            fill.gameObject.SetActive(true);
            SetFill(0f);

            float speed = 1f / Mathf.Max(transitionTime, 0.01f);
            float fillamount = 0f;
            while (fillamount < 1f)
            {
                fillamount += Time.deltaTime * speed;
                SetFill(Mathf.Clamp01(fillamount));
                yield return null;
            }

            SetFill(1f);

            // Re-enforce rally position at peak fill
            RallyCharacters();

            yield return new WaitForSeconds(0.1f);

            fillamount = 1f;
            while (fillamount > 0f)
            {
                fillamount -= Time.deltaTime * speed;
                SetFill(Mathf.Clamp01(fillamount));
                yield return null;
            }

            SetFill(0f);
            fill.gameObject.SetActive(false);
        }

        isRallyTransitioning = false;
        rallyRoutine = null;

        // Step 2: Now the dialogs appear
        if (SystemWork && !IsDialogeEnd)
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(true);
            }

            if (activeDialogueArray != null && activeIndex >= 0 && activeIndex < activeDialogueArray.Length)
            {
                DisplayLine(activeIndex);
            }
        }
    }

    Image ResolveFillImage()
    {
        return FillImage;
    }

    void SetFill(float amount)
    {
        Image fill = ResolveFillImage();
        if (fill != null)
        {
            fill.fillAmount = amount;
        }
    }

    void StopActiveMiniGame()
    {
        if (activeMiniGame != null)
        {
            activeMiniGame.SetActive(false);
            activeMiniGame = null;
        }

        waitingForMiniGame = false;
    }
    #region Typewriter
    public void StartTyping(string text, dialog currentLine = null)
    {
        StopTyping();

        if (dialogueText == null)
        {
            isTyping = false;
            return;
        }

        if (!useTypewriter)
        {
            dialogueText.text = ThaiFontFixer.Adjust(text);
            dialogueText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;
            if (isWaitingForChoice && !choiceButtonsActive)
            {
                ShowChoiceButtons();
            }
            return;
        }

        typingRoutine = StartCoroutine(TypeTextRoutine(text, currentLine));
    }

    public void SkipTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;

        if (dialogueText != null)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }

        if (isWaitingForChoice && !choiceButtonsActive)
        {
            ShowChoiceButtons();
        }
    }

    public void StopTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;
    }

    private IEnumerator TypeTextRoutine(string fullText, dialog currentLine)
    {
        isTyping = true;

        if (string.IsNullOrEmpty(fullText))
        {
            if (dialogueText != null)
            {
                dialogueText.text = "";
                dialogueText.maxVisibleCharacters = int.MaxValue;
            }
            isTyping = false;
            typingRoutine = null;
            if (isWaitingForChoice && !choiceButtonsActive)
            {
                ShowChoiceButtons();
            }
            yield break;
        }

        dialogueText.text = ThaiFontFixer.Adjust(fullText);
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int totalVisibleCharacters = dialogueText.textInfo.characterCount;
        if (totalVisibleCharacters <= 0)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;
            typingRoutine = null;
            if (isWaitingForChoice && !choiceButtonsActive)
            {
                ShowChoiceButtons();
            }
            yield break;
        }

        AudioSource audioSrc = (currentLine != null && currentLine.HasSound && currentLine.HasToPlayEveryTextAppear && currentLine.audioClip != null)
            ? ResolveAudioSource()
            : null;

        float originalPitch = audioSrc != null ? audioSrc.pitch : 1f;
        int step = Mathf.Max(1, soundEveryNCharacters);
        float delay = Mathf.Max(0.001f, typingSpeed);

        for (int i = 1; i <= totalVisibleCharacters; i++)
        {
            dialogueText.maxVisibleCharacters = i;

            if (audioSrc != null && currentLine.audioClip != null && (i % step == 0))
            {
                if (randomizeTypingPitch)
                {
                    audioSrc.pitch = Random.Range(0.95f, 1.05f);
                }
                audioSrc.PlayOneShot(currentLine.audioClip);
            }

            yield return new WaitForSeconds(delay);
        }

        if (audioSrc != null && randomizeTypingPitch)
        {
            audioSrc.pitch = originalPitch;
        }

        dialogueText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        typingRoutine = null;

        if (isWaitingForChoice && !choiceButtonsActive)
        {
            ShowChoiceButtons();
        }
    }
    #endregion

    #region Private Functions
    void AppendChoiceSlot(ButtonForMAkeChoice slot)
    {
        int len = buttonForMAkeChoice == null ? 0 : buttonForMAkeChoice.Length;
        System.Array.Resize(ref buttonForMAkeChoice, len + 1);
        buttonForMAkeChoice[len] = slot;

        if (choiceButtonsActive)
        {
            ShowChoiceButtons();
        }
        else if (slot.button != null)
        {
            slot.button.gameObject.SetActive(false);
        }
    }

    int IndexOfChoiceButton(Button button)
    {
        if (button == null || buttonForMAkeChoice == null)
        {
            return -1;
        }

        for (int i = 0; i < buttonForMAkeChoice.Length; i++)
        {
            if (buttonForMAkeChoice[i] != null && buttonForMAkeChoice[i].button == button)
            {
                return i;
            }
        }

        return -1;
    }

    AudioSource ResolveAudioSource()
    {
        if (audioSource != null)
        {
            audioSource.spatialBlend = 0f;
            if (audioSource.volume <= 0.001f) audioSource.volume = 1f;
            return audioSource;
        }



        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = GetComponentInChildren<AudioSource>(true);
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
        }

        if (audioSource != null)
        {
            audioSource.spatialBlend = 0f;
            if (audioSource.volume <= 0.001f) audioSource.volume = 1f;
        }

        return audioSource;
    }

    public static void StopAllDialogueAudio()
    {
        if (ActiveDialogueAudioSource != null && ActiveDialogueAudioSource.isPlaying)
        {
            ActiveDialogueAudioSource.Stop();
        }
        ActiveDialogueAudioSource = null;
    }

    public void StopAudio()
    {


        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        if (ActiveDialogueAudioSource == audioSource)
        {
            ActiveDialogueAudioSource = null;
        }
    }

    public void StopDialogueAudio() => StopAudio();
    public static void StopAnyDialogueAudio() => StopAllDialogueAudio();

    public void PlayClickSound()
    {
        if (clickSound == null) return;
        AudioSource src = ResolveAudioSource();
        if (src != null)
        {
            src.PlayOneShot(clickSound);
        }
    }

    void PlayLineAudio(dialog currentLine)
    {
        // Stop any old sound from previous dialog line or previous dialog system
        StopAudio();
        StopAllDialogueAudio();

        if (currentLine == null || !currentLine.HasSound || currentLine.audioClip == null)
        {
            return;
        }

        AudioSource src = ResolveAudioSource();
        if (src != null)
        {
            // If HasToPlayEveryTextAppear is true and typewriter is active, the typewriter plays sound per character
            if (!currentLine.HasToPlayEveryTextAppear || !useTypewriter)
            {
                src.clip = currentLine.audioClip;
                src.Play();
                ActiveDialogueAudioSource = src;
            }
        }
    }

    void ApplyCharacterVisuals(dialog currentLine, int index)
    {
        bool showChar = currentLine.CharAppear;

        if (NameTag != null)
        {
            NameTag.SetActive(showChar);
        }

        if (character != null)
        {
            for (int i = 0; i < character.Length; i++)
            {
                GameObject charObj = character[i];
                if (charObj == null)
                {
                    continue;
                }

                charObj.SetActive(showChar);
                if (showChar && currentLine.characterSprite != null)
                {
                    if (charObj.TryGetComponent(out Image img))
                    {
                        img.sprite = currentLine.characterSprite;
                    }
                    else if (charObj.TryGetComponent(out SpriteRenderer sr))
                    {
                        sr.sprite = currentLine.characterSprite;
                    }
                }
            }
        }

        SetPortrait(characterImage, showChar, currentLine.characterSprite);
        SetPortrait(currentLine.CharImage, showChar, currentLine.characterSprite);

        if (currentLine.CharSprite != null)
        {
            currentLine.CharSprite.gameObject.SetActive(showChar);
            if (showChar && currentLine.characterSprite != null)
            {
                currentLine.CharSprite.sprite = currentLine.characterSprite;
            }
        }

        for (int i = 0; i < activeDialogueArray.Length; i++)
        {
            if (i == index || activeDialogueArray[i] == null)
            {
                continue;
            }

            dialog other = activeDialogueArray[i];
            if (other.CharImage != null && other.CharImage != currentLine.CharImage)
            {
                other.CharImage.gameObject.SetActive(false);
            }

            if (other.CharSprite != null && other.CharSprite != currentLine.CharSprite)
            {
                other.CharSprite.gameObject.SetActive(false);
            }
        }
    }

    static void SetPortrait(Image img, bool show, Sprite sprite)
    {
        if (img == null)
        {
            return;
        }

        img.gameObject.SetActive(show);
        if (show && sprite != null)
        {
            img.sprite = sprite;
        }
    }

    static void SetButtonLabel(ButtonForMAkeChoice slot, string label)
    {
        string adjusted = ThaiFontFixer.Adjust(label);
        if (slot.buttonTextUI != null)
        {
            slot.buttonTextUI.text = adjusted;
            return;
        }

        TextMeshProUGUI tmp = slot.button.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = adjusted;
            return;
        }

        Text legacyText = slot.button.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            legacyText.text = label;
        }
    }

    static void HidePortraitsInArray(dialog[] lines)
    {
        if (lines == null)
        {
            return;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            dialog line = lines[i];
            if (line == null)
            {
                continue;
            }

            if (line.CharImage != null)
            {
                line.CharImage.gameObject.SetActive(false);
            }

            if (line.CharSprite != null)
            {
                line.CharSprite.gameObject.SetActive(false);
            }
        }
    }
    #endregion
}






