using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.Drawing;
using System.Diagnostics;

namespace LidarOG;
using FilterInput = List<GridPt>;
using LidarFilter = FilterObj<List<GridPt>>;


public struct GridPt
{
    public float[] pt = new float[3];
    public int[] id = new int[2];

    public GridPt()
    {
    }
    public GridPt(float[] _pt, int[] _id) {
        pt = _pt;
        id = _id;
    }
}

public interface ILidar: IDisposable
{
    void StartListening();
    void StopListening();
    bool AddFilter(FilterObj<FilterInput> filter);
    List<GridPt> GetGrid();
    FilterInput Parse(byte[] msg);
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
        _mGrids = new List<GridPt>();
        StartListening();
    }

    public void Listen()
    {
        Running = true;
        while (Active)
        {
            if (_mListener.Available > 0)
            {
                byte[] msg = _mListener.Receive(ref _mIpEndPoint);
                FilterInput grid = Parse(msg);
                if (!(_mFilterQueue == null || _mFilterQueue.IsEmpty))
                    foreach (LidarFilter filter in _mFilterQueue)
                        grid = filter.Filter(grid);
                //if (_mGrids is { Count: >= 200 }) _mGrids.TryDequeue(out _);
                if (_mGrids != null) _mGrids.AddRange(grid);
                else _mGrids = new FilterInput(grid);
            }
            else
            {
                Thread.Sleep(100);
            }
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
        if (Active) return;
        Active = true;
        _mParseTask = new Task(Listen);
        _mParseTask.Start();
    }

    public void StopListening()
    {
        if(!Active) return;
        Active = false;
        _mParseTask?.Wait();
    }

    public bool AddFilter(LidarFilter filter)
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
    public List<GridPt> GetGrid()
    {
        if (_mGrids == null) throw new Exception("Null grid");
        var tmp = new List<GridPt>(_mGrids);
        _mGrids = new();
        return tmp ?? throw new Exception("No grid available");
    }

    public FilterInput Parse(byte[] msg)
    {
        double distanceUnit = msg[9]/1000.0f; //expressed in mm
        byte[] payload = msg.Skip(12).ToArray();
        List<GridPt> inGrid = new();
        for (int i = 0; i < 8; ++i) // 8 Blocks in a payload - 32 channels each
        {
            double azimuth = (Math.PI/180.0f)*(BitConverter.ToInt16(payload, 0)/100.0f); // it is in hundreds of degree
            for (int j = 0; j < 32; ++j)
            {
                double elevation = (Math.PI / 180.0f) * (15 - j);
                double distance = BitConverter.ToUInt16(payload, 2 + j * 4) * distanceUnit;
                float[] pt = new float[3];
                //Point expressed in Lidar Frame
                pt[0] = (float)(distance * Math.Sin(azimuth) * Math.Cos(elevation));
                pt[1] = (float)(distance * Math.Cos(azimuth) * Math.Cos(elevation));
                pt[2] = (float)(distance * Math.Sin(elevation));
                //populate grid:
                inGrid.Add(new GridPt(pt, [0, 0]));
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
    private ConcurrentQueue<LidarFilter>? _mFilterQueue = new();
    private FilterInput? _mGrids;
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
    public Mid360(string ip, int port = 56301, int gridSize = 50, float side = 1.0f)
    {
        GridSize = gridSize;
        Side = side;
        _mParseTask = null;
        _mIpEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _mListener = new UdpClient(_mIpEndPoint);
        _mGrids = new List<GridPt>();
        StartListening();
    }

    public void Listen()
    {
        Running = true;
        while (Active)
        {
            if (_mListener.Available > 0)
            {
                byte[] msg = _mListener.Receive(ref _mIpEndPoint);
                FilterInput grid = Parse(msg);
                if (!(_mFilterQueue == null || _mFilterQueue.IsEmpty))
                    foreach (LidarFilter filter in _mFilterQueue)
                        grid = filter.Filter(grid);
                //if (_mGrids is { Count: >= 200 }) _mGrids.TryDequeue(out _);
                if (_mGrids != null) _mGrids.AddRange(grid);
                else _mGrids = new FilterInput(grid);
            }
            else
            {
                Thread.Sleep(100);
            }
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
        if (Active) return;
        Active = true;
        _mParseTask = new Task(Listen);
        _mParseTask.Start();
    }

    public void StopListening()
    {
        if (!Active) return;
        Active = false;
        _mParseTask?.Wait();
    }

    public bool AddFilter(LidarFilter filter)
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
    public List<GridPt> GetGrid()
    {
        if (_mGrids == null) throw new Exception("Null grid");
        var tmp = new List<GridPt>(_mGrids);
        _mGrids = new();
        return tmp ?? throw new Exception("No grid available");
    }

    public FilterInput Parse(byte[] msg)
    {
        int dot_num = BitConverter.ToUInt16(msg.Skip(5).ToArray());
        int data_type = msg[10];
        if (data_type != 1) throw new NotImplementedException("Data type not supported");
        byte[] payload = msg.Skip(36).ToArray();
        List<GridPt> inGrid = new();
        for (int i = 0; i < dot_num; ++i) // number of points present in the payload
        {
            float[] pt = new float[3];
            pt[0] = (float)(BitConverter.ToInt32(msg.Skip(36 + i * 14).ToArray()))/1000.0f;
            pt[1] = (float)(BitConverter.ToInt32(msg.Skip(40 + i * 14).ToArray()))/1000.0f;
            pt[2] = (float)(BitConverter.ToInt32(msg.Skip(44 + i * 14).ToArray()))/1000.0f;
            //byte refl = msg[49 + i * 14];
            //byte tag = msg[50 + i * 14];
            if (pt[0] == 0 && pt[1] == 0 && pt[2] == 0) continue;
            inGrid.Add(new GridPt(pt, [0, 0]));
            //populate grid:
            //int indexX = (int)((pt[0] + GridSize * Side / 2) / Side);
            //int indexY = (int)((pt[1] + GridSize * Side / 2) / Side);
            //if (indexX >= GridSize || indexY >= GridSize || indexX < 0 || indexY < 0) continue;
            //else inGrid.Add(new GridPt(pt, [indexX, indexY]));
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
    private ConcurrentQueue<LidarFilter>? _mFilterQueue = new();
    private FilterInput? _mGrids;
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