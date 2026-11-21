using System;
using System.Collections.Generic;
using System.Linq;
namespace Beastmaster;

public sealed class ToggleLeaseBook(Func<DateTime>? clock=null) {
	private sealed record Lease(string Key,bool Value,DateTime Until);
	private readonly Dictionary<string,Lease> leases=new Dictionary<string,Lease>();
	private readonly Func<DateTime> now=clock ?? ((Func<DateTime>)(() => DateTime.UtcNow));
	public string Summary {
		get {
			Prune();
			return string.Join(", ",from k in leases.Values.Select((Lease l) => l.Key).Distinct()
				orderby k
				select k+" "+(Get(k,baseline: true)?"on":"off"));
		}
	}
	public bool Renew(string owner,string key,bool value,float seconds) {
		Prune();
		if(string.IsNullOrWhiteSpace(owner)||owner.Length>128||string.IsNullOrWhiteSpace(key)||!float.IsFinite(seconds)||seconds<=0f||seconds>5f||(!leases.ContainsKey(owner)&&leases.Count>=128)) {
			return false;
		}
		leases[owner]=new Lease(key,value,now().AddSeconds(seconds));
		return true;
	}
	public bool Release(string owner) {
		return leases.Remove(owner);
	}
	public void Clear() {
		leases.Clear();
	}
	public bool Get(string key,bool baseline) {
		Prune();
		Lease[] array=leases.Values.Where((Lease l) => l.Key==key).ToArray();
		if(array.Length!=0) {
			return array.All((Lease l) => l.Value);
		}
		return baseline;
	}
	public bool? Override(string key) {
		Prune();
		Lease[] array=leases.Values.Where((Lease l) => l.Key==key).ToArray();
		if(array.Length!=0) {
			return array.All((Lease l) => l.Value);
		}
		return null;
	}
	private void Prune() {
		DateTime time=now();
		string[] array=(from p in leases
			where time>=p.Value.Until
			select p.Key).ToArray();
		foreach(string key in array) {
			leases.Remove(key);
		}
	}
}
