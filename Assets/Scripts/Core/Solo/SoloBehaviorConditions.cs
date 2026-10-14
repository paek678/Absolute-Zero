using System;
using Unity.Behavior;
using Unity.Properties;

namespace AbsoluteZero.Core.Solo
{
    // Read-only checks over the controller's latest score sheet, for built-in
    // Conditional Guard nodes. They never choose or submit an item.
    [Serializable, GeneratePropertyBag]
    [Condition(name: "Bot is in mortal danger", story: "Bot is in mortal danger", category: "Conditions/Absolute Zero/Solo", id: "a0460000000000000000000000000101")]
    public partial class SoloInCrisis : Condition
    {
        public override bool IsTrue()
            => GameObject != null && GameObject.TryGetComponent<BotTurnController>(out var controller) && controller.InCrisis;
    }

    [Serializable, GeneratePropertyBag]
    [Condition(name: "Lethal attack must be taken", story: "Lethal attack must be taken", category: "Conditions/Absolute Zero/Solo", id: "a0460000000000000000000000000102")]
    public partial class SoloMustFinish : Condition
    {
        public override bool IsTrue()
            => GameObject != null && GameObject.TryGetComponent<BotTurnController>(out var controller) && controller.MustTakeLethal;
    }
}
