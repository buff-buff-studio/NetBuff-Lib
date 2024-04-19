using CTF;
using NetBuff;
using NetBuff.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Samples.SceneLoading
{
    public class TestPanel : NetworkBehaviour
    {
        public string scene = "Test";
        public GameObject prefab;
        
        public void Update()
        {
            if (IsServer)
            {
                if (Input.GetKeyDown(KeyCode.K))
                {
                    if(NetworkManager.Instance.IsSceneLoaded(scene))
                        NetworkManager.Instance.UnloadScene(scene);
                    else
                        NetworkManager.Instance.LoadScene(scene);
                }

                if(Input.GetKeyDown(KeyCode.U))
                {
                    Spawn(prefab, Random.insideUnitSphere * 3f, Quaternion.identity, Vector3.one, true, scene: 0);
                }

                if(Input.GetKeyDown(KeyCode.I))
                {
                    Spawn(prefab, Random.insideUnitSphere * 3f, Quaternion.identity, true);
                }
            }
        }
    }
}