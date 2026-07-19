using Game.Client.Rendering.Graph;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class RenderPassGraphTests
{
    [Fact]
    public void Compile_ProducesDeterministicDependencyAndPhaseOrder()
    {
        var graph = CreateLinearGraph();

        var plan = graph.Compile([Resource(5)]);

        Assert.Equal(4, plan.PassCount);
        Assert.Equal(Pass(10), plan.GetPass(0).Id);
        Assert.Equal(Pass(20), plan.GetPass(1).Id);
        Assert.Equal(Pass(30), plan.GetPass(2).Id);
        Assert.Equal(Pass(40), plan.GetPass(3).Id);
        Assert.Equal(4, plan.Telemetry.DeclaredPasses);
        Assert.Equal(4, plan.Telemetry.ExecutedPasses);
        Assert.Equal(0, plan.Telemetry.CulledPasses);
    }

    [Fact]
    public void Compile_UsesPassIdAsStableTieBreakerWithinAPhase()
    {
        var graph = new RenderPassGraph();
        graph.DeclareResource(Resource(0), RenderResourceFlags.Imported);
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(2), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(3), RenderResourceFlags.None);
        graph.DeclarePass(Pass(7), RenderPassPhase.Opaque, [Resource(0)], [Resource(1)]);
        graph.DeclarePass(Pass(3), RenderPassPhase.Opaque, [Resource(0)], [Resource(2)]);
        graph.DeclarePass(
            Pass(12),
            RenderPassPhase.Composite,
            [Resource(1), Resource(2)],
            [Resource(3)]);

        var plan = graph.Compile([Resource(3)]);

        Assert.Equal(Pass(3), plan.GetPass(0).Id);
        Assert.Equal(Pass(7), plan.GetPass(1).Id);
        Assert.Equal(Pass(12), plan.GetPass(2).Id);
    }

    [Fact]
    public void Compile_CullsPassesThatDoNotContributeToRequestedOutputs()
    {
        var graph = new RenderPassGraph();
        graph.DeclareResource(Resource(0), RenderResourceFlags.Imported);
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(2), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(3), RenderResourceFlags.None);
        graph.DeclarePass(Pass(1), RenderPassPhase.Prepass, [Resource(0)], [Resource(1)]);
        graph.DeclarePass(Pass(2), RenderPassPhase.Opaque, [Resource(0)], [Resource(2)]);
        graph.DeclarePass(Pass(3), RenderPassPhase.Composite, [Resource(2)], [Resource(3)]);

        var plan = graph.Compile([Resource(3)]);

        Assert.Equal(2, plan.PassCount);
        Assert.Equal(Pass(2), plan.GetPass(0).Id);
        Assert.Equal(Pass(3), plan.GetPass(1).Id);
        Assert.Equal(1, plan.Telemetry.CulledPasses);
        Assert.False(plan.GetResourceLifetime(Resource(1)).IsUsed);
    }

    [Fact]
    public void Compile_PreservesNeverCullPassAndItsDependencies()
    {
        var graph = new RenderPassGraph();
        graph.DeclareResource(Resource(0), RenderResourceFlags.Imported);
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclarePass(
            Pass(1),
            RenderPassPhase.PostProcess,
            [Resource(0)],
            [Resource(1)],
            RenderPassFlags.NeverCull);

        var plan = graph.Compile([]);

        Assert.Equal(1, plan.PassCount);
        Assert.Equal(Pass(1), plan.GetPass(0).Id);
    }

    [Fact]
    public void Compile_RejectsReadWithoutProducer()
    {
        var graph = new RenderPassGraph();
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(2), RenderResourceFlags.None);
        graph.DeclarePass(Pass(1), RenderPassPhase.Composite, [Resource(1)], [Resource(2)]);

        var error = Assert.Throws<RenderGraphCompileException>(() => graph.Compile([Resource(2)]));

        Assert.Equal(RenderGraphCompileError.ReadBeforeProduce, error.Error);
        Assert.Equal(Pass(1), error.Pass);
        Assert.Equal(Resource(1), error.Resource);
    }

    [Fact]
    public void Compile_RejectsDependencyCycles()
    {
        var graph = new RenderPassGraph();
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(2), RenderResourceFlags.Transient);
        graph.DeclarePass(Pass(1), RenderPassPhase.Lighting, [Resource(2)], [Resource(1)]);
        graph.DeclarePass(Pass(2), RenderPassPhase.Lighting, [Resource(1)], [Resource(2)]);

        var error = Assert.Throws<RenderGraphCompileException>(() => graph.Compile([Resource(2)]));

        Assert.Equal(RenderGraphCompileError.Cycle, error.Error);
    }

    [Fact]
    public void Compile_RejectsMultipleResourceProducers()
    {
        var graph = new RenderPassGraph();
        graph.DeclareResource(Resource(0), RenderResourceFlags.Imported);
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclarePass(Pass(1), RenderPassPhase.Opaque, [Resource(0)], [Resource(1)]);
        graph.DeclarePass(Pass(2), RenderPassPhase.Opaque, [Resource(0)], [Resource(1)]);

        var error = Assert.Throws<RenderGraphCompileException>(() => graph.Compile([Resource(1)]));

        Assert.Equal(RenderGraphCompileError.MultipleProducers, error.Error);
        Assert.Equal(Resource(1), error.Resource);
    }

    [Fact]
    public void Compile_RejectsDependenciesThatRunBackwardAcrossPhases()
    {
        var graph = new RenderPassGraph();
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(2), RenderResourceFlags.None);
        graph.DeclarePass(Pass(1), RenderPassPhase.PostProcess, [], [Resource(1)]);
        graph.DeclarePass(Pass(2), RenderPassPhase.Opaque, [Resource(1)], [Resource(2)]);

        var error = Assert.Throws<RenderGraphCompileException>(() => graph.Compile([Resource(2)]));

        Assert.Equal(RenderGraphCompileError.PhaseOrderViolation, error.Error);
    }

    [Fact]
    public void Compile_PlansNonOverlappingTransientResourcesIntoAliasSlots()
    {
        var graph = CreateLinearGraph();

        var plan = graph.Compile([Resource(5)]);
        var first = plan.GetResourceLifetime(Resource(1));
        var second = plan.GetResourceLifetime(Resource(2));
        var third = plan.GetResourceLifetime(Resource(3));

        Assert.Equal((0, 1), (first.FirstPassIndex, first.LastPassIndex));
        Assert.Equal((1, 2), (second.FirstPassIndex, second.LastPassIndex));
        Assert.Equal((2, 3), (third.FirstPassIndex, third.LastPassIndex));
        Assert.Equal(first.AliasSlot, third.AliasSlot);
        Assert.NotEqual(first.AliasSlot, second.AliasSlot);
        Assert.Equal(3, plan.Telemetry.UsedTransientResources);
        Assert.Equal(2, plan.Telemetry.TransientAliasSlots);
        Assert.Equal(1, plan.Telemetry.SavedTransientSlots);
    }

    [Fact]
    public void Compile_ExtendsRequestedTransientOutputLifetimeThroughGraphEnd()
    {
        var graph = CreateLinearGraph();

        var plan = graph.Compile([Resource(1), Resource(5)]);
        var requestedIntermediate = plan.GetResourceLifetime(Resource(1));
        var laterTransient = plan.GetResourceLifetime(Resource(3));

        Assert.Equal(plan.PassCount, requestedIntermediate.LastPassIndex);
        Assert.NotEqual(requestedIntermediate.AliasSlot, laterTransient.AliasSlot);
        Assert.Equal(3, plan.Telemetry.TransientAliasSlots);
        Assert.Equal(0, plan.Telemetry.SavedTransientSlots);
    }

    [Fact]
    public void Execute_VisitsOnlyCompiledPassesInPlanOrder()
    {
        var graph = CreateLinearGraph();
        var plan = graph.Compile([Resource(5)]);
        var executor = new RecordingExecutor(new RenderPassId[4]);

        var executed = plan.Execute(ref executor);

        Assert.Equal(4, executed);
        Assert.Equal([Pass(10), Pass(20), Pass(30), Pass(40)], executor.Passes);
    }

    [Fact]
    public void Compile_InvalidatesPreviouslyPublishedPlanView()
    {
        var graph = CreateLinearGraph();
        var stale = graph.Compile([Resource(5)]);

        _ = graph.Compile([Resource(5)]);

        Assert.Throws<InvalidOperationException>(() => stale.GetPass(0));
    }

    [Fact]
    public void FailedCompile_AlsoInvalidatesPreviouslyPublishedPlanView()
    {
        var graph = CreateLinearGraph();
        var stale = graph.Compile([Resource(5)]);

        var error = Assert.Throws<RenderGraphCompileException>(() => graph.Compile([Resource(0)]));

        Assert.Equal(RenderGraphCompileError.RequestedOutputNotProduced, error.Error);
        Assert.Throws<InvalidOperationException>(() => stale.GetPass(0));
    }

    private static RenderPassGraph CreateLinearGraph()
    {
        var graph = new RenderPassGraph();
        graph.DeclareResource(Resource(0), RenderResourceFlags.Imported);
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(2), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(3), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(5), RenderResourceFlags.None);

        // Declaration order deliberately differs from dependency order.
        graph.DeclarePass(Pass(40), RenderPassPhase.Composite, [Resource(3)], [Resource(5)]);
        graph.DeclarePass(Pass(10), RenderPassPhase.Opaque, [Resource(0)], [Resource(1)]);
        graph.DeclarePass(Pass(30), RenderPassPhase.PostProcess, [Resource(2)], [Resource(3)]);
        graph.DeclarePass(Pass(20), RenderPassPhase.Lighting, [Resource(1)], [Resource(2)]);
        return graph;
    }

    private static RenderPassId Pass(int value) => new(value);

    private static RenderResourceId Resource(int value) => new(value);

    private struct RecordingExecutor : IRenderPassExecutor
    {
        private int _count;

        public RecordingExecutor(RenderPassId[] passes)
        {
            Passes = passes;
            _count = 0;
        }

        public RenderPassId[] Passes { get; }

        public void Execute(in RenderPassDescriptor pass)
        {
            Passes[_count++] = pass.Id;
        }
    }
}
