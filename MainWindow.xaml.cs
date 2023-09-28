using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Threading;
using Tako.CRC;
using System.Configuration;
using System.IO;
using Path = System.IO.Path;

namespace RS485传感器配置工具
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GetSerialPort();
            Set_Baud.Items.Clear();
            string[] Baud = { "2400", "4800", "9600" };
            foreach (var set in Baud)
            {
                Set_Baud.Items.Add(set);
            }
            DirectoryInfo directory = new DirectoryInfo("./img");
            var FileInfo = directory.GetFiles();
            foreach (var set in FileInfo)
            {
                BG_Image_List.Items.Add(set.Name);
            }
            string DfImgPath = "img/" + ConfigurationManager.AppSettings["ImgBG"];
            if (File.Exists(DfImgPath))
            {
                BG.ImageSource = new BitmapImage(new Uri(DfImgPath,UriKind.RelativeOrAbsolute));
            }
            else
            {
                BG.ImageSource = new BitmapImage(new Uri("img/[5731]すやすや-59652188.png",UriKind.RelativeOrAbsolute));
            }
            btn_Read.IsEnabled = false;
        }

        void GetSerialPort()
        {
            SerialPort_CB.Items.Clear();
            string[] serialPort = SerialPort.GetPortNames();
            foreach (var set in serialPort)
            {
                SerialPort_CB.Items.Add(set);
            }
        }
        private void btn_rsfs_Click(object sender, RoutedEventArgs e)
        {
            GetSerialPort();
        }
        SerialPort serialPort;
        private void btn_oc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SerialPort_CB.SelectedIndex != -1)
                {
                    if (btn_oc.Content.ToString() == "打开串口")
                    {
                        btn_oc.Content = "关闭串口";
                        btn_Read.IsEnabled = true;
                        serialPort = new SerialPort()
                        {
                            PortName = SerialPort_CB.SelectedValue.ToString(),
                            BaudRate = 2400,
                        };
                        serialPort.Open();
                        serialPort.DataReceived += SerialPort_DataReceived;
                    }
                    else
                    {
                        btn_oc.Content = "打开串口";
                        btn_Read.IsEnabled = false;
                        if (serialPort != null)
                        {
                            serialPort.DataReceived -= SerialPort_DataReceived;
                            serialPort.Close();
                            serialPort.Dispose();
                            serialPort = null;
                        }
                    }
                }
                else
                {
                    MessageBox.Show("未选择串口", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        byte DeviceAddress = 0;
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int len = serialPort.BytesToRead;
                if (ReadMode == true)
                {
                    byte[] Recv_RM = new byte[len];
                    serialPort.Read(Recv_RM, 0, len);
                    if (Recv_RM[1] == 03 && Recv_RM[2] == 02)
                    {
                        ReadMode = false;
                        DeviceAddress = Recv_RM[0];
                        Dispatcher.Invoke(() => { Read_Address.Text = DeviceAddress.ToString(); });

                        string RecvMsgStr = "回复指令";
                        foreach (var set in Recv_RM)
                        {
                            RecvMsgStr += set.ToString("X2") + " ";
                        }
                        Dispatcher.Invoke(() => { Log.Text += RecvMsgStr + "\n"; });
                    }
                }
                else
                {
                    byte[] Recv = new byte[len];
                    serialPort.Read(Recv, 0, len);
                    string RecvStr = "接收:";
                    foreach (var set in Recv)
                    {
                        RecvStr += set.ToString("X2") + " ";
                    }
                    Dispatcher.Invoke(() => { Log.Text += RecvStr + "\n"; });
                    if (Recv[1] == 0x06 && Recv[3] == 0xD0)
                    {
                        Thread.Sleep(1000);
                        string BaudTips = "发送波特率修改指令";
                        foreach (var set in SetBaudMsg)
                        {
                            BaudTips += set.ToString("X2") + " ";
                        }
                        BaudTips += "\n";
                        Dispatcher.Invoke(() =>
                        {
                            Log.Text += BaudTips;
                        });
                        serialPort.Write(SetBaudMsg, 0, SetBaudMsg.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        byte[] AddressRead = { 0xFF, 0x03, 0x07, 0xD0, 0x00, 0x01, 0x91, 0x59 };
        byte[] BaudRead = { 0xFF, 0x03, 0x07, 0xD1, 0x00, 0x01, 0xC0, 0x99 };
        bool ReadMode = false;
        int BaudFlag = 0;
        Thread BaudScan;
        private void btn_Read_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BaudFlag = 0;
                ReadMode = true;
                btn_Read.IsEnabled = false;
                BaudScan = new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(500);
                        if (ReadMode == false)
                        {
                            Dispatcher.Invoke(() => { btn_Read.IsEnabled = true; });
                            BaudScan.Abort();
                        }
                        BaudFlag++;
                        Dispatcher.Invoke(() =>
                        {
                            switch (BaudFlag)
                            {
                                case 1:
                                    serialPort.BaudRate = 2400;
                                    Read_Baud.Text = serialPort.BaudRate.ToString();
                                    Log.Text += "发送查询指令(2400): FF 03 07 D0 00 01 91 59 \n";
                                    serialPort.Close();
                                    serialPort.Open();
                                    serialPort.Write(BaudRead, 0, BaudRead.Length);
                                    break;
                                case 2:
                                    serialPort.BaudRate = 4800;
                                    Read_Baud.Text = serialPort.BaudRate.ToString();
                                    Log.Text += "发送查询指令(4800): FF 03 07 D0 00 01 91 59 \n";
                                    serialPort.Close();
                                    serialPort.Open();
                                    serialPort.Write(BaudRead, 0, BaudRead.Length);
                                    break;
                                case 3:
                                    serialPort.BaudRate = 9600;
                                    Read_Baud.Text = serialPort.BaudRate.ToString();
                                    Log.Text += "发送查询指令(9600): FF 03 07 D0 00 01 91 59 \n";
                                    serialPort.Close();
                                    serialPort.Open();
                                    serialPort.Write(BaudRead, 0, BaudRead.Length);
                                    break;
                                case 4:
                                    var MB = MessageBox.Show("识别波特率失败,是否继续？", "错误", MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.Yes);
                                    if (MB == MessageBoxResult.Yes)
                                    {
                                        BaudFlag = 0;
                                    }
                                    else
                                    {
                                        ReadMode = false;
                                    }
                                    break;
                            }
                        });
                    }
                })
                {
                    IsBackground = true
                };
                BaudScan.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "异常", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
        }
        byte[] CRC_Value = new byte[2];
        byte[] SetAddressMsg = new byte[8];
        byte[] SetBaudMsg = new byte[8];
        private void btn_set_Click(object sender, RoutedEventArgs e)
        {
            if (Set_Baud.SelectedIndex != -1 && Set_Address.Text != "")
            {
                try
                {
                    byte Setting_Address;
                    byte Setting_Baud = 0x00;
                    if (Set_Baud.SelectedValue.ToString() == "2400")
                    {
                        Setting_Baud = 0x00;
                    }
                    else if (Set_Baud.SelectedValue.ToString() == "4800")
                    {
                        Setting_Baud = 0x01;
                    }
                    else if (Set_Baud.SelectedValue.ToString() == "9600")
                    {
                        Setting_Baud = 0x02;
                    }
                    CRCManager crc = new CRCManager();
                    var ModBusCRC = crc.CreateCRCProvider(EnumCRCProvider.CRC16Modbus);
                    Setting_Address = Convert.ToByte(Set_Address.Text,16);
                    
                    byte[] SetBaudHalfMsg = { Setting_Address , 0x06 , 0x07 , 0xD1 , 0x00 , Setting_Baud};
                    string SABHMStr = ModBusCRC.BytesToHexString(SetBaudHalfMsg);
                    var BAS= ModBusCRC.GetCRC(SABHMStr);
                    CRC_Value = BAS.CrcArray;
                    SetBaudMsg[0] = SetBaudHalfMsg[0];
                    SetBaudMsg[1] = SetBaudHalfMsg[1];
                    SetBaudMsg[2] = SetBaudHalfMsg[2];
                    SetBaudMsg[3] = SetBaudHalfMsg[3];
                    SetBaudMsg[4] = SetBaudHalfMsg[4];
                    SetBaudMsg[5] = SetBaudHalfMsg[5];
                    SetBaudMsg[6] = CRC_Value[1];
                    SetBaudMsg[7] = CRC_Value[0];
                    byte[] SetAddressHalfMsg = { DeviceAddress, 0x06, 0x07, 0xD0, 0x00, Setting_Address };
                    string SAHMStr = ModBusCRC.BytesToHexString(SetAddressHalfMsg);
                    var CSCSTA = ModBusCRC.GetCRC(SAHMStr);
                    CRC_Value = CSCSTA.CrcArray;
                    SetAddressMsg[0] = SetAddressHalfMsg[0];
                    SetAddressMsg[1] = SetAddressHalfMsg[1];
                    SetAddressMsg[2] = SetAddressHalfMsg[2];
                    SetAddressMsg[3] = SetAddressHalfMsg[3];
                    SetAddressMsg[4] = SetAddressHalfMsg[4];
                    SetAddressMsg[5] = SetAddressHalfMsg[5];
                    SetAddressMsg[6] = CRC_Value[1];
                    SetAddressMsg[7] = CRC_Value[0];
                    string BaudTips = "发送地址修改指令";
                    foreach (var set in SetAddressMsg)
                    {
                        BaudTips += set.ToString("X2") + " ";
                    }
                    BaudTips += "\n";
                    Log.Text += BaudTips;
                    serialPort.Write(SetAddressMsg, 0, SetAddressMsg.Length);    
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message, "发生了异常", MessageBoxButton.OK, MessageBoxImage.Stop);
                }
            }
            else
            {
                MessageBox.Show("波特率与地址不能为空","错误",MessageBoxButton.OK,MessageBoxImage.Warning);
            }
        }
        private void btn_Cls_Log_Click(object sender, RoutedEventArgs e)
        {
            Log.Text = "";
        }

        private void btn_BG_Image_Click(object sender, RoutedEventArgs e)
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings["ImgBG"].Value = BG_Image_List.SelectedValue.ToString();
            configuration.Save();
            string Path = "img/" + BG_Image_List.SelectedValue.ToString();
            BG.ImageSource = new BitmapImage(new Uri(Path, UriKind.RelativeOrAbsolute));
        }
    }
}
