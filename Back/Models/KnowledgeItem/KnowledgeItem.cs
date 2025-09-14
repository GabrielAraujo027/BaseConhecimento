namespace BaseConhecimento.Models.Knowledge;

public class KnowledgeItem
{
    public int Id { get; set; }

    public string Categoria { get; set; } = string.Empty;

    public string Subcategoria { get; set; } = string.Empty;

    public string Conteudo { get; set; } = string.Empty;
    public string PerguntasFrequentes { get; set; } = string.Empty;

    // Para simplificar: salva o embedding como JSON (string)
    public string EmbeddingJson { get; set; } = string.Empty;

    // Se quiser acessar como array em memória
    public float[] GetEmbedding()
    {
        if (string.IsNullOrEmpty(EmbeddingJson)) return Array.Empty<float>();
        return System.Text.Json.JsonSerializer.Deserialize<float[]>(EmbeddingJson) ?? Array.Empty<float>();
    }

    public void SetEmbedding(float[] vector)
    {
        EmbeddingJson = System.Text.Json.JsonSerializer.Serialize(vector);
    }
}
