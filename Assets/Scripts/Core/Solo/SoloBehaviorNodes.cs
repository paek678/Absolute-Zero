using System;
using Unity.Behavior;
using Unity.Properties;

namespace AbsoluteZero.Core.Solo
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Capture own and public observation", story: "Capture own and public observation", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000001")]
    public partial class SoloObserve : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().CaptureObservation() ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Build legal own item candidates", story: "Build legal own item candidates", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000002")]
    public partial class SoloCandidates : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().BuildCandidates() ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Submit delayed item request", story: "Submit delayed item request", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000003")]
    public partial class SoloSubmitUse : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().Submit() ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Choose Ready timing", story: "Choose Ready timing", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000004")]
    public partial class SoloChooseReady : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().ChooseReadyTime() ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Submit authoritative Ready", story: "Submit authoritative Ready", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000005")]
    public partial class SoloReady : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().SubmitReady() ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Fallback Ready without item", story: "Fallback Ready without item", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000006")]
    public partial class SoloFallback : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().Fallback() ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Wait for bounded thinking", story: "Wait for bounded thinking", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000007")]
    public partial class SoloThinking : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().Think();
        protected override Status OnUpdate() => GameObject.GetComponent<BotTurnController>().Think();
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Await authoritative item result", story: "Await authoritative item result", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000008")]
    public partial class SoloAwaitUse : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().AwaitUse();
        protected override Status OnUpdate() => GameObject.GetComponent<BotTurnController>().AwaitUse();
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Wait for safe Ready time", story: "Wait for safe Ready time", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000009")]
    public partial class SoloWaitReady : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().WaitReady();
        protected override Status OnUpdate() => GameObject.GetComponent<BotTurnController>().WaitReady();
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Survival tactic", story: "Survival tactic", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000010")]
    public partial class SoloSurvival : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().Choose(BotTactic.Survival) ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Finish tactic", story: "Finish tactic", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000011")]
    public partial class SoloFinish : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().Choose(BotTactic.Finish) ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Counter tactic", story: "Counter tactic", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000012")]
    public partial class SoloCounter : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().Choose(BotTactic.Counter) ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Resource tactic", story: "Resource tactic", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000013")]
    public partial class SoloResource : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().Choose(BotTactic.Resource) ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "General tactic", story: "General tactic", category: "Action/Absolute Zero/Solo", id: "a0360000000000000000000000000014")]
    public partial class SoloGeneral : Unity.Behavior.Action
    {
        protected override Status OnStart() => GameObject.GetComponent<BotTurnController>().Choose(BotTactic.General) ? Status.Success : Status.Failure;
    }
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Bounded Solo retry", story: "Retry within profile and Prep window", category: "Flow/Absolute Zero", id: "a0360000000000000000000000000099")]
    public partial class SoloRetry : Modifier
    {
        int _retries;
        protected override Status OnStart() { _retries = 0; return StartChild(); }
        Status StartChild()
        {
            var result = StartNode(Child);
            return result == Status.Running ? Status.Waiting : result == Status.Failure ? Status.Running : result;
        }
        protected override Status OnUpdate()
        {
            if (Child.CurrentStatus == Status.Success) return Status.Success;
            if (Child.CurrentStatus != Status.Failure) return Status.Waiting;
            var controller = GameObject.GetComponent<BotTurnController>();
            if (!controller.CanAct() || _retries++ >= controller.RetryBudget) return Status.Failure;
            return StartChild();
        }
    }
}

