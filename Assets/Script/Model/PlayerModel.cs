using UnityEngine;
using System;

public class PlayerModel
{
    // ===== THÔNG TIN CƠ BẢN =====
    public string SessionId { get; private set; }
    public string Username { get; private set; }
    public bool IsLocalPlayer { get; private set; }
    public Player NetworkState { get; private set; }

    // ===== CHUYỂN ĐỘNG =====
    public Vector3 Position { get; set; }
    public float RotationY { get; set; }
    public float MoveSpeed { get; set; } = 5f;
    public float RotationSpeed { get; set; } = 10f;

    // ===== MẠNG =====
    public Vector3 NetworkPosition { get; set; }
    public Quaternion NetworkRotation { get; set; }
    public float SendInterval { get; set; } = 0.1f;
    public float LerpSpeed { get; set; } = 10f;
    public float LastSendTime { get; set; }

    // ===== ANIMATION =====
    public float MovementSpeed { get; set; }

    // ===== CLICK-TO-MOVE =====
    public bool IsMovingToTarget { get; set; }
    public Vector3 TargetPosition { get; set; }
    public float StoppingDistance { get; set; } = 0.5f;

    // ===== SỰ KIỆN =====
    public event Action<Vector3, float> OnPositionUpdated;
    public event Action<float> OnSpeedChanged;

    // ===== CONSTRUCTOR =====
    public PlayerModel(string sessionId, Player state, bool isLocalPlayer, float moveSpeed = 5f, float rotationSpeed = 10f)
    {
        SessionId = sessionId;
        NetworkState = state;
        IsLocalPlayer = isLocalPlayer;
        Username = state.username;
        
        MoveSpeed = moveSpeed;
        RotationSpeed = rotationSpeed;
        
        Position = new Vector3(state.x, state.y, state.z);
        RotationY = state.rotationY;
        NetworkPosition = Position;
        NetworkRotation = Quaternion.Euler(0, RotationY, 0);
        
        IsMovingToTarget = false;
    }

    // ===== CẬP NHẬT TỪ MẠNG (Remote Player) =====
    public void UpdateFromNetwork(float x, float y, float z, float rotY)
    {
        if (IsLocalPlayer) return;

        NetworkState.x = x;
        NetworkState.y = y;
        NetworkState.z = z;
        NetworkState.rotationY = rotY;
        
        NetworkPosition = new Vector3(x, y, z);
        NetworkRotation = Quaternion.Euler(0, rotY, 0);
        
        OnPositionUpdated?.Invoke(NetworkPosition, rotY);
    }

    // ===== CẬP NHẬT TỪ INPUT (Local Player) =====
    public void UpdateLocalPosition(Vector3 newPosition, float newRotationY)
    {
        if (!IsLocalPlayer) return;

        Position = newPosition;
        RotationY = newRotationY;
        
        OnPositionUpdated?.Invoke(Position, RotationY);
    }

    // ===== CẬP NHẬT TỐC ĐỘ ANIMATION =====
    public void SetMovementSpeed(float speed)
    {
        if (Mathf.Abs(MovementSpeed - speed) > 0.01f)
        {
            MovementSpeed = speed;
            OnSpeedChanged?.Invoke(speed);
        }
    }

    // ===== CLICK-TO-MOVE: SET TARGET =====
    public void SetTargetPosition(Vector3 target)
    {
        TargetPosition = target;
        IsMovingToTarget = true;
    }

    // ===== CLICK-TO-MOVE: STOP =====
    public void StopMovingToTarget()
    {
        IsMovingToTarget = false;
    }

    // ===== CLICK-TO-MOVE: CHECK ĐÃ ĐẾN ĐÍCH CHƯA =====
    public bool HasReachedTarget(Vector3 currentPosition)
    {
        // Chỉ tính khoảng cách XZ (bỏ qua Y)
        float distanceXZ = Vector2.Distance(
            new Vector2(currentPosition.x, currentPosition.z),
            new Vector2(TargetPosition.x, TargetPosition.z)
        );
        
        return distanceXZ <= StoppingDistance;
    }
}
