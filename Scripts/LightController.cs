using UnityEngine;
using System.Collections;

/// <summary>
/// 控制 Directional Light 的颜色、强度、角度过渡。
/// 挂在场景内任意 GameObject 上，引用场景内的 Directional Light。
/// </summary>
public class LightController : MonoBehaviour
{
    public Light directionalLight;
    public float transitionDuration = 1.5f;

    private Coroutine _currentTransition;

    // -------------------------------------------------------
    // 由 OutsideManager 在场景激活时调用
    // worldTargetRot = sceneRoot.rotation * Quaternion.Euler(lightSettings.localEuler)
    // -------------------------------------------------------
    public void LerpTo(LightSettings target, Quaternion worldTargetRot)
    {
        if (directionalLight == null) return;

        if (_currentTransition != null)
            StopCoroutine(_currentTransition);

        _currentTransition = StartCoroutine(DoLerp(target, worldTargetRot));
    }

    private IEnumerator DoLerp(LightSettings target, Quaternion targetRot)
    {
        Color     startColor     = directionalLight.color;
        float     startIntensity = directionalLight.intensity;
        Quaternion startRot      = directionalLight.transform.rotation;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / transitionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);  // 缓入缓出

            directionalLight.color     = Color.Lerp(startColor, target.color, smoothT);
            directionalLight.intensity = Mathf.Lerp(startIntensity, target.intensity, smoothT);
            directionalLight.transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

            yield return null;
        }

        // 确保最终值精确
        directionalLight.color     = target.color;
        directionalLight.intensity = target.intensity;
        directionalLight.transform.rotation = targetRot;

        _currentTransition = null;
    }
}
