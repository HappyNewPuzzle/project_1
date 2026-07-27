using System.Net;

public sealed class ConnectionAdmissionController
{
    private readonly object gate = new();
    private readonly int maxConnections;
    private readonly int maxConnectionsPerIp;
    private readonly Dictionary<IPAddress, int> connectionsByIp = new();
    private int activeConnections;

    public ConnectionAdmissionController(int maxConnections, int maxConnectionsPerIp)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConnections);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConnectionsPerIp);
        if (maxConnectionsPerIp > maxConnections)
        {
            throw new ArgumentException(
                "The per-IP connection limit cannot exceed the global connection limit.",
                nameof(maxConnectionsPerIp));
        }

        this.maxConnections = maxConnections;
        this.maxConnectionsPerIp = maxConnectionsPerIp;
    }

    public int ActiveConnections
    {
        get
        {
            lock (gate)
            {
                return activeConnections;
            }
        }
    }

    public ConnectionAdmissionResult TryAcquire(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        IPAddress normalizedAddress = address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;

        lock (gate)
        {
            if (activeConnections >= maxConnections)
            {
                return new(ConnectionAdmissionStatus.ServerFull, null);
            }

            connectionsByIp.TryGetValue(normalizedAddress, out int ipConnections);
            if (ipConnections >= maxConnectionsPerIp)
            {
                return new(ConnectionAdmissionStatus.IpLimitReached, null);
            }

            activeConnections++;
            connectionsByIp[normalizedAddress] = ipConnections + 1;
            return new(
                ConnectionAdmissionStatus.Accepted,
                new ConnectionAdmissionLease(() => Release(normalizedAddress)));
        }
    }

    private void Release(IPAddress address)
    {
        lock (gate)
        {
            if (!connectionsByIp.TryGetValue(address, out int ipConnections))
            {
                return;
            }

            activeConnections--;
            if (ipConnections == 1)
            {
                connectionsByIp.Remove(address);
            }
            else
            {
                connectionsByIp[address] = ipConnections - 1;
            }
        }
    }
}
