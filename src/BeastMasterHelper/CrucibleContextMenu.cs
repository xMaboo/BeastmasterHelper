using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CrucibleContextMenu {
	private const string Addon="ContextMenu";
	public static string LastResult { get; private set; }="not attempted";
	public unsafe static bool IsOpen => Unit()!=null;
	private unsafe static AtkUnitBase* Unit() {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("ContextMenu").Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return null;
		}
		return address;
	}
	public unsafe static List<string> Entries() {
		List<string> list=new List<string>();
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return list;
		}
		try {
			FindList(&ptr->UldManager,list,0);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the context menu.");
		}
		return list;
	}
	private unsafe static bool FindList(AtkUldManager* manager,List<string> entries,int depth) {
		if(depth>6||manager==null||manager->NodeList==null) {
			return false;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,512);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(component->GetComponentType()==ComponentType.List) {
				AtkComponentList* ptr2=(AtkComponentList*)component;
				for(int j=0;j<Math.Min(ptr2->ListLength,32);j++) {
					AtkComponentListItemRenderer* itemRenderer=ptr2->GetItemRenderer(j);
					string item=((itemRenderer!=null&&itemRenderer->ButtonTextNode!=null)?itemRenderer->ButtonTextNode->NodeText.ToString().Trim():string.Empty);
					entries.Add(item);
				}
				return true;
			}
			if(FindList(&component->UldManager,entries,depth+1)) {
				return true;
			}
		}
		return false;
	}
	public unsafe static bool Select(string text,int fallbackIndex) {
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			LastResult="the context menu is not open";
			return false;
		}
		List<string> list=Entries();
		int num=list.FindIndex((string e) => e.Contains(text,StringComparison.OrdinalIgnoreCase));
		if(num<0) {
			if(list.Any((string e) => e.Length>0)) {
				LastResult="no entry containing "+text+" among: "+string.Join(" | ",list);
				return false;
			}
			num=fallbackIndex;
		}
		AtkValue* ptr2=stackalloc AtkValue[5];
		for(int num2=0;num2<5;num2++) {
			ptr2[num2].Type=AtkValueType.Int;
			ptr2[num2].Int=0;
		}
		ptr2[1].Int=num;
		ptr->FireCallback(5u,ptr2);
		LastResult=$"chose entry {num} ({((num<list.Count)?list[num]:"unread")})";
		return true;
	}
	public unsafe static void Close() {
		AtkUnitBase* ptr=Unit();
		if(ptr!=null) {
			AtkValue* ptr2=stackalloc AtkValue[1];
			ptr2->Type=AtkValueType.Int;
			ptr2->Int=-1;
			ptr->FireCallback(1u,ptr2);
		}
	}
}
