using UnityEngine;

namespace ExamplePlatformer
{
    public class OrbitCamera : MonoBehaviour
    {
        public GameObject target;
        public float distance = 10.0f;
        public Vector3 offset = new Vector3(0, 1f, 0);

        public float rotationX;
        
        void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            
            if (target == null)
                return;
            
            rotationX -= Input.GetAxis("Mouse Y") * 3f;
            rotationX = Mathf.Clamp(rotationX, -20, 90);

            Transform t = transform;
            t.eulerAngles = new Vector3(rotationX, t.eulerAngles.y + Input.GetAxis("Mouse X") * 3f, 0);

            t.position = target.transform.position - (t.forward * distance) + offset;
        }
    }
}