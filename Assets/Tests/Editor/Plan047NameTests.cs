using System.Text;
using AbsoluteZero.Core.Cosmetic;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public sealed class Plan047NameTests
    {
        [TestCase(" 가나12 ", true, "가나12")]
        [TestCase("12345678", true, "12345678")]
        [TestCase("가나다라마바사아자", false, null)]
        [TestCase("가", false, null)]
        [TestCase("<b>abc</b>", false, null)]
        [TestCase("a\nb", false, null)]
        [TestCase("", false, null)]
        [TestCase(null, false, null)]
        public void HumanRulesAreSharedAndBounded(string input, bool success, string expected)
        {
            Assert.That(ParticipantDisplayName.TryHuman(input, out var actual), Is.EqualTo(success));
            Assert.That(actual, Is.EqualTo(expected));
        }
        [Test] public void BotAllowsSpacesAndBoundsWholeCharacters()
        {
            Assert.That(ParticipantDisplayName.Bot("연습 로봇"), Is.EqualTo("연습 로봇"));
            var name = ParticipantDisplayName.Bot(new string('봇', 100));
            Assert.That(Encoding.UTF8.GetByteCount(name), Is.LessThanOrEqualTo(60));
            Assert.That(name.IndexOf('\uFFFD'), Is.EqualTo(-1));
            Assert.That(ParticipantDisplayName.Bot("\n\t"), Is.EqualTo("봇"));
        }
        [TestCase(" 가나다 ", "123456789", "가나다")]
        [TestCase("Name1234", null, "Name1234")]
        [TestCase("a", "123456789", "Player_123456")]
        [TestCase("<b>A</b>", "ab", "Player_ab")]
        [TestCase("", null, "Player_Unknown")]
        public void LobbyUsesValidatedProfileNameAndSafeFallback(string nickname, string id, string expected)
            => Assert.That(ParticipantDisplayName.Lobby(nickname, id), Is.EqualTo(expected));
    }
}
