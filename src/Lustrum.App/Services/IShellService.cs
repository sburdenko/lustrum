namespace Lustrum.App.Services;

public interface IShellService
{
    bool OpenFolder(string path);

    bool RevealFile(string path);

    bool OpenInEditor(string path, string command);
}
