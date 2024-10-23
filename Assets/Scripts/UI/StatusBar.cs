using UnityEngine;
using UnityEngine.UI;


public class StatusBar : MonoBehaviour
{
    [SerializeField] Image barImage;
    [SerializeField] Image damagedBarImage;

    private const float DAMAGED_SHRINK_TIMER_MAX = 1f;
    private const float DAMAGED_GROW_TIMER_MAX = .3f;

    private float damagedShrinkTimer;
    private float damagedGrowTimer;
    public bool shrink = false;


    public void SetState(int current, int max)
    {
        if (shrink) { damagedShrinkTimer = DAMAGED_SHRINK_TIMER_MAX; }
        else { damagedGrowTimer = DAMAGED_GROW_TIMER_MAX; }

        float state = (float)current;
        state /= max;

        barImage.fillAmount = state;
    }


    private void Update()
    {
        if(shrink)
        {
            damagedShrinkTimer -= Time.deltaTime;
            if (damagedShrinkTimer < 0)
            {
                if (barImage.fillAmount < damagedBarImage.fillAmount)
                {
                    float shrinkSpeed = .75f;
                    damagedBarImage.fillAmount -= shrinkSpeed * Time.deltaTime;
                }
            }
        }
        else
        {
            damagedGrowTimer -= Time.deltaTime;
            if (damagedGrowTimer < 0)
            {
                if (barImage.fillAmount > damagedBarImage.fillAmount)
                {
                    float growSpeed = 0.55f;
                    damagedBarImage.fillAmount += growSpeed * Time.deltaTime;
                }

                if(barImage.fillAmount < damagedBarImage.fillAmount)
                {
                    damagedBarImage.fillAmount = barImage.fillAmount;
                }

            }
        }

    }
}
