using System.Linq;
namespace Beastmaster;

public sealed record CrucibleBestiaryEntry(int CatalogueId,bool Selected,uint? ClickParam) {
	public string Name => BeastmasterCatalog.Entries.FirstOrDefault((BeastmasterCatalogEntry e) => e.Number==CatalogueId)?.DisplayName ?? $"No. {CatalogueId}";
}
