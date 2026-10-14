using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using AbsoluteZero.UI.LobbyUI;
using NUnit.Framework;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.Tests
{
    public sealed class Plan050NicknameRoomTests
    {
        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void FailedJoinReadyRestoresVisibleEntryControlsWithoutEndingOnlineLease(int count)
        {
            WithRoom((root, room, coordinator, router, lease, presenter) =>
            {
                room.SetPlayerCount(count);
                room.ApplyMembership(true);
                room.SetLobbyCode("OLD123");
                room.SetCreateInteractable(false);
                room.SetJoinInteractable(false);
                room.SetStartVisible(true);
                SetCoordinator(coordinator, "_lastError", "controlled connection failure");
                Assert.IsFalse(Button(root, "CreateBtn").gameObject.activeInHierarchy);
                Assert.IsFalse(Button(root, "JoinBtn").gameObject.activeInHierarchy);

                // Compensation has already cleared CurrentLobby; the presenter receives
                // the ordinary Ready notification while its original online lease lives.
                NotifyReady(presenter);

                Assert.IsTrue(Button(root, "CreateBtn").gameObject.activeInHierarchy);
                Assert.IsTrue(Button(root, "CreateBtn").interactable);
                Assert.IsTrue(Button(root, "JoinBtn").gameObject.activeInHierarchy);
                Assert.IsTrue(Button(root, "JoinBtn").interactable);
                var input = root.transform.Find("PanelBG/JoinInput").GetComponent<TMP_InputField>();
                Assert.IsTrue(input.gameObject.activeInHierarchy);
                Assert.IsTrue(input.interactable);
                Assert.IsFalse(Button(root, "StartBtn").gameObject.activeInHierarchy);
                Assert.AreEqual("------", root.transform.Find("PanelBG/LobbyCode/CodeText").GetComponent<TMP_Text>().text);
                Assert.AreEqual("controlled connection failure", root.transform.Find("PanelBG/StatusText").GetComponent<TMP_Text>().text);
                int creates = 0, joins = 0;
                room.OnCreateClicked += () => creates++;
                room.OnJoinClicked += _ => joins++;
                Button(root, "CreateBtn").onClick.Invoke();
                Button(root, "JoinBtn").onClick.Invoke();
                Assert.AreEqual(1, creates); Assert.AreEqual(1, joins);
                Assert.AreSame(lease, router.Current);
                Assert.IsTrue(coordinator.HasOnlineOwnership);
                Assert.IsFalse(router.IsStopping);
                Assert.IsNull(coordinator.CurrentLobby);
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void ReadyWithMembershipKeepsJoinControlsHiddenAndHostStartVisible(bool host)
        {
            WithRoom((root, room, coordinator, router, lease, presenter) =>
            {
                SetCoordinator(coordinator, "_currentLobby", new Lobby(id: "retained", lobbyCode: "ROOM12", maxPlayers: 2));
                SetCoordinator(coordinator, "_isHostRole", host);
                room.ApplyMembership(true); room.SetLobbyCode("ROOM12"); room.SetStartVisible(host);
                NotifyReady(presenter);
                Assert.IsFalse(Button(root, "CreateBtn").gameObject.activeInHierarchy);
                Assert.IsFalse(Button(root, "JoinBtn").gameObject.activeInHierarchy);
                Assert.IsFalse(root.transform.Find("PanelBG/JoinInput").gameObject.activeInHierarchy);
                Assert.AreEqual(host, Button(root, "StartBtn").gameObject.activeInHierarchy);
                Assert.AreEqual(host, Button(root, "StartBtn").interactable);
                Assert.AreEqual("ROOM12", root.transform.Find("PanelBG/LobbyCode/CodeText").GetComponent<TMP_Text>().text);
                Assert.AreSame(lease, router.Current);
                Assert.AreEqual("retained", coordinator.CurrentLobby.Id);
            });
        }

        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void FailedJoinReadyClearsActualRowsButKeepsEnteredCodeAndAllowsNewRoom(int count)
        {
            WithRoom((root, room, coordinator, router, lease, presenter) =>
            {
                Lobby Room(string prefix)
                {
                    var players = new List<Player>();
                    for (int i = 0; i < count; i++) players.Add(new Player(id: prefix + i,
                        data: new Dictionary<string, PlayerDataObject>
                        { ["PlayerName"] = new(PlayerDataObject.VisibilityOptions.Public, prefix + i) }));
                    return new Lobby(id: prefix, maxPlayers: count, hostId: prefix + "0", players: players);
                }
                room.RenderPlayerList(Room("previous"), "previous0"); room.ApplyMembership(true);
                var list = root.transform.Find("PanelBG/PlayerList");
                Assert.AreEqual(count, list.childCount);
                var oldRows = new List<GameObject>();
                foreach (Transform row in list) oldRows.Add(row.gameObject);
                var input = root.transform.Find("PanelBG/JoinInput").GetComponent<TMP_InputField>();
                input.SetTextWithoutNotify("RETRY1");

                NotifyReady(presenter);

                Assert.AreEqual(0, list.childCount, "Abandoned room participants must not remain in the empty Room view.");
                Assert.IsTrue(oldRows.TrueForAll(row => row == null || !row.activeInHierarchy));
                Assert.AreEqual("RETRY1", input.text, "Visual membership recovery must preserve the entered retry code.");
                Assert.AreSame(lease, router.Current);
                NotifyReady(presenter); // A repeated status notification remains harmless.
                room.RenderPlayerList(Room("newroom"), "newroom0"); room.ApplyMembership(true);
                Assert.AreEqual(count, list.childCount);
                foreach (Transform row in list)
                    Assert.That(row.GetComponentInChildren<TMP_Text>().text, Does.StartWith("newroom"));
                Assert.IsFalse(Button(root, "JoinBtn").gameObject.activeInHierarchy);
            });
        }

        static void WithRoom(System.Action<GameObject, LobbyRoomView, NetworkSessionCoordinator,
            MatchSessionRouter, MatchSessionLease, LobbyPresenter> check)
        {
            var root = new GameObject("RecoveryRoom", typeof(RectTransform));
            var owner = new GameObject("RecoveryCoordinator");
            try
            {
                var panel = Child(root.transform, "PanelBG");
                foreach (string name in new[] { "CreateBtn", "JoinBtn", "StartBtn" })
                    Child(panel, name).gameObject.AddComponent<UnityEngine.UI.Button>();
                Child(panel, "JoinInput").gameObject.AddComponent<TMP_InputField>();
                var code = Child(panel, "LobbyCode");
                Child(code, "CodeText").gameObject.AddComponent<TextMeshProUGUI>();
                Child(panel, "StatusText").gameObject.AddComponent<TextMeshProUGUI>();
                Child(panel, "PlayerList").gameObject.AddComponent<VerticalLayoutGroup>();
                var room = new LobbyRoomView(root);
                var coordinator = owner.AddComponent<NetworkSessionCoordinator>();
                var router = coordinator.Router = new MatchSessionRouter();
                Assert.IsTrue(router.TryBegin(GameMode.OneVsOne, () => Task.CompletedTask, out var lease));
                SetCoordinator(coordinator, "_lease", lease);
                SetCoordinator(coordinator, "_state", SessionState.Ready);
                // Only this callback's real Room view is needed; no network command or
                // unrelated settings/closet lifetime is initialized by the fixture.
                var presenter = new LobbyPresenter(null, null, room, null, null);
                typeof(LobbyPresenter).GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(presenter, coordinator);
                check(root, room, coordinator, router, lease, presenter);
                router.End(lease);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(owner); }
        }
        static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform)); child.transform.SetParent(parent, false); return child.transform;
        }
        static UnityEngine.UI.Button Button(GameObject root, string name)
            => root.transform.Find("PanelBG/" + name).GetComponent<UnityEngine.UI.Button>();
        static void SetCoordinator(NetworkSessionCoordinator coordinator, string name, object value)
            => typeof(NetworkSessionCoordinator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(coordinator, value);
        static void NotifyReady(LobbyPresenter presenter)
            => typeof(LobbyPresenter).GetMethod("OnCoordinatorStateChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(presenter, new object[] { SessionState.Ready, SessionOperation.None });

        [TestCase(2, true)] [TestCase(3, true)] [TestCase(4, true)]
        [TestCase(2, false)] [TestCase(3, false)] [TestCase(4, false)]
        public void EveryNicknameFitsTheAuthoredRoomList(int count, bool controlHeight)
        {
            var root = new GameObject("Room", typeof(RectTransform));
            try
            {
                var panel = new GameObject("PanelBG", typeof(RectTransform)); panel.transform.SetParent(root.transform, false);
                var mode = new GameObject("ModeLabel", typeof(TextMeshProUGUI)); mode.transform.SetParent(panel.transform, false);
                var list = new GameObject("PlayerList", typeof(RectTransform), typeof(VerticalLayoutGroup));
                list.transform.SetParent(panel.transform, false);
                var rect = list.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(500, 180);
                var layout = list.GetComponent<VerticalLayoutGroup>();
                layout.spacing = 8; layout.childControlHeight = controlHeight; layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
                var players = new List<Player>();
                for (int i = 0; i < count; i++) players.Add(new Player(id: "id" + i, data: new Dictionary<string, PlayerDataObject>
                { ["PlayerName"] = new(PlayerDataObject.VisibilityOptions.Public, "이름검증" + i) }));
                new LobbyRoomView(root).RenderPlayerList(new Lobby(id: "test", maxPlayers: count, hostId: "id0", players: players), "id0");
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(rect.childCount, Is.EqualTo(count));
                for (int i = 0; i < count; i++)
                {
                    var row = (RectTransform)rect.GetChild(i);
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, row);
                    Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(rect.rect.yMin - .01f));
                    Assert.That(bounds.max.y, Is.LessThanOrEqualTo(rect.rect.yMax + .01f));
                    Assert.That(row.GetComponentInChildren<TMP_Text>().text, Does.StartWith("이름검증" + i));
                }
                Assert.That(mode.GetComponent<TMP_Text>().text, Is.EqualTo(count == 2 ? "1 대 1 대결" : count + "인 대결"));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
