using Tank;
using UnityEngine;


public class HealPickupObject : MonoBehaviour, IPickupObject
{
    [SerializeField] int healAmount;
    public void OnPickup(TankController character)
    {
        character.Heal(healAmount);

        Destroy(gameObject);
    }
}
