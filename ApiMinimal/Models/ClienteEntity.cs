using System.ComponentModel.DataAnnotations;

namespace ApiMinimal.Models
{
    public class ClienteEntity
    {
        [Key]
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Codigo { get; set;}
        public bool? Novo { get; set;}

    }
}
