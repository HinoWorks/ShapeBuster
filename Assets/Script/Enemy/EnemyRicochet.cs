using UnityEngine;

/// <summary>
/// 大きく斜めに落ち、壁で反射しながら画面を横切る。
/// </summary>
public class EnemyRicochet : EnemyBase
{
    [SerializeField] float minAngle = 45f;
    [SerializeField] float maxAngle = 65f;

    protected override void OnSpawn()
    {
        if (SpawnedInFormation) return;

        float sign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float angle = sign * UnityEngine.Random.Range(minAngle, maxAngle);
        SetMoveDirection(Quaternion.Euler(0f, 0f, angle) * Vector3.down);
    }
}
