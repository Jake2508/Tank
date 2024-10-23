using System;
using Tank;
using UnityEngine;


public class CoinPickupObject : MonoBehaviour, IPickupObject
{
    [SerializeField] int amount;
    [SerializeField] GameObject coinPickupEffect;
    [SerializeField] CoinType coinType;


    public void OnPickup(TankController character)
    {
        character.level.AddExperience(amount);
        character.level.RecordCoinType(coinType.ToString());

        GameObject c = Instantiate(coinPickupEffect, transform.position, Quaternion.identity);
        c.transform.parent = DropManager.instance.transform;
        SoundManager.Instance.CoinPickedUp();

        Destroy(gameObject);
    }
}


public class CoinPickupEventArgs : EventArgs
{
    public int Amount { get; }
    public CoinType CoinType { get; }


    public CoinPickupEventArgs(int amount, CoinType coinType)
    {
        Amount = amount;
        CoinType = coinType;
    }
}

public enum CoinType
{
    Bronze,
    Silver,
    Gold
}
