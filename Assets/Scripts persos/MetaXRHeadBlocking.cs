using UnityEngine;

// Meta XR Head Protection Script
// Prevents camera clipping from both player movement and dynamic objects moving into the player.
// This script actively pushes the player rig out of colliders.
//
// SETUP:
// 1) Add this script to the CenterEyeAnchor component of the OVRCameraRig.
// 2) This script requires a SphereCollider on the same GameObject. Set its radius and check "Is Trigger".
// 3) Set the "Player" field to your root object (e.g., "OVRCameraRig").
// 4) Set the "Collision Layers" to define what objects should block the head.
// 5) Make sure your player's own colliders are on a different layer or have the "Player" tag to be ignored.

[RequireComponent(typeof(SphereCollider))]
public class MetaXRHeadProtection : MonoBehaviour
{
    [Tooltip("The root of your player rig that will be moved.")]
    [SerializeField] private GameObject player = null;

    [Tooltip("Layers that the head should collide with.")]
    [SerializeField] private LayerMask _collisionLayers = 1 << 0;

    // We'll grab the radius directly from the attached SphereCollider.
    private float _collisionRadius;
    private SphereCollider _headCollider;

    private void Awake()
    {
        _headCollider = GetComponent<SphereCollider>();
        _headCollider.isTrigger = true; // Ensure it's a trigger
        _collisionRadius = _headCollider.radius;
    }

    private void Update()
    {
        if (player == null) return;

        // Detect all colliders overlapping with our head's sphere
        Collider[] overlappingColliders = Physics.OverlapSphere(transform.position, _collisionRadius, _collisionLayers, QueryTriggerInteraction.Ignore);

        foreach (var otherCollider in overlappingColliders)
        {
            // Ignore colliders tagged as "Player" to avoid self-collision
            if (otherCollider.CompareTag("Player"))
            {
                continue;
            }

            // Calculate the penetration vector. This is the magic part!
            // It gives us the direction and distance to push the player to resolve the collision.
            bool isPenetrating = Physics.ComputePenetration(
                _headCollider, transform.position, transform.rotation,
                otherCollider, otherCollider.transform.position, otherCollider.transform.rotation,
                out Vector3 pushDirection, out float pushDistance
            );

            if (isPenetrating)
            {
                // Apply the push to the main player rig, not just the head.
                // We add a small buffer (e.g., 0.01f) to prevent z-fighting or getting stuck.
                player.transform.position += pushDirection * (pushDistance + 0.001f);
            }
        }
    }
}