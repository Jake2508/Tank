using UnityEngine;


public class DamagePopup : MonoBehaviour
{
    public static DamagePopup instance;
    [SerializeField] GameObject damageMessage;


    private void Awake()
    {
        instance = this;
    }


    public void PostMessage(Vector3 worldPositon)
    {
        GameObject go = Instantiate(damageMessage, transform);
        go.transform.position = worldPositon;
        go.transform.parent = GameManager.Instance.particleContainer.transform;

        DamageTextNew damageText = go.GetComponent<DamageTextNew>();
        if(damageText != null)
        {
            damageText.UpdateText();
        }
    }
}
