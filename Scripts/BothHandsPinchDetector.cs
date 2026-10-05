using UnityEngine;

public class BothHandsPinchDetector : GestureDetectorBase
{
    [Header("Hands")]
    public OVRHand leftHand;
    public OVRHand rightHand;

    [Header("Settings")]
    [Tooltip("双手同时 Pinch 需要持续多久才触发")]
    public float holdRequired = 0.4f;

    bool _wasPinching;
    float _pinchHeld;

    protected override void OnDetectionTick()
    {
        bool bothTracked = leftHand  != null && leftHand.IsTracked
                                             && rightHand != null && rightHand.IsTracked;

        bool bothPinching = bothTracked
                            && leftHand.GetFingerIsPinching(OVRHand.HandFinger.Middle)
                            && rightHand.GetFingerIsPinching(OVRHand.HandFinger.Middle);

        if (bothPinching)
        {
            _pinchHeld += detectionInterval;

            if (!_wasPinching && _pinchHeld >= holdRequired)
            {
                if (!TryAcquire()) return;
                _wasPinching = true;
                GestureEvents.Fire_BothHandsPinchStart();
            }
        }
        else
        {
            if (_wasPinching)
            {
                _wasPinching = false;
                Release();
                GestureEvents.Fire_BothHandsPinchEnd();
            }
            _pinchHeld = 0f;
        }
    }
}