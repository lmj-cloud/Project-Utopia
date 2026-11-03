using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Unity.Cinemachine;
namespace Capstone.Direction.Workspace.S10.Editor
{
    public static partial class S10WorkbenchBuilder
    {
        [MenuItem("Capstone/Direction/S10 Workspace/Play S10 in Game view")]
        public static void PlayInGameView()
        {
            Guard();Open();
            var gameView=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if(gameView!=null)EditorWindow.GetWindow(gameView).Focus();
            EditorApplication.isPlaying=true;
        }
        [MenuItem("Capstone/Direction/S10 Workspace/Upgrade existing to driver POV")]
        public static void UpgradeDriver()
        {
            Guard();
            var settings=AssetDatabase.LoadAssetAtPath<S10Timing>(SettingsPath);
            if(settings.driverVersion<2){settings.blackHold=1.2f;settings.driverVersion=2;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);}
            var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var c=prefab.GetComponentInChildren<S10CrashSequence>();var car=c.car;
                var driver=c.GetComponent<S10DriverEffects>();if(!driver)driver=c.gameObject.AddComponent<S10DriverEffects>();c.driver=driver;driver.owner=c;
                driver.seatEye=FindChild(car,"DriverSeatEye");driver.seatEye.localPosition=new Vector3(-.43f,1.64f,-.17f);driver.seatEye.localRotation=Quaternion.Euler(3,0,0);
                c.cinematicPose.name="CM_S10_DriverFirstPerson";c.cinematicPose.SetPositionAndRotation(driver.seatEye.position,driver.seatEye.rotation);var lens=c.sequence.cinematicCamera.Lens;lens.FieldOfView=72;lens.NearClipPlane=.025f;c.sequence.cinematicCamera.Lens=lens;
                c.outputCamera.transform.SetPositionAndRotation(driver.seatEye.position,driver.seatEye.rotation);c.outputCamera.GetComponent<UniversalAdditionalCameraData>().requiresDepthTexture=true;
                var occupants=car.Find("Occupants_TEST_CountNotCanon");if(!occupants)occupants=car.Find("FamilySeats");occupants.name="FamilySeats";
                var hero=occupants.Find("Protagonist_Standin");if(hero){hero.name="Hero_DriverSeat";hero.Find("Head").gameObject.SetActive(false);}
                var wife=occupants.Find("Family_Standin_A");if(!wife)wife=occupants.Find("Wife_FrontPassenger");wife.name="Wife_FrontPassenger";wife.localPosition=new Vector3(.43f,.98f,.03f);driver.wifeSeat=wife;
                var child=occupants.Find("Family_Standin_B");if(!child)child=occupants.Find("Child_RearLeft");child.name="Child_RearLeft";child.localPosition=new Vector3(-.43f,.98f,-1.1f);child.localScale=Vector3.one*.8f;driver.rearLeft=child;
                var child2=occupants.Find("Child_RearRight");if(!child2){child2=UnityEngine.Object.Instantiate(child.gameObject,occupants).transform;child2.name="Child_RearRight";}child2.localPosition=new Vector3(.43f,.98f,-1.1f);driver.rearRight=child2;
                var dark=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/RubberAndCabin.mat");var interior=FindChild(car,"DriverInterior");
                if(!interior.Find("CabinRoof"))Box(interior,"CabinRoof",new Vector3(0,1.97f,-.4f),new Vector3(1.9f,.08f,2.7f),dark);
                if(!interior.Find("SteeringWheel"))
                {
                    var wheel=Child(interior,"SteeringWheel");wheel.localPosition=new Vector3(-.43f,1.25f,.55f);wheel.localRotation=Quaternion.Euler(-15,0,0);
                    for(int i=0;i<20;i++){float a=i*Mathf.PI*2/20;var segment=Box(wheel,"Rim",new Vector3(Mathf.Cos(a)*.19f,Mathf.Sin(a)*.19f,0),new Vector3(.07f,.035f,.035f),dark);segment.localRotation=Quaternion.Euler(0,0,i*18+90);}
                    Box(wheel,"Hub",Vector3.zero,new Vector3(.13f,.1f,.04f),dark);Box(wheel,"Spoke",Vector3.down*.11f,new Vector3(.045f,.18f,.03f),dark);
                }
                var glass=OwnedMat("DriverWindshield",new Color(.6f,.8f,.9f,.025f),true);
                if(!interior.Find("Windshield"))Box(interior,"Windshield",new Vector3(0,1.49f,.99f),new Vector3(1.7f,.72f,.008f),glass);
                var crack=FindChild(interior,"ImpactCracks");driver.windshieldCracks=crack.gameObject;
                if(crack.childCount==0){var crackMat=OwnedMat("WindshieldCrack",new Color(.18f,.21f,.22f,.8f),true);for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;var line=Box(crack,"CrackLine",new Vector3(-.15f+Mathf.Cos(a)*.2f,1.52f+Mathf.Sin(a)*.13f,.975f),new Vector3(.35f,.007f,.004f),crackMat);line.localRotation=Quaternion.Euler(0,0,i*30);}}crack.gameObject.SetActive(false);
                driver.mirror=FindChild(interior,"RearViewMirror_FamilyCue");driver.mirror.localPosition=new Vector3(0,1.80f,.78f);
                if(driver.mirror.childCount==0)
                {
                    var frame=Box(driver.mirror,"MirrorFrame",Vector3.zero,new Vector3(.40f,.13f,.025f),dark);
                    // A physical rear-view cue plus the brief passenger glance; no invented dialogue.
                    var cueMat=OwnedMat("MirrorFamilySilhouettes",new Color(.45f,.53f,.57f));for(int i=0;i<2;i++)Primitive(driver.mirror,"RearChildSilhouette",PrimitiveType.Sphere,new Vector3(-.09f+i*.18f,0,-.019f),new Vector3(.06f,.075f,.012f),cueMat);
                }
                var canvas=c.screen.overlay.canvas;var red=canvas.transform.Find("S10_SubjectiveRed");
                if(!red){var go=new GameObject("S10_SubjectiveRed",typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(canvas.transform,false);red=go.transform;var r=(RectTransform)red;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
                driver.subjectiveRed=red.GetComponent<UnityEngine.UI.Image>();driver.subjectiveRed.color=Color.clear;driver.subjectiveRed.raycastTarget=false;c.screen.overlay.transform.SetAsLastSibling();red.SetSiblingIndex(c.screen.overlay.transform.GetSiblingIndex()-1);
                var profile=c.confusionVolume.sharedProfile;
                if(!profile.TryGet<ColorAdjustments>(out var color))color=profile.Add<ColorAdjustments>(true);color.colorFilter.Override(new Color(1,.5f,.43f));color.saturation.Override(-15);
                if(!profile.TryGet<Vignette>(out var vignette))vignette=profile.Add<Vignette>(true);vignette.color.Override(new Color(.35f,.005f,0));vignette.intensity.Override(.52f);
                if(!profile.TryGet<DepthOfField>(out var dof)){dof=profile.Add<DepthOfField>(true);AssetDatabase.AddObjectToAsset(dof,profile);}dof.mode.Override(DepthOfFieldMode.Gaussian);dof.gaussianStart.Override(.25f);dof.gaussianEnd.Override(2.5f);dof.gaussianMaxRadius.Override(1.25f);
                foreach(var component in profile.components)EditorUtility.SetDirty(component);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
                InstallRealTruck(c,driver);
                PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            ApplyTiming();Debug.Log("S10_DRIVER_UPGRADE_PASS (existing Scene/Prefab/Timeline/Signals preserved)");
        }
        static Transform FindChild(Transform parent,string name)=>parent.Find(name)?parent.Find(name):Child(parent,name);
        static Material OwnedMat(string name,Color color,bool transparent=false)
        {
            string path=Root+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}material.color=color;
            if(transparent){material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;}EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
        }
        static Bounds LocalBounds(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>();Bounds bounds=new Bounds();bool first=true;
            foreach(var renderer in renderers){var b=renderer.bounds;foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1}){var p=root.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}}return bounds;
        }
        static void InstallRealTruck(S10CrashSequence c,S10DriverEffects driver)
        {
            string path=Root+"/Resources/TruckFromLocalCache/truck.fbx";var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!asset){driver.realTruckUsed=false;Debug.LogWarning("S10 real truck unavailable; keeping explicit temporary blockout.");return;}
            var model=c.truck.Find("ImportedSingleDetailedTruck");
            if(!model){foreach(Transform old in c.truck.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);model=((GameObject)PrefabUtility.InstantiatePrefab(asset,c.truck)).transform;model.name="ImportedSingleDetailedTruck";}
            foreach(var importedCamera in model.GetComponentsInChildren<Camera>(true))importedCamera.enabled=false;
            model.localPosition=Vector3.zero;model.localRotation=Quaternion.identity;model.localScale=Vector3.one;
            var bounds=LocalBounds(model);if(bounds.size.x>bounds.size.z)model.localRotation=Quaternion.Euler(0,90,0);
            var front=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.Contains("wheel_front"));var rear=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.Contains("wheels_back"));
            if(front&&rear&&c.truck.InverseTransformPoint(front.position).z<c.truck.InverseTransformPoint(rear.position).z)model.localRotation*=Quaternion.Euler(0,180,0);
            bounds=LocalBounds(c.truck);model.localScale*=2.6f/Mathf.Max(.01f,bounds.size.x);bounds=LocalBounds(c.truck);model.localPosition+=new Vector3(-bounds.center.x,-bounds.min.y,3.8f-bounds.max.z);
            foreach(var r in model.GetComponentsInChildren<Renderer>())
            {
                var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++)
                {
                    string name=materials[i]?materials[i].name:"truck";while(name.StartsWith("Imported_",StringComparison.Ordinal))name=name.Substring(9);bool wheels=name.ToLowerInvariant().Contains("wheel");var m=OwnedMat("Imported_"+name.Replace('/','_'),wheels?new Color(.18f,.18f,.18f):name.Contains("windows")?new Color(.08f,.15f,.19f):new Color(.7f,.72f,.73f));
                    if(name.Contains("main_color")||wheels){var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Resources/TruckFromLocalCache/"+(wheels?"wheels.bmp":"truck_color_01.bmp"));m.SetTexture("_BaseMap",texture);m.color=Color.white;EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);}materials[i]=m;
                }r.sharedMaterials=materials;
            }
            foreach(var collider in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);driver.realTruckUsed=model.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh&&m.sharedMesh.vertexCount>0);Debug.Log("S10_REAL_TRUCK "+driver.realTruckUsed+" bounds="+LocalBounds(c.truck));
        }
        public static void VerifyRebuildAndRunValidation()
        {
            Guard();var paths=new[]{ScenePath,PrefabPath,TimelinePath,SettingsPath}.Concat(CueNames.Select(n=>Root+"/Timelines/"+n+".signal")).ToArray();var ids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            UpgradeDriver();int count=AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}).Length;UpgradeDriver();
            if(count!=AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}).Length)throw new InvalidOperationException("S10 repeated upgrade duplicated materials.");
            for(int i=0;i<paths.Length;i++)if(ids[i]!=AssetDatabase.AssetPathToGUID(paths[i]))throw new InvalidOperationException("S10 GUID changed: "+paths[i]);
            Debug.Log("S10_REBUILD_PASS stable GUIDs / no material duplication / driver POV retained");S10Validation.Run();
        }
    }
}
