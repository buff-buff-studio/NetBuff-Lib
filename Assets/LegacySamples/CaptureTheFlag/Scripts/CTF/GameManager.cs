using System.IO;
using NetBuff;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using TMPro;
using UnityEngine;

namespace CTF
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }
        public Flag flagRed;
        public Flag flagBlue;
        
        public IntNetworkValue flagRedCarrier = new(-1);
        public IntNetworkValue flagBlueCarrier = new(-1);
        
        public IntNetworkValue redScore = new(0);
        public IntNetworkValue blueScore = new(0);
        public IntNetworkValue time = new(0);

        public GameObject canvas;
        public TMP_Text labelRedScore;
        public TMP_Text labelBlueScore;
        public TMP_Text labelTime;
        public TMP_Text labelTeamDisplay;
        
        
        private void OnEnable()
        {
            WithValues(flagRedCarrier, flagBlueCarrier, redScore, blueScore, time);
            Instance = this;
            
            InvokeRepeating(nameof(UpdateTime), 1, 1);
            
            time.OnValueChanged += OnChangeTime;
            redScore.OnValueChanged += OnChangeRedScore;
            blueScore.OnValueChanged += OnChangeBlueScore;
        }
        
        private void OnChangeTime(int oldValue, int newValue)
        {
            var minutes = newValue / 60;
            var seconds = newValue % 60;
            labelTime.text = $"{minutes}:{seconds:00}";
        }

        private void OnChangeRedScore(int oldValue, int newValue)
        {
            labelRedScore.text = newValue.ToString();
        }
        
        private void OnChangeBlueScore(int oldValue, int newValue)
        {
            labelBlueScore.text = newValue.ToString();
        }
        
        public void UpdateTime()
        {
            if(NetworkManager.Instance.IsServerRunning) 
                time.Value++;
        }
        
        public void CaptureFlag(Flag flag, PlayerController player)
        {
            if (flag.team == player.team.Value)
                return;
            
            flag.SetActive(false);
            
            if (flag.team == 0)
            {
                flagRedCarrier.Value = player.OwnerId;
            }
            else
            {
                flagBlueCarrier.Value = player.OwnerId;
            }
            
            player.hasFlag.Value = true;
        }
        
        public void FinishFlagCapture(PlayerController player)
        {
            if (player.team.Value == 1)
            {
                if (flagRedCarrier.Value == player.OwnerId)
                {
                    player.hasFlag.Value = false;
                    flagRedCarrier.Value = -1;
                    flagRed.SetActive(true);
                    blueScore.Value++;
                }
            }
            else
            {
                if (flagBlueCarrier.Value == player.OwnerId)
                {
                    player.hasFlag.Value = false;
                    flagBlueCarrier.Value = -1;
                    flagBlue.SetActive(true);
                    redScore.Value++;
                }
            }
        }

        public override void OnClientDisconnected(int clientId)
        {
            if (flagRedCarrier.Value == clientId)
            {
                flagRedCarrier.Value = -1;
                flagRed.SetActive(true);
            }
            
            if (flagBlueCarrier.Value == clientId)
            {
                flagBlueCarrier.Value = -1;
                flagBlue.SetActive(true);
            }
        }
        
        public Vector3 GetSpawnPoint(int team)
        {
            return CTF.Spawn.Spawns.Find(s => s.team == team).transform.position;
        }

        private void Update()
        {
            if(!NetworkManager.Instance.IsServerRunning)
                return;
            
            foreach (var player in PlayerController.Players)
            {
                if (player.transform.position.y < -3)
                {
                    SendPacket(new PacketPlayerRespawn {Id = player.Id});
                    
                    if (flagRedCarrier.Value == player.OwnerId)
                    {
                        flagRedCarrier.Value = -1;
                        flagRed.SetActive(true);
                        player.hasFlag.Value = false;
                    }
            
                    if (flagBlueCarrier.Value == player.OwnerId)
                    {
                        flagBlueCarrier.Value = -1;
                        flagBlue.SetActive(true);
                        player.hasFlag.Value = false;
                    }
                }
            }
        }

        public override void OnSpawned(bool isRetroactive)
        {
            canvas.SetActive(true);
        }
    }

    public class PacketPlayerRespawn : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
        }
    }
}