using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Rendering.Universal;
using Capstone.Direction;

/// <summary>
/// 격리된 연출 테스트 Scene과 빈 Timeline을 생성하는 기존 Editor 메뉴를 제공한다.
/// </summary>
public static class DirectionSetup
{
    const string Root="Assets/Capstone/Direction";
    /// <summary>
    /// 기존 Editor 메뉴에서 격리된 연출 테스트 Scene과 임시 연결을 생성한다.
    /// </summary>
    [MenuItem("Capstone/Direction/Create isolated S03 test scene")]
    public static void CreateTestScene()
    {
        // Do not close, overwrite, or save a teammate's currently open scene.
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        var root=new GameObject("S03_TEST_ONLY");
        var audio=root.AddComponent<DirectionAudio>();
        var fx=root.AddComponent<DirectionScreen>();
        var sequence=root.AddComponent<DirectionSequence>();
        root.AddComponent<DirectionVolume>();
        var camera=new GameObject("S03 Camera");camera.transform.SetParent(root.transform);
        camera.tag="MainCamera";camera.AddComponent<Camera>();camera.AddComponent<AudioListener>();
        camera.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
        camera.transform.position=new Vector3(0,2,-8);camera.transform.LookAt(new Vector3(0,1,0));
        var canvasObject=new GameObject("Fade Canvas",typeof(Canvas));canvasObject.transform.SetParent(root.transform);
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
        var imageObject=new GameObject("Overlay",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform,false);
        var rt=imageObject.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
        fx.overlay=imageObject.GetComponent<Image>();fx.overlay.color=Color.clear;fx.overlay.raycastTarget=false;
        var stage=GameObject.CreatePrimitive(PrimitiveType.Cube);stage.name="STADIUM_PLACEHOLDER";stage.transform.SetParent(root.transform);
        stage.transform.localScale=new Vector3(7,0.2f,7);
        var lightObject=new GameObject("Test Light");lightObject.transform.SetParent(root.transform);
        lightObject.AddComponent<Light>().type=LightType.Directional;lightObject.transform.rotation=Quaternion.Euler(50,-30,0);
        sequence.screen=fx;sequence.audioSystem=audio;sequence.enableF8Test=true;
        // In-memory procedural tones are intentionally not production crowd or door assets.
        var toneHost=root.AddComponent<DirectionTestAudio>();toneHost.sequence=sequence;
        var director=root.AddComponent<PlayableDirector>();sequence.director=director;
        System.IO.Directory.CreateDirectory(Root+"/Timelines");
        var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=2;
        string timelinePath=AssetDatabase.GenerateUniqueAssetPath(Root+"/Timelines/S03_Test.playable");AssetDatabase.CreateAsset(timeline,timelinePath);
        director.playableAsset=timeline;director.playOnAwake=false;director.extrapolationMode=DirectorWrapMode.None;director.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime;
        string path=AssetDatabase.GenerateUniqueAssetPath(Root+"/S03_Test.unity");EditorSceneManager.SaveScene(scene,path);
        Selection.activeGameObject=root;
        Debug.Log("S03 test scene created. Open it alone before Play to avoid duplicate AudioListeners. Press F8. This scene has no real player: input restore must also be tested in the gameplay scene.");
        AssetDatabase.SaveAssets();
    }
    /// <summary>
    /// 기존 Editor 메뉴에서 지정 길이의 빈 시네마틱 Timeline 에셋을 생성한다.
    /// </summary>
    [MenuItem("Capstone/Direction/Create 120 second cinematic Timeline")]
    public static void CreateCinematic()
    {
        System.IO.Directory.CreateDirectory(Root+"/Timelines");
        var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=120;
        AssetDatabase.CreateAsset(timeline,AssetDatabase.GenerateUniqueAssetPath(Root+"/Timelines/Cinematic120_Empty.playable"));AssetDatabase.SaveAssets();
        Selection.activeObject=timeline;
    }
}
