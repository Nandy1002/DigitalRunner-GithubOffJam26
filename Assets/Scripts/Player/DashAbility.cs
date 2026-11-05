using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(PlayerState))]
public class DashAbility : MonoBehaviour, IAbility
{
    [Header("Dash")]
    [SerializeField, Min(0.01f)] private float dashDuration = 0.2f;
    [SerializeField, Min(0f)] private float dashCooldown = 0.5f;
    [SerializeField, Min(0f)] private float dashDistance = 10f;

    private Rigidbody rb;
    private PlayerState playerState;
    private bool isDashing;
    private float dashTimeRemaining;
    private float cooldownRemaining;
    private float currentDashSpeed;
    private Vector3 dashDirection;
    private PlayerStateEnum stateBeforeDash;

    public bool IsActive => isDashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerState = GetComponent<PlayerState>();
    }

    private void Update()
    {
        if (InputManager.Instance != null && InputManager.Instance.DashPressedThisFrame)
        {
            TryStartDash();
        }
    }

    private void FixedUpdate()
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.fixedDeltaTime);
        }

        if (!isDashing)
        {
            return;
        }

        dashTimeRemaining -= Time.fixedDeltaTime;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = dashDirection.x * currentDashSpeed;
        velocity.z = dashDirection.z * currentDashSpeed;
        rb.linearVelocity = velocity;

        if (dashTimeRemaining <= 0f)
        {
            EndDash();
        }
    }

    private void TryStartDash()
    {
        if (isDashing || cooldownRemaining > 0f || !CanDashFromState(playerState.GetCurrentState()))
        {
            return;
        }

        Vector2 moveInput = InputManager.Instance.Move;
        Vector3 localDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        dashDirection = localDirection.sqrMagnitude > 0.01f
            ? transform.TransformDirection(localDirection).normalized
            : transform.forward;
        dashDirection = Vector3.ProjectOnPlane(dashDirection, Vector3.up).normalized;
        currentDashSpeed = dashDistance / dashDuration;

        stateBeforeDash = playerState.GetCurrentState();
        isDashing = true;
        dashTimeRemaining = dashDuration;
        cooldownRemaining = dashCooldown;
        playerState.SetCurrentState(PlayerStateEnum.Dashing);
    }

    private bool CanDashFromState(PlayerStateEnum state)
    {
        return state == PlayerStateEnum.Idle
            || state == PlayerStateEnum.Walk
            || state == PlayerStateEnum.Run
            || state == PlayerStateEnum.OnAir
            || state == PlayerStateEnum.Crouch;
    }

    private void EndDash()
    {
        isDashing = false;
        dashTimeRemaining = 0f;

        if (playerState.IsInState(PlayerStateEnum.Dashing))
        {
            playerState.SetCurrentState(stateBeforeDash);
        }
    }

    private void OnDisable()
    {
        if (isDashing)
        {
            EndDash();
        }
    }
}
