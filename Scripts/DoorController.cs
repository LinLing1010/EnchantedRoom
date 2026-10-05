using System.Collections;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Pivot & Animation")]
    public float openAngleDelta = -140f;
    [SerializeField] private float openDuration  = 0.6f;
    [SerializeField] private float stayOpenTime  = 2.0f;
    [SerializeField] private float closeDuration = 0.5f;
    [SerializeField] private AnimationCurve openCurve  = AnimationCurve.EaseInOut(0,0,1,1);
    [SerializeField] private AnimationCurve closeCurve = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Outline Layer")]
    [SerializeField] private string outlineLayerName = "Outline";
    [SerializeField] private string defaultLayerName = "Default";

    [Header("Wall Cover")]
    public Material wallMaterial;
    private static readonly int LerpProp = Shader.PropertyToID("_Lerp");

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Outside")]
    public OutsideManager outsideManager;

    // ── 缓存 ─────────────────────────────────────────────
    private int          _outlineLayer;
    private int          _defaultLayer;
    private GameObject[] _rendererObjects;
    private bool         _isOutlineActive;
    private bool         _isAnimating;
    private Coroutine    _doorRoutine;

    void Awake()
    {
        // 只做不依赖其他物体的初始化
        _outlineLayer = LayerMask.NameToLayer(outlineLayerName);
        _defaultLayer = LayerMask.NameToLayer(defaultLayerName);

        var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        _rendererObjects = new GameObject[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            _rendererObjects[i] = renderers[i].gameObject;

        ApplyLayer(_defaultLayer);
    }

    void Start()
    {
        // 依赖其他物体的初始化放这里
        if (outsideManager == null)
            outsideManager = FindAnyObjectByType<OutsideManager>();

        if (wallMaterial == null)
        {
            var wallRenderer = GameObject.FindWithTag("Wall")?.GetComponent<Renderer>();
            if (wallRenderer != null)
                wallMaterial = wallRenderer.sharedMaterial;
        }

        wallMaterial?.SetFloat(LerpProp, 0f);
    }

    void OnEnable()
    {
        GestureEvents.OnGunAimingDoor += OnGunAiming;
        GestureEvents.OnGunAimLost    += OnGunAimLost;
        GestureEvents.OnGunShot       += OnGunShot;
    }

    void OnDisable()
    {
        GestureEvents.OnGunAimingDoor -= OnGunAiming;
        GestureEvents.OnGunAimLost    -= OnGunAimLost;
        GestureEvents.OnGunShot       -= OnGunShot;
    }

    private void OnGunAiming(DoorController targeted)
    {
        bool shouldBeActive = (targeted == this);
        if (shouldBeActive != _isOutlineActive)
            SetOutline(shouldBeActive);
    }

    private void OnGunAimLost()
    {
        if (_isOutlineActive) SetOutline(false);
    }

    private void OnGunShot()
    {
        if (!_isOutlineActive) return;
        SetOutline(false);
        if (_isAnimating) return;
        if (_doorRoutine != null) StopCoroutine(_doorRoutine);
        _doorRoutine = StartCoroutine(OpenThenCloseRoutine());
    }

    private IEnumerator OpenThenCloseRoutine()
    {
        _isAnimating = true;
        Quaternion VfxRot = transform.rotation;
        Collider col = GetComponentInChildren<Collider>();
        Vector3 vfxPos =  col.bounds.center;
        
        // 2. 切换 outside environment + light
        outsideManager?.ActivateRandom();
        
        // 拿当前场景 config
        OutsideSceneConfig cfg = outsideManager?._activeSceneConfig;

        
        // 1. 开门音效
        if (cfg?.doorOpenSound != null && audioSource != null)
            audioSource.PlayOneShot(cfg.doorOpenSound);
        

        // 3. 设置 second texture，然后 lerp 墙纸 0→1
        if (cfg?.secondBaseColorTexture != null && wallMaterial != null)
        {
            wallMaterial.SetTexture("_SecondBaseColorTexture", cfg.secondBaseColorTexture);
            yield return LerpWall(0f, 1f, cfg.wallLerpInDuration);
        }

        // 4. 开门动画
        Quaternion closedRot = transform.rotation;
        Quaternion openRot   = closedRot * Quaternion.Euler(0f, openAngleDelta, 0f);
        yield return AnimateDoor(closedRot, openRot, openDuration, openCurve);

        // 5. 门洞特效
        if (cfg?.doorFxPrefab != null)
        {
            vfxPos.y = transform.position.y;  // y 对齐门的根节点（地面）
            Instantiate(cfg.doorFxPrefab, vfxPos,  VfxRot);
        }

        // 6. 等待开门时间
        yield return new WaitForSeconds(stayOpenTime);

        // 7. 关门动画 + 关门音效
        if (cfg?.doorCloseSound != null && audioSource != null)
            audioSource.PlayOneShot(cfg.doorCloseSound);
        yield return AnimateDoor(openRot, closedRot, closeDuration, closeCurve);

        // 8. 等待额外时间后 lerp 墙纸 1→0，同时切回 environment 和 light
        float extraTime = cfg?.stayOpenExtraTime ?? 6f;
        float lerpOutDuration = cfg?.wallLerpOutDuration ?? 6f;

        yield return new WaitForSeconds(extraTime);

        // 同时：environment 切回 default，light 切回，墙纸 lerp 回 0
        outsideManager?.Deactivate();
        if (wallMaterial != null)
            yield return LerpWall(1f, 0f, lerpOutDuration);

        _isAnimating = false;
        GestureEvents.Fire_DoorSequenceComplete();
    }

    private IEnumerator LerpWall(float from, float to, float duration)
    {
        if (wallMaterial == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            wallMaterial.SetFloat(LerpProp, Mathf.Lerp(from, to, t));
            yield return null;
        }
        wallMaterial.SetFloat(LerpProp, to);
    }

    private IEnumerator AnimateDoor(Quaternion from, Quaternion to, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.rotation = Quaternion.Lerp(from, to, curve.Evaluate(t));
            yield return null;
        }
        transform.rotation = to;
    }

    private void SetOutline(bool active)
    {
        _isOutlineActive = active;
        ApplyLayer(active ? _outlineLayer : _defaultLayer);
    }

    private void ApplyLayer(int layer)
    {
        foreach (var go in _rendererObjects)
            go.layer = layer;
    }
}