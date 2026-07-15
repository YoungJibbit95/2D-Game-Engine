using System.Globalization;

namespace Game.Core.Commands;

public sealed class CommandArguments
{
    private readonly CommandSpecification _specification;
    private readonly IReadOnlyList<string> _values;

    internal CommandArguments(CommandSpecification specification, IReadOnlyList<string> values)
    {
        _specification = specification;
        _values = values;
    }

    public int Count => _values.Count;

    public IReadOnlyList<string> Raw => _values;

    public bool Has(string name)
    {
        var index = FindIndex(name);
        return index >= 0 && index < _values.Count;
    }

    public string GetString(string name)
    {
        return _values[GetRequiredIndex(name)];
    }

    public string? GetOptionalString(string name)
    {
        var index = FindIndex(name);
        return index >= 0 && index < _values.Count ? _values[index] : null;
    }

    public int GetInt32(string name)
    {
        return int.Parse(GetString(name), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public double GetDouble(string name)
    {
        return double.Parse(GetString(name), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    public float GetSingle(string name)
    {
        return float.Parse(GetString(name), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private int GetRequiredIndex(string name)
    {
        var index = FindIndex(name);
        if (index < 0 || index >= _values.Count)
        {
            throw new InvalidOperationException($"Argument '{name}' was not provided.");
        }

        return index;
    }

    private int FindIndex(string name)
    {
        for (var index = 0; index < _specification.Arguments.Count; index++)
        {
            if (string.Equals(_specification.Arguments[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        throw new ArgumentException($"Argument '{name}' is not part of /{_specification.Name}.", nameof(name));
    }
}
