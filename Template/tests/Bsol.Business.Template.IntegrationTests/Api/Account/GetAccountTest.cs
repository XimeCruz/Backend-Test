using System.Net;
using Bsol.Business.Template.Api.Endpoints.Account;
using Bsol.Business.Template.IntegrationTests.Utils;
using Shouldly;
using Xunit;

namespace Bsol.Business.Template.IntegrationTests.Api.Account;

public class GetAccountTest : BaseEfRepositoryTest, IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GetAccountTest(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public static IEnumerable<object[]> GetAccountRequests()
    {
        yield return new object[]
        {
            "ACC-001", HttpStatusCode.OK
        };
        yield return new object[]
        {
            "NO-EXISTE", HttpStatusCode.NotFound
        };
        yield return new object[]
        {
            null!, HttpStatusCode.BadRequest
        };
    }

    [Theory]
    [MemberData(nameof(GetAccountRequests))]
    public async Task GetAccount_ShouldReturnExpectedStatusCode(string? accountNumber, HttpStatusCode expectedStatus)
    {
        var client = _factory.CreateClientWithMocks(services =>
        {
            _dbContext.Accounts.Add(new Infrastructure.Data.Account
            {
                Id = Guid.NewGuid(),
                AccountNumber = "ACC-001",
                Balance = 1000,
                UpdatedAt = DateTime.UtcNow
            });
            _dbContext.SaveChanges();
        });

        var url = string.IsNullOrEmpty(accountNumber)
            ? "Bsol/v1/accounts"
            : $"Bsol/v1/accounts?accountNumber={accountNumber}";

        var response = await client.GetAsync(url);

        response.StatusCode.ShouldBe(expectedStatus);
    }
}
