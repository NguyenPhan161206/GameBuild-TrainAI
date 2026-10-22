using AeroLab.Core;
using UnityEngine;

namespace AeroLab
{
    /// <summary>Self-contained HUD; no Canvas prefab, UI package or external asset store needed.</summary>
    public sealed class FlightHud : MonoBehaviour
    {
        private FlightLab lab;
        private Font font;
        private GUIStyle text;
        private GUIStyle button;
        private GUIStyle toggle;
        private readonly Color ink = FlightWorld.Hex("0b1926");
        private readonly Color mint = FlightWorld.Hex("57e4c2");
        private readonly Color white = FlightWorld.Hex("e5f1f6");
        private readonly Color muted = FlightWorld.Hex("8ba6b8");
        private readonly Color border = FlightWorld.Hex("284858");
        private Texture2D buttonNormal;
        private Texture2D buttonHover;
        private Texture2D buttonActive;
        private bool help;

        public void Initialize(FlightLab owner)
        {
            lab = owner;
            font = Resources.Load<Font>("Fonts/DejaVuSans");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void EnsureStyles()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { font = font, richText = false, padding = new RectOffset(0, 0, 0, 0) };
            buttonNormal = Solid(FlightWorld.Hex("173240"));
            buttonHover = Solid(FlightWorld.Hex("28515d"));
            buttonActive = Solid(FlightWorld.Hex("326a70"));
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 13, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 0, 0) };
            button.normal.background = buttonNormal; button.normal.textColor = white;
            button.hover.background = buttonHover; button.hover.textColor = mint;
            button.active.background = buttonActive; button.active.textColor = white;
            button.focused.background = buttonNormal; button.focused.textColor = white;
            toggle = new GUIStyle(GUI.skin.toggle) { font = font, fontSize = 13 };
            toggle.normal.textColor = muted;
            toggle.onNormal.textColor = mint;
            toggle.hover.textColor = mint;
            toggle.onHover.textColor = mint;
        }

        private void OnGUI()
        {
            if (lab == null || lab.Simulation == null) return;
            EnsureStyles();
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            Header();
            Mission();
            Map();
            Settings();
            Telemetry();
            Controls();
            // Modal overlays are last; their input-blocking window captures clicks.
            if (lab.ShowWelcome || lab.Simulation.Result != FlightResult.None) Modal();
            else if (lab.Paused) PauseModal();
            if (help) HelpModal();
            GUI.matrix = previous;
        }

        private void Header()
        {
            Panel(new Rect(24, 22, 1392, 72));
            Label(new Rect(44, 31, 48, 48), "◈", 36, mint);
            Label(new Rect(98, 32, 310, 32), "A E R O L A B", 24, white);
            Label(new Rect(99, 65, 350, 17), "SÂN THỬ NGHIỆM XE BAY TỰ HÀNH", 10, muted);
            Label(new Rect(820, 46, 310, 24), lab.Demo ? "●  BAY MẪU THEO LUẬT" : "●  ĐIỀU KHIỂN TAY", 13, mint);
            Label(new Rect(1160, 48, 230, 22), "UNITY  /  V0.1", 12, muted);
        }

        private void Mission()
        {
            FlightSimulation s = lab.Simulation;
            Panel(new Rect(24, 112, 255, 297));
            Label(new Rect(43, 131, 220, 20), "01  /  NHIỆM VỤ", 11, mint);
            Label(new Rect(43, 161, 220, 30), "Chuyến bay đầu tiên", 19, white);
            Label(new Rect(43, 201, 220, 71), "Từ bãi A đến bãi B.\nTránh tòa nhà và hạ cánh\nnhẹ trong ô đích.", 14, muted);
            Fill(new Rect(43, 282, 216, 1), border);
            string stage = s.LandingProgress > 0 ? "◉ Giữ ổn định trên bãi B" : s.Position.Y > 1.5f ? "◉ Tiếp cận bãi B" : "○ Cất cánh từ bãi A";
            Label(new Rect(43, 297, 220, 24), stage, 13, white);
            Label(new Rect(43, 332, 220, 18), "HẠ CÁNH · GIỮ ỔN ĐỊNH 1 GIÂY", 9, muted);
            Fill(new Rect(43, 358, 216, 5), border);
            Fill(new Rect(43, 358, 216 * s.LandingProgress, 5), mint);
            string status = s.Result != FlightResult.None ? ResultName(s.Result) : lab.Paused ? "ĐÃ TẠM DỪNG" : s.Running ? "ĐANG BAY · TRẦN CAO 30 M" : "SẴN SÀNG";
            Label(new Rect(43, 379, 220, 18), status, 10, s.Result == FlightResult.None || s.Result == FlightResult.Success ? mint : FlightWorld.Hex("f5b182"));
        }

        private void Map()
        {
            FlightSimulation s = lab.Simulation;
            Panel(new Rect(1196, 112, 220, 296));
            Label(new Rect(1214, 131, 190, 20), "BẢN ĐỒ  /  80 × 80 M", 11, muted);
            var area = new Rect(1214, 163, 184, 184);
            Fill(area, FlightWorld.Hex("0a1925"));
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                Fill(new Rect(area.x + area.width * t, area.y, 1, area.height), FlightWorld.Hex("183240"));
                Fill(new Rect(area.x, area.y + area.height * t, area.width, 1), FlightWorld.Hex("183240"));
            }
            foreach (Building b in s.Buildings)
            {
                Vector2 lower = MapPoint(b.Center - b.Half, area), upper = MapPoint(b.Center + b.Half, area);
                Fill(new Rect(lower.x, lower.y, upper.x - lower.x, upper.y - lower.y), FlightWorld.Hex("446071"));
            }
            Vector2 a = MapPoint(s.Start, area), goal = MapPoint(s.Goal, area), p = MapPoint(s.Position, area);
            Line(a, goal, FlightWorld.Hex("2d5663"), 1);
            Outline(new Rect(a.x - 4, a.y - 4, 8, 8), muted);
            Outline(new Rect(goal.x - 5, goal.y - 5, 10, 10), mint);
            Fill(new Rect(p.x - 3, p.y - 3, 6, 6), FlightWorld.Hex("f8d089"));
            Outline(area, border);
            Label(new Rect(1214, 358, 194, 18), "□ A  Xuất phát    □ B  Đích", 10, muted);
            Label(new Rect(1214, 382, 190, 18), string.Format("X {0:+0.0;-0.0;0.0}     Z {1:+0.0;-0.0;0.0}", s.Position.X, s.Position.Z), 10, mint);
        }

        private void Settings()
        {
            Panel(new Rect(24, 584, 255, 186));
            Label(new Rect(43, 603, 220, 20), "THIẾT LẬP SÂN THỬ", 10, muted);
            // These controls intentionally remain available on the welcome screen.
            GUI.enabled = !help;
            bool obstacles = GUI.Toggle(new Rect(43, 634, 220, 23), lab.Obstacles, "Có chướng ngại vật", toggle);
            if (obstacles != lab.Obstacles) lab.SetObstacles(obstacles);
            lab.ShowRoute = GUI.Toggle(new Rect(43, 664, 220, 23), lab.ShowRoute, "Hiện đường bay tham khảo", toggle);
            lab.ShowSensors = GUI.Toggle(new Rect(43, 694, 220, 23), lab.ShowSensors, "Hiện cảm biến khoảng cách", toggle);
            Label(new Rect(43, 736, 115, 20), "SEED  " + lab.Seed.ToString("0000"), 11, mint);
            if (Button(new Rect(166, 726, 94, 31), "Đổi sân")) lab.NextLayout();
            GUI.enabled = true;
        }

        private void Telemetry()
        {
            FlightSimulation s = lab.Simulation;
            Panel(new Rect(298, 788, 1118, 88));
            Metric(320, "ĐỘ CAO", Mathf.Max(0, s.Position.Y - 0.5f).ToString("0.0"), "m");
            Metric(535, "TỐC ĐỘ", s.Velocity.Length().ToString("0.0"), "m/s");
            Metric(750, "CÁCH ĐÍCH", System.Numerics.Vector3.Distance(s.Position, s.Goal).ToString("0.0"), "m");
            Metric(965, "CÒN LẠI", (60 - s.Elapsed).ToString("0.0"), "s");
            Metric(1180, "PHẦN THƯỞNG", s.TotalReward.ToString("0.00"), "");
            Panel(new Rect(24, 788, 255, 88));
            Label(new Rect(43, 806, 220, 19), "MÔ PHỎNG BƯỚC CỐ ĐỊNH", 9, muted);
            Label(new Rect(43, 833, 220, 23), "50 Hz  /  X · Y · Z", 15, mint);
        }

        private void Controls()
        {
            GUI.enabled = !Blocking;
            if (Button(new Rect(298, 112, 124, 39), lab.Demo && lab.Simulation.Running ? "Lái tay" : "▷  Bay mẫu"))
            { if (lab.Demo && lab.Simulation.Running) lab.TakeControl(); else lab.StartDemo(); }
            GUI.enabled = !Blocking && lab.Simulation.Running;
            if (Button(new Rect(430, 112, 124, 39), lab.Paused ? "▷  Tiếp tục" : "Ⅱ  Tạm dừng")) lab.TogglePause();
            GUI.enabled = !Blocking;
            if (Button(new Rect(562, 112, 114, 39), "↻  Chơi lại")) lab.ResetFlight();
            if (Button(new Rect(684, 112, 142, 39), "Camera [C]")) lab.ToggleCamera();
            if (Button(new Rect(834, 112, 108, 39), "Trợ giúp"))
            {
                if (lab.Simulation.Running && !lab.Paused) lab.TogglePause();
                help = true;
            }
            GUI.enabled = true;
            Panel(new Rect(298, 726, 1118, 44));
            Label(new Rect(316, 738, 1090, 24), "W A S D   Di chuyển     SPACE / CTRL   Lên / xuống     R   Chơi lại     ESC   Tạm dừng     CHUỘT PHẢI   Xoay · Cuộn để zoom", 11, muted);
            if (lab.Demo && lab.Simulation.Running)
            {
                Panel(new Rect(460, 673, 550, 36));
                Label(new Rect(477, 683, 520, 20), "BAY MẪU THEO LUẬT  ·  Chưa phải AI đã huấn luyện", 11, mint);
            }
        }

        private bool Blocking { get { return help || lab.ShowWelcome || lab.Paused || lab.Simulation.Result != FlightResult.None; } }

        private void Modal()
        {
            // A modal captures clicks while the HUD remains visible behind it.
            bool result = lab.Simulation.Result != FlightResult.None;
            Fill(new Rect(284, 166, 896, 478), new Color(0.025f, 0.05f, 0.08f, 0.35f));
            Panel(new Rect(456, 305, 528, 292));
            Label(new Rect(480, 329, 480, 20), "A E R O L A B   /   THỬ NGHIỆM 01", 11, mint);
            string title = !result ? "Sẵn sàng cất cánh?" : lab.Simulation.Result == FlightResult.Success ? "Hạ cánh thành công!" : "Thử lại chuyến bay";
            Label(new Rect(480, 369, 480, 40), title, 27, white);
            string description = !result
                ? "Giữ Space để lên cao, dùng WASD để bay.\nHạ xuống bãi B thật nhẹ bằng Ctrl.\nThả phím để hãm và giữ độ cao."
                : Reason(lab.Simulation.Result) + "\nThời gian: " + lab.Simulation.Elapsed.ToString("0.0") + " s · Phần thưởng: " + lab.Simulation.TotalReward.ToString("0.00") + "\n" + (lab.Demo ? "Bay mẫu theo luật — chưa phải AI huấn luyện." : "Bấm bay lại để bắt đầu lượt mới.");
            Label(new Rect(480, 427, 480, 80), description, 14, muted);
            if (Button(new Rect(480, 530, 230, 43), result ? "Bay lại bằng tay  →" : "Bắt đầu bay  →")) lab.StartManual();
            if (Button(new Rect(722, 530, 238, 43), "Xem bay mẫu")) lab.StartDemo();
            Label(new Rect(489, 612, 480, 20), "ENTER để bắt đầu  ·  Xe có hỗ trợ giữ độ cao", 11, muted);
        }

        private void PauseModal()
        {
            Panel(new Rect(500, 337, 440, 212));
            Label(new Rect(524, 362, 390, 38), "Chuyến bay tạm dừng", 24, white);
            Label(new Rect(524, 414, 390, 48), "Đồng hồ và mô phỏng đang dừng.\nTiếp tục khi bạn sẵn sàng.", 14, muted);
            if (Button(new Rect(524, 485, 186, 42), "Tiếp tục  [ESC]")) lab.TogglePause();
            if (Button(new Rect(722, 485, 194, 42), "Chơi lại")) lab.ResetFlight();
        }

        private void HelpModal()
        {
            Panel(new Rect(408, 252, 624, 398));
            Label(new Rect(432, 278, 575, 38), "Điều khiển chuyến bay", 26, white);
            Label(new Rect(432, 337, 575, 220), "W / S: theo trục Z − / +     A / D: theo trục X − / +\nSpace: lên cao     Ctrl: hạ xuống\nThả phím: hãm vận tốc, sau đó giữ độ cao\nC: đổi camera     Chuột phải: xoay     Cuộn: zoom\nR: về màn bắt đầu     Esc: tạm dừng\n\nHạ cánh: nằm trọn trong bãi B, tốc độ ≤ 0,5 m/s\nvà giữ ổn định đủ 1 giây. Tiếp đất > 1 m/s sẽ hỏng.\nĐổi sân hoặc bật/tắt vật cản sẽ đặt lại lượt bay.", 14, muted);
            if (Button(new Rect(432, 583, 575, 42), "Đã hiểu")) help = false;
        }

        private void Metric(float x, string title, string value, string unit)
        {
            Label(new Rect(x, 804, 198, 18), title, 10, muted);
            Label(new Rect(x, 827, 148, 37), value, 27, white);
            Label(new Rect(x + 151, 842, 45, 20), unit, 12, muted);
        }

        private void Panel(Rect area) { Fill(area, ink); Outline(area, border); }
        private void Label(Rect area, string value, int size, Color color)
        {
            text.fontSize = size; text.normal.textColor = color;
            GUI.Label(area, value, text);
        }
        private bool Button(Rect area, string value)
        {
            Outline(area, border);
            return GUI.Button(new Rect(area.x + 1, area.y + 1, area.width - 2, area.height - 2), value, button);
        }
        private static void Fill(Rect area, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = previous;
        }
        private static void Outline(Rect area, Color color)
        {
            Fill(new Rect(area.x, area.y, area.width, 1), color); Fill(new Rect(area.x, area.yMax - 1, area.width, 1), color);
            Fill(new Rect(area.x, area.y, 1, area.height), color); Fill(new Rect(area.xMax - 1, area.y, 1, area.height), color);
        }
        private static void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            Fill(new Rect(a.x, a.y, (b - a).magnitude, width), color);
            GUI.matrix = previous;
        }
        private static Vector2 MapPoint(System.Numerics.Vector3 p, Rect area)
        { return new Vector2(area.x + (p.X + 40) / 80 * area.width, area.y + (p.Z + 40) / 80 * area.height); }
        private static Texture2D Solid(Color color)
        { var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture; }
        private static string ResultName(FlightResult result)
        {
            switch (result) { case FlightResult.Success: return "HOÀN THÀNH"; case FlightResult.Collision: return "VA CHẠM"; case FlightResult.OutOfBounds: return "VƯỢT GIỚI HẠN"; default: return "HẾT GIỜ"; }
        }
        private static string Reason(FlightResult result)
        {
            switch (result)
            {
                case FlightResult.Success: return "Xe đã hạ cánh an toàn và giữ ổn định tại B.";
                case FlightResult.Collision: return "Xe đã va chạm hoặc tiếp đất quá mạnh.";
                case FlightResult.OutOfBounds: return "Xe đã vượt giới hạn sân thử nghiệm.";
                default: return "Đã hết thời gian của lượt bay.";
            }
        }
        private void OnDestroy()
        {
            if (buttonNormal != null) Destroy(buttonNormal);
            if (buttonHover != null) Destroy(buttonHover);
            if (buttonActive != null) Destroy(buttonActive);
        }
    }
}
