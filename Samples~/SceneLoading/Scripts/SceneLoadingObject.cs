using NetBuff.Components;
using NetBuff.Misc;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Samples.SceneLoading
{
    public class SceneLoadingObject : NetworkBehaviour
    {
        public TMP_Text text;
        public IntNetworkValue number = new(0);
        
        public bool destroyable;
        
        private static readonly Color[] _Colors = {Color.white, Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta};
        
        private void OnEnable()
        {
            WithValues(number);
            number.OnValueChanged += (_, newValue) =>
            {
                if (text != null) text.text = newValue.ToString();
            };
        }

        public override void OnSpawned(bool isRetroactive)
        {
            if (HasAuthority)
            {
                number.Value = Random.Range(10, 99);
            }
            
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
            
            if (number.CheckPermission() && Input.GetKeyDown(KeyCode.R))
            {
                number.Value = Random.Range(10, 99);
            }
        }
    }
}