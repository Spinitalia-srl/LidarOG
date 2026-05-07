using System;
using System.Buffers.Binary;
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

public abstract class HalfLidar : ILidar {
    protected FilterInput _mGrids = new();
    protected ConcurrentQueue<LidarFilter> _mFilterQueue = new();
    protected object _gridLock = new();
    public bool Running { get; protected set; } = false;

    public virtual bool AddFilter(LidarFilter filter)
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
    public virtual List<GridPt> GetGrid()
    {
        if (_mGrids == null) throw new Exception("Null grid");
        List<GridPt> tmp;
        lock(_gridLock)
            tmp = new List<GridPt>(_mGrids);
        _mGrids = new();
        return tmp ?? throw new Exception("No grid available");
    }
    public virtual bool isRunning()
    {
        return Running;
    }

    public virtual void StartListening()
    {
        throw new NotImplementedException();
    }
    public virtual void StopListening()
    {
        throw new NotImplementedException();
    }

    public virtual FilterInput Parse(byte[] msg)
    {
        throw new NotImplementedException();
    }
    public virtual void Dispose()
    {
        throw new NotImplementedException();
    }
}

public class PandarXT : HalfLidar
{
    public PandarXT(string ip, int port = 2368)
    {
        _mParseTask = null;
        _mIpEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _mListener = new();
        _mGrids = new List<GridPt>();
        StartListening();
    }

    private void Listen(CancellationToken token)
    {
        Running = true;
        _mListener = new UdpClient(_mIpEndPoint);
        while (!token.IsCancellationRequested)
        {
            try
            {
                var tmp = _mListener.ReceiveAsync(token);
                var msg = tmp.Result.Buffer;
                var grid = Parse(msg);
                if (!(_mFilterQueue == null || _mFilterQueue.IsEmpty))
                    foreach (var filter in _mFilterQueue)
                        grid = filter.Filter(grid);
                //if (_mGrids is { Count: >= 200 }) _mGrids.TryDequeue(out _);
                lock (_gridLock)
                {
                    _mGrids.AddRange(grid);
                }
            }
            catch
            {
                Console.WriteLine();
                _mListener.Close();
                _mListener = new UdpClient(_mIpEndPoint);
            }
        }
        _mListener.Close();
        Running = false;
    }
    
    #region Interface
    
    public override void StartListening()
    {
        if (Active) return;
        Active = true;
        _mParseTask = new Task(() => Listen(_cts.Token));
        _mParseTask.Start();
    }

    public override void StopListening()
    {
        if(!Active) return;
        Active = false;
        _mParseTask?.Wait();
        _mListener.Close();
    }

    public override FilterInput Parse(byte[] msg)
    {
        double distanceUnit = msg[9]/1000.0f; //expressed in mm
        byte[] payload = msg.Skip(12).ToArray();
        List<GridPt> inGrid = new();
        for (int i = 0; i < 8; ++i) // 8 Blocks in a payload - 32 channels each
        {
            double azimuth = (Math.PI/180.0f)*(BitConverter.ToUInt16(payload, 0)/100.0f); // it is in hundreds of degree
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
    private CancellationTokenSource _cts = new();
    private Task? _mParseTask;
    
    #endregion
    #region Disposable

    public override void Dispose()
    {
        Console.WriteLine("Disposing PandarXT...");
        StopListening();
    }
    
    #endregion
}

public class Mid360 : HalfLidar
{
    public Mid360(string ip, int port = 56301)
    {
        _mParseTask = null;
        _mIpEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _mListener = new();
        _mGrids = new List<GridPt>();
        StartListening();
    }

    private void Listen(CancellationToken token)
    {
        Running = true;
        _mListener = new UdpClient(_mIpEndPoint);
        while (!token.IsCancellationRequested)
        {
            try
            {
                var tmp = _mListener.ReceiveAsync(token);
                var msg = tmp.Result.Buffer;
                var grid = Parse(msg);
                if (!(_mFilterQueue == null || _mFilterQueue.IsEmpty))
                    foreach (var filter in _mFilterQueue)
                        grid = filter.Filter(grid);
                //if (_mGrids is { Count: >= 200 }) _mGrids.TryDequeue(out _);
                lock (_gridLock)
                {
                    _mGrids.AddRange(grid);
                }
            }
            catch
            {
                Console.WriteLine();
                _mListener.Close();
                _mListener = new UdpClient(_mIpEndPoint);
            }
        }
        _mListener.Close();
        Running = false;
    }
    #region Interface
    
    public override void StartListening()
    {
        if (Active) return;
        Active = true;
        _mParseTask = new Task(() => Listen(_cts.Token));
        _mParseTask.Start();
    }

    public override void StopListening()
    {
        if (!Active) return;
        Active = false;
        _mParseTask?.Wait();
        _mListener.Close();
    }

    public override FilterInput Parse(byte[] msg)
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
    private CancellationTokenSource _cts = new();
    private bool _mActive;
    public bool Active { get => _mActive; private set => _mActive = value; }
    private Task? _mParseTask;

    #endregion
    #region Disposable

    public override void Dispose()
    {
        StopListening();
    }
    
    #endregion
}

public class LivoxHAP : HalfLidar
{
    private enum LivoxHapWorkState : byte
    {
        Sampling = 0x01,
        Idle = 0x02,
        Sleep = 0x03,
        Error = 0x04,
        SelfCheck = 0x05,
        MotorStartup = 0x06,
        MotorStop = 0x07,
        Upgrade = 0x08
    }

    private const int HapCommandPort = 56000;
    private const int HapPointCloudSourcePort = 57000;
    private const int HapHeaderSize = 36;
    private const ushort ParameterConfigurationCommand = 0x0100;
    private const ushort ParameterInquiryCommand = 0x0101;
    private const ushort PointCloudHostIpConfigKey = 0x0006;
    private const ushort WorkTargetModeKey = 0x001A;
    private const ushort CurrentWorkStateKey = 0x8006;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMilliseconds(1000);

    private readonly IPAddress _mListenAddress;
    private readonly IPAddress _mControlAddress;
    private readonly int _mDataPort;
    private readonly int _mCommandPort;
    private readonly IPEndPoint _mCommandEndPoint;
    private readonly IPEndPoint _mListenEndPoint;
    private UdpClient _mListener;
    private CancellationTokenSource _cts = new();
    private Task? _mParseTask;
    private int _mCommandSequence;

    private bool Active { get; set; } = false;
    private LivoxHapWorkState LastRequestedState { get; set; } = LivoxHapWorkState.Idle;
    private LivoxHapWorkState? CurrentWorkState { get; set; }

    public LivoxHAP(string myAdapterIp, string lidarIp, int pointCloudDestPort = HapPointCloudSourcePort, int commandPort = HapCommandPort)
    {
        _mDataPort = pointCloudDestPort;
        _mCommandPort = commandPort;
        _mListenAddress = IPAddress.Parse(myAdapterIp);
        _mControlAddress = IPAddress.Parse(lidarIp);
        _mCommandEndPoint = new IPEndPoint(_mControlAddress, _mCommandPort);
        _mListenEndPoint = new IPEndPoint(_mListenAddress, _mDataPort);
        _mGrids = new List<GridPt>();

        MoveToIdle();
        
        StartListening();
    }

    private void Listen(CancellationToken token)
    {
        Running = true;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var tmp = _mListener.ReceiveAsync(token);
                    var msg = tmp.Result.Buffer;
                    var grid = Parse(msg);
                    if (!(_mFilterQueue == null || _mFilterQueue.IsEmpty))
                        foreach (var filter in _mFilterQueue)
                            grid = filter.Filter(grid);

                    lock (_gridLock)
                    {
                        _mGrids.AddRange(grid);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        finally
        {
            Running = false;
        }
    }

    public override void StartListening()
    {
        if (Active) return;
        
        ConfigurePointCloudDestination();
        MoveToSampling();
        
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _mListener = new UdpClient(_mListenEndPoint);
        Console.WriteLine(_mListener.Client.LocalEndPoint);
        Active = true;
        _mParseTask = new Task(() => Listen(_cts.Token));
        _mParseTask.Start();
    }

    public override void StopListening()
    {
        if (!Active) return;

        Active = false;
        MoveToIdle();
        _cts.Cancel();
        _mListener?.Close();
        _mParseTask?.Wait();
        _mListener?.Dispose();
        _mListener = null;
    }

    private bool MoveToSampling()
    {
        return TransitionTo(LivoxHapWorkState.Sampling, TimeSpan.FromSeconds(12));
    }

    private bool MoveToIdle()
    {
        return TransitionTo(LivoxHapWorkState.Idle, TimeSpan.FromSeconds(12));
    }

    private bool TransitionTo(LivoxHapWorkState targetState, TimeSpan timeout)
    {
        if (!SetWorkTargetMode(targetState)) return false;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var currentState = QueryWorkState();
            if (currentState == targetState) return true;
            if (currentState == LivoxHapWorkState.Error) return false;
            Thread.Sleep(200);
        }

        return false;
    }

    private LivoxHapWorkState? QueryWorkState()
    {
        try
        {
            var data = BuildParameterInquiryData(CurrentWorkStateKey);
            var response = SendCommand(ParameterInquiryCommand, data);
            if (response is null || response.Length < 32 || response[24] != 0) return CurrentWorkState;

            var keyCount = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(25, 2));
            var offset = 27;
            for (var i = 0; i < keyCount && offset + 4 <= response.Length; i++)
            {
                var key = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset, 2));
                var length = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset + 2, 2));
                offset += 4;
                if (offset + length > response.Length) break;

                if (key == CurrentWorkStateKey && length == 1)
                {
                    CurrentWorkState = (LivoxHapWorkState)response[offset];
                    return CurrentWorkState;
                }

                offset += length;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return CurrentWorkState;
    }

    public override FilterInput Parse(byte[] msg)
    {
        if (msg.Length < HapHeaderSize) return new();

        var packetLength = BinaryPrimitives.ReadUInt16LittleEndian(msg.AsSpan(1, 2));
        if (packetLength > msg.Length) return new();

        var dotNum = BinaryPrimitives.ReadUInt16LittleEndian(msg.AsSpan(5, 2));
        var dataType = msg[10];
        var payload = msg.AsSpan(HapHeaderSize);

        return dataType switch
        {
            1 => ParseCartesian32(payload, dotNum),
            2 => ParseCartesian16(payload, dotNum),
            _ => new FilterInput()
        };
    }

    public override void Dispose()
    {
        StopListening();
        _cts.Dispose();
    }

    private FilterInput ParseCartesian32(ReadOnlySpan<byte> payload, int dotNum)
    {
        const int pointSize = 14;
        var output = new FilterInput();
        var count = Math.Min(dotNum, payload.Length / pointSize);
        for (var i = 0; i < count; i++)
        {
            var point = payload.Slice(i * pointSize, pointSize);
            var x = BinaryPrimitives.ReadInt32LittleEndian(point) / 1000.0f;
            var y = BinaryPrimitives.ReadInt32LittleEndian(point.Slice(4, 4)) / 1000.0f;
            var z = BinaryPrimitives.ReadInt32LittleEndian(point.Slice(8, 4)) / 1000.0f;
            if (x == 0 && y == 0 && z == 0) continue;
            output.Add(new GridPt([x, y, z], [0, 0]));
        }

        return output;
    }

    private FilterInput ParseCartesian16(ReadOnlySpan<byte> payload, int dotNum)
    {
        const int pointSize = 8;
        var output = new FilterInput();
        var count = Math.Min(dotNum, payload.Length / pointSize);
        for (var i = 0; i < count; i++)
        {
            var point = payload.Slice(i * pointSize, pointSize);
            var x = BinaryPrimitives.ReadInt16LittleEndian(point) / 100.0f;
            var y = BinaryPrimitives.ReadInt16LittleEndian(point.Slice(2, 2)) / 100.0f;
            var z = BinaryPrimitives.ReadInt16LittleEndian(point.Slice(4, 2)) / 100.0f;
            if (x == 0 && y == 0 && z == 0) continue;
            output.Add(new GridPt([x, y, z], [0, 0]));
        }

        return output;
    }

    private void ConfigurePointCloudDestination()
    {
        var hostAddress = _mListenAddress;

        var addressBytes = hostAddress.GetAddressBytes();
        if (addressBytes.Length != 4) return;

        var value = new byte[8];
        addressBytes.CopyTo(value, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(value.AsSpan(4, 2), (ushort)_mDataPort);
        BinaryPrimitives.WriteUInt16LittleEndian(value.AsSpan(6, 2), HapPointCloudSourcePort);
        SetParameter(PointCloudHostIpConfigKey, value);
    }

    private IPAddress? GetLocalAddressForLidar()
    {
        try
        {
            using var client = new UdpClient(AddressFamily.InterNetwork);
            client.Connect(_mCommandEndPoint);
            return ((IPEndPoint?)client.Client.LocalEndPoint)?.Address;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    private bool SetWorkTargetMode(LivoxHapWorkState state)
    {
        var success = SetParameter(WorkTargetModeKey, [(byte)state]);
        if (success) LastRequestedState = state;
        return success;
    }

    private bool SetParameter(ushort key, byte[] value)
    {
        try
        {
            var data = BuildParameterConfigurationData(key, value);
            var response = SendCommand(ParameterConfigurationCommand, data);
            return response is { Length: >= 25 } && response[24] == 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    private byte[]? SendCommand(ushort commandId, byte[] data)
    {
        var sequence = NextCommandSequence();
        var frame = BuildCommandFrame(commandId, sequence, data);

        using var client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        client.Connect(_mCommandEndPoint);
        client.Send(frame, frame.Length);

        using var timeout = new CancellationTokenSource(CommandTimeout);
        while (!timeout.IsCancellationRequested)
        {
            UdpReceiveResult response;
            try
            {
                response = client.ReceiveAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return null;
            }

            if (IsMatchingAck(response.Buffer, commandId, sequence))
                return response.Buffer;
        }

        return null;
    }

    private int NextCommandSequence()
    {
        return Interlocked.Increment(ref _mCommandSequence) & 0xFFFF;
    }

    private static byte[] BuildParameterConfigurationData(ushort key, byte[] value)
    {
        var data = new byte[8 + value.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), key);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), (ushort)value.Length);
        value.CopyTo(data.AsSpan(8));
        return data;
    }

    private static byte[] BuildParameterInquiryData(ushort key)
    {
        var data = new byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), key);
        return data;
    }

    private static byte[] BuildCommandFrame(ushort commandId, int sequence, byte[] data)
    {
        var frame = new byte[24 + data.Length];
        frame[0] = 0xAA;
        frame[1] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2, 2), (ushort)frame.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4, 4), (uint)sequence);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8, 2), commandId);
        frame[10] = 0;
        frame[11] = 0;
        // 6 bytes from id 12 are reserved: defaulting to zero
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(12, 4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(16, 2), 0);
        //
        
        var crc16 = ComputeCrc16CcittFalse(frame.AsSpan(0, 18));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(18, 2), crc16);
        var crc32 = ComputeCrc32(data);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(20, 4), crc32);
        data.CopyTo(frame.AsSpan(24));
        return frame;
    }

    private static bool IsMatchingAck(byte[] frame, ushort commandId, int sequence)
    {
        if (frame.Length < 24 || frame[0] != 0xAA) return false;
        var length = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2, 2));
        if (length > frame.Length) return false;
        var responseSequence = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4, 4));
        var responseCommand = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(8, 2));
        return responseSequence == (uint)sequence && responseCommand == commandId && frame[10] == 1;
    }

    private static ushort ComputeCrc16CcittFalse(ReadOnlySpan<byte> data)
    {
        const ushort polynomial = 0x1021;
        ushort crc = 0xFFFF;

        foreach (var value in data)
        {
            crc ^= (ushort)(value << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ polynomial) : (ushort)(crc << 1);
        }

        return crc;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        const uint polynomial = 0xEDB88320;
        uint crc = 0xFFFFFFFF;

        foreach (var value in data)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
        }

        return crc ^ 0xFFFFFFFF;
    }
}

public class Mock : HalfLidar
{
    private CancellationTokenSource _cts = new();
    public bool Active { get; private set; } = false;
    private Task _mWorker;
    private Random rnd = new();
    private FilterInput _base;

    public Mock(string ip, int port = 56301)
    {
        _base = new();
        _mGrids = new();
        int max = rnd.Next(100, 200);
        for (int i = 0; i < max; ++i)
        {
            float[] tmp =
            [
                (float)(rnd.NextDouble() - 0.5) * 40,
                (float)(rnd.NextDouble() - 0.5) * 40,
                (float)(rnd.NextDouble() - 0.2) * 25
            ];
            _base.Add(new GridPt(tmp,[0,0]));
        }
        StartListening();
    }

    private void Propagate(CancellationToken ct)
    {
        Running = true;
        while (!ct.IsCancellationRequested)
        {
            foreach (var point in _base)
            {
                point.pt[0] += ((float)rnd.NextDouble() - 0.5f) / 2;
                point.pt[1] += ((float)rnd.NextDouble() - 0.5f) / 2;
                point.pt[2] += ((float)rnd.NextDouble() - 0.5f) / 2;
            }
            var grid = new FilterInput(_base);
            if(!(_mFilterQueue == null || _mFilterQueue.IsEmpty))
                foreach(var filter in _mFilterQueue)
                    grid =  filter.Filter(grid);
            lock (_gridLock) _mGrids.AddRange(grid);
            Thread.Sleep(10);
        }
        Running = false;
    }
    
    public override void StartListening()
    {
        if(Active) return;
        Active = true;
        _mWorker = new Task(() => {Propagate(_cts.Token);});
        _mWorker.Start();
    }

    public override void StopListening()
    {
        if (!Active) return;
        Active = false;
        _cts.Cancel();
        _mWorker.Wait();
    }

    public override FilterInput Parse(byte[] msg)
    {
        return new();
    }

    public override void Dispose()
    {
        StopListening();
    }
}
