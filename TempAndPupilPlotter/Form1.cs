using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using NetMQ;
using NetMQ.Sockets;

namespace TempAndPupilPlotter
{
    public partial class Form1 : Form
    {
        public float diameter;
        private NetMqListener _netMqListener;

        private void HandleMessage(string message)
        {
            string[] parsedMessage = message.Split(',');
            diameter = float.Parse(parsedMessage[1]);

        }
        public Form1()
        {
            InitializeComponent();
            serialPort1.Open();

            _netMqListener = new NetMqListener(HandleMessage);
            _netMqListener.Start();

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //lets save the mapper globally.
            Charting.For<MeasureModel>(mapper);

            //the ChartValues property will store our values array
            ChartValues = new ChartValues<MeasureModel>();
            ChartValues2 = new ChartValues<MeasureModel>();
            cartesianChart1.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Values = ChartValues,
                    PointGeometrySize = 18,
                    StrokeThickness = 4
                }
            };
            cartesianChart2.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Values = ChartValues2,
                    PointGeometrySize = 18,
                    StrokeThickness = 4
                }
            };
            cartesianChart1.AxisX.Add(new Axis
            {
                DisableAnimations = true,
                LabelFormatter = value => new System.DateTime((long)value).ToString("mm:ss"),
                Separator = new Separator
                {
                    Step = TimeSpan.FromSeconds(1).Ticks
                }
            });
            cartesianChart2.AxisX.Add(new Axis
            {
                DisableAnimations = true,
                LabelFormatter = value => new System.DateTime((long)value).ToString("mm:ss"),
                Separator = new Separator
                {
                    Step = TimeSpan.FromSeconds(1).Ticks
                }
            });

            SetAxisLimits(System.DateTime.Now);

            //The next code simulates data changes every 500 ms
            Timer = new Timer
            {
                Interval = 500
            };
            Timer.Tick += TimerOnTick;
            Timer.Start();
        }

        public ChartValues<MeasureModel> ChartValues { get; set; }
        public ChartValues<MeasureModel> ChartValues2 { get; set; }
        public Timer Timer { get; set; }
        public Random R { get; set; }

        private void SetAxisLimits(System.DateTime now)
        {
            cartesianChart1.AxisX[0].MaxValue = now.Ticks + TimeSpan.FromSeconds(0).Ticks; // lets force the axis to be 100ms ahead
            cartesianChart1.AxisX[0].MinValue = now.Ticks - TimeSpan.FromSeconds(50).Ticks; //we only care about the last 8 seconds
            cartesianChart2.AxisX[0].MaxValue = now.Ticks + TimeSpan.FromSeconds(0).Ticks; // lets force the axis to be 100ms ahead
            cartesianChart2.AxisX[0].MinValue = now.Ticks - TimeSpan.FromSeconds(50).Ticks; //we only care about the last 8 seconds
        }

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            var now = System.DateTime.Now;      
            double number;
            if (double.TryParse(serialPort1.ReadLine(), out number))
            {
                ChartValues.Add(new MeasureModel
                {
                    DateTime = now,
                    Value = number
                });
            }

            //Testing code:
            /*
            Random R = new Random();
            ChartValues.Add(new MeasureModel
            {
                DateTime = now,
                Value = R.Next()
            });
            */

            SetAxisLimits(now);

            //lets only use the last 30 values
            //if (ChartValues.Count > 150) ChartValues.RemoveAt(0);

            _netMqListener.Update();
            Debug.Print(diameter.ToString());
            ChartValues2.Add(new MeasureModel
            {
                DateTime = now,
                Value = diameter
            });
        }

        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            /*
            var now = System.DateTime.Now;
            double number;
            if (double.TryParse(serialPort1.ReadLine(), out number))
            {
                ChartValues.Add(new MeasureModel
                {
                    DateTime = now,
                    Value = number
                });
            }            
            
            //SetAxisLimits(now);

            //lets only use the last 30 values
            if (ChartValues.Count > 30) ChartValues.RemoveAt(0);
            */
        }
    }

    public class MeasureModel
    {
        public System.DateTime DateTime { get; set; }
        public double Value { get; set; }
    }

    public class NetMqListener
    {
        private readonly System.Threading.Thread _listenerWorker;

        private bool _listenerCancelled;

        public delegate void MessageDelegate(string message);

        private readonly MessageDelegate _messageDelegate;

        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messageQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();

        private void ListenerWork()
        {
            AsyncIO.ForceDotNet.Force();
            using (var subSocket = new SubscriberSocket())
            {
                subSocket.Options.ReceiveHighWatermark = 1000;
                subSocket.Connect("tcp://localhost:12345");
                subSocket.Subscribe("");
                while (!_listenerCancelled)
                {
                    string frameString;
                    if (!subSocket.TryReceiveFrameString(out frameString)) continue;
                    _messageQueue.Enqueue(frameString);
                }
                subSocket.Close();
            }
            NetMQConfig.Cleanup();
        }

        public void Update()
        {
            while (!_messageQueue.IsEmpty)
            {
                string message;
                if (_messageQueue.TryDequeue(out message))
                {
                    _messageDelegate(message);
                }
                else
                {
                    break;
                }
            }
        }

        public NetMqListener(MessageDelegate messageDelegate)
        {
            _messageDelegate = messageDelegate;
            _listenerWorker = new System.Threading.Thread(ListenerWork);
        }

        public void Start()
        {
            _listenerCancelled = false;
            _listenerWorker.Start();
        }

        public void Stop()
        {
            _listenerCancelled = true;
            _listenerWorker.Join();
        }
    }

    public class PupilReceiver
    {
        public float thresholdForTemp;
        private NetMqListener _netMqListener;

        private void HandleMessage(string message)
        {
            string[] parsedMessage = message.Split(',');
            float diameter = float.Parse(parsedMessage[1]);
            Debug.Print(diameter.ToString());
        }

        private void Start()
        {
            _netMqListener = new NetMqListener(HandleMessage);
            _netMqListener.Start();
        }

        private void Update()
        {
            _netMqListener.Update();
        }

        private void OnDestroy()
        {
            _netMqListener.Stop();
        }
    }
}
