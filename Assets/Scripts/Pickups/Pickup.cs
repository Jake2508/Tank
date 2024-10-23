using Tank;
using UnityEngine;


public class Pickup : MonoBehaviour
{
    private void OnTriggerEnter(Collider collision)
    {

        if (collision.gameObject.tag == "Player")
        {
            TankController c = collision.GetComponent<TankController>();

            if(c != null)
            {
                GetComponent<IPickupObject>().OnPickup(c);
                Destroy(this.gameObject);
            }
        }
    }
}
