using Bsol.Business.Template.Infrastructure.Data;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Bsol.Business.Template.Api.Endpoints.Transaction;

public class CreateTransactionRequest
{
    public string SourceAccountNumber { get; set; } = string.Empty;
    public string DestinationAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class CreateTransaction(AppDbContext db) : Endpoint<CreateTransactionRequest, Results<Ok<object>, BadRequest<string>, NotFound<string>>>
{
    public override void Configure()
    {
        Version(1);
        Post("/transactions");
        AllowAnonymous();
    }

    public override async Task<Results<Ok<object>, BadRequest<string>, NotFound<string>>> ExecuteAsync(CreateTransactionRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0)
            return TypedResults.BadRequest("El monto debe ser mayor a 0.");

        using var transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var source = await db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == request.SourceAccountNumber, ct);
            if (source is null)
                return TypedResults.NotFound("Cuenta origen no encontrada.");

            var destination = await db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == request.DestinationAccountNumber, ct);
            if (destination is null)
                return TypedResults.NotFound("Cuenta destino no encontrada.");

            if (source.Balance < request.Amount)
                return TypedResults.BadRequest("Fondos insuficientes.");

            source.Balance -= request.Amount;
            source.UpdatedAt = DateTime.UtcNow;

            destination.Balance += request.Amount;
            destination.UpdatedAt = DateTime.UtcNow;

            var newTransaction = new Infrastructure.Data.Transaction
            {
                SourceAccountId = source.Id,
                DestinationAccountId = destination.Id,
                Amount = request.Amount,
                Timestamp = DateTime.UtcNow,
                VoucherCode = Guid.NewGuid().ToString("N")[..12].ToUpper()
            };

            db.Transactions.Add(newTransaction);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return TypedResults.Ok<object>(new
            {
                transactionId = newTransaction.Id,
                voucherCode = newTransaction.VoucherCode
            });
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            return TypedResults.BadRequest("Error al procesar la transferencia.");
        }
    }
}
