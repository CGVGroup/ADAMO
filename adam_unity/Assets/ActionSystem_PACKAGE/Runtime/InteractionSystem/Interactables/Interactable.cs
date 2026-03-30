using HumanoidInteraction;using UnityEngine;

public class Interactable : MonoBehaviour, IInteractable
{
    public Transform InteractionPoint { get; protected set;}
    public string Desc { get; protected set;}
    public bool CanInteract { get; protected set; }

    protected void Awake()
    {
        InteractionPoint = this.transform;
        Desc = this.name;
        CanInteract = true;
    }
}
