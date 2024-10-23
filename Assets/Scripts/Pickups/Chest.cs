using UnityEngine;


public class Chest : MonoBehaviour, IDamagable
{
    [SerializeField] private GameObject chestOpenedVisual;


    public void TakeDamage(int damage)
    {
        ChestOpenedVisual();

        Destroy(gameObject);
    }


    private void ChestOpenedVisual()
    {
        Instantiate(chestOpenedVisual, transform.position, transform.rotation);

        GameManager.Instance.ChestOpenedAddHealth();
    }
}
