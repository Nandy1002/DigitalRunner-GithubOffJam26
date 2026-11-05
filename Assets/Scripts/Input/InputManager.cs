using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private InputSystem_Actions controls;
    [SerializeField, Range(1f, 2f)] private float sprintHoldDuration = 1.5f;
    private float forwardHoldTime;

    public InputSystem_Actions.PlayerActions Player => controls.Player;
    public Vector2 Move => Player.Move.ReadValue<Vector2>();
    public Vector2 Look => Player.Look.ReadValue<Vector2>();
    public bool LookUsesPointer => Player.Look.activeControl?.device is Pointer;
    public bool ZoomPressedThisFrame => Player.Gravity.WasPressedThisFrame();
    public bool ZoomReleasedThisFrame => Player.Gravity.WasReleasedThisFrame();
    public bool ZoomHeld => Player.Gravity.IsPressed();
    public bool SprintHeld => forwardHoldTime >= sprintHoldDuration;
    public bool JumpPressedThisFrame => Player.Jump.WasPressedThisFrame();
    public bool DashPressedThisFrame => Player.Dash.WasPressedThisFrame();
    public bool CrouchPressedThisFrame => Player.Crouch.WasPressedThisFrame();
    public bool CrouchHeld => Player.Crouch.IsPressed();
    public bool CrouchReleasedThisFrame => Player.Crouch.WasReleasedThisFrame();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateIfMissing()
    {
        if (Instance == null)
        {
            new GameObject(nameof(InputManager)).AddComponent<InputManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        controls = new InputSystem_Actions();
    }

    private void Update()
    {
        if (Move.y > 0.1f)
        {
            forwardHoldTime += Time.deltaTime;
        }
        else
        {
            forwardHoldTime = 0f;
        }
    }

    private void OnEnable()
    {
        controls?.Enable();
    }

    private void OnDisable()
    {
        controls?.Disable();
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        controls?.Dispose();
        Instance = null;
    }
}
