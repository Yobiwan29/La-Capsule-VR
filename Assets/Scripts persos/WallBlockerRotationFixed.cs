using CapsuleVR.Audio;
using Oculus.Interaction;
using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(35000)]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class WallBlockerRotationFixed : MonoBehaviour
{
    [Header("Cibles (mur/table)")]
    public LayerMask wallLayers = -1;

    [Header("Contact visuel")]
    public float skinWidth = 0.012f;
    public float separateEpsilon = 0.003f;

    [Header("Détection initiale")]
    public float boxInflation = 0.003f;

    [Header("Maintien du pin")]
    [Tooltip("Distance max pour maintenir le pin actif")]
    public float maxPinDistance = 0.05f;
    [Tooltip("Durée max du pin sans contact (secondes)")]
    public float pinTimeout = 0.5f;
    [Tooltip("Angle max de rotation par frame avant re-validation")]
    public float maxRotationPerFrame = 30f;

    [Header("Sécurité Anti-Traversée")]
    [Tooltip("Vérifications supplémentaires par frame")]
    public int safetyChecksPerFrame = 3;
    [Tooltip("Forcer le retour si pénétration détectée")]
    public bool forceSafetyFallback = true;
    [Tooltip("Distance de recul d'urgence")]
    public float emergencyPushback = 0.02f;
    public float maxCheckedMove = 0.7f;

    [Header("Lancer naturel")]
    [Tooltip("Passe en détection de collision continue pendant cette durée après un lancer.")]
    public float continuousDuration = 3f;
    [Range(3, 8)] public int velSamples = 5;
    public float throwVelocityScale = 1.0f;
    public float throwAngularScale = 1.0f;

    [Header("Debug")]
    public bool debugMode = false;
    public bool visualizePinPlane = false;

    [Header("Son")]
    [Tooltip("Intervalle minimum entre les sons de collision générés par le WallBlocker.")]
    public float soundCooldown = 0.2f;

    // Permet à d'autres scripts de verrouiller les interactions
    public bool isInteractionLocked = false;

    // --- AJOUTS POUR LE RELÂCHEMENT EN DOUCEUR ---
    private const string GRABBED_LAYER_NAME = "BeingHeld";
    private const string RELEASE_LAYER_NAME = "ReleaseGracePeriod";
    private int _originalLayer;
    // ---------------------------------------------

    // --- refs
    GrabInteractable grab;
    Rigidbody rb;
    Collider col;
    BoxCollider box;
    private AdvancedImpactAudio advancedImpactAudio;
    private float _lastWallBlockerSoundTime;
    
    // --- état
    bool isGrabbed;
    Vector3 lastPos;
    Quaternion lastRot;

    // pin amélioré
    bool pinned;
    Vector3 pinNormal;
    Vector3 pinPoint;
    float lastPinContactTime;
    Vector3 lastValidPinPos;
    Quaternion lastValidPinRot;

    // Cache du collider touché pour maintenir le pin
    Collider pinnedCollider;
    Vector3 pinnedColliderLastPos;

    // buffers lancer
    Vector3[] posBuf;
    Quaternion[] rotBuf;
    float[] timeBuf;
    int bufIdx;

    void OnEnable()
    {
        // Re-synchroniser les références de pose/buffers quand on (re)active
        lastPos = transform.position;
        lastRot = transform.rotation;
        pinned = false;
        pinnedCollider = null;

        if (posBuf == null || rotBuf == null || timeBuf == null || posBuf.Length != velSamples)
        {
            posBuf = new Vector3[velSamples];
            rotBuf = new Quaternion[velSamples];
            timeBuf = new float[velSamples];
        }
        for (int i = 0; i < velSamples; i++)
        {
            posBuf[i] = transform.position;
            rotBuf[i] = transform.rotation;
            timeBuf[i] = Time.time;
        }
        bufIdx = 0;
    }

    void Awake()
    {
        grab = GetComponent<GrabInteractable>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        box = col as BoxCollider;
        advancedImpactAudio = GetComponent<AdvancedImpactAudio>();

        if (wallLayers.value == 0)
            wallLayers = LayerMask.GetMask("Default");

        lastPos = transform.position;
        lastRot = transform.rotation;

        _originalLayer = gameObject.layer;

        posBuf = new Vector3[velSamples];
        rotBuf = new Quaternion[velSamples];
        timeBuf = new float[velSamples];
        for (int i = 0; i < velSamples; i++)
        {
            posBuf[i] = transform.position;
            rotBuf[i] = transform.rotation;
            timeBuf[i] = Time.time;
        }
    }

    void Update()
    {
        if (isInteractionLocked)
        {
            pinned = false;
            pinnedCollider = null;
            return;
        }

        bool was = isGrabbed;
        isGrabbed = grab != null && grab.SelectingInteractors.Count > 0;

        if (isGrabbed)
        {
            bufIdx = (bufIdx + 1) % velSamples;
            posBuf[bufIdx] = transform.position;
            rotBuf[bufIdx] = transform.rotation;
            timeBuf[bufIdx] = Time.time;
        }

        // --- LOGIQUE DE SAISIE MODIFIÉE ---
        if (!was && isGrabbed)
        {
            lastPos = transform.position;
            lastRot = transform.rotation;
            pinned = false;
            pinnedCollider = null;

            // On passe sur la couche "tenu"
            StopAllCoroutines();
        }
        // --- LOGIQUE DE RELÂCHEMENT MODIFIÉE ---
        else if (was && !isGrabbed)
        {
            // Au lieu d'appeler directement ApplyThrow, on lance la séquence de relâchement
            StartCoroutine(ReleaseSequence());

            pinned = false;
            pinnedCollider = null;
        }
    }

    void LateUpdate()
    {
        // Ne rien faire si l'interaction est verrouillée
        if (isInteractionLocked)
        {
            lastPos = transform.position;
            lastRot = transform.rotation;
            return;
        }

        if (!isGrabbed)
        {
            lastPos = transform.position;
            lastRot = transform.rotation;
            return;
        }

        Vector3 desiredPos = transform.position;
        Quaternion desiredRot = transform.rotation;
        Vector3 move = desiredPos - lastPos;
        float dist = move.magnitude;
        float rotationAngle = Quaternion.Angle(lastRot, desiredRot);

        if (dist <= 0.0005f && rotationAngle < 0.1f)
        {
            return;
        }

        if (dist >= maxCheckedMove)
        {
            Debug.LogWarning($"[WallBlocker] TÉLÉPORTATION DÉTECTÉE ! Distance: {dist}m > maxCheckedMove: {maxCheckedMove}m. Les vérifications de collision sont ignorées pour cette frame !");
            lastPos = desiredPos;
            lastRot = desiredRot;
            pinned = false;
            pinnedCollider = null;
            return;
        }

        // PIN ACTIF - Maintenir même pendant la rotation
        if (pinned)
        {
            if (!ValidatePin(desiredPos))
            {
                pinned = false;
                pinnedCollider = null;
                if (debugMode) Debug.Log("[WallBlocker] Pin lost - too far or timeout");
            }
            else
            {
                Vector3 constrainedPos = ApplyPinConstraints(desiredPos, desiredRot);
                Quaternion constrainedRot = desiredRot;
                if (rotationAngle > maxRotationPerFrame)
                {
                    constrainedRot = ValidateRotationAgainstPin(constrainedPos, desiredRot);
                }
                else
                {
                    EnsureCornersAbovePlane(ref constrainedPos, ref constrainedRot);
                }
                transform.SetPositionAndRotation(constrainedPos, constrainedRot);
                if (forceSafetyFallback)
                {
                    Depenetrate();
                }
                lastPos = transform.position;
                lastRot = transform.rotation;
                lastValidPinPos = transform.position;
                lastValidPinRot = transform.rotation;
                float distToPlane = Vector3.Dot(transform.position - pinPoint, pinNormal);
                if (distToPlane < skinWidth * 2)
                {
                    lastPinContactTime = Time.time;
                }
                return;
            }
        }

        // NON PINNED - Détection normale avec subdivision pour mouvements rapides
        if (dist > 0.0005f)
        {
            Vector3 dir = move.normalized;

            // ==========================================================
            // == DÉBUT DE LA CORRECTION POUR LES GROS OBJETS           ==
            // ==========================================================

            // On recule LÉGÈREMENT le point de départ du test pour s'assurer qu'il est hors de tout obstacle.
            float pullbackDistance = 0.02f; // 2cm, peut être ajusté
            Vector3 startPos = lastPos - dir * pullbackDistance;
            float totalCastDistance = dist + pullbackDistance;

            float stepDist = totalCastDistance / safetyChecksPerFrame;
            Vector3 currentPos = startPos; // On commence depuis la position reculée

            // ==========================================================
            // == FIN DE LA CORRECTION                                  ==
            // ==========================================================

            bool hitDetected = false;

            for (int i = 0; i < safetyChecksPerFrame; i++)
            {
                float castDist = (i == safetyChecksPerFrame - 1) ? (totalCastDistance - i * stepDist) : stepDist;
                if (BoxCastAhead(currentPos, desiredRot, dir, castDist + skinWidth, out RaycastHit hit))
                {
                    // La distance du hit est par rapport à notre point de départ reculé.
                    // On ajuste pour trouver le point de contact sur la surface réelle de l'objet.
                    float safeMove = hit.distance - pullbackDistance - skinWidth;
                    Vector3 atSurface = lastPos + dir * Mathf.Max(0f, safeMove) + hit.normal * separateEpsilon;

                    // On déclenche le son (la logique existante est déplacée ici)
                    if (Time.time - _lastWallBlockerSoundTime > soundCooldown)
                    {
                        if (advancedImpactAudio != null)
                        {
                            // On calcule la vélocité en se basant sur l'historique de mouvement de la main,
                            // qui n'est pas affecté par les corrections de position du WallBlocker.
                            int newest = bufIdx;
                            int oldest = (bufIdx - 1 + velSamples) % velSamples;
                            float dt = timeBuf[newest] - timeBuf[oldest];
                            float impactVelocity = 0f;
                            if (dt > 0.001f)
                            {
                                impactVelocity = Vector3.Distance(posBuf[newest], posBuf[oldest]) / dt;
                            }
                            advancedImpactAudio.NotifyProxyImpact(impactVelocity, hit.collider.gameObject.layer, hit.point);

                            // On réinitialise le timer
                            _lastWallBlockerSoundTime = Time.time;
                        }
                    }

                    // Initialiser le pin
                    pinned = true;
                    pinNormal = hit.normal.normalized;
                    pinPoint = hit.point;
                    pinnedCollider = hit.collider;
                    pinnedColliderLastPos = hit.collider.transform.position;
                    lastPinContactTime = Time.time;
                    lastValidPinPos = atSurface;
                    lastValidPinRot = desiredRot;

                    Quaternion validRot = desiredRot;
                    EnsureCornersAbovePlane(ref atSurface, ref validRot);
                    transform.SetPositionAndRotation(atSurface, validRot);

                    if (forceSafetyFallback)
                    {
                        Depenetrate();
                    }

                    lastPos = transform.position;
                    lastRot = transform.rotation;

                    if (debugMode) Debug.Log($"[WallBlocker] Pin initialized on {hit.collider.name}");
                    hitDetected = true;
                    break;
                }
                else
                {
                    currentPos += dir * castDist;
                }
            }

            if (hitDetected)
            {
                return;
            }
        }

        // Gestion de la rotation seule (sans mouvement positionnel significatif)
        if (dist <= 0.0005f && rotationAngle >= 0.1f)
        {
            if (WouldPenetrate(desiredRot))
            {
                Quaternion constrainedRot = ValidateRotation(desiredPos, desiredRot, lastRot);
                transform.SetPositionAndRotation(desiredPos, constrainedRot);
                if (forceSafetyFallback)
                {
                    Depenetrate();
                }
                lastPos = transform.position;
                lastRot = transform.rotation;
                return;
            }
        }

        // Pas d'obstacle - mouvement libre
        transform.SetPositionAndRotation(desiredPos, desiredRot);

        // Appliquer la sécurité anti-pénétration finale
        if (forceSafetyFallback)
        {
            Depenetrate();
        }

        lastPos = transform.position;
        lastRot = transform.rotation;
    }

    bool ValidatePin(Vector3 currentPos)
    {
        // Timeout check
        if (Time.time - lastPinContactTime > pinTimeout)
            return false;

        // Distance check
        float distToPlane = Mathf.Abs(Vector3.Dot(currentPos - pinPoint, pinNormal));
        if (distToPlane > maxPinDistance)
            return false;

        // Si le collider a bougé (objet dynamique), invalider le pin
        if (pinnedCollider != null && pinnedCollider.transform.position != pinnedColliderLastPos)
            return false;

        return true;
    }

    Vector3 ApplyPinConstraints(Vector3 desiredPos, Quaternion desiredRot)
    {
        Vector3 want = desiredPos - lastPos;

        // Bloquer la composante qui pousse dans le plan
        float into = Vector3.Dot(-pinNormal, want);
        if (into > 0f)
            want -= (-pinNormal) * into;

        // Maintenir la distance minimale
        Vector3 resultPos = lastPos + want;
        float signedDist = Vector3.Dot(resultPos - pinPoint, pinNormal);

        if (signedDist < skinWidth)
        {
            resultPos += pinNormal * (skinWidth - signedDist);
        }

        return resultPos;
    }

    Quaternion ValidateRotationAgainstPin(Vector3 pos, Quaternion desiredRot)
    {
        if (box == null)
            return desiredRot;

        // Si tous les coins sont OK, accepter la rotation
        if (AllCornersAbovePlane(pos, desiredRot, skinWidth))
            return desiredRot;

        // Sinon, interpoler vers une rotation valide
        Quaternion safeRot = lastValidPinRot;

        // Recherche binaire pour trouver la rotation max acceptable
        float low = 0f;
        float high = 1f;
        for (int i = 0; i < 6; i++) // Plus d'itérations pour précision
        {
            float mid = (low + high) / 2f;
            Quaternion testRot = Quaternion.Slerp(safeRot, desiredRot, mid);
            if (AllCornersAbovePlane(pos, testRot, skinWidth))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return Quaternion.Slerp(safeRot, desiredRot, low);
    }

    Quaternion ValidateRotation(Vector3 pos, Quaternion desiredRot, Quaternion safeRot)
    {
        if (box == null)
            return desiredRot;

        // Si pas de pénétration, accepter
        if (!WouldPenetrate(desiredRot))
            return desiredRot;

        // Recherche binaire
        float low = 0f;
        float high = 1f;
        for (int i = 0; i < 6; i++)
        {
            float mid = (low + high) / 2f;
            Quaternion testRot = Quaternion.Slerp(safeRot, desiredRot, mid);
            if (!WouldPenetrate(testRot))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return Quaternion.Slerp(safeRot, desiredRot, low);
    }

    void EnsureCornersAbovePlane(ref Vector3 pos, ref Quaternion rot)
    {
        if (box == null || !pinned)
            return;

        float minDist = GetMinCornerDistance(pos, rot);

        if (minDist < skinWidth)
        {
            // Pousser légèrement pour maintenir la distance
            pos += pinNormal * (skinWidth - minDist + separateEpsilon);
        }
    }

    float GetMinCornerDistance(Vector3 pos, Quaternion rot)
    {
        if (box == null)
            return skinWidth;

        Vector3 half = Vector3.Scale(box.size, Abs(transform.lossyScale)) * 0.5f;
        Vector3 center = pos + rot * Vector3.Scale(box.center, transform.lossyScale);

        float minDist = float.MaxValue;

        // Vérifier les 8 coins
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 offset = new Vector3(sx * half.x, sy * half.y, sz * half.z);
                    Vector3 corner = center + rot * offset;
                    float dist = Vector3.Dot(corner - pinPoint, pinNormal);
                    if (dist < minDist)
                        minDist = dist;
                }
            }
        }

        return minDist;
    }

    bool AllCornersAbovePlane(Vector3 pos, Quaternion rot, float minDistance)
    {
        return GetMinCornerDistance(pos, rot) >= minDistance - 0.0001f;
    }

    bool BoxCastAhead(Vector3 posePos, Quaternion poseRot, Vector3 dir, float distance, out RaycastHit hit)
    {
        // CAS 1 : C'est un BoxCollider (le plus courant), on garde le BoxCast rapide.
        if (box != null)
        {
            Vector3 worldCenter = posePos + poseRot * Vector3.Scale(box.center, transform.lossyScale);
            Vector3 half = Vector3.Scale(box.size, Abs(transform.lossyScale)) * 0.5f + Vector3.one * boxInflation;
            if (Physics.BoxCast(worldCenter, half, dir, out hit, poseRot, distance, wallLayers, QueryTriggerInteraction.Ignore))
                return IsStatic(hit.collider);

            return false;
        }
        // NOUVEAU CAS 2 : C'est un MeshCollider convexe, on utilise le SweepTest précis.
        else if (col is MeshCollider meshCol && meshCol.convex)
        {
            // On déplace temporairement le Rigidbody pour le test, puis on le remet à sa place.
            // C'est nécessaire pour que SweepTest fonctionne correctement quand l'objet est tenu.
            Vector3 originalPos = rb.position;
            Quaternion originalRot = rb.rotation;
            rb.position = posePos;
            rb.rotation = poseRot;

            bool result = rb.SweepTest(dir, out hit, distance, QueryTriggerInteraction.Ignore);

            rb.position = originalPos;
            rb.rotation = originalRot;

            if (result && (wallLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
            {
                return IsStatic(hit.collider);
            }
            return false;
        }
        // CAS 3 : Fallback pour les autres colliders (Sphere, Capsule...), on garde le SphereCast.
        else
        {
            Bounds b = col.bounds;
            float r = Mathf.Min(b.extents.x, Mathf.Min(b.extents.y, b.extents.z)) + boxInflation;
            if (Physics.SphereCast(posePos, r, dir, out hit, distance, wallLayers, QueryTriggerInteraction.Ignore))
                return IsStatic(hit.collider);

            return false;
        }
    }

    bool IsStatic(Collider c)
    {
        if (c == null) return false;
        if (c.CompareTag("ZoneFixation")) return false;
        if (c.gameObject.layer == LayerMask.NameToLayer("GrabbableVR")) return false;
        var rbOther = c.attachedRigidbody;
        return rbOther == null || rbOther.isKinematic;
    }

    private IEnumerator ResetCollisionDetectionMode()
    {
        // On attend la durée spécifiée
        yield return new WaitForSeconds(continuousDuration);

        // Si l'objet n'a pas été attrapé à nouveau pendant ce temps,
        // on remet le mode de détection par défaut, plus performant.
        if (!isGrabbed && rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }

    // --- NOUVELLE FONCTION POUR LE RELÂCHEMENT ---
    private IEnumerator ReleaseSequence()
    {
        // 1. On force l'objet à revenir à sa dernière position valide (corrige la téléportation)
        transform.position = lastPos;

        // 2. On passe sur la couche de grâce (corrige le combat de physique)
        gameObject.layer = LayerMask.NameToLayer(RELEASE_LAYER_NAME);

        // 3. On applique la physique du lancer
        ApplyThrowFromBuffer();

        // 4. On attend un court instant
        yield return new WaitForSeconds(0.2f);

        // 5. On revient à la couche d'origine, une fois que la main est loin
        gameObject.layer = _originalLayer;
    }
    // ------------------------------------------

    void ApplyThrowFromBuffer()
    {
        int oldest = (bufIdx + 1) % velSamples;
        int newest = bufIdx;
        float dt = Mathf.Max(1e-4f, timeBuf[newest] - timeBuf[oldest]);

        Vector3 v = (posBuf[newest] - posBuf[oldest]) / dt;

        Quaternion dq = rotBuf[newest] * Quaternion.Inverse(rotBuf[oldest]);
        dq.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        Vector3 w = (dt > 0f && axis != Vector3.zero) ? axis.normalized * (angleRad / dt) : Vector3.zero;

        if (rb)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            // On active la détection de collision la plus précise
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // On lance la coroutine qui la désactivera plus tard
            StartCoroutine(ResetCollisionDetectionMode());

            rb.linearVelocity = v * throwVelocityScale;
            rb.angularVelocity = w * throwAngularScale;
            rb.WakeUp();
        }
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    void OnDrawGizmosSelected()
    {
        if (!debugMode || !Application.isPlaying) return;

        Gizmos.color = pinned ? Color.cyan : Color.green;
        Gizmos.DrawLine(lastPos, transform.position);
        Gizmos.DrawWireSphere(lastPos, 0.02f);

        if (pinned)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, pinNormal * 0.12f);

            if (visualizePinPlane)
            {
                // Dessiner le plan
                Gizmos.color = new Color(1, 0, 1, 0.3f);
                Vector3 tangent1 = Vector3.Cross(pinNormal, Vector3.up);
                if (tangent1.magnitude < 0.01f)
                    tangent1 = Vector3.Cross(pinNormal, Vector3.right);
                tangent1.Normalize();
                Vector3 tangent2 = Vector3.Cross(pinNormal, tangent1);

                float size = 0.2f;
                Vector3[] corners = new Vector3[]
                {
                    pinPoint + tangent1 * size + tangent2 * size,
                    pinPoint - tangent1 * size + tangent2 * size,
                    pinPoint - tangent1 * size - tangent2 * size,
                    pinPoint + tangent1 * size - tangent2 * size
                };

                for (int i = 0; i < 4; i++)
                {
                    Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
                }
            }

            // Afficher la distance au plan
            float dist = Vector3.Dot(transform.position - pinPoint, pinNormal);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(pinPoint, pinNormal * dist);
        }
    }

    bool WouldPenetrate(Quaternion testRot)
    {
        if (col == null) return false;

        if (box != null)
        {
            Vector3 testCenter = transform.position + testRot * Vector3.Scale(box.center, transform.lossyScale);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, Abs(transform.lossyScale));
            return Physics.CheckBox(testCenter, halfExtents, testRot, wallLayers, QueryTriggerInteraction.Ignore);
        }
        else if (col is SphereCollider sphere)
        {
            Vector3 testCenter = transform.position + testRot * sphere.center;
            return Physics.CheckSphere(testCenter, sphere.radius, wallLayers, QueryTriggerInteraction.Ignore);
        }
        else
        {
            return false;
        }
    }

    void Depenetrate()
    {
        if (col == null) return;

        const int maxIterations = 3;
        const int maxOverlaps = 16;
        Collider[] overlaps = new Collider[maxOverlaps];

        for (int iter = 0; iter < maxIterations; iter++)
        {
            int numOverlaps = Physics.OverlapBoxNonAlloc(
                transform.position + transform.rotation * Vector3.Scale(box != null ? box.center : Vector3.zero, transform.lossyScale),
                box != null ? Vector3.Scale(box.size * 0.5f, Abs(transform.lossyScale)) : col.bounds.extents,
                overlaps,
                transform.rotation,
                wallLayers,
                QueryTriggerInteraction.Ignore
            );

            if (numOverlaps == 0) break;

            Vector3 totalSeparation = Vector3.zero;
            bool penetrated = false;

            for (int i = 0; i < numOverlaps; i++)
            {
                Collider other = overlaps[i];
                if (!IsStatic(other)) continue;

                if (Physics.ComputePenetration(
                    col, transform.position, transform.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out Vector3 direction, out float depth
                ))
                {
                    if (depth > 0f)
                    {
                        penetrated = true;
                        totalSeparation += direction * (depth + separateEpsilon + (depth > skinWidth ? emergencyPushback : 0f));
                    }
                }
            }

            if (!penetrated) break;

            transform.position += totalSeparation;

            if (debugMode) Debug.Log($"[WallBlocker] Depenetration applied: {totalSeparation.magnitude} units");
        }
    }
}