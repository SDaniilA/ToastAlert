using System.Threading.Tasks;

namespace ToastAlert.Services
{
    public interface IEasterEgg
    {
        string Command { get; }
        string Name { get; }
        string Description { get; }
        bool IsActive { get; }

        Task ActivateAsync(EasterEggContext context);
        Task DeactivateAsync();
        object GetDefaultConfig();   // возвращает объект с настройками по умолчанию (тип EasterEggConfig)
    }
}