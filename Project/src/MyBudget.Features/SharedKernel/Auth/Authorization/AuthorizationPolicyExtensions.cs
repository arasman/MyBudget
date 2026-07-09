using Microsoft.AspNetCore.Authorization;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Auth.Authorization;

/// <summary>Named budget authorization policies.</summary>
public static class AuthorizationPolicyExtensions
{
    public static void AddBudgetPolicies(this AuthorizationOptions opts)
    {
        opts.AddPolicy("budget:read", policy =>
            policy.Requirements.Add(new BudgetRequirement(BudgetRole.ReadOnly)));

        opts.AddPolicy("budget:operator", policy =>
            policy.Requirements.Add(new BudgetRequirement(BudgetRole.Operator)));

        opts.AddPolicy("budget:admin", policy =>
            policy.Requirements.Add(new BudgetRequirement(BudgetRole.Admin)));
    }
}
