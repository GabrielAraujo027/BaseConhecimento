namespace BaseConhecimento.DTOs.Knowledge;

public class IngestBatchResultDTO
{
    public int Inseridos { get; set; }
    public List<int> Ids { get; set; } = new();
    public List<string> Erros { get; set; } = new();
}
