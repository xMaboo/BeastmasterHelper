using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CrucibleShopReader {
	private const int FirstValue=3;
	private const int Stride=5;
	private const int MaxRows=16;
	public unsafe static bool IsOpen => Shop()!=null;
	public unsafe static int Tokens {
		get {
			AtkUnitBase* ptr=Shop();
			if(ptr==null||ptr->AtkValuesCount<2) {
				return -1;
			}
			return ParseNumber(Text(ptr,1));
		}
	}
	public static string LastRead { get; private set; }="not attempted";
	private unsafe static AtkUnitBase* Shop() {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("XBMContentsItemShop").Address;
		if(address==null||!address->IsVisible||!address->IsReady||address->AtkValues==null) {
			return null;
		}
		return address;
	}
	public unsafe static List<CrucibleShopOffer> Offers() {
		List<CrucibleShopOffer> list=new List<CrucibleShopOffer>();
		AtkUnitBase* ptr=Shop();
		if(ptr==null) {
			LastRead="the shop is not open";
			return list;
		}
		int num=CrucibleListClick.RowCount("XBMContentsItemShop");
		try {
			for(int i=0;i<16;i++) {
				int num2=3+i*5;
				if(num2+3>=ptr->AtkValuesCount) {
					break;
				}
				if(Bool(ptr,num2)) {
					CrucibleItem crucibleItem=CrucibleItemCatalogue.Get((int)UInt(ptr,num2+1));
					if(!(crucibleItem==null)) {
						list.Add(new CrucibleShopOffer(i,crucibleItem,ParseNumber(Text(ptr,num2+2)),Bool(ptr,num2+3)));
					}
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the Crucible shop offers.");
		}
		LastRead=((num>0&&list.Count<num)?$"read {list.Count} of {num} row(s) - the shelf may not be fully loaded":($"read {list.Count} row(s)"+((num>0)?$" of {num}":string.Empty)));
		return list;
	}
	private static int ParseNumber(string text) {
		if(string.IsNullOrWhiteSpace(text)) {
			return -1;
		}
		StringBuilder stringBuilder=new StringBuilder();
		foreach(char c in text) {
			if(char.IsDigit(c)) {
				stringBuilder.Append(c);
			}
			else if((c!=' '&&c!=',')||1==0) {
				break;
			}
		}
		if(stringBuilder.Length<=0||!int.TryParse(stringBuilder.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var result)) {
			return -1;
		}
		return result;
	}
	private unsafe static string Text(AtkUnitBase* unit,int index) {
		if(unit==null||index<0||index>=unit->AtkValuesCount) {
			return string.Empty;
		}
		AtkValue atkValue=unit->AtkValues[index];
		AtkValueType atkValueType=atkValue.Type&AtkValueType.TypeMask;
		if((atkValueType!=AtkValueType.String&&atkValueType!=AtkValueType.ConstString)||1==0) {
			return string.Empty;
		}
		return atkValue.String.ToString();
	}
	private unsafe static uint UInt(AtkUnitBase* unit,int index) {
		if(unit==null||index<0||index>=unit->AtkValuesCount) {
			return 0u;
		}
		AtkValue atkValue=unit->AtkValues[index];
		return (atkValue.Type&AtkValueType.TypeMask) switch {
			AtkValueType.UInt => atkValue.UInt,
			AtkValueType.Int => (uint)Math.Max(0,atkValue.Int),
			_ => 0u,
		};
	}
	private unsafe static bool Bool(AtkUnitBase* unit,int index) {
		if(unit==null||index<0||index>=unit->AtkValuesCount) {
			return false;
		}
		AtkValue atkValue=unit->AtkValues[index];
		if((atkValue.Type&AtkValueType.TypeMask)==AtkValueType.Bool) {
			return atkValue.Bool;
		}
		return false;
	}
}
