namespace Game.Client.Rendering.Graph;

/// <summary>
/// Fixed-capacity render-pass graph with reusable compile storage. Declaration
/// may allocate the graph's fixed arrays once; successful warm Compile calls,
/// plan queries and generic execution do not allocate.
/// </summary>
public sealed class RenderPassGraph
{
    public const int DefaultMaximumPasses = 64;
    public const int DefaultMaximumResources = 128;
    public const int DefaultMaximumResourceUsages = 1_024;
    private const int MaximumSupportedPasses = 256;
    private const int MaximumSupportedResources = 1_024;

    private readonly PassNode[] _passes;
    private readonly ResourceNode[] _resources;
    private readonly int[] _resourceUsages;
    private readonly int[] _resourceProducers;
    private readonly bool[] _dependencies;
    private readonly bool[] _selectedPasses;
    private readonly bool[] _emittedPasses;
    private readonly bool[] _requestedResources;
    private readonly int[] _indegrees;
    private readonly int[] _compiledPassOrder;
    private readonly int[] _firstResourceUses;
    private readonly int[] _lastResourceUses;
    private readonly int[] _resourceAliasSlots;
    private readonly int[] _transientResourceOrder;
    private readonly int[] _slotLastUses;
    private int _declaredPassCount;
    private int _declaredResourceCount;
    private int _resourceUsageCount;
    private int _compiledPassCount;
    private int _generation;

    public RenderPassGraph(
        int maximumPasses = DefaultMaximumPasses,
        int maximumResources = DefaultMaximumResources,
        int maximumResourceUsages = DefaultMaximumResourceUsages)
    {
        if (maximumPasses <= 0 || maximumPasses > MaximumSupportedPasses)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPasses),
                $"Pass capacity must be between 1 and {MaximumSupportedPasses}.");
        }

        if (maximumResources <= 0 || maximumResources > MaximumSupportedResources)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResources),
                $"Resource capacity must be between 1 and {MaximumSupportedResources}.");
        }

        if (maximumResourceUsages <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResourceUsages));
        }

        MaximumPasses = maximumPasses;
        MaximumResources = maximumResources;
        MaximumResourceUsages = maximumResourceUsages;
        _passes = new PassNode[maximumPasses];
        _resources = new ResourceNode[maximumResources];
        _resourceUsages = new int[maximumResourceUsages];
        _resourceProducers = new int[maximumResources];
        _dependencies = new bool[checked(maximumPasses * maximumPasses)];
        _selectedPasses = new bool[maximumPasses];
        _emittedPasses = new bool[maximumPasses];
        _requestedResources = new bool[maximumResources];
        _indegrees = new int[maximumPasses];
        _compiledPassOrder = new int[maximumPasses];
        _firstResourceUses = new int[maximumResources];
        _lastResourceUses = new int[maximumResources];
        _resourceAliasSlots = new int[maximumResources];
        _transientResourceOrder = new int[maximumResources];
        _slotLastUses = new int[maximumResources];
    }

    public int MaximumPasses { get; }

    public int MaximumResources { get; }

    public int MaximumResourceUsages { get; }

    public int DeclaredPassCount => _declaredPassCount;

    public int DeclaredResourceCount => _declaredResourceCount;

    public void DeclareResource(RenderResourceId id, RenderResourceFlags flags)
    {
        var index = ValidateResourceId(id);
        const RenderResourceFlags supportedFlags =
            RenderResourceFlags.Imported |
            RenderResourceFlags.Transient;
        if ((flags & ~supportedFlags) != 0 ||
            (flags & (RenderResourceFlags.Imported | RenderResourceFlags.Transient)) ==
            (RenderResourceFlags.Imported | RenderResourceFlags.Transient))
        {
            throw new ArgumentOutOfRangeException(nameof(flags));
        }

        if (_resources[index].Declared)
        {
            throw new InvalidOperationException($"Render resource {id} is already declared.");
        }

        _resources[index] = new ResourceNode(
            new RenderResourceDescriptor(id, flags),
            Declared: true);
        _declaredResourceCount++;
        InvalidateCompiledPlan();
    }

    public void DeclarePass(
        RenderPassId id,
        RenderPassPhase phase,
        ReadOnlySpan<RenderResourceId> reads,
        ReadOnlySpan<RenderResourceId> writes,
        RenderPassFlags flags = RenderPassFlags.None)
    {
        var passIndex = ValidatePassId(id);
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        if ((flags & ~RenderPassFlags.NeverCull) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flags));
        }

        if (_passes[passIndex].Declared)
        {
            throw new InvalidOperationException($"Render pass {id} is already declared.");
        }

        var requestedUsages = checked(reads.Length + writes.Length);
        if (requestedUsages > MaximumResourceUsages - _resourceUsageCount)
        {
            throw new InvalidOperationException(
                $"Render graph resource-usage capacity {MaximumResourceUsages} was exceeded.");
        }

        ValidatePassResources(reads, writes);
        var readStart = _resourceUsageCount;
        CopyResourceIds(reads, _resourceUsageCount);
        _resourceUsageCount += reads.Length;
        var writeStart = _resourceUsageCount;
        CopyResourceIds(writes, _resourceUsageCount);
        _resourceUsageCount += writes.Length;

        _passes[passIndex] = new PassNode(
            new RenderPassDescriptor(id, phase, flags),
            readStart,
            reads.Length,
            writeStart,
            writes.Length,
            Declared: true);
        _declaredPassCount++;
        InvalidateCompiledPlan();
    }

    public CompiledRenderGraphPlan Compile(ReadOnlySpan<RenderResourceId> requestedOutputs)
    {
        // Invalidate any prior view even when this compile later reports an
        // error, because all compile scratch storage is reused in-place.
        _generation++;
        ResetCompileStorage();
        DiscoverResourceProducers();
        BuildDependencies();
        ValidateAcyclicGraph();
        SelectRequiredPasses(requestedOutputs);
        CompileSelectedPassOrder();
        var (usedTransientResources, aliasSlots) = PlanTransientResourceLifetimes(requestedOutputs);
        var telemetry = new RenderGraphTelemetry(
            _declaredPassCount,
            _compiledPassCount,
            _declaredPassCount - _compiledPassCount,
            _declaredResourceCount,
            usedTransientResources,
            aliasSlots,
            usedTransientResources - aliasSlots);
        return new CompiledRenderGraphPlan(this, _generation, telemetry);
    }

    public void Clear()
    {
        Array.Clear(_passes);
        Array.Clear(_resources);
        _declaredPassCount = 0;
        _declaredResourceCount = 0;
        _resourceUsageCount = 0;
        _compiledPassCount = 0;
        InvalidateCompiledPlan();
    }

    internal RenderPassDescriptor GetCompiledPass(int generation, int index)
    {
        ValidateGeneration(generation);
        if ((uint)index >= (uint)_compiledPassCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _passes[_compiledPassOrder[index]].Descriptor;
    }

    internal RenderResourceLifetime GetCompiledResourceLifetime(
        int generation,
        RenderResourceId resource)
    {
        ValidateGeneration(generation);
        var index = ValidateDeclaredResource(resource);
        var isUsed = _lastResourceUses[index] >= 0;
        return new RenderResourceLifetime(
            resource,
            isUsed,
            isUsed ? _firstResourceUses[index] : -1,
            isUsed ? _lastResourceUses[index] : -1,
            isUsed ? _resourceAliasSlots[index] : -1);
    }

    private void ResetCompileStorage()
    {
        Array.Fill(_resourceProducers, -1);
        Array.Clear(_dependencies);
        Array.Clear(_selectedPasses);
        Array.Clear(_emittedPasses);
        Array.Clear(_requestedResources);
        Array.Clear(_indegrees);
        Array.Fill(_firstResourceUses, int.MaxValue);
        Array.Fill(_lastResourceUses, -1);
        Array.Fill(_resourceAliasSlots, -1);
        _compiledPassCount = 0;
    }

    private void DiscoverResourceProducers()
    {
        for (var passIndex = 0; passIndex < MaximumPasses; passIndex++)
        {
            ref readonly var pass = ref _passes[passIndex];
            if (!pass.Declared)
            {
                continue;
            }

            for (var write = 0; write < pass.WriteCount; write++)
            {
                var resourceIndex = _resourceUsages[pass.WriteStart + write];
                var resource = _resources[resourceIndex].Descriptor;
                if ((resource.Flags & RenderResourceFlags.Imported) != 0)
                {
                    throw new RenderGraphCompileException(
                        RenderGraphCompileError.WriteToImportedResource,
                        $"Render pass {pass.Descriptor.Id} writes imported resource {resource.Id}.",
                        pass.Descriptor.Id,
                        resource.Id);
                }

                var existingProducer = _resourceProducers[resourceIndex];
                if (existingProducer >= 0)
                {
                    throw new RenderGraphCompileException(
                        RenderGraphCompileError.MultipleProducers,
                        $"Render resource {resource.Id} is written by passes " +
                        $"{_passes[existingProducer].Descriptor.Id} and {pass.Descriptor.Id}.",
                        pass.Descriptor.Id,
                        resource.Id);
                }

                _resourceProducers[resourceIndex] = passIndex;
            }
        }
    }

    private void BuildDependencies()
    {
        for (var consumerIndex = 0; consumerIndex < MaximumPasses; consumerIndex++)
        {
            ref readonly var consumer = ref _passes[consumerIndex];
            if (!consumer.Declared)
            {
                continue;
            }

            for (var read = 0; read < consumer.ReadCount; read++)
            {
                var resourceIndex = _resourceUsages[consumer.ReadStart + read];
                var resource = _resources[resourceIndex].Descriptor;
                var producerIndex = _resourceProducers[resourceIndex];
                if (producerIndex < 0)
                {
                    if ((resource.Flags & RenderResourceFlags.Imported) != 0)
                    {
                        continue;
                    }

                    throw new RenderGraphCompileException(
                        RenderGraphCompileError.ReadBeforeProduce,
                        $"Render pass {consumer.Descriptor.Id} reads {resource.Id} without a producer.",
                        consumer.Descriptor.Id,
                        resource.Id);
                }

                ref readonly var producer = ref _passes[producerIndex];
                if (producer.Descriptor.Phase > consumer.Descriptor.Phase)
                {
                    throw new RenderGraphCompileException(
                        RenderGraphCompileError.PhaseOrderViolation,
                        $"Render pass {consumer.Descriptor.Id} in phase {consumer.Descriptor.Phase} " +
                        $"depends on later phase {producer.Descriptor.Phase}.",
                        consumer.Descriptor.Id,
                        resource.Id);
                }

                var edge = producerIndex * MaximumPasses + consumerIndex;
                if (!_dependencies[edge])
                {
                    _dependencies[edge] = true;
                    _indegrees[consumerIndex]++;
                }
            }
        }
    }

    private void ValidateAcyclicGraph()
    {
        Array.Clear(_emittedPasses);
        var emittedCount = 0;
        while (emittedCount < _declaredPassCount)
        {
            var next = FindNextReadyPass(includeOnlySelected: false);
            if (next < 0)
            {
                throw CreateCycleException(includeOnlySelected: false);
            }

            EmitPass(next, includeOnlySelected: false);
            emittedCount++;
        }
    }

    private void SelectRequiredPasses(ReadOnlySpan<RenderResourceId> requestedOutputs)
    {
        for (var outputIndex = 0; outputIndex < requestedOutputs.Length; outputIndex++)
        {
            var resourceIndex = ValidateDeclaredResource(requestedOutputs[outputIndex]);
            var producer = _resourceProducers[resourceIndex];
            if (producer < 0)
            {
                throw new RenderGraphCompileException(
                    RenderGraphCompileError.RequestedOutputNotProduced,
                    $"Requested render output {requestedOutputs[outputIndex]} has no producer.",
                    resource: requestedOutputs[outputIndex]);
            }

            _requestedResources[resourceIndex] = true;
            _selectedPasses[producer] = true;
        }

        for (var passIndex = 0; passIndex < MaximumPasses; passIndex++)
        {
            if (_passes[passIndex].Declared &&
                (_passes[passIndex].Descriptor.Flags & RenderPassFlags.NeverCull) != 0)
            {
                _selectedPasses[passIndex] = true;
            }
        }

        var changed = true;
        while (changed)
        {
            changed = false;
            for (var consumer = 0; consumer < MaximumPasses; consumer++)
            {
                if (!_selectedPasses[consumer])
                {
                    continue;
                }

                for (var producer = 0; producer < MaximumPasses; producer++)
                {
                    if (_dependencies[producer * MaximumPasses + consumer] &&
                        !_selectedPasses[producer])
                    {
                        _selectedPasses[producer] = true;
                        changed = true;
                    }
                }
            }
        }
    }

    private void CompileSelectedPassOrder()
    {
        Array.Clear(_emittedPasses);
        Array.Clear(_indegrees);
        var selectedCount = 0;
        for (var pass = 0; pass < MaximumPasses; pass++)
        {
            if (!_selectedPasses[pass])
            {
                continue;
            }

            selectedCount++;
            for (var producer = 0; producer < MaximumPasses; producer++)
            {
                if (_selectedPasses[producer] &&
                    _dependencies[producer * MaximumPasses + pass])
                {
                    _indegrees[pass]++;
                }
            }
        }

        while (_compiledPassCount < selectedCount)
        {
            var next = FindNextReadyPass(includeOnlySelected: true);
            if (next < 0)
            {
                throw CreateCycleException(includeOnlySelected: true);
            }

            _compiledPassOrder[_compiledPassCount++] = next;
            EmitPass(next, includeOnlySelected: true);
        }
    }

    private (int UsedResources, int AliasSlots) PlanTransientResourceLifetimes(
        ReadOnlySpan<RenderResourceId> requestedOutputs)
    {
        for (var orderIndex = 0; orderIndex < _compiledPassCount; orderIndex++)
        {
            ref readonly var pass = ref _passes[_compiledPassOrder[orderIndex]];
            MarkPassResourceUses(pass.ReadStart, pass.ReadCount, orderIndex);
            MarkPassResourceUses(pass.WriteStart, pass.WriteCount, orderIndex);
        }

        for (var outputIndex = 0; outputIndex < requestedOutputs.Length; outputIndex++)
        {
            var resourceIndex = requestedOutputs[outputIndex].Value;
            if ((_resources[resourceIndex].Descriptor.Flags & RenderResourceFlags.Transient) != 0 &&
                _lastResourceUses[resourceIndex] >= 0)
            {
                _lastResourceUses[resourceIndex] = _compiledPassCount;
            }
        }

        var transientCount = 0;
        for (var resourceIndex = 0; resourceIndex < MaximumResources; resourceIndex++)
        {
            if (!_resources[resourceIndex].Declared ||
                (_resources[resourceIndex].Descriptor.Flags & RenderResourceFlags.Transient) == 0 ||
                _lastResourceUses[resourceIndex] < 0)
            {
                continue;
            }

            _transientResourceOrder[transientCount++] = resourceIndex;
        }

        SortTransientResources(transientCount);
        Array.Fill(_slotLastUses, -1);
        var slotCount = 0;
        for (var orderIndex = 0; orderIndex < transientCount; orderIndex++)
        {
            var resourceIndex = _transientResourceOrder[orderIndex];
            var slot = 0;
            while (slot < slotCount &&
                   _slotLastUses[slot] >= _firstResourceUses[resourceIndex])
            {
                slot++;
            }

            if (slot == slotCount)
            {
                slotCount++;
            }

            _resourceAliasSlots[resourceIndex] = slot;
            _slotLastUses[slot] = _lastResourceUses[resourceIndex];
        }

        return (transientCount, slotCount);
    }

    private void MarkPassResourceUses(int start, int count, int passOrderIndex)
    {
        for (var usage = 0; usage < count; usage++)
        {
            var resourceIndex = _resourceUsages[start + usage];
            _firstResourceUses[resourceIndex] =
                Math.Min(_firstResourceUses[resourceIndex], passOrderIndex);
            _lastResourceUses[resourceIndex] =
                Math.Max(_lastResourceUses[resourceIndex], passOrderIndex);
        }
    }

    private void SortTransientResources(int count)
    {
        for (var index = 1; index < count; index++)
        {
            var value = _transientResourceOrder[index];
            var destination = index - 1;
            while (destination >= 0 &&
                   IsResourceAfter(_transientResourceOrder[destination], value))
            {
                _transientResourceOrder[destination + 1] = _transientResourceOrder[destination];
                destination--;
            }

            _transientResourceOrder[destination + 1] = value;
        }
    }

    private bool IsResourceAfter(int left, int right)
    {
        var firstUseComparison = _firstResourceUses[left].CompareTo(_firstResourceUses[right]);
        return firstUseComparison > 0 || firstUseComparison == 0 && left > right;
    }

    private int FindNextReadyPass(bool includeOnlySelected)
    {
        var candidate = -1;
        for (var passIndex = 0; passIndex < MaximumPasses; passIndex++)
        {
            if (!_passes[passIndex].Declared ||
                _emittedPasses[passIndex] ||
                _indegrees[passIndex] != 0 ||
                includeOnlySelected && !_selectedPasses[passIndex])
            {
                continue;
            }

            if (candidate < 0 || IsPassBefore(passIndex, candidate))
            {
                candidate = passIndex;
            }
        }

        return candidate;
    }

    private bool IsPassBefore(int left, int right)
    {
        var leftDescriptor = _passes[left].Descriptor;
        var rightDescriptor = _passes[right].Descriptor;
        var phaseComparison = leftDescriptor.Phase.CompareTo(rightDescriptor.Phase);
        return phaseComparison < 0 ||
               phaseComparison == 0 && leftDescriptor.Id.Value < rightDescriptor.Id.Value;
    }

    private void EmitPass(int passIndex, bool includeOnlySelected)
    {
        _emittedPasses[passIndex] = true;
        for (var consumer = 0; consumer < MaximumPasses; consumer++)
        {
            if (includeOnlySelected && !_selectedPasses[consumer])
            {
                continue;
            }

            if (_dependencies[passIndex * MaximumPasses + consumer])
            {
                _indegrees[consumer]--;
            }
        }
    }

    private RenderGraphCompileException CreateCycleException(bool includeOnlySelected)
    {
        var pass = default(RenderPassId);
        for (var passIndex = 0; passIndex < MaximumPasses; passIndex++)
        {
            if (_passes[passIndex].Declared &&
                !_emittedPasses[passIndex] &&
                (!includeOnlySelected || _selectedPasses[passIndex]))
            {
                pass = _passes[passIndex].Descriptor.Id;
                break;
            }
        }

        return new RenderGraphCompileException(
            RenderGraphCompileError.Cycle,
            $"Render graph contains a dependency cycle involving {pass}.",
            pass);
    }

    private void ValidatePassResources(
        ReadOnlySpan<RenderResourceId> reads,
        ReadOnlySpan<RenderResourceId> writes)
    {
        for (var read = 0; read < reads.Length; read++)
        {
            _ = ValidateDeclaredResource(reads[read]);
            for (var previous = 0; previous < read; previous++)
            {
                if (reads[read] == reads[previous])
                {
                    throw new ArgumentException("A pass cannot declare the same read twice.", nameof(reads));
                }
            }
        }

        for (var write = 0; write < writes.Length; write++)
        {
            _ = ValidateDeclaredResource(writes[write]);
            for (var previous = 0; previous < write; previous++)
            {
                if (writes[write] == writes[previous])
                {
                    throw new ArgumentException("A pass cannot declare the same write twice.", nameof(writes));
                }
            }

            for (var read = 0; read < reads.Length; read++)
            {
                if (writes[write] == reads[read])
                {
                    throw new ArgumentException(
                        "A pass cannot read and write the same unversioned resource.",
                        nameof(writes));
                }
            }
        }
    }

    private void CopyResourceIds(ReadOnlySpan<RenderResourceId> resources, int destination)
    {
        for (var index = 0; index < resources.Length; index++)
        {
            _resourceUsages[destination + index] = resources[index].Value;
        }
    }

    private int ValidatePassId(RenderPassId id)
    {
        if ((uint)id.Value >= (uint)MaximumPasses)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return id.Value;
    }

    private int ValidateResourceId(RenderResourceId id)
    {
        if ((uint)id.Value >= (uint)MaximumResources)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return id.Value;
    }

    private int ValidateDeclaredResource(RenderResourceId id)
    {
        var index = ValidateResourceId(id);
        if (!_resources[index].Declared)
        {
            throw new InvalidOperationException($"Render resource {id} is not declared.");
        }

        return index;
    }

    private void ValidateGeneration(int generation)
    {
        if (generation != _generation)
        {
            throw new InvalidOperationException(
                "The compiled render-graph plan was invalidated by a later compile or declaration.");
        }
    }

    private void InvalidateCompiledPlan()
    {
        _compiledPassCount = 0;
        _generation++;
    }

    private readonly record struct PassNode(
        RenderPassDescriptor Descriptor,
        int ReadStart,
        int ReadCount,
        int WriteStart,
        int WriteCount,
        bool Declared);

    private readonly record struct ResourceNode(
        RenderResourceDescriptor Descriptor,
        bool Declared);
}
