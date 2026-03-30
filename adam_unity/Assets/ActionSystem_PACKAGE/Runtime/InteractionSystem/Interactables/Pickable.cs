using HumanoidInteraction;using UnityEngine;

public class Pickable : Interactable
{
    private bool m_isBeingCarried = false;
    public bool IsBeingCarried => m_isBeingCarried;

    public void OnPick()
    {
        //Debug.Log($"{this.name} has been picked up");
        
        m_isBeingCarried = true;
    }

    public void OnDrop()
    {
        //Debug.Log($"{this.name} has been dropped");
        
        m_isBeingCarried = false;
    }
}
