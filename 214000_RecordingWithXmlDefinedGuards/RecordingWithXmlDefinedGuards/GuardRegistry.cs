namespace RecordingWithXmlDefinedGuards;

/// <summary>Maps XML-defined guard names to their runtime implementations.</summary>
public sealed class GuardRegistry
{
    private readonly Dictionary<string, Func<bool>> _guards = [];

    /// <summary>Registers a guard implementation with a given name.</summary>
    /// <param name="name">The guard name as it appears in XML; must be non-empty.</param>
    /// <param name="evaluate">A synchronous, side-effect-free function that returns whether the guard is satisfied.</param>
    /// <exception cref="ArgumentException">The name is null, empty, or already registered.</exception>
    /// <exception cref="ArgumentNullException">The evaluate function is null.</exception>
    public void Register(string name, Func<bool> evaluate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(evaluate);

        if (!_guards.TryAdd(name, evaluate))
        {
            throw new ArgumentException($"Guard '{name}' is already registered.", nameof(name));
        }
    }

    /// <summary>Evaluates a registered guard by name.</summary>
    /// <param name="guardName">The guard name as declared in XML.</param>
    /// <returns>The result returned by the registered guard implementation.</returns>
    /// <exception cref="ArgumentException">The name is null, empty, or not registered.</exception>
    internal bool Evaluate(string guardName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guardName);

        if (!_guards.TryGetValue(guardName, out Func<bool>? evaluate))
        {
            throw new ArgumentException($"Guard '{guardName}' is not registered.", nameof(guardName));
        }

        return evaluate();
    }
}
