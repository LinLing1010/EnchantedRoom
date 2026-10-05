using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

/// <summary>
/// Detects the "cup hands" gesture with hysteresis:
///
/// ENTER conditions (strict — all must be true):
///   - Both hands open (fingers extended)
///   - Palms facing each other (dot <= facingDotThreshold)
///   - Distance between palms in [minDistance, maxDistance]
///   - Held for confirmFrames consecutive ticks
///
/// EXIT conditions (loose — any one triggers exit):
///   - Distance exceeds exitMaxDistance (much larger than maxDistance)
///   - Palms completely stop facing each other (dot > exitFacingDotThreshold, close to 0)
///   - Hand tracking lost
///
/// Open-palm check is only used for ENTER, not EXIT.
/// This prevents the flower from flickering once it appears.
/// </summary>
public class CupHandsDetector : GestureDetectorBase
{
    [Header("OVR Hand References")]
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private OVRHand rightHand;
    [SerializeField] private HandVisual leftHandVisual;
    [SerializeField] private HandVisual rightHandVisual;

    [Header("Enter Thresholds (strict)")]
    [Tooltip("Minimum distance between palms to enter")]
    [SerializeField] private float minDistance = 0.1f;
    [Tooltip("Maximum distance between palms to enter")]
    [SerializeField] private float maxDistance = 0.2f;
    [Tooltip("Dot product threshold to enter (closer to -1 = stricter)")]
    [SerializeField] private float facingDotThreshold = -0.3f;
    [Tooltip("Finger curl must be BELOW this to count as open (enter only)")]
    [SerializeField] private float curlThreshold = 0.45f;
    [Tooltip("Consecutive ticks required to confirm enter")]
    [SerializeField] private int confirmFrames = 3;

    [Header("Exit Thresholds (loose)")]
    [Tooltip("Distance must EXCEED this to exit (should be larger than maxDistance)")]
    [SerializeField] private float exitMaxDistance = 0.4f;
    [Tooltip("Dot must be ABOVE this to exit (close to 0 = palms no longer facing at all)")]
    [SerializeField] private float exitFacingDotThreshold = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private bool _isCupHands = false;
    private int _confirmCounter = 0;

    /// <summary>
    /// Current midpoint between both palms (world space).
    /// </summary>
    public Vector3 PalmCenterWorld { get; private set; }

    /// <summary>
    /// Whether the cup-hands gesture is currently active.
    /// </summary>
    public bool IsCupHands => _isCupHands;

// 在 CupHandsDetector 里
    void OnEnable()
    {
        GestureEvents.OnFlowerPlaced += OnFlowerPlaced;
    }

    void OnDisable()
    {
        GestureEvents.OnFlowerPlaced -= OnFlowerPlaced;
    }

    private void OnFlowerPlaced(Vector3 pos)
    {
  
        _isCupHands = false;
        _confirmCounter = 0;
        GestureStateManager.Instance.Exit(AppGestureState.ObjectPlaced);
        Release();
    }
    
    protected override void OnDetectionTick()
    {
        if (leftHand == null || rightHand == null) return;
        if (!leftHand.IsTracked || !rightHand.IsTracked)
        {
            SetCupHands(false);
            return;
        }

        if (leftHandVisual == null || rightHandVisual == null) return;

        var leftJoints  = leftHandVisual.Joints;
        var rightJoints = rightHandVisual.Joints;
        if (leftJoints == null || leftJoints.Count == 0) return;
        if (rightJoints == null || rightJoints.Count == 0) return;

        // ── Get wrist, index1, middle1 from each hand ──
        Transform leftWrist   = GetJoint(leftJoints,  HandJointId.HandWristRoot);
        Transform leftIndex1  = GetJoint(leftJoints,  HandJointId.HandIndex1);
        Transform leftMiddle1 = GetJoint(leftJoints,  HandJointId.HandMiddle1);

        Transform rightWrist   = GetJoint(rightJoints, HandJointId.HandWristRoot);
        Transform rightIndex1  = GetJoint(rightJoints, HandJointId.HandIndex1);
        Transform rightMiddle1 = GetJoint(rightJoints, HandJointId.HandMiddle1);

        if (leftWrist == null || leftIndex1 == null || leftMiddle1 == null) return;
        if (rightWrist == null || rightIndex1 == null || rightMiddle1 == null) return;
        
        // ── Compute shared values ──
        Vector3 leftPalmPos  = leftMiddle1.position; // middle finger root = palm center
        Vector3 rightPalmPos = rightMiddle1.position; // middle finger root = palm center
        float dist = Vector3.Distance(leftPalmPos, rightPalmPos);

        Vector3 leftNormal  = ComputePalmNormal(leftWrist, leftIndex1, leftMiddle1, true);
        Vector3 rightNormal = ComputePalmNormal(rightWrist, rightIndex1, rightMiddle1, false);
        float facingDot = Vector3.Dot(leftNormal, rightNormal);

        if (debugMode && Time.frameCount % 30 == 0)
            Debug.Log($"[CupHands] dist={dist:F3} dot={facingDot:F2} active={_isCupHands}");

        // ════════════════════════════════════════════════
        //  Already active -> use LOOSE exit conditions
        // ════════════════════════════════════════════════
        if (_isCupHands)
        {
            bool tooFar      = dist > exitMaxDistance;
            bool notFacing   = facingDot > exitFacingDotThreshold;

            if (tooFar || notFacing)
            {
                if (debugMode) Debug.Log($"[CupHands] EXIT tooFar={tooFar} notFacing={notFacing}");
                SetCupHands(false);
            }
            else
            {
                // Still active, keep updating center
                PalmCenterWorld = (leftPalmPos + rightPalmPos) * 0.5f;
            }
            return;
        }

        // ════════════════════════════════════════════════
        //  Not active -> use STRICT enter conditions
        // ════════════════════════════════════════════════

        // Open palm check (enter only)
        bool leftOpen  = IsHandOpen(leftJoints, leftWrist, "L");
        bool rightOpen = IsHandOpen(rightJoints, rightWrist, "R");
        if (!leftOpen || !rightOpen)
        {
            if (debugMode) Debug.Log($"[CupHands] FAIL open: L={leftOpen} R={rightOpen}");
            _confirmCounter = 0;
            return;
        }

        // Distance check (strict range)
        bool distOk = dist >= minDistance && dist <= maxDistance;

        // Facing check (strict)
        bool facingOk = facingDot <= facingDotThreshold;

        bool gestureDetected = distOk && facingOk;

        if (gestureDetected)
        {
            _confirmCounter++;
            if (_confirmCounter >= confirmFrames)
            {
                PalmCenterWorld = (leftPalmPos + rightPalmPos) * 0.5f;
                SetCupHands(true);
            }
        }
        else
        {
            _confirmCounter = 0;
        }
    }

    private void SetCupHands(bool value)
    {
        if (value == _isCupHands) return;
    
        if (value)
        {
            if (!TryAcquire())
            {
                _confirmCounter = 0;              // ← 拿不到锁，重置计数，下次重新确认
                return;
            }
            _isCupHands = true;
            Debug.Log("[CupHands] Gesture started");
            GestureEvents.Fire_CupHandsStart(PalmCenterWorld);
        }
        else
        {
            _isCupHands = false;
            Release();
            Debug.Log("[CupHands] Gesture ended");
            GestureEvents.Fire_CupHandsEnd();
            _confirmCounter = 0;
        }
    }

    // ────────── Helpers ──────────

    /// <summary>
    /// Get a joint Transform by HandJointId from HandVisual.Joints (same as GunPoseDetector).
    /// </summary>
    private Transform GetJoint(System.Collections.Generic.IList<Transform> joints, HandJointId id)
    {
        int index = (int)id;
        if (index < 0 || index >= joints.Count) return null;
        return joints[index];
    }

    /// <summary>
    /// Check if all four fingers (index, middle, ring, pinky) are extended (curl below threshold).
    /// Only used for ENTER detection, not EXIT.
    /// </summary>
    private bool IsHandOpen(System.Collections.Generic.IList<Transform> joints, Transform wrist, string handLabel = "")
    {
        float indexCurl  = CalcCurlFromJoint(joints, HandJointId.HandIndex3,  HandJointId.HandIndex1,  wrist);
        float middleCurl = CalcCurlFromJoint(joints, HandJointId.HandMiddle3, HandJointId.HandMiddle1, wrist);
        float ringCurl   = CalcCurlFromJoint(joints, HandJointId.HandRing3,   HandJointId.HandRing1,   wrist);
        float pinkyCurl  = CalcCurlFromJoint(joints, HandJointId.HandPinky3,  HandJointId.HandPinky1,  wrist);

        bool open = indexCurl < curlThreshold
                 && middleCurl < curlThreshold
                 && ringCurl < curlThreshold
                 && pinkyCurl < curlThreshold;

        if (debugMode && Time.frameCount % 30 == 0)
            Debug.Log($"[CupHands] {handLabel} curl: idx={indexCurl:F2} mid={middleCurl:F2} ring={ringCurl:F2} pinky={pinkyCurl:F2} open={open}");

        return open;
    }

    private float CalcCurlFromJoint(System.Collections.Generic.IList<Transform> joints, HandJointId tipId, HandJointId proxId, Transform wrist)
    {
        Transform tip  = GetJoint(joints, tipId);
        Transform prox = GetJoint(joints, proxId);
        if (tip == null || prox == null) return 1f; // assume curled if missing
        return CalcCurl(tip.position, prox.position, wrist.position);
    }

    /// <summary>
    /// Calculate finger curl (0 = fully extended, 1 = fully curled).
    /// Same formula as GunPoseDetector.
    /// </summary>
    private float CalcCurl(Vector3 tip, Vector3 proximal, Vector3 wristPos)
    {
        float extended = Vector3.Distance(proximal, wristPos)
                       + Vector3.Distance(tip, proximal);
        float actual   = Vector3.Distance(tip, wristPos);
        return 1f - (actual / (extended + 0.001f));
    }

    /// <summary>
    /// Compute palm normal from wrist, index root, and middle root transforms.
    /// Cross product of (wrist->middle) x (wrist->index) gives the palm normal.
    /// isLeft flips the cross product direction.
    /// </summary>
    private Vector3 ComputePalmNormal(Transform wrist, Transform index1, Transform middle1, bool isLeft)
    {
        Vector3 toMiddle = (middle1.position - wrist.position).normalized;
        Vector3 toIndex  = (index1.position  - wrist.position).normalized;

        return isLeft
            ? Vector3.Cross(toMiddle, toIndex).normalized
            : Vector3.Cross(toIndex, toMiddle).normalized;
    }
}