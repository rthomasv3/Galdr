using System;

namespace Galdr;

/// <summary>
/// Class used to indicate a class's public methods should be made available for use on the frontend.
/// </summary>
public sealed class CommandsAttribute : Attribute
{
    #region Fields

    private readonly bool _prefixClassName;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="CommandsAttribute"/> class.
    /// </summary>
    /// <param name="prefixClassName">A value indicating if the class name should be used as a prefix on method names (ex. "myClass.MyMethod").</param>
    public CommandsAttribute(bool prefixClassName = false)
    {
        _prefixClassName = prefixClassName;
    }

    #endregion

    #region Properties

    /// <summary>
    /// A value indicating if the class name should be used as a prefix on method names (ex. "myClass.MyMethod").
    /// </summary>
    public bool PrefixClassName => _prefixClassName;

    #endregion

}
