using PurrNet;
using UnityEngine;
using static UnityEditor.PlayerSettings;

namespace OfficeAI
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerVacuumReceiver : NetworkBehaviour
    {
        [Header("Résistance")]
        [SerializeField] private float maxAttractionSpeed = 10f;
        [SerializeField] private float resistanceForce = 5f;

        [Header("Dégâts")]
        [SerializeField] private float damagePerSecond = 10f;
        [SerializeField] private float nearDistance = 1.5f;
        [SerializeField] private float farDistance = 8f;

        [Header("Feedback Prefabs")]
        [SerializeField] private GameObject vacuumIndicatorUIPrefab;

        private GameObject _spawnedUI;
        private GameObject _spawned3D;
        private Rigidbody _rb;
        private AudioSource _audio;
        private bool _beingVacuumed;
        private float _vacuumTimer;
        private const float VacuumTimeout = 0.3f;

        private Transform _vacuumSource;
        private PlayerHealth _health;
        private float _damageTick;

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (!isOwner) return;

            _health = GetComponent<PlayerHealth>();

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
            _health = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            if (_beingVacuumed)
            {
                _vacuumTimer -= Time.deltaTime;
                if (_vacuumTimer <= 0f)
                    StopVacuumEffect();
            }

            if (_rb.linearVelocity.magnitude > maxAttractionSpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxAttractionSpeed;
        }

        private void FixedUpdate()
        {
            if (!isOwner || !_beingVacuumed || _vacuumSource == null || _health == null)
                return;

            float distance = Vector3.Distance(transform.position, _vacuumSource.position);
            float t = Mathf.InverseLerp(farDistance, nearDistance, distance);
            float damageNow = Mathf.Lerp(0f, damagePerSecond, t);

            _damageTick += Time.fixedDeltaTime;
            if (_damageTick >= 0.1f)
            {
                _damageTick = 0f;
                _health.TakeDamage(damageNow * 0.1f);
            }
        }

        public void ReceiveVacuumForce(Vector3 force, Transform vacuumSource)
        {
            if (!isOwner) return;

            _vacuumSource = vacuumSource;

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
            _damageTick = 0f;

            if (_spawnedUI) _spawnedUI.SetActive(true);
            if (_spawned3D) _spawned3D.SetActive(true);


            AudioController.Instance.PlayLoopedSound(AudioType.Vacuum, AudioSourceType.Mob);
        }

        private void StopVacuumEffect()
        {
            _beingVacuumed = false;
            _vacuumSource = null;
            _damageTick = 0f;

            if (_spawnedUI) _spawnedUI.SetActive(false);
            if (_spawned3D) _spawned3D.SetActive(false);
            if (_audio) _audio.Stop();

            AudioController.Instance.StopSound(AudioType.Vacuum);
        }

        private void OnJump()
        {
            if (!_beingVacuumed) return;

            var awayDir = -(_rb.linearVelocity.normalized);
            _rb.AddForce(awayDir * resistanceForce, ForceMode.Impulse);
        }
    }
}