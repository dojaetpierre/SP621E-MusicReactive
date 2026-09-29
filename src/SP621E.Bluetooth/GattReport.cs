namespace SP621E.Bluetooth;

/// <summary>
/// Human-readable record of one BLE connection's GATT topology.
/// This is the artifact a human operator pastes back to verify protocol work.
/// </summary>
public sealed class GattReport
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines => _lines;

    public void AddLine(string line) => _lines.Add(line);

    public void AddSplitLine() => _lines.Add(new string('-', 72));

    public override string ToString()
    {
        var header = new List<string>
        {
            "SP621E-MusicReactive — BLE GATT diagnostic report",
            $"Generated: {DateTimeOffset.Now:O}",
            $"Machine  : {Environment.MachineName} / {Environment.OSVersion.VersionString}",
        };
        header.AddRange(_lines);
        return string.Join(Environment.NewLine, header) + Environment.NewLine;
    }
}