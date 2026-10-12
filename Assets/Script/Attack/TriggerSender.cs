using UnityEngine;
using System;

public class TriggerSender : MonoBehaviour
{
    public event Action<Collider> OnEnter;
    public event Action<Collider> OnExit;



    void OnTriggerEnter(Collider other)
        => OnEnter?.Invoke(other);

    void OnTriggerExit(Collider other)
        => OnExit?.Invoke(other);


}
