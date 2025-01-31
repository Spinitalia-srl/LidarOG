namespace LidarOG;

public class FilterObj
{
    // List<float[]>[,] vs float[,][][]
    FilterObj(Func<List<float[]>[,], List<float[]>[,]> filter)
    {
        _mFilterFunc = filter;
    }
    public List<float[]>[,] Filter(List<float[]>[,] input)
    {
        if(_mFilterFunc == null) throw new Exception("Uninitialized filter");
        return _mFilterFunc(input);
    }

    private Func<List<float[]>[,], List<float[]>[,]>? _mFilterFunc;
}