using NetBuff.Components;
using UnityEngine;

namespace NetBuff.Samples.LocalSplitScreen
{
    public class PlayerController : NetworkBehaviour
    {
        private void Update()
        {
            if (!HasAuthority)
                return;

            var moveInput = GetMoveInput();
            const float speed = 5f;
            transform.position += moveInput * (speed * Time.deltaTime);
        }

        public override void OnSpawned(bool isRetroactive)
        {
            var localClientCount = GetLocalClientCount();
            var localClientIndex = GetLocalClientIndex(OwnerId);
            var cam = Camera.main!;

            if (localClientIndex != 0)
            {
                cam = Instantiate(cam);
                if (cam.TryGetComponent<AudioListener>(out var al))
                    Destroy(al);
                cam.tag = "Untagged";
            }

            _SetupCameraPos(localClientCount, localClientIndex, cam);
        }

        private void _SetupCameraPos(int count, int index, Camera cam)
        {
            if (count == 1)
                return;

            var top = count / 2;
            var bottom = count - top;

            var isOnTop = index < top;
            var y = isOnTop ? 0.5F : 0;
            var x = isOnTop ? index / (float)top : (index - top) / (float)bottom;
            var h = 0.5f;
            var w = isOnTop ? 1f / top : 1f / bottom;
            cam.rect = new Rect(x, y, w, h);
        }

        public Vector3 GetMoveInput()
        {
            switch (OwnerId)
            {
                case 0:
                    var moveX1 = (Input.GetKey(KeyCode.A) ? -1 : 0) + (Input.GetKey(KeyCode.D) ? 1 : 0);
                    var moveY1 = (Input.GetKey(KeyCode.S) ? -1 : 0) + (Input.GetKey(KeyCode.W) ? 1 : 0);
                    return new Vector3(moveX1, moveY1, 0);

                case 1:
                    var moveX2 = (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) +
                                 (Input.GetKey(KeyCode.RightArrow) ? 1 : 0);
                    var moveY2 = (Input.GetKey(KeyCode.DownArrow) ? -1 : 0) + (Input.GetKey(KeyCode.UpArrow) ? 1 : 0);
                    return new Vector3(moveX2, moveY2, 0);
            }

            return Vector3.zero;
        }
    }
}