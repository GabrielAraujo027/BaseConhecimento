namespace BaseConhecimento.DTOs.Common
{
    public class EnumValue<T> where T : Enum
    {
        public int Id{ get; set; }
        public string Descricao { get; set; }
    }
}
