using NetBuff;
using NetBuff.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Samples.SceneLoading
{
    public class TestPanel : NetworkBehaviour
    {
        public string scene = "Test";
        
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
            }
        }
    }
}