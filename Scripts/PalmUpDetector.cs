using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

public class PalmUpDetector : GestureDetectorBase
{
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private HandVisual handVisual;
    [SerializeField] private float palmUpThreshold   =  0.7f;
    [SerializeField] private float palmDownThreshold = -0.7f;
    [SerializeField] private float fingerAngleThreshold = 45f;

    private bool _wasDetected  = false;
    private bool _wasPalmDown  = false;
    

    protected override void OnDetectionTick()
    {
        if (leftHand == null || !leftHand.IsTracked) return;

        Vector3 palmNormal = leftHand.PointerPose.up;
        float   dot        = Vector3.Dot(palmNormal, Vector3.up);
        bool    isOpen     = CheckFingersOpen();

        bool isPalmUp   = dot >  palmUpThreshold && isOpen;
        bool isPalmDown = dot <  palmDownThreshold;

        // PalmUp
        if (isPalmUp && !_wasDetected)
        {
            if (TryAcquire())
            {
                _wasDetected = true;                  // ← 只有拿到锁才标记
                GestureEvents.Fire_LeftPalmUpStart();
            }
            // 拿不到锁，_wasDetected 保持 false，不做任何事
        }
        else if (!isPalmUp && _wasDetected)
        {
            if (isOpen)
                GestureEvents.Fire_FreeDrop();
            GestureEvents.Fire_LeftPalmUpEnd();
            Release();
            _wasDetected = false;                     // ← 明确清掉
        }

        // PalmDown
        if (isPalmDown && !_wasPalmDown)
        {
            if (!IsBlocked)
                GestureEvents.Fire_LeftPalmDownStart();
        }
        else if (!isPalmDown && _wasPalmDown)
            GestureEvents.Fire_LeftPalmDownEnd();

        // 注意：这里不再无条件赋值 _wasDetected
        _wasPalmDown = isPalmDown;
    }

    private bool CheckFingersOpen()
    {
        if (handVisual == null) return true;

        return IsFingerOpen(HandJointId.HandIndex1,  HandJointId.HandIndex2)
            && IsFingerOpen(HandJointId.HandMiddle1, HandJointId.HandMiddle2)
            && IsFingerOpen(HandJointId.HandRing1,   HandJointId.HandRing2);
    }

    private bool IsFingerOpen(HandJointId proximal, HandJointId intermediate)
    {
        var joints = handVisual.Joints;
        Transform t1 = joints[(int)proximal];
        Transform t2 = joints[(int)intermediate];
        if (t1 == null || t2 == null) return true;

        float angle = Quaternion.Angle(t1.rotation, t2.rotation);
        return angle < fingerAngleThreshold;
    }
}