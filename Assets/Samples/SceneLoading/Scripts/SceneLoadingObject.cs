using NetBuff.Components;
using UnityEngine;

namespace Samples.SceneLoading
{
    public class SceneLoadingObject : NetworkBehaviour
    {
        public bool destroyable = false;
        
        private static readonly Color[] _Colors = {Color.white, Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta};
        
        public override void OnSpawned(bool isRetroactive)
        {
            var sceneId = GetSceneId(gameObject.scene.name);
            GetComponent<Renderer>().material.color = _Colors[sceneId];
        }

        public override void OnSceneChanged(int fromScene, int toScene)
        {
            GetComponent<Renderer>().material.color = _Colors[toScene];
        }

        private void Update()
        {
            if(HasAuthority && destroyable && Input.GetKeyDown(KeyCode.K))
            {
                Despawn();
            }
        }
    }
}