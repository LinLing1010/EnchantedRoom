using UnityEngine;

public class MiniRoomTrigger : MonoBehaviour
{
    Transform _miniRoomRoot;

    public void Init(Transform miniRoomRoot)
    {
        _miniRoomRoot = miniRoomRoot;
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log($"[MiniRoomTrigger] Exit: {other.name} tag={other.tag}");
        if (!other.CompareTag("HandBall")) return;

        Vector3 localPos = _miniRoomRoot.InverseTransformPoint(other.transform.position);
        Debug.Log($"[MiniRoomTrigger] Fired localPos={localPos}");
        GestureEvents.Fire_BallEnteredMiniRoom(localPos);
    }
}