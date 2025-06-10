using System.IO;
using NetBuff;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace Samples.SceneLoading
{
    public class SceneLoadingController : NetworkBehaviour
    {
        public string scene1 = "Scene1";
        public string scene2 = "Scene2";
        public GameObject prefab;

        public void OnEnable()
        {
            PacketListener.GetPacketListener<SceneLoadingPacketTest>().AddClientListener(OnSceneLoadingPacketTest);
        }

        public void OnDisable()
        {
            PacketListener.GetPacketListener<SceneLoadingPacketTest>().RemoveClientListener(OnSceneLoadingPacketTest);
        }

        private bool OnSceneLoadingPacketTest(SceneLoadingPacketTest test)
        {
            Debug.Log($"Received packet from server: {test.TestMessage}");
            var objectCount = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            Debug.Log("Counted " + objectCount + " objects in the scene.");
            return true; // Return true to indicate the packet was handled
        }

        public void Update()
        {
            if (IsServer)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    Debug.Log("Toggling scene 1");
                    if (NetworkManager.Instance.IsSceneLoaded(scene1))
                        NetworkManager.Instance.UnloadScene(scene1);
                    else
                        NetworkManager.Instance.LoadScene(scene1);

                    Debug.Log($"After toggling scene 1, is scene loaded: {NetworkManager.Instance.IsSceneLoaded(scene1)}");
                    ServerBroadcastPacket(new SceneLoadingPacketTest
                    {
                        TestMessage = "Hello from the server!"
                    }, true);
                }

                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    if (NetworkManager.Instance.IsSceneLoaded(scene2))
                        NetworkManager.Instance.UnloadScene(scene2);
                    else
                        NetworkManager.Instance.LoadScene(scene2);

                    ServerBroadcastPacket(new SceneLoadingPacketTest
                    {
                        TestMessage = "Hello from the server!"
                    });
                }

                var count = NetworkManager.Instance.GetNotReadyClientCount();
                if(count > 0)
                    Debug.Log($"Not ready clients: {count}");
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                Spawn(prefab, Random.insideUnitSphere * 3f, Quaternion.identity, Vector3.one, true, scene: 0);
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                var sc1 = NetworkManager.Instance.GetSceneId(scene1);
                if (sc1 != -1)
                    Spawn(prefab, Random.insideUnitSphere * 5f, Quaternion.identity, Vector3.one, true, scene: sc1);
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                var sc2 = NetworkManager.Instance.GetSceneId(scene2);
                if (sc2 != -1)
                    Spawn(prefab, Random.insideUnitSphere * 5f, Quaternion.identity, Vector3.one, true, scene: sc2);
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                Spawn(prefab, Random.insideUnitSphere * 5f, Quaternion.identity, Vector3.one, true);
            }
        }
    }

    public class SceneLoadingPacketTest : IPacket
    {
        public string TestMessage { get; set; }

        public void Deserialize(BinaryReader reader)
        {
            TestMessage = reader.ReadString();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(TestMessage);
        }
    }
}