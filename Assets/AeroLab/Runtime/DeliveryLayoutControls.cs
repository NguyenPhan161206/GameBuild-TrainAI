using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AeroLab.Core;
using UnityEngine;
using NVector3=System.Numerics.Vector3;
namespace AeroLab
{
    public sealed partial class DeliveryLab
    {
        [Serializable] private sealed class LayoutBox { public float X,Z,Width,Depth,Height; }
        [Serializable] private sealed class LayoutDocument { public int Version=1,LayoutSeed; public string Origin; public LayoutBox[] Buildings; }
        [SerializeField,HideInInspector] private LayoutDocument retainedLayout;
        [SerializeField,HideInInspector] private bool hasRetainedLayout;
        private ScenarioConfig interactiveScenario;
        private readonly List<Renderer> buildingVisuals=new List<Renderer>();
        private bool layoutEditorOpen;
        private int selectedBuilding;
        private string draftX="",draftZ="",draftHeight="",layoutMessage="";
        private Vector2 sidebarScroll,editorScroll;
        public bool InteractiveResetInProgress {get;private set;}
        public string LayoutMessage {get{return layoutMessage;}}
#if UNITY_EDITOR
        [HideInInspector] public bool AllowBatchInteractionForTests;
#endif
        public bool CanEditScenario
        {
            get
            {
                bool surface=!Application.isBatchMode;
#if UNITY_EDITOR
                surface|=AllowBatchInteractionForTests;
#endif
                return surface && EvaluationCount==0 && (Mode=="reference"||Mode=="flight-eval"||Mode=="dispatch-eval");
            }
        }
        private ScenarioConfig ScenarioForReset()
        {
            var generated=ScenarioConfig.Create(Seed,Noisy,Stage);
            if(!CanEditScenario)return generated;
            if(interactiveScenario!=null)return CampusLayout.Copy(interactiveScenario);
            return !hasRetainedLayout || retainedLayout==null || retainedLayout.Buildings==null?generated:FromDocument(generated,retainedLayout);
        }
        public bool RestartInteractive()
        {
            if(!CanEditScenario)return false;
            var config=CampusLayout.Copy(Simulation.Config);
            config.Sensors=Noisy?SensorConfig.Noisy():new SensorConfig();
            InstallInteractive(config);layoutMessage="Đã reset: giữ bố trí và đơn hàng, drone về kho, pin đầy.";return true;
        }
        public bool ShuffleInteractive()
        {
            if(!CanEditScenario)return false;
            try
            {
                int next=unchecked(Simulation.Config.LayoutSeed+1);
                InstallInteractive(CampusLayout.Randomize(Simulation.Config,next,Stage));
                LoadDraft();layoutMessage="Đã đổi bố trí; giữ nguyên luồng đơn hàng. Bấm Tiếp tục để thử.";return true;
            }
            catch(ArgumentException error){layoutMessage=error.Message;return false;}
        }
        public bool ApplyBuilding(int index,float x,float z,float height)
        {
            if(!CanEditScenario)return false;
            try
            {
                var config=CampusLayout.Edit(Simulation.Config,index,x,z,height);
                selectedBuilding=index;InstallInteractive(config);LoadDraft();
                layoutMessage="Đã áp dụng và reset. Bố trí tùy chỉnh chưa được nghiệm thu AI.";return true;
            }
            catch(ArgumentException error){layoutMessage=error.Message;return false;}
        }
        public bool RestoreGeneratedLayout()
        {
            if(!CanEditScenario)return false;
            InstallInteractive(ScenarioConfig.Create(Seed,Noisy,Stage));LoadDraft();
            layoutMessage="Đã khôi phục bố trí gốc của seed đơn hàng.";return true;
        }
        private void InstallInteractive(ScenarioConfig config)
        {
            CampusLayout.Validate(config);Paused=true;InteractiveResetInProgress=true;
            try
            {
                // EndEpisode clears stored actions/rewards. Both callbacks suppress automatic seed advance.
                if(DispatchAgent!=null)DispatchAgent.EndEpisode();
                if(FlightAgent!=null)FlightAgent.EndEpisode();
                interactiveScenario=CampusLayout.Copy(config);Seed=config.Seed;
                hasRetainedLayout=config.LayoutOrigin!="generated";
                retainedLayout=hasRetainedLayout?ToDocument(config):null;
                ResetEpisode();
                if(FlightAgent!=null)FlightAgent.RequestDecision();
            }
            finally{InteractiveResetInProgress=false;}
        }
        private static LayoutDocument ToDocument(ScenarioConfig config)
        {
            var doc=new LayoutDocument{LayoutSeed=config.LayoutSeed,Origin=config.LayoutOrigin,Buildings=new LayoutBox[config.Buildings.Length]};
            for(int i=0;i<doc.Buildings.Length;i++)
            {
                var b=config.Buildings[i];doc.Buildings[i]=new LayoutBox{X=b.Center.X,Z=b.Center.Z,Width=b.Half.X*2,Depth=b.Half.Z*2,Height=b.Half.Y*2};
            }
            return doc;
        }
        private static ScenarioConfig FromDocument(ScenarioConfig source,LayoutDocument doc)
        {
            if(doc==null || doc.Version!=1 || doc.Buildings==null || doc.Buildings.Length>64)throw new ArgumentException("File bố trí không hợp lệ hoặc khác phiên bản.");
            var config=CampusLayout.Copy(source);config.LayoutSeed=doc.LayoutSeed;config.LayoutOrigin="loaded";
            config.Buildings=new Building[doc.Buildings.Length];
            for(int i=0;i<doc.Buildings.Length;i++)
            {
                var b=doc.Buildings[i];if(b==null)throw new ArgumentException("Thiếu dữ liệu tòa nhà.");
                config.Buildings[i]=new Building(new NVector3(b.X,b.Height/2,b.Z),new NVector3(b.Width/2,b.Height/2,b.Depth/2));
            }
            CampusLayout.Validate(config);return config;
        }
        public string LayoutStoragePath="artifacts/layouts/campus.json";
        public string LayoutFilePath {get{return Path.GetFullPath(LayoutStoragePath);}}
        public bool SaveInteractiveLayout()
        {
            if(!CanEditScenario)return false;
            try
            {
                string path=LayoutFilePath;Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path+".tmp",JsonUtility.ToJson(ToDocument(Simulation.Config),true));
                if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
                layoutMessage="Đã lưu: "+path;return true;
            }
            catch(Exception error) when(error is IOException || error is UnauthorizedAccessException){layoutMessage="Không lưu được: "+error.Message;return false;}
        }
        public bool LoadInteractiveLayout()
        {
            if(!CanEditScenario)return false;
            try
            {
                var config=FromDocument(Simulation.Config,JsonUtility.FromJson<LayoutDocument>(File.ReadAllText(LayoutFilePath)));
                InstallInteractive(config);selectedBuilding=0;LoadDraft();layoutMessage="Đã nạp bố trí và reset lượt.";return true;
            }
            catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {layoutMessage="Không nạp được: "+error.Message;return false;}
        }
        private void ToggleLayoutEditor()
        {
            layoutEditorOpen=!layoutEditorOpen;
            if(layoutEditorOpen){Paused=true;LoadDraft();}HighlightBuilding();
        }
        private void LoadDraft()
        {
            if(Simulation.Config.Buildings.Length==0)return;
            selectedBuilding=Math.Max(0,Math.Min(selectedBuilding,Simulation.Config.Buildings.Length-1));
            var b=Simulation.Config.Buildings[selectedBuilding];draftX=b.Center.X.ToString("0.###",CultureInfo.InvariantCulture);
            draftZ=b.Center.Z.ToString("0.###",CultureInfo.InvariantCulture);draftHeight=(b.Half.Y*2).ToString("0.###",CultureInfo.InvariantCulture);
        }
        private void HighlightBuilding()
        {
            for(int i=0;i<buildingVisuals.Count;i++)if(buildingVisuals[i]!=null)
                buildingVisuals[i].sharedMaterial.color=layoutEditorOpen && i==selectedBuilding?new Color(.95f,.55f,.2f):new Color(.22f,.34f,.43f);
        }
        private static bool Number(string value,out float number)
        {return float.TryParse(value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out number);}
        private void NumberField(string label,ref string value,float min,float max)
        {
            GUILayout.Label(label,text);GUILayout.BeginHorizontal();
            float number;
            if(Number(value,out number) && !float.IsNaN(number) && !float.IsInfinity(number))
            {
                float initial=Mathf.Clamp(number,min,max);
                float selected=GUILayout.HorizontalSlider(initial,min,max,GUILayout.Width(210));
                if(selected!=initial)value=selected.ToString("0.###",CultureInfo.InvariantCulture);
            }
            value=GUILayout.TextField(value,GUILayout.Width(82));GUILayout.EndHorizontal();
        }
        private void DrawLayoutEditor()
        {
            if(!layoutEditorOpen || !CanEditScenario)return;
            float width=Mathf.Min(365,Screen.width-30);
            GUI.Box(new Rect(Screen.width-width-15,15,width,Screen.height-30),GUIContent.none);
            GUILayout.BeginArea(new Rect(Screen.width-width,28,width-30,Screen.height-55));editorScroll=GUILayout.BeginScrollView(editorScroll);
            GUILayout.Label("CHỈNH TÒA NHÀ",title);GUILayout.Label("Nền đất phẳng · X/Z và chiều cao tính bằng mét",text);
            int count=Simulation.Config.Buildings.Length;
            if(count==0)GUILayout.Label("Bố trí này không có tòa nhà. Chọn Stage 1–3 trước khi mở mô phỏng.",text);
            else
            {
                var names=new string[count];for(int i=0;i<count;i++)names[i]="Nhà "+(i+1);
                int selection=GUILayout.SelectionGrid(selectedBuilding,names,3);
                if(selection!=selectedBuilding){selectedBuilding=selection;LoadDraft();HighlightBuilding();}
                var half=Simulation.Config.Buildings[selectedBuilding].Half;
                NumberField("Vị trí X",ref draftX,-FlightSimulation.Limit+half.X,FlightSimulation.Limit-half.X);
                NumberField("Vị trí Z",ref draftZ,-FlightSimulation.Limit+half.Z,FlightSimulation.Limit-half.Z);
                NumberField("Chiều cao (0,5–30 m)",ref draftHeight,.5f,FlightSimulation.Ceiling);
                GUILayout.Label("Thay đổi chỉ có hiệu lực sau khi áp dụng. Drone sẽ về kho, pin và đơn hàng được reset.",text);
                if(GUILayout.Button("Áp dụng và reset",GUILayout.Height(32)))
                {
                    float x,z,h;
                    if(Number(draftX,out x)&&Number(draftZ,out z)&&Number(draftHeight,out h))ApplyBuilding(selectedBuilding,x,z,h);
                    else layoutMessage="Nhập tọa độ và chiều cao bằng số hợp lệ.";
                }
                if(GUILayout.Button("Hủy thay đổi đang nhập")){LoadDraft();layoutMessage="Đã bỏ phần nhập; bố trí hiện tại giữ nguyên.";}
            }
            if(GUILayout.Button("Khôi phục bố trí gốc"))RestoreGeneratedLayout();
            GUILayout.BeginHorizontal();if(GUILayout.Button("Lưu bố trí"))SaveInteractiveLayout();if(GUILayout.Button("Nạp bố trí"))LoadInteractiveLayout();GUILayout.EndHorizontal();
            GUILayout.Label("Model học với nhà dưới 24 m và lối tiếp cận trống. Bố trí tự chỉnh cần đánh giá lại.",text);
            if(!string.IsNullOrEmpty(layoutMessage))GUILayout.Label(layoutMessage,text);
            if(GUILayout.Button("Đóng bảng chỉnh sửa"))ToggleLayoutEditor();
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
