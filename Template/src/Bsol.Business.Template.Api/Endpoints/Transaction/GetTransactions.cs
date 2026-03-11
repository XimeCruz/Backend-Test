using Bsol.Business.Template.Infrastructure.Data;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Bsol.Business.Template.Api.Endpoints.Transaction;

public class GetTransactions(AppDbContext db) : EndpointWithoutRequest<Ok<object>>
{
    public override void Configure()
    {
        Version(1);
        Get("/transactions");
        AllowAnonymous();
    }

    public override async Task<Ok<object>> ExecuteAsync(CancellationToken ct)
    {
        var transactions = await db.Transactions
            .Select(t => new
            {
                t.Id,
                t.SourceAccountId,
                t.DestinationAccountId,
                t.Amount,
                t.Timestamp,
                t.VoucherCode
            })
            .ToListAsync(ct);

        return TypedResults.Ok<object>(transactions);
    }
}
