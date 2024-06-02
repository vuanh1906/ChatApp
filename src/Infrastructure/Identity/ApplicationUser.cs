using ChatApp.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace ChatApp.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public DateTime DateOfBirth { get; set; }
    public string? Name { get; set; }
    public eGender Gender { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? ImagePath { get; set; }
   
}
