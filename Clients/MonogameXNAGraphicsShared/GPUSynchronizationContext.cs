using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    /// <summary>
    /// TaskScheduler that runs tasks by posting to a SynchronizationContext.
    /// Used when TaskScheduler.FromCurrentSynchronizationContext() throws (e.g. on .NET Core with default context).
    /// </summary>
    internal sealed class SynchronizationContextTaskScheduler : TaskScheduler
    {
        private readonly SynchronizationContext _context;

        internal SynchronizationContextTaskScheduler(SynchronizationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        protected override void QueueTask(Task task)
        {
            _context.Post(_ => TryExecuteTask(task), null);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (SynchronizationContext.Current == _context)
                return TryExecuteTask(task);
            return false;
        }

        protected override IEnumerable<Task> GetScheduledTasks() => Array.Empty<Task>();
    }

    /// <summary>
    /// This class records the thread the GraphicsDeviceManager was created upon.  Initialization should be called 
    /// right after the GraphicsDeviceManager constructor.
    /// </summary>
    public static class GpuSynchronizationManager
    {
        public static SynchronizationContext Context { get; private set; }
        public static TaskScheduler Scheduler { get; private set; }

        public static void Initialize()
        {
            // Capture the current SynchronizationContext
            Context = SynchronizationContext.Current ?? new SynchronizationContext();
            try
            {
                Scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            }
            catch (InvalidOperationException)
            {
                // Some SynchronizationContexts (e.g. default on .NET Core) cannot be used as TaskScheduler.
                // Fall back to a scheduler that runs tasks by posting to the captured context.
                Scheduler = new SynchronizationContextTaskScheduler(Context);
            }
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

        public static void Initialize() =>
            // Capture the current SynchronizationContext
            GpuSynchronizationManager.Initialize();

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
