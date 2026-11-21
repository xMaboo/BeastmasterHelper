using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CrucibleTreasureReader {
	public enum TokenCardState {
		Unknown,
		Waiting,
		Acquired,
		Absent
	}
	public const string Addon="XBMContentsTreasure";
	public const string BootyAddon="XBMContentsBooty";
	public static string LastResult { get; private set; }="not attempted";
	public unsafe static bool IsOpen => Unit()!=null;
	private unsafe static AtkUnitBase* Unit() {
		return Unit("XBMContentsTreasure");
	}
	private unsafe static AtkUnitBase* Unit(string addon) {
		try {
			AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addon).Address;
			return (address!=null&&address->IsVisible&&address->IsReady)?address:null;
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not reach the treasure window.");
			return null;
		}
	}
	public static List<CrucibleTreasureOffer> Offers() {
		return Offers("XBMContentsTreasure");
	}
	public unsafe static List<CrucibleTreasureOffer> Offers(string addon) {
		List<CrucibleTreasureOffer> list=new List<CrucibleTreasureOffer>();
		AtkUnitBase* ptr=Unit(addon);
		if(ptr==null) {
			LastResult="the treasure window is not open";
			return list;
		}
		AtkUldManager* ptr2=&ptr->UldManager;
		if(ptr2->NodeList==null) {
			LastResult="the treasure window has no nodes yet";
			return list;
		}
		HashSet<int> hashSet=new HashSet<int>();
		for(int i=0;i<Math.Min((int)ptr2->NodeListCount,2048);i++) {
			AtkResNode* ptr3=ptr2->NodeList[i];
			if(ptr3==null||!ptr3->IsVisible()||(int)ptr3->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr3)->Component;
			if(component==null||component->GetComponentType()!=ComponentType.Button) {
				continue;
			}
			int? num=ClickParam(ptr3,(AtkEventListener*)ptr);
			if(!num.HasValue) {
				continue;
			}
			List<string> list2=new List<string>();
			Collect(&component->UldManager,list2,0);
			CrucibleItem crucibleItem=null;
			bool acquired=false;
			foreach(string item in list2) {
				if(item.StartsWith("Acquired",StringComparison.OrdinalIgnoreCase)) {
					acquired=true;
				}
				else if((object)crucibleItem==null) {
					crucibleItem=CrucibleItemCatalogue.ByName(item);
				}
			}
			if(!(crucibleItem==null)&&hashSet.Add(crucibleItem.Id)) {
				list.Add(new CrucibleTreasureOffer(crucibleItem,num.Value,acquired));
			}
		}
		LastResult=((list.Count==0)?"the treasure window is open but no card matched a known item":$"read {list.Count} card(s)");
		return list;
	}
	public static bool Take(CrucibleTreasureOffer offer) {
		return Take("XBMContentsTreasure",offer);
	}
	public unsafe static bool Take(string addon,CrucibleTreasureOffer offer) {
		AtkUnitBase* ptr=Unit(addon);
		if(ptr==null) {
			LastResult="the treasure window closed before the card could be taken";
			return false;
		}
		AtkUldManager* ptr2=&ptr->UldManager;
		if(ptr2->NodeList==null) {
			return false;
		}
		AtkEventListener* ptr3=(AtkEventListener*)ptr;
		for(int i=0;i<Math.Min((int)ptr2->NodeListCount,2048);i++) {
			AtkResNode* ptr4=ptr2->NodeList[i];
			if(ptr4==null||!ptr4->IsVisible()||(int)ptr4->Type<1000) {
				continue;
			}
			AtkEvent* ptr5=ptr4->AtkEventManager.Event;
			int num=0;
			while(ptr5!=null&&num<128) {
				if(ptr5->State.EventType==AtkEventType.ButtonClick&&ptr5->Listener==ptr3&&ptr5->Param==offer.Param) {
					AtkEvent atkEvent=*ptr5;
					atkEvent.Node=null;
					atkEvent.State.StateFlags=AtkEventStateFlags.Unk3;
					atkEvent.State.ReturnFlags=0;
					AtkEventData atkEventData=default;
					ptr3->ReceiveEvent(AtkEventType.ButtonClick,offer.Param,&atkEvent,&atkEventData);
					LastResult=$"took {offer.Item.Name} from slot {offer.Param}";
					return true;
				}
				num++;
				ptr5=ptr5->NextEvent;
			}
		}
		LastResult=$"no card left with slot {offer.Param} for {offer.Item.Name}";
		return false;
	}
	public unsafe static TokenCardState TerritoryTokenState() {
		AtkUnitBase* ptr=Unit("XBMContentsBooty");
		if(ptr==null||ptr->UldManager.NodeList==null) {
			return TokenCardState.Unknown;
		}
		AtkEventListener* listener=(AtkEventListener*)ptr;
		for(int i=0;i<Math.Min((int)ptr->UldManager.NodeListCount,2048);i++) {
			AtkResNode* ptr2=ptr->UldManager.NodeList[i];
			if(ptr2==null||!ptr2->IsVisible()||(int)ptr2->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr2)->Component;
			if(component==null||component->GetComponentType()!=ComponentType.Button) {
				continue;
			}
			List<string> list=new List<string>();
			Collect(&component->UldManager,list,0);
			if(list.Any((string text) => text.Contains("Territory Tokens",StringComparison.OrdinalIgnoreCase))&&ClickParam(ptr2,listener).HasValue) {
				if(!list.Any((string text) => text.StartsWith("Acquired",StringComparison.OrdinalIgnoreCase))) {
					return TokenCardState.Waiting;
				}
				return TokenCardState.Acquired;
			}
		}
		return TokenCardState.Absent;
	}
	public unsafe static bool TakeTerritoryTokens(out string result) {
		AtkUnitBase* ptr=Unit("XBMContentsBooty");
		if(ptr==null||ptr->UldManager.NodeList==null) {
			result="loot window is not ready";
			return false;
		}
		AtkEventListener* ptr2=(AtkEventListener*)ptr;
		for(int i=0;i<Math.Min((int)ptr->UldManager.NodeListCount,2048);i++) {
			AtkResNode* ptr3=ptr->UldManager.NodeList[i];
			if(ptr3==null||!ptr3->IsVisible()||(int)ptr3->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr3)->Component;
			if(component==null||component->GetComponentType()!=ComponentType.Button) {
				continue;
			}
			List<string> list=new List<string>();
			Collect(&component->UldManager,list,0);
			if(!list.Any((string text) => text.Contains("Territory Tokens",StringComparison.OrdinalIgnoreCase))) {
				continue;
			}
			int? value=ClickParam(ptr3,ptr2);
			if(!value.HasValue) {
				continue;
			}
			AtkEvent* ptr4=ptr3->AtkEventManager.Event;
			int num=0;
			while(ptr4!=null&&num<128) {
				if(ptr4->State.EventType==AtkEventType.ButtonClick&&ptr4->Listener==ptr2&&ptr4->Param==value.Value) {
					AtkEvent atkEvent=*ptr4;
					atkEvent.Node=null;
					atkEvent.State.StateFlags=AtkEventStateFlags.Unk3;
					atkEvent.State.ReturnFlags=0;
					AtkEventData atkEventData=default;
					ptr2->ReceiveEvent(AtkEventType.ButtonClick,value.Value,&atkEvent,&atkEventData);
					result=$"clicked Territory Tokens card (param {value})";
					return true;
				}
				num++;
				ptr4=ptr4->NextEvent;
			}
			result="token card had no dispatchable registered click";
			return false;
		}
		result="no unclaimed Territory Tokens card was visible";
		return false;
	}
	private unsafe static int? ClickParam(AtkResNode* node,AtkEventListener* listener) {
		AtkEvent* ptr=node->AtkEventManager.Event;
		int num=0;
		while(ptr!=null&&num<128) {
			if(ptr->State.EventType==AtkEventType.ButtonClick&&ptr->Listener==listener) {
				return (int)ptr->Param;
			}
			num++;
			ptr=ptr->NextEvent;
		}
		return null;
	}
	private unsafe static void Collect(AtkUldManager* manager,List<string> texts,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||!ptr->IsVisible()) {
				continue;
			}
			if(ptr->Type==NodeType.Text) {
				string text=((AtkTextNode*)ptr)->NodeText.ToString();
				if(text.Length>0) {
					texts.Add(text);
				}
			}
			if((int)ptr->Type>=1000) {
				AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
				if(component!=null) {
					Collect(&component->UldManager,texts,depth+1);
				}
			}
		}
	}
}
