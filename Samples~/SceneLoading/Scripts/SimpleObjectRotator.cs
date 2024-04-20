using NetBuff.Components;
using UnityEngine;

namespace Samples.SceneLoading
{
    public class SimpleObjectRotator : NetworkBehaviour
    {
        [SerializeField, HideInInspector]
        private Vector3 initialRotation;
        [SerializeField, HideInInspector]
        private Vector3 rotDirection;

        public override void OnSpawned(bool isRetroactive)
        {
            initialRotation = new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
            rotDirection = Random.insideUnitSphere.normalized;
        }
        
        private void Update()
        {
            if (!HasAuthority)
                return;
            
            transform.eulerAngles = initialRotation + rotDirection * (Time.time * 30 % 360);
        }
    }
}