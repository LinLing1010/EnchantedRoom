using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cup-hands flower controller:
/// 1. Flower already exists in scene, SetActive(false) at start
/// 2. Cup-hands gesture -> SetActive(true), follow palm center
/// 3. Hands release + flower touching surface -> place on surface, update shader params
/// 4. Hands release + flower NOT touching surface -> SetActive(false), shader _Flower? = false
/// 5. Once placed (_isPlaced=true): cup-hands near flower (<pickupRadius) -> re-pick up, _isPlaced=false
/// 6. While held or placed: shader _Flower?=true, _FlowerLocation=position
/// 7. PalmUp detector disables itself by listening to CupHands events directly
///
/// Inspector setup:
///   - flowerObject: the flower GameObject already in scene
///   - cupHandsDetector: reference to CupHandsDetector
///   - surfaceLayerMask: Layer for floor / table surfaces
///   - flowerMaterials: materials that receive _Flower? and _FlowerLocation shader params
/// </summary>
public class CupHandsFlowerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Flower GameObject already in scene (not a prefab)")]
    [SerializeField] private GameObject flowerObject;
    [SerializeField] private CupHandsDetector cupHandsDetector;

    [Header("Materials - Shader Params")]
    [Tooltip("Materials that receive _Flower? (Boolean) and _FlowerLocation (Vector3) on placement")]
    public List<Material> flowerMaterials = new List<Material>();

    [Header("Placement Settings")]
    [Tooltip("Raycast distance to detect floor/table surface at gesture end")]
    [SerializeField] private float surfaceCheckDistance = 0.08f;
    [Tooltip("Layer mask for floor/table surfaces")]
    [SerializeField] private LayerMask surfaceLayerMask;
    [Tooltip("Smoothing speed for flower following hands")]
    [SerializeField] private float followSmooth = 15f;
    [Tooltip("Max distance from palm center to placed flower to re-pick it up")]
    [SerializeField] private float pickupRadius = 0.3f;

    // ── Shader Properties ──
    private const string FlowerPropertyName = "_Flower";
    private static readonly string FlowerKeyword = "FLOWER_ON";
    static readonly int _FlowerLocation = Shader.PropertyToID("_FlowerLocation");

    // ── State ──
    private bool _isHolding = false;
    private bool _isPlaced  = false;

    void Awake()
    {
        if (flowerObject != null)
            flowerObject.SetActive(false);

        // Ensure shader starts with _Flower? = false
        ApplyFlowerToMaterials(false, Vector3.zero);
    }

    void OnEnable()
    {
        GestureEvents.OnCupHandsStart += HandleCupHandsStart;
        GestureEvents.OnCupHandsEnd   += HandleCupHandsEnd;
    }

    void OnDisable()
    {
        GestureEvents.OnCupHandsStart -= HandleCupHandsStart;
        GestureEvents.OnCupHandsEnd   -= HandleCupHandsEnd;
    }

    void Update()
    {
        if (!_isHolding || flowerObject == null) return;

        // ── Flower follows midpoint of both hands ──
        Vector3 target = cupHandsDetector.PalmCenterWorld;
        flowerObject.transform.position = Vector3.Lerp(
            flowerObject.transform.position,
            target,
            Time.deltaTime * followSmooth
        );
    }

    // ───────────────────────── Event Callbacks ─────────────────────────

    private void HandleCupHandsStart(Vector3 palmCenter)
    {
        if (_isHolding) return;

        if (_isPlaced)
        {
            // Flower is on surface — only re-pick if hands are close enough to it
            float dist = Vector3.Distance(palmCenter, flowerObject.transform.position);
            if (dist > pickupRadius)
            {
                Debug.Log($"[Flower] Placed but hands too far to pick up: dist={dist:F3}");
                return;
            }

            // Re-pick up
            SetIsPlaced(false);
            GestureStateManager.Instance.TryEnter(AppGestureState.ShowingOnPalm);
            _isHolding = true;
            Debug.Log("[Flower] Re-picked up from surface");
            return;
        }

        // Normal spawn
        if (!GestureStateManager.Instance.TryEnter(AppGestureState.ShowingOnPalm))
            return;

        flowerObject.transform.position = palmCenter;
        flowerObject.SetActive(true);
        ApplyFlowerToMaterials(true, palmCenter);
        _isHolding = true;

        Debug.Log("[Flower] Visible, now following hands");
    }

    private void HandleCupHandsEnd()
    {
        if (!_isHolding) return;

        if (CheckSurfaceContact())
            PlaceFlower();
        else
            HideFlower();
    }

    // ───────────────────────── Core Logic ─────────────────────────

    /// <summary>
    /// Place the flower on the surface.
    /// </summary>
    private void PlaceFlower()
    {
        _isHolding = false;
        SetIsPlaced(true);

        // Snap to surface
        int mask = surfaceLayerMask == 0 ? Physics.DefaultRaycastLayers : (int)surfaceLayerMask;
        if (Physics.Raycast(
            flowerObject.transform.position + Vector3.up * 0.05f,
            Vector3.down,
            out RaycastHit hit,
            surfaceCheckDistance + 0.1f,
            mask))
        {
            flowerObject.transform.position = hit.point;
            flowerObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        }

        // Freeze Rigidbody if present
        var rb = flowerObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        // Shader: placed position
        ApplyFlowerToMaterials(true, flowerObject.transform.position);

        GestureStateManager.Instance.TryEnter(AppGestureState.ObjectPlaced);
        GestureEvents.Fire_FlowerPlaced(flowerObject.transform.position);

        Debug.Log($"[Flower] Placed at {flowerObject.transform.position}");
    }

    /// <summary>
    /// Hide the flower (hands released, not near any surface).
    /// </summary>
    private void HideFlower()
    {
        _isHolding = false;
        flowerObject.SetActive(false);
        ApplyFlowerToMaterials(false, Vector3.zero);

        GestureStateManager.Instance.Exit(AppGestureState.ShowingOnPalm);

        Debug.Log("[Flower] Hidden (not touching surface)");
    }

    // ───────────────────────── Helpers ─────────────────────────

    private void SetIsPlaced(bool value)
    {
        _isPlaced = value;
        Debug.Log($"[Flower] _isPlaced = {value}");
    }

    private bool CheckSurfaceContact()
    {
        if (flowerObject == null) return false;

        // Check at palm center (more reliable than flower position which has follow delay)
        Vector3 checkPos = cupHandsDetector.PalmCenterWorld;
        int mask = surfaceLayerMask == 0 ? Physics.DefaultRaycastLayers : (int)surfaceLayerMask;
        Collider[] hits = Physics.OverlapSphere(checkPos, surfaceCheckDistance, mask);

        if (hits.Length > 0) Debug.Log($"[Flower] Surface detected: {hits[0].name}");
        else                 Debug.Log($"[Flower] No surface within {surfaceCheckDistance}m of palm center");

        return hits.Length > 0;
    }

    private void ApplyFlowerToMaterials(bool placed, Vector3 position)
    {
        foreach (var mat in flowerMaterials)
        {
            if (mat == null) continue;

            if (placed)
                mat.EnableKeyword(FlowerKeyword);
            else
                mat.DisableKeyword(FlowerKeyword);
            mat.SetFloat(FlowerPropertyName, placed ? 1f : 0f);
            mat.SetVector(_FlowerLocation, new Vector4(position.x, position.y, position.z, 0f));
        }
    }

    // ───────────────────────── Debug ─────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (flowerObject == null || !flowerObject.activeInHierarchy) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(
            flowerObject.transform.position,
            Vector3.down * surfaceCheckDistance
        );

        // Draw pickup radius
        if (_isPlaced)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(flowerObject.transform.position, pickupRadius);
        }
    }
#endif
}