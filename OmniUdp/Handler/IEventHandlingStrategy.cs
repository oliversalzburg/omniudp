using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OmniUdp.Handler {
  interface IEventHandlingStrategy {
    void HandleErrorEvent( byte[] payload );
    void HandleUidEvent( byte[] payload );
  }
}
