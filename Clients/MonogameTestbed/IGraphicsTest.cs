using System.Threading.Tasks;

namespace MonogameTestbed
{
    interface IGraphicsTest
    {
        string Title { get; }
        bool Initialized { get; }

        Task Init(MonoTestbed window);

        void Update();

        void Draw(MonoTestbed window);

        /// <summary>
        /// Called once at the end of the test
        /// </summary>
        /// <returns></returns>
        void UnloadContent(MonoTestbed window);
    }
}
