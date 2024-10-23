using System.Collections.Generic;
using Tank;
using UnityEngine;


public class PassiveItems : MonoBehaviour
{
    [SerializeField] List<Item> items;
    TankController character;
    [SerializeField] Item armorTest;


    private void Awake()
    {
        character = GetComponent<TankController>();
    }

    public void Equip(Item itemToEquip)
    {
        if(items == null)
        {
            items = new List<Item>();
        }
        items.Add(itemToEquip);
        itemToEquip.Equip(character);
    }

    public void UnEquip (Item itemToUnequip)
    {
        // not used
    }
}
