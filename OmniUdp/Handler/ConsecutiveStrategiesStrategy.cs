using System.Collections.Generic;

namespace OmniUdp.Handler {
	/// <summary>
	///     An event handling strategy that can wrap multiple other strategies and apply them in ordern.
	/// </summary>
	internal class ConsecutiveStrategiesStrategy : IEventHandlingStrategy {
		/// <summary>
		///     The strategies to use.
		/// </summary>
		public List<IEventHandlingStrategy> Strategies { get; private set; }

		/// <summary>
		///     Construct a new ConsecutiveStrategiesStrategy instance.
		/// </summary>
		public ConsecutiveStrategiesStrategy() {
			Strategies = new List<IEventHandlingStrategy>();
		}

		/// <summary>
		///     Handle an event that should be treated as an error.
		/// </summary>
		/// <param name="payload">The payload to send with the event.</param>
		public void HandleErrorEvent( byte[] error ) {
			foreach( IEventHandlingStrategy strategy in Strategies ) {
				strategy.HandleErrorEvent( error );
			}
		}

		/// <summary>
		///     Handle an event that should be treated as a UID being successfully read.
		/// </summary>
		/// <param name="payload">The payload to send with the event.</param>
		public void HandleUidEvent( byte[] uid ) {
			foreach( IEventHandlingStrategy strategy in Strategies ) {
				strategy.HandleUidEvent( uid );
			}
		}
	}
}