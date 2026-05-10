using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [SerializeField] Image fadeImage;
    [SerializeField] Color fadeColor = Color.black;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeImage == null)
        {
            var go = new GameObject("FadeImage");
            go.transform.SetParent(transform, false);
            fadeImage = go.AddComponent<Image>();
            var rt = fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        var c = fadeColor; c.a = 0f; fadeImage.color = c;
        fadeImage.raycastTarget = false;
    }

    public void FadeAndLoad(string sceneName, float fadeTime = 1.5f)
    {
        StartCoroutine(FadeAndLoadCo(sceneName, fadeTime));
    }

    IEnumerator FadeAndLoadCo(string sceneName, float fadeTime)
    {
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            var c = fadeColor; c.a = Mathf.Clamp01(elapsed / fadeTime); fadeImage.color = c;
            yield return null;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public IEnumerator FadeFromBlack(float fadeTime = 1f)
    {
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            var c = fadeColor; c.a = 1f - Mathf.Clamp01(elapsed / fadeTime); fadeImage.color = c;
            yield return null;
        }
    }
}
