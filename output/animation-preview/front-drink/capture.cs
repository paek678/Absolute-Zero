var sourceScene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene_Multi.unity");
var preview = new UnityEditor.PreviewRenderUtility();
UnityEngine.Material previewMaterial = null;
try {
    var source = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.Transform>(true)).First(t => t.name == "EnemyPlayer_0").gameObject;
    var clone = UnityEngine.Object.Instantiate(source);
    preview.AddSingleGO(clone);
    clone.transform.position = UnityEngine.Vector3.zero;
    clone.transform.rotation = UnityEngine.Quaternion.identity;
    clone.transform.localScale = UnityEngine.Vector3.one;
    clone.SetActive(true);
    var animator = clone.GetComponent<UnityEngine.Animator>();
    var controller = animator.runtimeAnimatorController;
    animator.enabled = false;
    var clip = controller.animationClips.First(c => c.name == "playerA_drink");
    var renderers = clone.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true);
    previewMaterial = new UnityEngine.Material(UnityEngine.Shader.Find("Sprites/Default"));
    foreach (var renderer in renderers) renderer.sharedMaterial = previewMaterial;
    var freeze = clone.transform.Find("freezeice");
    if (freeze != null) freeze.gameObject.SetActive(false);
    var resolvers = clone.GetComponentsInChildren<UnityEngine.U2D.Animation.SpriteResolver>(true);
    var camera = preview.camera;
    camera.orthographic = true;
    camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
    camera.backgroundColor = new UnityEngine.Color(0.88f, 0.89f, 0.91f, 1f);
    camera.nearClipPlane = 0.01f;
    camera.farClipPlane = 100f;
    var bounds = new UnityEngine.Bounds();
    bool initialized = false;
    for (int frame = 0; frame <= 45; frame++) {
        clip.SampleAnimation(clone, frame / 30f);
        foreach (var resolver in resolvers) resolver.ResolveSpriteToSpriteRenderer();
        foreach (var renderer in renderers) {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null) continue;
            if (!initialized) { bounds = renderer.bounds; initialized = true; } else bounds.Encapsulate(renderer.bounds);
        }
    }
    camera.orthographicSize = UnityEngine.Mathf.Max(bounds.extents.y, bounds.extents.x / 0.75f) * 1.16f;
    camera.transform.position = new UnityEngine.Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 10f);
    camera.transform.rotation = UnityEngine.Quaternion.identity;
    var evidence = new System.Collections.Generic.List<string>();
    for (int frame = 0; frame <= 45; frame++) {
        float time = frame / 30f;
        clip.SampleAnimation(clone, time);
        foreach (var resolver in resolvers) resolver.ResolveSpriteToSpriteRenderer();
        preview.BeginPreview(new UnityEngine.Rect(0, 0, 384, 512), UnityEngine.GUIStyle.none);
        preview.Render(true);
        var rendered = (UnityEngine.RenderTexture)preview.EndPreview();
        var old = UnityEngine.RenderTexture.active;
        UnityEngine.RenderTexture.active = rendered;
        var image = new UnityEngine.Texture2D(384, 512, UnityEngine.TextureFormat.RGB24, false);
        image.ReadPixels(new UnityEngine.Rect(0, 0, 384, 512), 0, 0);
        image.Apply();
        UnityEngine.RenderTexture.active = old;
        System.IO.File.WriteAllBytes("C:/Users/paek6/Absolute Zero/output/animation-preview/front-drink/frames/" + frame.ToString("D3") + ".png", UnityEngine.ImageConversion.EncodeToPNG(image));
        UnityEngine.Object.DestroyImmediate(image);
        if (frame % 15 == 0) evidence.Add(time + ": " + string.Join(", ", resolvers.Select(r => r.name + "=" + r.GetCategory() + "/" + r.GetLabel())));
    }
    var report = "Source: GameScene_Multi/EnemyPlayer_0\nController: " + UnityEditor.AssetDatabase.GetAssetPath(controller) + "\nClip: " + UnityEditor.AssetDatabase.GetAssetPath(clip) + "\nLength: " + clip.length + "\nFrames: 46 at 30 fps\nMethod: original clip sampling and SpriteResolver evaluation on isolated preview clone; neutral unlit preview material; no live gameplay/VFX driver\n" + string.Join("\n", evidence);
    System.IO.File.WriteAllText("C:/Users/paek6/Absolute Zero/output/animation-preview/front-drink/evidence.txt", report);
    return report;
} finally {
    preview.Cleanup();
    if (previewMaterial != null) UnityEngine.Object.DestroyImmediate(previewMaterial);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(sourceScene);
}
