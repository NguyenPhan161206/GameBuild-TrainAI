using UnityEngine;

namespace AeroLab
{
    public sealed class FlightCamera : MonoBehaviour
    {
        private FlightLab lab;
        private Camera view;
        private bool overview = true;
        private float yaw = -30;
        private float pitch = 43;
        private float distance = 97;
        private bool initialized;
        private Vector3 lastMouse;
        private bool dragging;

        public void Initialize(FlightLab owner)
        {
            lab = owner;
            var cameraObject = new GameObject("Flight camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            view = cameraObject.GetComponent<Camera>();
            view.tag = "MainCamera";
            view.fieldOfView = 49;
            view.nearClipPlane = 0.1f;
            view.farClipPlane = 500;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = FlightWorld.Hex("0b1c2b");
        }

        public void ToggleView() { overview = !overview; initialized = false; }

        public void Refresh()
        {
            if (view == null || lab.Simulation == null) return;
            if (Input.GetMouseButton(1))
            {
                Vector3 delta = dragging ? Input.mousePosition - lastMouse : Vector3.zero;
                yaw += delta.x * 0.25f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.25f, 20, 78);
            }
            dragging = Input.GetMouseButton(1);
            lastMouse = Input.mousePosition;
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 4, 60, 135);
            Vector3 target = overview ? new Vector3(0, 2, 0) : FlightLab.ToUnity(lab.Simulation.Position);
            float d = overview ? distance : 15;
            float elevation = pitch * Mathf.Deg2Rad, azimuth = yaw * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Cos(azimuth) * Mathf.Cos(elevation)) * d;
            Vector3 wanted = target + offset;
            view.transform.position = initialized ? Vector3.Lerp(view.transform.position, wanted, 1 - Mathf.Exp(-7 * Time.unscaledDeltaTime)) : wanted;
            view.transform.LookAt(target);
            initialized = true;
        }
    }
}
