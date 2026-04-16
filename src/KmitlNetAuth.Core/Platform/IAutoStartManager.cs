namespace KmitlNetAuth.Core.Platform;

public interface IAutoStartManager
{
    bool IsEnabled { get; }
    void Enable(string executablePath);
    void Disable();
}
