using System.Threading.Tasks;

namespace AbsoluteZero.Core.Session
{
    public interface ISceneTransitionService
    {
        void LoadTitleScene();
    }

    public interface IAsyncSceneTransitionService : ISceneTransitionService
    {
        Task LoadTitleSceneAsync();
    }
}
