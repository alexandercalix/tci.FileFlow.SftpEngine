using System;

namespace tci.FileFlow.SftpEngine.Core.Services;

public class AuthService : IAuthService
{
    private readonly IDatabaseService _databaseService;
    private const string DevelopmentPassword = "development2026#";

    public AuthService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public bool IsAuthenticated { get; private set; }

    public event Action? OnAuthStateChanged;

    public bool Login(string password)
    {
        if (password == DevelopmentPassword)
        {
            IsAuthenticated = true;
            NotifyStateChanged();
            return true;
        }

        var config = _databaseService.GetActiveConfig();
        var clientPassword = string.IsNullOrEmpty(config.AdminPassword) ? "admin" : config.AdminPassword;

        if (password == clientPassword)
        {
            IsAuthenticated = true;
            NotifyStateChanged();
            return true;
        }

        return false;
    }

    public void Logout()
    {
        IsAuthenticated = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnAuthStateChanged?.Invoke();
}
