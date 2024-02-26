using System;

namespace Galdr;

public sealed class CommandAttribute : Attribute
{
    #region Fields

    private readonly string _name;

    #endregion

    #region Constructor

    public CommandAttribute(string name = null)
    {
        _name = name;
    }

    #endregion

    #region Properties

    public string Name => _name;

    #endregion
}
