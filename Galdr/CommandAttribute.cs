using System;

namespace Galdr;

public sealed class CommandAttribute : Attribute
{
    #region Fields

    private readonly string _name;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="CommandAttribute"/> class.
    /// </summary>
    public CommandAttribute(string name = null)
    {
        _name = name;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The name of the command.
    /// </summary>
    public string Name => _name;

    #endregion
}
