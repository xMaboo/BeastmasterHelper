using System;
namespace Beastmaster;

public sealed class BeastmasterTrickWindow {
	private DateTime expires;
	public void Begin(DateTime now) {
		expires=now.AddSeconds(3.0);
	}
	public void Reset() {
		expires=default;
	}
	public bool Waiting(DateTime now,bool heartPresent,bool familiarPresent) {
		if(heartPresent||!familiarPresent||now>=expires) {
			Reset();
			return false;
		}
		return expires!=default(DateTime);
	}
}
