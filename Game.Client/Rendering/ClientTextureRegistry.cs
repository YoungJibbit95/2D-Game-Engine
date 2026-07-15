using System.Diagnostics;
using Game.Client.Rendering.TextureGroups;
using Game.Core.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class ClientTextureRegistry : IDisposable
{
    private readonly string _contentRoot;
    private readonly SpriteAssetRegistry _assets;
    private readonly ITextureResourceFactory _resourceFactory;
    private readonly TextureResourceGroupBudgets _budgets;
    private readonly Dictionary<TextureFrameKey, SpriteTexture> _frames = new();
    private readonly Dictionary<string, ResourceEntry> _resources = new(PathComparer);
    private readonly Dictionary<ITextureResource, ResourceEntry> _entriesByResource =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, ResourcePlan> _plansByPath = new(PathComparer);
    private readonly Dictionary<string, ResourcePlan> _plansByAssetId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _resourcePathsByAssetId = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _placeholderAssetIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly GroupState[] _groupStates = new GroupState[TextureResourceGroups.Ordered.Length];
    private readonly List<ResourcePlan> _orderedPlans = new();
    private readonly bool[] _preloadedGroups = new bool[TextureResourceGroups.Ordered.Length];
    private readonly int _expectedPreloadedFrameCount;
    private int _preloadedGroupCount;
    private int _placeholderResourceCount;
    private int _invalidResourceCount;
    private int _pinnedResourceCount;
    private long _fileLoadCount;
    private long _resourceLoadTicks;
    private long _resourceLoadAllocatedBytes;
    private long _residentDecodedTextureBytes;
    private long _evictedResourceCount;
    private long _evictedDecodedTextureBytes;
    private long _budgetRejectedResourceCount;
    private long _accessSequence;
    private bool _preloadModeActivated;
    private bool _disposed;

    public ClientTextureRegistry(GraphicsDevice graphicsDevice, string contentRoot, SpriteAssetRegistry assets)
        : this(contentRoot, assets, new MonoGameTextureResourceFactory(graphicsDevice))
    {
    }

    public ClientTextureRegistry(
        GraphicsDevice graphicsDevice,
        string contentRoot,
        SpriteAssetRegistry assets,
        TextureResourceGroupBudgets budgets)
        : this(contentRoot, assets, new MonoGameTextureResourceFactory(graphicsDevice), budgets)
    {
    }

    public ClientTextureRegistry(
        string contentRoot,
        SpriteAssetRegistry assets,
        ITextureResourceFactory resourceFactory,
        TextureResourceGroupBudgets? budgets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        _contentRoot = Path.GetFullPath(contentRoot);
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _resourceFactory = resourceFactory ?? throw new ArgumentNullException(nameof(resourceFactory));
        _budgets = budgets ?? new TextureResourceGroupBudgets();

        for (var index = 0; index < _groupStates.Length; index++)
        {
            var group = (TextureResourceGroup)index;
            _groupStates[index] = new GroupState(group, _budgets[group]);
        }

        var definitions = new List<SpriteAssetDefinition>(_assets.Definitions);
        definitions.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Id, right.Id));
        foreach (var asset in definitions)
        {
            _expectedPreloadedFrameCount += Math.Max(1, asset.Frames.Count);
            AddResourcePlan(asset);
        }

        _orderedPlans.AddRange(_plansByPath.Values);
        _orderedPlans.Sort(static (left, right) => PathComparer.Compare(left.CanonicalPath, right.CanonicalPath));

        var fallbackPlan = _plansByAssetId[_assets.Fallback.Id];
        if (fallbackPlan.ExpectedDecodedBytes > _budgets[fallbackPlan.Group])
        {
            throw new ArgumentException(
                $"The {fallbackPlan.Group} decoded-byte budget must reserve at least " +
                $"{fallbackPlan.ExpectedDecodedBytes} bytes for the system fallback texture.",
                nameof(budgets));
        }
    }

    public TextureRegistryTelemetry Telemetry => new(
        _resources.Count,
        _frames.Count,
        _placeholderResourceCount,
        _invalidResourceCount,
        _fileLoadCount,
        _resourceLoadTicks * 1000d / Stopwatch.Frequency,
        _resourceLoadAllocatedBytes,
        _residentDecodedTextureBytes);

    public TextureResidencyTelemetry ResidencyTelemetry => new(
        _resources.Count,
        _residentDecodedTextureBytes,
        _evictedResourceCount,
        _evictedDecodedTextureBytes,
        _pinnedResourceCount,
        _budgetRejectedResourceCount);

    public bool IsPreloaded => _preloadedGroupCount == _preloadedGroups.Length;

    public int ExpectedPreloadedFrameCount => _expectedPreloadedFrameCount;

    public IReadOnlyCollection<string> PlaceholderAssetIds => _placeholderAssetIds;

    public string FallbackAssetId => _assets.Fallback.Id;

    public TextureResourceGroup GetGroup(string spriteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ThrowIfDisposed();
        return _plansByAssetId[_assets.GetById(spriteId).Id].Group;
    }

    public TextureResourceGroupTelemetry GetGroupTelemetry(TextureResourceGroup group)
    {
        ThrowIfDisposed();
        ValidateGroup(group);
        var state = _groupStates[(int)group];
        return new TextureResourceGroupTelemetry(
            group,
            state.DecodedByteBudget,
            state.ResidentResourceCount,
            state.ResidentDecodedBytes,
            state.EvictedResourceCount,
            state.EvictedDecodedBytes,
            state.PinnedResourceCount,
            state.BudgetRejectedResourceCount);
    }

    public bool IsResident(string spriteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ThrowIfDisposed();
        return _plansByAssetId[_assets.GetById(spriteId).Id].Entry is not null;
    }

    public void PreloadAll()
    {
        PreloadGroups(TextureResourceGroups.Ordered);
    }

    public void PreloadGroup(TextureResourceGroup group)
    {
        PreloadGroups(group);
    }

    public void PreloadGroups(params TextureResourceGroup[] groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ThrowIfDisposed();
        _preloadModeActivated = true;
        EnsureFallbackResident();

        var requested = new bool[_preloadedGroups.Length];
        foreach (var group in groups)
        {
            ValidateGroup(group);
            requested[(int)group] = true;
        }

        foreach (var group in TextureResourceGroups.Ordered)
        {
            var groupIndex = (int)group;
            if (!requested[groupIndex] || _preloadedGroups[groupIndex])
            {
                continue;
            }

            foreach (var plan in _orderedPlans)
            {
                if (plan.Group == group && TryEnsureResident(plan, allowEviction: false))
                {
                    MaterializeFrames(plan);
                }
            }

            _preloadedGroups[groupIndex] = true;
            _preloadedGroupCount++;
        }
    }

    public bool MakeResident(string spriteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ThrowIfDisposed();
        _preloadModeActivated = true;
        EnsureFallbackResident();

        var asset = _assets.GetById(spriteId);
        var plan = _plansByAssetId[asset.Id];
        if (!TryEnsureResident(plan, allowEviction: true))
        {
            return false;
        }

        MaterializeFrames(plan);
        return true;
    }

    public TextureResourceLease Acquire(string spriteId, int frameIndex = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ThrowIfDisposed();

        var asset = _assets.GetById(spriteId);
        var plan = _plansByAssetId[asset.Id];
        var entry = plan.Entry ?? throw new InvalidOperationException(
            $"Sprite resource '{asset.Id}' is not resident. Preload its group or call MakeResident before acquiring a lease.");
        var sprite = GetCore(asset, frameIndex);
        if (entry.PinCount++ == 0)
        {
            _pinnedResourceCount++;
            entry.GroupState.PinnedResourceCount++;
        }

        Touch(entry);
        return new TextureResourceLease(this, sprite.Resource, sprite);
    }

    public SpriteTexture Get(string spriteId, int frameIndex = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ThrowIfDisposed();

        var asset = _assets.GetById(spriteId);
        return GetCore(asset, frameIndex);
    }

    public SpriteTexture GetForAutoTileMask(string spriteId, int autoTileMask)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ThrowIfDisposed();

        var asset = _assets.GetById(spriteId);
        return GetCore(asset, asset.ResolveFrameIndexForAutoTileMask(autoTileMask));
    }

    public bool TryGetRealTexture(string spriteId, out SpriteTexture sprite, int frameIndex = 0)
    {
        sprite = Get(spriteId, frameIndex);
        return !sprite.IsPlaceholder;
    }

    public bool TryGetRealTextureForAutoTileMask(string spriteId, int autoTileMask, out SpriteTexture sprite)
    {
        sprite = GetForAutoTileMask(spriteId, autoTileMask);
        return !sprite.IsPlaceholder;
    }

    internal void ReleaseLease(ITextureResource resource)
    {
        if (_disposed || !_entriesByResource.TryGetValue(resource, out var entry) || entry.PinCount <= 0)
        {
            return;
        }

        entry.PinCount--;
        if (entry.PinCount == 0)
        {
            _pinnedResourceCount--;
            entry.GroupState.PinnedResourceCount--;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var entry in _resources.Values)
        {
            entry.Resource.Dispose();
            entry.Plan.Entry = null;
        }

        _frames.Clear();
        _resources.Clear();
        _entriesByResource.Clear();
        _resourcePathsByAssetId.Clear();
        _placeholderAssetIds.Clear();
        _placeholderResourceCount = 0;
        _pinnedResourceCount = 0;
        _residentDecodedTextureBytes = 0;
        _preloadedGroupCount = 0;
        Array.Clear(_preloadedGroups);
        foreach (var state in _groupStates)
        {
            state.ResidentResourceCount = 0;
            state.ResidentDecodedBytes = 0;
            state.PinnedResourceCount = 0;
        }

        _disposed = true;
    }

    private SpriteTexture GetCore(SpriteAssetDefinition asset, int frameIndex)
    {
        var resolvedFrameIndex = ResolveFrameIndex(asset, frameIndex);
        var frameKey = new TextureFrameKey(asset.Id, resolvedFrameIndex);
        if (_frames.TryGetValue(frameKey, out var cached))
        {
            Touch(_entriesByResource[cached.Resource]);
            return cached;
        }

        var plan = _plansByAssetId[asset.Id];
        if (plan.Entry is null && !_preloadModeActivated)
        {
            _ = TryEnsureResident(plan, allowEviction: true);
        }

        if (plan.Entry is not null)
        {
            return CreateFrame(asset, resolvedFrameIndex, plan.Entry);
        }

        EnsureFallbackResident();
        var fallback = _assets.Fallback;
        var fallbackKey = new TextureFrameKey(fallback.Id, 0);
        if (_frames.TryGetValue(fallbackKey, out var fallbackFrame))
        {
            Touch(_entriesByResource[fallbackFrame.Resource]);
            return fallbackFrame;
        }

        var fallbackEntry = _plansByAssetId[fallback.Id].Entry
            ?? throw new InvalidOperationException("The system fallback texture is not resident.");
        return CreateFrame(fallback, 0, fallbackEntry);
    }

    private void EnsureFallbackResident()
    {
        var fallbackPlan = _plansByAssetId[_assets.Fallback.Id];
        if (fallbackPlan.Entry is null && !TryEnsureResident(fallbackPlan, allowEviction: false, ignoreBudget: true))
        {
            throw new InvalidOperationException("The system fallback texture could not be made resident.");
        }

        fallbackPlan.IsPermanent = true;
        MaterializeFrames(fallbackPlan);
    }

    private bool TryEnsureResident(ResourcePlan plan, bool allowEviction, bool ignoreBudget = false)
    {
        if (plan.Entry is { } existing)
        {
            Touch(existing);
            return true;
        }

        var state = _groupStates[(int)plan.Group];
        if (!ignoreBudget && !TryReserveDecodedBytes(state, plan.ExpectedDecodedBytes, allowEviction))
        {
            _budgetRejectedResourceCount++;
            state.BudgetRejectedResourceCount++;
            return false;
        }

        var hasRealTexture = File.Exists(plan.CanonicalPath);
        var startedAt = Stopwatch.GetTimestamp();
        var allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
        ITextureResource? resource = null;
        try
        {
            resource = hasRealTexture
                ? _resourceFactory.LoadFromFile(plan.CanonicalPath)
                : _resourceFactory.CreatePlaceholder(plan.Assets[0]);
            foreach (var asset in plan.Assets)
            {
                ValidateResourceDimensions(asset, resource, plan.CanonicalPath);
            }
        }
        catch
        {
            resource?.Dispose();
            throw;
        }

        _resourceLoadTicks += Stopwatch.GetTimestamp() - startedAt;
        _resourceLoadAllocatedBytes += Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedAtStart);
        if (hasRealTexture)
        {
            _fileLoadCount++;
        }
        else
        {
            _placeholderResourceCount++;
            foreach (var asset in plan.Assets)
            {
                _placeholderAssetIds.Add(asset.Id);
            }
        }

        var entry = new ResourceEntry(plan, resource, state);
        plan.Entry = entry;
        _resources.Add(plan.CanonicalPath, entry);
        _entriesByResource.Add(resource, entry);
        _residentDecodedTextureBytes += resource.DecodedByteCount;
        state.ResidentResourceCount++;
        state.ResidentDecodedBytes += resource.DecodedByteCount;
        Touch(entry);
        return true;
    }

    private bool TryReserveDecodedBytes(GroupState state, long requiredBytes, bool allowEviction)
    {
        if (requiredBytes > state.DecodedByteBudget)
        {
            return false;
        }

        while (state.ResidentDecodedBytes > state.DecodedByteBudget - requiredBytes)
        {
            if (!allowEviction || !TryEvictLeastRecentlyUsed(state))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryEvictLeastRecentlyUsed(GroupState state)
    {
        ResourceEntry? candidate = null;
        foreach (var entry in _resources.Values)
        {
            if (!ReferenceEquals(entry.GroupState, state) || entry.PinCount > 0 || entry.Plan.IsPermanent)
            {
                continue;
            }

            if (candidate is null ||
                entry.LastAccessSequence < candidate.LastAccessSequence ||
                (entry.LastAccessSequence == candidate.LastAccessSequence &&
                 PathComparer.Compare(entry.Plan.CanonicalPath, candidate.Plan.CanonicalPath) < 0))
            {
                candidate = entry;
            }
        }

        if (candidate is null)
        {
            return false;
        }

        Evict(candidate);
        return true;
    }

    private void Evict(ResourceEntry entry)
    {
        foreach (var frameKey in entry.FrameKeys)
        {
            _frames.Remove(frameKey);
        }

        var decodedBytes = entry.Resource.DecodedByteCount;
        var isPlaceholder = entry.Resource.IsPlaceholder;
        entry.Resource.Dispose();
        _resources.Remove(entry.Plan.CanonicalPath);
        _entriesByResource.Remove(entry.Resource);
        entry.Plan.Entry = null;

        _residentDecodedTextureBytes -= decodedBytes;
        _evictedResourceCount++;
        _evictedDecodedTextureBytes += decodedBytes;
        entry.GroupState.ResidentResourceCount--;
        entry.GroupState.ResidentDecodedBytes -= decodedBytes;
        entry.GroupState.EvictedResourceCount++;
        entry.GroupState.EvictedDecodedBytes += decodedBytes;
        if (isPlaceholder)
        {
            _placeholderResourceCount--;
            foreach (var asset in entry.Plan.Assets)
            {
                _placeholderAssetIds.Remove(asset.Id);
            }
        }
    }

    private void MaterializeFrames(ResourcePlan plan)
    {
        var entry = plan.Entry ?? throw new InvalidOperationException("Cannot materialize frames for a non-resident resource.");
        foreach (var asset in plan.Assets)
        {
            var frameCount = Math.Max(1, asset.Frames.Count);
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var frameKey = new TextureFrameKey(asset.Id, frameIndex);
                if (!_frames.ContainsKey(frameKey))
                {
                    _ = CreateFrame(asset, frameIndex, entry);
                }
            }
        }
    }

    private SpriteTexture CreateFrame(SpriteAssetDefinition asset, int frameIndex, ResourceEntry entry)
    {
        var frameKey = new TextureFrameKey(asset.Id, frameIndex);
        var source = ResolveSourceRectangle(asset, frameIndex, entry.Resource.Width, entry.Resource.Height);
        var sprite = new SpriteTexture(asset.Id, entry.Resource, source);
        _frames.Add(frameKey, sprite);
        entry.FrameKeys.Add(frameKey);
        Touch(entry);
        return sprite;
    }

    private void AddResourcePlan(SpriteAssetDefinition asset)
    {
        var canonicalPath = GetCanonicalResourcePath(asset);
        var assetGroup = TextureResourceGroups.FromCategory(asset.Category);
        if (!_plansByPath.TryGetValue(canonicalPath, out var plan))
        {
            plan = new ResourcePlan(canonicalPath, assetGroup, asset.Width * (long)asset.Height * 4L);
            _plansByPath.Add(canonicalPath, plan);
        }
        else if ((int)assetGroup < (int)plan.Group)
        {
            plan.Group = assetGroup;
        }

        plan.Assets.Add(asset);
        _plansByAssetId.Add(asset.Id, plan);
    }

    private string GetCanonicalResourcePath(SpriteAssetDefinition asset)
    {
        if (_resourcePathsByAssetId.TryGetValue(asset.Id, out var cached))
        {
            return cached;
        }

        var sourceRoot = string.IsNullOrWhiteSpace(asset.SourceRoot)
            ? _contentRoot
            : Path.GetFullPath(asset.SourceRoot);
        var normalized = NormalizePath(asset.Path);
        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException($"Sprite asset '{asset.Id}' must use a relative source path.");
        }

        var canonicalPath = Path.GetFullPath(Path.Combine(sourceRoot, normalized));
        var relativePath = Path.GetRelativePath(sourceRoot, canonicalPath);
        if (Path.IsPathRooted(relativePath) ||
            string.Equals(relativePath, "..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Sprite asset '{asset.Id}' path '{asset.Path}' escapes source root '{sourceRoot}'.");
        }

        _resourcePathsByAssetId.Add(asset.Id, canonicalPath);
        return canonicalPath;
    }

    private static int ResolveFrameIndex(SpriteAssetDefinition asset, int frameIndex)
    {
        return asset.Frames.Count == 0
            ? 0
            : Math.Clamp(frameIndex, 0, asset.Frames.Count - 1);
    }

    private static Rectangle ResolveSourceRectangle(SpriteAssetDefinition asset, int frameIndex, int textureWidth, int textureHeight)
    {
        if (textureWidth <= 0 || textureHeight <= 0)
        {
            return Rectangle.Empty;
        }

        if (asset.Frames.Count == 0)
        {
            return new Rectangle(0, 0, Math.Min(asset.Width, textureWidth), Math.Min(asset.Height, textureHeight));
        }

        var frame = asset.Frames[Math.Clamp(frameIndex, 0, asset.Frames.Count - 1)];
        if (frame.X >= textureWidth || frame.Y >= textureHeight)
        {
            return new Rectangle(0, 0, Math.Min(asset.Width, textureWidth), Math.Min(asset.Height, textureHeight));
        }

        return new Rectangle(
            frame.X,
            frame.Y,
            Math.Max(1, Math.Min(frame.Width, textureWidth - frame.X)),
            Math.Max(1, Math.Min(frame.Height, textureHeight - frame.Y)));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }

    private void ValidateResourceDimensions(
        SpriteAssetDefinition asset,
        ITextureResource resource,
        string canonicalPath)
    {
        if (resource.IsPlaceholder || (resource.Width == asset.Width && resource.Height == asset.Height))
        {
            return;
        }

        _invalidResourceCount++;
        throw new InvalidDataException(
            $"Sprite asset '{asset.Id}' expects {asset.Width}x{asset.Height}, " +
            $"but '{canonicalPath}' decoded as {resource.Width}x{resource.Height}.");
    }

    private void Touch(ResourceEntry entry)
    {
        entry.LastAccessSequence = ++_accessSequence;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void ValidateGroup(TextureResourceGroup group)
    {
        if ((uint)group >= TextureResourceGroups.Ordered.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown texture resource group.");
        }
    }

    private sealed class ResourcePlan
    {
        public ResourcePlan(string canonicalPath, TextureResourceGroup group, long expectedDecodedBytes)
        {
            CanonicalPath = canonicalPath;
            Group = group;
            ExpectedDecodedBytes = expectedDecodedBytes;
        }

        public string CanonicalPath { get; }

        public TextureResourceGroup Group { get; set; }

        public long ExpectedDecodedBytes { get; }

        public List<SpriteAssetDefinition> Assets { get; } = new();

        public ResourceEntry? Entry { get; set; }

        public bool IsPermanent { get; set; }
    }

    private sealed class ResourceEntry
    {
        public ResourceEntry(ResourcePlan plan, ITextureResource resource, GroupState groupState)
        {
            Plan = plan;
            Resource = resource;
            GroupState = groupState;
        }

        public ResourcePlan Plan { get; }

        public ITextureResource Resource { get; }

        public GroupState GroupState { get; }

        public HashSet<TextureFrameKey> FrameKeys { get; } = new();

        public long LastAccessSequence { get; set; }

        public int PinCount { get; set; }
    }

    private sealed class GroupState
    {
        public GroupState(TextureResourceGroup group, long decodedByteBudget)
        {
            Group = group;
            DecodedByteBudget = decodedByteBudget;
        }

        public TextureResourceGroup Group { get; }

        public long DecodedByteBudget { get; }

        public int ResidentResourceCount { get; set; }

        public long ResidentDecodedBytes { get; set; }

        public long EvictedResourceCount { get; set; }

        public long EvictedDecodedBytes { get; set; }

        public int PinnedResourceCount { get; set; }

        public long BudgetRejectedResourceCount { get; set; }
    }

    private static StringComparer PathComparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
