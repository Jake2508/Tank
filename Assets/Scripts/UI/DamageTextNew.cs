using System.Collections.Generic;
using TMPro;
using UnityEngine;


public class DamageTextNew : MonoBehaviour
{
    [SerializeField] TextMeshPro HitText;

    private Animator animator;
    private Canvas canvas;


    private List<string> explosionTexts = new List<string>
    {
        "Boom",
        "Pow",
        "Blam",
        "Crash",
        "Bang",
        "Kabang",
        "Thud",
        "Crunch",
        "Kaboom",
        "Wham",
        "Kapow",
        "Explosion",
    };

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        UpdateText();
        animator.SetTrigger("Popup");
    }
    private string GetRandomExplosionText()
    {
        int randomIndex = Random.Range(0, explosionTexts.Count);
        return explosionTexts[randomIndex];
    }


    public void UpdateText()
    {
        HitText.text = GetRandomExplosionText();
        //Debug.Log(GetRandomExplosionText());
    }
}
