namespace FortressSouls.DwarfFortress;

using System.Net.Sockets;

public sealed class TcpDfHackPreflight(DfHackProcessAdapterOptions options) : IDfHackTcpPreflight
{
    private readonly DfHackProcessAdapterOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(_options.PreflightTimeoutMs);

        try
        {
            await tcpClient.ConnectAsync(_options.Host, _options.Port, timeoutCancellation.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
