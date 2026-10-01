#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Session;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    // Opt-in diagnostics. Uses the real menu; list stress is presentation-only.
    public sealed class SoloSelectionValidationProbe : MonoBehaviour
    {
        [Serializable] sealed class Report { public bool passed; public string failure; public List<string> checks = new(), errors = new(); }
        readonly Report _report = new();
        string _directory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args,"--plan040-solo-menu");
            if (Application.isEditor || at < 0 || at + 1 >= args.Length) return;
            var probe = new GameObject(nameof(SoloSelectionValidationProbe)).AddComponent<SoloSelectionValidationProbe>();
            probe._directory = Path.GetFullPath(args[at+1]);
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject); Application.runInBackground = true; Directory.CreateDirectory(_directory);
            Application.logMessageReceived += Log;
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while(stack.Count > 0)
            {
                object value = null;
                try { var item = stack.Peek(); if(!item.MoveNext()) { stack.Pop(); continue; } value=item.Current;
                    if(value is IEnumerator child) { stack.Push(child); continue; } }
                catch(Exception error) { _report.failure=error.ToString(); break; }
                yield return value;
            }
            Application.logMessageReceived -= Log;
            _report.passed = _report.failure == null && _report.errors.Count == 0;
            File.WriteAllText(Path.Combine(_directory,"menu-report.json"),JsonUtility.ToJson(_report,true));
            Application.Quit(_report.passed ? 0 : 2);
        }
        IEnumerator Run()
        {
            yield return Menu();
            var app = AppBootstrapper.Instance;
            for(int i=0;i<20;i++)
            {
                Main().onClick.Invoke();
                var view = FindAnyObjectByType<SoloSelectionBindings>();
                Check(view != null && view.IsComplete && view.StartButton.interactable, "open " + i + " usable selection");
                Check(app.SessionRouter.Current == null && !NetworkManager.Singleton.IsListening, "open " + i + " starts no network");
                Check(view.Content.GetComponentsInChildren<Button>().Length == 1, "open " + i + " no duplicated profile buttons");
                if(i==0) { yield return null; yield return null; CheckBounds(view.StartButton); CheckBounds(view.BackButton); yield return Capture("default-selection"); }
                view.BackButton.onClick.Invoke(); yield return null;
                Check(Main()?.interactable == true, "back " + i + " restores menu");
            }
            Main().onClick.Invoke();
            var owner = FindAnyObjectByType<AZLobbyUI>();
            var field = typeof(AZLobbyUI).GetField("_soloView",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var actualView = (SoloSelectionView)field.GetValue(owner);
            actualView.Render(Enumerable.Range(1,16).Select(i=>"목록 검사 " + i).ToArray(),0,"개발 검사에서만 표시하는 긴 목록입니다.","목록 스크롤 검사",false,true,true);
            yield return null; yield return null;
            var panel = FindAnyObjectByType<SoloSelectionBindings>();
            var scroll = panel.Content.GetComponentInParent<ScrollRect>();
            Check(panel.Content.rect.height > scroll.viewport.rect.height, "long presentation list exceeds viewport and can scroll");
            scroll.verticalNormalizedPosition = 0; yield return null; yield return Capture("list-scroll");
            panel.BackButton.onClick.Invoke(); yield return null;
            for(int i=0;i<3;i++)
            {
                yield return Menu(); Main().onClick.Invoke();
                var current = FindAnyObjectByType<SoloSelectionBindings>();
                Check(current.Content.GetComponentsInChildren<Button>().Length==1,"restored real profile count before cancellation " + i);
                current.StartButton.onClick.Invoke();
                Check(app.SessionRouter.IsSolo,"start acquired local lease " + i);
                current.BackButton.onClick.Invoke(); current.BackButton.onClick.Invoke();
                yield return Menu();
                Check(!NetworkManager.Singleton.IsListening && app.SessionRouter.Current==null,"cancel " + i + " releases host and lease");
            }
            Check(!new UnityServicesGateway().IsInitialized,"menu and cancellation never initialize UGS");
            Main().onClick.Invoke(); yield return null; yield return Capture("reopened-after-cancel");
            FindAnyObjectByType<SoloSelectionBindings>().BackButton.onClick.Invoke();
            yield return Menu();
        }
        static Button Main() => GameObject.Find("MainUI/MainPanel/SoloBtn")?.GetComponent<Button>();
        IEnumerator Menu() => Until(()=>AppBootstrapper.Instance?.IsReady==true && SceneManager.GetActiveScene().name=="LobbyScene"
            && AppBootstrapper.Instance.SessionRouter.Current==null && Main()?.interactable==true
            && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.ShutdownInProgress,"usable menu");
        IEnumerator Until(Func<bool> ready,string label)
        { double end=Time.realtimeSinceStartupAsDouble+45; while(!ready()) { if(Time.realtimeSinceStartupAsDouble>end) throw new TimeoutException(label); yield return null; } yield return null; }
        void CheckBounds(Button button)
        {
            var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
            Check(corners[0].x>=0 && corners[0].y>=0 && corners[2].x<=Screen.width+1 && corners[2].y<=Screen.height+1
                && corners[2].x-corners[0].x>30 && corners[2].y-corners[0].y>20,"visible bounds " + button.name);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            try { Check(image!=null && image.width==Screen.width,"framebuffer " + name); File.WriteAllBytes(Path.Combine(_directory,name+".png"),image.EncodeToPNG()); }
            finally { if(image!=null) Destroy(image); }
        }
        void Check(bool ok,string label) { if(!ok) throw new InvalidOperationException(label); _report.checks.Add(label); }
        void Log(string message,string trace,LogType type) { if(type is LogType.Error or LogType.Exception) _report.errors.Add(message); }
    }
}
#endif
