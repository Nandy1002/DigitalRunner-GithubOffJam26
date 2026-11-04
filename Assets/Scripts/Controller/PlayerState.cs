using UnityEngine;

[System.Serializable]
public enum PlayerStateEnum
{
    Idle,
    Walk,
    Run,
    OnAir,
    Attack,
    wallRun,
    Slide,
    Crouch,
    Dead,
    Disabled
}
public class PlayerState : MonoBehaviour
{
    [SerializeField] private PlayerStateEnum currentState;

    public event System.Action<PlayerStateEnum, PlayerStateEnum> StateChanged;


    public PlayerStateEnum GetCurrentState()
    {
        return currentState;
    }

    public bool IsInState(PlayerStateEnum state)
    {
        return currentState == state;
    }

    public void SetCurrentState(PlayerStateEnum newState)
    {
        if (currentState == newState)
        {
            return;
        }

        PlayerStateEnum previousState = currentState;
        currentState = newState;
        StateChanged?.Invoke(previousState, currentState);
    }
}
