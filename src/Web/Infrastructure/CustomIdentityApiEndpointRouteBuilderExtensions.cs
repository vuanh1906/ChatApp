using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using ChatApp.Application.DTOs;
using ChatApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChatApp.Web.Infrastructure;

public static class CustomIdentityApiEndpointRouteBuilderExtensions
{
    private static readonly EmailAddressAttribute _emailAddressAttribute = new();
    public static IEndpointConventionBuilder MapCustomIdentityApi<TUser>(this IEndpointRouteBuilder endpoints)
        where TUser : ApplicationUser, new()
    {
        var routeGroup = endpoints.MapGroup("");
        var bearerTokenOptions = endpoints.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>();
        var timeProvider = endpoints.ServiceProvider.GetRequiredService<TimeProvider>();

        routeGroup.MapPost("/register", async Task<Results<Ok, ValidationProblem>>
        ([FromBody] RegisterModel registration, HttpContext context, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();

            if (!userManager.SupportsUserEmail)
            {
                throw new NotSupportedException($"{nameof(MapCustomIdentityApi)} requires a user store with email support.");
            }

            var userStore = sp.GetRequiredService<IUserStore<TUser>>();
            var emailStore = (IUserEmailStore<TUser>)userStore;
            var email = registration.Email;

            if (string.IsNullOrEmpty(email) || !_emailAddressAttribute.IsValid(email))
            {
                return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(email)));
            }

            var user = new TUser
            {
                UserName = registration.Email,
                Email = registration.Email,
                DateOfBirth = registration.DateOfBirth,
                City = registration.City,
                Country = registration.Country,
                Gender = registration.Gender,
                EmailConfirmed = true,
                ImagePath = registration.ImagePath
            };

            await userStore.SetUserNameAsync(user, email, CancellationToken.None);
            await emailStore.SetEmailAsync(user, email, CancellationToken.None);
            var result = await userManager.CreateAsync(user, registration.Password);

            if (!result.Succeeded)
            {
                return CreateValidationProblem(result);
            }

            return TypedResults.Ok();
        });

        routeGroup.MapPost("/login", async Task<Results<Ok<AccessTokenResponse>, EmptyHttpResult, ProblemHttpResult>>
           ([FromBody] LoginRequest login, [FromQuery] bool? useCookies, [FromQuery] bool? useSessionCookies, [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();

            var useCookieScheme = (useCookies == true) || (useSessionCookies == true);
            var isPersistent = (useCookies == true) && (useSessionCookies != true);
            signInManager.AuthenticationScheme = useCookieScheme ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;

            var result = await signInManager.PasswordSignInAsync(login.Email, login.Password, isPersistent, lockoutOnFailure: true);

            if (result.RequiresTwoFactor)
            {
                if (!string.IsNullOrEmpty(login.TwoFactorCode))
                {
                    result = await signInManager.TwoFactorAuthenticatorSignInAsync(login.TwoFactorCode, isPersistent, rememberClient: isPersistent);
                }
                else if (!string.IsNullOrEmpty(login.TwoFactorRecoveryCode))
                {
                    result = await signInManager.TwoFactorRecoveryCodeSignInAsync(login.TwoFactorRecoveryCode);
                }
            }

            if (!result.Succeeded)
            {
                return TypedResults.Problem(result.ToString(), statusCode: StatusCodes.Status401Unauthorized);
            }

            return TypedResults.Empty;
        });

        routeGroup.MapPost("/refresh", async Task<Results<Ok<AccessTokenResponse>, UnauthorizedHttpResult, SignInHttpResult, ChallengeHttpResult>>
           ([FromBody] RefreshRequest refreshRequest, [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();
            var refreshTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
            var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

            if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
                timeProvider.GetUtcNow() >= expiresUtc ||
                await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not TUser user)

            {
                return TypedResults.Challenge();
            }

            var newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
            return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
        });

        return routeGroup;
    }



    private static ValidationProblem CreateValidationProblem(IdentityResult result)
    {
        Debug.Assert(!result.Succeeded);
        var errorDictionary = new Dictionary<string, string[]>(1);

        foreach (var error in result.Errors)
        {
            string[] newDescriptions;

            if (errorDictionary.TryGetValue(error.Code, out var descriptions))
            {
                newDescriptions = new string[descriptions.Length + 1];
                Array.Copy(descriptions, newDescriptions, descriptions.Length);
                newDescriptions[descriptions.Length] = error.Description;
            }
            else
            {
                newDescriptions = [error.Description];
            }

            errorDictionary[error.Code] = newDescriptions;
        }

        return TypedResults.ValidationProblem(errorDictionary);
    }
}

