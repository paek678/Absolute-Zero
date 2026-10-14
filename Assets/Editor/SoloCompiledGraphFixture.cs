using System;
using System.Linq;
using Unity.Behavior;
using UnityEditor;
using UnityEngine;

// Editor-only control-flow fixture. Clones the saved production graph, retaining its
// composites, guards, conditions and retry decorator. Leaf command results are supplied
// by the test: this is not evidence of network admission or real item consumption.
public static class SoloCompiledGraphFixture
{
    [Serializable]
    sealed class Leaf : Unity.Behavior.Action
    {
        internal Func<string, Node.Status> Execute;
        internal string Label;
        protected override Status OnStart() => Execute(Label);
        protected override Status OnUpdate() => Execute(Label);
    }

    public static string Run(GameObject owner, Func<string, Node.Status> execute)
    {
        var source = AssetDatabase.LoadAllAssetsAtPath(Plan046SoloBotBuilder.GraphPath).OfType<BehaviorGraph>().Single();
        Plan046SoloBotBuilder.ValidateRuntimeTopology(source);
        var instance = UnityEngine.Object.Instantiate(source);
        try
        {
            Node Replace(Node node)
            {
                if (node is Composite composite)
                {
                    var children = composite.Children.ToArray();
                    composite.Children.Clear();
                    foreach (var child in children) composite.Add(Replace(child));
                }
                else if (node is Modifier modifier) modifier.Add(Replace(modifier.Child));
                else if (node.GetType().Namespace == "AbsoluteZero.Core.Solo")
                    return new Leaf { Label = node.GetType().Name, Execute = execute };
                return node;
            }
            foreach (var module in instance.Graphs)
            {
                module.Root = Replace(module.Root);
                module.GameObject = owner;
                module.InitializeNodes();
            }
            instance.Start();
            // Synchronous EditMode calls share Time.frameCount. The package Start node
            // waits for a later frame even when Repeat=false. Advance only its private
            // frame stamp; keep production Repeat/child/guard/retry behavior unchanged.
            var startFrame = instance.RootGraph.Root.GetType().GetField("m_CurrentFrame",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (startFrame == null) throw new InvalidOperationException("Installed Start frame contract changed.");
            void TickFrame()
            {
                startFrame.SetValue(instance.RootGraph.Root, Time.frameCount - 1);
                instance.Tick();
            }
            int ticks = 0;
            while (instance.IsRunning && ticks++ < 64) TickFrame();
            if (instance.IsRunning)
            {
                string Describe(Node node) => node.GetType().Name + ":" + node.CurrentStatus
                    + (node is Composite c ? "[" + string.Join(",", c.Children.Select(Describe)) + "]"
                       : node is Modifier m ? "[" + Describe(m.Child) + "]" : node is Leaf l ? "(" + l.Label + ")" : "");
                throw new InvalidOperationException("Compiled graph failed to terminate within 64 ticks: " + Describe(instance.RootGraph.Root));
            }
            var status = instance.CurrentStatus.ToString();
            // Single-shot Start must not execute again just because more frames pass.
            for (int i = 0; i < 5; i++) TickFrame();
            return status;
        }
        finally
        {
            instance.End();
            foreach (var module in instance.Graphs) module.TeardownNodes();
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
