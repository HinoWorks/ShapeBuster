using UnityEngine;

/// <summary>
/// 少し落ちて止まり、また落ちる。
/// </summary>
public class EnemyStepFall : EnemyBase
{
    [SerializeField] float fallDuration = 0.48f;
    [SerializeField] float pauseDuration = 0.38f;

    float fallSpeed;
    float phaseTime;
    bool pausing;

    protected override bool RecoverVelocityYEnabled => false;

    protected override void OnSpawn()
    {
        fallSpeed = MoveSpeed;
        phaseTime = 0f;
        pausing = false;
    }

    protected override void TickMovement()
    {
        phaseTime += Time.fixedDeltaTime;
        float duration = pausing ? pauseDuration : fallDuration;
        if (phaseTime >= duration)
        {
            phaseTime = 0f;
            pausing = !pausing;
        }

        MoveSpeed = pausing ? 0f : fallSpeed;
    }
}
