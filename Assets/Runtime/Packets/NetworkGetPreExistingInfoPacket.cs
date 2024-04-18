using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Packets
{
    public class NetworkGetPreExistingInfoPacket : IPacket
    {
        public class PreExistingState
        {
            public NetworkId Id { get; set; }
            public NetworkId PrefabId { get; set; }
            public int OwnerId { get; set; }
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
            public Vector3 Scale { get; set; }
            public bool IsActive { get; set; }
            public int SceneId { get; set; }
        }  
        
        public PreExistingState[] PreExistingObjects { get; set; }
        public NetworkId[] RemovedObjects { get; set; }
        public string[] SceneNames { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PreExistingObjects.Length);
            foreach (var preExistingObject in PreExistingObjects)
            {
                preExistingObject.Id.Serialize(writer);
                preExistingObject.PrefabId.Serialize(writer);
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
            }
            writer.Write(RemovedObjects.Length);
            foreach (var removedObject in RemovedObjects)
            {
                removedObject.Serialize(writer);
            }
            
            writer.Write(SceneNames.Length);
            foreach (var sceneName in SceneNames)
            {
                writer.Write(sceneName);
            }
        }
        
        public void Deserialize(BinaryReader reader)
        {
            var preExistingObjectsLength = reader.ReadInt32();
            PreExistingObjects = new PreExistingState[preExistingObjectsLength];
            for (var i = 0; i < preExistingObjectsLength; i++)
            {
                PreExistingObjects[i] = new PreExistingState
                {
                    Id = NetworkId.Read(reader),
                    PrefabId = NetworkId.Read(reader),
                    OwnerId = reader.ReadInt32(),
                    Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    Scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    IsActive = reader.ReadBoolean()
                };
            }
            var removedObjectsLength = reader.ReadInt32();
            RemovedObjects = new NetworkId[removedObjectsLength];
            for (var i = 0; i < removedObjectsLength; i++)
            {
                RemovedObjects[i] = NetworkId.Read(reader);
            }
            
            var sceneNamesLength = reader.ReadInt32();
            SceneNames = new string[sceneNamesLength];
            for (var i = 0; i < sceneNamesLength; i++)
            {
                SceneNames[i] = reader.ReadString();
            }
        }
    }
}