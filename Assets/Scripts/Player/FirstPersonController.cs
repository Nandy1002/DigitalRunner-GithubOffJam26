using UnityEngine;

[RequireComponent(typeof(PlayerState))]
public class FirstPersonController : MonoBehaviour
{
    private Rigidbody rb;
    private PlayerState playerState;
    private WallRunAbility wallRunAbility;
    private float currentWallRunLean;

    [Header("Camera Movement")]
    public Camera playerCamera;

    public float fov = 60f;
    public bool invertCamera = false;
    public bool cameraCanMove = true;
    public float mouseSensitivity = 0.15f;
    public float gamepadLookSpeed = 120f;
    public float maxLookAngle = 50f;

    public bool lockCursor = true;

    // Internal Variables
    private float yaw = 0.0f;
    private float pitch = 0.0f;

    [Header("Camera Zoom")]
    public bool enableZoom = true;
    public bool holdToZoom = false;
    public float zoomFOV = 30f;
    public float zoomStepTime = 5f;

    // Internal Variables
    private bool isZoomed = false;


    [Header("Movement")]
    public bool playerCanMove = true;
    public float walkSpeed = 8f;
    public float maxVelocityChange = 15f;
    public float speedTransitionRate = 1.5f;
    public LayerMask groundLayerMask = ~0;

    // Internal Variables
    private float currentMoveSpeed;

    [Header("Sprint")]
    public bool enableSprint = true;
    public float sprintSpeed = 12f;
    public float sprintFOV = 80f;

    // Internal Variables

    [Header("Slide")]
    public bool enableSlide = true;
    public float slideDuration = 0.8f;
    public float slideSpeedMultiplier = 1.25f;
    public float slideDeceleration = 12f;
    public float slideCooldown = 0.4f;
    [Range(0.35f, 0.75f)] public float slideHeightMultiplier = 0.6f;
    [Range(-80f, 80f)] public float slideTiltAngle = -60f;
    public float slidePostureTransitionSpeed = 0.8f;
    public float slideTiltTransitionSpeed = 180f;

    private float slideTimeRemaining;
    private float slideCooldownRemaining;
    private float slideCurrentSpeed;
    private Vector3 slideDirection;
    private float currentSlideTilt;
    private float currentPostureHeightMultiplier = 1f;
    private RigidbodyConstraints standingRigidbodyConstraints;


    [Header("Jump")]
    public bool enableJump = true;
    public float jumpPower = 10f;
    public float jumpBufferTime = 0.2f;
    public float coyoteTime = 0.15f;
    public float risingGravityMultiplier = 1.5f;
    public float fallingGravityMultiplier = 2.5f;

    // Internal Variables
    private float jumpBufferRemaining;
    private float coyoteTimeRemaining;
    private bool groundContactThisStep;


    [Header("Crouch")]
    public bool enableCrouch = true;
    public bool holdToCrouch = true;
    public float crouchHeight = .75f;
    public float speedReduction = .5f;

    // Internal Variables
    private CapsuleCollider playerCapsule;
    private float standingCapsuleHeight;
    private Vector3 standingCapsuleCenter;
    private float standingWalkSpeed;


    [Header("Head Bob")]
    public bool enableHeadBob = true;
    public Transform joint;
    public float bobSpeed = 10f;
    [Range(1f, 2f)] public float sprintBobSpeedMultiplier = 1.35f;
    [Range(0f, 1f)] public float sprintBobAmountMultiplier = 0.5f;
    public Vector3 bobAmount = new Vector3(.15f, .05f, 0f);

    // Internal Variables
    private Vector3 jointOriginalPos;
    private Vector3 jointPosturePosition;
    private float timer = 0;

    private PlayerStateEnum CurrentState => playerState.GetCurrentState();
    private bool IsGrounded => IsControllerLocomotionState(CurrentState) && CurrentState != PlayerStateEnum.OnAir;
    private bool IsSliding => CurrentState == PlayerStateEnum.Slide;
    private bool IsCrouched => CurrentState == PlayerStateEnum.Crouch;
    private bool IsSprinting => CurrentState == PlayerStateEnum.Run || IsSliding;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerState = GetComponent<PlayerState>();
        wallRunAbility = GetComponent<WallRunAbility>();
        standingRigidbodyConstraints = rb.constraints;
        playerCapsule = GetComponent<CapsuleCollider>();

        if (playerState.IsInState(PlayerStateEnum.Idle))
        {
            playerState.SetCurrentState(PlayerStateEnum.OnAir);
        }

        // Set internal variables
        playerCamera.fieldOfView = fov;
        standingCapsuleHeight = playerCapsule.height;
        standingCapsuleCenter = playerCapsule.center;
        standingWalkSpeed = walkSpeed;
        currentMoveSpeed = walkSpeed;
        jointOriginalPos = joint.localPosition;
        jointPosturePosition = jointOriginalPos;

    }

    void Start()
    {
        if(lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

    }

    float camRotation;

    private void Update()
    {
        #region Camera

        // Control camera movement
        if(cameraCanMove)
        {
            InputManager cameraInput = InputManager.Instance;
            Vector2 lookInput = cameraInput.Look;
            float lookScale = cameraInput.LookUsesPointer ? mouseSensitivity : gamepadLookSpeed * Time.deltaTime;

            yaw = transform.localEulerAngles.y + lookInput.x * lookScale;

            if (!invertCamera)
            {
                pitch -= lookScale * lookInput.y;
            }
            else
            {
                // Inverted Y
                pitch += lookScale * lookInput.y;
            }
            // Clamp pitch between lookAngle
            pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);


        }

        #region Camera Zoom

        if (enableZoom)
        {
            if (InputManager.Instance.ZoomPressedThisFrame && !holdToZoom && !IsSprinting)
            {
                isZoomed = !isZoomed;
            }

            if (holdToZoom && !IsSprinting)
            {
                if (InputManager.Instance.ZoomPressedThisFrame)
                {
                    isZoomed = true;
                }
                else if (InputManager.Instance.ZoomReleasedThisFrame)
                {
                    isZoomed = false;
                }
            }
        }

        if (IsSprinting)
        {
            isZoomed = false;
        }

        float sprintProgress = Mathf.InverseLerp(walkSpeed, sprintSpeed, currentMoveSpeed);
        float targetFOV = Mathf.Lerp(fov, sprintFOV, sprintProgress);
        if (enableZoom && isZoomed)
        {
            targetFOV = zoomFOV;
        }

        if (enableZoom && isZoomed)
        {
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, zoomStepTime * Time.deltaTime);
        }
        else
        {
            // Sprint FOV follows currentMoveSpeed, which changes using speedTransitionRate.
            playerCamera.fieldOfView = targetFOV;
        }
        #endregion
        #endregion
        UpdateSlidePosture(Time.deltaTime);

        #region Jump Input Buffer

        if (InputManager.Instance.JumpPressedThisFrame)
        {
            // WallRunAbility consumes this press as a wall jump, so don't also buffer
            // it for an automatic jump when the player lands.
            jumpBufferRemaining = CurrentState == PlayerStateEnum.WallRun ? 0f : jumpBufferTime;
        }
        else if (jumpBufferRemaining > 0f)
        {
            jumpBufferRemaining -= Time.deltaTime;
        }

        #endregion

        #region Crouch and Slide Input

        InputManager input = InputManager.Instance;
        if (input.CrouchPressedThisFrame)
        {
            if (CanStartSlide(input.Move))
            {
                StartSlide(input.Move);
            }
            else if (enableCrouch && IsControllerLocomotionState(CurrentState) && !IsSliding && IsGrounded
                && (!holdToCrouch || !playerState.IsInState(PlayerStateEnum.Crouch)))
            {
                Crouch();
            }
        }

        if (enableCrouch && holdToCrouch && playerState.IsInState(PlayerStateEnum.Crouch)
            && input.CrouchReleasedThisFrame && !IsSliding)
        {
            Crouch();
        }

        if (enableCrouch && holdToCrouch && playerState.IsInState(PlayerStateEnum.Crouch)
            && !input.CrouchHeld && !IsSliding && CanStandUp())
        {
            Crouch();
        }

        #endregion

        bool canJump = enableJump && jumpBufferRemaining > 0f && (IsGrounded || coyoteTimeRemaining > 0f) && CanJumpFromCurrentState();
        if (canJump)
        {
            Jump();
            jumpBufferRemaining = 0f;
            coyoteTimeRemaining = 0f;
        }

        transform.localRotation = Quaternion.Euler(currentSlideTilt, yaw, 0f);
        float targetWallRunLean = wallRunAbility != null ? wallRunAbility.CameraLeanAngle : 0f;
        currentWallRunLean = Mathf.MoveTowardsAngle(currentWallRunLean, targetWallRunLean, 180f * Time.deltaTime);
        playerCamera.transform.localEulerAngles = new Vector3(pitch - currentSlideTilt, 0f, currentWallRunLean);

        if(enableHeadBob)
        {
            HeadBob();
        }
    }

    private void FixedUpdate()
    {
        bool groundedThisStep = groundContactThisStep || CheckGroundedRaycast();
        groundContactThisStep = false;

        if (groundedThisStep)
        {
            coyoteTimeRemaining = coyoteTime;
        }
        else if (coyoteTimeRemaining > 0f)
        {
            coyoteTimeRemaining = Mathf.Max(0f, coyoteTimeRemaining - Time.fixedDeltaTime);
        }

        // WallRunAbility applies its own reduced gravity while wall-running.
        if (!groundedThisStep && CurrentState != PlayerStateEnum.WallRun)
        {
            float gravityMultiplier = rb.linearVelocity.y > 0f
                ? risingGravityMultiplier
                : fallingGravityMultiplier;
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }

        PlayerStateEnum state = CurrentState;
        if (IsControllerLocomotionState(state))
        {
            // Keep the slide state through brief missed ground-contact samples. The slide timer
            // ends the slide; if the player has actually left the ground, EndSlide moves to OnAir.
            if (!groundedThisStep && coyoteTimeRemaining <= 0f && state != PlayerStateEnum.OnAir && state != PlayerStateEnum.Slide)
            {
                SetState(PlayerStateEnum.OnAir);
            }
            else if (groundedThisStep && (state == PlayerStateEnum.OnAir
                || state == PlayerStateEnum.Idle || state == PlayerStateEnum.Walk || state == PlayerStateEnum.Run))
            {
                UpdateGroundMovementState(InputManager.Instance.Move, InputManager.Instance.SprintHeld);
            }
        }

        if (!playerCanMove || !IsControllerLocomotionState(CurrentState))
        {
            return;
        }

        // Preserve wall jump momentum while in the push phase
        if (wallRunAbility != null && wallRunAbility.HasWallJumpMomentum)
        {
            return;
        }

        if (slideCooldownRemaining > 0f)
        {
            slideCooldownRemaining -= Time.fixedDeltaTime;
        }

        if (playerState.IsInState(PlayerStateEnum.Slide))
        {
            slideTimeRemaining -= Time.fixedDeltaTime;
            slideCurrentSpeed = Mathf.MoveTowards(slideCurrentSpeed, standingWalkSpeed, slideDeceleration * Time.fixedDeltaTime);
            if (slideTimeRemaining <= 0f)
            {
                EndSlide(groundedThisStep);
            }
        }

        InputManager input = InputManager.Instance;
        Vector2 moveInput = input.Move;
        state = CurrentState;
        bool sliding = state == PlayerStateEnum.Slide;
        Vector3 moveDirection = sliding
            ? slideDirection
            : new Vector3(moveInput.x, 0f, moveInput.y);
        bool sprinting = state == PlayerStateEnum.Run;
        float targetSpeed = sliding
            ? slideCurrentSpeed
            : sprinting ? sprintSpeed
            : state == PlayerStateEnum.Crouch ? standingWalkSpeed * speedReduction
            : walkSpeed;
        currentMoveSpeed = Mathf.Lerp(currentMoveSpeed, targetSpeed, speedTransitionRate * Time.fixedDeltaTime);

        Vector3 worldMoveDirection = sliding ? moveDirection : transform.TransformDirection(moveDirection);
        Vector3 targetVelocity = worldMoveDirection * currentMoveSpeed;
        Vector3 velocityChange = targetVelocity - rb.linearVelocity;

        float effectiveMaxChange = (state == PlayerStateEnum.OnAir)
            ? maxVelocityChange * 0.25f
            : maxVelocityChange;

        velocityChange.x = Mathf.Clamp(velocityChange.x, -effectiveMaxChange, effectiveMaxChange);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -effectiveMaxChange, effectiveMaxChange);
        velocityChange.y = 0f;

        rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckGroundContacts(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        CheckGroundContacts(collision);
    }

    private void CheckGroundContacts(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                groundContactThisStep = true;
                return;
            }
        }
    }

    private bool CheckGroundedRaycast()
    {
        if (playerCapsule == null)
        {
            return false;
        }

        Vector3 origin = new Vector3(
            playerCapsule.bounds.center.x,
            playerCapsule.bounds.min.y + 0.1f,
            playerCapsule.bounds.center.z);

        float radius = playerCapsule.radius * 0.9f;
        float distance = 0.2f;

        return Physics.SphereCast(
            origin, radius, Vector3.down, out RaycastHit hit, distance,
            groundLayerMask, QueryTriggerInteraction.Ignore)
            && hit.normal.y > 0.5f;
    }

    private void Jump()
    {
        if (!CanJumpFromCurrentState())
        {
            return;
        }

        if (playerState.IsInState(PlayerStateEnum.Slide))
        {
            EndSlide(IsGrounded);
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.AddForce(0f, jumpPower, 0f, ForceMode.Impulse);
        SetState(PlayerStateEnum.OnAir);
        coyoteTimeRemaining = 0f;
    }
    private void Crouch()
    {
        if (playerState.IsInState(PlayerStateEnum.Crouch))
        {
            if (!CanStandUp())
            {
                return;
            }

            walkSpeed = standingWalkSpeed;
            UpdateGroundMovementState(InputManager.Instance.Move, InputManager.Instance.SprintHeld);
        }
        else if (IsGrounded)
        {
            walkSpeed = standingWalkSpeed * speedReduction;
            SetState(PlayerStateEnum.Crouch);
        }
    }
    private bool CanStartSlide(Vector2 moveInput)
    {
        Vector3 horizontalVelocity = rb.linearVelocity;
        horizontalVelocity.y = 0f;
        PlayerStateEnum state = CurrentState;

        return enableSlide && !playerState.IsInState(PlayerStateEnum.Slide)
            && (state == PlayerStateEnum.Walk || state == PlayerStateEnum.Run)
            && IsGrounded && slideCooldownRemaining <= 0f
            && moveInput.y > 0.1f
            && horizontalVelocity.magnitude >= standingWalkSpeed * 0.5f;
    }

    private void StartSlide(Vector2 moveInput)
    {
        slideTimeRemaining = slideDuration;
        slideDirection = Vector3.ProjectOnPlane(
            transform.TransformDirection(new Vector3(moveInput.x, 0f, moveInput.y)), Vector3.up).normalized;
        slideCurrentSpeed = Mathf.Max(currentMoveSpeed, standingWalkSpeed) * slideSpeedMultiplier;
        rb.constraints = standingRigidbodyConstraints & ~RigidbodyConstraints.FreezeRotationX;
        SetState(PlayerStateEnum.Slide);
    }

    private void EndSlide(bool grounded)
    {
        slideCooldownRemaining = slideCooldown;

        if (!grounded)
        {
            SetState(PlayerStateEnum.OnAir);
            return;
        }

        if (!CanStandUp())
        {
            walkSpeed = standingWalkSpeed * speedReduction;
            SetState(PlayerStateEnum.Crouch);
            return;
        }

        walkSpeed = standingWalkSpeed;
        UpdateGroundMovementState(InputManager.Instance.Move, InputManager.Instance.SprintHeld);
    }
    private void SetState(PlayerStateEnum state)
    {
        playerState.SetCurrentState(state);
    }

    private bool CanJumpFromCurrentState()
    {
        PlayerStateEnum state = CurrentState;
        return state == PlayerStateEnum.Idle
            || state == PlayerStateEnum.Walk
            || state == PlayerStateEnum.Run
            || state == PlayerStateEnum.Slide
            || state == PlayerStateEnum.Crouch;
    }

    private bool IsControllerLocomotionState(PlayerStateEnum state)
    {
        return state == PlayerStateEnum.Idle
            || state == PlayerStateEnum.Walk
            || state == PlayerStateEnum.Run
            || state == PlayerStateEnum.OnAir
            || state == PlayerStateEnum.Slide
            || state == PlayerStateEnum.Crouch;
    }

    private void UpdateGroundMovementState(Vector2 moveInput, bool sprintHeld)
    {
        if (moveInput.sqrMagnitude <= 0.01f)
        {
            SetState(PlayerStateEnum.Idle);
        }
        else if (enableSprint && sprintHeld)
        {
            SetState(PlayerStateEnum.Run);
        }
        else
        {
            SetState(PlayerStateEnum.Walk);
        }
    }

    private void UpdateSlidePosture(float deltaTime)
    {
        bool sliding = IsSliding;
        bool crouched = IsCrouched;
        float targetHeightMultiplier = sliding ? slideHeightMultiplier : crouched ? crouchHeight : 1f;
        currentPostureHeightMultiplier = Mathf.MoveTowards(currentPostureHeightMultiplier, targetHeightMultiplier, slidePostureTransitionSpeed * deltaTime);
        SetCapsuleHeight(currentPostureHeightMultiplier);

        float targetTilt = sliding ? slideTiltAngle : 0f;
        currentSlideTilt = Mathf.MoveTowards(currentSlideTilt, targetTilt, slideTiltTransitionSpeed * deltaTime);
        if (!sliding && Mathf.Approximately(currentSlideTilt, 0f) && rb.constraints != standingRigidbodyConstraints)
        {
            rb.rotation = transform.rotation;
            rb.constraints = standingRigidbodyConstraints;
        }
    }
    private void SetCapsuleHeight(float heightMultiplier)
    {
        if (playerCapsule == null)
        {
            return;
        }

        float newHeight = Mathf.Max(playerCapsule.radius * 2f, standingCapsuleHeight * heightMultiplier);
        float heightDifference = standingCapsuleHeight - newHeight;
        playerCapsule.height = newHeight;
        playerCapsule.center = standingCapsuleCenter - Vector3.up * (heightDifference * 0.5f);
        jointPosturePosition = jointOriginalPos - Vector3.up * (heightDifference * 0.5f);
        joint.localPosition = jointPosturePosition;
    }

    private bool CanStandUp()
    {
        if (playerCapsule == null)
        {
            return true;
        }

        float currentTop = playerCapsule.bounds.max.y;
        float standingTop = transform.position.y + (Quaternion.Euler(0f, yaw, 0f) * standingCapsuleCenter).y + standingCapsuleHeight * 0.5f;
        float clearance = standingTop - currentTop;
        if (clearance <= 0.01f)
        {
            return true;
        }

        Vector3 origin = new Vector3(playerCapsule.bounds.center.x, currentTop - 0.02f, playerCapsule.bounds.center.z);
        float probeOffset = playerCapsule.bounds.extents.x * 0.15f;
        Vector3 right = transform.right * probeOffset;
        Vector3 forward = transform.forward * probeOffset;
        float probeDistance = clearance + 0.05f;

        return !Physics.Raycast(origin, Vector3.up, probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !Physics.Raycast(origin + right, Vector3.up, probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !Physics.Raycast(origin - right, Vector3.up, probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !Physics.Raycast(origin + forward, Vector3.up, probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !Physics.Raycast(origin - forward, Vector3.up, probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
    #region Head Bob
    private void HeadBob()
    {
        PlayerStateEnum state = CurrentState;
        bool moving = state == PlayerStateEnum.Slide
            || ((state == PlayerStateEnum.Walk || state == PlayerStateEnum.Run || state == PlayerStateEnum.Crouch)
                && InputManager.Instance.Move.sqrMagnitude > 0.01f);

        if (moving)
        {
            float sprintProgress = Mathf.InverseLerp(walkSpeed, sprintSpeed, currentMoveSpeed);
            float frequency = bobSpeed * Mathf.Lerp(1f, sprintBobSpeedMultiplier, sprintProgress);
            float amountScale = Mathf.Lerp(1f, sprintBobAmountMultiplier, sprintProgress);

            if (state == PlayerStateEnum.Crouch)
            {
                frequency *= speedReduction;
            }

            timer += Time.deltaTime * frequency;
            Vector3 bobOffset = Mathf.Sin(timer) * bobAmount * amountScale;
            joint.localPosition = jointPosturePosition + bobOffset;
        }
        else
        {
            timer = 0f;
            joint.localPosition = Vector3.Lerp(joint.localPosition, jointPosturePosition, Time.deltaTime * bobSpeed);
        }
    }
    #endregion
}
