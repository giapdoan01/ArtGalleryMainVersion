using UnityEngine;
using System.Collections.Generic;

public class NPCPatrol : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private List<Transform> movePoints;
    [SerializeField] private float speed            = 3f;
    [SerializeField] private float stoppingDistance = 0.2f;

    private int currentIndex = 0;

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Update()
    {
        if (movePoints == null || movePoints.Count == 0) return;

        MoveToCurrentPoint();
        CheckAndAdvance();
    }

    // ═══════════════════════════════════════════════
    // CORE
    // ═══════════════════════════════════════════════

    private void MoveToCurrentPoint()
    {
        Transform target = movePoints[currentIndex];
        if (target == null) return;

        // Di chuyển
        Vector3 dir = (target.position - transform.position).normalized;
        transform.position += dir * speed * Time.deltaTime;

        // Xoay mặt về hướng di chuyển
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                10f * Time.deltaTime
            );
    }

    private void CheckAndAdvance()
    {
        Transform target = movePoints[currentIndex];
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= stoppingDistance)
        {
            // Sang điểm tiếp theo, về 0 nếu hết list → lặp lại
            currentIndex = (currentIndex + 1) % movePoints.Count;
        }
    }
}