using System;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
namespace Beastmaster;

public static class GameCommandService {
	public unsafe static bool Execute(string command) {
		if(!command.StartsWith("/",StringComparison.Ordinal)) {
			return false;
		}
		byte[] bytes=Encoding.UTF8.GetBytes(command);
		int num=bytes.Length;
		bool flag=((num>500||num==0)?true:false);
		if(flag) {
			return false;
		}
		UIModule* ptr=UIModule.Instance();
		if(ptr==null) {
			return false;
		}
		Utf8String* ptr2=Utf8String.FromSequence(bytes);
		try {
			ptr->ProcessChatBoxEntry(ptr2);
			flag=true;
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Failed to execute game command {Command}.",command);
			flag=false;
		}
		finally {
			ptr2->Dtor(free: true);
		}
		return flag;
	}
}
