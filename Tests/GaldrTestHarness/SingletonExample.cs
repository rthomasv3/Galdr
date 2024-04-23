namespace GaldrTestHarness;

internal sealed class SingletonExample
{
    private int _count = 0;

    public int Increment()
    {
        _count++;
        return _count;
    }
}
