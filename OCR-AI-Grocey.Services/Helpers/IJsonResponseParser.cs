
namespace OCR_AI_Grocey.Services.Helpers
{
    public interface IJsonResponseParser
    {
        Dictionary<string, StoreData> ParseOpenAIResponse(string responseString);
        Dictionary<string, List<TimeSeriesDataPoint>> ParseOpenAIResponseForTimeSeries(string responseString);
    }
}