using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Chat;
namespace Beastmaster;

public sealed class CrucibleFeedFeedback : IDisposable {
	private readonly HashSet<string> full=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private DateTime insufficientAtUtc=DateTime.MinValue;
	private int campsiteCapacity=3;
	private readonly Dictionary<string,HashSet<string>> eaten=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
	private readonly List<string> log=new List<string>();
	private readonly object gate=new object();
	private static readonly Regex NotHungry=new Regex("Your ([^,.]+?) is not hungry",RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static readonly Regex CampsiteCapacityLine=new Regex("campsite can only accommodate\\D{0,20}(\\d+)\\s*familiar",RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static readonly Regex Insufficient=new Regex("Insufficient territory tokens|Unable to purchase",RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static readonly Regex Variety=new Regex("Your ([^,.]+?) desires variety.*?will not consume another ([^.]+)",RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.Singleline|RegexOptions.CultureInvariant);
	private static readonly Regex Askance=new Regex("Your ([^,.]+?) (?:looks askance|refuses)",RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private readonly Dictionary<string,int> fed=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
	public static CrucibleFeedFeedback Shared { get; }=new CrucibleFeedFeedback();
	public int CampsiteCapacity {
		get {
			lock(gate) {
				return campsiteCapacity;
			}
		}
	}
	public IReadOnlyCollection<string> Full {
		get {
			lock(gate) {
				return full.ToList();
			}
		}
	}
	public void ResetCampsiteCapacity() {
		lock(gate) {
			campsiteCapacity=3;
		}
	}
	private CrucibleFeedFeedback() {
		DalamudApi.ChatGui.ChatMessage+=OnChat;
	}
	public void Dispose() {
		DalamudApi.ChatGui.ChatMessage-=OnChat;
	}
	public void Ate(string familiarName) {
		if(string.IsNullOrWhiteSpace(familiarName)) {
			return;
		}
		lock(gate) {
			fed[familiarName]=((!fed.TryGetValue(familiarName,out var value))?1:(value+1));
		}
	}
	public int FedCount(string familiarName) {
		lock(gate) {
			foreach(var (text2,result) in fed) {
				if(familiarName.Contains(text2,StringComparison.OrdinalIgnoreCase)||text2.Contains(familiarName,StringComparison.OrdinalIgnoreCase)) {
					return result;
				}
			}
		}
		return 0;
	}
	public bool HasRoom(string familiarName,int feedSlots) {
		if(IsFull(familiarName)) {
			return false;
		}
		if(feedSlots>0) {
			return FedCount(familiarName)<feedSlots;
		}
		return true;
	}
	public bool IsFull(string familiarName) {
		lock(gate) {
			return full.Any((string name) => familiarName.Contains(name,StringComparison.OrdinalIgnoreCase)||name.Contains(familiarName,StringComparison.OrdinalIgnoreCase));
		}
	}
	public bool HasEaten(string familiarName,CrucibleItem? item) {
		if(item==null) {
			return false;
		}
		lock(gate) {
			foreach(var (text2,source) in eaten) {
				if((familiarName.Contains(text2,StringComparison.OrdinalIgnoreCase)||text2.Contains(familiarName,StringComparison.OrdinalIgnoreCase))&&source.Any(item.IsNamed)) {
					return true;
				}
			}
		}
		return false;
	}
	public string Describe() {
		lock(gate) {
			return (log.Count==0)?string.Empty:string.Join("; ",log.TakeLast(4));
		}
	}
	public bool RefusedForTokensSince(DateTime sinceUtc) {
		lock(gate) {
			return insufficientAtUtc>=sinceUtc;
		}
	}
	public void Reset() {
		lock(gate) {
			full.Clear();
			eaten.Clear();
			fed.Clear();
			log.Clear();
			insufficientAtUtc=DateTime.MinValue;
			campsiteCapacity=3;
		}
	}
	private void OnChat(IHandleableChatMessage message) {
		try {
			string textValue=message.Message.TextValue;
			if(textValue.Length==0) {
				return;
			}
			Match match=CampsiteCapacityLine.Match(textValue);
			if(match.Success&&int.TryParse(match.Groups[1].Value,out var result)) {
				lock(gate) {
					campsiteCapacity=Math.Clamp(result,0,3);
					log.Add($"this campsite accommodates {campsiteCapacity} familiar(s)");
					while(log.Count>20) {
						log.RemoveAt(0);
					}
					return;
				}
			}
			if(Insufficient.IsMatch(textValue)) {
				lock(gate) {
					insufficientAtUtc=DateTime.UtcNow;
					log.Add("a purchase was refused for want of tokens");
					while(log.Count>20) {
						log.RemoveAt(0);
					}
					return;
				}
			}
			Match match2=Variety.Match(textValue);
			if(match2.Success) {
				string text=match2.Groups[1].Value.Trim();
				string text2=match2.Groups[2].Value.Trim();
				lock(gate) {
					if(!eaten.TryGetValue(text,out HashSet<string> value)) {
						value=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
						eaten[text]=value;
					}
					if(value.Add(text2)) {
						log.Add(text+" has already eaten "+text2);
						while(log.Count>20) {
							log.RemoveAt(0);
						}
					}
					return;
				}
			}
			Match match3=NotHungry.Match(textValue);
			if(match3.Success) {
				string text3=match3.Groups[1].Value.Trim();
				lock(gate) {
					if(full.Add(text3)) {
						log.Add(text3+" has no buff slot left");
					}
					return;
				}
			}
			Match match4=Askance.Match(textValue);
			if(!match4.Success) {
				return;
			}
			lock(gate) {
				log.Add(match4.Groups[1].Value.Trim()+" refused the item outright");
				while(log.Count>20) {
					log.RemoveAt(0);
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read a feed refusal from chat.");
		}
	}
}
