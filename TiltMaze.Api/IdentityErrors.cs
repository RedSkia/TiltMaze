using Microsoft.AspNetCore.Identity;

namespace TiltMaze.Api;

// Identity's fejlbeskeder på vores engelsk. Her er dem, vi kan komme ud for
public class IdentityErrors : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"The password must be at least {length} characters" };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "That username is taken" };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "The username must be 3-16 letters, digits or underscores" };

    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Something went wrong. Try again" };
}
