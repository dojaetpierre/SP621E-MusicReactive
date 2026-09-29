namespace SP621E.Core.Audio.Capture;

public sealed class AudioFailureEventArgs(string message, Exception? inner = null) : EventArgs
{
    public string Message { get; } = message;
    public Exception? Inner { get; } = inner;
}