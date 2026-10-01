using System;
using Unity.Behavior;
using Unity.Properties;

namespace AbsoluteZero.Validation.Behavior
{
    // Tooling fixture only. This node never reads or changes game state.
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Plan036 Lifecycle Check", story: "Exercise lifecycle",
        category: "Action/Validation", id: "8e04d55f7f054282b2088123d7d2f161")]
    public partial class Plan036LifecycleAction : Unity.Behavior.Action
    {
        private Plan036BehaviorProbe Probe => GameObject.GetComponent<Plan036BehaviorProbe>();

        protected override void OnSetup() => Probe.Setups++;
        protected override void OnTeardown() => Probe.Teardowns++;

        protected override Status OnStart()
        {
            Probe.Starts++;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            Probe.Updates++;
            if (!Probe.AllowCompletion) return Status.Running;
            Probe.Completions++;
            return Status.Success;
        }

        protected override void OnEnd() => Probe.Ends++;
    }
}
