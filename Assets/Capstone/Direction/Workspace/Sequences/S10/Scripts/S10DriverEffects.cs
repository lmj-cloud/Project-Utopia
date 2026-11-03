using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
namespace Capstone.Direction.Workspace.S10
{
    public sealed class S10DriverEffects : MonoBehaviour
    {
        public S10CrashSequence owner;
        public Transform seatEye,wifeSeat,rearLeft,rearRight,mirror;
        public UnityEngine.UI.Image subjectiveRed;
        public GameObject windshieldCracks;
        public bool realTruckUsed;
        public string truckSource="Assets/Single_detailed_truck/3dobjects/truck.fbx";
        public float RedStrength {get;private set;}
        public int EyeCycle {get;private set;}
        CinemachineBlendDefinition previousBlend;
        CinemachineBrain brain;
        CinemachineBrain.UpdateMethods previousUpdate;
        bool previousIgnoreTimeScale;
        ICinemachineCamera previousSolo;
        bool previousCracks,acquired;
        Color previousRed;
        void Awake()
        {
            // The FBX contains an authoring camera aimed at its grille. It must never
            // render over the scene camera, even outside an active cutscene.
            if(owner&&owner.truck)foreach(var importedCamera in owner.truck.GetComponentsInChildren<Camera>(true))
                if(importedCamera!=owner.outputCamera)importedCamera.enabled=false;
        }
        void OnEnable(){if(owner)owner.onStarted.AddListener(Begin);}
        void OnDisable(){if(owner)owner.onStarted.RemoveListener(Begin);Restore();}
        void Begin()
        {
            previousRed=subjectiveRed.color;previousCracks=windshieldCracks.activeSelf;previousBlend=owner.outputCamera.GetComponent<CinemachineBrain>().DefaultBlend;acquired=true;
            owner.outputCamera.GetComponent<CinemachineBrain>().DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut,0);
            brain=owner.outputCamera.GetComponent<CinemachineBrain>();
            previousUpdate=brain.UpdateMethod;previousIgnoreTimeScale=brain.IgnoreTimeScale;
            previousSolo=CinemachineCore.SoloCamera;CinemachineCore.SoloCamera=null;
            brain.UpdateMethod=CinemachineBrain.UpdateMethods.ManualUpdate;brain.IgnoreTimeScale=true;
            owner.cinematicPose.SetPositionAndRotation(seatEye.position,seatEye.rotation);owner.outputCamera.transform.SetPositionAndRotation(seatEye.position,seatEye.rotation);
        }
        void LateUpdate()
        {
            // Procedural pose is written in Update. Resolve the output camera afterwards,
            // including when SmartUpdate would otherwise classify a stationary seat rig.
            if(acquired&&owner&&owner.Active&&brain&&brain.isActiveAndEnabled)brain.ManualUpdate();
        }
        public void Apply(float t)
        {
            var s=owner.timing;
            float hit=t>=s.ImpactAt?Mathf.Exp(-(t-s.ImpactAt)*10):0;
            float disorient=t>=s.ConfusionAt?Mathf.Exp(-(t-s.ConfusionAt)*.55f):0;
            float glance=t<s.ApproachAt?Mathf.Sin(Mathf.Clamp01(t/Mathf.Max(.1f,s.ApproachAt))*Mathf.PI)*38:0;
            float sway=t<s.ImpactAt?.003f:.012f*disorient;
            Vector3 offset=new Vector3(Mathf.Sin(t*19)*sway,Mathf.Sin(t*23)*sway,-hit*.02f+hit*.20f);
            owner.cinematicPose.SetPositionAndRotation(seatEye.position+seatEye.TransformVector(offset),seatEye.rotation*Quaternion.Euler(hit*2.3f+disorient*.6f,glance,hit*Mathf.Sin(t*37)*.7f));
            var lens=owner.sequence.cinematicCamera.Lens;lens.FieldOfView=Mathf.Lerp(72,68,Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.DangerAt,s.ImpactAt,t)));lens.NearClipPlane=.025f;owner.sequence.cinematicCamera.Lens=lens;
            float progression=Mathf.Clamp01(Mathf.InverseLerp(s.ImpactAt,s.CoveredAt,t));
            RedStrength=t<s.ImpactAt?0:Mathf.Lerp(.05f,.18f,Mathf.SmoothStep(0,1,progression));
            subjectiveRed.color=new Color(.58f,.015f,.01f,RedStrength);
            owner.confusionVolume.weight=t<s.ImpactAt?0:Mathf.Lerp(.3f,1,progression);
            windshieldCracks.SetActive(t>=s.ImpactAt);
            float eye=s.EyeAlpha(t);
            if(t>=s.BlackAt)owner.screen.overlay.color=new Color(0,0,0,eye);
            EyeCycle=t<s.BlackAt?0:t<s.SecondAt?1:t<s.ThirdAt?2:3;
            float growth=Mathf.Clamp01(s.tinnitusGrowth.Evaluate(Mathf.InverseLerp(s.ConfusionAt,s.CoveredAt,t)));
            float endFade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.CoveredAt,s.FinishAt,t));
            owner.tinnitus.volume=t<s.ConfusionAt?0:Mathf.Clamp(growth*Mathf.Clamp(s.tinnitusMaximum,0,.25f),0,.25f)*endFade;
            float remaining=1-Mathf.Clamp01(Mathf.InverseLerp(s.ConfusionAt,s.CoveredAt,t));
            if(t>=s.ConfusionAt){owner.engine.volume*=remaining;owner.road.volume*=remaining;owner.cabin.volume*=remaining;owner.truckAudio.volume*=remaining;}
            if(owner.status)owner.status.text="S10 DRIVER POV | wife: front passenger / children: rear\nX cancel / eye cycle "+EyeCycle+" / TEMP audio";
        }
        public void Restore()
        {
            if(!acquired)return;acquired=false;
            if(subjectiveRed)subjectiveRed.color=previousRed;if(windshieldCracks)windshieldCracks.SetActive(previousCracks);
            if(owner&&owner.outputCamera)owner.outputCamera.GetComponent<CinemachineBrain>().DefaultBlend=previousBlend;
            if(brain){brain.UpdateMethod=previousUpdate;brain.IgnoreTimeScale=previousIgnoreTimeScale;}
            CinemachineCore.SoloCamera=previousSolo;previousSolo=null;
            RedStrength=0;EyeCycle=0;
        }
    }
}
