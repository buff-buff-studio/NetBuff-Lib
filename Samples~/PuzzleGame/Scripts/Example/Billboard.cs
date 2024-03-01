using UnityEngine;

namespace ExamplePlatformer
{
    public class Billboard : MonoBehaviour
    {
        private void Update()
        {
            transform.LookAt(Camera.main!.transform);
        }
    }
}