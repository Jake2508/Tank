using UnityEngine;


public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioClipRefSO audioClipRefSO;
    [SerializeField] private AudioSource button;
    [SerializeField] private AudioSource coin;
    [SerializeField] private AudioSource levelUp;

    private float volume = 1f;


    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }


    // Base Audio Interactions
    public void PlaySound(AudioClip audioClip, Vector3 position, float volume = 1f)
    {
        AudioSource.PlayClipAtPoint(audioClip, position, volume);
    }

    public void PlayExplosionSound(Vector3 position)
    {
        AudioSource.PlayClipAtPoint(audioClipRefSO.Explosion, position, volume);
    }

    public void FireShotSound(Vector3 position)
    {
        AudioSource.PlayClipAtPoint(audioClipRefSO.bulletShot, position, volume);
    }

    public void CoinPickedUp()
    {
        if(coin != null)
        {
            coin.Play();
        }
    }

    public void PlayButtonSound()
    {
        if(button != null)
        {
            button.Play();
        }

    }

    public void LevelUpSound()
    {
        if(levelUp != null)
        {
            levelUp.Play();
        }

    }

    public void PlaySound(AudioClip[] audioClipArray, Vector3 position, float volume = 1f)
    {
        PlaySound(audioClipArray[Random.Range(0, audioClipArray.Length)], position, volume);
    }
}
