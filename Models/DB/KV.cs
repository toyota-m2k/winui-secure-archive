using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Models.DB;

public class KV {
    [Key, Required]
    public string Key { get; set; } = string.Empty;
    public string? sValue { get; set; }
    public int iValue { get; set; }
}
