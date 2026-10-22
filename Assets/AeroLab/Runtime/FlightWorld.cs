using System.Collections.Generic;
using AeroLab.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace AeroLab
{
    public sealed class FlightWorld : MonoBehaviour
    {
        private FlightLab lab;
        private GameObject generated;
        private Transform vehicle;
        private Transform shadow;
        private GameObject route;
        private readonly List<Transform> rotors = new List<Transform>();
        private readonly List<TextMesh> labels = new List<TextMesh>();
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        private readonly List<LineRenderer> sensorLines = new List<LineRenderer>();
        private Material surface;
        private Font font;

        public void Initialize(FlightLab owner)
        {
            lab = owner;
            surface = Resources.Load<Material>("FlightSurface");
            if (surface == null) surface = new Material(Shader.Find("Standard"));
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("9abbd1") * 0.75f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = Hex("0b1c2b");
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 130;
            RenderSettings.fogEndDistance = 260;
            var lightObject = new GameObject("Sun", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(52, -30, 0);
            Light sun = lightObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("def0ff");
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
        }

        public void Rebuild()
        {
            if (generated != null) { generated.SetActive(false); Destroy(generated); }
            generated = new GameObject("Generated flight arena");
            generated.transform.SetParent(transform, false);
            Transform root = generated.transform;
            rotors.Clear(); labels.Clear(); sensorLines.Clear();
            Box(root, new Vector3(220, 0.4f, 220), new Vector3(0, -0.3f, 0), Hex("101f2d"));
            Box(root, new Vector3(80, 0.12f, 80), new Vector3(0, -0.07f, 0), Hex("1a303e"));
            for (int i = -40; i <= 40; i += 5)
            {
                Color grid = Hex(i % 10 == 0 ? "2d4a57" : "243d49");
                Box(root, new Vector3(0.035f, 0.012f, 80), new Vector3(i, 0.007f, 0), grid);
                Box(root, new Vector3(80, 0.012f, 0.035f), new Vector3(0, 0.007f, i), grid);
            }
            foreach (float edge in new[] { -40f, 40f })
            {
                Box(root, new Vector3(0.12f, 0.1f, 80), new Vector3(edge, 0.04f, 0), Hex("47798a"));
                Box(root, new Vector3(80, 0.1f, 0.12f), new Vector3(0, 0.04f, edge), Hex("47798a"));
                foreach (float other in new[] { -40f, 40f })
                {
                    Box(root, new Vector3(0.1f, 30, 0.1f), new Vector3(edge, 15, other), Hex("244959"));
                    Box(root, new Vector3(0.5f, 0.2f, 0.5f), new Vector3(edge, 30, other), Hex("57e4c2"));
                }
            }
            var random = new System.Random(1729);
            for (int i = 0; i < 22; i++)
            {
                float x = (float)random.NextDouble() * 180 - 90;
                float z = (float)random.NextDouble() * 35 + 62;
                float h = (float)random.NextDouble() * 25 + 8;
                Box(root, new Vector3(7, h, 7), new Vector3(x, h / 2, z), Hex("142b3b"));
            }
            int index = 0;
            foreach (Building building in lab.Simulation.Buildings)
            {
                Vector3 center = FlightLab.ToUnity(building.Center), half = FlightLab.ToUnity(building.Half);
                Box(root, half * 2, center, Hex("355366"));
                Box(root, new Vector3(half.x * 2 + 0.1f, 0.15f, half.z * 2 + 0.1f), center + Vector3.up * half.y, Hex("6a8594"));
                for (int level = 1; level < half.y * 2; level += 2)
                    Box(root, new Vector3(half.x * 2 + 0.03f, 0.09f, half.z * 2 + 0.03f), new Vector3(center.x, level, center.z), Hex("789eae"));
                Label(root, (++index).ToString("00"), center + Vector3.up * (half.y + 1.2f), Hex("a7bfcc"), 0.35f);
            }
            Pad(root, lab.Simulation.Start, "A", Hex("a4bfcd"));
            Pad(root, lab.Simulation.Goal, "B", Hex("57e4c2"));
            route = new GameObject("Reference route at 16m");
            route.transform.SetParent(root, false);
            Vector3 a = FlightLab.ToUnity(lab.Simulation.Start), b = FlightLab.ToUnity(lab.Simulation.Goal);
            var points = new[] { a + Vector3.up * 0.4f, new Vector3(a.x, 16, a.z), new Vector3(b.x, 16, b.z), b + Vector3.up * 0.4f };
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 delta = points[i + 1] - points[i];
                for (float d = 0; d < delta.magnitude; d += 1.7f)
                    Primitive(route.transform, PrimitiveType.Sphere, Vector3.one * 0.12f, points[i] + delta.normalized * d, Hex("4e999b"));
            }
            CreateVehicle(root);
            shadow = Box(root, new Vector3(2, 0.014f, 2), Vector3.zero, Hex("0e222e")).transform;
            for (int i = 0; i < 10; i++) sensorLines.Add(CreateLine(root, Hex("edc68a")));
            Refresh();
        }

        private void CreateVehicle(Transform root)
        {
            vehicle = new GameObject("Flying car · 2 x 1 x 2m").transform;
            vehicle.SetParent(root, false);
            Box(vehicle, new Vector3(1.18f, 0.42f, 1.7f), Vector3.zero, Hex("d4e4e9"));
            Box(vehicle, new Vector3(0.85f, 0.27f, 0.92f), new Vector3(0, 0.29f, 0.18f), Hex("173e50"));
            Box(vehicle, new Vector3(0.6f, 0.05f, 0.08f), new Vector3(0, 0.1f, 0.88f), Hex("57e4c2"));
            Box(vehicle, new Vector3(0.6f, 0.05f, 0.08f), new Vector3(0, 0.1f, -0.88f), Hex("ed947b"));
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                Box(vehicle, new Vector3(0.1f, 0.1f, 1.5f), new Vector3(x, -0.1f, 0), Hex("76929f"));
                foreach (float z in new[] { -0.65f, 0.65f })
                {
                    Primitive(vehicle, PrimitiveType.Cylinder, new Vector3(0.48f, 0.06f, 0.48f), new Vector3(x, 0, z), Hex("183846"));
                    Transform rotor = new GameObject("Rotor").transform;
                    rotor.SetParent(vehicle, false);
                    rotor.localPosition = new Vector3(x, 0.08f, z);
                    Box(rotor, new Vector3(0.42f, 0.025f, 0.055f), Vector3.zero, Hex("57e4c2"));
                    rotors.Add(rotor);
                }
            }
            foreach (float x in new[] { -0.45f, 0.45f })
                Box(vehicle, new Vector3(0.08f, 0.2f, 1.3f), new Vector3(x, -0.35f, 0), Hex("526d7c"));
        }

        public void Refresh()
        {
            if (vehicle == null) return;
            Vector3 position = FlightLab.ToUnity(lab.Simulation.Position);
            vehicle.position = position;
            foreach (Transform rotor in rotors) rotor.Rotate(0, Time.deltaTime * (lab.Simulation.Running && !lab.Paused ? 2200 : 150), 0);
            shadow.position = new Vector3(position.x, 0.025f, position.z);
            shadow.localScale = new Vector3(2, 0.014f, 2) * (1 + (position.y - 0.5f) * 0.02f);
            route.SetActive(lab.ShowRoute);
            Camera camera = Camera.main;
            if (camera != null) foreach (TextMesh label in labels) label.transform.rotation = camera.transform.rotation;
            float[] ranges = lab.ShowSensors ? lab.Simulation.Sensors() : null;
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.back, Vector3.forward,
                new Vector3(1, 0, -1).normalized, new Vector3(-1, 0, -1).normalized,
                new Vector3(1, 0, 1).normalized, new Vector3(-1, 0, 1).normalized, Vector3.up, Vector3.down };
            for (int i = 0; i < sensorLines.Count; i++)
            {
                sensorLines[i].gameObject.SetActive(lab.ShowSensors);
                if (ranges == null) continue;
                sensorLines[i].SetPosition(0, position);
                sensorLines[i].SetPosition(1, position + directions[i] * ranges[i] * FlightSimulation.SensorRange);
            }
        }

        private void Pad(Transform root, NVector3 point, string letter, Color color)
        {
            Vector3 center = new Vector3(point.X, 0.025f, point.Z);
            Box(root, new Vector3(6, 0.025f, 6), center, Hex("25414e"));
            foreach (float side in new[] { -2.95f, 2.95f })
            {
                Box(root, new Vector3(0.1f, 0.025f, 6), center + new Vector3(side, 0.03f, 0), color);
                Box(root, new Vector3(6, 0.025f, 0.1f), center + new Vector3(0, 0.03f, side), color);
            }
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                GameObject segment = Box(root, new Vector3(0.09f, 0.025f, 0.32f), center + new Vector3(Mathf.Sin(angle) * 1.8f, 0.03f, Mathf.Cos(angle) * 1.8f), color);
                segment.transform.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg + 90, 0);
            }
            Label(root, letter + "  /  " + (letter == "A" ? "CẤT CÁNH" : "ĐIỂM ĐẾN"), center + Vector3.up * 2.6f, color, 0.3f);
        }

        private GameObject Box(Transform parent, Vector3 scale, Vector3 position, Color color)
        { return Primitive(parent, PrimitiveType.Cube, scale, position, color); }

        private GameObject Primitive(Transform parent, PrimitiveType type, Vector3 scale, Vector3 position, Color color)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            Collider collider = item.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            item.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            return item;
        }

        private Material MaterialFor(Color color)
        {
            Material material;
            if (materials.TryGetValue(color, out material)) return material;
            material = new Material(surface) { color = color };
            material.SetFloat("_Glossiness", 0.2f);
            materials.Add(color, material);
            return material;
        }

        private void Label(Transform root, string text, Vector3 position, Color color, float scale)
        {
            var item = new GameObject(text, typeof(TextMesh));
            item.transform.SetParent(root, false);
            item.transform.localPosition = position;
            TextMesh label = item.GetComponent<TextMesh>();
            label.font = font;
            label.fontSize = 72;
            label.characterSize = scale;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.text = text;
            label.color = color;
            item.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            labels.Add(label);
        }

        private LineRenderer CreateLine(Transform parent, Color color)
        {
            var item = new GameObject("Range sensor", typeof(LineRenderer));
            item.transform.SetParent(parent, false);
            var line = item.GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = 0.025f;
            line.sharedMaterial = MaterialFor(color);
            return line;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials.Values) if (material != null) Destroy(material);
        }

        public static Color Hex(string value)
        { Color color; ColorUtility.TryParseHtmlString("#" + value, out color); return color; }
    }
}
