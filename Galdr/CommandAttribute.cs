﻿using System;

namespace Galdr;

/// <summary>
/// Class used to tag public methods for use on the frontend.
/// </summary>
public sealed class CommandAttribute : Attribute
{
    #region Fields

    private readonly string _name;
    private readonly bool _prefixClassName;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="CommandAttribute"/> class.
    /// </summary>
    /// <param name="name">An optional name to give the command.</param>
    /// <param name="prefixClassName">A value indicating if the class name should be used as a prefix on method names (ex. "myClass.MyMethod").</param>
    public CommandAttribute(string name = null, bool prefixClassName = false)
    {
        _name = name;
        _prefixClassName = prefixClassName;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The name of the command.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// A value indicating if the class name should be used as a prefix on method names (ex. "myClass.MyMethod").
    /// </summary>
    public bool PrefixClassName => _prefixClassName;

    #endregion
}
