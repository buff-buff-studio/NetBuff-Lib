using System;
using NetBuff;
using NetBuff.Components;
using UnityEngine;

namespace CTF
{
    public class MovableObstacle : NetworkBehaviour
    {
        public Transform pointA;
        public Transform pointB;

        private void OnDrawGizmos()
        {
            var posA = pointA.position;
            var posB = pointB.position;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(posA, 0.25f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(posB, 0.25f);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(posA, posB);
        }
        

        private void Update()
        {
            if (NetworkManager.Instance.IsServerRunning)
            {
                var t = Mathf.PingPong(Time.time, 1);
                transform.position = Vector3.Lerp(pointA.position, pointB.position, t);
            }
        }
    }
}