using System;

namespace GaldrApp.Services.Abstractions;

public interface ILoggingService
{
    void Debug(string source, string message);
    void Info(string source, string message);
    void Warn(string source, string message);
    void Error(string source, string message, Exception ex = null);
}
