using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.Drawing;

namespace LidarOG;

public interface ILidar: IDisposable
{
    void StartListening();
    void StopListening();
    bool AddFilter(FilterObj filter);
    List<float[]>[,] GetGrid();
    List<float[]>[,] Parse(byte[] msg);
    public bool isRunning();
    public void Dispose();
}

public class PandarXT : ILidar
{
    public PandarXT(string ip, int port = 2368, int gridSize = 50, float side = 1.0f)
    {
        GridSize = gridSize;
        Side = side;
        _mParseTask = null;
        _mListener = new UdpClient(port);
        _mIpEndPoint = new IPEndPoint(IPAddress.Any, port);
        StartListening();
    }

    public void Listen()
    {
        Running = true;
        while (Active)
        {
            byte[] msg = _mListener.Receive(ref _mIpEndPoint);
            List<float[]>[,] grid = Parse(msg);
            if(!(_mFilterQueue == null || _mFilterQueue.IsEmpty))
                foreach (FilterObj filter in _mFilterQueue)
                    grid = filter.Filter(grid);
            if (_mGrids is { Count: >= 10 }) _mGrids.TryDequeue(out _);
            _mGrids?.Enqueue(grid);
        }
        Running = false;
    }
    
    #region Interface

    public bool isRunning()
    {
        return Running;
    }
    public void StartListening()
    {
        Active = true;
        _mParseTask = new Task(Listen);
        _mParseTask.Start();
    }

    public void StopListening()
    {
        Active = false;
        _mParseTask?.Wait();
    }

    public bool AddFilter(FilterObj filter)
    {
        try
        {
            _mFilterQueue?.Enqueue(filter);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }
    public List<float[]>[,] GetGrid()
    {
        var tmp= _mGrids?.Last();
        _mGrids = new ConcurrentQueue<List<float[]>[,]>();
        return tmp ?? throw new Exception("No grid available");
    }

    public List<float[]>[,] Parse(byte[] msg)
    {
        double distanceUnit = msg[9]/1000.0f; //expressed in mm
        byte[] payload = msg.Skip(12).ToArray();
        List<float[]>[,] inGrid = new List<float[]>[GridSize, GridSize];
        for (int i = 0; i < 8; ++i) // 8 Blocks in a payload - 32 channels each
        {
            double azimuth = (Math.PI/180.0f)*(BitConverter.ToInt16(payload, 0)/100.0f); // it is in hundreds of degree
            for (int j = 0; j < 32; ++j)
            {
                double elevation = (Math.PI / 180.0f) * (15 - j);
                double distance = BitConverter.ToUInt16(payload, 2 + j * 4) * distanceUnit;
                float[] pt = new float[3];
                //Point expressed in Lidar Frame
                pt[0] = (float)(distance * Math.Cos(azimuth) * Math.Cos(elevation));
                pt[1] = (float)(distance * Math.Sin(azimuth) * Math.Cos(elevation));
                pt[2] = (float)(distance * Math.Sin(elevation));
                //populate grid:
                int indexX = (int)((pt[0] + GridSize * Side / 2) / Side);
                int indexY = (int)((pt[1] + GridSize * Side / 2) / Side);
                inGrid[indexX, indexY].Add(pt);
            }
        }
        return inGrid;
    }
    #endregion
    #region MEMBERS
    
    private UdpClient _mListener;
    private IPEndPoint _mIpEndPoint;
    private bool _mActive;
    public bool Active { get => _mActive; private set => _mActive = value; }
    private bool _mRunning;
    public bool Running { get => _mRunning; private set => _mRunning = value; }
    private Task? _mParseTask;
    private ConcurrentQueue<FilterObj>? _mFilterQueue = new();
    private ConcurrentQueue<List<float[]>[,]>? _mGrids = new();
    private float _mSide;
    public float Side { get => _mSide; private set => _mSide = value; }
    private int _mGridSize;
    public int GridSize { get => _mGridSize; private set => _mGridSize = value; }
    
    #endregion
    #region Disposable

    public void Dispose()
    {
        Console.WriteLine("Disposing PandarXT...");
        StopListening();
    }
    
    #endregion
}

public class Mid360 : ILidar
{
    public Mid360()
    {
        throw new NotImplementedException();
    }

    public void Listen()
    {
        throw new NotImplementedException();
    }
    #region Interface

    public bool isRunning()
    {
        return Running;
    }
    public void StartListening()
    {
        Active = true;
        _mParseTask = new Task(Listen);
        _mParseTask.Start();
    }

    public void StopListening()
    {
        Active = false;
        _mParseTask?.Wait();
    }

    public bool AddFilter(FilterObj filter)
    {
        try
        {
            _mFilterQueue?.Enqueue(filter);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }
    public List<float[]>[,] GetGrid()
    {
        var tmp= _mGrids?.Last();
        _mGrids = new ConcurrentQueue<List<float[]>[,]>();
        return tmp ?? throw new Exception("No grid available");
    }
    public List<float[]>[,] Parse(byte[] msg)
    {
        throw new NotImplementedException();
    }

    #endregion
    #region MEMBERS
    
    private UdpClient _mListener;
    private IPEndPoint _mIpEndPoint;
    private bool _mActive;
    public bool Active { get => _mActive; private set => _mActive = value; }
    private bool _mRunning;
    public bool Running { get => _mRunning; private set => _mRunning = value; }
    private Task? _mParseTask;
    private ConcurrentQueue<FilterObj>? _mFilterQueue = new();
    private ConcurrentQueue<List<float[]>[,]>? _mGrids = new();
    private float _mSide;
    public float Side { get => _mSide; private set => _mSide = value; }
    private int _mGridSize;
    public int GridSize { get => _mGridSize; private set => _mGridSize = value; }
    
    #endregion
    #region Disposable

    public void Dispose()
    {
        StopListening();
    }
    
    #endregion
}