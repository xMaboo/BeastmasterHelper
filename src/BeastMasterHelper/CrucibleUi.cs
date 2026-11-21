using System;
using System.Collections.Generic;
using System.Globalization;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CrucibleUi {
	private static bool rowFound;
	private static bool buttonFound;
	private static DateTime lastYesUtc;
	private static int yesAttempts;
	public static string LastRowClick { get; private set; }="not attempted";
	public static string LastYesNo { get; private set; }="not attempted";
	public unsafe static bool YesNoOpen {
		get {
			AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("SelectYesno").Address;
			if(address!=null&&address->IsVisible) {
				return address->IsReady;
			}
			return false;
		}
	}
	private static string Clean(string text) {
		return text.Trim().TrimEnd('.');
	}
	private static bool Matches(string text,string wanted) {
		return Clean(text).Equals(Clean(wanted),StringComparison.OrdinalIgnoreCase);
	}
	public static bool HasMenu(string label) {
		return Menu(label,select: false);
	}
	public static bool SelectMenu(string label) {
		return Menu(label,select: true);
	}
	private unsafe static bool Menu(string label,bool select) {
		AddonSelectIconString* address=(AddonSelectIconString*)DalamudApi.GameGui.GetAddonByName("SelectIconString").Address;
		if(address!=null&&address->IsVisible&&address->IsReady&&MenuEntry(&address->AtkUnitBase,(PopupMenu*)(&address->PopupMenu),label,select)) {
			return true;
		}
		AddonSelectString* address2=(AddonSelectString*)DalamudApi.GameGui.GetAddonByName("SelectString").Address;
		if(address2!=null&&address2->IsVisible&&address2->IsReady) {
			return MenuEntry(&address2->AtkUnitBase,(PopupMenu*)(&address2->PopupMenu),label,select);
		}
		return false;
	}
	private unsafe static bool MenuEntry(AtkUnitBase* addon,PopupMenu* menu,string label,bool select) {
		bool flag=menu->EntryNames==null;
		if(!flag) {
			int entryCount=menu->EntryCount;
			bool flag2=((entryCount<1||entryCount>100)?true:false);
			flag=flag2;
		}
		if(flag||menu->List==null) {
			return false;
		}
		for(int i=0;i<menu->EntryCount;i++) {
			if(Matches(menu->EntryNames[i].ToString(),label)&&!menu->List->GetItemDisabledState(i)) {
				if(select) {
					addon->FireCallbackInt(i);
				}
				return true;
			}
		}
		return false;
	}
	public unsafe static bool Commence(string boardName) {
		AddonContentsFinderConfirm* address=(AddonContentsFinderConfirm*)DalamudApi.GameGui.GetAddonByName("ContentsFinderConfirm").Address;
		if(address==null||!address->IsVisible||!address->IsReady||address->AtkTextNode230==null) {
			return false;
		}
		if(!address->AtkTextNode230->NodeText.ToString().Contains(boardName,StringComparison.OrdinalIgnoreCase)) {
			return false;
		}
		return Click("Commence");
	}
	public unsafe static bool SelectBoard(string label) {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("XBMStageList").Address;
		if(address!=null&&address->IsVisible&&address->IsReady) {
			return SelectBoardTree(address,&address->UldManager,label,0);
		}
		return false;
	}
	private unsafe static bool SelectBoardTree(AtkUnitBase* addon,AtkUldManager* manager,string label,int depth) {
		if(depth>8||manager->NodeList==null) {
			return false;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||!ptr->IsVisible()||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(component->GetComponentType()==ComponentType.TreeList) {
				AtkComponentTreeList* ptr2=(AtkComponentTreeList*)component;
				for(int j=0;j<Math.Min(ptr2->Items.Count,100);j++) {
					AtkComponentTreeListItem* value=ptr2->Items[j].Value;
					if(value==null||value->IsHidden||(value->State&TreeListItemState.Disabled)!=0) {
						continue;
					}
					AtkComponentListItemRenderer* renderer=value->Renderer;
					if(renderer==null||!renderer->IsEnabled) {
						continue;
					}
					string text=((value->StringValues.Count>0)?value->StringValues[0].ToString():"");
					if(string.IsNullOrWhiteSpace(text)&&renderer->ButtonTextNode!=null) {
						text=renderer->ButtonTextNode->NodeText.ToString();
					}
					if(Matches(text,label)) {
						int listItemIndex=renderer->ListItemIndex;
						if(listItemIndex<0||listItemIndex>=ptr2->ListLength||!ptr2->IsItemInteractionEnabled||ptr2->GetItemDisabledState(listItemIndex)) {
							return false;
						}
						AtkEvent atkEvent=new AtkEvent {
							Target = (AtkEventTarget*)ptr2->OwnerNode,
							Listener = (AtkEventListener*)addon,
							State = new AtkEventState {
								EventType = AtkEventType.ListItemClick,
								StateFlags = AtkEventStateFlags.Unk3
							}
						};
						AtkEventData atkEventData=new AtkEventData {
							ListItemData = {
								ListItemRenderer = renderer,
								ListItem = value,
								SelectedIndex = listItemIndex
							}
						};
						((short*)(&atkEventData))[10]=-1;
						atkEventData.ListItemData.HoveredItemIndex3=-1;
						((AtkEventListener*)addon)->ReceiveEvent(AtkEventType.ListItemClick,0,&atkEvent,&atkEventData);
						return true;
					}
				}
			}
			if(SelectBoardTree(addon,&component->UldManager,label,depth+1)) {
				return true;
			}
		}
		return false;
	}
	public static List<string> VisibleWindows() {
		return VisibleWindows(xbmOnly: false);
	}
	public static List<string> VisibleXbmWindows() {
		return VisibleWindows(xbmOnly: true);
	}
	private unsafe static List<string> VisibleWindows(bool xbmOnly) {
		List<string> list=new List<string>();
		AtkStage* ptr=AtkStage.Instance();
		if(ptr==null||ptr->RaptureAtkUnitManager==null) {
			return list;
		}
		AtkUnitList* ptr2=&ptr->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
		for(int i=0;i<Math.Min((int)ptr2->Count,256);i++) {
			AtkUnitBase* value=ptr2->Entries[i].Value;
			if(value!=null&&value->IsVisible&&value->IsReady) {
				string nameString=value->NameString;
				if(nameString.Length!=0&&!list.Contains(nameString)&&(!xbmOnly||nameString.StartsWith("XBM",StringComparison.OrdinalIgnoreCase))) {
					list.Add(nameString);
				}
			}
		}
		return list;
	}
	public unsafe static List<string> ButtonCaptions(string addonName) {
		List<string> list=new List<string>();
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return list;
		}
		CollectCaptions(&address->UldManager,list,0);
		return list;
	}
	private unsafe static void CollectCaptions(AtkUldManager* manager,List<string> captions,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||!ptr->IsVisible()||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(component->GetComponentType()==ComponentType.Button) {
				AtkComponentButton* ptr2=(AtkComponentButton*)component;
				if(ptr2->IsEnabled&&ptr2->ButtonTextNode!=null) {
					string text=ptr2->ButtonTextNode->NodeText.ToString();
					if(text.Length>0&&!captions.Contains(text)) {
						captions.Add(text);
					}
				}
			}
			CollectCaptions(&component->UldManager,captions,depth+1);
		}
	}
	public unsafe static bool ClickIn(string addonName,string caption) {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			return false;
		}
		return ClickCaption(&address->UldManager,caption,(AtkEventListener*)address,0);
	}
	private unsafe static bool ClickCaption(AtkUldManager* manager,string caption,AtkEventListener* listener,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return false;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||!ptr->IsVisible()||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(component->GetComponentType()==ComponentType.Button) {
				AtkComponentButton* ptr2=(AtkComponentButton*)component;
				if(ptr2->IsEnabled&&ptr2->ButtonTextNode!=null&&string.Equals(ptr2->ButtonTextNode->NodeText.ToString(),caption,StringComparison.OrdinalIgnoreCase)&&ClickButton(ptr2,listener)) {
					return true;
				}
			}
			if(ClickCaption(&component->UldManager,caption,listener,depth+1)) {
				return true;
			}
		}
		return false;
	}
	public static bool Click(string label,bool lists=false) {
		return Scan(label,lists,activate: true).clicked;
	}
	public static bool Has(string label) {
		return Scan(label,lists: false,activate: false).found;
	}
	public static string DescribeAddon(string name) {
		return Scan("",lists: true,activate: false,fightOnly: false,name).description;
	}
	public static string Describe() {
		return Scan("",lists: true,activate: false).description;
	}
	public unsafe static string DescribeFightPreparation() {
		List<string> list=new List<string>();
		string[] array=new string[2] { "XBMStageDetailList", "XBMPetParty" };
		foreach(string text in array) {
			AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(text).Address;
			if(address==null||!address->IsVisible||!address->IsReady||address->AtkValues==null) {
				list.Add(text+": not ready/visible");
				continue;
			}
			list.Add($"{text}: values={address->AtkValuesCount}");
			for(int j=0;j<Math.Min((int)address->AtkValuesCount,4096);j++) {
				AtkValue atkValue=address->AtkValues[j];
				string text2;
				switch(atkValue.Type&AtkValueType.TypeMask) {
				case AtkValueType.UInt:
				text2=atkValue.UInt.ToString();
				break;
				case AtkValueType.Int:
				text2=atkValue.Int.ToString();
				break;
				case AtkValueType.Bool:
				text2=atkValue.Bool.ToString();
				break;
				case AtkValueType.Float:
				text2=atkValue.Float.ToString(CultureInfo.InvariantCulture);
				break;
				case AtkValueType.String:
				case AtkValueType.ConstString:
				text2=atkValue.String.ToString();
				break;
				default:
				text2=null;
				break;
				}
				string text3=text2;
				if(text3!=null) {
					list.Add($"{text}: value[{j}] {atkValue.Type&AtkValueType.TypeMask} = {text3}");
				}
			}
		}
		return string.Join("\n",list);
	}
	private unsafe static (bool found,bool clicked,string description) Scan(string label,bool lists,bool activate,bool fightOnly=false,string? onlyAddon=null) {
		AtkStage* ptr=AtkStage.Instance();
		if(ptr==null||ptr->RaptureAtkUnitManager==null) {
			return (found: false,clicked: false,description: "UI unavailable");
		}
		AtkUnitList* ptr2=&ptr->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
		List<string> list=new List<string>();
		bool flag=false;
		for(int i=0;i<Math.Min((int)ptr2->Count,256);i++) {
			AtkUnitBase* value=ptr2->Entries[i].Value;
			if(value==null||!value->IsVisible||!value->IsReady) {
				continue;
			}
			string nameString=value->NameString;
			if((onlyAddon!=null&&nameString!=onlyAddon)||(fightOnly&&nameString!="XBMPetParty"&&nameString!="XBMStageDetailList"&&nameString!="XBMStageDeatilList")) {
				continue;
			}
			if(label.Length==0) {
				list.Add($"Visible addon: {nameString} address=0x{(nuint)value:X}");
			}
			if(!nameString.StartsWith("XBM",StringComparison.OrdinalIgnoreCase)&&nameString!="ContentsFinderConfirm"&&onlyAddon==null) {
				continue;
			}
			if(label.Length==0&&value->AtkValues!=null) {
				for(int j=0;j<Math.Min((int)value->AtkValuesCount,1024);j++) {
					AtkValue atkValue=value->AtkValues[j];
					string text;
					switch(atkValue.Type&AtkValueType.TypeMask) {
					case AtkValueType.Int:
					text=atkValue.Int.ToString();
					break;
					case AtkValueType.UInt:
					text=atkValue.UInt.ToString();
					break;
					case AtkValueType.Bool:
					text=atkValue.Bool.ToString();
					break;
					case AtkValueType.Float:
					text=atkValue.Float.ToString(CultureInfo.InvariantCulture);
					break;
					case AtkValueType.String:
					case AtkValueType.ConstString:
					text=atkValue.String.ToString();
					break;
					default:
					text="[uninterpreted]";
					break;
					}
					string text2=text;
					if(text2.Length>0) {
						list.Add($"{nameString}: value[{j}] {atkValue.Type} = {text2}");
					}
				}
			}
			(bool,bool) tuple=Visit(&value->UldManager,label,lists,activate,list,nameString,(AtkEventListener*)value,0);
			flag|=tuple.Item1;
			if(tuple.Item2) {
				return (found: true,clicked: true,description: string.Join("\n",list));
			}
		}
		return (found: flag,clicked: false,description: (list.Count==0)?"No visible Crucible windows detected.":string.Join("\n",list));
	}
	private unsafe static (bool found,bool clicked) Visit(AtkUldManager* manager,string label,bool lists,bool activate,List<string> report,string addon,AtkEventListener* addonListener,int depth) {
		if(depth>8||manager->NodeList==null) {
			return (found: false,clicked: false);
		}
		bool flag=false;
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(label.Length!=0&&!ptr->IsVisible())) {
				continue;
			}
			if(label.Length==0) {
				report.Add($"{addon}: depth={depth} index={i} node={ptr->NodeId} ptr=0x{(nuint)ptr:X} type={ptr->Type} visible={ptr->IsVisible()} flags={ptr->NodeFlags} parent=0x{(nuint)ptr->ParentNode:X} screen=({ptr->ScreenX},{ptr->ScreenY}) size=({ptr->Width},{ptr->Height})");
				DescribeEvents(ptr,report,addon);
			}
			if(label.Length==0&&ptr->Type==NodeType.Image) {
				AtkImageNode* ptr2=(AtkImageNode*)ptr;
				report.Add($"{addon}: image node={ptr->NodeId} part={ptr2->PartId} parts=0x{(nuint)ptr2->PartsList:X}");
			}
			if(ptr->Type==NodeType.Text) {
				string text=((AtkTextNode*)ptr)->NodeText.ToString();
				if(text.Length>0) {
					report.Add(addon+": "+text);
				}
			}
			AtkComponentButton* asAtkComponentButton=ptr->GetAsAtkComponentButton();
			if(asAtkComponentButton!=null&&asAtkComponentButton->ButtonTextNode!=null) {
				string text2=asAtkComponentButton->ButtonTextNode->NodeText.ToString();
				if(text2.Length>0) {
					report.Add($"{addon}: {text2} ({(asAtkComponentButton->IsEnabled?"enabled":"disabled")}) node={ptr->NodeId}");
				}
				if(label.Length==0) {
					DescribeEvents(ptr,report,addon);
					AtkUldManager* ptr3=&asAtkComponentButton->UldManager;
					if(ptr3->NodeList!=null) {
						for(int j=0;j<Math.Min((int)ptr3->NodeListCount,2048);j++) {
							DescribeEvents(ptr3->NodeList[j],report,addon);
						}
					}
				}
				if(Matches(text2,label)) {
					flag=true;
					if(activate&&!lists&&asAtkComponentButton->IsEnabled&&ClickButton(asAtkComponentButton,addonListener)) {
						return (found: true,clicked: true);
					}
				}
			}
			AtkComponentList* asAtkComponentList=ptr->GetAsAtkComponentList();
			if(label.Length==0&&asAtkComponentList!=null) {
				report.Add($"{addon}: list node={ptr->NodeId} rows={asAtkComponentList->ListLength} interaction={asAtkComponentList->IsItemInteractionEnabled} click={asAtkComponentList->IsItemClickEnabled} selected={asAtkComponentList->SelectedItemIndex} hovered={asAtkComponentList->HoveredItemIndex}/{asAtkComponentList->HoveredItemIndex2}/{asAtkComponentList->HoveredItemIndex3}");
				DescribeEvents(ptr,report,addon);
			}
			if(lists&&asAtkComponentList!=null) {
				int listLength=asAtkComponentList->ListLength;
				if(listLength>0&&listLength<100) {
					for(int k=0;k<asAtkComponentList->ListLength;k++) {
						AtkComponentListItemRenderer* itemRenderer=asAtkComponentList->GetItemRenderer(k);
						if(itemRenderer==null) {
							if(label.Length==0) {
								report.Add($"{addon}: row {k+1}: renderer unavailable (skipped)");
							}
							continue;
						}
						string text3=((itemRenderer->ButtonTextNode!=null)?itemRenderer->ButtonTextNode->NodeText.ToString():"");
						if(text3.Length>0) {
							report.Add($"{addon}: row {k+1}: {text3}");
						}
						if(Matches(text3,label)) {
							flag=true;
							if(activate&&asAtkComponentList->IsItemInteractionEnabled&&asAtkComponentList->IsItemClickEnabled&&!asAtkComponentList->GetItemDisabledState(k)&&itemRenderer!=null&&DispatchListSelection(ptr,itemRenderer,k,addonListener)) {
								return (found: true,clicked: true);
							}
						}
					}
				}
			}
			if((int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(label.Length==0) {
				ComponentType componentType=component->GetComponentType();
				report.Add($"{addon}: component node={ptr->NodeId} kind={componentType} address=0x{(nuint)component:X}");
				if(componentType==ComponentType.Icon) {
					report.Add($"{addon}: node={ptr->NodeId} icon={((AtkComponentIcon*)component)->IconId}");
				}
				if(componentType==ComponentType.TreeList) {
					AtkComponentTreeList* ptr4=(AtkComponentTreeList*)component;
					for(int l=0;l<Math.Min(ptr4->Items.Count,200);l++) {
						AtkComponentTreeListItem* value=ptr4->Items[l].Value;
						if(value!=null) {
							report.Add($"{addon}: tree node={ptr->NodeId} item={l} state={value->State} type={value->Type} hidden={value->IsHidden} renderer=0x{(nuint)value->Renderer:X}");
							for(int m=0;m<Math.Min(value->UIntValues.Count,64);m++) {
								report.Add($"{addon}: tree item={l} uint[{m}]={value->UIntValues[m]}");
							}
							for(int n=0;n<Math.Min(value->StringValues.Count,64);n++) {
								report.Add($"{addon}: tree item={l} string[{n}]={value->StringValues[n]}");
							}
						}
					}
				}
			}
			(bool,bool) result=Visit(&component->UldManager,label,lists,activate,report,addon,addonListener,depth+1);
			flag|=result.Item1;
			if(result.Item2) {
				return result;
			}
		}
		return (found: flag,clicked: false);
	}
	private unsafe static bool DispatchListSelection(AtkResNode* node,AtkComponentListItemRenderer* renderer,int index,AtkEventListener* addonListener) {
		AtkEvent* ptr=node->AtkEventManager.Event;
		int num=0;
		while(ptr!=null&&num<128) {
			if(ptr->State.EventType==AtkEventType.ListItemClick&&ptr->Listener==addonListener) {
				AtkEvent atkEvent=*ptr;
				atkEvent.State.StateFlags&=~AtkEventStateFlags.Handled;
				atkEvent.State.ReturnFlags=0;
				AtkEventData atkEventData=new AtkEventData {
					ListItemData = {
						ListItemRenderer = renderer,
						SelectedIndex = index,
						HoveredItemIndex3 = (short)index
					}
				};
				addonListener->ReceiveEvent(AtkEventType.ListItemClick,(int)ptr->Param,&atkEvent,&atkEventData);
				return true;
			}
			num++;
			ptr=ptr->NextEvent;
		}
		return false;
	}
	public unsafe static bool ClickRowContaining(string addonName,string text) {
		if(string.IsNullOrWhiteSpace(addonName)||string.IsNullOrWhiteSpace(text)) {
			LastRowClick="no addon or text given";
			return false;
		}
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName).Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			LastRowClick=addonName+" is not open";
			return false;
		}
		rowFound=false;
		buttonFound=false;
		if(FindAndClickRow(&address->UldManager,text,(AtkEventListener*)address,0)) {
			LastRowClick="clicked the row for "+text;
			return true;
		}
		string lastRowClick;
		if(rowFound) {
			lastRowClick=((!buttonFound)?("found the row for "+text+" but no button inside it"):("found the button for "+text+" but the click was refused"));
		}
		else {
			lastRowClick="no row containing "+text;
		}
		LastRowClick=lastRowClick;
		return false;
	}
	private unsafe static bool FindAndClickRow(AtkUldManager* manager,string text,AtkEventListener* addonListener,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return false;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||!ptr->IsVisible()||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(FindAndClickRow(&component->UldManager,text,addonListener,depth+1)) {
				return true;
			}
			if(component->GetComponentType()!=ComponentType.ListItemRenderer||!SubtreeContains(&component->UldManager,text,0)) {
				continue;
			}
			rowFound=true;
			AtkComponentButton* ptr2=FindButton(&component->UldManager,0);
			if(ptr2!=null) {
				buttonFound=true;
				if(ClickButton(ptr2,addonListener)) {
					return true;
				}
			}
		}
		return false;
	}
	private unsafe static bool SubtreeContains(AtkUldManager* manager,string text,int depth) {
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
				if(component!=null&&SubtreeContains(&component->UldManager,text,depth+1)) {
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
	public unsafe static bool ConfirmYes() {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("SelectYesno").Address;
		if(address==null||!address->IsVisible||!address->IsReady) {
			LastYesNo="no dialog open";
			return false;
		}
		DateTime utcNow=DateTime.UtcNow;
		if((utcNow-lastYesUtc).TotalSeconds>5.0) {
			yesAttempts=0;
		}
		lastYesUtc=utcNow;
		yesAttempts++;
		if(yesAttempts>1&&ClickIn("SelectYesno","Yes")) {
			LastYesNo=$"pressed Yes (attempt {yesAttempts})";
			return true;
		}
		try {
			AtkValue* ptr=stackalloc AtkValue[1];
			ptr->Type=AtkValueType.Int;
			ptr->Int=0;
			address->FireCallback(1u,ptr,close: true);
			LastYesNo=$"answered Yes by callback (attempt {yesAttempts})";
			return true;
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not confirm the yes/no dialog.");
			LastYesNo="the yes/no callback threw - see the log";
			return false;
		}
	}
	private unsafe static bool ClickButton(AtkComponentButton* button,AtkEventListener* addonListener) {
		AtkResNode* ownerNode=(AtkResNode*)button->OwnerNode;
		if(ownerNode==null) {
			return false;
		}
		if(DispatchButtonAction(ownerNode,addonListener)) {
			return true;
		}
		AtkUldManager* ptr=&button->UldManager;
		if(ptr->NodeList!=null) {
			for(int i=0;i<Math.Min((int)ptr->NodeListCount,2048);i++) {
				AtkResNode* ptr2=ptr->NodeList[i];
				if(ptr2!=null&&ptr2->IsVisible()&&DispatchButtonAction(ptr2,addonListener)) {
					return true;
				}
			}
		}
		PrepareButtonHover(button);
		if(DispatchRegistered(ownerNode,AtkEventType.ButtonClick,null)) {
			return true;
		}
		if(DispatchRegistered(ownerNode,AtkEventType.MouseClick,(AtkEventListener*)button)) {
			return true;
		}
		AtkUldManager* ptr3=&button->UldManager;
		if(ptr3->NodeList==null) {
			return false;
		}
		for(int j=0;j<Math.Min((int)ptr3->NodeListCount,2048);j++) {
			AtkResNode* ptr4=ptr3->NodeList[j];
			if(ptr4!=null&&ptr4->IsVisible()&&DispatchRegistered(ptr4,AtkEventType.MouseClick,(AtkEventListener*)button)) {
				return true;
			}
		}
		return false;
	}
	private unsafe static bool DispatchButtonAction(AtkResNode* node,AtkEventListener* addonListener) {
		if(!DispatchRegistered(node,AtkEventType.ButtonClick,addonListener)) {
			return DispatchRegistered(node,AtkEventType.MouseClick,addonListener);
		}
		return true;
	}
	private unsafe static void PrepareButtonHover(AtkComponentButton* button) {
		DispatchRegistered((AtkResNode*)button->OwnerNode,AtkEventType.MouseOver,(AtkEventListener*)button);
		AtkUldManager* ptr=&button->UldManager;
		if(ptr->NodeList==null) {
			return;
		}
		for(int i=0;i<Math.Min((int)ptr->NodeListCount,2048);i++) {
			AtkResNode* ptr2=ptr->NodeList[i];
			if(ptr2!=null&&ptr2->IsVisible()) {
				DispatchRegistered(ptr2,AtkEventType.MouseOver,(AtkEventListener*)button);
			}
		}
	}
	private unsafe static bool DispatchRegistered(AtkResNode* node,AtkEventType type,AtkEventListener* requiredListener) {
		if(node==null) {
			return false;
		}
		AtkEvent* ptr=node->AtkEventManager.Event;
		int num=0;
		while(ptr!=null&&num<128) {
			if(ptr->State.EventType==type&&ptr->Listener!=null&&(requiredListener==null||ptr->Listener==requiredListener)) {
				AtkEvent atkEvent=*ptr;
				atkEvent.State.StateFlags&=~AtkEventStateFlags.Handled;
				atkEvent.State.ReturnFlags=0;
				AtkEventData atkEventData=new AtkEventData {
					MouseData = {
						PosX = (short)Math.Clamp(node->ScreenX+(float)(int)node->Width*node->GetScaleX()/2f,-32768f,32767f),
						PosY = (short)Math.Clamp(node->ScreenY+(float)(int)node->Height*node->GetScaleY()/2f,-32768f,32767f)
					}
				};
				ptr->Listener->ReceiveEvent(type,(int)ptr->Param,&atkEvent,&atkEventData);
				return true;
			}
			num++;
			ptr=ptr->NextEvent;
		}
		return false;
	}
	private unsafe static void DescribeEvents(AtkResNode* node,List<string> report,string addon) {
		if(node!=null) {
			AtkEvent* ptr=node->AtkEventManager.Event;
			int num=0;
			while(ptr!=null&&num<128) {
				report.Add($"{addon}: node {node->NodeId} event {ptr->State.EventType} parameter {ptr->Param} listener=0x{(nuint)ptr->Listener:X} target=0x{(nuint)ptr->Target:X} eventNode=0x{(nuint)ptr->Node:X} state={ptr->State.StateFlags}");
				num++;
				ptr=ptr->NextEvent;
			}
		}
	}
}
