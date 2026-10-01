#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class MatrixScenarioProbe
    {
        IEnumerator VerifyLobbyPublication(NetworkSessionCoordinator coordinator)
        {
            if (Arg("--plan037-lobby-publication", "0") != "1") yield break;
            string expected = CosmeticProfileService.Instance.GetCompactDto();
            var a = coordinator.PublishCosmeticsAsync("", 1001);
            var b = coordinator.PublishCosmeticsAsync(expected, 1002);
            var c = coordinator.PublishCosmeticsAsync(expected, 1003);
            var all = Task.WhenAll(a, b, c);
            yield return WaitForRelayTask(all, 30f, "Cosmetic publication drain");
            if (_done) yield break;
            if (!all.IsCompletedSuccessfully ||
                (a.Result.Status != CosmeticPublicationStatus.Published && a.Result.Status != CosmeticPublicationStatus.Superseded) ||
                b.Result.Status != CosmeticPublicationStatus.Superseded || c.Result.Status != CosmeticPublicationStatus.Published)
            { Fail("Cosmetic publication did not settle A/B/C correctly"); yield break; }

            var work = coordinator.LobbyWork;
            yield return WaitForRelayTask(work.PollAsync(), 15f, "Confirmed cosmetic readback");
            if (_done) yield break;
            var manager = LobbyManager.Instance;
            var lobby = coordinator.CurrentLobby;
            var player = lobby.Players?.FirstOrDefault(p => p.Id == manager.PlayerId);
            if (!ReferenceEquals(lobby, manager.CurrentLobby) || player?.Data == null ||
                !player.Data.TryGetValue("CosmeticData", out var value) || value.Value != expected ||
                work.ConfirmedRevision != 1003)
            { Fail("Cosmetic publication cache/readback mismatch"); yield break; }
            Debug.Log($"[MATRIX] LOBBY_PUBLICATION_PASS revision=1003 version={lobby.Version} superseded=1002");
        }
    }
}
#endif
