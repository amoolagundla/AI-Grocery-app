namespace OCR_AI_Grocery.services
{
    public interface ICleanJsonResponseHelper
    {
        T CleanAndParseJson<T>(string jsonResponse) where T : new();
        string CleanJsonResponse(string response);
    }
}