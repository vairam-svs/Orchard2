using System.Threading.Tasks;

namespace Orchard.Environment.Recipes.Services
{
    public interface IRecipeScheduler
    {
        Task ScheduleWork(string executionId);
    }
}