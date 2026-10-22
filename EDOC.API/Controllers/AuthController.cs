using EDOC.API.Data;
using EDOC.API.DTOs;
using EDOC.API.Entities;
using EDOC.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.API.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(UserManager<EdocUser> users, EdocDbContext db, TokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = new EdocUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim() };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        var roleResult = await users.AddToRoleAsync(user, "Patient");
        if (!roleResult.Succeeded) { await users.DeleteAsync(user); return Problem("Could not assign patient role."); }
        db.PatientProfiles.Add(new PatientProfile { UserId = user.Id });
        await db.SaveChangesAsync(ct);
        return Created("/api/profile/me", await BuildAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await users.CheckPasswordAsync(user, request.Password)) return Unauthorized(new { error = "Invalid email or password." });
        return Ok(await BuildAuthResponse(user));
    }
    private async Task<AuthResponse> BuildAuthResponse(EdocUser user) => new(await tokens.CreateAsync(user), user.Id, user.DisplayName, (await users.GetRolesAsync(user)).ToArray());
}
