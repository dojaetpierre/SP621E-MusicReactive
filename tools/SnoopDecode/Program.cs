using SP621E.Bluetooth.Snoop;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine(
        """
        SnoopDecode — extract GATT ATT traffic from an Android Bluetooth HCI snoop log.

        Usage:
          SnoopDecode <btsnoop-file> [--debug]

        <btsnoop-file> is the file produced by Android's "Bluetooth HCI snoop log"
        (btsnoop_hci.log or btsnoop_hci.cfa) while operating the BanlanX app.

        The output lists every ATT transaction with direction and hex payloads, then
        summarizes the write targets and notification sources — the evidence needed to
        confirm the SP621E protocol (Milestone 3).
        """);
    return 1;
}

var file = args[0];
var debug = args.Contains("--debug");
if (!File.Exists(file))
{
    Console.Error.WriteLine($"File not found: {file}");
    return 2;
}

try
{
    var packets = new HciSnoopReader(printDebug: debug).Read(file);

    Console.WriteLine($"Parsed {packets.Count} ATT packets from {file}");
    Console.WriteLine(new string('-', 100));
    foreach (var p in packets)
        Console.WriteLine(p);

    Console.WriteLine(new string('-', 100));

    var writes = packets.Where(p => p.Opcode is 0x12 or 0x52 or 0x14).ToList();
    var notifications = packets.Where(p => p.Opcode is 0x18 or 0x19).ToList();
    var mtuExchanges = packets.Where(p => p.Opcode is 0x02 or 0x03).ToList();

    Console.WriteLine($"Writes: {writes.Count}");
    foreach (var group in writes.GroupBy(p => p.AttributeHandle).OrderBy(g => g.Key))
    {
        Console.WriteLine($"  handle 0x{group.Key:x4}: {group.Count()} packets");
        foreach (var sample in group.Take(5))
            Console.WriteLine($"      {sample.RelativeTime.TotalSeconds,8:0.000}s -> {sample.OpcodeName} value={sample.ValueHex}");
    }

    Console.WriteLine($"Notifications/Indications: {notifications.Count}");
    foreach (var group in notifications.GroupBy(p => p.AttributeHandle).OrderBy(g => g.Key))
        Console.WriteLine($"  handle 0x{group.Key:x4}: {group.Count()} packets, last value = {group.Last().ValueHex}");

    if (mtuExchanges.Count == 0)
        Console.WriteLine("No ATT MTU exchange observed (default MTU 23 likely in use).");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed: {ex.Message}");
    if (debug)
        Console.Error.WriteLine(ex);
    return 3;
}