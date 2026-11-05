using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(PlayerState))]
public class WallRunAbility : MonoBehaviour, IAbility
{
    [Header("Wall Run")]
    [SerializeField] private float wallRunSpeed = 10f;
    [SerializeField] private float wallCheckDistance = 1.2f;
    [SerializeField, Min(0f)] private float wallSnapSpeed = 6f;
    [SerializeField, Min(0f)] private float wallClearance = 0.05f;
    [SerializeField, Range(0f, 45f)] private float wallRunCameraRoll = 20f;
    [SerializeField] private float wallCheckHeight = 1f;
    [SerializeField] private LayerMask wallLayerMask = ~0;
    [SerializeField] private float wallRunGravityMultiplier = 0.25f;
    [SerializeField, Min(0f)] private float maxWallRunFallSpeed = 1.5f;
    [SerializeField] private float maxWallRunDuration = 2f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Header("Wall Jump")]
    [SerializeField, Min(0f)] private float wallJumpUpForce = 9f;
    [SerializeField, Min(0f)] private float wallJumpAwayForce = 8f;
    [SerializeField, Min(0f)] private float wallJumpForwardMultiplier = 1f;
    [SerializeField, Min(0f)] private float wallJumpRegrabCooldown = 0.75f;
    [SerializeField, Min(0f)] private float wallJumpPushDuration = 0.45f;
    private Rigidbody rb;
    private PlayerState playerState;
    private CapsuleCollider capsuleCollider;
    private bool isWallRunning;
    private bool jumpQueued;
    private float jumpBufferTimer;
    private float wallRunTimer;
    private Vector3 wallNormal;
    private Collider lastWallCollider;
    private Vector3 lastWallPoint;
    private Vector3 lastWallNormal;
    private bool hasWallJumpedInAir;
    private float wallJumpRegrabTimer;
    private float wallJumpPushTimer;
    private readonly RaycastHit[] wallHitBuffer = new RaycastHit[8];

    public bool IsActive => isWallRunning;
    public bool HasWallJumpMomentum => wallJumpPushTimer > 0f;

    public float CameraLeanAngle
    {
        get
        {
            if (!isWallRunning)
            {
                return 0f;
            }

            bool wallIsOnLeft = Vector3.Dot(transform.right, wallNormal) > 0f;
            return wallIsOnLeft ? -wallRunCameraRoll : wallRunCameraRoll;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerState = GetComponent<PlayerState>();
        capsuleCollider = GetComponent<CapsuleCollider>();
    }

    private void Update()
    {
        // Cache the press until FixedUpdate so this works at any render/physics rate.
        if (InputManager.Instance != null && InputManager.Instance.JumpPressedThisFrame)
        {
            jumpQueued = true;
            jumpBufferTimer = 0.2f;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime;
            if (jumpBufferTimer <= 0f)
            {
                jumpQueued = false;
            }
        }
    }

    private void FixedUpdate()
    {
        if (wallJumpRegrabTimer > 0f)
        {
            wallJumpRegrabTimer = Mathf.Max(0f, wallJumpRegrabTimer - Time.fixedDeltaTime);
        }

        if (wallJumpPushTimer > 0f)
        {
            wallJumpPushTimer = Mathf.Max(0f, wallJumpPushTimer - Time.fixedDeltaTime);
        }

        bool grounded = IsGrounded();
        if (grounded)
        {
            hasWallJumpedInAir = false;
            lastWallCollider = null;
            lastWallNormal = Vector3.zero;
            wallJumpRegrabTimer = 0f;
        }

        bool foundWall = FindWall(out Vector3 detectedWallNormal, out float detectedWallDistance,
            out Collider detectedWallCollider, out Vector3 detectedWallPoint);
        bool movingForward = InputManager.Instance != null && InputManager.Instance.Move.y > 0.1f;

        if (isWallRunning)
        {
            if (jumpQueued)
            {
                WallJump();
            }
            else if (grounded || !foundWall || !movingForward ||
                     wallRunTimer >= maxWallRunDuration)
            {
                StopWallRun();
            }
            else
            {
                wallNormal = detectedWallNormal;
                lastWallCollider = detectedWallCollider;
                lastWallPoint = detectedWallPoint;
                lastWallNormal = detectedWallNormal;
                ContinueWallRun(detectedWallDistance);
            }
        }
        else if (playerState.IsInState(PlayerStateEnum.OnAir)
                 && !grounded && foundWall && movingForward)
        {
            StartWallRun(detectedWallNormal, detectedWallCollider, detectedWallPoint);
        }
    }

    private bool FindWall(out Vector3 normal, out float distance, out Collider wallCollider, out Vector3 point)
    {
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;

        if (TryFindWallInDirection(origin, transform.right, out RaycastHit rightHit)
            || TryFindWallInDirection(origin, -transform.right, out rightHit))
        {
            normal = rightHit.normal;
            distance = rightHit.distance;
            wallCollider = rightHit.collider;
            point = rightHit.point;
            return true;
        }

        normal = Vector3.zero;
        distance = 0f;
        wallCollider = null;
        point = Vector3.zero;
        return false;
    }

    private bool TryFindWallInDirection(Vector3 origin, Vector3 direction, out RaycastHit closestHit)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            origin, 0.15f, direction, wallHitBuffer, wallCheckDistance,
            wallLayerMask, QueryTriggerInteraction.Ignore);

        closestHit = default;
        float closestDistance = float.MaxValue;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = wallHitBuffer[i];
            bool isSameWallSurface = (hit.collider == lastWallCollider)
                || (lastWallNormal != Vector3.zero && Vector3.Dot(hit.normal, lastWallNormal) > 0.3f);

            bool blockedByWallJump = (hasWallJumpedInAir || wallJumpRegrabTimer > 0f) && isSameWallSurface;

            if (hit.collider == null
                || hit.transform == transform
                || hit.transform.IsChildOf(transform)
                || blockedByWallJump
                || hit.distance >= closestDistance)
            {
                continue;
            }

            closestHit = hit;
            closestDistance = hit.distance;
            foundHit = true;
        }

        return foundHit;
    }

    private bool IsGrounded()
    {
        if (capsuleCollider == null)
        {
            capsuleCollider = GetComponent<CapsuleCollider>();
        }

        Vector3 checkPosition;
        if (capsuleCollider != null)
        {
            // Ground check from the bottom of the capsule collider bounds to avoid head bob or joint offsets.
            checkPosition = new Vector3(
                capsuleCollider.bounds.center.x,
                capsuleCollider.bounds.min.y + groundCheckRadius,
                capsuleCollider.bounds.center.z);
        }
        else if (groundCheck != null)
        {
            checkPosition = groundCheck.position;
        }
        else
        {
            checkPosition = transform.position;
        }

        // A downward ray avoids counting a nearby vertical wall as ground.
        RaycastHit[] hits = Physics.RaycastAll(
            checkPosition + Vector3.up * 0.05f, Vector3.down, groundCheckRadius + 0.15f,
            groundLayerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            bool isPlayerCollider = hitTransform == transform || hitTransform.IsChildOf(transform);
            if (!isPlayerCollider && hits[i].normal.y > 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private void StartWallRun(Vector3 normal, Collider wallCollider, Vector3 point)
    {
        isWallRunning = true;
        hasWallJumpedInAir = false;
        wallNormal = normal;
        lastWallCollider = wallCollider;
        lastWallPoint = point;
        lastWallNormal = normal;
        wallRunTimer = 0f;
        playerState.SetCurrentState(PlayerStateEnum.WallRun);
    }

    private void ContinueWallRun(float wallHitDistance)
    {
        wallRunTimer += Time.fixedDeltaTime;

        Vector3 runDirection =
            Vector3.ProjectOnPlane(transform.forward, wallNormal).normalized;

        if (runDirection.sqrMagnitude > 0f)
        {
            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(
                runDirection.x * wallRunSpeed,
                Mathf.Max(velocity.y, -maxWallRunFallSpeed),
                runDirection.z * wallRunSpeed);
        }

        SnapTowardWall(wallHitDistance);

        // Reduce the effect of gravity while wall-running.
        rb.AddForce(
            -Physics.gravity * (1f - wallRunGravityMultiplier),
            ForceMode.Acceleration);
    }

    private void SnapTowardWall(float wallHitDistance)
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        float playerRadius = capsule != null
            ? capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z)
            : 0.5f;
        const float wallCastRadius = 0.15f;
        float distanceFromPlayerToWall = wallHitDistance + wallCastRadius - playerRadius;
        float distanceToClose = distanceFromPlayerToWall - wallClearance;
        if (distanceToClose <= 0f)
        {
            return;
        }

        float targetSnapSpeed = Mathf.Min(wallSnapSpeed, distanceToClose / Time.fixedDeltaTime);
        float currentSnapSpeed = Vector3.Dot(rb.linearVelocity, -wallNormal);
        rb.AddForce(-wallNormal * (targetSnapSpeed - currentSnapSpeed), ForceMode.VelocityChange);
    }

    private void WallJump()
    {
        hasWallJumpedInAir = true;
        lastWallNormal = wallNormal;
        wallJumpRegrabTimer = wallJumpRegrabCooldown;
        wallJumpPushTimer = wallJumpPushDuration;
        jumpQueued = false;
        jumpBufferTimer = 0f;
        StopWallRun();

        Vector3 wallForward = Vector3.ProjectOnPlane(transform.forward, wallNormal).normalized;
        float forwardSpeed = Mathf.Max(Vector3.Dot(rb.linearVelocity, wallForward), wallRunSpeed * 0.75f);
        Vector3 forwardVelocity = wallForward * (forwardSpeed * wallJumpForwardMultiplier);

        Vector3 jumpVelocity = forwardVelocity + wallNormal * wallJumpAwayForce + Vector3.up * wallJumpUpForce;
        rb.linearVelocity = jumpVelocity;
    }

    private void StopWallRun()
    {
        isWallRunning = false;
        hasWallJumpedInAir = true;
        wallRunTimer = 0f;
        if (playerState.IsInState(PlayerStateEnum.WallRun))
        {
            playerState.SetCurrentState(PlayerStateEnum.OnAir);
        }
    }

    private void OnDisable()
    {
        if (isWallRunning)
        {
            StopWallRun();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin + transform.right * wallCheckDistance, 0.15f);
        Gizmos.DrawWireSphere(origin - transform.right * wallCheckDistance, 0.15f);

        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
