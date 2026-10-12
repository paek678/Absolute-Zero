#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    /// <summary>Explicit development capture only; no input, game state or persistent settings writes.</summary>
    public sealed class GameplayVideoProbe : MonoBehaviour
    {
        const int Fps = 20;
        sealed class Frame { public byte[] Bytes; public double Time; }
        readonly BlockingCollection<Frame> _frames = new(4);
        System.Diagnostics.Process _encoder;
        Thread _writer;
        string _path, _encoderPath, _error;
        double _start;
        double _startGameTime;
        int _captured, _dropped, _encoded;
        int _width, _height;
        bool _closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args = Environment.GetCommandLineArgs();
            int output = Array.IndexOf(args, "--az-record-video"), encoder = Array.IndexOf(args, "--az-ffmpeg");
            if (Application.isEditor || output < 0 || output + 1 >= args.Length || encoder < 0 || encoder + 1 >= args.Length) return;
            var go = new GameObject(nameof(GameplayVideoProbe)); DontDestroyOnLoad(go);
            var probe = go.AddComponent<GameplayVideoProbe>();
            probe._path = Path.GetFullPath(args[output + 1]); probe._encoderPath = args[encoder + 1];
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var end = new WaitForEndOfFrame();
            double next = Time.realtimeSinceStartupAsDouble + 1;
            while (!_closed)
            {
                yield return end;
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < next) continue;
                next = now + 1.0 / Fps;
                if (!Capture(now)) break;
            }
        }

        bool Capture(double now)
        {
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null) throw new InvalidOperationException("Missing rendered framebuffer");
                if (_encoder == null)
                {
                    _width = texture.width; _height = texture.height;
                    string format = texture.format == TextureFormat.RGB24 ? "rgb24" : texture.format == TextureFormat.RGBA32 ? "rgba" : null;
                    if (format == null) throw new InvalidOperationException("Unsupported framebuffer format " + texture.format);
                    Directory.CreateDirectory(Path.GetDirectoryName(_path));
                    _encoder = new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo {
                        FileName = _encoderPath, UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardInput = true, RedirectStandardError = true,
                        Arguments = "-hide_banner -loglevel error -y -f rawvideo -pix_fmt " + format
                            + " -video_size " + texture.width + "x" + texture.height + " -framerate " + Fps
                            + " -i pipe:0 -an -vf \"vflip,pad=ceil(iw/2)*2:ceil(ih/2)*2\" -c:v libx264 -preset veryfast -threads 2 -crf 20 -pix_fmt yuv420p -movflags +faststart \"" + _path + "\""
                    }};
                    _encoder.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _error = e.Data; };
                    _encoder.Start(); _encoder.BeginErrorReadLine();
                    _writer = new Thread(WriteFrames) { IsBackground = true, Name = "AZ video encoder pipe" };
                    _writer.Start(); _start = now; _startGameTime = Time.unscaledTimeAsDouble;
                    Debug.Log("[VIDEO] Started " + _path + " framebuffer=" + texture.width + "x" + texture.height);
                }
                if (_encoder.HasExited) throw new InvalidOperationException("Encoder stopped: " + _error);
                if (texture.width != _width || texture.height != _height) throw new InvalidOperationException("Window resized during recording");
                var frame = new Frame { Bytes = texture.GetRawTextureData<byte>().ToArray(), Time = now - _start };
                if (_frames.TryAdd(frame)) _captured++; else _dropped++;
                return true;
            }
            catch (Exception error) { _error = error.Message; Debug.LogWarning("[VIDEO] Capture stopped: " + error.Message); Close(); return false; }
            finally { if (texture != null) Destroy(texture); }
        }

        void WriteFrames()
        {
            try
            {
                byte[] previous = null;
                foreach (var frame in _frames.GetConsumingEnumerable())
                {
                    int expected = (int)Math.Round(frame.Time * Fps);
                    while (previous != null && _encoded < expected) { _encoder.StandardInput.BaseStream.Write(previous, 0, previous.Length); _encoded++; }
                    _encoder.StandardInput.BaseStream.Write(frame.Bytes, 0, frame.Bytes.Length); _encoded++;
                    previous = frame.Bytes;
                }
                _encoder.StandardInput.Close();
            }
            catch (Exception error) { _error = error.Message; }
        }

        void OnApplicationQuit() => Close();
        void OnDestroy() => Close();
        void Close()
        {
            if (_closed) return; _closed = true;
            _frames.CompleteAdding();
            bool drained = _writer == null || _writer.Join(10000);
            if (_encoder != null)
            {
                if (!drained || !_encoder.WaitForExit(10000)) { _error = "Encoder finish timeout"; _encoder.Kill(); }
                else if (_encoder.ExitCode != 0) _error = "Encoder exit " + _encoder.ExitCode + ": " + _error;
                _encoder.Dispose();
            }
            if (!string.IsNullOrEmpty(_path))
                File.WriteAllText(_path + ".capture.txt", "source=Unity framebuffer\nfps=" + Fps + "\ncaptured=" + _captured
                    + "\nqueueDropped=" + _dropped + "\nencoded=" + _encoded
                    + "\nstartGameTime=" + _startGameTime.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\nerror=" + _error);
        }
    }
}
#endif
