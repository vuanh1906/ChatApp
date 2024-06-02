using ChatApp.Domain.Enums;

namespace ChatApp.Application.DTOs;
public class RegisterModel
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required eGender Gender { get; set; }
    public required string City { get; set; }
    public required string Country { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? ImagePath { get; set; }
}
