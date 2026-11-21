using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CrucibleDifficulty {
	public static readonly string[] Names=new string[4] { "Standard", "First Degree", "Second Degree", "Third Degree" };
	private const uint DropDownNode=25u;
	private const uint ListNodeInDropDown=3u;
	private const uint LeftArrowNode=26u;
	private const uint RightArrowNode=27u;
	private const int LeftParam=5;
	private const int RightParam=6;
	public static bool Available => Current().HasValue;
	private unsafe static AtkUnitBase* Detail() {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("XBMStageDetailList").Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return null;
		}
		return address;
	}
	private unsafe static AtkResNode* Root(AtkUnitBase* unit,uint nodeId) {
		AtkUldManager* ptr=&unit->UldManager;
		if(ptr->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)ptr->NodeListCount,2048);i++) {
			AtkResNode* ptr2=ptr->NodeList[i];
			if(ptr2!=null&&ptr2->NodeId==nodeId) {
				return ptr2;
			}
		}
		return null;
	}
	private unsafe static string? Label(AtkUldManager* manager,int depth) {
		if(depth>4||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,512);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||!ptr->IsVisible()) {
				continue;
			}
			if(ptr->Type==NodeType.Text) {
				string text=ptr->GetAsAtkTextNode()->NodeText.ToString().Trim();
				if(Array.IndexOf(Names,text)>=0) {
					return text;
				}
			}
			else {
				if((int)ptr->Type<1000||(depth==0&&ptr->NodeId==3)) {
					continue;
				}
				AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
				if(component!=null) {
					string text2=Label(&component->UldManager,depth+1);
					if(text2!=null) {
						return text2;
					}
				}
			}
		}
		return null;
	}
	public unsafe static int? Current() {
		try {
			AtkUnitBase* ptr=Detail();
			if(ptr==null) {
				return null;
			}
			AtkResNode* ptr2=Root(ptr,25u);
			if(ptr2==null||!ptr2->IsVisible()||(int)ptr2->Type<1000) {
				return null;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr2)->Component;
			string text=((component==null)?null:Label(&component->UldManager,0));
			return (text==null)?((int?)null):new int?(Array.IndexOf(Names,text));
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the difficulty selector.");
			return null;
		}
	}
	public static string NameOf(int? difficulty) {
		switch(difficulty) {
		default:
		return "unknown";
		case 0:
		case 1:
		case 2:
		case 3:
		return Names[difficulty.Value];
		}
	}
	public unsafe static bool Arrow(bool right) {
		try {
			AtkUnitBase* ptr=Detail();
			if(ptr==null) {
				return false;
			}
			AtkResNode* ptr2=Root(ptr,right?27u:26u);
			if(ptr2==null||!ptr2->IsVisible()) {
				return false;
			}
			int num=(right?6:5);
			AtkEvent* ptr3=ptr2->AtkEventManager.Event;
			int num2=0;
			while(ptr3!=null&&num2<128) {
				if(ptr3->State.EventType==AtkEventType.ButtonClick&&ptr3->Param==num&&ptr3->Listener==ptr) {
					AtkEvent atkEvent=*ptr3;
					atkEvent.Node=null;
					atkEvent.State.StateFlags=AtkEventStateFlags.Unk3;
					atkEvent.State.ReturnFlags=0;
					AtkEventData atkEventData=default;
					((AtkEventListener*)ptr)->ReceiveEvent(AtkEventType.ButtonClick,num,&atkEvent,&atkEventData);
					return true;
				}
				num2++;
				ptr3=ptr3->NextEvent;
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not press the difficulty arrow.");
		}
		return false;
	}
}
