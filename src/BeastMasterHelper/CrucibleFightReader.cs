using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CrucibleFightReader {
	public unsafe static bool WindowsOpen {
		get {
			if(Window("XBMStageDetailList")!=null) {
				return Window("XBMPetParty")!=null;
			}
			return false;
		}
	}
	private unsafe static AtkUnitBase* Window(string name) {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(name).Address;
		if(address==null||!address->IsVisible||!address->IsReady||address->AtkValues==null) {
			return null;
		}
		return address;
	}
	private unsafe static string Text(AtkUnitBase* unit,int i) {
		if(unit==null||i<0||i>=unit->AtkValuesCount) {
			return "";
		}
		AtkValue atkValue=unit->AtkValues[i];
		AtkValueType atkValueType=atkValue.Type&AtkValueType.TypeMask;
		if((atkValueType!=AtkValueType.String&&atkValueType!=AtkValueType.ConstString)||1==0) {
			return "";
		}
		return atkValue.String.ToString();
	}
	private unsafe static uint? Number(AtkUnitBase* unit,int i) {
		if(unit==null||i<0||i>=unit->AtkValuesCount) {
			return null;
		}
		AtkValue atkValue=unit->AtkValues[i];
		if((atkValue.Type&AtkValueType.TypeMask)!=AtkValueType.UInt) {
			return null;
		}
		return atkValue.UInt;
	}
	public unsafe static CrucibleFightState Read() {
		AtkUnitBase* ptr=Window("XBMStageDetailList");
		AtkUnitBase* ptr2=Window("XBMPetParty");
		if(ptr==null||ptr2==null) {
			return Error("Waiting for Board Layout and Team Composition.");
		}
		string text=Text(ptr,57);
		if(!CrucibleBoardGuide.LooksLikeEnemy(text)) {
			for(int i=0;i<ptr->AtkValuesCount;i++) {
				string text2=Text(ptr,i);
				if(text2.Length>0&&CrucibleBoardGuide.IsKnownEnemy(text2)) {
					text=text2;
					break;
				}
			}
		}
		string text3=(Text(ptr,61).Contains("Weakness",StringComparison.OrdinalIgnoreCase)?Text(ptr,62):"");
		uint? num=Number(ptr2,5);
		bool flag;
		switch(num) {
		default:
		flag=true;
		break;
		case 1u:
		case 2u:
		case 3u:
		case 4u:
		case 5u:
		case 6u:
		case 7u:
		case 8u:
		case 9u:
		case 10u:
		case 11u:
		case 12u:
		case 13u:
		case 14u:
		case 15u:
		case 16u:
		case 17u:
		case 18u:
		case 19u:
		case 20u:
		case 21u:
		case 22u:
		case 23u:
		case 24u:
		case 25u:
		case 26u:
		case 27u:
		case 28u:
		case 29u:
		case 30u:
		case 31u:
		case 32u:
		case 33u:
		case 34u:
		case 35u:
		case 36u:
		case 37u:
		case 38u:
		case 39u:
		case 40u:
		case 41u:
		case 42u:
		case 43u:
		case 44u:
		case 45u:
		case 46u:
		case 47u:
		case 48u:
		case 49u:
		case 50u:
		flag=false;
		break;
		}
		if(flag||82+(num.Value-1)*77>=ptr2->AtkValuesCount) {
			return Error("Familiar list layout is not recognised.");
		}
		List<CrucibleFamiliar> list=new List<CrucibleFamiliar>();
		int num2=0;
		FamiliarCombatProfile value=default;
		for(int j=0;j<num.Value;j++) {
			int num3=j*77;
			uint? num4=Number(ptr2,82+num3);
			uint? num5=Number(ptr2,7+num3);
			uint? num6=Number(ptr2,80+num3);
			uint? num7=Number(ptr2,11+num3);
			uint? num8=Number(ptr2,12+num3);
			string text4=Text(ptr2,9+num3);
			flag=!num4.HasValue||!FamiliarCombatData.All.TryGetValue((int)num4.Value,out value)||num5!=242000+num4;
			if(!flag) {
				bool flag2=((!num6.HasValue||num6.GetValueOrDefault()>3)?true:false);
				flag=flag2;
			}
			if(flag||!num7.HasValue||!num8.HasValue||num7>num8||!text4.Equals(value.Name,StringComparison.OrdinalIgnoreCase)) {
				num2++;
			}
			else {
				list.Add(new CrucibleFamiliar(j,(int)num4.Value,text4,num7.Value,num8.Value,(int)num6.Value,value));
			}
		}
		if(list.Count==0) {
			return Error($"None of the {num.Value} familiar rows matches the verified layout (still updating?).");
		}
		CrucibleFamiliar[] array=list.Where((CrucibleFamiliar f) => f.Slot<3).ToArray();
		if(array.Select((CrucibleFamiliar f) => f.Slot).Distinct().Count()!=array.Length) {
			return Error("Team slots are updating.");
		}
		return new CrucibleFightState(text,text3,CrucibleTeamPlanner.WeaknessTypes(text3),list,"");
		static CrucibleFightState Error(string message) {
			return new CrucibleFightState("","",Array.Empty<string>(),Array.Empty<CrucibleFamiliar>(),message);
		}
	}
	public unsafe static bool Commence() {
		AtkUnitBase* ptr=Window("XBMStageDetailList");
		if(ptr!=null) {
			return CommenceNode(ptr,&ptr->UldManager,0);
		}
		return false;
	}
	private unsafe static bool CommenceNode(AtkUnitBase* addon,AtkUldManager* manager,int depth) {
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
			AtkComponentButton* asAtkComponentButton=ptr->GetAsAtkComponentButton();
			if(ptr->NodeId==33&&asAtkComponentButton!=null&&asAtkComponentButton->IsEnabled&&asAtkComponentButton->ButtonTextNode!=null&&asAtkComponentButton->ButtonTextNode->NodeText.ToString()=="Commence Battle") {
				AtkEvent* ptr2=ptr->AtkEventManager.Event;
				int num=0;
				while(ptr2!=null&&num<128) {
					if(ptr2->State.EventType==AtkEventType.ButtonClick&&ptr2->Param==9&&ptr2->Listener==addon) {
						AtkEvent atkEvent=*ptr2;
						atkEvent.Node=null;
						atkEvent.State.StateFlags=AtkEventStateFlags.Unk3;
						atkEvent.State.ReturnFlags=0;
						AtkEventData atkEventData=default;
						((AtkEventListener*)addon)->ReceiveEvent(AtkEventType.ButtonClick,9,&atkEvent,&atkEventData);
						return true;
					}
					num++;
					ptr2=ptr2->NextEvent;
				}
			}
			if(CommenceNode(addon,&component->UldManager,depth+1)) {
				return true;
			}
		}
		return false;
	}
	public unsafe static bool ClickFamiliarRow(int row) {
		AtkUnitBase* ptr=Window("XBMPetParty");
		if(ptr!=null&&row>=0) {
			return ClickRow(ptr,&ptr->UldManager,row,0);
		}
		return false;
	}
	public unsafe static bool ClickFamiliar(int catalogueId) {
		CrucibleFightState crucibleFightState=Read();
		if(!crucibleFightState.Valid) {
			return false;
		}
		CrucibleFamiliar crucibleFamiliar=crucibleFightState.Familiars.SingleOrDefault((CrucibleFamiliar f) => f.CatalogueId==catalogueId);
		AtkUnitBase* ptr=Window("XBMPetParty");
		if(crucibleFamiliar!=null&&ptr!=null) {
			return ClickRow(ptr,&ptr->UldManager,crucibleFamiliar.Row,0);
		}
		return false;
	}
	private unsafe static bool ClickRow(AtkUnitBase* addon,AtkUldManager* manager,int row,int depth) {
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
			if(component->GetComponentType()==ComponentType.List) {
				AtkComponentList* ptr2=(AtkComponentList*)component;
				AtkEvent* ptr3=ptr->AtkEventManager.Event;
				int num=0;
				while(ptr3!=null&&num<128) {
					if(ptr3->State.EventType==AtkEventType.ListItemClick&&ptr3->Param==0&&ptr3->Listener==addon) {
						if(row>=ptr2->ListLength||!ptr2->IsItemInteractionEnabled||ptr2->GetItemDisabledState(row)) {
							return false;
						}
						AtkComponentListItemRenderer* itemRenderer=ptr2->GetItemRenderer(row);
						if(itemRenderer==null||!itemRenderer->IsEnabled) {
							return false;
						}
						AtkEvent atkEvent=*ptr3;
						atkEvent.Node=null;
						atkEvent.State.StateFlags=AtkEventStateFlags.Unk3;
						atkEvent.State.ReturnFlags=0;
						AtkEventData atkEventData=new AtkEventData {
							ListItemData = {
								ListItemRenderer = itemRenderer,
								SelectedIndex = row,
								HoveredItemIndex3 = (short)row
							}
						};
						((AtkEventListener*)addon)->ReceiveEvent(AtkEventType.ListItemClick,0,&atkEvent,&atkEventData);
						return true;
					}
					num++;
					ptr3=ptr3->NextEvent;
				}
			}
			if(ClickRow(addon,&component->UldManager,row,depth+1)) {
				return true;
			}
		}
		return false;
	}
}
