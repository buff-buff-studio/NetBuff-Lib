using System.Linq;
using System.Collections.Generic;
using NetBuff;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace CTF
{
    public class PlayerController : NetworkBehaviour
    {
        public static readonly List<PlayerController> Players = new();

        [Header("REFERENCES")] 
        public Renderer[] body;
        private Rigidbody _rigidbody;
        private Camera _camera;
        public GameObject carryingFlag;
        public Renderer carryingFlagRenderer;
        public GameObject shotPrefab;
        public NetworkAnimator animator;
        
        [Header("SETTINGS")]
        public float walkSpeed = 5;
        public float jumpForce = 3;
        public float mouseSensitivity = 2;

        [Header("STATE")]
        public IntNetworkValue team = new(-1);
        public BoolNetworkValue hasFlag = new(false, NetworkValue.ModifierType.Server);

        public float cameraRot;
        
        public float shotTimeout = 0.5f;

        private void OnEnable()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _camera = Camera.main;
            Players.Add(this);
            
            team.OnValueChanged += OnChangeTeam;
            hasFlag.OnValueChanged += OnChangeHasFlag;
        }
        
        private void OnDisable()
        {
            Players.Remove(this);
        }
        
        private void Update()
        {
            if (!HasAuthority)
                return;

            var deltaTime = Time.deltaTime;
            var isGrounded = false;
            var velocity = _rigidbody.linearVelocity;
            
            shotTimeout -= deltaTime;
            
            //Ground check
            if (Physics.Raycast(transform.position, Vector3.down, out var hit, 1.1f))
            {
                if (hit.distance < 1.08f)
                    isGrounded = true;
            }
            
            //Jump
            if (Input.GetKey(KeyCode.Space) && isGrounded)
            {
                velocity.y = jumpForce;
            }
            
            //Camera view
            transform.localEulerAngles += new Vector3(0, Input.GetAxis("Mouse X") * mouseSensitivity, 0);
            cameraRot -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            cameraRot = Mathf.Clamp(cameraRot, -90, 90);
            _camera.transform.localEulerAngles = new Vector3(cameraRot, 0, 0);
            
            //Moving direction
            var move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            move = Quaternion.Euler(0, _camera.transform.eulerAngles.y, 0) * move;
            velocity.x = move.x * walkSpeed;
            velocity.z = move.z * walkSpeed;
            
            //Apply velocity and equilibrium
            _rigidbody.linearVelocity = velocity;
            
            //Shot
            if(Input.GetMouseButtonDown(0) && shotTimeout <= 0)
            {
                shotTimeout = 0.5f;
                var cameraTransform = _camera.transform;
                Spawn(shotPrefab, cameraTransform.position + cameraTransform.forward * 1, cameraTransform.rotation, Vector3.one * 0.5f, true);
            }
            
            animator.SetBool("walking", move.magnitude > 0.1f);
            animator.SetBool("jumping", !isGrounded);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!NetworkManager.Instance.IsServerRunning)
                return;
            
            if (other.TryGetComponent(out Flag flag))
                GameManager.Instance.CaptureFlag(flag, this);
            
            if (other.TryGetComponent(out Spawn spawn) && hasFlag.Value)
                if (spawn.team == team.Value)
                    GameManager.Instance.FinishFlagCapture(this);
        }

        public override void OnSpawned(bool isRetroactive)
        {
            if (!HasAuthority)
                return;
            
            var teamRed = Players.FindAll(p => p.team.Value == 0);
            var teamBlue = Players.FindAll(p => p.team.Value == 1);
            
            team.Value = teamRed.Count > teamBlue.Count ? 1 : 0;
            
            //Take camera
            var camTransform = _camera.transform;
            camTransform.SetParent(transform);
            camTransform.localPosition = new Vector3(0, 0.9f, 0);
            
            var t = transform;
            t.position = GameManager.Instance.GetSpawnPoint(team.Value);
            t.forward = new Vector3(-t.position.x, 0, 0);
                
            var teamDisplay = GameManager.Instance.labelTeamDisplay;
            teamDisplay.text = team.Value == 0 ? "Your team: Red" : "Your team: Blue";
            teamDisplay.color = team.Value == 0 ? Color.red : Color.blue;
            Cursor.lockState = CursorLockMode.Locked;
            body[0].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
        
        private void OnChangeTeam(int oldValue, int newValue)
        {
            body.ToList().ForEach(r => r.material.color = newValue == 0 ? Color.red : Color.blue);
        }
        
        private void OnChangeHasFlag(bool oldValue, bool newValue)
        {
            carryingFlag.gameObject.SetActive(newValue);
            if (!newValue) return;
            carryingFlagRenderer.materials[1].color = team.Value == 0 ? Color.blue : Color.red;
        }
        
        public override void OnReceivePacket(IOwnedPacket packet)
        {
            if (!HasAuthority)
                return;
            
            if (packet is PacketPlayerRespawn _)
            {
                var t = transform;
                transform.position = GameManager.Instance.GetSpawnPoint(team.Value);
                _rigidbody.linearVelocity = Vector3.zero;
                t.forward = new Vector3(-t.position.x, 0, 0);
            }
        }
    }
}