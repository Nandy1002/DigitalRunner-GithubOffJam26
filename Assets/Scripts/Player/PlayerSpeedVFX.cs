using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody), typeof(PlayerState))]
public class PlayerSpeedVFX : MonoBehaviour
{
    [Header("Effect Prefab")]
    [FormerlySerializedAs("highSpeedEffect")]
    [SerializeField] private ParticleSystem highSpeedEffectPrefab;
    [SerializeField] private float cameraForwardDistance = 5f;
    [SerializeField] private Vector2 cameraLocalOffset;
    [SerializeField] private Vector3 cameraLocalEulerAngles;
    [SerializeField, Min(0f)] private float highSpeedThreshold = 18f;

    private Rigidbody rb;
    private PlayerState playerState;
    private ParticleSystem highSpeedEffect;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerState = GetComponent<PlayerState>();

        if (highSpeedEffectPrefab == null)
        {
            return;
        }

        FirstPersonController controller = GetComponent<FirstPersonController>();
        Camera targetCamera = controller != null ? controller.playerCamera : GetComponentInChildren<Camera>();
        if (targetCamera == null)
        {
            Debug.LogError("PlayerSpeedVFX needs a player camera to parent the particle effect to.", this);
            return;
        }

        highSpeedEffect = Instantiate(highSpeedEffectPrefab, targetCamera.transform, false);
        highSpeedEffect.transform.localPosition = new Vector3(
            cameraLocalOffset.x, cameraLocalOffset.y, cameraForwardDistance);
        highSpeedEffect.transform.localEulerAngles = cameraLocalEulerAngles;

        ParticleSystem.MainModule main = highSpeedEffect.main;
        main.playOnAwake = false;
        highSpeedEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (highSpeedEffect == null)
        {
            return;
        }

        Vector3 horizontalVelocity = rb.linearVelocity;
        horizontalVelocity.y = 0f;

        PlayerStateEnum state = playerState.GetCurrentState();
        bool abilityActive = state == PlayerStateEnum.Dashing || state == PlayerStateEnum.WallRun;
        bool shouldPlay = abilityActive || horizontalVelocity.magnitude >= highSpeedThreshold;

        if (shouldPlay && !highSpeedEffect.isPlaying)
        {
            highSpeedEffect.Play();
        }
        else if (!shouldPlay && highSpeedEffect.isPlaying)
        {
            highSpeedEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void OnDisable()
    {
        if (highSpeedEffect != null)
        {
            highSpeedEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
