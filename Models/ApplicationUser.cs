using Microsoft.AspNetCore.Identity;
using QuizApi.Models.EnumModel;

namespace QuizApi.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public SubscriptionType SubscriptionType { get; set; } = SubscriptionType.Free;
        public DateTime? SubscriptionExpiry { get; set; }
        public DateTime? SubscriptionStartDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public int LoginAttempts { get; set; } = 0;
        public bool IsLocked { get; set; } = false;
    }
}