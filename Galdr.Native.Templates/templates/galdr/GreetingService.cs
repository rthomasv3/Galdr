namespace GaldrApp;

internal class GreetingService
{
    public string Greet(string name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        string target = trimmed.Length > 0 ? trimmed : "World";
        return $"Hello, {target}";
    }
}
