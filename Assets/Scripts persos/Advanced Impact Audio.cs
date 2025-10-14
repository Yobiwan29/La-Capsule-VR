using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

namespace CapsuleVR.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class AdvancedImpactAudio : MonoBehaviour
    {
        // ... (toutes tes variables publiques restent les mêmes) ...
        [Header("Audio source")]
        public AudioSource source;

        [Header("Randomisation")]
        public Vector2 pitchRange = new Vector2(0.96f, 1.04f);

        [Header("Courbe volume vs vitesse (m/s)")]
        public AnimationCurve volumeBySpeed = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.5f, 0.15f), new Keyframe(1.5f, 0.35f),
            new Keyframe(3f, 0.65f), new Keyframe(6f, 1f));
        [Min(0f)] public float speedClampMax = 6f;
        [Min(0f)] public float minImpactSpeed = 0.1f;
        [Min(0f)] public float minInterval = 0.06f;

        [System.Serializable]
        public class SoundSet { public LayerMask layers = ~0; public AudioClip[] clips; [Range(0f, 1f)] public float baseVolume = 1f; }

        [Header("Sets par layer (ex: Ground, Default)")]
        public List<SoundSet> impactSets = new List<SoundSet>();
        public SoundSet defaultImpact;

        [Header("Grab / Release")]
        public AudioClip[] grabClips;
        public AudioClip[] releaseClips;
        [Range(0f, 1f)] public float grabBaseVolume = 1f;
        public bool autoDetectGrab = true;
        public GrabInteractable grabInteractable;

        [Header("Filtrage collisions audio")]
        public bool ignoreTriggersForImpact = true;
        public bool ignoreWithMarker = true;
        public bool ignoreWhileGrabbed = true; // CHANGÉ : On va gérer ça différemment
        public LayerMask ignoreLayersForImpact = 0;

        [Header("Anti-spam Grab")]
        [Min(0f)] public float minGrabInterval = 0.15f;

        private float _lastGrabPlay, _lastReleasePlay, _lastPlayTime;
        private Rigidbody _rb;
        private bool _wasGrabbed;

        // ... (Reset, Awake, Update, PlayGrab, PlayRelease, PickSetForLayer restent les mêmes) ...

        void Reset()
        {
            source = GetComponent<AudioSource>();
            if (!source) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (!source)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
            }
        }

        void Update()
        {
            if (autoDetectGrab && grabInteractable != null)
            {
                bool isGrabbed = grabInteractable.Interactors.Count > 0;
                if (isGrabbed && !_wasGrabbed) PlayGrab();
                else if (!isGrabbed && _wasGrabbed) PlayRelease();
                _wasGrabbed = isGrabbed;
            }
        }

        void OnCollisionEnter(Collision c)
        {
            // On gère le "ignoreWhileGrabbed" directement dans la logique du WallBlocker.
            // Si l'objet n'est pas tenu (pas kinematic), OnCollisionEnter fonctionne.
            if (_rb && _rb.isKinematic) return;

            var other = c.collider;
            if (ignoreTriggersForImpact && other.isTrigger) return;
            if (ignoreWithMarker && other.GetComponentInParent<IgnoreWallBlockerAndImpact>() != null) return;
            if ((ignoreLayersForImpact.value & (1 << other.gameObject.layer)) != 0) return;

            float speed = c.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            // MODIFIÉ : On passe le point de contact
            PlayImpact(speed, other.gameObject.layer, c.contacts[0].point);
        }

        // MODIFIÉ : API publique appelée par le WallBlocker
        public void NotifyProxyImpact(float speed, int otherLayer, Vector3 position)
        {
            if (speed < minImpactSpeed) return;
            PlayImpact(speed, otherLayer, position);
        }

        // MODIFIÉ : PlayImpact utilise maintenant la méthode de l'AudioSource "jetable"
        void PlayImpact(float rawSpeed, int otherLayer, Vector3 position)
        {
            if (Time.time - _lastPlayTime < minInterval) return;

            var set = PickSetForLayer(otherLayer);
            if (set == null || set.clips == null || set.clips.Length == 0) return;

            float s = Mathf.Clamp(rawSpeed, 0f, speedClampMax);
            float vol = volumeBySpeed.Evaluate(s) * set.baseVolume;
            if (vol <= 0.001f) return;

            _lastPlayTime = Time.time;

            // LOGIQUE FUSIONNÉE
            var clip = set.clips[Random.Range(0, set.clips.Length)];
            var pitch = Random.Range(pitchRange.x, pitchRange.y);
            PlayClipAtPoint(clip, position, vol, pitch);
        }

        // MÉTHODE ROBUSTE DE LECTURE (de notre script précédent)
        void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            GameObject tempGO = new GameObject("TempAudio");
            tempGO.transform.position = position;
            AudioSource aSource = tempGO.AddComponent<AudioSource>();
            aSource.clip = clip;
            aSource.volume = volume;
            aSource.pitch = pitch;
            aSource.spatialBlend = 1.0f;
            aSource.rolloffMode = AudioRolloffMode.Logarithmic;
            aSource.Play();
            Destroy(tempGO, clip.length);
        }

        // Les sons de Grab/Release peuvent continuer à utiliser la source locale car ils ne posaient pas problème.
        public void PlayGrab()
        {
            if (Time.time - _lastGrabPlay < minGrabInterval) return;
            _lastGrabPlay = Time.time;
            PlayRandom(grabClips, grabBaseVolume);
        }

        public void PlayRelease()
        {
            if (Time.time - _lastReleasePlay < minGrabInterval) return;
            _lastReleasePlay = Time.time;
            PlayRandom(releaseClips, grabBaseVolume);
        }

        void PlayRandom(AudioClip[] clips, float baseVol)
        {
            if (source == null || clips == null || clips.Length == 0) return;
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(clips[Random.Range(0, clips.Length)], baseVol);
        }

        SoundSet PickSetForLayer(int layer)
        {
            int mask = 1 << layer;
            foreach (var s in impactSets)
                if ((s.layers.value & mask) != 0) return s;
            return defaultImpact;
        }
    }

    // Un composant marqueur vide, si tu en as besoin
    public class IgnoreWallBlockerAndImpact : MonoBehaviour { }
}