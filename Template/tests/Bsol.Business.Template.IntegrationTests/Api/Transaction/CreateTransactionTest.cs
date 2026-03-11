using System.Net;
using System.Text;
using System.Text.Json;
using Bsol.Business.Template.Api.Endpoints.Transaction;
using Bsol.Business.Template.IntegrationTests.Utils;
using Shouldly;
using Xunit;

namespace Bsol.Business.Template.IntegrationTests.Api.Transaction;

public class CreateTransactionTest : BaseEfRepositoryTest, IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CreateTransactionTest(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public static IEnumerable<object[]> GetTransactionRequests()
    {
        yield return new object[]
        {
            new CreateTransactionRequest { SourceAccountNumber = "ACC-001", DestinationAccountNumber = "ACC-002", Amount = 100 },
            HttpStatusCode.OK
        };
        yield return new object[]
        {
            new CreateTransactionRequest { SourceAccountNumber = "ACC-001", DestinationAccountNumber = "ACC-002", Amount = 99999 },
            HttpStatusCode.BadRequest  // prueba fondos insuficientes
        };
        yield return new object[]
        {
            new CreateTransactionRequest { SourceAccountNumber = "ACC-001", DestinationAccountNumber = "ACC-002", Amount = -1 },
            HttpStatusCode.BadRequest  // prueba monto inválido
        };
        yield return new object[]
        {
            new CreateTransactionRequest { SourceAccountNumber = "NO-EXISTE", DestinationAccountNumber = "ACC-002", Amount = 100 },
            HttpStatusCode.NotFound  // prueba cuenta origen no existe
        };
    }

    [Theory]
    [MemberData(nameof(GetTransactionRequests))]
    public async Task CreateTransaction_ShouldReturnExpectedStatusCode(CreateTransactionRequest request, HttpStatusCode expectedStatus)
    {
        var client = _factory.CreateClientWithMocks(services =>
        {
            _dbContext.Accounts.AddRange(
                new Infrastructure.Data.Account { Id = Guid.NewGuid(), AccountNumber = "ACC-001", Balance = 500, UpdatedAt = DateTime.UtcNow },
                new Infrastructure.Data.Account { Id = Guid.NewGuid(), AccountNumber = "ACC-002", Balance = 200, UpdatedAt = DateTime.UtcNow }
            );
            _dbContext.SaveChanges();
        });

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("Bsol/v1/transactions", content);

        response.StatusCode.ShouldBe(expectedStatus);
    }
}
