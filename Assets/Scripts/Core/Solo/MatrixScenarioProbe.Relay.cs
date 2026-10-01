#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Session;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class MatrixScenarioProbe
    {
        IEnumerator StartRelayMatrixFlow()
        {
            string coordinationFile = Arg("--az-coordination-file", "");
            if (_reentryStarted) coordinationFile += ".reentry";
            if (string.IsNullOrWhiteSpace(coordinationFile))
            {
                Fail("Relay coordination file missing");
                yield break;
            }

            var coordinator = NetworkSessionCoordinator.Instance;
            var initialization = coordinator.EnsureInitializedAsync();
            float initializationDeadline = Time.realtimeSinceStartup + 45f;
            while (!initialization.IsCompleted)
            {
                if (Time.realtimeSinceStartup >= initializationDeadline)
                {
                    Fail("Relay Services initialization timeout");
                    yield break;
                }
                yield return null;
            }
            if (!RelayTaskSucceeded(initialization) || coordinator.State != SessionState.Ready)
            {
                Fail("Relay Services initialization failed: " + coordinator.LastError);
                yield break;
            }

            if (_role == "host")
            {
                Task<Result<Unit>> create = coordinator.CreateLobbyAsync("AZ_Matrix_" + _token.Substring(0, 8));
                yield return WaitForRelayTask(create, 45f, "Create lobby");
                if (_done) yield break;
                if (!RelayTaskSucceeded(create))
                {
                    Fail("Create lobby failed: " + RelayTaskError(create));
                    yield break;
                }
                string code = coordinator.CurrentLobby?.LobbyCode;
                if (string.IsNullOrEmpty(code))
                {
                    Fail("Lobby did not provide a code");
                    yield break;
                }
                string fullPath = Path.GetFullPath(coordinationFile);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                File.WriteAllText(fullPath + ".tmp", code, Encoding.UTF8);
                File.Move(fullPath + ".tmp", fullPath);
                Debug.Log("[MATRIX] RELAY_LOBBY_READY");

                float populationDeadline = Time.realtimeSinceStartup + 75f;
                while ((coordinator.CurrentLobby?.Players?.Count ?? 0) < _count)
                {
                    if (Time.realtimeSinceStartup >= populationDeadline)
                    {
                        Fail("Relay lobby population timeout");
                        yield break;
                    }
                    yield return null;
                }
                Task<Result<Unit>> start = coordinator.StartMatchAsHostAsync();
                yield return WaitForRelayTask(start, 90f, "Relay host start");
                if (_done) yield break;
                if (!RelayTaskSucceeded(start))
                {
                    Fail("Relay host start failed: " + RelayTaskError(start));
                    yield break;
                }
                Debug.Log("[MATRIX] RELAY_HOST_LISTENING");
            }
            else
            {
                string code = null;
                float codeDeadline = Time.realtimeSinceStartup + 45f;
                while (string.IsNullOrWhiteSpace(code))
                {
                    if (File.Exists(coordinationFile))
                        code = File.ReadAllText(coordinationFile).Trim();
                    if (Time.realtimeSinceStartup >= codeDeadline)
                    {
                        Fail("Relay lobby code timeout");
                        yield break;
                    }
                    yield return null;
                }
                Task<Result<Unit>> join = coordinator.JoinGameAsync(code);
                yield return WaitForRelayTask(join, 90f, "Relay client join");
                if (_done) yield break;
                if (!RelayTaskSucceeded(join))
                {
                    Fail("Relay client join failed: " + RelayTaskError(join));
                    yield break;
                }
                Debug.Log("[MATRIX] RELAY_CLIENT_CONNECTED");
            }
            yield return VerifyLobbyPublication(coordinator);
        }

        IEnumerator WaitForRelayTask(Task task, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Fail(operation + " timed out");
                    yield break;
                }
                yield return null;
            }
        }

        static bool RelayTaskSucceeded(Task<Result<Unit>> task)
            => task.IsCompletedSuccessfully && task.Result.IsSuccess;

        static string RelayTaskError(Task<Result<Unit>> task)
        {
            if (task.IsCanceled) return "task cancelled";
            if (task.IsFaulted) return task.Exception?.GetBaseException().Message ?? "task faulted";
            return task.Result.ErrorMessage ?? task.Result.ErrorCode.ToString();
        }
    }
}
#endif
