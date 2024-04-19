using NetBuff;
using NetBuff.Components;
using UnityEngine;

namespace Samples.SceneLoading
{
    public class SceneLoadingController : NetworkBehaviour
    {
        public string scene1 = "Scene1";
        public string scene2 = "Scene2";
        public GameObject prefab;
        
        public void Update()
        {
            if (IsServer)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    if (NetworkManager.Instance.IsSceneLoaded(scene1))
                        NetworkManager.Instance.UnloadScene(scene1);
                    else
                        NetworkManager.Instance.LoadScene(scene1);
                }

                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    if (NetworkManager.Instance.IsSceneLoaded(scene2))
                        NetworkManager.Instance.UnloadScene(scene2);
                    else
                        NetworkManager.Instance.LoadScene(scene2);
                }
            }

            if(Input.GetKeyDown(KeyCode.U))
            {
                Spawn(prefab, Random.insideUnitSphere * 3f, Quaternion.identity, Vector3.one, true, scene: 0);
            }

            if(Input.GetKeyDown(KeyCode.I))
            {
                Spawn(prefab, Random.insideUnitSphere * 3f, Quaternion.identity, Vector3.one, true, scene: 1);
            }
            
            if(Input.GetKeyDown(KeyCode.O))
            {
                Spawn(prefab, Random.insideUnitSphere * 3f, Quaternion.identity, Vector3.one, true, scene: 2);
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                Spawn(prefab, Random.insideUnitSphere * 3f, Quaternion.identity, Vector3.one, true);
            }
        }
    }
}