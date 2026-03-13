using UnityEngine;

public class Model3DRotate : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 1f;

    private float defaultSpeed;

    private void Awake()
    {
        defaultSpeed = rotateSpeed;
    }

    private void Update()
    {
        if (rotateSpeed == 0f) return;
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
    }

    public void Stop()   => rotateSpeed = 0f;
    public void Resume() => rotateSpeed = defaultSpeed;
}
