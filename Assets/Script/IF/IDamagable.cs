using UnityEngine;

public interface IDamagable
{
    bool isAlive { get; }
    bool Damage(int damage, float _resourceUpRate = 1f);
    Transform GetTransform();


}
