namespace Desktop.Services;

public interface IScriptFileSaveService
{
    bool Save(string script, string suggestedFileName);
}
