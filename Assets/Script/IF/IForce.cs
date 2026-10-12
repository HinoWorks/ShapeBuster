using UnityEngine;

public interface IForce
{
    void ApplyForce(Vector3 _force);
    Transform GetTransform();
}
