using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Geometry;
using System.Windows; 
using Viking.Common;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace Viking.UI.Commands
{
    /// <summary>
    /// An entry either contains an existing command object or the type and constructor parameters to create a new command
    /// </summary>
    public struct CommandQueueEntry 
    {
        public readonly System.Type CommandType;
        public readonly Object[] Args;
        public readonly Command commandObj;

        public CommandQueueEntry(System.Type type, Object[] args)
        {
            this.CommandType = type; 
            this.Args = args;
            this.commandObj = null; 
        }

        public CommandQueueEntry(Command command)
        {
            this.CommandType = null; 
            this.Args = null;
            this.commandObj = command;
            
        }
    }

    public class CommandInjectedEventHandler : System.EventArgs
    {
        public Command injectedCommand;
        public bool SaveCurrentCommand;

        public CommandInjectedEventHandler(Command injectedCommand, bool SaveCurrentCommand)
        {
            this.injectedCommand = injectedCommand;
            this.SaveCurrentCommand = SaveCurrentCommand;
        }
    }


    public class CommandQueue
    {
        private Queue<CommandQueueEntry> _CommandQueue = new Queue<CommandQueueEntry>();

        public System.Collections.Specialized.NotifyCollectionChangedEventHandler OnQueueChanged;
        public delegate void CommandInjectedHandler(object sender, CommandInjectedEventHandler e);

        public event CommandInjectedHandler OnCommandInjected;
                
        public void EnqueueCommand(System.Type CommandType)
        {
            EnqueueCommand(CommandType, new Object[] { Viking.UI.State.ViewerControl });
            OnQueueChanged(this, new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Add, _CommandQueue.Peek(), 0));
        }

        /// <summary>
        /// We enqueue commands that we want to run immediately after completing the current command
        /// If the default command is active, then set the passed command as the current command, otherwise add to queue.
        /// </summary>
        /// <param name="CommandType"></param>
        /// <param name="Args"></param>
        public void EnqueueCommand(System.Type CommandType, Object[] Args)
        {
            CommandQueueEntry entry = new CommandQueueEntry(CommandType, Args);
            _CommandQueue.Enqueue(entry);
            OnQueueChanged(this, new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Add, entry));
        }

        /// <summary>
        /// Replace our current command with a new one.  By default the existing command will return to being the current command when the new command executes.
        /// </summary>
        /// <param name="replacementCommand"></param>
        /// <param name="SaveCurrentCommand"></param>
        public void InjectCommand(Command replacementCommand, bool SaveCurrentCommand = true)
        {
            OnCommandInjected(this, new CommandInjectedEventHandler(replacementCommand, SaveCurrentCommand)); 
        }

        public void ClearQueue()
        {
            _CommandQueue.Clear();
        }

        public int QueueDepth
        {
            get { return _CommandQueue.Count; }
        }


        /// <summary>
        /// Pop the next command of the queue.  If the queue is empty, return the default command
        /// </summary>
        /// <returns></returns>
        public Command Pop()
        {
            Command newCommand = null;

            //Check if there is a command in the queue
            if (_CommandQueue.Count != 0)
            {
                CommandQueueEntry nextCommand = _CommandQueue.Dequeue();

                if (nextCommand.commandObj != null)
                    newCommand = nextCommand.commandObj;
                else
                    newCommand = Activator.CreateInstance(nextCommand.CommandType, nextCommand.Args) as Command;
            }

            return newCommand;
        }

        /// <summary>
        /// Push the passed command to the front of the queue
        /// </summary>
        /// <returns></returns>
        public void Push(Command command)
        {
            List<CommandQueueEntry> existingQueue = new List<CommandQueueEntry>(_CommandQueue.ToArray());
            existingQueue.Insert(0, new CommandQueueEntry(command)); 
            _CommandQueue.Clear();
            foreach (CommandQueueEntry e in existingQueue)
            {
                _CommandQueue.Enqueue(e);
            }
        }

        /*
        /// <summary>
        /// OK, the normal state of the UI is that the default command is active.  When the current command dies we 
        /// check for two things
        /// 1) Are there any commands queued to become active when the current command is dead
        /// 2) Are there any commands registered to handle the current selected object
        /// 3) If not, we activate the default command
        /// </summary>
        /// <param name="Parent"></param>
        /// <param name="Obj"></param>
        /// <returns></returns>
        public Command CreateFor(Viking.UI.Controls.SectionViewerControl Parent, object Obj)
        {
            Command newCommand = null;

            //Check if there is a command in the queue
            if (_CommandQueue.Count != 0)
            {
                CommandQueueEntry nextCommand = _CommandQueue.Dequeue();

                if (nextCommand.commandObj != null)
                    newCommand = nextCommand.commandObj;
                else
                    newCommand = Activator.CreateInstance(nextCommand.CommandType, nextCommand.Args) as Command;
            }

            if (Obj != null && newCommand == null)
            {
                System.Type[] Commands = Viking.Common.ExtensionManager.GetCommandsForType(Obj.GetType());

                //TODO: Figure out how to handle multiple commands
                if (Commands.Length > 0)
                {
                    newCommand = Activator.CreateInstance(Commands[0], new object[] { Parent }) as Command;
                }
            }

            if (newCommand == null)
            {
                newCommand = new DefaultCommand(Parent);
            }



            return newCommand;
        }
        */
    }

    public abstract class Command : DependencyObject
    {
        protected Viking.UI.Controls.SectionViewerControl Parent; //Control the command is listening to

        protected MouseEventArgs oldMouse = null;

        /*
        public static readonly DependencyProperty HelpStringsProperty;

        public string[] HelpStrings
        {
            get { return (string[])GetValue(Command.HelpStringsProperty); }
            set { SetValue(Command.HelpStringsProperty, value); }
        }

        static Command()
        {
            Command.HelpStringsProperty = DependencyProperty.Register("HelpStringsProperty",
                typeof(string[]),
                typeof(Command),
                new FrameworkPropertyMetadata(Command.AllDefaultHelpStrings));
        }
        */

        public static string[] DefaultMouseHelpStrings = new String[] {
            "Hold Right click + Drag: Move view",
            "Scroll wheel: Zoom",
            "Forward/Backward button click: Change sections",
            };

        public static string[] DefaultKeyHelpStrings = new String[] {
            "Escape Key: Cancel command",
            "+/- key: Step up/down a section",
            "Shift +/- key: Step up/down ten sections",
            "Page up/down key: Change Magnification",
            "Arrow key: Move view",
            "Home key: Round magnification to whole number"
            };

        public static string[] AllDefaultHelpStrings
        {
            get
            {
                List<string> s = new List<string>(DefaultMouseHelpStrings);
                s.AddRange(DefaultKeyHelpStrings);
                return s.ToArray();
            }
        }

        /// <summary>
        /// If the base Command class' OnMouseMove is called this variable contains the mouse position at the last mouse move
        /// </summary>
        protected GridVector2 oldWorldPosition = new GridVector2(0,0); 

        MouseEventHandler MyMouseClick;
        MouseEventHandler MyMouseDoubleClick;
        MouseEventHandler MyMouseDown;
        MouseEventHandler MyMouseUp;
        MouseEventHandler MyMouseWheel;
        MouseEventHandler MyMouseMove;
        EventHandler MyMouseHover; 
        EventHandler MyMouseLeave;
        EventHandler MyMouseEnter; 
        KeyPressEventHandler MyKeyPress;
        KeyEventHandler MyKeyDown; 

        /* UI Extensions, extensions can register with these delegates to be notified whenever the default command does not process input.  This gives
        * extensions the chance to select objects only they are aware of or provide special behavior for key presses */
        public static event MouseEventHandler OnUnhandledMouseDown;
        public static event KeyPressEventHandler OnUnhandledKeyPress;

        /// <summary>
        /// Event fired whenever a command completes successfully
        /// </summary>
        public event CommandCompleteEventHandler OnCommandCompleteHandler;

        public Command(Viking.UI.Controls.SectionViewerControl parent)
        {
            this.Parent = parent; 
        }

        public void SubscribeToInterfaceEvents()
        {
            MyMouseClick = new MouseEventHandler(this.OnMouseClick);
            MyMouseDoubleClick = new MouseEventHandler(this.OnMouseDoubleClick);
            MyMouseDown = new MouseEventHandler(this.OnMouseDown);
            MyMouseUp = new MouseEventHandler(this.OnMouseUp);
            MyMouseWheel = new MouseEventHandler(this.OnMouseWheel);
            MyMouseMove = new MouseEventHandler(this.OnMouseMove);

            MyMouseHover = new EventHandler(this.OnMouseHover);
            MyMouseLeave = new EventHandler(this.OnMouseLeave);
            MyMouseEnter = new EventHandler(this.OnMouseEnter);

            MyKeyPress = new KeyPressEventHandler(this.OnKeyPress);

            MyKeyDown = new KeyEventHandler(this.OnKeyDown);

            Parent.MouseClick += MyMouseClick;
            Parent.MouseDoubleClick += MyMouseDoubleClick;
            Parent.MouseDown += MyMouseDown;
            Parent.MouseUp += MyMouseUp;
            Parent.MouseWheel += MyMouseWheel;
            Parent.MouseMove += MyMouseMove;
            Parent.MouseHover += MyMouseHover;
            Parent.MouseLeave += MyMouseLeave;
            Parent.MouseEnter += MyMouseEnter;
            Parent.KeyPress += MyKeyPress;
            Parent.KeyDown += MyKeyDown;
        }

        public void UnsubscribeToInterfaceEvents()
        {
            Parent.MouseClick -= MyMouseClick;
            Parent.MouseDoubleClick -= MyMouseDoubleClick;
            Parent.MouseDown -= MyMouseDown;
            Parent.MouseUp -= MyMouseUp;
            Parent.MouseWheel -= MyMouseWheel; 
            Parent.MouseMove -= MyMouseMove;

            Parent.MouseHover -= MyMouseHover;
            Parent.MouseLeave -= MyMouseLeave;
            Parent.MouseEnter -= MyMouseEnter; 

            Parent.KeyPress -= MyKeyPress;
            Parent.KeyDown -= MyKeyDown; 
        }

        /// <summary>
        /// Set to true if the command is in the middle of processing user input
        /// </summary>
        private bool _CommandActive = false; 

        /// <summary>
        /// Returns true if the command is in the middle of a user input sequence like
        /// defining a rectangle
        /// </summary>
        public bool CommandActive
        {
            get
            {
                if (_Deactivated)
                    return false;
                return _CommandActive;
            }
            set
            {
                if (_Deactivated != false)
                    return;

                _CommandActive = value;
            }
        }

        /// <summary>
        /// This is set to true when the command is not recieving updates from its parent.  
        /// Once set to true it should never be set to false again. 
        /// </summary>
        /// 
        private bool _Deactivated = false;

        /// <summary>
        /// This is set to true when the command is not recieving updates from its parent.  
        /// Once set to true it should never be set to false again. 
        /// </summary>
        /// 
        public bool Deactivated
        {
            get { return _Deactivated; }
            set
            {
                if (_Deactivated == false)
                {
                    Trace.WriteLine("Command Deactivated", "Command"); 

                    //Cancel any active command and remove our mouse events
                    if (value == true)
                    {
                        _CommandActive = false;
                        UnsubscribeToInterfaceEvents();
                        OnDeactivate();

                        if (OnCommandCompleteHandler != null)
                            OnCommandCompleteHandler(this, null);
                    }

                    _Deactivated = value;
                }
            }
        }

        virtual public void OnActivate() { }

        virtual public void OnDeactivate() { }

        virtual public void Undo() { }

        virtual public void Redo() { }

        #region Mouse Events

        protected virtual void OnMouseClick(object sender, MouseEventArgs e)
        {
        }

        protected virtual void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            
        }

        protected virtual void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.XButton2)
            {
                Parent.StepUpNSections(1);
            }
            else if (e.Button == MouseButtons.XButton1)
            {
                Parent.StepDownNSections(1); 
            }
            else if (Command.OnUnhandledMouseDown != null)
            {
                Command.OnUnhandledMouseDown(sender, e); 
            }
        }

        protected virtual void OnMouseUp(object sender, MouseEventArgs e)
        {
        }

        protected virtual void OnMouseWheel(object sender, MouseEventArgs e)
        {
            float multiplier = ((float)e.Delta / 120.0f);

            //This seems complicated, but we want the mouse cursor to be pointing at the same 
            //point in the volume before and after the zoom
            
            //This is the point the mouse is at...
            GridVector2 BeforeZoomPosition = Parent.ScreenToWorld(e.X, e.Y);

            StepCameraDistance(multiplier); 

            //This is the point the mouse is at after zooming camera...
            GridVector2 AfterZoomPosition = Parent.ScreenToWorld(e.X, e.Y);

            GridVector2 Offset = BeforeZoomPosition - AfterZoomPosition;

            //Move the camera position by the offset.
            GridVector2 CameraLookat = new GridVector2(Parent.Camera.LookAt.X, Parent.Camera.LookAt.Y);
            GridVector2 NewCameraLookat = CameraLookat + Offset;
            Parent.Camera.LookAt = new Vector2((float)NewCameraLookat.X, (float)NewCameraLookat.Y);

            this.Parent.Invalidate();
        }

        protected void StepCameraDistance(float multiplier)
        {
            if (multiplier > 0)
                Parent.Downsample = Parent.Downsample * 0.86956521739130434782608695652174f;
            else
                Parent.Downsample = Parent.Downsample * 1.15f;

            this.Parent.Invalidate();
        }

        protected virtual void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
            this.Parent.StatusPosition = NewPosition;

            //Figure out which tile we are over and update the status bar
            /*
            if (State.volume != null)
            {
                if (State.volume.CurrentSection != null && State.volume.CurrentSection.CurrentTransform != null)
                {
                    foreach (TileGridTransform T in Parent.Section.WarpedTo[State.volume.CurrentSection.CurrentTransform].TileTransforms)
                    {
                        if (T.CachedControlBounds.Contains(NewPosition))
                        {
                            Parent.StatusTileName = T.Number.ToString();
                            break;
                        }
                    }
                }
            }
            */

            if (oldMouse == null)
            {
                oldMouse = e;
                return;
            }

            if (e.Button.Right())
            {
                GridVector2 OldPosition = Parent.ScreenToWorld(oldMouse.X, oldMouse.Y);

                Debug.Assert(double.IsNaN(NewPosition.X) == false);

                GridVector2 delta = NewPosition - OldPosition;

                if (double.IsNaN(delta.X))
                    return;

                Parent.Camera.LookAt -= new Vector2((float)delta.X, (float)delta.Y);

                this.Parent.Invalidate();
            }
            else if (e.Button.Middle())
            {
                //Figure out if the mouse went clockwise or counterclockwise relative to the center of the screen
                System.Drawing.Rectangle rect = Parent.ClientRectangle;
                Vector2 Center = new Vector2(((rect.Width - rect.X) / 2) + rect.X, ((rect.Height - rect.Y) / 2) + rect.Y);

                Vector2 old = new Vector2(oldMouse.X - Center.X, oldMouse.Y - Center.Y);
                Vector2 newMouse = new Vector2(e.X - Center.X, e.Y - Center.Y);

                newMouse.Normalize();
                old.Normalize();

                float angle = (float)Math.Acos((double)Vector2.Dot(newMouse, old));

                if (newMouse.Y < 0)
                {
                    if (newMouse.X - old.X < 0)
                        angle = -angle;
                }
                else if (newMouse.Y >= 0)
                {
                    if (newMouse.X - old.X > 0)
                        angle = -angle;
                }
                angle = MathHelper.ToDegrees(angle);

                //    Trace.WriteLine("Angle changed " + angle.ToString() + " to " + control.CameraRotation.ToString(), "Command"); 

                Parent.Camera.Rotation += angle;

                this.Parent.Invalidate();
            }

            SaveAsOldMousePosition(e);            
        }

        protected void SaveAsOldMousePosition(MouseEventArgs e)
        { 
            this.oldMouse = e;
            this.oldWorldPosition = Parent.ScreenToWorld(e.X, e.Y);

        }

        protected virtual void OnMouseHover(object sender, EventArgs e)
        {
        }

        protected virtual void OnMouseLeave(object sender, EventArgs e)
        {
        }

        protected virtual void OnMouseEnter(object sender, EventArgs e)
        {
        }

        #endregion

        #region Key Event Handlers
        protected virtual void OnKeyPress(object sender, KeyPressEventArgs e)
        {

            //Escape cancels the current command and sets the selected item to null
            if (e.KeyChar == (char)Keys.Escape)
            {
                //On escape kill the current command, and any active queues
                this.Parent.CommandQueue.ClearQueue(); 
                UI.State.SelectedObject = null;

                //This will probably already be set by adjusting the selected object, but to be safe...
                this.Deactivated = true;
            }
            else if (e.KeyChar == '=' || e.KeyChar == '+')
            {
                if (((int)Control.ModifierKeys & (int)Keys.Shift) > 0)
                {
                    Parent.StepUpNSections(10);
                }
                else
                {
                    Parent.StepUpNSections(1);
                }
            }
            else if (e.KeyChar == '-' || e.KeyChar == '_')
            {
                if (((int)Control.ModifierKeys & (int)Keys.Shift) > 0)
                {
                    Parent.StepDownNSections(10);
                }
                else
                {
                    Parent.StepDownNSections(1);
                }
            }
            else if (e.KeyChar == (char)Keys.PrintScreen ||
                     e.KeyChar == 'z')
            {
                
                this.CommandActive = false; 
                
           //     Parent.TakeScreenShot(); 
            }
            else if (Command.OnUnhandledKeyPress != null)
            {
                Command.OnUnhandledKeyPress(sender, e);
            }

            Parent.Invalidate();
        }

        protected virtual void OnKeyDown(object sender, KeyEventArgs e)
        {

            switch (e.KeyCode)
            {
                case Keys.PageUp:
                    StepCameraDistance(1);
                    break;
                case Keys.PageDown:
                    StepCameraDistance(-1);
                    break;                     
                case Keys.Left:
                    GridVector2 Left = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Top);
                    GridVector2 Right = Parent.ScreenToWorld(Parent.ClientRectangle.Right, Parent.ClientRectangle.Top);
                    GridVector2 Diff = Right - Left;
                    Diff.Scale(0.25);

                    Parent.Camera.LookAt -= new Vector2((float)Diff.X, (float)Diff.Y);
                    this.Parent.Invalidate();
                    break; 
                case Keys.Right:
                    Left = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Top);
                    Right = Parent.ScreenToWorld(Parent.ClientRectangle.Right, Parent.ClientRectangle.Top);
                    Diff = Right - Left;
                    Diff.Scale(0.25);

                    Parent.Camera.LookAt += new Vector2((float)Diff.X, (float)Diff.Y);
                    this.Parent.Invalidate();
                    break; 
                case Keys.Up:
                    GridVector2 Bottom = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Bottom);
                    GridVector2 Top = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Top);
                    Diff = Top - Bottom;
                    Diff.Scale(0.25);

                    Parent.Camera.LookAt += new Vector2((float)Diff.X, (float)Diff.Y);
                    this.Parent.Invalidate();
                    break; 
                case Keys.Down:
                    Bottom = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Bottom);
                    Top = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Top);
                    Diff = Top - Bottom;
                    Diff.Scale(0.25);

                    Parent.Camera.LookAt -= new Vector2((float)Diff.X, (float)Diff.Y);
                    this.Parent.Invalidate();
                    break; 
                case Keys.Home:
                    Parent.Downsample = Math.Round(Parent.Downsample) < 1.0 ? 0.5 : Math.Round(Parent.Downsample);
                    this.Parent.Invalidate();
                    break;
            }
        }

        #endregion

        /// <summary>
        /// Called when the user input is completed and the command should make whatever changes are required and shut down
        /// </summary>
        protected virtual void Execute()
        {
            this.Deactivated = true;
            Parent.Invalidate();
        }

        public virtual void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            return; 
        }

        


    }
}
