using System.Threading.Tasks;

namespace BMM.Core.GuardedActions.Base.Interfaces
{
    public interface IDataContextGuardedAction<TDataContext> : IBaseGuardedAction
    {
        Task ExecuteGuarded();
    }
}