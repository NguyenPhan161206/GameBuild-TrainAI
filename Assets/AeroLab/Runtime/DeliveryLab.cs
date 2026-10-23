using System;
using System.IO;
using System.Linq;
using AeroLab.Core;
using UnityEngine;
using NVector3=System.Numerics.Vector3;
namespace AeroLab
{
    public sealed class DeliveryLab : MonoBehaviour
    {
        public DeliverySimulation Simulation {get;private set;}
        public bool Paused {get;private set;} = true;
        public bool ShowSensors=true, Noisy;
        public int Seed, Stage=3;
        public DispatchRule Rule=DispatchRule.EarliestDeadline;
        public RoutePilot Pilot {get;private set;} = new RoutePilot();
        public string Mode="reference", FlightModel="reference-route", DispatchModel="EarliestDeadline";
        public DeliveryFlightAgent FlightAgent {get;private set;}
        public DeliveryDispatchAgent DispatchAgent {get;private set;}
        public int EpisodeCount {get;private set;}
        public EpisodeReport LastReport {get;private set;}
        public void Resume(){Paused=false;}
        public int EvaluationCount;
        public string ReportDirectory="artifacts/delivery-runs";
        private NVector3 previousGoal=new NVector3(float.NaN);
        private GameObject world;
        private Transform drone;
        private GameObject parcel;
        private readonly System.Collections.Generic.List<Transform> rotors=new System.Collections.Generic.List<Transform>();
        private Camera view;
        private Material surface;
        private readonly System.Collections.Generic.List<Material> materials=new System.Collections.Generic.List<Material>();
        private LineRenderer routeLine, trackLine;
        private readonly System.Collections.Generic.List<Vector3> track=new System.Collections.Generic.List<Vector3>();
        private LineRenderer[] rays;
        private bool recorded;
        private string capturePath;
        [Serializable] private sealed class Provenance { public string sha256=""; public string sourceSha256=""; public bool qualified; }
        private string qualificationLabel="Ứng viên; chưa nghiệm thu vận hành nhiều chuyến";
        private int captureFrame;
        private string traceDirectory;
        private StreamWriter traceWriter;
        private Font font;
        private GUIStyle text, title;
        private Vector2 orderScroll;
        private void OnEnable()
        {
            if(Simulation!=null)return;
            ResetEpisode();
            if(!Application.isBatchMode && view==null)BuildWorld();
            Paused=!Application.isBatchMode;
        }
        private void Awake()
        {
            var args=Environment.GetCommandLineArgs();
            capturePath=Arg(args,"--capture","");
            traceDirectory=Arg(args,"--trace-directory","");
            Mode=Arg(args,"--delivery-mode",Mode);Seed=int.Parse(Arg(args,"--scenario-seed",Seed.ToString()));Stage=int.Parse(Arg(args,"--stage",Stage.ToString()));
            Noisy=bool.Parse(Arg(args,"--noisy",Noisy.ToString()));EvaluationCount=int.Parse(Arg(args,"--episodes",EvaluationCount.ToString()));
            ReportDirectory=Arg(args,"--reports",ReportDirectory);Rule=Arg(args,"--rule",Rule==DispatchRule.Fifo?"fifo":"edd")=="fifo"?DispatchRule.Fifo:DispatchRule.EarliestDeadline;if(Mode=="reference")DispatchModel=Rule.ToString();
            if(Array.IndexOf(args,"--reports")<0)
                ReportDirectory=Path.Combine(ReportDirectory,"session-"+Guid.NewGuid().ToString("N"));
            Time.fixedDeltaTime=FlightSimulation.Dt;
            if(Application.isBatchMode && Mode!="flight" && Mode!="dispatch")
            {
                float speed=float.Parse(Arg(args,"--evaluation-time-scale","30"),System.Globalization.CultureInfo.InvariantCulture);
                if(float.IsNaN(speed) || speed<1 || speed>1000)throw new ArgumentOutOfRangeException("evaluation-time-scale");
                Time.timeScale=speed;
            }
            Time.maximumDeltaTime=.1f;
            Application.runInBackground=true;
            ResetEpisode();
            if(!Application.isBatchMode || capturePath!="") BuildWorld();
            if(Mode!="reference") SetupAgents(args);
            if(Application.isBatchMode || capturePath!="") Paused=false;
        }
        public static string Arg(string[] args,string key,string fallback)
        {int i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length?args[i+1]:fallback;}
        private void SetupAgents(string[] args)
        {
            if((Mode=="flight" || Mode=="dispatch") && !Unity.MLAgents.Academy.Instance.IsCommunicatorOn)
            {Debug.LogError("Python trainer connection failed; no heuristic fallback allowed.");Paused=true;enabled=false;Application.Quit(3);return;}
            string selectedFlight=Arg(args,"--flight-model",FlightModel=="reference-route"?"":FlightModel);
            string selectedDispatch=Arg(args,"--dispatch-model",DispatchModel=="EarliestDeadline" || DispatchModel=="Fifo"?"":DispatchModel);
            var flightObject=new GameObject("Flight policy");flightObject.SetActive(false);flightObject.transform.SetParent(transform);
            DeliveryFlightAgent.Configure(flightObject,this,selectedFlight);
            FlightAgent=flightObject.GetComponent<DeliveryFlightAgent>();flightObject.SetActive(true);
            if(Mode=="dispatch" || Mode=="dispatch-eval")
            {
                var dispatchObject=new GameObject("Dispatch policy");dispatchObject.SetActive(false);dispatchObject.transform.SetParent(transform);
                DeliveryDispatchAgent.Configure(dispatchObject,this,selectedDispatch);
                DispatchAgent=dispatchObject.GetComponent<DeliveryDispatchAgent>();dispatchObject.SetActive(true);
            }
            FlightModel=selectedFlight;if(FlightModel=="")FlightModel=Mode=="flight-demo"?"reference-demonstration":"training-flight";
            var evidence=Resources.Load<TextAsset>(FlightModel+".provenance");
            if(evidence!=null && JsonUtility.FromJson<Provenance>(evidence.text).qualified)
                qualificationLabel="Đã kiểm chứng một chuyến; nhiều chuyến đánh giá riêng";
            DispatchModel=DispatchAgent!=null?(selectedDispatch==""?"training-dispatch":selectedDispatch):Rule.ToString();
        }
        public void NextScenario()
        {
            Seed = Mode=="flight" || Mode=="flight-demo" || Mode=="dispatch" ? (Seed+1)%8000 : Seed+1;
            ResetEpisode();
        }
        public void ResetEpisode()
        {
            if(traceWriter!=null){traceWriter.Dispose();traceWriter=null;}
            Simulation=new DeliverySimulation(ScenarioConfig.Create(Seed,Noisy,Stage));Pilot.Reset(Simulation.Flight);previousGoal=Simulation.Flight.Goal;recorded=false;
            if(!string.IsNullOrEmpty(traceDirectory))
            {
                Directory.CreateDirectory(traceDirectory);
                traceWriter=new StreamWriter(Path.Combine(traceDirectory,$"trace-{Seed}-{EpisodeCount+1}-{Guid.NewGuid():N}.jsonl"),false);
                Simulation.TraceSink=frame=>traceWriter.WriteLine(DeliveryReportJson.Serialize(frame));
            }
            if(world!=null && view!=null)RebuildWorld();
        }
        public NVector3 ReferenceAction()
        {
            if(Simulation.Flight.Goal!=previousGoal){Pilot.Reset(Simulation.Flight);previousGoal=Simulation.Flight.Goal;}
            return Pilot.Action(Simulation.Flight);
        }
        private void FixedUpdate()
        {
            if(Paused || !Simulation.Running)return;
            if(FlightAgent==null)
            {
                if(Simulation.AtDepot && Simulation.Tick%50==0)Simulation.Dispatch(new DispatchAction(Simulation.Choose(Rule)));
                Advance(ReferenceAction());
            }
        }
        public float Advance(NVector3 action)
        {
            if(Paused || !Simulation.Running)return 0;
            float reward=Simulation.Step(new FlightAction(action));
            if(!Simulation.Running && !recorded)
            {
                Directory.CreateDirectory(ReportDirectory);
                var report=Simulation.Report(FlightModel,DispatchModel);
                report.FlightModelSha256=ModelHash(FlightModel);report.DispatchModelSha256=ModelHash(DispatchModel);
                #if UNITY_EDITOR
                report.SourceSha256="editor-unbuilt";
#else
                var info=Resources.Load<TextAsset>("DeliveryBuildInfo");if(info!=null)report.SourceSha256=JsonUtility.FromJson<Provenance>(info.text).sourceSha256;
#endif
                // Unity JsonUtility cannot serialize System.Numerics.Vector3/readonly fields reliably.
                string reportPath=Path.Combine(ReportDirectory,$"episode-{Seed}-{EpisodeCount+1}.json");
                File.WriteAllText(reportPath+".tmp",DeliveryReportJson.Serialize(report));File.Move(reportPath+".tmp",reportPath);
                recorded=true;EpisodeCount++;LastReport=report;
                if(traceWriter!=null)traceWriter.Flush();
                if(EvaluationCount>0 && EpisodeCount>=EvaluationCount){Debug.Log("AEROLAB_DELIVERY_EVALUATION_COMPLETE");Application.Quit(0);}
                else if(Application.isBatchMode && Mode=="reference"){Seed++;ResetEpisode();}
            }
            return reward;
        }
        private static string ModelHash(string resource)
        {var data=Resources.Load<TextAsset>(resource+".provenance");return data==null?"":JsonUtility.FromJson<Provenance>(data.text).sha256;}
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape))Paused=!Paused;
            if(Input.GetKeyDown(KeyCode.R) && FlightAgent==null){ResetEpisode();}
            if(Input.GetKeyDown(KeyCode.Space))Paused=false;
        }
        private void LateUpdate()
        {
            if(capturePath!="" && Time.frameCount>120 && captureFrame==0)
            {Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(capturePath)));ScreenCapture.CaptureScreenshot(capturePath);captureFrame=Time.frameCount;}
            if(captureFrame>0 && Time.frameCount>captureFrame+30 && File.Exists(capturePath))Application.Quit(0);
            if(drone==null)return;
            drone.position=UnityVector(Simulation.Flight.Position);
            if(track.Count==0 || Vector3.Distance(track[track.Count-1],drone.position)>.5f)
            {
                track.Add(drone.position);trackLine.positionCount=track.Count;trackLine.SetPosition(track.Count-1,drone.position);
            }
            parcel.SetActive(Simulation.Payload>0);
            foreach(var rotor in rotors)rotor.Rotate(0,Time.deltaTime*(Simulation.Flight.Grounded?120:1800),0);
            if(previousGoal!=Simulation.Flight.Goal){Pilot.Reset(Simulation.Flight);previousGoal=Simulation.Flight.Goal;}
            var route=Pilot.Route;
            routeLine.positionCount=route==null?0:route.Count+1;
            if(route!=null){routeLine.SetPosition(0,UnityVector(Simulation.Flight.Start));for(int i=0;i<route.Count;i++)routeLine.SetPosition(i+1,UnityVector(route[i]));}
            float[] values=Simulation.ObserveFlight().Values;
            Vector3[] dirs={Vector3.right,Vector3.left,Vector3.back,Vector3.forward,new Vector3(1,0,-1).normalized,new Vector3(-1,0,-1).normalized,new Vector3(1,0,1).normalized,new Vector3(-1,0,1).normalized,Vector3.up,Vector3.down};
            for(int i=0;i<10;i++){rays[i].gameObject.SetActive(ShowSensors && values[i+8]>=0);rays[i].SetPosition(0,drone.position);rays[i].SetPosition(1,drone.position+dirs[i]*Math.Max(0,values[i+8])*30);}
        }
        private void BuildWorld()
        {
            // Owned children survive a domain reload even when managed references do not.
            for(int i=transform.childCount-1;i>=0;i--)
            {
                var child=transform.GetChild(i).gameObject;
                if(child.name=="Delivery campus" || child.name=="Delivery spectator camera" || child.name=="Delivery sun")
                {child.SetActive(false);Destroy(child);}
            }
            surface=Resources.Load<Material>("FlightSurface");
            font=Resources.Load<Font>("Fonts/DejaVuSans");if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var cameraObject=new GameObject("Delivery spectator camera",typeof(Camera));cameraObject.transform.SetParent(transform);view=cameraObject.GetComponent<Camera>();view.transform.position=new Vector3(83,95,-100);view.transform.LookAt(new Vector3(0,0,0));view.fieldOfView=47;view.backgroundColor=new Color(.035f,.07f,.12f);view.clearFlags=CameraClearFlags.SolidColor;
            var lightObject=new GameObject("Delivery sun",typeof(Light));lightObject.transform.SetParent(transform);lightObject.transform.rotation=Quaternion.Euler(55,-30,0);lightObject.GetComponent<Light>().type=LightType.Directional;lightObject.GetComponent<Light>().intensity=1.2f;
            RenderSettings.ambientLight=new Color(.6f,.67f,.75f);
            RebuildWorld();
        }
        private void RebuildWorld()
        {
            if(Application.isBatchMode && capturePath=="")return;
            if(world!=null){world.SetActive(false);Destroy(world);}foreach(var material in materials)Destroy(material);materials.Clear();
            world=new GameObject("Delivery campus");world.transform.SetParent(transform);
            Box(new Vector3(80,.2f,80),new Vector3(0,-.15f,0),new Color(.09f,.17f,.22f));
            foreach(var b in Simulation.Config.Buildings)Box(UnityVector(b.Half)*2,UnityVector(b.Center),new Color(.22f,.34f,.43f));
            Pad(Simulation.Config.Depot,new Color(.15f,.8f,.7f),"KHO / SẠC");for(int i=0;i<Simulation.Config.Pads.Length;i++)Pad(Simulation.Config.Pads[i],new Color(.95f,.66f,.23f),"GIAO "+(i+1));
            rotors.Clear();
            drone=Box(new Vector3(1,.45f,1),UnityVector(Simulation.Flight.Position),Color.white).transform;
            parcel=Box(new Vector3(.7f,.35f,.7f),Vector3.zero,new Color(.8f,.5f,.15f));parcel.transform.SetParent(drone,false);parcel.transform.localPosition=Vector3.down*.4f;
            foreach(float x in new[]{-.7f,.7f})foreach(float z in new[]{-.7f,.7f})
            {
                var arm=Box(new Vector3(.1f,.08f,1.8f),Vector3.zero,new Color(.4f,.5f,.6f));arm.transform.SetParent(drone,false);arm.transform.localPosition=new Vector3(x,0,0);
                var rotor=Box(new Vector3(.65f,.035f,.08f),Vector3.zero,new Color(.2f,.95f,.75f));rotor.transform.SetParent(drone,false);rotor.transform.localPosition=new Vector3(x,.15f,z);rotors.Add(rotor.transform);
            }
            track.Clear();trackLine=Line(Color.white,.14f);trackLine.positionCount=0;
            routeLine=Line(new Color(.2f,.9f,.75f),.1f);rays=new LineRenderer[10];for(int i=0;i<10;i++)rays[i]=Line(Color.yellow,.035f);
        }
        private GameObject Box(Vector3 scale,Vector3 position,Color color)
        {
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.transform.SetParent(world.transform);item.transform.position=position;item.transform.localScale=scale;
            item.GetComponent<Collider>().enabled=false;Destroy(item.GetComponent<Collider>());
            var material=new Material(surface!=null?surface:new Material(Shader.Find("Standard")));material.color=color;materials.Add(material);item.GetComponent<Renderer>().sharedMaterial=material;return item;
        }
        private void Pad(NVector3 p,Color color,string name)
        {
            Box(new Vector3(6,.08f,6),new Vector3(p.X,.03f,p.Z),color);
            var item=new GameObject(name,typeof(TextMesh));item.transform.SetParent(world.transform);item.transform.position=new Vector3(p.X,3,p.Z);item.transform.rotation=view.transform.rotation;
            var label=item.GetComponent<TextMesh>();label.font=font;label.text=name;label.fontSize=48;label.characterSize=.22f;label.anchor=TextAnchor.MiddleCenter;label.color=Color.white;item.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        }
        private LineRenderer Line(Color color,float width)
        {var item=new GameObject("Visual overlay",typeof(LineRenderer));item.transform.SetParent(world.transform);var line=item.GetComponent<LineRenderer>();var material=new Material(Shader.Find("Sprites/Default"));material.color=color;materials.Add(material);line.sharedMaterial=material;line.startWidth=line.endWidth=width;line.positionCount=2;return line;}
        private void OnGUI()
        {
            if((Application.isBatchMode && capturePath=="") || Simulation==null)return;
            if(text==null){text=new GUIStyle(GUI.skin.label){font=font,fontSize=15,wordWrap=true};text.normal.textColor=Color.white;title=new GUIStyle(text){fontSize=23,fontStyle=FontStyle.Bold};}
            GUI.Box(new Rect(15,15,370,Screen.height-30),GUIContent.none);GUILayout.BeginArea(new Rect(30,28,340,Screen.height-55));
            GUILayout.Label("AEROLAB / DELIVERY",title);
            GUILayout.Label("Drone không camera · Khuôn viên mô phỏng",text);
            GUILayout.Space(12);GUILayout.Label("Chế độ: "+(FlightAgent==null?"Tham chiếu theo luật":Mode=="flight" || Mode=="dispatch"?"Đang huấn luyện PPO":"AI thử nghiệm (xem báo cáo đánh giá)"),text);
            if(FlightAgent!=null){GUILayout.Label("Bay: "+FlightModel+"\nĐiều phối: "+DispatchModel,text);GUILayout.Label(qualificationLabel,text);}
            GUILayout.Label($"Seed {Seed} | {Simulation.Elapsed:F1}s | {PhaseName(Simulation.Phase)}",text);
            GUILayout.Label($"Pin: {Simulation.Battery:F1}/{Simulation.Config.Capacity} | Tải: {Simulation.Payload:F1} kg",text);
            var report=Simulation.Report();GUILayout.Label($"Đã giao {report.Delivered} · Đúng hạn {report.OnTime}\nQuay về an toàn {report.SafeReturns} · {Simulation.Distance:F0} m",text);
            if(GUILayout.Button(Paused?"Bắt đầu / Tiếp tục":"Tạm dừng",GUILayout.Height(34)))Paused=!Paused;
            if(FlightAgent==null)
            {
                if(GUILayout.Button("Lượt mới / seed tiếp theo")){Seed++;ResetEpisode();}
                if(GUILayout.Button("Điều phối: "+(Rule==DispatchRule.Fifo?"Đến trước, giao trước":"Ưu tiên hạn giao")))Rule=Rule==DispatchRule.Fifo?DispatchRule.EarliestDeadline:DispatchRule.Fifo;
                bool noise=GUILayout.Toggle(Noisy,"Sai số, độ trễ và mất mẫu cảm biến");if(noise!=Noisy){Noisy=noise;ResetEpisode();}
            }
            ShowSensors=GUILayout.Toggle(ShowSensors,"Hiện tia khoảng cách (mất mẫu: ẩn)");
            GUILayout.Label("Trắng: vệt bay thực tế · Xanh: tuyến tham chiếu",text);
            GUILayout.Space(10);GUILayout.Label("ĐƠN HÀNG",text);
            orderScroll=GUILayout.BeginScrollView(orderScroll,GUILayout.Height(Mathf.Max(80,Screen.height-(FlightAgent==null?600:710))));
            foreach(var order in Simulation.Orders.Where(o=>o.Status!=OrderStatus.Scheduled).Take(12))GUILayout.Label($"#{order.Spec.Id:00} → Điểm {order.Spec.Destination+1} | {StatusName(order.Status)}\nHạn {order.Spec.Deadline:F0}s · {order.Spec.Mass:F1} kg",text);
            GUILayout.EndScrollView();
            if(!Simulation.Running)GUILayout.Label("Kết quả: "+Simulation.Result,text);
            GUILayout.Label("Pin là mô hình quy ước. Pháp lý khu vực: chưa xác minh.",text);GUILayout.EndArea();
        }
        private void OnDestroy(){if(traceWriter!=null)traceWriter.Dispose();foreach(var m in materials)if(m!=null)Destroy(m);}
        private static string PhaseName(DeliveryPhase phase)
        {return new[]{"Chờ","Nhận hàng","Đang giao","Bàn giao","Quay về","Đang sạc","Kết thúc"}[(int)phase];}
        private static string StatusName(OrderStatus status)
        {return new[]{"Chưa phát sinh","Chờ giao","Đang giao","Đã giao","Từ chối","Thất bại","Chưa hoàn tất"}[(int)status];}
        private static Vector3 UnityVector(NVector3 v){return new Vector3(v.X,v.Y,v.Z);}
    }
}
