using System.Collections;
using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(Rigidbody))]
public class AutoFixableObject_WithDelayRobust : MonoBehaviour
{
    [Header("Zone")]
    public string zoneTag = "ZoneFixation";
    public float fixationDelay = 0.8f;
    public float graceAfterExit = 0.25f;

    [Header("Recherche de secours")]
    public float probeRadius = 0.15f;
    public LayerMask probeMask = ~0;

    private Rigidbody rb;
    private GrabInteractable grab;
    private FixedJoint joint;

    private bool isGrabbed;
    private bool isInZone;
    private float lastSeenInZoneTime = -999f;
    private Rigidbody lastDrawerBody;

    private Coroutine fixationRoutine;

    // NEW ─────────────
    private static bool isQuitting = false;
    private bool isShuttingDown = false;
    // ────────────────

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<GrabInteractable>();
    }

    void OnEnable()
    {
        if (grab != null) grab.WhenStateChanged += OnGrabStateChanged;
        isShuttingDown = false; // NEW
    }

    void OnDisable()
    {
        // NEW: marquer un shutdown local et couper toute coroutine AVANT l’unsubscribe
        isShuttingDown = true;
        StopFixationRoutine();

        if (grab != null) grab.WhenStateChanged -= OnGrabStateChanged;
    }

    void OnApplicationQuit() // NEW
    {
        isQuitting = true;
        StopFixationRoutine();
    }

    void OnTriggerEnter(Collider other)
    {
        if (isQuitting || isShuttingDown) return; // NEW
        if (!other.CompareTag(zoneTag)) return;

        isInZone = true;
        lastSeenInZoneTime = Time.time;

        var body = other.GetComponentInParent<Rigidbody>();
        if (body != null) lastDrawerBody = body;

        if (!isGrabbed) StartFixationAfterDelay();
    }

    void OnTriggerStay(Collider other)
    {
        if (isQuitting || isShuttingDown) return; // NEW
        if (!other.CompareTag(zoneTag)) return;

        isInZone = true;
        lastSeenInZoneTime = Time.time;

        var body = other.GetComponentInParent<Rigidbody>();
        if (body != null) lastDrawerBody = body;
    }

    void OnTriggerExit(Collider other)
    {
        if (isQuitting || isShuttingDown) return; // NEW
        if (!other.CompareTag(zoneTag)) return;

        isInZone = false; // on laisse la coroutine gérer graceAfterExit
    }

    private void OnGrabStateChanged(InteractableStateChangeArgs args)
    {
        // NEW: ignorer tout pendant la sortie du Play Mode / désactivation
        if (isQuitting || isShuttingDown || !this || !gameObject.activeInHierarchy) return;

        isGrabbed = args.NewState == InteractableState.Select;

        if (isGrabbed)
        {
            StopFixationRoutine();
            if (joint != null) Unfix();
            rb.useGravity = true;
        }
        else
        {
            if (isInZone || (Time.time - lastSeenInZoneTime) <= graceAfterExit)
                StartFixationAfterDelay();
        }
    }

    private void StartFixationAfterDelay()
    {
        // NEW: triple garde
        if (isQuitting || isShuttingDown) return;
        if (!this || !gameObject.activeInHierarchy) return;

        StopFixationRoutine();
        fixationRoutine = StartCoroutine(FixationDelayCoroutine());
    }

    private void StopFixationRoutine()
    {
        if (fixationRoutine != null)
        {
            StopCoroutine(fixationRoutine);
            fixationRoutine = null;
        }
    }

    private IEnumerator FixationDelayCoroutine()
    {
        float start = Time.time;

        while (Time.time - start < fixationDelay)
        {
            // NEW: sortir proprement si on éteint tout
            if (isQuitting || isShuttingDown || !this || !gameObject.activeInHierarchy)
            {
                fixationRoutine = null;
                yield break;
            }

            if (isGrabbed)
            {
                fixationRoutine = null;
                yield break;
            }

            if (!isInZone && (Time.time - lastSeenInZoneTime) > graceAfterExit)
            {
                fixationRoutine = null;
                yield break;
            }

            yield return null;
        }

        if (joint == null && !isGrabbed && !isQuitting && !isShuttingDown && this && gameObject.activeInHierarchy)
        {
            Rigidbody drawerBody = lastDrawerBody;

            if (drawerBody == null)
            {
                var hits = Physics.OverlapSphere(transform.position, probeRadius, probeMask, QueryTriggerInteraction.Collide);
                foreach (var h in hits)
                {
                    if (h != null && h.CompareTag(zoneTag))
                    {
                        var b = h.GetComponentInParent<Rigidbody>();
                        if (b != null) { drawerBody = b; break; }
                    }
                }
            }

            if (drawerBody != null)
            {
                joint = gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = drawerBody;
                joint.enableCollision = false;

                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        fixationRoutine = null;
    }

    private void Unfix()
    {
        if (joint != null)
        {
            Destroy(joint);
            joint = null;
        }
        rb.useGravity = true;
    }
}
