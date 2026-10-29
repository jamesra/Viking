using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Controls;
using WinForms = System.Windows.Forms;

namespace VikingCoreTests
{
    /// <summary>
    /// Drives the real <see cref="SliderWithValueBox"/> from the keyboard and mouse the way Viking hosts it: a WPF window
    /// shown modelessly from a Windows Forms message loop with keyboard interop, with key presses posted to the window as
    /// WM_KEYDOWN/WM_KEYUP and pumped by that loop (so TranslateMessage makes the characters, as for a real keyboard).
    /// Ported from Legacy 40c5766e (arrow keys and click focus); Legacy's typing tests from 3c823484 come with the
    /// port of that commit.
    /// </summary>
    /// <remarks>
    /// Each test runs on its own STA thread. The window is placed off screen and not activated; posted messages go to
    /// its own handle, so nothing is typed into whatever window the user has in front.
    /// </remarks>
    [TestClass]
    public class SliderWithValueBoxKeyboardTests
    {
        private const double Tick = 0.05;

        [TestMethod]
        public void ArrowKeysNudgeTheFocusedSliderOneTick_LikeADrag()
        {
            Host.Run(h =>
            {
                h.Tab(from: h.Before);
                Assert.IsTrue(h.Slider.IsKeyboardFocused, "Tab should reach the slider");

                int changes = h.Model.Changes;
                h.Press(Key.Right);
                Assert.AreEqual(1 + Tick, h.Model.Level, 1e-9, "Right");
                Assert.AreEqual(changes + 1, h.Model.Changes, "Each arrow updates the bound value once, as a drag step does");
                Assert.AreEqual("1.05", h.Box.Text);
                h.Press(Key.Up);
                Assert.AreEqual(1 + (2 * Tick), h.Model.Level, 1e-9, "Up");
                h.Press(Key.Left);
                h.Press(Key.Down);
                Assert.AreEqual(1, h.Model.Level, 1e-9, "Left then Down");
                Assert.IsTrue(h.Slider.IsKeyboardFocused, "Arrows must not move focus off the slider");

                h.Model.Level = 1.02;
                h.Pump();
                h.Press(Key.Right);
                Assert.AreEqual(1.05, h.Model.Level, 1e-9, "Off a tick, Right snaps to the next one");

                h.Model.Level = h.Control.Maximum;
                h.Pump();
                h.Press(Key.Right);
                Assert.AreEqual(h.Control.Maximum, h.Model.Level, 1e-12, "Right stops at the maximum");
                h.Model.Level = h.Control.Minimum;
                h.Pump();
                h.Press(Key.Left);
                Assert.AreEqual(h.Control.Minimum, h.Model.Level, 1e-12, "Left stops at the minimum");
            });
        }

        [TestMethod]
        public void ArrowStepIsSmallChangeWhenSet()
        {
            Host.Run(h =>
            {
                h.Control.SmallChange = 0.25;
                h.Control.IsSnapToTickEnabled = false;
                h.Pump();
                h.Tab(from: h.Before);
                h.Press(Key.Right);
                Assert.AreEqual(1.25, h.Model.Level, 1e-12);
            });
        }

        /// <summary>
        /// A press on the thumb, or on the track beside it, focuses the slider (its thumb and track buttons cannot take
        /// focus themselves), which commits text typed in the box; the arrows then move the slider. The press goes in
        /// through <see cref="InputManager"/> as the mouse's PreviewMouseDown, so WPF derives the left-button events
        /// and routes them from the pressed part as it does for a real click.
        /// </summary>
        [TestMethod]
        public void ClickingTheSliderGivesItTheKeyboard()
        {
            Host.Run(h =>
            {
                h.FocusBox();
                h.Type("2.5");
                h.PressMouseOn(FindDescendant<Thumb>(h.Slider));
                Assert.IsTrue(h.Slider.IsKeyboardFocused, "A press on the thumb should focus the slider");
                Assert.AreEqual(2.5, h.Model.Level, 1e-12, "Leaving the box for the slider commits what was typed");

                h.Press(Key.Right);
                Assert.AreEqual(2.55, h.Model.Level, 1e-9);

                foreach (RepeatButton track in FindDescendants<RepeatButton>(h.Slider))
                {
                    h.Focus(h.Box);
                    h.PressMouseOn(track);
                    Assert.IsTrue(h.Slider.IsKeyboardFocused, "A press on the track should focus the slider");
                }
            });
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject =>
            FindDescendants<T>(root).First();

        private static System.Collections.Generic.IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                    yield return match;

                foreach (T inner in FindDescendants<T>(child))
                    yield return inner;
            }
        }

        /// <summary>Bound to the control's <see cref="SliderWithValueBox.Value"/>, standing in for a preference view model.</summary>
        private sealed class Preference : INotifyPropertyChanged
        {
            private double level = 1;

            public event PropertyChangedEventHandler? PropertyChanged;

            /// <summary>How many times <see cref="Level"/> changed, from either side of the binding.</summary>
            public int Changes { get; private set; }

            public double Level
            {
                get => level;
                set
                {
                    if (level == value)
                        return;

                    level = value;
                    Changes++;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
                }
            }
        }

        /// <summary>
        /// One test's window: a text box before the control, the control (range -5 to 20, ticks of 0.05 with snapping,
        /// "F2"), and a text box after it, shown from a Windows Forms loop as Viking shows its dialogs.
        /// </summary>
        private sealed class Host
        {
            private const uint WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;

            [DllImport("user32.dll")]
            private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            private static extern uint MapVirtualKey(uint code, uint mapType);

            [DllImport("user32.dll")]
            private static extern bool SetKeyboardState(byte[] keyState);

            private IntPtr hwnd;

            public SliderWithValueBox Control = null!;
            public TextBox Box = null!;
            public Slider Slider = null!;
            public TextBox Before = null!;
            public TextBox After = null!;
            public Preference Model = null!;

            /// <summary>Runs <paramref name="body"/> against the standard test window.</summary>
            public static void Run(Action<Host> body, string culture = "en-US") =>
                RunWindow(() =>
                {
                    var model = new Preference();
                    var control = new SliderWithValueBox
                    {
                        Minimum = -5,
                        Maximum = 20,
                        TickFrequency = Tick,
                        IsSnapToTickEnabled = true,
                        ValueFormat = "F2",
                    };
                    BindingOperations.SetBinding(control, SliderWithValueBox.ValueProperty,
                        new Binding(nameof(Preference.Level)) { Source = model, Mode = BindingMode.TwoWay });
                    var panel = new StackPanel();
                    panel.Children.Add(new TextBox { Text = "before" });
                    panel.Children.Add(control);
                    panel.Children.Add(new TextBox { Text = "after" });
                    return (new Window { Content = panel, Width = 400, Height = 160 }, model);
                }, (h, window, model) =>
                {
                    var panel = (StackPanel)window.Content;
                    h.Before = (TextBox)panel.Children[0];
                    h.Control = (SliderWithValueBox)panel.Children[1];
                    h.After = (TextBox)panel.Children[2];
                    h.Box = (TextBox)h.Control.FindName("PART_ValueBox");
                    h.Slider = (Slider)h.Control.FindName("PART_Slider");
                    h.Model = model;
                    body(h);
                }, culture);

            /// <summary>
            /// On a new STA thread: makes a window, shows it from a Windows Forms loop with keyboard interop, runs
            /// <paramref name="body"/>, and rethrows its failure here.
            /// </summary>
            public static void RunWindow<TModel>(Func<(Window, TModel)> make, Action<Host, Window, TModel> body, string culture = "en-US")
            {
                Exception? failure = null;
                var thread = new Thread(() =>
                {
                    CultureInfo.CurrentCulture = new CultureInfo(culture);
                    var owner = new WinForms.Form
                    {
                        ShowInTaskbar = false,
                        StartPosition = WinForms.FormStartPosition.Manual,
                        Location = new System.Drawing.Point(-20000, -20000),
                        Size = new System.Drawing.Size(200, 100),
                    };
                    Window? window = null;
                    try
                    {
                        owner.Show();
                        (window, TModel model) = make();
                        window.WindowStartupLocation = WindowStartupLocation.Manual;
                        window.Left = -20000;
                        window.Top = -20000;
                        window.ShowActivated = false;
                        window.ShowInTaskbar = false;
                        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(window);
                        window.Show();

                        var host = new Host { hwnd = new WindowInteropHelper(window).Handle };
                        host.Pump();
                        body(host, window, model);
                    }
                    catch (Exception e)
                    {
                        failure = e;
                    }
                    finally
                    {
                        window?.Close();
                        owner.Close();
                        WinForms.Application.DoEvents();
                        Dispatcher.CurrentDispatcher.InvokeShutdown();
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                if (!thread.Join(TimeSpan.FromSeconds(60)))
                    Assert.Fail("The WPF test window did not finish within 60 s");

                if (failure is not null)
                    throw new AssertFailedException(failure.Message, failure);
            }

            /// <summary>Lets the Windows Forms loop dispatch everything queued, including WPF's own dispatcher work.</summary>
            public void Pump()
            {
                for (int i = 0; i < 4; i++)
                {
                    WinForms.Application.DoEvents();
                    Thread.Sleep(5);
                }
            }

            public void Focus(UIElement element)
            {
                element.Focus();
                Pump();
                Assert.IsTrue(element.IsKeyboardFocused, $"{element.GetType().Name} should take the keyboard focus");
            }

            /// <summary>
            /// Presses and releases the left mouse button on <paramref name="target"/> through the input pipeline, then
            /// drops any capture the press took (a thumb starts a drag).
            /// </summary>
            public void PressMouseOn(UIElement target)
            {
                foreach (RoutedEvent routed in new[] { Mouse.PreviewMouseDownEvent, Mouse.PreviewMouseUpEvent })
                {
                    InputManager.Current.ProcessInput(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    {
                        RoutedEvent = routed,
                        Source = target,
                    });
                    Pump();
                }

                Mouse.Capture(null);
                Pump();
            }

            /// <summary>Focuses the number box and selects its text, as clicking into it and selecting would.</summary>
            public void FocusBox()
            {
                Focus(Box);
                Box.SelectAll();
            }

            /// <summary>Focuses <paramref name="from"/> and presses Tab once.</summary>
            public void Tab(UIElement from)
            {
                Focus(from);
                Press(Key.Tab);
            }

            /// <summary>Presses each character's key in turn: digits, '.', ',' and '-'.</summary>
            public void Type(string text)
            {
                foreach (char c in text)
                {
                    Key key = c switch
                    {
                        >= '0' and <= '9' => Key.D0 + (c - '0'),
                        '.' => Key.OemPeriod,
                        ',' => Key.OemComma,
                        '-' => Key.OemMinus,
                        _ => throw new ArgumentException($"No key for '{c}'"),
                    };
                    Press(key);
                }
            }

            /// <summary>Posts one key's down and up messages to the window and pumps until they are handled.</summary>
            public void Press(Key key)
            {
                // TranslateMessage reads this thread's key state; clear it so a Shift the user holds elsewhere cannot
                // turn "1" into "!"
                SetKeyboardState(new byte[256]);
                uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                uint scan = MapVirtualKey(vk, 0);
                bool extended = key is Key.Left or Key.Right or Key.Up or Key.Down;
                long down = 1 | ((long)scan << 16) | (extended ? 1L << 24 : 0);
                long up = down | (3L << 30);
                PostMessage(hwnd, WM_KEYDOWN, (IntPtr)vk, (IntPtr)down);
                PostMessage(hwnd, WM_KEYUP, (IntPtr)vk, (IntPtr)up);
                Pump();
            }
        }
    }
}
