using System;
using System.Windows;
using Velopack;

namespace Jotunn
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();

            App app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
