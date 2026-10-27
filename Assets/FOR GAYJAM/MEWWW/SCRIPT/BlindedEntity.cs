using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class BlindedEntity : MonoBehaviour
{
    [Header("Range")]
    public float MovementSensingRange = 5f;
    public float LockerSensingRange = 3f;
    public float SuspectRange = 10f;
    public float StopRange = 2f;
    public float lifeTime = 20f;

    [Header("Hitbox")]
    public Vector2 AttackRange = new Vector2(2f, 2f);
    public Vector2 HitboxOffset;

    public enum State { RunAway, Suspicious, Detected };
    public State currentState = State.RunAway;

    [Header("Movement")]
    public float runAwaySpeed = 3f;
    public float chaseSpeedMultiplier = 2f;
    public bool IsReversal = false;
    private SpriteRenderer spriteRenderer;


    [Header("Animator")]
    public Animator animator;

    [Header("Audio")]
    public AudioSource audioSource;
    [Tooltip("Optional: dedicated AudioSource for scream/detection sound. If unassigned, audioSource is used.")]
    public AudioSource screamAudioSource;
    public AudioClip DetectedSound;
    public AudioClip WalkSound;

    [Header("Locker Detection")]
    public float lockerNoiseDuration = 0.5f;

    private SideScrollMovement playerMovement;
    private Hided playerHided;
    private Animator PlayerAnimator;

    private bool hasPlayedDetectedSound = false;
    private bool isChecking = false;
    private bool isAttacking = false;
    private bool attackHitEvaluated = false;
    private bool isPlayerHidingThatTime = false;
    private float currentLifeTimer = 0f;
    private Coroutine checkingCoroutine;
    private Coroutine attackCoroutine;
    private Coroutine hideNoiseCoroutine;

    void Start()
    {
        FindPlayerReferencesIfNeeded();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        currentLifeTimer = lifeTime;

        if (IsReversal)
        {
            spriteRenderer.flipX = true;
        }
    }

    void Update()
    {
        CheckHideNoiseInput();
        UpdateLifeTimer();
        UpdateState();
        UpdateAction();
    }

    void UpdateLifeTimer()
    {
        // Lifetime countdown only ticks down while running away.
        // Paused while Suspicious (investigating) or Detected (chasing) so entity doesn't despawn mid-encounter!
        if (currentState == State.RunAway)
        {
            currentLifeTimer -= Time.deltaTime;
            if (currentLifeTimer <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }

    void CheckHideNoiseInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (hideNoiseCoroutine != null)
            {
                StopCoroutine(hideNoiseCoroutine);
            }
            hideNoiseCoroutine = StartCoroutine(HideNoiseRoutine());
        }
    }

    IEnumerator HideNoiseRoutine()
    {
        isPlayerHidingThatTime = true;
        yield return new WaitForSeconds(lockerNoiseDuration);
        isPlayerHidingThatTime = false;
        hideNoiseCoroutine = null;
    }

    bool IsPlayerHideThatTime()
    {
        bool justHidInScript = playerHided != null && playerHided.JustHid;
        return isPlayerHidingThatTime || justHidInScript;
    }

    void UpdateState()
    {
        if (isKillingPlayer)
        {
            currentState = State.RunAway;
            return;
        }

        // 1. If already detected, it can NEVER return to RunAway or Suspicious state
        if (currentState == State.Detected)
        {
            return;
        }

        FindPlayerReferencesIfNeeded();

        bool isPlayerHidden = playerHided != null && playerHided.IsPlayerHide;
        // Hidden players are stationary and silent inside lockers
        bool isPlayerMoving = playerMovement != null && playerMovement.isMoving && !isPlayerHidden;
        bool isPlayerHideNow = IsPlayerHideThatTime();
        bool isPlayerCrawling = playerMovement != null && playerMovement.isCrawling;

        // 2. Detected takes highest priority:
        // Close range movement OR player enters/interacts with locker within locker range
        if ((IsPlayerInMovementRange() && isPlayerMoving && !isPlayerCrawling) || (IsPlayerInLockerRange() && isPlayerHidden && isPlayerHideNow))
        {
            currentState = State.Detected;

            if (isChecking && checkingCoroutine != null)
            {
                StopCoroutine(checkingCoroutine);
                isChecking = false;
            }

            PlayDetectedSound();
            return;
        }

        // 3. If currently Suspicious:
        // It stays in Suspicious while walking towards the stop range and while checking out.
        // It ONLY returns to RunAway when the 5-second CheckingOut() coroutine completes.
        if (currentState == State.Suspicious)
        {
            return;
        }

        // 4. If in RunAway, check if player makes noise (Suspect Range + moving)
        if (IsPlayerInSuspectRange() && isPlayerMoving && !isPlayerCrawling)
        {
            currentState = State.Suspicious;
        }
        else
        {
            currentState = State.RunAway;
        }
    }

    void UpdateAction()
    {
        switch (currentState)
        {
            case State.RunAway:
                RunAway();
                break;
            case State.Suspicious:
                Suspicious();
                break;
            case State.Detected:
                Detected();
                break;
        }
    }

    void RunAway()
    {
        // Ensure entity faces to the right while running away to the right
        if (transform.localScale.x < 0f)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        if(IsReversal)
        {
            transform.position += Vector3.left * runAwaySpeed * Time.deltaTime;
        }
        else
        {
            transform.position += Vector3.right * runAwaySpeed * Time.deltaTime;
        }

        if (animator != null && !animator.GetBool("IsMoving"))
        {
            animator.SetBool("IsMoving", true);
        }
    }

    void Suspicious()
    {
        // If entity has reached stop range and is currently waiting out the 5 seconds, remain stopped
        if (isChecking)
        {
            if (animator != null && animator.GetBool("IsMoving"))
            {
                animator.SetBool("IsMoving", false);
            }
            return;
        }

        // Check if entity has reached the stop range
        if (IsPlayerInStopRange())
        {
            if (animator != null && animator.GetBool("IsMoving"))
            {
                animator.SetBool("IsMoving", false);
            }

            checkingCoroutine = StartCoroutine(CheckingOut());
        }
        else
        {
            // Move towards the sound/player until stop range
            FacePlayer();
            if(IsReversal)
            {
                spriteRenderer.flipX = false;
            }
            float dirX = GetDirectionToPlayerX();
            Vector3 moveDir = new Vector3(dirX, 0f, 0f);

            transform.position += moveDir * runAwaySpeed * Time.deltaTime;

            if (animator != null && !animator.GetBool("IsMoving"))
            {
                animator.SetBool("IsMoving", true);
               
            }
        }
    }

    void Detected()
    {
        if (!hasPlayedDetectedSound)
        {
            PlayDetectedSound();
        }

      
  

        // Don't slide while attacking
        if (isAttacking)
        {
            if (animator != null && animator.GetBool("IsMoving"))
            {
                animator.SetBool("IsMoving", false);
            }
            return;
        }

        FacePlayer();

        if (IsPlayerInAttackRange())
        {
            if (animator != null && animator.GetBool("IsMoving"))
            {
                animator.SetBool("IsMoving", false);
            }

            StartAttack();
        }
        else
        {
            
            float dirX = GetDirectionToPlayerX();
            Vector3 moveDir = new Vector3(dirX, 0f, 0f);

            float chaseSpeed = runAwaySpeed * chaseSpeedMultiplier;

            transform.position += moveDir * chaseSpeed * Time.deltaTime;

            if (animator != null && !animator.GetBool("IsMoving"))
            {
                animator.SetBool("IsMoving", true);
            }
        }
    }

    void StartAttack()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    IEnumerator CheckingOut()
    {
        isChecking = true;
        yield return new WaitForSeconds(5f);
        isChecking = false;
         if(IsReversal)
                {
                    spriteRenderer.flipX = true;
                }
        currentState = State.RunAway;
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        attackHitEvaluated = false;

        FacePlayer();

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.Play("punch", -1, 0f);
            animator.Update(0f);
        }

        yield return new WaitForSeconds(0.1f);

        // Fallback: If animation event did not trigger AttackPlayer(), evaluate here
        if (!attackHitEvaluated)
        {
            AttackPlayer();
        }

        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
        }

        isAttacking = false;
        attackCoroutine = null;

        // If player is still in attack range after punch concludes, immediately throw next punch!
        if (currentState == State.Detected && IsPlayerInAttackRange())
        {
            StartAttack();
        }
    }

    void FacePlayer()
    {
        float dirX = GetDirectionToPlayerX();
        if (dirX != 0f)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * dirX, transform.localScale.y, transform.localScale.z);
        }
    }

    void FindPlayerReferencesIfNeeded()
    {
        if (playerMovement == null)
        {
            playerMovement = FindObjectOfType<SideScrollMovement>();
            if (playerMovement == null)
            {
                SideScrollMovement[] found = FindObjectsOfType<SideScrollMovement>(true);
                if (found != null && found.Length > 0)
                {
                    playerMovement = found[0];
                }
            }
        }
        if (playerHided == null)
        {
            playerHided = FindObjectOfType<Hided>();
            if (playerHided == null)
            {
                Hided[] found = FindObjectsOfType<Hided>(true);
                if (found != null && found.Length > 0)
                {
                    playerHided = found[0];
                }
            }
        }
    }

    float GetDirectionToPlayerX()
    {
        FindPlayerReferencesIfNeeded();

        Transform target = null;
        if (playerMovement != null)
        {
            target = playerMovement.transform;
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null)
        {
            return (target.position.x >= transform.position.x) ? 1f : -1f;
        }

        return 1f;
    }

    Vector2 GetCurrentHitboxOffset()
    {
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        return new Vector2(HitboxOffset.x * facing, HitboxOffset.y);
    }

    private bool isKillingPlayer = false;

    public void AttackPlayer()
    {
        if (isKillingPlayer) return;
        isKillingPlayer = true;
        StartCoroutine(PlayerDieCoroutine());
    }

    IEnumerator PlayerDieCoroutine()
    {
        currentState = State.RunAway;
        isAttacking = false;
        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            PlayerAnimator = player.GetComponent<Animator>();
            if (PlayerAnimator == null)
            {
                PlayerAnimator = player.GetComponentInChildren<Animator>();
            }

            if (PlayerAnimator != null)
            {
                if (PlayerAnimator.HasState(0, Animator.StringToHash("Die")))
                    PlayerAnimator.Play("Die");
                else if (PlayerAnimator.HasState(0, Animator.StringToHash("Dead")))
                    PlayerAnimator.Play("Dead");
                else
                    PlayerAnimator.Play("Die");
            }

            SideScrollMovement movement = player.GetComponent<SideScrollMovement>();
            if (movement != null)
            {
                movement.enabled = false;
            }

            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
            }
        }

        if (playerHided != null) playerHided.enabled = false;
        if (playerMovement != null) playerMovement.enabled = false;

        yield return new WaitForSeconds(2f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, MovementSensingRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, LockerSensingRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, SuspectRange);

        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(transform.position + (Vector3)GetCurrentHitboxOffset(), new Vector3(AttackRange.x, AttackRange.y, 0.1f));

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, StopRange);
    }

    bool IsPlayerInMovementRange()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, MovementSensingRange);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    bool IsPlayerInLockerRange()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, LockerSensingRange);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    bool IsPlayerInSuspectRange()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, SuspectRange);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    bool IsPlayerInStopRange()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, StopRange);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    bool IsPlayerInAttackRange()
    {
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll((Vector2)transform.position + GetCurrentHitboxOffset(), AttackRange, 0f);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }
    public void PlayWalkSound()
    {
        if (audioSource != null && WalkSound != null)
        {
            audioSource.PlayOneShot(WalkSound);
        }
    }

    public void PlayDetectedSound()
    {
        if (hasPlayedDetectedSound) return;
        hasPlayedDetectedSound = true;

        AudioSource source = screamAudioSource != null ? screamAudioSource : audioSource;
        if (source != null && DetectedSound != null)
        {
            source.PlayOneShot(DetectedSound);
        }
    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("I'm in range of a door");
        Door door = collision.GetComponent<Door>();
        if (door != null)
        {
            Debug.Log("I'm in range of a door");
            door.UnlockDoor();
            door.needKey = false;
        }
    }
}
