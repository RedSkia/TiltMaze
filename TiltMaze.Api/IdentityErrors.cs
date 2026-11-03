using Microsoft.AspNetCore.Identity;

namespace TiltMaze.Api;

// Identity's egne fejlbeskeder taler om "username", men hos os er det e-mailen. Her er de vi kan komme ud for
public class IdentityErrors : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"The password must be at least {length} characters" };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "There is already an account with that email" };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "There is already an account with that email" };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "The email address is invalid" };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "The email address is invalid" };

    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Something went wrong. Try again" };
}
