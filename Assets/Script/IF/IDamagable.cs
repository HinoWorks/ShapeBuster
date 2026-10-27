using UnityEngine;

public interface IDamagable
{
    bool isAlive { get; }
    bool Damage(int damage, float _resourceUpRate = 1f, int attackIndex = -1, int enemiesInArea = 1);
    Transform GetTransform();


}
