using Game.Client.GameStates;
using Game.Core.Commands;
using Game.Core.DeveloperTools;
using System.Reflection;
using Xunit;

namespace Game.Tests.DeveloperToolsTests;

public sealed class DeveloperIntentHandlerParityTests
{
    [Fact]
    public void PlayingStateHandler_ReferencesEveryDeclaredRequestIntentType()
    {
        var registry = CommandRegistry.CreateDefault();
        var expected = registry.Commands
            .Select(registry.GetSpecification)
            .Where(specification => specification.RequestIntentType is not null)
            .Select(specification => specification.RequestIntentType!)
            .Distinct()
            .ToArray();
        var handler = typeof(PlayingState).GetMethod(
            "HandleDeveloperIntent",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(handler);
        var referenced = ResolveReferencedIntentTypes(handler!);
        Assert.All(expected, intentType => Assert.Contains(intentType, referenced));
    }

    private static ISet<Type> ResolveReferencedIntentTypes(MethodInfo method)
    {
        var body = method.GetMethodBody();
        Assert.NotNull(body);
        var il = body!.GetILAsByteArray();
        Assert.NotNull(il);

        var result = new HashSet<Type>();
        for (var index = 0; index <= il!.Length - sizeof(int); index++)
        {
            var token = BitConverter.ToInt32(il, index);
            var table = token & unchecked((int)0xff000000);
            if (table is not 0x01000000 and not 0x02000000 and not 0x1b000000)
            {
                continue;
            }

            try
            {
                var type = method.Module.ResolveType(token);
                if (typeof(IDeveloperCommandIntent).IsAssignableFrom(type))
                {
                    result.Add(type);
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return result;
    }
}
