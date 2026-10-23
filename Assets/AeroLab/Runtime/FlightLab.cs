using AeroLab.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace AeroLab
{
    [DisallowMultipleComponent]
    public sealed class FlightLab : MonoBehaviour
    {
        public FlightSimulation Simulation { get; private set; }
        public bool Demo { get; private set; }
        public bool Paused { get; private set; }
        public bool Obstacles { get; private set; } = true;
        public bool ShowRoute { get; set; } = true;
        public bool ShowSensors { get; set; }
        public int Seed { get; private set; }
        public bool ShowWelcome { get; private set; } = true;

        private readonly DemoPilot pilot = new DemoPilot();
        private FlightWorld world;
        private FlightCamera flightCamera;
        private FlightHud hud;

        private void OnEnable()
        {
            // Unity can reload managed assemblies while retaining native scene objects.
            // Recreate the non-serialized simulation and rebind the existing renderers.
            if (Simulation != null) return;
            foreach (Transform child in transform)
            {
                if (child.name != "Generated flight arena" && child.name != "Flight camera" && child.name != "Sun") continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            Awake();
        }

        private void Awake()
        {
            Time.fixedDeltaTime = FlightSimulation.Dt;
            Time.maximumDeltaTime = 0.1f;
            Application.targetFrameRate = 60;
            Simulation = new FlightSimulation();
            world = GetComponent<FlightWorld>() ?? gameObject.AddComponent<FlightWorld>();
            world.Initialize(this);
            flightCamera = GetComponent<FlightCamera>() ?? gameObject.AddComponent<FlightCamera>();
            flightCamera.Initialize(this);
            hud = GetComponent<FlightHud>() ?? gameObject.AddComponent<FlightHud>();
            hud.Initialize(this);
            ResetFlight();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) ResetFlight();
            if (Input.GetKeyDown(KeyCode.C)) flightCamera.ToggleView();
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Return) && ShowWelcome) StartManual();
        }

        private void FixedUpdate()
        {
            if (!Simulation.Running || Paused) return;
            NVector3 action = Demo ? pilot.Action(Simulation) : new NVector3(
                Axis(KeyCode.D, KeyCode.A), Axis(KeyCode.Space, KeyCode.LeftControl, KeyCode.RightControl),
                Axis(KeyCode.S, KeyCode.W));
            Simulation.Step(action);
        }

        private void LateUpdate()
        {
            world.Refresh();
            flightCamera.Refresh();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Simulation != null && Simulation.Running) Paused = true;
        }

        public void ResetFlight()
        {
            Simulation.Reset(Seed, Obstacles);
            pilot.Reset();
            Demo = false;
            Paused = false;
            ShowWelcome = true;
            world.Rebuild();
        }

        public void StartManual()
        {
            if (Simulation.Result != FlightResult.None) ResetFlight();
            Demo = false;
            Paused = false;
            ShowWelcome = false;
            Simulation.Begin();
        }

        public void StartDemo()
        {
            ResetFlight();
            Demo = true;
            ShowWelcome = false;
            Simulation.Begin();
        }

        public void TakeControl() { Demo = false; Paused = false; }
        public void TogglePause() { if (Simulation.Running) Paused = !Paused; }
        public void ToggleCamera() { flightCamera.ToggleView(); }
        public void SetObstacles(bool value) { if (Obstacles != value) { Obstacles = value; ResetFlight(); } }
        public void NextLayout() { Seed++; ResetFlight(); }

        private static float Axis(KeyCode positive, KeyCode negative, KeyCode alternateNegative = KeyCode.None)
        {
            return (Input.GetKey(positive) ? 1 : 0) - (Input.GetKey(negative) || (alternateNegative != KeyCode.None && Input.GetKey(alternateNegative)) ? 1 : 0);
        }

        public static Vector3 ToUnity(NVector3 value) { return new Vector3(value.X, value.Y, value.Z); }
    }
}
