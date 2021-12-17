using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
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

        protected PenEventArgs oldPen = null;

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
        KeyEventHandler MyKeyUp;

        PropertyChangedEventHandler MyCameraChanged;
        
        /* UI Extensions, extensions can register with these delegates to be notified whenever the default command does not process input.  This gives
        * extensions the chance to select objects only they are aware of or provide special behavior for key presses */
        public static event MouseEventHandler OnUnhandledMouseDown;
        public static event KeyPressEventHandler OnUnhandledKeyPress;

        /// <summary>
        /// Event fired whenever a command completes successfully
        /// </summary>
        public event CommandCompleteEventHandler OnCommandCompleteHandler;

        public static int _NextID = 0;
        public int ID;

        private void AssignID()
        {
            this.ID = _NextID;
            _NextID = _NextID + 1;
        }

        public Command(Viking.UI.Controls.SectionViewerControl parent)
        {
            AssignID();
            this.Parent = parent; 
        }

        /// <summary>
        /// Allows refreshing the parent regardless of thread
        /// </summary>
        protected void ThreadSafeParentInvalidate()
        {
            if (Parent.InvokeRequired)
                Parent.BeginInvoke(new Action(() => Parent.Invalidate()));
            else
                Parent.Invalidate();
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
            MyKeyUp = new KeyEventHandler(this.OnKeyUp);

            MyCameraChanged = new PropertyChangedEventHandler(this.OnCameraChanged);

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
            Parent.KeyUp += MyKeyUp;

            Parent.OnPenEnterRange += OnPenEnterRange;
            Parent.OnPenLeaveRange += OnPenLeaveRange;
            Parent.OnPenContact += OnPenContact;
            Parent.OnPenLeaveContact += OnPenLeaveContact;
            Parent.OnPenMove += OnPenMove;
            Parent.OnPenButtonDown += OnPenButtonDown;
            Parent.OnPenButtonUp += OnPenButtonUp;

            Parent.OnGestureBegin += OnGestureBegin;
            Parent.OnGestureZoom += OnGestureZoom;
            Parent.OnGesturePan += OnGesturePan; 
             
            Parent.Camera.PropertyChanged += MyCameraChanged;
        }

        protected virtual void OnPenButtonUp(object sender, PenEventArgs e)
        {
            return;
        }

        protected virtual void OnPenButtonDown(object sender, PenEventArgs e)
        {
            return;
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
            Parent.KeyUp -= MyKeyUp;

            Parent.OnPenEnterRange -= OnPenEnterRange;
            Parent.OnPenLeaveRange -= OnPenLeaveRange;
            Parent.OnPenContact -= OnPenContact;
            Parent.OnPenLeaveContact -= OnPenLeaveContact;
            Parent.OnPenMove -= OnPenMove;

            Parent.OnGestureBegin -= OnGestureBegin;
            Parent.OnGestureZoom -= OnGestureZoom;
            Parent.OnGesturePan -= OnGesturePan;

            Parent.Camera.PropertyChanged -= MyCameraChanged;
        }

        /// <summary>
        /// Set to true if the command is in the middle of processing user input
        /// </summary>
        private bool _CommandActive = false; 

        /// <summary>
        /// Returns true if the command is in the middle of a user input sequence like
        /// defining a rectangle.
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

        virtual protected void OnDeactivate() { }

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
            
            /*if(Touch.IsPenEvent(out uint PointerID))
            {
                Trace.WriteLine("Pen button down {0}", e.Button.ToString());
            }
            else
            {
                Trace.WriteLine("Mouse button down {0}", e.Button.ToString());
            }*/

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

            RecenterCameraAfterZoom(BeforeZoomPosition, AfterZoomPosition);

            this.Parent.Invalidate();
        }

        protected void RecenterCameraAfterZoom(GridVector2 BeforeZoomPosition, GridVector2 AfterZoomPosition)
        {
            GridVector2 Offset = BeforeZoomPosition - AfterZoomPosition;

            //Move the camera position by the offset.
            GridVector2 CameraLookat = Parent.Camera.LookAt.ToGridVector2();
            GridVector2 NewCameraLookat = CameraLookat + Offset;
            Parent.Camera.LookAt = new Vector2((float)NewCameraLookat.X, (float)NewCameraLookat.Y);
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

            bool TranslateButtonDown = e.Button.Right();
            bool RotateButtonDown = e.Button.Middle();
            if (oldMouse == null)
            {
                oldMouse = e;
                return;
            }

            if (TranslateButtonDown)
            {
                GridVector2 OldPosition = Parent.ScreenToWorld(oldMouse.X, oldMouse.Y);
                Debug.Assert(double.IsNaN(NewPosition.X) == false);

                OnTranslateInput(NewPosition, OldPosition);
            }
            else if (RotateButtonDown)
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

        protected virtual void OnPenMove(object sender, PenEventArgs e)
        {
            GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
            this.Parent.StatusPosition = NewPosition;

            //Cancel the command on a barrel click
            if (oldPen != null && e.Barrel && oldPen.Barrel == false)
            {
                CancelCommand();
                SaveAsOldPenPosition(e);
                return;
            }
             
            if (oldPen != null && e.Erase && e.InContact)
            {
                GridVector2 OldPosition = Parent.ScreenToWorld(oldPen.X, oldPen.Y);
                System.Diagnostics.Debug.Assert(double.IsNaN(NewPosition.X) == false);

                OnTranslateInput(NewPosition, OldPosition);
            }

            SaveAsOldPenPosition(e);

            return;
        }

        protected void OnTranslateInput(GridVector2 NewWorldPosition, GridVector2 OldWorldPosition)
        {
            

            Debug.Assert(double.IsNaN(NewWorldPosition.X) == false);

            GridVector2 delta = NewWorldPosition - OldWorldPosition;

            if (double.IsNaN(delta.X))
                return;

            Parent.Camera.LookAt -= new Vector2((float)delta.X, (float)delta.Y);

            this.Parent.Invalidate();

        }

        protected void SaveAsOldMousePosition(MouseEventArgs e)
        { 
            this.oldMouse = e;
            this.oldWorldPosition = Parent.ScreenToWorld(e.X, e.Y);
        }

        protected void SaveAsOldPenPosition(PenEventArgs e)
        {
            this.oldPen = e;
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

        /// <summary>
        /// If this method is overrriden the implementation should call the baseimplemen
        /// </summary>
        protected void CancelCommand()
        {
            //On escape kill the current command, and any active queues
            this.Parent.CommandQueue.ClearQueue();
            UI.State.SelectedObject = null;

            //This will probably already be set by adjusting the selected object, but to be safe...
            this.Deactivated = true;
        }

        #region Key Event Handlers
        protected virtual void OnKeyPress(object sender, KeyPressEventArgs e)
        {

            //Escape cancels the current command and sets the selected item to null
            if (e.KeyChar == (char)Keys.Escape)
            {
                CancelCommand();
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

        protected virtual void OnKeyUp(object sender, KeyEventArgs e)
        {
            return;
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
                    Diff *= 0.25;

                    Parent.Camera.LookAt -= new Vector2((float)Diff.X, (float)Diff.Y);
                    this.Parent.Invalidate();
                    break; 
                case Keys.Right:
                    Left = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Top);
                    Right = Parent.ScreenToWorld(Parent.ClientRectangle.Right, Parent.ClientRectangle.Top);
                    Diff = Right - Left;
                    Diff *= 0.25;

                    Parent.Camera.LookAt += new Vector2((float)Diff.X, (float)Diff.Y);
                    this.Parent.Invalidate();
                    break; 
                case Keys.Up:
                    GridVector2 Bottom = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Bottom);
                    GridVector2 Top = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Top);
                    Diff = Top - Bottom;
                    Diff *= 0.25;

                    Parent.Camera.LookAt += new Vector2((float)Diff.X, (float)Diff.Y);
                    this.Parent.Invalidate();
                    break; 
                case Keys.Down:
                    Bottom = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Bottom);
                    Top = Parent.ScreenToWorld(Parent.ClientRectangle.Left, Parent.ClientRectangle.Top);
                    Diff = Top - Bottom;
                    Diff *= 0.25;

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
        /// Where a gesture began in world coordinates
        /// </summary>
        /// 
        GridVector2 PanGestureWorldPositionOrigin;

        /// <summary>
        /// Distance between the fingers when they first begin the zoom gesture
        /// </summary>
        double ZoomGestureInitialLineLength;
        
        /// <summary>
        /// Magnification level when a zoom gesture first began
        /// </summary>
        double ZoomGestureStartingMagnification;

        protected virtual void OnGestureBegin(object sender, BeginGestureEventArgs e)
        {
            Trace.WriteLine($"{this.ID}: Begin Gesture");
            PanGestureWorldPositionOrigin = Parent.Camera.LookAt.ToGridVector2();
            ZoomGestureStartingMagnification = Parent.Camera.Downsample;
        }

        protected virtual void OnGesturePan(object sender, PanGestureEventArgs e)
        {
            GridVector2 screen_begin = new GridVector2(e.BeginPt.X, e.BeginPt.Y);
            GridVector2 screen_end = new GridVector2(e.EndPt.X, e.EndPt.Y);

            //Trace.WriteLine($"{e}");
            /*
            GridVector2 screen_delta = new GridVector2(e.Delta.X, e.Delta.Y);
            GridVector2 screen_origin = screen_end - screen_delta; 

            GridVector2 Begin = Parent.ScreenToWorld(screen_origin.X, screen_origin.Y);
            GridVector2 End   = Parent.ScreenToWorld(screen_end.X, screen_end.Y);
            GridVector2 World_Delta = End - Begin;
            Parent.Camera.LookAt = World_Delta.ToXNAVector2();
            this.Parent.Invalidate();  
            */
            
            
            if (e.Gesture.State == GestureState.GF_BEGIN)
            {
                //PanGestureWorldPositionOrigin = Parent.ScreenToWorld(e.BeginPt.X, e.BeginPt.Y);
                return;
            }
            

            GridVector2 Begin = Parent.ScreenToWorld(screen_begin.X, screen_begin.Y);
            GridVector2 End = Parent.ScreenToWorld(screen_end.X, screen_end.Y);
            GridVector2 World_Delta = End - Begin;
            //Trace.WriteLine($"{this.ID}: {End} - {Begin} = {World_Delta}");
            Parent.Camera.LookAt = (PanGestureWorldPositionOrigin - World_Delta).ToXNAVector2();
            this.Parent.Invalidate();
        }

        protected virtual void OnGestureZoom(object sender, PanGestureEventArgs e)
        {
            
            //GridVector2 screen_begin = new GridVector2(e.BeginPt.X, e.BeginPt.Y);
            GridVector2 screen_Center = new GridVector2(e.EndPt.X, e.EndPt.Y);

            /*if (screen_begin == screen_end)
                return;
            */

            //Figure out how much of the total client screen the distance represents
            double DistanceBetweenFingers = (double)e.Gesture.Arguments;

            if (e.Gesture.State == GestureState.GF_BEGIN)
            {
                ZoomGestureInitialLineLength = DistanceBetweenFingers;
                return;
            }

            double scale = ZoomGestureInitialLineLength / DistanceBetweenFingers;
            double newDownsample = ZoomGestureStartingMagnification * scale;
            //Trace.WriteLine($"{this.ID}: Zoom: {newDownsample} = {ZoomGestureStartingMagnification:F2} * {scale:F2} Length: {ZoomGestureInitialLineLength:F2} / {DistanceBetweenFingers:F2}");

            /*
            if (newDownsample < 0.5)
                newDownsample = 0.5; 

            if (newDownsample > 256)
                newDownsample = 256;
            */

            //This is the point the mouse is at before zoom...
            GridVector2 BeforeZoomPosition = Parent.ScreenToWorld(screen_Center.X, screen_Center.Y);

            Parent.Camera.Downsample = newDownsample;

            //This is the point the mouse is at after zoom...
            GridVector2 AfterZoomPosition = Parent.ScreenToWorld(screen_Center.X, screen_Center.Y);

            RecenterCameraAfterZoom(BeforeZoomPosition, AfterZoomPosition);

            this.Parent.Invalidate();
        }


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

        protected virtual void OnCameraChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        protected virtual void OnPenEnterRange(object sender, PenEventArgs e)
        {  
        }

        protected virtual void OnPenLeaveRange(object sender, PenEventArgs e)
        {
        }

        protected virtual void OnPenContact(object sender, PenEventArgs e)
        {
        }
        protected virtual void OnPenLeaveContact(object sender, PenEventArgs e)
        {
        }

    }
}
