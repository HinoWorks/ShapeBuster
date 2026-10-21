using UnityEngine;

/// <summary>
/// 一定の横距離ごとに、左右の向きを鋭く切り替える。
/// </summary>
public class EnemyZigzag : EnemyBase
{
    [SerializeField] float horizontalSpeed = 1.7f;
    [SerializeField] float legDistance = 1.15f;

    float fallSpeed;
    float horizontalSign = 1f;
    float traveled;

    protected override bool RecoverVelocityYEnabled => false;

    protected override void OnSpawn()
    {
        fallSpeed = MoveSpeed;
        horizontalSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        traveled = 0f;
    }

    protected override void TickMovement()
    {
        float step = horizontalSpeed * Time.fixedDeltaTime;
        traveled += step;
        if (traveled >= legDistance)
        {
            traveled = 0f;
            horizontalSign = -horizontalSign;
        }

        SetVelocity(new Vector2(horizontalSign * horizontalSpeed, -fallSpeed));
    }

    protected override void OnWallReflected(Vector2 normal)
    {
        if (Mathf.Abs(normal.x) < 0.2f) return;

        horizontalSign = Mathf.Sign(normal.x);
        traveled = 0f;
    }
}
