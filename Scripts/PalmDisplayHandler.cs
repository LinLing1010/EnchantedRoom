using UnityEngine;
using Oculus.Interaction.Input;

public class PalmDisplayHandler : MonoBehaviour
{
    [SerializeField] private GameObject displayTarget;
    [SerializeField] private OVRHand leftHand;


    private bool _isShowing = false;


    void OnEnable()
    {
        GestureEvents.OnLeftPalmUpStart += HandlePalmUpStart;
        GestureEvents.OnLeftPalmUpEnd   += HandlePalmUpEnd;
    }

    void OnDisable()
    {
        GestureEvents.OnLeftPalmUpStart -= HandlePalmUpStart;
        GestureEvents.OnLeftPalmUpEnd   -= HandlePalmUpEnd;
        
    }
    

    void HandlePalmUpStart()
    {
        // 已放置状态下，手掌朝上不重新显示在手掌
        if (GestureStateManager.Instance.Is(AppGestureState.ObjectPlaced)) return;
        if (displayTarget == null) return;
        
        if (GestureStateManager.Instance.TryEnter(AppGestureState.ShowingOnPalm))
        {
            _isShowing = true;
            displayTarget.SetActive(true);
        }
        
    }

    void HandlePalmUpEnd()
    {
        if (GestureStateManager.Instance.Is(AppGestureState.ObjectPlaced)) return;
        if (displayTarget == null) return;
        GestureStateManager.Instance.Exit(AppGestureState.ShowingOnPalm);
        _isShowing = false;
        displayTarget?.SetActive(false);  // null 就什么都不做
    }

    void Update()
    {
        if (!_isShowing) return;
        if (displayTarget == null || leftHand == null) return;
        if (GestureStateManager.Instance.Is(AppGestureState.ObjectPlaced)) return;

        Vector3 palmPos = leftHand.PointerPose.position;
        Vector3 palmUp  = leftHand.PointerPose.up;
        Vector3 palmFwd = leftHand.PointerPose.forward;

        displayTarget.transform.position = palmPos + Vector3.up * 0.02f;

        Vector3 flatFwd = Vector3.ProjectOnPlane(palmFwd, Vector3.up).normalized;
        displayTarget.transform.rotation = Quaternion.LookRotation(flatFwd, Vector3.up);
    }
    
    public void SetDisplayTarget(GameObject newTarget)
    {
        displayTarget = newTarget;
        if (displayTarget != null)
            displayTarget.SetActive(false);
    }
}