
var rootPath="Assets/Tests/SideDrinkPreview/SideAtlasV4";
System.IO.Directory.CreateDirectory(rootPath);
System.IO.File.Copy("output/imagegen/side-complete-parts-v4/atlas.png",rootPath+"/side_idle.png",true);
var path=rootPath+"/side_idle.png";
UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
importer.textureType=UnityEditor.TextureImporterType.Sprite;
importer.spriteImportMode=UnityEditor.SpriteImportMode.Multiple;
importer.spritePixelsPerUnit=200;
importer.maxTextureSize=2048;
importer.alphaIsTransparency=true;
importer.mipmapEnabled=false;
importer.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed;
importer.SaveAndReimport();
var factory=new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();factory.Init();
var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
var cap=provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteFrameEditCapability>();
var required=UnityEditor.U2D.Sprites.EEditCapability.CreateAndDeleteSprite|UnityEditor.U2D.Sprites.EEditCapability.EditSpriteName|UnityEditor.U2D.Sprites.EEditCapability.EditSpriteRect|UnityEditor.U2D.Sprites.EEditCapability.EditPivot;
if(cap==null || !cap.GetEditCapability().HasCapability(required))throw new System.Exception("Sprite capability unavailable");
var old=provider.GetSpriteRects().ToDictionary(r=>r.name,r=>r.spriteID);
var rects=new System.Collections.Generic.List<UnityEditor.SpriteRect>();
rects.Add(new UnityEditor.SpriteRect{name="backHair",rect=new UnityEngine.Rect(24,1355,521,669),alignment=UnityEngine.SpriteAlignment.Center,pivot=new UnityEngine.Vector2(.5f,.5f),spriteID=old.ContainsKey("backHair")?old["backHair"]:UnityEditor.GUID.Generate()});
rects.Add(new UnityEditor.SpriteRect{name="lowerbody",rect=new UnityEngine.Rect(569,1751,539,273),alignment=UnityEngine.SpriteAlignment.Center,pivot=new UnityEngine.Vector2(.5f,.5f),spriteID=old.ContainsKey("lowerbody")?old["lowerbody"]:UnityEditor.GUID.Generate()});
rects.Add(new UnityEditor.SpriteRect{name="body",rect=new UnityEngine.Rect(1132,1589,366,435),alignment=UnityEngine.SpriteAlignment.Center,pivot=new UnityEngine.Vector2(.5f,.5f),spriteID=old.ContainsKey("body")?old["body"]:UnityEditor.GUID.Generate()});
rects.Add(new UnityEditor.SpriteRect{name="frontHair_2",rect=new UnityEngine.Rect(1522,1701,70,323),alignment=UnityEngine.SpriteAlignment.Center,pivot=new UnityEngine.Vector2(.5f,.5f),spriteID=old.ContainsKey("frontHair_2")?old["frontHair_2"]:UnityEditor.GUID.Generate()});
rects.Add(new UnityEditor.SpriteRect{name="arm2",rect=new UnityEngine.Rect(1616,1613,163,411),alignment=UnityEngine.SpriteAlignment.Center,pivot=new UnityEngine.Vector2(.5f,.5f),spriteID=old.ContainsKey("arm2")?old["arm2"]:UnityEditor.GUID.Generate()});
rects.Add(new UnityEditor.SpriteRect{name="arm1",rect=new UnityEngine.Rect(24,832,274,473),alignment=UnityEngine.SpriteAlignment.Center,pivot=new UnityEngine.Vector2(.5f,.5f),spriteID=old.ContainsKey("arm1")?old["arm1"]:UnityEditor.GUID.Generate()});
rects.Add(new UnityEditor.SpriteRect{name="head",rect=new UnityEngine.Rect(322,748,472,557),alignment=UnityEngine.SpriteAlignment.Center,pivot=new UnityEngine.Vector2(.5f,.5f),spriteID=old.ContainsKey("head")?old["head"]:UnityEditor.GUID.Generate()});
rects.Add(new UnityEditor.SpriteRect{name="frontHair",rect=new UnityEngine.Rect(818,921,645,384),alignment=UnityEngine.SpriteAlignment.Center,pivot=new UnityEngine.Vector2(.5f,.5f),spriteID=old.ContainsKey("frontHair")?old["frontHair"]:UnityEditor.GUID.Generate()});
rects.Add(new UnityEditor.SpriteRect{name="frontHair_1",rect=new UnityEngine.Rect(1487,878,124,427),alignment=UnityEngine.SpriteAlignment.Center,pivot=new UnityEngine.Vector2(.5f,.5f),spriteID=old.ContainsKey("frontHair_1")?old["frontHair_1"]:UnityEditor.GUID.Generate()});

provider.SetSpriteRects(rects.ToArray());
var ids=provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
if(ids!=null)ids.SetNameFileIdPairs(rects.Select(r=>new UnityEditor.SpriteNameFileIdPair(r.name,r.spriteID)));
provider.Apply();importer.SaveAndReimport();
var sprites=UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEngine.Sprite>().ToDictionary(s=>s.name);
var character=new UnityEngine.GameObject("SideCharacterV4");
var mat=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Tests/SideDrinkPreview/PreviewSprite.mat");
{var g=new UnityEngine.GameObject("backHair");g.transform.SetParent(character.transform,false);g.transform.localPosition=new UnityEngine.Vector3(-0.36f,1.115f,0);var r=g.AddComponent<UnityEngine.SpriteRenderer>();r.sprite=sprites["backHair"];r.sortingOrder=0;r.sharedMaterial=mat;}
{var g=new UnityEngine.GameObject("lowerbody");g.transform.SetParent(character.transform,false);g.transform.localPosition=new UnityEngine.Vector3(0.205f,-2.25f,0);var r=g.AddComponent<UnityEngine.SpriteRenderer>();r.sprite=sprites["lowerbody"];r.sortingOrder=1;r.sharedMaterial=mat;}
{var g=new UnityEngine.GameObject("body");g.transform.SetParent(character.transform,false);g.transform.localPosition=new UnityEngine.Vector3(-0.1875f,-0.955f,0);var r=g.AddComponent<UnityEngine.SpriteRenderer>();r.sprite=sprites["body"];r.sortingOrder=2;r.sharedMaterial=mat;}
{var g=new UnityEngine.GameObject("frontHair_2");g.transform.SetParent(character.transform,false);g.transform.localPosition=new UnityEngine.Vector3(-0.2725f,0.835f,0);var r=g.AddComponent<UnityEngine.SpriteRenderer>();r.sprite=sprites["frontHair_2"];r.sortingOrder=3;r.sharedMaterial=mat;}
{var g=new UnityEngine.GameObject("arm2");g.transform.SetParent(character.transform,false);g.transform.localPosition=new UnityEngine.Vector3(0.525f,-1.19f,0);var r=g.AddComponent<UnityEngine.SpriteRenderer>();r.sprite=sprites["arm2"];r.sortingOrder=4;r.sharedMaterial=mat;}
{var g=new UnityEngine.GameObject("arm1");g.transform.SetParent(character.transform,false);g.transform.localPosition=new UnityEngine.Vector3(-0.0275f,-1.09f,0);var r=g.AddComponent<UnityEngine.SpriteRenderer>();r.sprite=sprites["arm1"];r.sortingOrder=5;r.sharedMaterial=mat;}
{var g=new UnityEngine.GameObject("head");g.transform.SetParent(character.transform,false);g.transform.localPosition=new UnityEngine.Vector3(0.3175f,1.01f,0);var r=g.AddComponent<UnityEngine.SpriteRenderer>();r.sprite=sprites["head"];r.sortingOrder=6;r.sharedMaterial=mat;}
{var g=new UnityEngine.GameObject("frontHair");g.transform.SetParent(character.transform,false);g.transform.localPosition=new UnityEngine.Vector3(-0.055f,1.9275f,0);var r=g.AddComponent<UnityEngine.SpriteRenderer>();r.sprite=sprites["frontHair"];r.sortingOrder=7;r.sharedMaterial=mat;}
{var g=new UnityEngine.GameObject("frontHair_1");g.transform.SetParent(character.transform,false);g.transform.localPosition=new UnityEngine.Vector3(0.0525f,0.895f,0);var r=g.AddComponent<UnityEngine.SpriteRenderer>();r.sprite=sprites["frontHair_1"];r.sortingOrder=8;r.sharedMaterial=mat;}

UnityEditor.PrefabUtility.SaveAsPrefabAsset(character,rootPath+"/SideCharacterV4.prefab");
UnityEngine.Object.DestroyImmediate(character);
UnityEditor.AssetDatabase.SaveAssets();
return new {sprites=sprites.Count,prefab=rootPath+"/SideCharacterV4.prefab"};

