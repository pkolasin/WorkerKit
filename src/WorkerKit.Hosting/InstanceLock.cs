
using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting;

public sealed class InstanceLock : IDisposable
{
    private readonly FileStream _stream;
    private readonly string _pidPath;

    private InstanceLock(FileStream stream, string pidPath)
    {
        _stream = stream;
        _pidPath = pidPath;
    }

    public static InstanceLock Acquire(IInstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var path = Path.Combine(instance.InstanceDirectory, ".lock");
        var pidPath = Path.Combine(instance.InstanceDirectory, "instance.pid");
        FileStream stream;
        try
        {
            // FileShare.None is an exclusive handle on Windows. On Linux, .NET emulates it with flock(LOCK_EX).
            // Any other share mode becomes a shared flock, which does not block a second process.
            stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException($"Instance '{instance.InstanceName}' is already running ({path}).", exception);
        }

        try
        {
            // The PID lives in a separate file. The lock file itself must stay exclusively locked.
            // Dispose removes the PID when the lock file is deleted, so a clean stop does not look like a running instance.
            File.WriteAllText(pidPath, Environment.ProcessId.ToString());
            return new InstanceLock(stream, pidPath);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        // Delete our pid before the lock file goes away. A process that acquires the lock
        // afterwards writes its own pid, and this dispose must not remove that file.
        try
        {
            DeleteOwnPid();
        }
        finally
        {
            _stream.Dispose();
        }
    }

    private void DeleteOwnPid()
    {
        if (!File.Exists(_pidPath))
        {
            return;
        }

        var text = File.ReadAllText(_pidPath).Trim();
        if (text == Environment.ProcessId.ToString())
        {
            File.Delete(_pidPath);
        }
    }
}
