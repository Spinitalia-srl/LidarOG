namespace LidarOG;

public class FilterObj<T>
{
    // List<float[]>[,] vs float[,][][]
    public FilterObj(FilterObj<T> _filter) {
        _mFilterFunc = new(_filter._mFilterFunc);
    }
    public FilterObj(Func<T, T> filter)
    {
        _mFilterFunc = filter;
    }
    public T Filter(T input)
    {
        if(_mFilterFunc == null) throw new Exception("Uninitialized filter");
        return _mFilterFunc(input);
    }

    private Func<T, T>? _mFilterFunc;
}