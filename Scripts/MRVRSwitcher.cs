using UnityEngine;
using System.Collections.Generic;

public class MRVRSwitcher : MonoBehaviour
{
    [Header("References")]
    public OVRPassthroughLayer passthroughLayer;
    public Transform roomRoot;
    public Transform outsideRoot;

    [Header("Materials - Walls/Floor with _ProgressBar")]
    public List<Material> progressBarMaterials = new List<Material>();

    [Header("Progress Bar Mesh")]
    [Tooltip("Tag of the ceiling mesh GameObject to find at runtime")]
    public string progressBarMeshTag = "CeilingMesh";
    private Renderer _progressBarMeshRenderer;

    private Renderer GetProgressBarMeshRenderer()
    {
        if (_progressBarMeshRenderer != null) return _progressBarMeshRenderer;
        var go = GameObject.FindWithTag(progressBarMeshTag);
        if (go != null) _progressBarMeshRenderer = go.GetComponent<Renderer>();
        return _progressBarMeshRenderer;
    }

    [Header("Door/Window")]
    public Material doorWindowMaterial;
    public string depthMaskPropertyName = "_IsDepthMask";

    [Header("Settings")]
    public float passthroughFadeDuration = 0.4f;
    public float progressBarFadeDuration = 0.6f;

    [Header("Startup")]
    [Tooltip("Seconds to wait before showing the room after launch")]
    public float startupDelay = 4f;

    enum SceneMode { MR, VR }
    SceneMode _mode = SceneMode.MR;

    // Passthrough
    float _ptTarget, _ptCurrent;

    // ProgressBar
    float _pbTarget, _pbCurrent;
    bool _pbAnimating;
    static readonly int _ProgressBar = Shader.PropertyToID("_ProgressBar");

    // Startup
    bool _startupDone;
    bool _startupAnimCompleted;
    float _startupTimer;

    void Start()
    {
        _mode = SceneMode.MR;

        _ptTarget = _ptCurrent = 1f;
        ApplyPassthrough(_ptCurrent);

        _pbTarget = _pbCurrent = 0f;
        _pbAnimating = false;
        ApplyProgressBar(_pbCurrent);

        SetDoorWindowDepthMask(false);
        ApplyDoorWindowProgressBar(0f);

        SetRoomVisible(false);
        SetOutsideVisible(false);

        _startupDone = false;
        _startupAnimCompleted = false;
        _startupTimer = 0f;
    }

    void OnEnable()  => GestureEvents.OnBothHandsPinchStart += ToggleScene;
    void OnDisable() => GestureEvents.OnBothHandsPinchStart -= ToggleScene;

    void Update()
    {
        // Startup: wait for startupDelay seconds
        if (!_startupDone)
        {
            _startupTimer += Time.deltaTime;
            if (_startupTimer >= startupDelay)
            {
                _startupDone = true;
                StartupSequence();
            }
            return;
        }

        // Passthrough fade
        if (!Mathf.Approximately(_ptCurrent, _ptTarget))
        {
            _ptCurrent = Mathf.MoveTowards(_ptCurrent, _ptTarget,
                Time.deltaTime / passthroughFadeDuration);
            ApplyPassthrough(_ptCurrent);
        }

        // ProgressBar fade
        if (_pbAnimating)
        {
            _pbCurrent = Mathf.MoveTowards(_pbCurrent, _pbTarget,
                Time.deltaTime / progressBarFadeDuration);
            ApplyProgressBar(_pbCurrent);

            // During startup, door/window also animates with ProgressBar
            if (_mode == SceneMode.VR && !_startupAnimCompleted)
            {
                ApplyDoorWindowProgressBar(_pbCurrent);
            }

            if (Mathf.Approximately(_pbCurrent, _pbTarget))
            {
                _pbAnimating = false;
                OnProgressBarAnimComplete();
            }
        }
    }

    // ===================== Startup =====================

    void StartupSequence()
    {
        _mode = SceneMode.VR;
        _ptTarget = 0f;

        SetDoorWindowDepthMask(false);
        SetRoomVisible(true);

        _pbTarget = 1f;
        _pbAnimating = true;

        Debug.Log("[MRVRSwitcher] Startup -> VR mode (door/window animation included)");
    }

    void OnProgressBarAnimComplete()
    {
        if (_mode == SceneMode.VR && Mathf.Approximately(_pbTarget, 1f))
        {
            if (!_startupAnimCompleted)
            {
                _startupAnimCompleted = true;
                SetOutsideVisible(true);
                Debug.Log("[MRVRSwitcher] Startup animation complete, outsideRoot visible");
            }

            // VR animation done -> switch door/window to Depth Mask
            SetDoorWindowDepthMask(true);
            Debug.Log("[MRVRSwitcher] Door/window switched to Depth Mask");
        }

        if (_mode == SceneMode.MR && Mathf.Approximately(_pbCurrent, 0f))
        {
            //SetRoomVisible(false);
            Debug.Log("[MRVRSwitcher] Room hidden");
        }
    }

    // ===================== Toggle =====================

    void ToggleScene()
    {
        if (!_startupDone) return;
        if (_pbAnimating) return;

        if (_mode == SceneMode.MR) SwitchToVR();
        else                       SwitchToMR();
    }

    void SwitchToVR()
    {
        _mode = SceneMode.VR;
        _ptTarget = 0f;

        // Door/window back to normal material; will switch to Depth Mask after animation
        SetDoorWindowDepthMask(false);

        SetRoomVisible(true);
        _pbTarget = 1f;
        _pbAnimating = true;

        Debug.Log("[MRVRSwitcher] -> VR mode");
    }

    void SwitchToMR()
    {
        _mode = SceneMode.MR;
        _ptTarget = 1f;

        // Door/window immediately to Depth Mask
        SetDoorWindowDepthMask(true);

        _pbTarget = 0f;
        _pbAnimating = true;

        Debug.Log("[MRVRSwitcher] -> MR mode");
    }

    // ===================== Helpers =====================

    void SetRoomVisible(bool visible)
    {
        if (roomRoot == null) return;
        foreach (var r in roomRoot.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    void SetOutsideVisible(bool visible)
    {
        if (outsideRoot == null) return;
        outsideRoot.gameObject.SetActive(visible);
    }

    void ApplyPassthrough(float opacity)
    {
        if (passthroughLayer != null)
            passthroughLayer.textureOpacity = opacity;
    }

    void ApplyProgressBar(float value)
    {
        foreach (var mat in progressBarMaterials)
        {
            if (mat != null)
                mat.SetFloat(_ProgressBar, value);
        }

        // Show mesh only when progress bar is fully complete (= 1)
        var r = GetProgressBarMeshRenderer();
        if (r != null) r.enabled = Mathf.Approximately(value, 1f);
    }

    void ApplyDoorWindowProgressBar(float value)
    {
        if (doorWindowMaterial != null)
            doorWindowMaterial.SetFloat(_ProgressBar, value);
    }

    void SetDoorWindowDepthMask(bool isDepthMask)
    {
        if (doorWindowMaterial == null) return;

        // Shader Graph exposes Boolean as a keyword: "[NAME]_ON"
        string keyword = depthMaskPropertyName.TrimStart('_').ToUpper() + "_ON";

        if (isDepthMask)
            doorWindowMaterial.EnableKeyword(keyword);
        else
            doorWindowMaterial.DisableKeyword(keyword);

        doorWindowMaterial.SetFloat(depthMaskPropertyName, isDepthMask ? 1f : 0f);
    }

    // ===================== Public API =====================

    public void ForceVR() => SwitchToVR();
    public void ForceMR() => SwitchToMR();
}