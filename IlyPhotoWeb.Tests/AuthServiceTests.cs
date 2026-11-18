using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IlyPhotoWeb.Tests;

public class AuthServiceTests
{
    private readonly Mock<UserStore> _userStoreMock;
    private readonly Mock<CounterStore> _counterStoreMock;
    private readonly IOptions<AdminOptions> _adminOptions;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userStoreMock = new Mock<UserStore>();
        _counterStoreMock = new Mock<CounterStore>();

        _adminOptions = Options.Create(new AdminOptions
        {
            Emails = new List<string> { "admin@ilyphoto.co.za", "ayshaallie23@gmail.com" }
        });

        _authService = new AuthService(
            _userStoreMock.Object,
            _counterStoreMock.Object,
            _adminOptions,
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_WithValidDetails_CreatesUserAndReturnsOk()
    {
        // Arrange
        _userStoreMock.Setup(s => s.GetByEmailAsync("newclient@example.com"))
            .ReturnsAsync((UserProfile?)null);
        _counterStoreMock.Setup(c => c.NextAsync("clients", 1000))
            .ReturnsAsync(1025);

        UserProfile? savedProfile = null;
        _userStoreMock.Setup(s => s.SaveAsync(It.IsAny<UserProfile>()))
            .Callback<UserProfile>(p => savedProfile = p)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.RegisterAsync("Jane Smith", "newclient@example.com", "0821234567", "SecurePass123!");

        // Assert
        Assert.True(result.Ok);
        Assert.NotNull(result.Profile);
        Assert.Equal("newclient@example.com", result.Profile.Email);
        Assert.Equal("Jane Smith", result.Profile.FullName);
        Assert.Equal("CL-1025", result.Profile.ClientId);
        Assert.Equal(Roles.Client, result.Profile.Role);
        Assert.True(BCrypt.Net.BCrypt.Verify("SecurePass123!", result.Profile.PasswordHash));
        _userStoreMock.Verify(s => s.SaveAsync(It.IsAny<UserProfile>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsFailureError()
    {
        // Arrange
        var existing = new UserProfile { Email = "existing@example.com", FullName = "Existing User" };
        _userStoreMock.Setup(s => s.GetByEmailAsync("existing@example.com"))
            .ReturnsAsync(existing);

        // Act
        var result = await _authService.RegisterAsync("Another", "existing@example.com", "082", "Pass1234");

        // Assert
        Assert.False(result.Ok);
        Assert.Contains("already exists", result.Error);
        _userStoreMock.Verify(s => s.SaveAsync(It.IsAny<UserProfile>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithShortPassword_ReturnsFailure()
    {
        // Act
        var result = await _authService.RegisterAsync("John", "john@example.com", "082", "123");

        // Assert
        Assert.False(result.Ok);
        Assert.Contains("at least 8 characters", result.Error);
    }

    [Fact]
    public async Task RegisterAsync_WithoutSpecialCharacter_ReturnsComplexityFailure()
    {
        // Act
        var result = await _authService.RegisterAsync("John", "john@example.com", "082", "Password123");

        // Assert
        Assert.False(result.Ok);
        Assert.Contains("special character", result.Error);
    }

    [Fact]
    public async Task RegisterAsync_WithAdminEmail_RejectsRegistration()
    {
        // Act
        var result = await _authService.RegisterAsync("Admin Guy", "admin@ilyphoto.co.za", "082", "Password123!");

        // Assert
        Assert.False(result.Ok);
        Assert.Contains("reserved for system administration", result.Error);
    }

    [Fact]
    public async Task SignInAsync_WithValidPassword_ReturnsSuccess()
    {
        // Arrange
        var hashed = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!");
        var user = new UserProfile
        {
            Id = "user-1",
            Email = "client@example.com",
            FullName = "Verified Client",
            PasswordHash = hashed,
            Role = Roles.Client
        };
        _userStoreMock.Setup(s => s.GetByEmailAsync("client@example.com")).ReturnsAsync(user);

        // Act
        var result = await _authService.SignInAsync("client@example.com", "CorrectPassword1!");

        // Assert
        Assert.True(result.Ok);
        Assert.NotNull(result.Profile);
        Assert.Equal("Verified Client", result.Profile.FullName);
    }

    [Fact]
    public async Task SignInAsync_WithWrongPassword_FailsWithoutBypass()
    {
        // Arrange
        var hashed = BCrypt.Net.BCrypt.HashPassword("ActualPassword99!");
        var user = new UserProfile
        {
            Id = "user-2",
            Email = "client2@example.com",
            PasswordHash = hashed
        };
        _userStoreMock.Setup(s => s.GetByEmailAsync("client2@example.com")).ReturnsAsync(user);

        // Act
        var result = await _authService.SignInAsync("client2@example.com", "WrongPassword123");

        // Assert
        Assert.False(result.Ok);
        Assert.Contains("Incorrect email or password", result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithNonExistentEmail_ReturnsAccurateFailure()
    {
        // Rubric Criterion 2: Password recovery must accurately report failure if account not found
        _userStoreMock.Setup(s => s.GetByEmailAsync("ghost@example.com")).ReturnsAsync((UserProfile?)null);

        // Act
        var result = await _authService.ResetPasswordAsync("ghost@example.com", "BrandNewPass123!");

        // Assert
        Assert.False(result.Ok);
        Assert.Equal("No registered account was found with that email address.", result.Error);
        _userStoreMock.Verify(s => s.SaveAsync(It.IsAny<UserProfile>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithShortPassword_ReturnsAccurateFailure()
    {
        // Act
        var result = await _authService.ResetPasswordAsync("user@example.com", "abc");

        // Assert
        Assert.False(result.Ok);
        Assert.Contains("at least 8 characters", result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidUser_UpdatesPasswordHashAndSucceeds()
    {
        // Arrange
        var user = new UserProfile
        {
            Id = "user-real-1",
            Email = "realuser@example.com",
            FullName = "Real Client",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!")
        };
        _userStoreMock.Setup(s => s.GetByEmailAsync("realuser@example.com")).ReturnsAsync(user);

        UserProfile? updated = null;
        _userStoreMock.Setup(s => s.SaveAsync(It.IsAny<UserProfile>()))
            .Callback<UserProfile>(p => updated = p)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.ResetPasswordAsync("realuser@example.com", "BrandNewSecretPassword123!");

        // Assert
        Assert.True(result.Ok);
        Assert.NotNull(updated);
        Assert.True(BCrypt.Net.BCrypt.Verify("BrandNewSecretPassword123!", updated.PasswordHash));
        _userStoreMock.Verify(s => s.SaveAsync(It.IsAny<UserProfile>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_NeverReusesExistingClientId()
    {
        // Arrange: existing clients already hold CL-1001 and CL-1030
        _userStoreMock.Setup(s => s.GetByEmailAsync("brandnew@example.com"))
            .ReturnsAsync((UserProfile?)null);
        _userStoreMock.Setup(s => s.ListClientsAsync())
            .ReturnsAsync(new List<UserProfile>
            {
                new UserProfile { Id = "u1", ClientId = "CL-1001", Email = "aaliyah@example.com", Role = Roles.Client },
                new UserProfile { Id = "u2", ClientId = "CL-1030", Email = "other@example.com", Role = Roles.Client }
            });
        _counterStoreMock.Setup(c => c.NextAsync("clients", 1030))
            .ReturnsAsync(1031);
        _userStoreMock.Setup(s => s.SaveAsync(It.IsAny<UserProfile>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.RegisterAsync("Emily Rain", "brandnew@example.com", "0821234567", "SecurePass123!");

        // Assert
        Assert.True(result.Ok);
        Assert.NotNull(result.Profile);
        Assert.Equal("CL-1031", result.Profile.ClientId);
        Assert.NotEqual("CL-1001", result.Profile.ClientId);
    }

    [Fact]
    public async Task SignInAsync_DoesNotAllowDefaultPasswordBypassOnExistingHash()
    {
        var user = new UserProfile
        {
            Id = "user-real-2",
            Email = "secure@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("MyRealSecret99!"),
            Role = Roles.Client
        };
        _userStoreMock.Setup(s => s.GetByEmailAsync("secure@example.com")).ReturnsAsync(user);

        var result = await _authService.SignInAsync("secure@example.com", "Password123!");

        Assert.False(result.Ok);
    }
}
