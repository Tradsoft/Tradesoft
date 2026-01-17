using System;

namespace TradeSoft.Core.Domain
{
	public class Bar
	{
		public DateTime Timestamp { get; set; }
		public double Open { get; set; }
		public double High { get; set; }
		public double Low { get; set; }
		public double Close { get; set; }
		public long Volume { get; set; }

		public override string ToString()
			=> $"[BAR] {Timestamp:yyyy-MM-dd HH:mm} | O: {Open:F5} | H: {High:F5} | L: {Low:F5} | C: {Close:F5}";
	}
}