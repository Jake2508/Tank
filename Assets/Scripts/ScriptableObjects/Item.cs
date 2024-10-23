using Tank;
using UnityEngine;


[CreateAssetMenu]
public class Item : ScriptableObject
{
    public string Name;
    public int armor;


    public void Equip(TankController character)
    {
        character.armor += armor;
    }

    public void UnEquip(TankController character)
    {
        character.armor -= armor;
    }
}
