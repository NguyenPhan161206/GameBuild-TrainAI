using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AeroLab.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class Program
{
    private static int passed;
    private static int failed;
    private static void Main()
    {
        Check("Reset clears all episode state", () => {
            var s = New(); for (int i = 0; i < 40; i++) s.Step(Vector3.UnitY);
            s.Reset(); Assert(s.Position == s.Start && s.Velocity == Vector3.Zero && s.Ticks == 0 && s.TotalReward == 0 && !s.Running && s.Result == FlightResult.None && s.LandingProgress == 0);
        });
        Check("No movement before Begin", () => {
            var s = new FlightSimulation(); s.Step(Vector3.One); Assert(s.Ticks == 0 && s.Position == s.Start);
        });
        Check("Takeoff, hover braking, speed limits and diagonal normalization", () => {
            var s = New(false);
            for (int i = 0; i < 100; i++) s.Step(new Vector3(1, 1, 1));
            Assert(s.Position.Y > 1 && s.Velocity.Y <= 3.001f);
            Assert(new Vector2(s.Velocity.X, s.Velocity.Z).Length() <= 8.001f);
            for (int i = 0; i < 150; i++) s.Step(Vector3.Zero);
            Assert(s.Velocity.Length() < 0.001f && s.Result == FlightResult.None);
        });
        Check("Landing requires full hull, contact and one second of stability", () => {
            var s = New(false); s.Position = s.Goal + Vector3.UnitY;
            for (int i = 0; i < 60; i++) s.Step(Vector3.Zero);
            Assert(s.Result == FlightResult.None && s.LandingProgress == 0);
            s.Position = s.Goal + new Vector3(2.1f, 0, 0);
            for (int i = 0; i < 60; i++) s.Step(Vector3.Zero);
            Assert(s.Result == FlightResult.None);
            s.Position = s.Goal;
            for (int i = 0; i < 49; i++) s.Step(Vector3.Zero);
            Assert(s.Result == FlightResult.None);
            s.Step(Vector3.Zero); Assert(s.Result == FlightResult.Success);
        });
        Check("Leaving pad resets hold progress", () => {
            var s = New(false); s.Position = s.Goal;
            for (int i = 0; i < 30; i++) s.Step(Vector3.Zero);
            s.Position = s.Goal + new Vector3(4, 0, 0); s.Step(Vector3.Zero);
            s.Position = s.Goal;
            for (int i = 0; i < 30; i++) s.Step(Vector3.Zero);
            Assert(s.Result == FlightResult.None && s.LandingProgress < 0.7f);
        });
        Check("Soft landing outside B is not success", () => {
            var s = New(false); s.Position = s.Start + Vector3.UnitY * 0.001f;
            s.Velocity = new Vector3(0, -0.5f, 0); s.Step(Vector3.Zero);
            Assert(s.Grounded && s.Result == FlightResult.None);
        });
        Check("Hard touchdown fails using pre-contact speed", () => {
            var s = New(false); s.Position = s.Goal + Vector3.UnitY * 0.01f;
            s.Velocity = new Vector3(0, -2, 0); s.Step(Vector3.Zero);
            Assert(s.Result == FlightResult.Collision && s.Velocity.Y == 0);
        });
        Check("Building collision detects hull edges", () => {
            var s = New(); Building b = s.Buildings[0];
            s.Position = b.Center + new Vector3(-b.Half.X - 1.05f, 0, 0);
            s.Velocity = new Vector3(8, 0, 0); s.Step(Vector3.UnitX);
            Assert(s.Result == FlightResult.Collision);
        });
        Check("Swept collision prevents tunneling through a box", () => {
            float hit = FlightSimulation.RayBox(new Vector3(-10, 0, 0), Vector3.UnitX, -Vector3.One, Vector3.One, 20);
            Assert(Math.Abs(hit - 9) < 0.00001f);
        });
        Check("Bounds check full hull and ceiling", () => {
            var s = New(false); s.Position = new Vector3(38.99f, 5, 0); s.Velocity = new Vector3(1, 0, 0);
            s.Step(Vector3.UnitX); Assert(s.Result == FlightResult.OutOfBounds);
            s.Reset(0, false); s.Begin(); s.Position = new Vector3(0, 29.49f, 0); s.Velocity = Vector3.UnitY;
            s.Step(Vector3.UnitY); Assert(s.Result == FlightResult.OutOfBounds);
        });
        Check("Timeout at exactly 3000 fixed steps", () => {
            var s = New(false); for (int i = 0; i < 2999; i++) s.Step(Vector3.Zero);
            Assert(s.Result == FlightResult.None); s.Step(Vector3.Zero);
            Assert(s.Result == FlightResult.Timeout && s.Elapsed == 60);
        });
        Check("Collision has priority over timeout; success beats timeout", () => {
            var s = New(false); s.Ticks = 2999; s.Position = s.Goal + Vector3.UnitY * 0.01f; s.Velocity = -Vector3.UnitY * 2;
            s.Step(Vector3.Zero); Assert(s.Result == FlightResult.Collision);
            s.Reset(0, false); s.Begin(); s.Position = s.Goal;
            for (int i = 0; i < 49; i++) s.Step(Vector3.Zero);
            s.Ticks = 2999; s.Step(Vector3.Zero); Assert(s.Result == FlightResult.Success);
        });
        Check("Terminal episodes freeze position, time and reward", () => {
            var s = New(false); s.Ticks = 2999; s.Step(Vector3.Zero);
            Vector3 p = s.Position; float reward = s.TotalReward;
            for (int i = 0; i < 10; i++) Assert(s.Step(Vector3.One) == 0);
            Assert(s.Position == p && s.TotalReward == reward && s.Ticks == 3000);
        });
        Check("Sensors see ground, ceiling, boundaries and buildings", () => {
            var s = New(false); s.Position = new Vector3(0, 5, 0); float[] ranges = s.Sensors();
            Near(ranges[9], 4.5f / 30); Near(ranges[8], 24.5f / 30);
            s.Position = new Vector3(38, 5, 0); Near(s.Sensors()[0], 1f / 30);
            s.Reset(); s.Position = new Vector3(-12, 4, 0); Near(s.Sensors()[0], 4f / 30);
        });
        Check("Observation is finite and fixed at 20 floats", () => {
            var s = New(); foreach (float v in s.Observe()) Assert(!float.IsNaN(v) && !float.IsInfinity(v) && v >= -1 && v <= 1);
            Assert(s.Observe().Length == FlightSimulation.ObservationSize);
        });
        Check("Invalid actions are rejected without advancing state", () => {
            var s = New(); bool rejected = false;
            try { s.Step(new Vector3(float.NaN, 0, 0)); } catch (ArgumentException) { rejected = true; }
            Assert(rejected && s.Ticks == 0);
        });
        Check("Seeded layouts and actions replay deterministically", () => {
            var a = New(true, 42); var b = New(true, 42);
            Assert(a.Start == b.Start && a.Goal == b.Goal);
            for (int i = 0; i < 100; i++) { a.Step(Vector3.UnitY); b.Step(Vector3.UnitY); }
            Assert(a.Position == b.Position && a.Velocity == b.Velocity && a.TotalReward == b.TotalReward);
        });
        Check("Known-layout demo completes 100 seeds through the real simulation", () => {
            for (int seed = 0; seed < 100; seed++)
            {
                var s = New(true, seed); var pilot = new DemoPilot();
                while (s.Running) s.Step(pilot.Action(s));
                Assert(s.Result == FlightResult.Success, $"seed {seed}: {s.Result}, position {s.Position}, time {s.Elapsed}");
            }
        });
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        Check("All Unity C# files pass C# 9 syntax parsing (not Unity API compilation)", () => {
            string[] files = Directory.GetFiles(Path.Combine(root, "Assets"), "*.cs", SearchOption.AllDirectories);
            Assert(files.Length > 0, "No Unity source files found.");
            foreach (string file in files)
            {
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.CSharp9), path: file);
                var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                Assert(errors.Length == 0, string.Join("\n", errors.Select(d => d.ToString())));
            }
        });
        Check("Scene, build settings, script and asset GUIDs resolve", () => {
            var known = new HashSet<string>();
            foreach (string meta in Directory.GetFiles(Path.Combine(root, "Assets"), "*.meta", SearchOption.AllDirectories))
            {
                string guid = Regex.Match(File.ReadAllText(meta), @"^guid: ([a-f0-9]{32})$", RegexOptions.Multiline).Groups[1].Value;
                Assert(guid.Length == 32 && known.Add(guid), "Missing/duplicate GUID: " + meta);
                Assert(File.Exists(meta.Substring(0, meta.Length - 5)) || Directory.Exists(meta.Substring(0, meta.Length - 5)), "Orphan meta: " + meta);
            }
            string scene = File.ReadAllText(Path.Combine(root, "Assets/AeroLab/Scenes/FlightLab.unity"));
            foreach (Match match in Regex.Matches(scene, @"guid: ([a-f0-9]{32})")) Assert(known.Contains(match.Groups[1].Value));
            string scriptMeta = File.ReadAllText(Path.Combine(root, "Assets/AeroLab/Runtime/FlightLab.cs.meta"));
            Assert(scriptMeta.Contains("2bb1a7bb68654d6ca3b286de9425c733") && scene.Contains("2bb1a7bb68654d6ca3b286de9425c733"));
            string sceneMeta = File.ReadAllText(Path.Combine(root, "Assets/AeroLab/Scenes/FlightLab.unity.meta"));
            Assert(sceneMeta.Contains("a4c0bfd5ddab4ade81a187e8d537461c"));
            Assert(File.ReadAllText(Path.Combine(root, "ProjectSettings/EditorBuildSettings.asset")).Contains("a4c0bfd5ddab4ade81a187e8d537461c"));
            foreach (string asm in Directory.GetFiles(Path.Combine(root, "Assets"), "*.asmdef", SearchOption.AllDirectories))
                using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(asm))) Assert(document.RootElement.GetProperty("name").GetString().StartsWith("AeroLab."));
        });
        Console.WriteLine($"\n{passed} passed, {failed} failed. Demo results are scripted-controller checks, NOT trained-AI evaluation.");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }

    private static FlightSimulation New(bool obstacles = true, int seed = 0)
    { var s = new FlightSimulation(); s.Reset(seed, obstacles); s.Begin(); return s; }
    private static void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    private static void Near(float actual, float expected) { Assert(Math.Abs(actual - expected) < 0.0001f, $"{actual} != {expected}"); }
    private static void Check(string title, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + title); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + title + ": " + e.Message); }
    }
}
