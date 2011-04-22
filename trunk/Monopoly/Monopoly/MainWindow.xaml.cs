﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections;
using Networking;
using System.Threading;
using System.Net;

namespace Monopoly
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point _dim;
        private Point _loc;
        private Communicator comm;
        private MessageHandler mHandler = new MessageHandler();
        private GameBoard myBoard;
        private ChatComponent myChat;
        private MenuFader myMenu;
        private UserPiece[] pieces;

        public MainWindow()
        {
            InitializeComponent();
            comm = Communicator.Instance;
            this.Background = ThemeParser.GetColor(ThemeParser.Colors.Background);
            comm.ConnectionStatusChanged += new EventHandler<ConnectionStatusChangedEventArgs>(comm_ConnectionStatusChanged);
            comm.DataRecieved += new EventHandler<DataReceivedEventArgs>(comm_DataRecieved);
            mHandler.NewIncomingMessage += new EventHandler<NewIncomingMessageEventArgs>(mHandler_NewIncomingMessage);
            mHandler.Start();
            this.Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
        }


        void mHandler_NewIncomingMessage(object sender, NewIncomingMessageEventArgs e)
        {
            if (myChat != null)
            {
                myChat.NewMessage(e.Sender, e.Message);
            }
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ThreadStart s = new ThreadStart(SplashDelay);
            Thread splash = new Thread(s);
            splash.IsBackground = true;
            splash.Start();
        }

        private void SplashDelay()
        {
            DateTime loadTime = DateTime.Now;
            while (DateTime.Now.Subtract(loadTime).TotalSeconds < 2.5) { }
            ChooseRole();
        }

        private void ChooseRole()
        {
            HideSplash();
            while(Splash.Visibility != Visibility.Collapsed){}
            Setup();
        }

        private void HideSplash()
        {
            if (this.Dispatcher.CheckAccess())
            {
                Splash.Visibility = Visibility.Collapsed;
            }
            else this.Dispatcher.BeginInvoke(new Action(HideSplash));
        }

        private void Setup()
        {
            if (this.Dispatcher.CheckAccess())
            {
                myBoard = new GameBoard();
                myBoard.SetBinding(WidthProperty, "myGrid.Height");
                myBoard.SetBinding(HeightProperty, "myGrid.Height");
                myBoard.Background = ThemeParser.GetColor(ThemeParser.Colors.Board);
                Grid.SetRowSpan(myBoard, 3);
                Grid.SetColumn(myBoard, 0);
                myChat = new ChatComponent();
                myChat.NewOutgoingMessage += new EventHandler<NewOutgoingMessageEventArgs>(Chat_NewOutgoingMessage);
                Grid.SetColumn(myChat, 1);
                Grid.SetRow(myChat, 2);
                myChat.Margin = new Thickness(5, 0, 0, 0);
                myMenu = new MenuFader(myCanvas);
                myMenu.Margin = new Thickness(25,0,25,0);
                myMenu.HostGameClicked += new EventHandler<HostGameClickEventArgs>(myMenu_HostGameClicked);
                myMenu.JoinGameClicked += new EventHandler<JoinGameClickEventArgs>(myMenu_JoinGameClicked);
                myMenu.CloseGameClicked += new EventHandler<CloseGameClickEventArgs>(myMenu_CloseGameClicked);
                myGrid.Children.Add(myBoard);
                myGrid.Children.Add(myChat);
                myCanvas.Children.Add(myMenu);
                myBoard.GameBuilt += new EventHandler<GameBoardBuiltEventArgs>(myBoard_GameBuilt);
                myBoard.Dice.RollEnded += new EventHandler<RollEndedEventArgs>(Dice_RollEnded);
                
            }
            else this.Dispatcher.BeginInvoke(new Action(Setup), null);
        }

        void Dice_RollEnded(object sender, RollEndedEventArgs e)
        {
            int d1 = e.DiceOneValue;
            int d2 = e.DiceTwoValue;
            //TODO This is where you handle the dice values.
            myChat.NewMessage("System", "You rolled a " + d1 + " and a " + d2 + ".");
            Move(pieces[0], d1 + d2);
        }

        public void InitialPlacement(ref UserPiece u)
        {
            foreach (var tp in myBoard.myBoard.Children)
            {
                if (tp as Property != null && ((Property)tp).PropertyListing.Location == 0)
                {
                    ((Property)tp).Spots.Children.Add(u = new UserPiece());
                    u.CurrentLocation = 0;
                }
            }
        }

        public void Move(UserPiece up, int value)
        {
            ParameterizedThreadStart start = new ParameterizedThreadStart(MoveWork);
            Thread moveThread = new Thread(start);
            moveThread.IsBackground = true;
            moveThread.Name = "MoveThread";
            moveThread.Start(new object[] { up, value } as object);
        }

        private void MoveWork(object param)
        {
            object[] parameters = param as object[];
            if (parameters == null)
            {
                System.Console.WriteLine("Error in parsing parameters for MoveWork.");
                return;
            }
            UserPiece up = parameters[0] as UserPiece;
            int value = (int)parameters[1];
            if (up == null)
            {
                System.Console.WriteLine("Error in parsing UserPiece for MoveWork.");
                return;
            }
            int i = up.CurrentLocation;
            while(up.CurrentLocation < i + value)
            {
                Jump(up, up.CurrentLocation, up.CurrentLocation + 1);
                Thread.Sleep(250);
            }
        }

        public void Jump(UserPiece up, int current, int destination)
        {
            if (this.Dispatcher.CheckAccess())
            {
                Property cur = null;
                Property des = null;
                foreach (var tp in myBoard.myBoard.Children)
                {
                    if (tp as Property != null && ((Property)tp).PropertyListing.Location == current)
                        cur = ((Property)tp);
                    if (tp as Property != null && ((Property)tp).PropertyListing.Location == destination)
                        des = ((Property)tp);
                }
                if (cur == null || des == null)
                    return;
                up.CurrentLocation = destination;
                cur.Spots.Children.Remove(up);
                des.Spots.Children.Add(up);
            }
            else this.Dispatcher.BeginInvoke(new Action<UserPiece, int, int>(Jump), new object[] { up, current, destination });
        }

        void myMenu_CloseGameClicked(object sender, CloseGameClickEventArgs e)
        {
            this.Close();
        }

        void myMenu_JoinGameClicked(object sender, JoinGameClickEventArgs e)
        {
            //TODO implement
            comm.UserRole = Communicator.ROLE.CLIENT;
            comm.StartClient(comm.GetMyIpAddr().ToString(),23);
        }

        void myMenu_HostGameClicked(object sender, HostGameClickEventArgs e)
        {
            //TODO implement
            comm.UserRole = Communicator.ROLE.SERVER;
            comm.StartServer(23);
            IPAddress ip = comm.GetMyIpAddr();
            MessageBox.Show(ip.ToString());
        }

        void myBoard_GameBuilt(object sender, GameBoardBuiltEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                Loading.Visibility = Visibility.Hidden;

                pieces = new UserPiece[1];
                InitialPlacement(ref pieces[0]);
            }
            else Dispatcher.BeginInvoke(new Action<object, GameBoardBuiltEventArgs>(myBoard_GameBuilt), new object[] { null, null });
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            mHandler.Stop();
        }

        void comm_DataRecieved(object sender, DataReceivedEventArgs e)
        {
            mHandler.QueueMessage(e.DataReceived);
        }

        void comm_ConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            Console.Write("Connected = " + e.Connected);
        }

        void Chat_NewOutgoingMessage(object sender, NewOutgoingMessageEventArgs e)
        {
            string localName = "RemoteUser";
            string str = String.Concat(localName, "<@>", e.Message);
            byte[] msg = Encoding.UTF8.GetBytes(str);
            comm.Send((new Message(Message.Type.Chat, msg)).ToBytes());
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1)
            {
                if (this.WindowState != System.Windows.WindowState.Normal)
                    Restore();
                else
                    Maximize();   
            }
        }
        
        private void Maximize()
        {
            if (this.Dispatcher.CheckAccess())
            {
                _dim = new Point(this.Width, this.Height);
                _loc = new Point(this.Left, this.Top);
                this.WindowState = System.Windows.WindowState.Maximized;
                this.WindowStyle = System.Windows.WindowStyle.None;
                this.myMenu.Close();
            }
            else this.Dispatcher.BeginInvoke(new Action(Maximize));
        }

        private void Restore()
        {
            if (this.Dispatcher.CheckAccess())
            {
                this.Left = _loc.X;
                this.Top = _loc.Y;
                this.Width = _dim.X;
                this.Height = _loc.Y;
                this.WindowState = System.Windows.WindowState.Normal;
                this.WindowStyle = System.Windows.WindowStyle.ThreeDBorderWindow;
                this.myMenu.Close();
            }
            else this.Dispatcher.BeginInvoke(new Action(Restore));
        }
    }
}