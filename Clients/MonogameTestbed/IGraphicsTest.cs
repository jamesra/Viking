namespace MonogameTestbed
{
    interface IGraphicsTest
    {
        string Title { get; }
        bool Initialized { get; }

        void Init(MonoTestbed window);

        void Update();

        void Draw(MonoTestbed window);

        /// <summary>
        /// Called once at the end of the test
        /// </summary>
        /// <returns></returns>
        void UnloadContent(MonoTestbed window);
    }
}
