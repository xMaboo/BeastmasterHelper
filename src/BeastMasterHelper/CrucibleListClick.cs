using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CrucibleListClick {
	public static bool RowByIndex(string addonName,int index,out string result) {
		return RowByIndex(addonName,index,null,out result);
	}
	public unsafe static bool RowByIndex(string addonName,int index,string? expected,out string result) {
		result="not attempted";
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			result=addonName+" is not open";
			return false;
		}
		AtkComponentList* ptr=FindList(&address->UldManager,0);
		if(ptr==null) {
			result=addonName+" has no list";
			return false;
		}
		if(index<0||index>=ptr->ListLength) {
			result=$"row {index} is outside the {ptr->ListLength} rows {addonName} has";
			return false;
		}
		ptr->ScrollToItem((short)index);
		AtkComponentListItemRenderer* itemRenderer=ptr->GetItemRenderer(index);
		if(itemRenderer==null) {
			result=$"row {index} has no renderer even after scrolling to it";
			return false;
		}
		if(!string.IsNullOrWhiteSpace(expected)&&!Contains(&((AtkComponentBase*)itemRenderer)->UldManager,expected,0)) {
			int num=IndexOf(addonName,expected);
			if(num<0||num==index) {
				result=$"row {index} does not hold {expected}, and no row does";
				return false;
			}
			result=$"row {index} held something else, so used row {num} for {expected}";
			string result2;
			return RowByIndex(addonName,num,null,out result2);
		}
		ptr->HoveredItemIndex=index;
		ptr->HoveredItemIndex2=index;
		ptr->HoveredItemIndex3=index;
		ptr->SelectedItemIndex=index;
		AtkEventListener* listener=(AtkEventListener*)address;
		if(DispatchListClick(&address->UldManager,listener,itemRenderer,index,0)) {
			result=$"sent {addonName} a list click on row {index}";
			return true;
		}
		AtkComponentButton* ptr2=FindButton(&((AtkComponentBase*)itemRenderer)->UldManager,0);
		if(ptr2!=null&&DispatchButton(ptr2)) {
			result=$"pressed the button in row {index}";
			return true;
		}
		result=$"row {index} would not take a click";
		return false;
	}
	private unsafe static AtkComponentList* FindList(AtkUldManager* manager,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return null;
		}
		AtkComponentList* ptr=null;
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr2=manager->NodeList[i];
			if(ptr2==null||(int)ptr2->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr2)->Component;
			if(component==null) {
				continue;
			}
			if(component->GetComponentType()==ComponentType.List) {
				AtkComponentList* ptr3=(AtkComponentList*)component;
				if(ptr==null||ptr3->ListLength>ptr->ListLength) {
					ptr=ptr3;
				}
			}
			else {
				AtkComponentList* ptr4=FindList(&component->UldManager,depth+1);
				if(ptr4!=null&&(ptr==null||ptr4->ListLength>ptr->ListLength)) {
					ptr=ptr4;
				}
			}
		}
		return ptr;
	}
	public unsafe static int IndexOf(string addonName,string text) {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return -1;
		}
		AtkComponentList* ptr=FindList(&address->UldManager,0);
		if(ptr==null) {
			return -1;
		}
		int firstVisibleItemIndex=ptr->FirstVisibleItemIndex;
		for(int i=0;i<ptr->ListLength;i++) {
			ptr->ScrollToItem((short)i);
			AtkComponentListItemRenderer* itemRenderer=ptr->GetItemRenderer(i);
			if(itemRenderer!=null&&Contains(&((AtkComponentBase*)itemRenderer)->UldManager,text,0)) {
				return i;
			}
		}
		ptr->ScrollToItem((short)firstVisibleItemIndex);
		return -1;
	}
	public unsafe static List<string> RowTexts(string addonName) {
		List<string> list=new List<string>();
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return list;
		}
		AtkComponentList* ptr=FindList(&address->UldManager,0);
		if(ptr==null) {
			return list;
		}
		int firstVisibleItemIndex=ptr->FirstVisibleItemIndex;
		for(int i=0;i<ptr->ListLength;i++) {
			ptr->ScrollToItem((short)i);
			AtkComponentListItemRenderer* itemRenderer=ptr->GetItemRenderer(i);
			if(itemRenderer!=null) {
				List<string> list2=new List<string>();
				CollectText(&((AtkComponentBase*)itemRenderer)->UldManager,list2,0);
				list.AddRange(list2);
			}
		}
		ptr->ScrollToItem((short)firstVisibleItemIndex);
		return list;
	}
	private unsafe static void CollectText(AtkUldManager* manager,List<string> texts,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null) {
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
					CollectText(&component->UldManager,texts,depth+1);
				}
			}
		}
	}
	public unsafe static void Realise(string addonName) {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return;
		}
		AtkComponentList* ptr=FindList(&address->UldManager,0);
		if(ptr!=null&&ptr->ListLength>0) {
			int firstVisibleItemIndex=ptr->FirstVisibleItemIndex;
			for(int i=0;i<ptr->ListLength;i++) {
				ptr->ScrollToItem((short)i);
			}
			ptr->ScrollToItem((short)firstVisibleItemIndex);
		}
	}
	public unsafe static bool RealiseRow(string addonName,int index,out int rows) {
		rows=-1;
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return false;
		}
		AtkComponentList* ptr=FindList(&address->UldManager,0);
		if(ptr==null||ptr->ListLength<=0) {
			return false;
		}
		rows=ptr->ListLength;
		if(index<0||index>=rows) {
			return false;
		}
		ptr->ScrollToItem((short)index);
		return true;
	}
	public unsafe static int RowCount(string addonName) {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return -1;
		}
		AtkComponentList* ptr=FindList(&address->UldManager,0);
		if(ptr!=null) {
			return ptr->ListLength;
		}
		return -1;
	}
	public unsafe static bool Row(string addonName,string text,out string result) {
		result="not attempted";
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			result=addonName+" is not open";
			return false;
		}
		int index=0;
		AtkComponentList* list=null;
		AtkComponentListItemRenderer* ptr=FindRow(&address->UldManager,text,0,ref index,ref list);
		if(ptr==null) {
			result="no row containing "+text+" in "+addonName;
			return false;
		}
		if(list!=null) {
			list->HoveredItemIndex=index;
			list->HoveredItemIndex2=index;
			list->HoveredItemIndex3=index;
			list->SelectedItemIndex=index;
		}
		AtkEventListener* listener=(AtkEventListener*)address;
		if(DispatchListClick(&address->UldManager,listener,ptr,index,0)) {
			result=$"sent {addonName} a list click on row {index} ({text})";
			return true;
		}
		AtkComponentButton* ptr2=FindButton(&((AtkComponentBase*)ptr)->UldManager,0);
		if(ptr2==null) {
			result=$"found row {index} for {text} but no button inside it";
			return false;
		}
		if(DispatchButton(ptr2)) {
			result=$"pressed the button in row {index} ({text})";
			return true;
		}
		result=$"row {index} for {text} has a button but it refused the click";
		return false;
	}
	private unsafe static bool DispatchListClick(AtkUldManager* manager,AtkEventListener* listener,AtkComponentListItemRenderer* renderer,int index,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return false;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null) {
				continue;
			}
			AtkEvent* ptr2=ptr->AtkEventManager.Event;
			int num=0;
			while(ptr2!=null&&num<128) {
				if(ptr2->State.EventType==AtkEventType.ListItemClick&&ptr2->Listener==listener) {
					AtkEvent atkEvent=*ptr2;
					atkEvent.State.StateFlags&=~AtkEventStateFlags.Handled;
					atkEvent.State.ReturnFlags=0;
					AtkEventData atkEventData=new AtkEventData {
						ListItemData = {
							ListItemRenderer = renderer,
							SelectedIndex = index,
							HoveredItemIndex3 = (short)index
						}
					};
					listener->ReceiveEvent(AtkEventType.ListItemClick,(int)ptr2->Param,&atkEvent,&atkEventData);
					return true;
				}
				num++;
				ptr2=ptr2->NextEvent;
			}
			if((int)ptr->Type>=1000) {
				AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
				if(component!=null&&DispatchListClick(&component->UldManager,listener,renderer,index,depth+1)) {
					return true;
				}
			}
		}
		return false;
	}
	private unsafe static bool DispatchButton(AtkComponentButton* button) {
		AtkResNode* ownerNode=(AtkResNode*)button->OwnerNode;
		if(ownerNode==null) {
			return false;
		}
		AtkEventType[] array=new AtkEventType[2] {
			AtkEventType.ButtonClick,
			AtkEventType.MouseClick
		};
		foreach(AtkEventType type in array) {
			if(Dispatch(ownerNode,type,(AtkEventListener*)button)) {
				return true;
			}
			AtkUldManager* ptr=&((AtkComponentBase*)button)->UldManager;
			if(ptr->NodeList==null) {
				continue;
			}
			for(int j=0;j<Math.Min((int)ptr->NodeListCount,512);j++) {
				AtkResNode* ptr2=ptr->NodeList[j];
				if(ptr2!=null&&Dispatch(ptr2,type,(AtkEventListener*)button)) {
					return true;
				}
			}
		}
		return false;
	}
	private unsafe static bool Dispatch(AtkResNode* node,AtkEventType type,AtkEventListener* listener) {
		AtkEvent* ptr=node->AtkEventManager.Event;
		int num=0;
		while(ptr!=null&&num<128) {
			if(ptr->State.EventType==type&&ptr->Listener==listener) {
				AtkEvent atkEvent=*ptr;
				atkEvent.State.StateFlags&=~AtkEventStateFlags.Handled;
				atkEvent.State.ReturnFlags=0;
				AtkEventData atkEventData=default;
				listener->ReceiveEvent(type,(int)ptr->Param,&atkEvent,&atkEventData);
				return true;
			}
			num++;
			ptr=ptr->NextEvent;
		}
		return false;
	}
	private unsafe static AtkComponentListItemRenderer* FindRow(AtkUldManager* manager,string text,int depth,ref int index,ref AtkComponentList* list) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			ComponentType componentType=component->GetComponentType();
			if(componentType==ComponentType.List) {
				list=(AtkComponentList*)component;
				index=0;
			}
			if(componentType==ComponentType.ListItemRenderer) {
				if(Contains(&component->UldManager,text,0)) {
					return (AtkComponentListItemRenderer*)component;
				}
				index++;
			}
			else {
				AtkComponentListItemRenderer* ptr2=FindRow(&component->UldManager,text,depth+1,ref index,ref list);
				if(ptr2!=null) {
					return ptr2;
				}
			}
		}
		return null;
	}
	private unsafe static bool Contains(AtkUldManager* manager,string text,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return false;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null) {
				continue;
			}
			if(ptr->Type==NodeType.Text&&((AtkTextNode*)ptr)->NodeText.ToString().Contains(text,StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			if((int)ptr->Type>=1000) {
				AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
				if(component!=null&&Contains(&component->UldManager,text,depth+1)) {
					return true;
				}
			}
		}
		return false;
	}
	private unsafe static AtkComponentButton* FindButton(AtkUldManager* manager,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component!=null) {
				if(component->GetComponentType()==ComponentType.Button) {
					return (AtkComponentButton*)component;
				}
				AtkComponentButton* ptr2=FindButton(&component->UldManager,depth+1);
				if(ptr2!=null) {
					return ptr2;
				}
			}
		}
		return null;
	}
}
