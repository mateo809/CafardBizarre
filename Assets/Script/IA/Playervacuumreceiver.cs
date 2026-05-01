using UnityEngine;
using PurrNet;

namespace OfficeAI
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerVacuumReceiver : NetworkBehaviour
    {
        [Header("Résistance")]
        [SerializeField] private float maxAttractionSpeed = 10f;
        [SerializeField] private float resistanceForce = 5f;

        [Header("Feedback Prefabs")]
        [SerializeField] private GameObject vacuumIndicatorUIPrefab;
        [SerializeField] private AudioClip vacuumSound;

        private GameObject _spawnedUI;
        private GameObject _spawned3D;
        private Rigidbody _rb;
        private AudioSource _audio;
        private bool _beingVacuumed;
        private float _vacuumTimer;
        private const float VacuumTimeout = 0.3f;

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (!isOwner) return;

            GameObject canvasObj = GameObject.FindWithTag("Canvas");
            if (canvasObj != null && vacuumIndicatorUIPrefab != null)
            {
                _spawnedUI = Instantiate(vacuumIndicatorUIPrefab, canvasObj.transform);
                _spawnedUI.SetActive(false);
            }

        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _audio = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (_beingVacuumed)
            {
                _vacuumTimer -= Time.deltaTime;
                if (_vacuumTimer <= 0f) StopVacuumEffect();
            }

            if (_rb.linearVelocity.magnitude > maxAttractionSpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxAttractionSpeed;
        }

        public void ReceiveVacuumForce(Vector3 force)
        {
            if (!isOwner) return;

            var movement = GetComponent<RoachController1>();
            if (movement != null)
                movement.ApplyExternalForce(force * Time.fixedDeltaTime);

            StartVacuumEffect();
        }

        private void StartVacuumEffect()
        {
            _vacuumTimer = VacuumTimeout;
            if (_beingVacuumed) return;
            _beingVacuumed = true;

            if (_spawnedUI) _spawnedUI.SetActive(true);
            if (_spawned3D) _spawned3D.SetActive(true);

            if (_audio && vacuumSound && !_audio.isPlaying)
                _audio.PlayOneShot(vacuumSound);
        }

        private void StopVacuumEffect()
        {
            _beingVacuumed = false;
            if (_spawnedUI) _spawnedUI.SetActive(false);
            if (_spawned3D) _spawned3D.SetActive(false);
            if (_audio) _audio.Stop();
        }

        private void OnJump()
        {
            if (!_beingVacuumed) return;
            var awayDir = -(_rb.linearVelocity.normalized);
            _rb.AddForce(awayDir * resistanceForce, ForceMode.Impulse);
        }
    }
}