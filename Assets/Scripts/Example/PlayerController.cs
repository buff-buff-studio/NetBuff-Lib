using System.IO;
using System.Threading.Tasks;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ExamplePlatformer
{
    public class PlayerController : NetworkBehaviour
    {
        private OrbitCamera _orbitCamera;
        private CharacterController _controller;
        
        [Header("REFERENCES")]
        public NetworkAnimator animator;
        public Transform body;
        public TMP_Text headplate;
        
        [Header("SETTINGS")]
        public float walkSpeed = 2f;
        public float jumpForce = 4f;
        public float rotationSpeed = 5f;
        public float gravity = 12f;
        public int tickRate = 50;
        
        [Header("STATE")]
        public Vector3 velocity = Vector3.zero;
        public float punchCooldown;
        public float remoteBodyRotation;
        public bool IsGrounded => _controller.isGrounded;
        
        //Synced Name
        public string Name { get; set; }
        

        public void OnEnable()
        { 
            remoteBodyRotation = body.localEulerAngles.y;
            _controller = GetComponent<CharacterController>();
            _orbitCamera = FindFirstObjectByType<OrbitCamera>();
            InvokeRepeating(nameof(Tick), 0, 1f / tickRate);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(Tick));
        }
        
        /// <summary>
        /// Send data to new clients
        /// </summary>
        /// <param name="clientId"></param>
        public override void OnClientConnected(int clientId)
        {
            ServerSendPacket(new PacketPlayerData
            {
                Id = Id,
                Name = headplate.text
            }, clientId, true);
        }
        
        /// <summary>
        /// Tick the behaviour
        /// </summary>
        public void Tick()
        {
            if (!HasAuthority || !IsOwnedByClient)
                return;
            
            var packet = new PacketBodyRotation
            {
                Id = Id,
                BodyRotation = body.localEulerAngles.y
            };
            SendPacket(packet);
        }

        /// <summary>
        /// Called when a packet is received by the server
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="clientId"></param>
        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            switch (packet)
            {
                case PacketBodyRotation data:
                    if(clientId == OwnerId)
                        ServerBroadcastPacketExceptFor(data, clientId);
                    break;
                case PacketPlayerData data2:
                    if(clientId == OwnerId)
                        ServerBroadcastPacketExceptFor(data2, clientId , true);
                    break;
            }
        }

        /// <summary>
        /// Called when a packet is received by the client
        /// </summary>
        /// <param name="packet"></param>
        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            switch (packet)
            {
                case PacketBodyRotation bodyRot:
                    remoteBodyRotation = bodyRot.BodyRotation;
                    break;
                case PacketPlayerData data:
                    headplate.text = data.Name;
                    break;
            }
        }

        /// <summary>
        /// Called when the object is spawned
        /// </summary>
        /// <param name="isRetroactive"></param>
        public override void OnSpawned(bool isRetroactive)
        {
            if (!HasAuthority || !IsOwnedByClient)
                return;
            
            _orbitCamera.target = gameObject;
            headplate.text = CreateRandomEnglishName();
            
            SendPacket(new PacketPlayerData
            {
                Id = Id,
                Name = headplate.text
            }, true);
        }
        
        private string CreateRandomEnglishName()
        {
            string[] vowels = {"a", "e", "i", "o", "u"};
            string[] others = {"jh", "w", "n", "g", "gn", "b", "t", "th", "r", "l", "s", "sh", "k", "m", "d", "f", "v", "z", "p", "j", "ch"};

            var current = "";
            var b = Random.Range(0, 2) == 0;
            
            for (var i = 0; i < Random.Range(3, 10); i++)
            {
                if (b)
                    current += vowels[Random.Range(0, vowels.Length)];
                else
                    current += others[Random.Range(0, others.Length)];
                
                b = !b;
            }
            
            return current[..1].ToUpper() + current[1..];
        }

        private void Update()
        {
            if (!HasAuthority || !IsOwnedByClient)
            {
                body.localEulerAngles = new Vector3(0, Mathf.LerpAngle(body.localEulerAngles.y, remoteBodyRotation, Time.deltaTime * 20), 0);
                return;
            }

            if (IsGrounded)
                velocity.y = Mathf.Max(velocity.y, -1);
            else
                velocity.y -= gravity * Time.deltaTime;

            var move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;
            var camAngle = Camera.main!.transform.eulerAngles.y;
            var targetAngle = camAngle;
            var moveAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
            var te = transform.eulerAngles;
            transform.eulerAngles = new Vector3(0, Mathf.LerpAngle(te.y, targetAngle, move.magnitude * Time.deltaTime * rotationSpeed), 0);
            body.localEulerAngles = new Vector3(0, Mathf.LerpAngle(body.localEulerAngles.y, moveAngle, move.magnitude * Time.deltaTime * rotationSpeed * 1f));
            move = Quaternion.Euler(0, te.y, 0) * move;
            
            if (punchCooldown > 0)
            {
                move = Vector3.zero;
                punchCooldown -= Time.deltaTime;
            }
            
            velocity.x = move.x;
            velocity.z = move.z;
            _controller.Move(velocity * (walkSpeed * Time.deltaTime));

            var to = move.magnitude > 0.1f ? 1 : 0;
            
            animator.SetBool("Jumping", !IsGrounded);

            if (Input.GetMouseButtonDown(0) && punchCooldown <= 0 && IsGrounded)
            {
                animator.SetTrigger("Punch");
                punchCooldown = 1.25f;
                
                //Run after
                Task.Run(async () =>
                {
                    await Task.Delay(500);

                    SendPacket(new PlayerPunchActionPacket
                    {
                        Id = Id
                    }, true);
                });
            }
            
            animator.SetFloat("Running", Mathf.Lerp(animator.GetFloat("Running"), to, Time.deltaTime * 5f));
            
            if (Input.GetKeyDown(KeyCode.Space) && IsGrounded)
                velocity.y = jumpForce;
        }
    }
    
    public class PacketBodyRotation : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public float BodyRotation { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(BodyRotation);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            BodyRotation = reader.ReadSingle();
        }
    }
    
    public class PacketPlayerData : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public string Name { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(Name);
        }
        
        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            Name = reader.ReadString();
        }
    }

    public class PlayerPunchActionPacket : IPacket
    {
        public NetworkId Id { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
        }
    }
}