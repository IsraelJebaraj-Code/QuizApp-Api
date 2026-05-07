using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using QuizApi.Models;
using QuizApi.Models.EnumModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace QuizApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _roleManager = roleManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validate that at least email or phone is provided
            if (string.IsNullOrEmpty(model.Email) && string.IsNullOrEmpty(model.PhoneNumber))
                return BadRequest(new { Message = "Either email or phone number must be provided" });

            // Check if email already exists
            if (!string.IsNullOrEmpty(model.Email))
            {
                var existingEmail = await _userManager.FindByEmailAsync(model.Email);
                if (existingEmail != null)
                    return BadRequest(new { Message = "Email already registered" });
            }

            // Check if username already exists
            var existingUsername = await _userManager.FindByNameAsync(model.Username);
            if (existingUsername != null)
                return BadRequest(new { Message = "Username already taken" });

            // Check if phone number already exists
            if (!string.IsNullOrEmpty(model.PhoneNumber))
            {
                var existingPhone = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);
                if (existingPhone != null)
                    return BadRequest(new { Message = "Phone number already registered" });
            }

            var subscriptionStartDate = DateTime.UtcNow;
            DateTime? subscriptionExpiry = model.SubscriptionType != SubscriptionType.Free ?
                subscriptionStartDate.AddMonths(1) : (DateTime?)null;

            var user = new ApplicationUser
            {
                UserName = model.Username,
                Email = string.IsNullOrEmpty(model.Email) ? $"{model.Username}@noemail.local" : model.Email,
                PhoneNumber = model.PhoneNumber,
                FullName = model.FullName,
                SubscriptionType = model.SubscriptionType,
                SubscriptionExpiry = subscriptionExpiry,
                SubscriptionStartDate = model.SubscriptionType != SubscriptionType.Free ? subscriptionStartDate : null,
                EmailConfirmed = !string.IsNullOrEmpty(model.Email),
                PhoneNumberConfirmed = !string.IsNullOrEmpty(model.PhoneNumber)
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

            // Assign role
            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                // Delete user if role assignment fails
                await _userManager.DeleteAsync(user);
                return BadRequest(new { Errors = roleResult.Errors.Select(e => e.Description) });
            }

            return Ok(new
            {
                Message = "User registered successfully",
                User = new
                {
                    user.Id,
                    Email = string.IsNullOrEmpty(model.Email) ? null : user.Email,
                    user.UserName,
                    user.FullName,
                    user.PhoneNumber,
                    user.SubscriptionType,
                    user.SubscriptionExpiry
                }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrEmpty(model.Credential))
                return BadRequest(new { Message = "Username, email, or phone number must be provided" });

            ApplicationUser user = null;

            // Try to find user by email first
            if (model.Credential.Contains("@"))
            {
                user = await _userManager.FindByEmailAsync(model.Credential);
            }
            // Try username
            if (user == null)
            {
                user = await _userManager.FindByNameAsync(model.Credential);
            }
            // Try phone number
            if (user == null)
            {
                user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == model.Credential);
            }

            if (user == null)
                return Unauthorized(new { Message = "Invalid credentials" });

            if (!user.IsActive)
                return Unauthorized(new { Message = "Account is deactivated" });

            if (user.IsLocked)
                return Unauthorized(new { Message = "Account is locked. Please contact support" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded)
            {
                user.LoginAttempts++;
                if (user.LoginAttempts >= 5)
                {
                    user.IsLocked = true;
                }
                await _userManager.UpdateAsync(user);
                return Unauthorized(new { Message = "Invalid credentials" });
            }

            // Reset login attempts on successful login
            user.LoginAttempts = 0;
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var token = GenerateJwtToken(user);
            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                Token = token,
                User = new
                {
                    user.Id,
                    Email = string.IsNullOrEmpty(user.Email) || user.Email.EndsWith("@noemail.local") ? (string)null : user.Email,
                    user.UserName,
                    user.FullName,
                    user.PhoneNumber,
                    user.SubscriptionType,
                    user.SubscriptionExpiry,
                    Roles = roles
                }
            });
        }

        [HttpPost("upgrade-subscription")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UpgradeSubscription([FromBody] UpgradeSubscriptionModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            var oldSubscription = user.SubscriptionType;
            user.SubscriptionType = model.SubscriptionType;
            user.SubscriptionStartDate = DateTime.UtcNow;
            user.SubscriptionExpiry = DateTime.UtcNow.AddMonths(1); // 1 month validity

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

            return Ok(new
            {
                Message = "Subscription upgraded successfully",
                SubscriptionDetails = new
                {
                    OldSubscription = oldSubscription,
                    NewSubscription = user.SubscriptionType,
                    StartDate = user.SubscriptionStartDate,
                    ExpiryDate = user.SubscriptionExpiry
                }
            });
        }

        [HttpGet("profile")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                User = new
                {
                    user.Id,
                    user.Email,
                    user.UserName,
                    user.FullName,
                    user.PhoneNumber,
                    user.SubscriptionType,
                    user.SubscriptionStartDate,
                    user.SubscriptionExpiry,
                    user.IsActive,
                    user.CreatedAt,
                    user.LastLoginAt,
                    Roles = roles
                }
            });
        }

        [HttpPut("update-profile")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            // Check if new phone number is already registered
            if (!string.IsNullOrEmpty(model.PhoneNumber) && model.PhoneNumber != user.PhoneNumber)
            {
                var existingPhone = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber && u.Id != userId);
                if (existingPhone != null)
                    return BadRequest(new { Message = "Phone number already registered" });
            }

            user.FullName = model.FullName ?? user.FullName;
            user.PhoneNumber = model.PhoneNumber ?? user.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

            return Ok(new
            {
                Message = "Profile updated successfully",
                User = new
                {
                    user.Id,
                    user.Email,
                    user.UserName,
                    user.FullName,
                    user.PhoneNumber
                }
            });
        }

        [HttpPost("change-password")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

            return Ok(new { Message = "Password changed successfully" });
        }

        [HttpGet("subscription")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetSubscriptionDetails()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            var isActive = user.SubscriptionExpiry == null || user.SubscriptionExpiry > DateTime.UtcNow;
            int? daysRemaining = user.SubscriptionExpiry.HasValue 
                ? (int)(user.SubscriptionExpiry.Value - DateTime.UtcNow).TotalDays 
                : (int?)null;

            return Ok(new
            {
                SubscriptionDetails = new
                {
                    user.Id,
                    SubscriptionType = user.SubscriptionType.ToString(),
                    StartDate = user.SubscriptionStartDate,
                    ExpiryDate = user.SubscriptionExpiry,
                    IsActive = isActive,
                    Status = isActive ? "Active" : "Expired",
                    DaysRemaining = daysRemaining,
                    CreatedAt = user.CreatedAt
                }
            });
        }

        [HttpGet("subscription/status")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> CheckSubscriptionStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound(new { Message = "User not found" });

            var isExpired = user.SubscriptionExpiry.HasValue && user.SubscriptionExpiry < DateTime.UtcNow;
            var isFree = user.SubscriptionType == SubscriptionType.Free;

            return Ok(new
            {
                SubscriptionStatus = new
                {
                    user.Id,
                    CurrentSubscription = user.SubscriptionType.ToString(),
                    IsFreeUser = isFree,
                    IsExpired = isExpired,
                    CanAccessProFeatures = user.SubscriptionType == SubscriptionType.Pro || 
                                          user.SubscriptionType == SubscriptionType.ProMax,
                    CanAccessProMaxFeatures = user.SubscriptionType == SubscriptionType.ProMax,
                    ExpiryDate = user.SubscriptionExpiry
                }
            });
        }

        [HttpGet("subscription/all-users")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> GetAllUsersSubscriptions()
        {
            var users = _userManager.Users.ToList();

            var subscriptionList = users.Select(u => new
            {
                u.Id,
                Email = string.IsNullOrEmpty(u.Email) || u.Email.EndsWith("@noemail.local") ? (string)null : u.Email,
                u.UserName,
                u.FullName,
                u.PhoneNumber,
                SubscriptionType = u.SubscriptionType.ToString(),
                u.SubscriptionStartDate,
                u.SubscriptionExpiry,
                IsExpired = u.SubscriptionExpiry.HasValue && u.SubscriptionExpiry < DateTime.UtcNow,
                DaysRemaining = u.SubscriptionExpiry.HasValue 
                    ? (int?)(int)(u.SubscriptionExpiry.Value - DateTime.UtcNow).TotalDays 
                    : (int?)null,
                u.CreatedAt,
                u.LastLoginAt,
                u.IsActive
            }).ToList();

            return Ok(new
            {
                TotalUsers = subscriptionList.Count,
                SubscriptionBreakdown = new
                {
                    FreeUsers = subscriptionList.Count(s => s.SubscriptionType == "Free"),
                    ProUsers = subscriptionList.Count(s => s.SubscriptionType == "Pro"),
                    ProMaxUsers = subscriptionList.Count(s => s.SubscriptionType == "ProMax"),
                    ExpiredSubscriptions = subscriptionList.Count(s => s.IsExpired)
                },
                Users = subscriptionList
            });
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim("SubscriptionType", user.SubscriptionType.ToString())
            };

            var roles = _userManager.GetRolesAsync(user).Result;
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(double.Parse(_configuration["Jwt:ExpiryInMinutes"])),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class RegisterModel
    {
        public string FullName { get; set; }
        public string Username { get; set; }
        public string Email { get; set; } // Optional - either email or phone required
        public string Password { get; set; }
        public string PhoneNumber { get; set; } // Optional - either email or phone required
        public string Role { get; set; } = "User"; // "SuperAdmin", "Admin", "User"
        public SubscriptionType SubscriptionType { get; set; } = SubscriptionType.Free;
    }

    public class LoginModel
    {
        public string Credential { get; set; } // Can be username, email, or phone number
        public string Password { get; set; }
    }

    public class UpgradeSubscriptionModel
    {
        public SubscriptionType SubscriptionType { get; set; }
    }

    public class UpdateProfileModel
    {
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class ChangePasswordModel
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmNewPassword { get; set; }
    }
}