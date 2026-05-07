using Microsoft.EntityFrameworkCore;
using QuizApi.Models;
using QuizApi.Models.EnumModel;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace QuizApi.Data
{
    public class QuizAppDbContext : IdentityDbContext<ApplicationUser>
    {
        private QuizAppDbContext _dbContext;
        public QuizAppDbContext(DbContextOptions options) : base(options)
        {
            
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure ApplicationUser
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.ToTable("AspNetUsers");
                entity.Property(e => e.FullName).HasMaxLength(256);
                entity.Property(e => e.SubscriptionType).HasDefaultValue(SubscriptionType.Free);
            });

            // Configure a one-to-many relationship with cascade delete
            modelBuilder.Entity<Quiz>()            
                .HasMany(p=>p.Options)              
                .WithOne()                   
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Quiz>()            
                .HasOne(p=>p.QnCorrectOption)              
                .WithOne()     
                .HasForeignKey<CorrectOption>(p => p.QnId)            
                .OnDelete(DeleteBehavior.Cascade); 
            modelBuilder.Entity<Quiz>()            
                .HasOne(p=>p.QnCategory)              
                .WithOne()        
                .HasForeignKey<Category>(c => c.QnId)    
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Quiz>()
                .HasOne(p => p.AnswerKeyExplanation)
                .WithOne()
                .HasForeignKey<AnswerKeyExplanation>(e => e.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
         
        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<Quiz> Quiz { get; set;  }
        public DbSet<AnswerKeyExplanation> AnswerKeyExplanations { get; set; }
        public DbSet<Test> TestData { get; set; }
    }
}

