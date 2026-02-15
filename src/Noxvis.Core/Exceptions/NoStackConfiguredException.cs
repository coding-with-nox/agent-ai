namespace Noxvis.Core.Exceptions;

/// <summary>
/// Thrown when a stack-dependent command executes without an active stack.
/// </summary>
public sealed class NoStackConfiguredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoStackConfiguredException"/> class.
    /// </summary>
    public NoStackConfiguredException()
        : base("No stack is configured. Run '/stack set <preset>' first.")
    {
    }
}
