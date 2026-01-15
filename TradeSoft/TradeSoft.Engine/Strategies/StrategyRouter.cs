using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
	public sealed class StrategyRouter : Handler
	{
		private readonly IReadOnlyList<Handler> _strategies;

		public StrategyRouter(params Handler[] strategies)
		{
			if (strategies == null) throw new ArgumentNullException(nameof(strategies));
			if (strategies.Length == 0) throw new ArgumentException("Au moins une stratégie doit être fournie.", nameof(strategies));

			_strategies = strategies;
		}

		protected override void OnProcess(TickContext context)
		{
			for (int i = 0; i < _strategies.Count; i++)
			{
				_strategies[i].Execute(context);

				if (context.IgnoreTick)
				{
					return;
				}
			}
		}
	}
}