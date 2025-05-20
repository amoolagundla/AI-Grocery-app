using OCR_AI_Grocery.Models;
using System.Threading.Tasks;

public interface IPredictionsRepository
{
    Task SavePrediction(PredictionDocument prediction);
    Task<PredictionDocument?> GetLatestPredictionByUserEmail(string userEmail);
}