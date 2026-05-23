namespace tci.FileFlow.SftpEngine.Core.Services;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    event Action? OnAuthStateChanged;
    
    bool Login(string password);
    void Logout();
}
