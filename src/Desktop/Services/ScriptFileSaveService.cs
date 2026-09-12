using System.Text;
using System.IO;
using Microsoft.Win32;

namespace Desktop.Services;

public sealed class ScriptFileSaveService : IScriptFileSaveService
{
    public bool Save(string script, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Guardar script SQL",
            Filter = "Script SQL (*.sql)|*.sql",
            FileName = suggestedFileName
        };

        if (dialog.ShowDialog() != true)
            return false;

        File.WriteAllText(dialog.FileName, script, Encoding.UTF8);
        return true;
    }
}
