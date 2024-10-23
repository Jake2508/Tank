using System;
using UnityEngine;


public class DropManager : MonoBehaviour
{
    public static DropManager instance { get; private set; }
    public event EventHandler OnCoinPickup;


    private void Awake()
    {
        instance = this;
    }
}
