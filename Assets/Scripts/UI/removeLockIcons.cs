using UnityEngine;
using UnityEngine.UI;

public class removeLockIcons : MonoBehaviour
{
    [SerializeField] Button thisButton;
    [SerializeField] Image[] lockIcons;
    [SerializeField] Sprite basic;
    private int clicks = 0;
    

    private void Awake()
    {
        thisButton.onClick.AddListener(() =>
        {
            UpdateIcons();
        });
    }

    private void UpdateIcons()
    {
        lockIcons[clicks].sprite = basic;
        lockIcons[clicks].color = Color.green;
        clicks++;
    }
}
