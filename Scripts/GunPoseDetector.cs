using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using System.Collections;

public class GunPoseDetector : GestureDetectorBase
{
    [Header("Hand Tracking")]
    [SerializeField] private OVRHand    leftHand;
    [SerializeField] private OVRHand    rightHand;
    [SerializeField] private HandVisual leftHandVisual;
    [SerializeField] private HandVisual rightHandVisual;
    [SerializeField] private Material   lineRendererMaterial;

    [Header("Finger Curl")]
    [SerializeField] private float curlThreshold = 0.25f;

    [Header("Thumb Angle")]
    [SerializeField] private float thumbIndexAngleMin = 30f;
    [SerializeField] private float thumbIndexAngleMax = 130f;

    [Header("Raycast")]
    [SerializeField] private float rayLength = 8f;

    [Header("Shoot Detection")]
    [SerializeField] private float shootWristDeltaY = 0.04f;
    [SerializeField] private float shootCooldown    = 0.8f;

    [Header("Performance")]
    [SerializeField] private float doorCheckIntervalNoDoor   = 0.1f;
    [SerializeField] private float doorCheckIntervalWithDoor = 0.5f;
    [SerializeField] private float doorInitDelay             = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private bool             _enabled = true;
    private DoorController   _aimedDoor;
    private OVRHand          _activeHand;
    private HandVisual        _activeVisual;
    private Vector3          _prevWristPos;
    private bool             _prevWristValid;
    private float            _cooldownTimer;
    private LineRenderer     _lineRenderer;
    private DoorController[] _allDoors;
    private Camera           _mainCamera;
    private float            _doorCheckTimer = 0f;
    private bool             _doorInView     = false;

    void Awake()
    {
        _lineRenderer               = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.startWidth    = 0.01f;
        _lineRenderer.endWidth      = 0.01f;
        _lineRenderer.positionCount = 2;
        _lineRenderer.material      = lineRendererMaterial;
        _lineRenderer.enabled       = false;
    }

    void Start()
    {
        _mainCamera = Camera.main;
        StartCoroutine(InitDoorsDelayed());
    }

    void OnEnable()
    {
        GestureEvents.OnGunShot              += OnShotFired;
        GestureEvents.OnDoorSequenceComplete += OnDoorSequenceComplete;
    }

    void OnDisable()
    {
        GestureEvents.OnGunShot              -= OnShotFired;
        GestureEvents.OnDoorSequenceComplete -= OnDoorSequenceComplete;
    }

    private IEnumerator InitDoorsDelayed()
    {
        yield return new WaitForSeconds(doorInitDelay);
        _allDoors = FindObjectsByType<DoorController>(FindObjectsInactive.Exclude);
        if (debugMode) Debug.Log($"[Gun] Found {_allDoors.Length} doors after delay");
    }

    public void RefreshDoorCache()
    {
        _allDoors = FindObjectsByType<DoorController>(FindObjectsInactive.Exclude);
        if (debugMode) Debug.Log($"[Gun] RefreshDoorCache: {_allDoors.Length} doors");
    }

    protected override void OnDetectionTick()
    {
        _cooldownTimer  -= detectionInterval;
        _doorCheckTimer -= detectionInterval;

        if (!_enabled)
        {
            if (_aimedDoor != null) ClearAim();
            _activeHand           = null;
            _activeVisual         = null;
            _prevWristValid       = false;
            _lineRenderer.enabled = false;
            Release();
            return;
        }

        // 已有激活手势，跳过门检查直接处理
        if (_activeHand != null)
        {
            if (!TryProcessHand(_activeHand, _activeVisual))
            {
                _activeHand           = null;
                _activeVisual         = null;
                _prevWristValid       = false;
                _lineRenderer.enabled = false;
                if (_aimedDoor != null) ClearAim();
                Release();
            }
            return;
        }

        // 被其他手势锁住，直接跳过
        if (IsBlocked) return;

        // 按频率刷新门的可见性
        if (_doorCheckTimer <= 0f)
        {
            _doorInView     = HasDoorInView();
            _doorCheckTimer = _doorInView ? doorCheckIntervalWithDoor : doorCheckIntervalNoDoor;
        }

        if (!_doorInView)
        {
            _prevWristValid = false;
            return;
        }

        if (rightHand != null && TryProcessHand(rightHand, rightHandVisual))
        {
            if (!TryAcquire()) return;
            _activeHand   = rightHand;
            _activeVisual = rightHandVisual;
        }
        else if (leftHand != null && TryProcessHand(leftHand, leftHandVisual))
        {
            if (!TryAcquire()) return;
            _activeHand   = leftHand;
            _activeVisual = leftHandVisual;
        }
        else
        {
            _prevWristValid = false;
            if (_aimedDoor != null) ClearAim();
        }
    }

    private bool HasDoorInView()
    {
        if (_mainCamera == null || _allDoors == null) return false;
        foreach (var door in _allDoors)
        {
            if (door == null) continue;
            Collider col = door.GetComponentInChildren<Collider>();
            Vector3 checkPos = col != null ? col.bounds.center : door.transform.position;
            Vector3 vp = _mainCamera.WorldToViewportPoint(checkPos);
            if (vp.x > -0.3f && vp.x < 1.3f && vp.y > -0.3f && vp.y < 1.3f && vp.z > 0f)
                return true;
        }
        return false;
    }

    private bool TryProcessHand(OVRHand hand, HandVisual visual)
    {
        if (hand == null || !hand.IsTracked) return false;
        if (visual == null) return false;

        var joints = visual.Joints;
        if (joints == null || joints.Count == 0) return false;

        Transform indexTip      = GetJoint(joints, HandJointId.HandIndex3);
        Transform indexProximal = GetJoint(joints, HandJointId.HandIndex1);
        Transform wrist         = GetJoint(joints, HandJointId.HandWristRoot);

        if (indexTip == null || indexProximal == null || wrist == null) return false;

        Vector3 indexDir  = (indexTip.position - indexProximal.position).normalized;
        float   indexCurl = CalcCurl(indexTip.position, indexProximal.position, wrist.position);

        // 已经在姿势中，只判断食指是否还伸直
        if (_activeHand == hand)
        {
            if (indexCurl > 0.15f)
            {
                if (debugMode) Debug.Log($"[Gun] EXIT index={indexCurl:F2}");
                return false;
            }

            _lineRenderer.enabled = true;
            _lineRenderer.SetPosition(0, indexTip.position);
            _lineRenderer.SetPosition(1, indexTip.position + indexDir * rayLength);
            UpdateAim(indexTip.position, indexDir);

            if (_prevWristValid && _cooldownTimer <= 0f && _aimedDoor != null)
            {
                float deltaY = wrist.position.y - _prevWristPos.y;
                if (deltaY > shootWristDeltaY)
                {
                    _cooldownTimer = shootCooldown;
                    GestureEvents.Fire_GunShot();
                }
            }

            _prevWristPos   = wrist.position;
            _prevWristValid = true;
            return true;
        }

        // 首次进入，完整检测
        float midCurl   = CalcCurlFromJoint(joints, HandJointId.HandMiddle3, HandJointId.HandMiddle1, wrist);
        float ringCurl  = CalcCurlFromJoint(joints, HandJointId.HandRing3,   HandJointId.HandRing1,   wrist);
        float pinkyCurl = CalcCurlFromJoint(joints, HandJointId.HandPinky3,  HandJointId.HandPinky1,  wrist);

        Transform thumbTip      = GetJoint(joints, HandJointId.HandThumb3);
        Transform thumbProximal = GetJoint(joints, HandJointId.HandThumb1);
        if (thumbTip == null || thumbProximal == null) return false;

        Vector3 thumbDir = (thumbTip.position - thumbProximal.position).normalized;
        float   angle    = Vector3.Angle(indexDir, thumbDir);

        if (debugMode && Time.frameCount % 30 == 0)
            Debug.Log($"[Gun] index={indexCurl:F2} mid={midCurl:F2} ring={ringCurl:F2} pinky={pinkyCurl:F2} angle={angle:F1}");

        if (indexCurl > curlThreshold)
            { if (debugMode) Debug.Log($"[Gun] FAIL index={indexCurl:F2}"); return false; }
        if (midCurl < curlThreshold)
            { if (debugMode) Debug.Log($"[Gun] FAIL middle={midCurl:F2}"); return false; }
        if (ringCurl < curlThreshold)
            { if (debugMode) Debug.Log($"[Gun] FAIL ring={ringCurl:F2}"); return false; }
        if (pinkyCurl < curlThreshold)
            { if (debugMode) Debug.Log($"[Gun] FAIL pinky={pinkyCurl:F2}"); return false; }
        if (angle < thumbIndexAngleMin || angle > thumbIndexAngleMax)
            { if (debugMode) Debug.Log($"[Gun] FAIL angle={angle:F1}"); return false; }

        if (debugMode) Debug.Log("[Gun] POSE OK");

        _lineRenderer.enabled = true;
        _lineRenderer.SetPosition(0, indexTip.position);
        _lineRenderer.SetPosition(1, indexTip.position + indexDir * rayLength);
        UpdateAim(indexTip.position, indexDir);

        _prevWristPos   = wrist.position;
        _prevWristValid = true;
        return true;
    }

    private float CalcCurlFromJoint(System.Collections.Generic.IList<Transform> joints, HandJointId tipID, HandJointId proxID, Transform wrist)
    {
        Transform tip  = GetJoint(joints, tipID);
        Transform prox = GetJoint(joints, proxID);
        if (tip == null || prox == null) return 0f;
        return CalcCurl(tip.position, prox.position, wrist.position);
    }

    private float CalcCurl(Vector3 tip, Vector3 proximal, Vector3 wristPos)
    {
        float extended = Vector3.Distance(proximal, wristPos)
                       + Vector3.Distance(tip, proximal);
        float actual   = Vector3.Distance(tip, wristPos);
        return 1f - (actual / (extended + 0.001f));
    }

    private void UpdateAim(Vector3 indexTipPos, Vector3 indexDir)
    {
        if (Physics.Raycast(indexTipPos, indexDir, out RaycastHit hit, rayLength,
                Physics.AllLayers, QueryTriggerInteraction.Collide))
        {
            var door = hit.collider.GetComponentInParent<DoorController>();
            if (door != null)
            {
                if (door != _aimedDoor)
                {
                    if (_aimedDoor != null) GestureEvents.Fire_GunAimLost();
                    _aimedDoor = door;
                }
                GestureEvents.Fire_GunAimingDoor(door);
                return;
            }
        }

        if (_aimedDoor != null) ClearAim();
    }

    private void ClearAim()
    {
        _aimedDoor = null;
        GestureEvents.Fire_GunAimLost();
    }

    private Transform GetJoint(System.Collections.Generic.IList<Transform> joints, HandJointId id)
    {
        int index = (int)id;
        if (index < 0 || index >= joints.Count) return null;
        return joints[index];
    }

    private void OnShotFired()
    {
        _enabled              = false;
        _prevWristValid       = false;
        _activeHand           = null;
        _activeVisual         = null;
        _lineRenderer.enabled = false;
    }

    private void OnDoorSequenceComplete()
    {
        _enabled   = true;
        _aimedDoor = null;
        if (debugMode) Debug.Log("[Gun] DoorSequenceComplete, re-enabled");
    }
}