using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonogameTestbed
{
    interface IGraphicsTest
    {
        bool Initialized { get; }

        void Init(MonoTestbed window);

        void Update();

        void Draw(MonoTestbed window);
    }
}
