using System;
using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Packets
{
    public class NetworkPreExistingInfoPacket : IPacket
    {
        public PreExistingState[] PreExistingObjects { get; set; }

        public NetworkId[] RemovedObjects { get; set; }

        public string[] LoadedSceneNames { get; set; }

        public NetworkObjectSpawnPacket[] SpawnedObjects { get; set; }

        public NetworkBehaviourDataPacket[] NetworkValues { get; set; }
        
        public bool IsSnapshot { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PreExistingObjects.Length);
            foreach (var preExistingObject in PreExistingObjects)
            {
                writer.Write(preExistingObject.Id);
                writer.Write(preExistingObject.PrefabId);
                writer.Write(preExistingObject.OwnerId);
                writer.Write(preExistingObject.Position.x);
                writer.Write(preExistingObject.Position.y);
                writer.Write(preExistingObject.Position.z);
                writer.Write(preExistingObject.Rotation.x);
                writer.Write(preExistingObject.Rotation.y);
                writer.Write(preExistingObject.Rotation.z);
                writer.Write(preExistingObject.Rotation.w);
                writer.Write(preExistingObject.Scale.x);
                writer.Write(preExistingObject.Scale.y);
                writer.Write(preExistingObject.Scale.z);
                writer.Write(preExistingObject.IsActive);
                writer.Write(preExistingObject.SceneId);
            }

            writer.Write(RemovedObjects.Length);
            foreach (var removedObject in RemovedObjects)
                writer.Write(removedObject);

            writer.Write(LoadedSceneNames.Length);
            foreach (var sceneName in LoadedSceneNames) writer.Write(sceneName);

            writer.Write(SpawnedObjects.Length);
            foreach (var spawnedObject in SpawnedObjects)
            {
                writer.Write(spawnedObject.Id);
                writer.Write(spawnedObject.PrefabId);
                writer.Write(spawnedObject.OwnerId);
                writer.Write(spawnedObject.Position.x);
                writer.Write(spawnedObject.Position.y);
                writer.Write(spawnedObject.Position.z);
                writer.Write(spawnedObject.Rotation.x);
                writer.Write(spawnedObject.Rotation.y);
                writer.Write(spawnedObject.Rotation.z);
                writer.Write(spawnedObject.Rotation.w);
                writer.Write(spawnedObject.Scale.x);
                writer.Write(spawnedObject.Scale.y);
                writer.Write(spawnedObject.Scale.z);
                writer.Write(spawnedObject.IsActive);
                writer.Write(spawnedObject.SceneId);
            }

            writer.Write(NetworkValues.Length);
            foreach (var networkValue in NetworkValues)
            {
                writer.Write(networkValue.Id);
                writer.Write(networkValue.BehaviourId);
                writer.Write(networkValue.Payload.Length);
                writer.Write(networkValue.Payload);
            }
            
            writer.Write(IsSnapshot);
        }

        public void Deserialize(BinaryReader reader)
        {
            var preExistingObjectsLength = reader.ReadInt32();
            PreExistingObjects = new PreExistingState[preExistingObjectsLength];
            for (var i = 0; i < preExistingObjectsLength; i++)
                PreExistingObjects[i] = new PreExistingState
                {
                    Id = reader.ReadNetworkId(),
                    PrefabId = reader.ReadNetworkId(),
                    OwnerId = reader.ReadInt32(),
                    Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                        reader.ReadSingle()),
                    Scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    IsActive = reader.ReadBoolean(),
                    SceneId = reader.ReadInt32()
                };

            var removedObjectsLength = reader.ReadInt32();
            RemovedObjects = new NetworkId[removedObjectsLength];
            for (var i = 0; i < removedObjectsLength; i++)
                RemovedObjects[i] = reader.ReadNetworkId();

            var sceneNamesLength = reader.ReadInt32();
            LoadedSceneNames = new string[sceneNamesLength];
            for (var i = 0; i < sceneNamesLength; i++)
                LoadedSceneNames[i] = reader.ReadString();

            var spawnedObjectsLength = reader.ReadInt32();
            SpawnedObjects = new NetworkObjectSpawnPacket[spawnedObjectsLength];
            for (var i = 0; i < spawnedObjectsLength; i++)
                SpawnedObjects[i] = new NetworkObjectSpawnPacket
                {
                    Id = reader.ReadNetworkId(),
                    PrefabId = reader.ReadNetworkId(),
                    OwnerId = reader.ReadInt32(),
                    Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                        reader.ReadSingle()),
                    Scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    IsActive = reader.ReadBoolean(),
                    SceneId = reader.ReadInt32()
                };

            var networkValuesLength = reader.ReadInt32();
            NetworkValues = new NetworkBehaviourDataPacket[networkValuesLength];
            for (var i = 0; i < networkValuesLength; i++)
                NetworkValues[i] = new NetworkBehaviourDataPacket
                {
                    Id = reader.ReadNetworkId(),
                    BehaviourId = reader.ReadByte(),
                    Payload = reader.ReadBytes(reader.ReadInt32())
                };
            
            IsSnapshot = reader.ReadBoolean();
        }

        [Serializable]
        public class PreExistingState
        {
            [NetworkIdInspectorMode(NetworkIdInspectorMode.Object)]
            public NetworkId Id { get; set; }

            [NetworkIdInspectorMode(NetworkIdInspectorMode.Prefab)]
            public NetworkId PrefabId { get; set; }

            [NetworkIdInspectorMode(NetworkIdInspectorMode.Owner)]
            public int OwnerId { get; set; }

            public Vector3 Position { get; set; }

            public Quaternion Rotation { get; set; }

            public Vector3 Scale { get; set; }

            public bool IsActive { get; set; }

            [NetworkIdInspectorMode(NetworkIdInspectorMode.Scene)]
            public int SceneId { get; set; }
        }
    }
}