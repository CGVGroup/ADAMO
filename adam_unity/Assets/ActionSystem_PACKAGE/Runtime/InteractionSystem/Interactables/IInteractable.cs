using UnityEngine;

namespace HumanoidInteraction
{
    /// <summary>
    /// Base interface for any object that can be interacted with
    /// </summary>
    public interface IInteractable
    {
        Transform InteractionPoint { get; }
        string Desc { get; }
        bool CanInteract { get; }
    }
} 