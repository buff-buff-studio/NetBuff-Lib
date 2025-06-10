using NetBuff.Components;
using UnityEngine;

namespace Samples.SceneLoading
{
    public class SimpleObjectWanderer : NetworkBehaviour
    {
        private Vector3 _startPos;
        private Vector3 _position;

        private void Start()
        {
            if (!HasAuthority)
                return;

            _startPos = transform.position;
            _position = transform.position;
        }

        private void Update()
        {
            if (!HasAuthority)
                return;

            if (Vector3.Distance(transform.position, _position) < 0.2f)
                _position = Random.insideUnitSphere * 2f + _startPos;
                
            transform.position = Vector3.MoveTowards(transform.position, _position, Time.deltaTime * 2f);
        }
    }
}