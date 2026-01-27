using TradeSoft.Core.Context;

namespace TradeSoft.Core.Pipeline
{
    public abstract class Handler
    {
        private Handler? _next;

        public void SetNext(Handler next)
        {
            _next = next;
        }

        public void Execute(TickContext context)
        {
            OnProcess(context);

            if (!context.IgnoreTick && _next != null)
            {
                _next.Execute(context);
            }
        }

        protected abstract void OnProcess(TickContext context);
    }
}