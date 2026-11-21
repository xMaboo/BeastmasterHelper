namespace Beastmaster;

public static class NatureTogglePolicy {
	public static OneWithNatureChoice? Resolve(bool enabled,OneWithNatureChoice selected,bool? master,bool? borrow,bool? tempered) {
		if(master==false||(borrow==true&&tempered==true)) {
			return null;
		}
		if(borrow==true) {
			return OneWithNatureChoice.Borrow;
		}
		if(tempered==true) {
			return OneWithNatureChoice.TemperedRelease;
		}
		if(!(master ?? enabled)) {
			return null;
		}
		if(selected!=OneWithNatureChoice.Borrow) {
			if(tempered!=false) {
				return selected;
			}
			return null;
		}
		if(borrow!=false) {
			return selected;
		}
		return null;
	}
}
