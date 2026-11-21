using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace Beastmaster;

public static class CrucibleHotbarDiagnostics {
	public unsafe static string Describe() {
		RaptureHotbarModule* ptr=RaptureHotbarModule.Instance();
		if(ptr==null||!ptr->ModuleReady) {
			return "Hotbars not ready.";
		}
		StringBuilder stringBuilder=new StringBuilder("Active hotbars; bar/slot numbers are one-based. Icon matches are diagnostic candidates, not proof of command identity.\n");
		for(int i=0;i<ptr->Hotbars.Length;i++) {
			for(uint num=0u;num<16;num++) {
				RaptureHotbarModule.HotbarSlot* hotbarSlot=ptr->Hotbars[i].GetHotbarSlot(num);
				if(hotbarSlot!=null&&hotbarSlot->CommandType!=RaptureHotbarModule.HotbarSlotType.Empty) {
					StringBuilder stringBuilder2=stringBuilder;
					StringBuilder.AppendInterpolatedStringHandler handler=new StringBuilder.AppendInterpolatedStringHandler(75,9,stringBuilder2);
					handler.AppendLiteral("bar=");
					handler.AppendFormatted(i+1);
					handler.AppendLiteral(" slot=");
					handler.AppendFormatted(num+1);
					handler.AppendLiteral(" commandType=");
					handler.AppendFormatted((byte)hotbarSlot->CommandType);
					handler.AppendLiteral(" (");
					handler.AppendFormatted(hotbarSlot->CommandType);
					handler.AppendLiteral(") commandId=");
					handler.AppendFormatted(hotbarSlot->CommandId);
					handler.AppendLiteral(" apparentType=");
					handler.AppendFormatted((byte)hotbarSlot->ApparentSlotType);
					handler.AppendLiteral(" apparentId=");
					handler.AppendFormatted(hotbarSlot->ApparentActionId);
					handler.AppendLiteral(" icon=");
					handler.AppendFormatted(hotbarSlot->IconId);
					handler.AppendLiteral(" cost=");
					handler.AppendFormatted(hotbarSlot->CostValue);
					stringBuilder2.AppendLine(ref handler);
				}
			}
		}
		return stringBuilder.ToString();
	}
}
