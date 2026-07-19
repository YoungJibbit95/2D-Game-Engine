namespace Game.Client.Rendering.Graph;

public readonly record struct RenderPassId(int Value)
{
    public override string ToString() => $"Pass({Value})";
}

public readonly record struct RenderResourceId(int Value)
{
    public override string ToString() => $"Resource({Value})";
}

public enum RenderPassPhase : byte
{
    Prepass,
    Opaque,
    Lighting,
    Transparent,
    PostProcess,
    Composite,
    Overlay
}

[Flags]
public enum RenderPassFlags : byte
{
    None = 0,
    NeverCull = 1 << 0
}

[Flags]
public enum RenderResourceFlags : byte
{
    None = 0,
    Imported = 1 << 0,
    Transient = 1 << 1
}

public readonly record struct RenderPassDescriptor(
    RenderPassId Id,
    RenderPassPhase Phase,
    RenderPassFlags Flags);

public readonly record struct RenderResourceDescriptor(
    RenderResourceId Id,
    RenderResourceFlags Flags);

public readonly record struct RenderResourceLifetime(
    RenderResourceId Resource,
    bool IsUsed,
    int FirstPassIndex,
    int LastPassIndex,
    int AliasSlot)
{
    public bool IsTransient => AliasSlot >= 0;
}

public readonly record struct RenderGraphTelemetry(
    int DeclaredPasses,
    int ExecutedPasses,
    int CulledPasses,
    int DeclaredResources,
    int UsedTransientResources,
    int TransientAliasSlots,
    int SavedTransientSlots);

public enum RenderGraphCompileError
{
    MultipleProducers,
    ReadBeforeProduce,
    Cycle,
    PhaseOrderViolation,
    RequestedOutputNotProduced,
    WriteToImportedResource
}

public sealed class RenderGraphCompileException : InvalidOperationException
{
    public RenderGraphCompileException(
        RenderGraphCompileError error,
        string message,
        RenderPassId pass = default,
        RenderResourceId resource = default)
        : base(message)
    {
        Error = error;
        Pass = pass;
        Resource = resource;
    }

    public RenderGraphCompileError Error { get; }

    public RenderPassId Pass { get; }

    public RenderResourceId Resource { get; }
}

public interface IRenderPassExecutor
{
    void Execute(in RenderPassDescriptor pass);
}

/// <summary>
/// Allocation-free view over a compiled plan. The view is invalidated by the
/// next Compile or Clear call on its owning graph.
/// </summary>
public readonly struct CompiledRenderGraphPlan
{
    private readonly RenderPassGraph? _owner;
    private readonly int _generation;

    internal CompiledRenderGraphPlan(
        RenderPassGraph owner,
        int generation,
        RenderGraphTelemetry telemetry)
    {
        _owner = owner;
        _generation = generation;
        Telemetry = telemetry;
    }

    public RenderGraphTelemetry Telemetry { get; }

    public int PassCount => Telemetry.ExecutedPasses;

    public RenderPassDescriptor GetPass(int index)
    {
        return GetOwner().GetCompiledPass(_generation, index);
    }

    public RenderResourceLifetime GetResourceLifetime(RenderResourceId resource)
    {
        return GetOwner().GetCompiledResourceLifetime(_generation, resource);
    }

    public int Execute<TExecutor>(ref TExecutor executor)
        where TExecutor : IRenderPassExecutor
    {
        var owner = GetOwner();
        for (var index = 0; index < Telemetry.ExecutedPasses; index++)
        {
            var pass = owner.GetCompiledPass(_generation, index);
            executor.Execute(pass);
        }

        return Telemetry.ExecutedPasses;
    }

    private RenderPassGraph GetOwner()
    {
        return _owner ?? throw new InvalidOperationException("The render-graph plan is not initialized.");
    }
}
