using Microsoft.AspNetCore.Authorization;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Auth.Authorization;

/// <summary>Authorization requirement that enforces a minimum BudgetRole for a given budget.</summary>
public sealed record BudgetRequirement(BudgetRole MinimumRole) : IAuthorizationRequirement;
