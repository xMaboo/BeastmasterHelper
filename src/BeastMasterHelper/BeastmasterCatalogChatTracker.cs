using System;
using System.Linq;
namespace Beastmaster;

public sealed class BeastmasterCatalogChatTracker : IDisposable {
	private const uint PactForgedLogMessage=11400u;
	private static readonly char[] NameTrim=new char[6] { '“', '”', '"', '\'', ' ', '\t' };
	private readonly BeastmasterConfiguration configuration;
	private readonly BeastmasterProgressService progressService;
	public BeastmasterCatalogChatTracker(BeastmasterConfiguration configuration,BeastmasterProgressService progressService) {
		this.configuration=configuration;
		this.progressService=progressService;
		DalamudApi.ChatGui.ChatMessage+=OnChatMessage;
	}
	public void Dispose() {
		DalamudApi.ChatGui.ChatMessage-=OnChatMessage;
	}
	private void OnChatMessage(object message) {
		if(!configuration.AutoCompleteCatalogFromChat) {
			return;
		}
		try {
			string text=ExtractChatMessageText(message);
			if(string.IsNullOrWhiteSpace(text)) {
				return;
			}
			string name=ExtractPactName(text);
			if(name!=null) {
				BeastmasterCatalogEntry beastmasterCatalogEntry=BeastmasterCatalog.Entries.FirstOrDefault((BeastmasterCatalogEntry candidate) => string.Equals(candidate.DisplayName,name,StringComparison.OrdinalIgnoreCase));
				if(beastmasterCatalogEntry==null) {
					DalamudApi.Log.Warning("Pact forged for \"{Name}\", which matches no catalogue entry.",name);
				}
				else if(!progressService.IsCompleted(beastmasterCatalogEntry.Key)) {
					progressService.SetCompleted(beastmasterCatalogEntry.Key,completed: true);
					DalamudApi.Log.Information("Marked catalogue entry {Number} ({Name}) complete - pact forged.",beastmasterCatalogEntry.Number,beastmasterCatalogEntry.DisplayName);
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Failed to inspect a chat message for a forged pact.");
		}
	}
	private static string? ExtractPactName(string message) {
		string text=BeastmasterGameData.LogMessage(11400u);
		if(string.IsNullOrWhiteSpace(text)) {
			return null;
		}
		int i;
		for(i=0;i<text.Length&&i<message.Length&&text[i]==message[i];i++) {
		}
		int j;
		for(j=0;j<text.Length-i&&j<message.Length-i&&text[text.Length-1-j]==message[message.Length-1-j];j++) {
		}
		if(i+j<text.Length) {
			return null;
		}
		int num=i;
		string text2=message.Substring(num,message.Length-j-num).Trim(NameTrim);
		if(!string.IsNullOrWhiteSpace(text2)) {
			return text2;
		}
		return null;
	}
	private static string ExtractChatMessageText(object message) {
		try {
			object obj=message.GetType().GetProperty("Message")?.GetValue(message);
			return ((obj?.GetType().GetProperty("TextValue"))?.GetValue(obj) as string) ?? obj?.ToString() ?? message.ToString() ?? string.Empty;
		}
		catch {
			return message.ToString() ?? string.Empty;
		}
	}
}
