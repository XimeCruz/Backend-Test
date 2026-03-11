using Bsol.Business.Template.Infrastructure.Data;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Bsol.Business.Template.Api.Endpoints.Account;

public class GetAccountRequest
{
    public Guid? AccountId { get; set; }
    public string? AccountNumber { get; set; }
}

public class GetAccount(AppDbContext db) : Endpoint<GetAccountRequest, Results<Ok<object>, NotFound, BadRequest<string>>>
{
    public override void Configure()
    {
        Version(1);
        Get("/accounts");
        AllowAnonymous();
    }

    public override async Task<Results<Ok<object>, NotFound, BadRequest<string>>> ExecuteAsync(GetAccountRequest request, CancellationToken ct)
    {
        if (request.AccountId == null && string.IsNullOrEmpty(request.AccountNumber))
            return TypedResults.BadRequest("Debe proporcionar accountId o accountNumber.");

        var account = await db.Accounts
            .Where(a => a.Id == request.AccountId || a.AccountNumber == request.AccountNumber)
            .Select(a => new
            {
                a.Id,
                a.AccountNumber,
                a.Balance,
            })
            .FirstOrDefaultAsync(ct);

        if (account is null)
            return TypedResults.NotFound();

        return TypedResults.Ok<object>(account);
    }
}
