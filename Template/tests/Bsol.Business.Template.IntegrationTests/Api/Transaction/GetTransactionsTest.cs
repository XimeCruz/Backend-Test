using System.Net;
using Bsol.Business.Template.IntegrationTests.Utils;
using Shouldly;
using Xunit;

namespace Bsol.Business.Template.IntegrationTests.Api.Transaction;

public class GetTransactionsTest : BaseEfRepositoryTest, IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GetTransactionsTest(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTransactions_ShouldReturnOk_WithList()
    {
        var client = _factory.CreateClientWithMocks(services =>
        {
            var sourceId = Guid.NewGuid();
            var destId = Guid.NewGuid();

            _dbContext.Accounts.AddRange(
                new Infrastructure.Data.Account { Id = sourceId, AccountNumber = "ACC-001", Balance = 500, UpdatedAt = DateTime.UtcNow },
                new Infrastructure.Data.Account { Id = destId, AccountNumber = "ACC-002", Balance = 200, UpdatedAt = DateTime.UtcNow }
            );
            _dbContext.Transactions.Add(new Infrastructure.Data.Transaction
            {
                SourceAccountId = sourceId,
                DestinationAccountId = destId,
                Amount = 100,
                Timestamp = DateTime.UtcNow,
                VoucherCode = "VOUCHER001"
            });
            _dbContext.SaveChanges();
        });

        var response = await client.GetAsync("Bsol/v1/transactions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
