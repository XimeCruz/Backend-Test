using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bsol.Business.Template.Infrastructure.Data;

public class Transaction
{
    public int Id { get; set; }
    public Guid SourceAccountId { get; set; }
    public Guid DestinationAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string VoucherCode { get; set; } = string.Empty;

}
