using UnityEngine;

/// <summary>
/// 上では速く、画面下に近づくほど遅くなる。
/// </summary>
public class EnemyBrakeFall : EnemyBase
{
    [SerializeField] float minSpeedRatio = 0.18f;
    [SerializeField] float topY = 6f;
    [SerializeField] float bottomY = -4.5f;

    float initialSpeed;

    protected override bool RecoverVelocityYEnabled => false;

    protected override void OnSpawn()
    {
        initialSpeed = MoveSpeed;
    }

    protected override void TickMovement()
    {
        float height = Mathf.Max(0.01f, topY - bottomY);
        float t = Mathf.Clamp01((topY - transform.position.y) / height);
        MoveSpeed = Mathf.Lerp(initialSpeed, initialSpeed * minSpeedRatio, t);
    }
}
