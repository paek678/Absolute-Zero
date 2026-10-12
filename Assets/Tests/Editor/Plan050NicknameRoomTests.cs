using System.Collections.Generic;
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
