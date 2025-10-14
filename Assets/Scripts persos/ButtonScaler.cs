using UnityEngine;

public class ButtonScaler : MonoBehaviour
{
    public Vector3 defaultScale = Vector3.one;
    public Vector3 hoveredScale = new Vector3(1.2f, 1.2f, 1.2f);
    public float transitionTime = 0.1f;

    public void Enlarge()
    {
        StopAllCoroutines();
        StartCoroutine(ScaleTo(hoveredScale));
    }

    public void ResetScale()
    {
        StopAllCoroutines();
        StartCoroutine(ScaleTo(defaultScale));
    }

    private System.Collections.IEnumerator ScaleTo(Vector3 targetScale)
    {
        Vector3 initial = transform.localScale;
        float elapsed = 0f;
        while (elapsed < transitionTime)
        {
            transform.localScale = Vector3.Lerp(initial, targetScale, elapsed / transitionTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = targetScale;
    }
}
