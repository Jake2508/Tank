using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


public class LevelLoaderNew : MonoBehaviour
{
    public Animator transition;
    public float transitionTime = 1f;


    public void LoadNextLevel(int levelID)
    {
        Time.timeScale = 1f;
        StartCoroutine(LoadLevel(levelID));
    }

    IEnumerator LoadLevel(int levelIndex)
    {
        // Play Animation
        transition.SetTrigger("Start");
        // Wait
        yield return new WaitForSeconds(transitionTime);
        // Load Scene
        SceneManager.LoadScene(levelIndex);
    }
}
