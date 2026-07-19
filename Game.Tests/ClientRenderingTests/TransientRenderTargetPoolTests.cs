using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class TransientRenderTargetPoolTests
{
    [Fact]
    public void Prepare_MapsCompatibleGraphAliasesToSharedPhysicalTargets()
    {
        var graph = CreateLinearGraph();
        var plan = graph.Compile([Resource(5)]);
        var factory = new FakeFactory();
        using var pool = new TransientRenderTargetPool(factory, 3, 3);
        var descriptor = Descriptor(64, 64);

        var lease = pool.PrepareForTesting(
            factory.DeviceA,
            plan,
            Bindings(descriptor, descriptor, descriptor));

        var first = pool.AcquireResourceForTesting(Resource(1), lease, passIndex: 0);
        var second = pool.AcquireResourceForTesting(Resource(2), lease, passIndex: 1);
        var third = pool.AcquireResourceForTesting(Resource(3), lease, passIndex: 2);
        Assert.Same(first, third);
        Assert.NotSame(first, second);
        Assert.Equal(3, pool.Telemetry.LogicalResourceCount);
        Assert.Equal(2, pool.Telemetry.PhysicalTargetCount);
        Assert.Equal(1, pool.Telemetry.AliasBindingsSaved);
        Assert.Equal(descriptor.EstimatedByteCount * 2, pool.Telemetry.EstimatedResidentBytes);
    }

    [Fact]
    public void Prepare_SplitsOneGraphAliasWhenDescriptorsDiffer()
    {
        var graph = CreateLinearGraph();
        var plan = graph.Compile([Resource(5)]);
        var factory = new FakeFactory();
        using var pool = new TransientRenderTargetPool(factory, 3, 3);

        var lease = pool.PrepareForTesting(
            factory.DeviceA,
            plan,
            Bindings(Descriptor(64, 64), Descriptor(64, 64), Descriptor(128, 64)));

        Assert.NotSame(
            pool.AcquireResourceForTesting(Resource(1), lease, 0),
            pool.AcquireResourceForTesting(Resource(3), lease, 2));
        Assert.Equal(3, pool.Telemetry.PhysicalTargetCount);
        Assert.Equal(0, pool.Telemetry.AliasBindingsSaved);
    }

    [Fact]
    public void Acquire_EnforcesCompiledLifetimeAndGraphGeneration()
    {
        var graph = CreateLinearGraph();
        var plan = graph.Compile([Resource(5)]);
        var factory = new FakeFactory();
        using var pool = new TransientRenderTargetPool(factory, 3, 3);
        var descriptor = Descriptor(64, 64);
        var lease = pool.PrepareForTesting(
            factory.DeviceA,
            plan,
            Bindings(descriptor, descriptor, descriptor));

        _ = pool.AcquireResourceForTesting(Resource(1), lease, 0);
        _ = pool.AcquireResourceForTesting(Resource(1), lease, 1);
        Assert.Throws<InvalidOperationException>(
            () => pool.AcquireResourceForTesting(Resource(1), lease, 2));

        _ = graph.Compile([Resource(5)]);
        Assert.Throws<InvalidOperationException>(
            () => pool.AcquireResourceForTesting(Resource(1), lease, 0));
    }

    [Fact]
    public void Resize_ReusesMatchingDescriptorsAndReleasesOnlyReplacedTargets()
    {
        var graph = CreateLinearGraph();
        var plan = graph.Compile([Resource(5)]);
        var factory = new FakeFactory();
        using var pool = new TransientRenderTargetPool(factory, 3, 3);
        var small = Descriptor(64, 64);
        var stable = Descriptor(128, 64);
        var firstLease = pool.PrepareForTesting(
            factory.DeviceA,
            plan,
            Bindings(small, stable, small));
        var firstAlias = Assert.IsType<FakeResource>(
            pool.AcquireResourceForTesting(Resource(1), firstLease, 0));
        var stableTarget = pool.AcquireResourceForTesting(Resource(2), firstLease, 1);

        var large = Descriptor(256, 128);
        var secondLease = pool.PrepareForTesting(
            factory.DeviceA,
            plan,
            Bindings(large, stable, large));

        Assert.True(firstAlias.IsDisposed);
        Assert.Same(stableTarget, pool.AcquireResourceForTesting(Resource(2), secondLease, 1));
        Assert.Equal(3, factory.CreateCount);
        Assert.Equal(1, factory.DisposedCount);
        Assert.Equal(2, pool.Telemetry.PhysicalTargetCount);
        Assert.True(pool.Telemetry.ReusedTargetCount >= 1);
    }

    [Fact]
    public void ScreenSpaceReconfigure_ReusesSceneAndReplacesOnlyChangedQualityAndResizeTargets()
    {
        var graph = CreateScreenSpaceGraph();
        var factory = new FakeFactory();
        using var pool = new TransientRenderTargetPool(factory, 3, 3);
        var fullHd = new Rectangle(0, 0, 1920, 1080);
        var initialPlan = graph.Compile([Resource(3)]);
        var initialLease = pool.PrepareForTesting(
            factory.DeviceA,
            initialPlan,
            ScreenSpaceBindings(fullHd, PresentationQualityTier.High));
        var initialScene = pool.AcquireResourceForTesting(Resource(0), initialLease, 0);
        var initialPing = Assert.IsType<FakeResource>(
            pool.AcquireResourceForTesting(Resource(1), initialLease, 1));
        var initialPong = Assert.IsType<FakeResource>(
            pool.AcquireResourceForTesting(Resource(2), initialLease, 2));

        var mediumPlan = graph.Compile([Resource(3)]);
        var mediumLease = pool.PrepareForTesting(
            factory.DeviceA,
            mediumPlan,
            ScreenSpaceBindings(fullHd, PresentationQualityTier.Medium));

        Assert.Same(initialScene, pool.AcquireResourceForTesting(Resource(0), mediumLease, 0));
        Assert.True(initialPing.IsDisposed);
        Assert.True(initialPong.IsDisposed);
        Assert.Equal(5, factory.CreateCount);
        Assert.Equal(2, factory.DisposedCount);
        Assert.Equal(1, pool.Telemetry.ReusedTargetCount);
        Assert.Equal(3, pool.Telemetry.PhysicalTargetCount);

        var resizedViewport = new Rectangle(0, 0, 1280, 720);
        var resizedPlan = graph.Compile([Resource(3)]);
        _ = pool.PrepareForTesting(
            factory.DeviceA,
            resizedPlan,
            ScreenSpaceBindings(resizedViewport, PresentationQualityTier.Medium));

        Assert.Equal(8, factory.CreateCount);
        Assert.Equal(5, factory.DisposedCount);
        Assert.Equal(5, pool.Telemetry.ReleasedTargetCount);
        Assert.Equal(3, pool.Telemetry.PhysicalTargetCount);
    }

    [Fact]
    public void DeviceReplacement_UnbindsAndReleasesOldDeviceTargets()
    {
        var graph = CreateLinearGraph();
        var plan = graph.Compile([Resource(5)]);
        var factory = new FakeFactory();
        using var pool = new TransientRenderTargetPool(factory, 3, 3);
        var descriptor = Descriptor(64, 64);
        var firstLease = pool.PrepareForTesting(
            factory.DeviceA,
            plan,
            Bindings(descriptor, descriptor, descriptor));
        var first = Assert.IsType<FakeResource>(
            pool.AcquireResourceForTesting(Resource(1), firstLease, 0));

        var secondLease = pool.PrepareForTesting(
            factory.DeviceB,
            plan,
            Bindings(descriptor, descriptor, descriptor));

        Assert.True(first.IsDisposed);
        Assert.Equal(1, factory.UnbindCountFor(factory.DeviceA));
        Assert.Equal(4, factory.CreateCount);
        Assert.Same(
            factory.DeviceB,
            Assert.IsType<FakeResource>(
                pool.AcquireResourceForTesting(Resource(1), secondLease, 0)).DeviceKey);
    }

    [Fact]
    public void Prepare_RejectsPhysicalCapacityBeforeCreatingTargets()
    {
        var graph = CreateLinearGraph();
        var plan = graph.Compile([Resource(5)]);
        var factory = new FakeFactory();
        using var pool = new TransientRenderTargetPool(factory, 3, 1);
        var descriptor = Descriptor(64, 64);

        Assert.Throws<InvalidOperationException>(() => pool.PrepareForTesting(
            factory.DeviceA,
            plan,
            Bindings(descriptor, descriptor, descriptor)));
        Assert.Equal(0, factory.CreateCount);
        Assert.Equal(0, pool.Telemetry.PhysicalTargetCount);
    }

    [Fact]
    public void Release_IsDeviceSafeIdempotentThroughDisposeAndInvalidatesLease()
    {
        var graph = CreateLinearGraph();
        var plan = graph.Compile([Resource(5)]);
        var factory = new FakeFactory();
        var pool = new TransientRenderTargetPool(factory, 3, 3);
        var descriptor = Descriptor(64, 64);
        var lease = pool.PrepareForTesting(
            factory.DeviceA,
            plan,
            Bindings(descriptor, descriptor, descriptor));

        pool.Release();
        Assert.Equal(2, factory.DisposedCount);
        Assert.Equal(1, factory.UnbindCountFor(factory.DeviceA));
        Assert.Equal(0, pool.Telemetry.PhysicalTargetCount);
        Assert.Equal(0, pool.Telemetry.EstimatedResidentBytes);
        Assert.Throws<InvalidOperationException>(
            () => pool.AcquireResourceForTesting(Resource(1), lease, 0));

        pool.Dispose();
        pool.Dispose();
        Assert.Equal(2, factory.DisposedCount);
    }

    internal static RenderPassGraph CreateLinearGraph()
    {
        var graph = new RenderPassGraph(maximumPasses: 4, maximumResources: 6, maximumResourceUsages: 9);
        graph.DeclareResource(Resource(0), RenderResourceFlags.Imported);
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(2), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(3), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(5), RenderResourceFlags.None);
        graph.DeclarePass(Pass(0), RenderPassPhase.Opaque, [Resource(0)], [Resource(1)]);
        graph.DeclarePass(Pass(1), RenderPassPhase.Lighting, [Resource(1)], [Resource(2)]);
        graph.DeclarePass(Pass(2), RenderPassPhase.PostProcess, [Resource(2)], [Resource(3)]);
        graph.DeclarePass(Pass(3), RenderPassPhase.Composite, [Resource(3)], [Resource(5)]);
        return graph;
    }

    private static RenderPassGraph CreateScreenSpaceGraph()
    {
        var graph = new RenderPassGraph(maximumPasses: 4, maximumResources: 4, maximumResourceUsages: 9);
        graph.DeclareResource(Resource(0), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(1), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(2), RenderResourceFlags.Transient);
        graph.DeclareResource(Resource(3), RenderResourceFlags.None);
        graph.DeclarePass(Pass(0), RenderPassPhase.Opaque, [], [Resource(0)]);
        graph.DeclarePass(Pass(1), RenderPassPhase.PostProcess, [Resource(0)], [Resource(1)]);
        graph.DeclarePass(Pass(2), RenderPassPhase.PostProcess, [Resource(1)], [Resource(2)]);
        graph.DeclarePass(
            Pass(3),
            RenderPassPhase.Composite,
            [Resource(0), Resource(2)],
            [Resource(3)]);
        return graph;
    }

    private static TransientRenderTargetBinding[] ScreenSpaceBindings(
        Rectangle viewport,
        PresentationQualityTier quality)
    {
        var blur = BackdropBlurPlanner.Build(quality, viewport, radiusPixels: 8);
        var sceneDescriptor = Descriptor(viewport.Width, viewport.Height);
        var blurDescriptor = Descriptor(blur.TargetSize.X, blur.TargetSize.Y);
        return
        [
            new TransientRenderTargetBinding(Resource(0), sceneDescriptor),
            new TransientRenderTargetBinding(Resource(1), blurDescriptor),
            new TransientRenderTargetBinding(Resource(2), blurDescriptor)
        ];
    }

    internal static TransientRenderTargetBinding[] Bindings(
        TransientRenderTargetDescriptor first,
        TransientRenderTargetDescriptor second,
        TransientRenderTargetDescriptor third)
    {
        return
        [
            new TransientRenderTargetBinding(Resource(1), first),
            new TransientRenderTargetBinding(Resource(2), second),
            new TransientRenderTargetBinding(Resource(3), third)
        ];
    }

    internal static TransientRenderTargetDescriptor Descriptor(int width, int height)
    {
        return new TransientRenderTargetDescriptor(width, height);
    }

    internal static RenderPassId Pass(int value) => new(value);

    internal static RenderResourceId Resource(int value) => new(value);

    internal sealed class FakeFactory : ITransientRenderTargetFactory
    {
        private readonly Dictionary<object, int> _unbindCounts = new(ReferenceEqualityComparer.Instance);

        public object DeviceA { get; } = new();

        public object DeviceB { get; } = new();

        public int CreateCount { get; private set; }

        public int DisposedCount { get; private set; }

        public int BindCount { get; private set; }

        public ITransientRenderTargetResource Create(
            object deviceKey,
            in TransientRenderTargetDescriptor descriptor)
        {
            CreateCount++;
            return new FakeResource(this, deviceKey, descriptor);
        }

        public void Bind(object deviceKey, ITransientRenderTargetResource resource)
        {
            if (resource is not FakeResource fake ||
                !ReferenceEquals(deviceKey, fake.DeviceKey) ||
                resource.IsDisposed)
            {
                throw new InvalidOperationException();
            }

            BindCount++;
        }

        public void Unbind(object deviceKey)
        {
            _unbindCounts.TryGetValue(deviceKey, out var count);
            _unbindCounts[deviceKey] = count + 1;
        }

        public int UnbindCountFor(object deviceKey)
        {
            return _unbindCounts.TryGetValue(deviceKey, out var count) ? count : 0;
        }

        public void RecordDispose()
        {
            DisposedCount++;
        }
    }

    internal sealed class FakeResource : ITransientRenderTargetResource
    {
        private readonly FakeFactory _owner;

        public FakeResource(
            FakeFactory owner,
            object deviceKey,
            TransientRenderTargetDescriptor descriptor)
        {
            _owner = owner;
            DeviceKey = deviceKey;
            Descriptor = descriptor;
        }

        public object DeviceKey { get; }

        public TransientRenderTargetDescriptor Descriptor { get; }

        public RenderTarget2D Target => throw new InvalidOperationException("Fake resources have no GPU target.");

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _owner.RecordDispose();
        }
    }
}
