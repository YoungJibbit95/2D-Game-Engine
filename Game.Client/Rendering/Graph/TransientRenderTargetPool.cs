using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering.Graph;

public readonly record struct TransientRenderTargetDescriptor(
    int Width,
    int Height,
    bool MipMap = false,
    SurfaceFormat SurfaceFormat = SurfaceFormat.Color,
    DepthFormat DepthFormat = DepthFormat.None,
    int MultiSampleCount = 0,
    RenderTargetUsage Usage = RenderTargetUsage.DiscardContents,
    int EstimatedColorBytesPerPixel = 4,
    int EstimatedDepthBytesPerPixel = 0)
{
    public long EstimatedByteCount
    {
        get
        {
            var samples = Math.Max(1, MultiSampleCount);
            var bytesPerPixel =
                (long)EstimatedColorBytesPerPixel + EstimatedDepthBytesPerPixel;
            var baseBytes = SaturatingMultiply(
                SaturatingMultiply(Width, Height),
                SaturatingMultiply(bytesPerPixel, samples));
            var mipTail = baseBytes / 3 + (baseBytes % 3 == 0 ? 0 : 1);
            return MipMap
                ? SaturatingAdd(baseBytes, mipTail)
                : baseBytes;
        }
    }

    internal void Validate()
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Render-target dimensions must be positive.");
        }

        if (MultiSampleCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MultiSampleCount));
        }

        if (!Enum.IsDefined(SurfaceFormat) ||
            !Enum.IsDefined(DepthFormat) ||
            !Enum.IsDefined(Usage))
        {
            throw new ArgumentOutOfRangeException(nameof(SurfaceFormat));
        }

        if (EstimatedColorBytesPerPixel <= 0 || EstimatedColorBytesPerPixel > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(EstimatedColorBytesPerPixel));
        }

        if (EstimatedDepthBytesPerPixel < 0 || EstimatedDepthBytesPerPixel > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(EstimatedDepthBytesPerPixel));
        }
    }

    private static long SaturatingMultiply(long left, long right)
    {
        if (left <= 0 || right <= 0)
        {
            return 0;
        }

        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }

    private static long SaturatingAdd(long left, long right)
    {
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}

public readonly record struct TransientRenderTargetBinding(
    RenderResourceId Resource,
    TransientRenderTargetDescriptor Descriptor);

public readonly record struct TransientRenderTargetPoolTelemetry(
    int Generation,
    int LogicalResourceCount,
    int PhysicalTargetCount,
    int AliasBindingsSaved,
    long EstimatedResidentBytes,
    long CreatedTargetCount,
    long ReusedTargetCount,
    long ReleasedTargetCount);

/// <summary>
/// Generation-bound mapping between a compiled graph and physical targets.
/// The lease is invalidated by graph recompilation, pool preparation, release,
/// resize, device replacement or disposal.
/// </summary>
public readonly struct TransientRenderTargetPoolLease
{
    internal TransientRenderTargetPoolLease(
        TransientRenderTargetPool owner,
        int generation,
        CompiledRenderGraphPlan plan)
    {
        Owner = owner;
        Generation = generation;
        Plan = plan;
    }

    internal TransientRenderTargetPool? Owner { get; }

    internal int Generation { get; }

    internal CompiledRenderGraphPlan Plan { get; }

    public bool IsInitialized => Owner is not null;
}

/// <summary>
/// Fixed-capacity descriptor-keyed RenderTarget2D pool. Graph alias slots only
/// share one physical target when their complete target descriptors match.
/// Prepare owns all creation/release work; Acquire and Bind are allocation-free
/// validation and lookup paths for the active generation.
/// </summary>
public sealed class TransientRenderTargetPool : IDisposable
{
    public const int DefaultMaximumLogicalResources = 128;
    public const int DefaultMaximumPhysicalTargets = 64;

    private readonly ITransientRenderTargetFactory _factory;
    private readonly LogicalEntry[] _logicalEntries;
    private readonly LogicalEntry[] _pendingLogicalEntries;
    private readonly PhysicalEntry[] _physicalEntries;
    private object? _deviceKey;
    private int _logicalCount;
    private int _physicalCount;
    private int _generation;
    private long _createdTargetCount;
    private long _reusedTargetCount;
    private long _releasedTargetCount;
    private bool _disposed;

    public TransientRenderTargetPool(
        int maximumLogicalResources = DefaultMaximumLogicalResources,
        int maximumPhysicalTargets = DefaultMaximumPhysicalTargets)
        : this(
            GraphicsDeviceTransientRenderTargetFactory.Instance,
            maximumLogicalResources,
            maximumPhysicalTargets)
    {
    }

    internal TransientRenderTargetPool(
        ITransientRenderTargetFactory factory,
        int maximumLogicalResources = DefaultMaximumLogicalResources,
        int maximumPhysicalTargets = DefaultMaximumPhysicalTargets)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (maximumLogicalResources <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLogicalResources));
        }

        if (maximumPhysicalTargets <= 0 || maximumPhysicalTargets > maximumLogicalResources)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPhysicalTargets));
        }

        _factory = factory;
        MaximumLogicalResources = maximumLogicalResources;
        MaximumPhysicalTargets = maximumPhysicalTargets;
        _logicalEntries = new LogicalEntry[maximumLogicalResources];
        _pendingLogicalEntries = new LogicalEntry[maximumLogicalResources];
        _physicalEntries = new PhysicalEntry[maximumPhysicalTargets];
    }

    public int MaximumLogicalResources { get; }

    public int MaximumPhysicalTargets { get; }

    public TransientRenderTargetPoolTelemetry Telemetry
    {
        get
        {
            var bytes = 0L;
            for (var index = 0; index < _physicalEntries.Length; index++)
            {
                var resource = _physicalEntries[index].Resource;
                if (resource is not null)
                {
                    bytes = SaturatingAdd(bytes, resource.Descriptor.EstimatedByteCount);
                }
            }

            return new TransientRenderTargetPoolTelemetry(
                _generation,
                _logicalCount,
                _physicalCount,
                Math.Max(0, _logicalCount - _physicalCount),
                bytes,
                _createdTargetCount,
                _reusedTargetCount,
                _releasedTargetCount);
        }
    }

    public TransientRenderTargetPoolLease Prepare(
        GraphicsDevice graphicsDevice,
        in CompiledRenderGraphPlan plan,
        ReadOnlySpan<TransientRenderTargetBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        return PrepareCore(graphicsDevice, plan, bindings);
    }

    public RenderTarget2D Acquire(
        RenderResourceId resource,
        in TransientRenderTargetPoolLease lease,
        int passIndex)
    {
        return AcquireResource(resource, lease, passIndex).Target;
    }

    public void Bind(
        GraphicsDevice graphicsDevice,
        RenderResourceId resource,
        in TransientRenderTargetPoolLease lease,
        int passIndex)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        if (!ReferenceEquals(_deviceKey, graphicsDevice))
        {
            throw new InvalidOperationException("The render-target pool belongs to a different graphics device.");
        }

        var target = AcquireResource(resource, lease, passIndex);
        _factory.Bind(graphicsDevice, target);
    }

    public void Unbind(GraphicsDevice graphicsDevice)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        if (!ReferenceEquals(_deviceKey, graphicsDevice))
        {
            throw new InvalidOperationException("The render-target pool belongs to a different graphics device.");
        }

        _factory.Unbind(graphicsDevice);
    }

    public void Release()
    {
        ThrowIfDisposed();
        _generation++;
        ReleaseCore();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _generation++;
        ReleaseCore();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal TransientRenderTargetPoolLease PrepareForTesting(
        object deviceKey,
        in CompiledRenderGraphPlan plan,
        ReadOnlySpan<TransientRenderTargetBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(deviceKey);
        return PrepareCore(deviceKey, plan, bindings);
    }

    internal ITransientRenderTargetResource AcquireResourceForTesting(
        RenderResourceId resource,
        in TransientRenderTargetPoolLease lease,
        int passIndex)
    {
        return AcquireResource(resource, lease, passIndex);
    }

    internal void BindForTesting(
        object deviceKey,
        RenderResourceId resource,
        in TransientRenderTargetPoolLease lease,
        int passIndex)
    {
        if (!ReferenceEquals(_deviceKey, deviceKey))
        {
            throw new InvalidOperationException("The render-target pool belongs to a different device.");
        }

        _factory.Bind(deviceKey, AcquireResource(resource, lease, passIndex));
    }

    private TransientRenderTargetPoolLease PrepareCore(
        object deviceKey,
        in CompiledRenderGraphPlan plan,
        ReadOnlySpan<TransientRenderTargetBinding> bindings)
    {
        ThrowIfDisposed();
        _generation++;
        if (_deviceKey is not null && !ReferenceEquals(_deviceKey, deviceKey))
        {
            ReleaseCore();
        }

        ValidateAndStageBindings(plan, bindings);
        _deviceKey = deviceKey;
        for (var index = 0; index < _physicalEntries.Length; index++)
        {
            _physicalEntries[index].Retained = false;
        }

        MatchExistingPhysicalTargets();
        ReleaseUnmatchedPhysicalTargets();
        CreateMissingPhysicalTargets(deviceKey);
        Array.Copy(_pendingLogicalEntries, _logicalEntries, _logicalCount);
        return new TransientRenderTargetPoolLease(this, _generation, plan);
    }

    private void ValidateAndStageBindings(
        in CompiledRenderGraphPlan plan,
        ReadOnlySpan<TransientRenderTargetBinding> bindings)
    {
        if (bindings.Length != plan.Telemetry.UsedTransientResources)
        {
            throw new ArgumentException(
                "Bindings must cover every used transient graph resource exactly once.",
                nameof(bindings));
        }

        if (bindings.Length > MaximumLogicalResources)
        {
            throw new InvalidOperationException(
                $"Logical render-target capacity {MaximumLogicalResources} was exceeded.");
        }

        var uniquePhysicalTargets = 0;
        for (var index = 0; index < bindings.Length; index++)
        {
            ref readonly var binding = ref bindings[index];
            binding.Descriptor.Validate();
            var lifetime = plan.GetResourceLifetime(binding.Resource);
            if (!lifetime.IsTransient)
            {
                throw new ArgumentException(
                    $"Render resource {binding.Resource} is not a used transient resource.",
                    nameof(bindings));
            }

            for (var previous = 0; previous < index; previous++)
            {
                if (_pendingLogicalEntries[previous].Resource == binding.Resource)
                {
                    throw new ArgumentException(
                        $"Render resource {binding.Resource} is bound more than once.",
                        nameof(bindings));
                }
            }

            var key = new PhysicalKey(lifetime.AliasSlot, binding.Descriptor);
            var isNewKey = true;
            for (var previous = 0; previous < index; previous++)
            {
                if (_pendingLogicalEntries[previous].Key == key)
                {
                    isNewKey = false;
                    break;
                }
            }

            uniquePhysicalTargets += isNewKey ? 1 : 0;
            _pendingLogicalEntries[index] = new LogicalEntry(
                binding.Resource,
                lifetime.FirstPassIndex,
                lifetime.LastPassIndex,
                key,
                PhysicalIndex: -1);
        }

        if (uniquePhysicalTargets > MaximumPhysicalTargets)
        {
            throw new InvalidOperationException(
                $"Physical render-target capacity {MaximumPhysicalTargets} was exceeded by " +
                $"{uniquePhysicalTargets} descriptor-keyed alias slots.");
        }

        _logicalCount = bindings.Length;
    }

    private void MatchExistingPhysicalTargets()
    {
        for (var logicalIndex = 0; logicalIndex < _logicalCount; logicalIndex++)
        {
            ref var logical = ref _pendingLogicalEntries[logicalIndex];
            for (var physicalIndex = 0; physicalIndex < _physicalEntries.Length; physicalIndex++)
            {
                ref var physical = ref _physicalEntries[physicalIndex];
                if (physical.Resource is null ||
                    physical.Retained ||
                    physical.Key != logical.Key ||
                    physical.Resource.IsDisposed)
                {
                    continue;
                }

                physical.Retained = true;
                logical.PhysicalIndex = physicalIndex;
                _reusedTargetCount++;
                break;
            }

            if (logical.PhysicalIndex < 0)
            {
                for (var previous = 0; previous < logicalIndex; previous++)
                {
                    if (_pendingLogicalEntries[previous].Key == logical.Key)
                    {
                        logical.PhysicalIndex = _pendingLogicalEntries[previous].PhysicalIndex;
                        break;
                    }
                }
            }
        }
    }

    private void ReleaseUnmatchedPhysicalTargets()
    {
        var needsRelease = false;
        for (var index = 0; index < _physicalEntries.Length; index++)
        {
            if (_physicalEntries[index].Resource is not null && !_physicalEntries[index].Retained)
            {
                needsRelease = true;
                break;
            }
        }

        if (needsRelease && _deviceKey is not null)
        {
            _factory.Unbind(_deviceKey);
        }

        for (var index = 0; index < _physicalEntries.Length; index++)
        {
            ref var entry = ref _physicalEntries[index];
            if (entry.Resource is null || entry.Retained)
            {
                continue;
            }

            entry.Resource.Dispose();
            entry = default;
            _physicalCount--;
            _releasedTargetCount++;
        }
    }

    private void CreateMissingPhysicalTargets(object deviceKey)
    {
        try
        {
            for (var logicalIndex = 0; logicalIndex < _logicalCount; logicalIndex++)
            {
                ref var logical = ref _pendingLogicalEntries[logicalIndex];
                if (logical.PhysicalIndex >= 0)
                {
                    continue;
                }

                for (var previous = 0; previous < logicalIndex; previous++)
                {
                    if (_pendingLogicalEntries[previous].Key == logical.Key)
                    {
                        logical.PhysicalIndex = _pendingLogicalEntries[previous].PhysicalIndex;
                        break;
                    }
                }

                if (logical.PhysicalIndex >= 0)
                {
                    continue;
                }

                var physicalIndex = FindFreePhysicalIndex();
                var resource = _factory.Create(deviceKey, logical.Key.Descriptor);
                _physicalEntries[physicalIndex] = new PhysicalEntry(
                    logical.Key,
                    resource,
                    Retained: true);
                logical.PhysicalIndex = physicalIndex;
                _physicalCount++;
                _createdTargetCount++;
            }
        }
        catch
        {
            ReleaseCore();
            throw;
        }
    }

    private int FindFreePhysicalIndex()
    {
        for (var index = 0; index < _physicalEntries.Length; index++)
        {
            if (_physicalEntries[index].Resource is null)
            {
                return index;
            }
        }

        throw new InvalidOperationException("No free physical render-target slot remains.");
    }

    private ITransientRenderTargetResource AcquireResource(
        RenderResourceId resource,
        in TransientRenderTargetPoolLease lease,
        int passIndex)
    {
        ThrowIfDisposed();
        if (!ReferenceEquals(lease.Owner, this) || lease.Generation != _generation)
        {
            throw new InvalidOperationException("The render-target pool lease is stale or belongs to another pool.");
        }

        var compiledLifetime = lease.Plan.GetResourceLifetime(resource);
        for (var index = 0; index < _logicalCount; index++)
        {
            ref readonly var logical = ref _logicalEntries[index];
            if (logical.Resource != resource)
            {
                continue;
            }

            if (compiledLifetime.FirstPassIndex != logical.FirstPassIndex ||
                compiledLifetime.LastPassIndex != logical.LastPassIndex ||
                compiledLifetime.AliasSlot != logical.Key.GraphAliasSlot)
            {
                throw new InvalidOperationException("The render-target binding no longer matches the compiled graph.");
            }

            if (passIndex < logical.FirstPassIndex || passIndex > logical.LastPassIndex)
            {
                throw new InvalidOperationException(
                    $"Render resource {resource} is not alive at pass index {passIndex}; " +
                    $"valid lifetime is {logical.FirstPassIndex}..{logical.LastPassIndex}.");
            }

            var target = _physicalEntries[logical.PhysicalIndex].Resource;
            if (target is null || target.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(TransientRenderTargetPool));
            }

            return target;
        }

        throw new KeyNotFoundException($"Render resource {resource} is not bound by the current pool generation.");
    }

    private void ReleaseCore()
    {
        if (_deviceKey is not null && _physicalCount > 0)
        {
            _factory.Unbind(_deviceKey);
        }

        for (var index = 0; index < _physicalEntries.Length; index++)
        {
            ref var entry = ref _physicalEntries[index];
            if (entry.Resource is null)
            {
                continue;
            }

            entry.Resource.Dispose();
            entry = default;
            _releasedTargetCount++;
        }

        Array.Clear(_logicalEntries);
        Array.Clear(_pendingLogicalEntries);
        _logicalCount = 0;
        _physicalCount = 0;
        _deviceKey = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static long SaturatingAdd(long left, long right)
    {
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    private readonly record struct PhysicalKey(
        int GraphAliasSlot,
        TransientRenderTargetDescriptor Descriptor);

    private record struct LogicalEntry(
        RenderResourceId Resource,
        int FirstPassIndex,
        int LastPassIndex,
        PhysicalKey Key,
        int PhysicalIndex);

    private record struct PhysicalEntry(
        PhysicalKey Key,
        ITransientRenderTargetResource? Resource,
        bool Retained);
}

internal interface ITransientRenderTargetResource : IDisposable
{
    TransientRenderTargetDescriptor Descriptor { get; }

    RenderTarget2D Target { get; }

    bool IsDisposed { get; }
}

internal interface ITransientRenderTargetFactory
{
    ITransientRenderTargetResource Create(
        object deviceKey,
        in TransientRenderTargetDescriptor descriptor);

    void Bind(object deviceKey, ITransientRenderTargetResource resource);

    void Unbind(object deviceKey);
}

internal sealed class GraphicsDeviceTransientRenderTargetFactory : ITransientRenderTargetFactory
{
    public static GraphicsDeviceTransientRenderTargetFactory Instance { get; } = new();

    private GraphicsDeviceTransientRenderTargetFactory()
    {
    }

    public ITransientRenderTargetResource Create(
        object deviceKey,
        in TransientRenderTargetDescriptor descriptor)
    {
        var graphicsDevice = deviceKey as GraphicsDevice ??
            throw new ArgumentException("A GraphicsDevice is required.", nameof(deviceKey));
        return new GraphicsDeviceTransientRenderTargetResource(
            descriptor,
            new RenderTarget2D(
                graphicsDevice,
                descriptor.Width,
                descriptor.Height,
                descriptor.MipMap,
                descriptor.SurfaceFormat,
                descriptor.DepthFormat,
                descriptor.MultiSampleCount,
                descriptor.Usage));
    }

    public void Bind(object deviceKey, ITransientRenderTargetResource resource)
    {
        var graphicsDevice = deviceKey as GraphicsDevice ??
            throw new ArgumentException("A GraphicsDevice is required.", nameof(deviceKey));
        graphicsDevice.SetRenderTarget(resource.Target);
    }

    public void Unbind(object deviceKey)
    {
        var graphicsDevice = deviceKey as GraphicsDevice ??
            throw new ArgumentException("A GraphicsDevice is required.", nameof(deviceKey));
        graphicsDevice.SetRenderTarget(null);
    }
}

internal sealed class GraphicsDeviceTransientRenderTargetResource : ITransientRenderTargetResource
{
    private readonly RenderTarget2D _target;
    private bool _disposed;

    public GraphicsDeviceTransientRenderTargetResource(
        TransientRenderTargetDescriptor descriptor,
        RenderTarget2D target)
    {
        Descriptor = descriptor;
        _target = target;
    }

    public TransientRenderTargetDescriptor Descriptor { get; }

    public RenderTarget2D Target => !_disposed
        ? _target
        : throw new ObjectDisposedException(nameof(GraphicsDeviceTransientRenderTargetResource));

    public bool IsDisposed => _disposed || _target.IsDisposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _target.Dispose();
        _disposed = true;
    }
}
