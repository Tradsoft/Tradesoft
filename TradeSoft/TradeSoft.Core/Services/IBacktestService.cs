using System;
using System.Threading;
using System.Threading.Tasks;
using TradeSoft.Core.DTO;

namespace TradeSoft.Core.Services
{
    public interface IBacktestService
    {
        Task<BacktestResult> RunBacktestAsync(
            BacktestConfig config,
            IProgress<float>? progress = null,
            CancellationToken cancellationToken = default
        );
    }
}