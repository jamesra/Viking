using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System.Threading;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    /// <summary>
    /// This class records the thread the GraphicsDeviceManager was created upon.  Initialization should be called 
    /// right after the GraphicsDeviceManager constructor.
    /// </summary>
    public static class GpuSynchronizationManager
    {
        public static SynchronizationContext Context { get; private set; }
        public static TaskScheduler Scheduler { get; private set;}
        
        public static void Initialize()
        {
            // Capture the current SynchronizationContext
            Context = SynchronizationContext.Current ?? new SynchronizationContext();
            Scheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }
        
        public static void Post(Action action)
        {
            if (Context is null)
            {
                throw new InvalidOperationException("GameSynchronizationContext has not been initialized.  Initialization should occur on the game thread, ideally the line after the GraphicsDevice is created");
            }
            // Post the action to the captured SynchronizationContext
            Context.Post(_ => action(), null); 
        }

        public static Task RunTask(Action action) => RunTask(action, CancellationToken.None);

        public static Task RunTask(Action action, CancellationToken token)
        {
            if (Scheduler is null)
            {
                throw new InvalidOperationException("GameSynchronizationContext has not been initialized.  Initialization should occur on the game thread, ideally the line after the GraphicsDevice is created");
            }
            
            return Task.Factory.StartNew(action, token, TaskCreationOptions.None, Scheduler);
        }
    }

    /// <summary>
    /// Typed methods for Task Scheduling.  Calling Initialize on any template initializes the GpuSynchronizationManager for every template and the GpuSynchronizationManager.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class GpuSynchronizationManager<T>
    {
        public static SynchronizationContext Context => GpuSynchronizationManager.Context;
        public static TaskScheduler Scheduler => GpuSynchronizationManager.Scheduler;

        public static void Initialize()
        {
            // Capture the current SynchronizationContext
            GpuSynchronizationManager.Initialize();
        }

        public static void Post(Action action)
        {
            if (Context is null)
            {
                throw new InvalidOperationException("GameSynchronizationContext has not been initialized.  Initialization should occur on the game thread, ideally the line after the GraphicsDevice is created");
            }
            // Post the action to the captured SynchronizationContext
            Context.Post(_ => action(), null);
        }

        public static Task<T> RunTask(Func<T> func) => RunTask(func, CancellationToken.None);

        public static Task<T> RunTask(Func<T> func, CancellationToken token)
        {
            if (Scheduler is null)
            {
                throw new InvalidOperationException("GameSynchronizationContext has not been initialized.  Initialization should occur on the game thread, ideally the line after the GraphicsDevice is created");
            }

            return Task<T>.Factory.StartNew(func, token, TaskCreationOptions.None, Scheduler);
        }
    }
}
