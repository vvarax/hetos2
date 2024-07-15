using HET.Configurations;
using Stimulsoft.Report;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace HET
{
    public partial class PrintVideoJET : Form
    {
        int jobRunno = 0;
        int board1 = 0;
        int board2 = 0;

        int verifyBoard1 = 0;
        int verifyBoard2 = 0;


        bool IsActive = false;
        SerialPort serial;
        Guid BoardHeaderKey = Guid.Empty;
        bool IsLikeItem = false;
        bool IsSampleItem = false;
        int JobMaximumQty = 0;
        int JobNotVerifyQty = 0;
        int JobVerifyQty = 0;
        int AssemblyCurrentBoard = 0;
        string lastBarcode = "";
        string BarCodeProfile = "";
        string BarCodeDefindField = "";
        //double LastControlSerial = 0;
        private System.Windows.Forms.Timer tmResetBarcode = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer tmResetScanner = new System.Windows.Forms.Timer();
        int BranchID = 0;
        private int listenPort = 11000;

        System.IO.Ports.SerialPort SerialPort1;
        System.IO.Ports.SerialPort SerialPort2;

        System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();

        NetworkStream serverStream;

        bool IsValidVideoJETOnline = false;

        public PrintVideoJET(int branchID)
        {
            InitializeComponent();

            if (System.Configuration.ConfigurationManager.AppSettings["UdpListenPort"] != null)
            {
                listenPort = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["UdpListenPort"]);
            }

            //IsValidVideoJETOnline = PingHost("192.168.100.2", 0);


            Ping ping = new Ping();
            PingReply pingReply = ping.Send("192.168.100.2");

            if (pingReply.Status == IPStatus.Success)
            {
                IsValidVideoJETOnline = true;
                splitContainerAssembly.Visible = false;
                splitContainerVideoJET.Visible = true;
                splitContainerVideoJET.Dock = DockStyle.Fill;

                splitContainer1.Panel2Collapsed = true;
            }
            else
            {
                IsValidVideoJETOnline = false;
                splitContainerAssembly.Visible = true;
                splitContainerVideoJET.Visible = false;
                splitContainerAssembly.Dock = DockStyle.Fill;

                splitContainer1.Panel2Collapsed = false;
            }

            BranchID = branchID;

            //string code = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            StartPosition = FormStartPosition.CenterScreen;

            Common.KeepConfig();

            //for (int i = 0; i < 10; i++)
            //{
            //    string u = code.Substring(i+1, 1);


            //    if (i % 2 == 0)
            //    {
            //        AddRowToList(false, true, Common.Board.Board1, "00L20170028D00" + (i+1).ToString(), "00L7PVD0AA" + u);
            //    }
            //    else
            //    {

            //        AddRowToList(false, false, Common.Board.Board2, "00L20170028D00" + (i + 1).ToString(), "00L7PVD0AA" + u);
            //    }
            //}


            txtInputBarcode.SendToBack();
            txtFixedDate.Text = DateTime.Now.Date.ToString("yyyy-MM-dd");
            BindSerial();
            DoBindRemoteProduction();


            
            //netsh firewall add portopening UDP 11000 "HET Remote Barcode Update [UDP 11000]"
        }

        public static bool PingHost(string hostUri, int portNumber)
        {
            try
            {
                using (var client = new TcpClient(hostUri, portNumber))
                    return true;
            }
            catch (SocketException ex)
            {
                //MessageBox.Show("VideoJET not found :'" + hostUri + ":" + portNumber.ToString() + "'");
                return false;
            }
        }

        void BindSerial()
        {
            int tryCount = 0;



            var db = new DB();

            this.Text = "VideoJET (v1.2023.01.27), Server: " + db.Connection.DataSource + ", Database: " + db.Connection.Database;

            var com = from c in db.ComportProfiles
                      where (c.Type == "A" || c.Type == "B") && c.Computer.ComputerName == Environment.GetEnvironmentVariable("COMPUTERNAME")

                      select c;


            string errorDisplayEN = "";
            string errorDisplayTH = "";


            if (this.SerialPort1 != null)
            {
                tryCount = 0;
                while (SerialPort1.IsOpen || tryCount == 10)
                {
                    Application.DoEvents();
                    SerialPort1.Close();
                    tryCount++;
                }

                SerialPort1.Dispose();
            }


            var port1 = com.FirstOrDefault(f => f.Type == "A");

            if (port1 != null)
            {
                this.SerialPort1 = new SerialPort(port1.PortName, 9600, Parity.None, 8, StopBits.One);

                try
                {
                    this.SerialPort1.RtsEnable = true;
                    this.SerialPort1.Open();
                    this.SerialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.SerialPort1_DataReceived);
                }
                catch (Exception ex)
                {
                    errorDisplayEN += "Comport A not avlailable\n";
                    errorDisplayTH += "คอมพอร์ต A ไม่สามารถใช้งานได้\n";
                }

            }

            if (this.SerialPort2 != null)
            {
                tryCount = 0;
                while (SerialPort2.IsOpen || tryCount == 10)
                {
                    Application.DoEvents();
                    SerialPort2.Close();
                    tryCount++;
                }
                SerialPort2.Dispose();
            }

            var port2 = com.FirstOrDefault(f => f.Type == "B");

            if (port2 != null)
            {
                this.SerialPort2 = new SerialPort(port2.PortName, 9600, Parity.None, 8, StopBits.One);
                //this.SerialPort2.ReceivedBytesThreshold = 1;
                //this.SerialPort2.RtsEnable = true;
                try
                {
                    this.SerialPort2.RtsEnable = true;
                    this.SerialPort2.Open();
                    this.SerialPort2.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.SerialPort2_DataReceived);
                }
                catch (Exception ex)
                {
                    errorDisplayEN += "Comport B not avlailable\n";
                    errorDisplayTH += "คอมพอร์ต B ไม่สามารถใช้งานได้\n";
                }

            }

            if (port1 == null || port2 == null)
            {
                errorDisplayEN = "";
                errorDisplayTH = "";
                if (port1 == null)
                {
                    errorDisplayEN += "Comport A not avlailable\n";
                    errorDisplayTH += "คอมพอร์ต A ไม่สามารถใช้งานได้\n";
                }
                if (port2 == null)
                {
                    errorDisplayEN += "Comport B not avlailable\n";
                    errorDisplayTH += "คอมพอร์ต B ไม่สามารถใช้งานได้\n";
                }

            }

            if (errorDisplayEN != "")
            {
                var message = new MessageDialog("Important Note", errorDisplayEN, errorDisplayTH, MessageDialog.IconType.Error);
                message.ShowDialog(this);


                errorDisplayEN = "";
                errorDisplayTH = "";
            }

        }

        private void SerialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            AssemblyCurrentBoard = 0;
            SerialPort sp = (SerialPort)sender;
            Thread.Sleep(500);
            string indata = sp.ReadExisting();
             

            if ((indata == "1" || indata == "2") && !IsValidVideoJETOnline)
            {
                if (txtJobNo.Enabled)
                {
                    // MessageBox.Show("Please brows active job.", "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    return;
                }

                Invoke(new Action(() =>
                {
                    //AddRowToList(false, false, Common.Board.Board1);
                    if (indata == "1" && !txtEmployee1.Enabled)
                    {
                        AssemblyCurrentBoard = 1;
                        imageStatusInputAss.Image = Properties.Resources.online_state;
                    }
                    else if (indata == "2" && !txtEmployee2.Enabled)
                    {
                        AssemblyCurrentBoard = 2;
                        imageStatusInputAss.Image = Properties.Resources.online_state;
                    }
                    else
                    {
                        var message = new MessageDialog("Important Note", "Employee " + indata + " not loged-in", "กรุณาเข้าระบบด้วยรหัสพนักงาน " + indata, MessageDialog.IconType.Error);
                        message.ShowDialog(this);
                        AssemblyCurrentBoard = 0;
                    }


                   
                }));

            }
            else //VDOJET
            {

                if (indata != "ERROR")
                {
                    Invoke(new Action(() =>
                    {

                        //listBox1.Items.Add("*" + indata);
                        if (indata.Substring(0, 2) == "OK" || indata.Substring(0, 2) == "ER")
                        {

                        }
                        else
                        {
                            imageStatusInput.Image = Properties.Resources.online_state;
                            SetTimeoutBarcode(5000);


                             
                            string cmd = "";
                            byte[] command;
                            

                            try
                            {
                                var db = new DB();

                                var f = db.BarcodeDetails.First(fx => fx.BoardDetail.BoardKey == BoardHeaderKey && fx.BarcodeData == indata);

                                //Can not read from scan
                                if(!f.IsCancel)
                                {

                                    cmd = "ALLOFF\r";

                                    command = ASCIIEncoding.ASCII.GetBytes(cmd);
                                    SerialPort1.Write(command, 0, command.Length);
                                    System.Threading.Thread.Sleep(5);

                                    cmd = "\nOUTON,3\r";
                                    command = ASCIIEncoding.ASCII.GetBytes(cmd);
                                    SerialPort1.Write(command, 0, command.Length);
                                    System.Threading.Thread.Sleep(5);

                                    cmd = "ALLOFF\r";
                                    command = ASCIIEncoding.ASCII.GetBytes(cmd);
                                    SerialPort1.Write(command, 0, command.Length);
                                    System.Threading.Thread.Sleep(5);

                                  

                                    JobNotVerifyQty++;



                                if (barcode.Count(c => !c.IsVerify && c.IsPrint && !c.IsCancel) > 0)
                                { 
                                    var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nTEST 2 LOC 357", txtInputBarcode.Text.ToUpper(), MessageDialog.IconType.Error);
                                        message.ShowDialog(this);
                                    var fx = barcode.First();
                                    var v = db.BarcodeDetails.First(f => f.RowKey == fx.RowKey && f.BoardDetailKey == fx.BoardDetailKey);

                                    if (IsValidVideoJETOnline)
                                    {
                                        var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nBarcode exists in this job or duplicate LOC 364", txtInputBarcode.Text.ToUpper() + "\nบาร์โค้ดนี้รับเข้าระบบแล้ว หรือมีบาร์โค้ดซ้ำ", MessageDialog.IconType.Error);
                                        message.ShowDialog(this);

                                    }
                                }
                                    if (JobVerifyQty >= JobMaximumQty)
                                    {
                                        cmd = "\nOUTON,1\r";
                                        command = ASCIIEncoding.ASCII.GetBytes(cmd);
                                        SerialPort1.Write(command, 0, command.Length);
                                        System.Threading.Thread.Sleep(5);

                                        cmd = "ALLOFF\r";
                                        command = ASCIIEncoding.ASCII.GetBytes(cmd);
                                        SerialPort1.Write(command, 0, command.Length);
                                        System.Threading.Thread.Sleep(5);
                                    }

                                   // this.lbJobNotVerifyCount.Text = "Not Verified: " + JobNotVerifyQty.ToString("#,##0");
                                   
                                }
                                else
                                {

                                  

                                    f.IsCancel = false;

                                    db.SubmitChanges();

                                    JobVerifyQty++;

                                    cmd = "ALLOFF\r";

                                    command = ASCIIEncoding.ASCII.GetBytes(cmd);
                                    SerialPort1.Write(command, 0, command.Length);
                                    System.Threading.Thread.Sleep(5);


                                    cmd = "\nOUTON,4\r";
                                    command = ASCIIEncoding.ASCII.GetBytes(cmd);
                                    SerialPort1.Write(command, 0, command.Length);
                                    System.Threading.Thread.Sleep(10);


                                    cmd = "ALLOFF\r";
                                    command = ASCIIEncoding.ASCII.GetBytes(cmd);
                                    SerialPort1.Write(command, 0, command.Length);

                                    //this.lbJobVerifyCount.Text = "Verified: " + JobVerifyQty.ToString("#,##0");
                                    //lbJobCount.Text = "Total: " + JobVerifyQty.ToString("#,##0") + " of " + JobMaximumQty.ToString("#,##0");
                                    this.lbPrintingBarcode.Text = indata;
                                }

                                

                                UpdateList(indata, Common.Board.Board1);
                            }
                            catch (Exception)
                            {

                                
                            }

                            lbJobCount.Text = "Total: " + JobVerifyQty.ToString("#,##0") + " of " + JobMaximumQty.ToString("#,##0");

                            //Invoke(new Action(() =>
                            //{

                            //    return;
                            //}));

                            if (IsValidVideoJETOnline && JobVerifyQty >= JobMaximumQty)
                            {
                                stateStart = false;
                                tmTrigger.Enabled = stateStart;
                                btnStart.BackColor = Color.Empty;
                                btnStart.Text = "Start";
                                lbFinish.Visible = true;
                                txtInputBarcode.Enabled = false;

                                //System.Threading.Thread.Sleep(5);
                                
                                   
                                btnStart.Image = Properties.Resources.ApproveRequest_32x32;
                                try
                                {



                                    if (clientSocket.Connected)
                                        clientSocket.Close();

                                }
                                catch (Exception)
                                {

                                }
                                UpdateQtyBroadCast();
                                var message = new MessageDialog("Finished",
                                    "Job " + this.txtJobNo.Text + " has been finished\n",
                                   "ใบงาน " + this.txtJobNo.Text + " เสร็จสิ้นแล้ว", MessageDialog.IconType.Error);
                                message.ShowDialog(this);
                                return;
                            }
                        }
                    }));
                }
                else
                {
                    Invoke(new Action(() =>
                    {
                        //listBox1.Items.Add("-" + indata);
                        string cmd = "";
                        byte[] command;

                        cmd = "ALLOFF\r";
                        command = ASCIIEncoding.ASCII.GetBytes(cmd);
                        SerialPort1.Write(command, 0, command.Length);
                        System.Threading.Thread.Sleep(5);

                        cmd = "\nOUTON,3\r";
                        command = ASCIIEncoding.ASCII.GetBytes(cmd);
                        SerialPort1.Write(command, 0, command.Length);
                        System.Threading.Thread.Sleep(5);

                        cmd = "ALLOFF\r";
                        command = ASCIIEncoding.ASCII.GetBytes(cmd);
                        SerialPort1.Write(command, 0, command.Length);
                        System.Threading.Thread.Sleep(5);


                        cmd = "\nOUTON,1\r";
                        command = ASCIIEncoding.ASCII.GetBytes(cmd);
                        SerialPort1.Write(command, 0, command.Length);
                        System.Threading.Thread.Sleep(5);

                        cmd = "ALLOFF\r";
                        command = ASCIIEncoding.ASCII.GetBytes(cmd);
                        SerialPort1.Write(command, 0, command.Length);
                        System.Threading.Thread.Sleep(5);

                        lbJobCount.Text = "Total: " + JobVerifyQty.ToString("#,##0") + " of " + JobMaximumQty.ToString("#,##0");

                        if (JobVerifyQty< JobMaximumQty)
                        {
                            JobNotVerifyQty++;
                            //this.lbJobNotVerifyCount.Text = "Not Verified: " + JobNotVerifyQty.ToString("#,##0");
                        }
                       
                    }));

                }
            }

             



            Console.WriteLine("Data Received:");
            Console.Write(indata);
        }

        private void SerialPort2_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            AssemblyCurrentBoard = 0;
            SerialPort sp = (SerialPort)sender;
            Thread.Sleep(500);
            string indata = sp.ReadExisting();


            if ((indata == "1" || indata == "2") && !IsValidVideoJETOnline)
            {
                if (txtJobNo.Enabled)
                {
                    // MessageBox.Show("Please brows active job.", "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    return;
                }

                Invoke(new Action(() =>
                {
                    //AddRowToList(false, false, Common.Board.Board1);
                    if (indata == "1" && !txtEmployee1.Enabled)
                    {
                        //AssemblyCurrentBoard = 1;
                        imageStatusInputAss.Image = Properties.Resources.online_state;
                    }
                    else if (indata == "2" && !txtEmployee2.Enabled)
                    {
                        //AssemblyCurrentBoard = 2;
                        imageStatusInputAss.Image = Properties.Resources.online_state;
                    }
                    else
                    {
                        var message = new MessageDialog("Important Note", "Employee " + indata + " not loged-in", "กรุณาเข้าระบบด้วยรหัสพนักงาน " + indata, MessageDialog.IconType.Error);
                        message.ShowDialog(this);
                       // AssemblyCurrentBoard = 0;
                    }



                }));

            }
            //    if (txtJobNo.Enabled)
            //    {
            //        // MessageBox.Show("Please brows active job.", "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            //        return;
            //    }


            //    if (!txtEmployee1.Enabled)
            //    {
            //        Invoke(new Action(() =>
            //        {
            //            AddRowToList(false, false, Common.Board.Board1);
            //        }));
            //    }
            //    else
            //    {
            //        Invoke(new Action(() =>
            //        {
            //            if (txtEmployee1.Enabled)
            //            {
            //                //MessageBox.Show("Employee 1 not loged-in", "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

            //                var message = new MessageDialog("Important Note", "Employee 1 not loged-in", "กรุณาเข้าระบบด้วยรหัสพนักงาน 1", MessageDialog.IconType.Error);
            //                message.ShowDialog(this);
            //            }
            //        }));

            //    }
            //}




            //Console.WriteLine("Data Received:");
            //Console.Write(indata);
        }

        private void DoBindRemoteProduction()
        {
            IPEndPoint localpt = new IPEndPoint(IPAddress.Any, listenPort);
            ThreadPool.QueueUserWorkItem(delegate (object param0)
            {
                UdpClient client = new UdpClient
                {
                    ExclusiveAddressUse = false
                };
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(localpt);
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {

                    if (txtJobNo.Enabled || txtEmployee1.Enabled || txtEmployee2.Enabled)
                    {
                        return;
                    }

                    string str;
                    MethodInvoker method = null;
                    byte[] bytes = client.Receive(ref remoteEP);
                    string command = Encoding.ASCII.GetString(bytes);
                    if (command.IndexOf(":DATA:") > 0)
                    {
                        string[] arrCommand = command.Split(':');

                        if (!IsValidVideoJETOnline)
                        {
                            if (arrCommand[2] == "NEW" && arrCommand[3].ToUpper() == this.txtJobNo.Text.ToUpper())
                            {
                                if (method == null)
                                {
                                    JobVerifyQty++;
                                    method = () =>
                                    AddRowToAllList(Convert.ToBoolean(arrCommand[4]), arrCommand[5], arrCommand[6], arrCommand[7], arrCommand[8], arrCommand[9]);


                                }
                                this.Invoke(method);
                            }
                            else if (arrCommand[2] == "UPDATE" && arrCommand[3].ToUpper() == this.txtJobNo.Text.ToUpper())
                            {
                                if (method == null)
                                {
                                    JobVerifyQty++;
                                    //this.lbJobVerifyCount.Text = "Verified: " + JobVerifyQty.ToString("#,##0");
                                    //this.lbJobNotVerifyCount.Text = "Not Verified: " + (JobMaximumQty - JobVerifyQty).ToString("#,##0");
                                    method = () =>
                                    //AddRowToAllList(Convert.ToBoolean(arrCommand[4]), arrCommand[5], arrCommand[6], arrCommand[7], arrCommand[8], arrCommand[9], arrCommand[10]);
                                    UpdateList(arrCommand[5]);
                                }
                                this.Invoke(method);
                            }
                            else if (arrCommand[2] == "QTY" && arrCommand[3].ToUpper() == this.txtJobNo.Text.ToUpper())
                            {
                                if (method == null)
                                {

                                    method = () => txtQty.Text = Convert.ToInt32(arrCommand[4]).ToString("#,##0");
                                    //AddRowToAllList(Convert.ToBoolean(arrCommand[4]), arrCommand[5], arrCommand[6], arrCommand[7], arrCommand[8], arrCommand[9], arrCommand[10]);

                                    KeepLog("UDP-IN", "DATA-QTY", command);
                                }
                                this.Invoke(method);
                            }
                        }
                    }

                }
            });

            Thread.Sleep(1000);
        }

        void UpdateList(string BarcodeData)
        {
            foreach (DataGridViewRow item in dgvBoardList.Rows)
            {
                if (item.Cells[1].Value.ToString() == BarcodeData)
                {
                    item.Cells[0].Value = imageList1.Images[1];
                    item.Cells[1].Style.BackColor = Color.Green;
                    item.Cells[1].Style.ForeColor = Color.White;
                    Application.DoEvents();
                    break;
                }
            }

            if (JobVerifyQty >= JobMaximumQty)
            {

                lbFinish.Visible = true;
                txtInputBarcode.Enabled = false;
                splitContainer1.Panel1.Enabled = false;
                splitContainer1.Panel2.Enabled = false;
                return;
            }
        }

        void UpdateList(string BarcodeData, Common.Board bNo)
        {
            foreach (DataGridViewRow item in(bNo == Common.Board.Board1 ? dgvBoard1.Rows : dgvBoard2.Rows))
            {
                if (item.Cells[2].Value.ToString() == BarcodeData.ToUpper())
                {
                    item.Cells[0].Value = imageList1.Images[1];
                    item.Cells[1].Style.BackColor = Color.Green;
                    item.Cells[1].Style.ForeColor = Color.White;
                    Application.DoEvents();
                    break;
                }
            }

            foreach (DataGridViewRow item in dgvBoardList.Rows)
            {
                if (item.Cells[1].Value.ToString() == BarcodeData)
                {
                    item.Cells[0].Value = imageList1.Images[1];
                    item.Cells[1].Style.BackColor = Color.Green;
                    item.Cells[1].Style.ForeColor = Color.White;
                    Application.DoEvents();
                    break;
                }
            }
        }
        
        //VDOJET
        void AddRowToList(bool IsNew, bool status, Common.Board bNo,  string code = "", string controlsn = "", bool test = false)
        {
            if (txtItemNo.Text.Length == 0 || !IsActive) return;

            int runno = 0;
            string maxControl = "0";
            int lastRun = 0;
            int runLength = 0;
            string runformatArchive = "";
            string runformatArchiveA = "";
            bool IsSequence = false;
            string runformatArchiveB = "";
            string sn = "";
            jobRunno = jobRunno + 1;
            string dn = ""; //Day Night


            if (IsValidVideoJETOnline && JobVerifyQty >= JobMaximumQty)
            {
                lbFinish.Visible = true;
                txtInputBarcode.Enabled = false;
                btnStart_Click(null, null);

                this.splitContainer1.Enabled = false;


                var message = new MessageDialog("Finished",
                                  "Job " + this.txtJobNo.Text + " has been finished\n",
                                 "ใบงาน " + this.txtJobNo.Text + " เสร็จสิ้นแล้ว", MessageDialog.IconType.Error);
                message.ShowDialog(this);
                return;
            }
            else if (!IsValidVideoJETOnline && JobVerifyQty >= JobMaximumQty)
            {
                lbFinish.Visible = true;
                txtInputBarcode.Enabled = false;
                this.splitContainer1.Enabled = false;

                var message = new MessageDialog("Finished",
                                  "Job " + this.txtJobNo.Text + " has been finished\n",
                                 "ใบงาน " + this.txtJobNo.Text + " เสร็จสิ้นแล้ว", MessageDialog.IconType.Error);
                message.ShowDialog(this);
                return;

            }

            var db = new DB();
            if (IsNew)
            {
               // JobVerifyQty++;


                string[] ScanArr = txtJobNo.Text.Split('-');
                int suffix = 0;
                try
                {
                    suffix = Convert.ToInt32(ScanArr[1]);
                }
                catch (Exception)
                {

                }
                 
                var runFormat = db.GetFormatBarcode(ScanArr[0], suffix, txtItemNo.Text.ToUpper(), pnlFixedDate.Visible ? txtFixedDate.Text : null);

                //maxControl = db.BarcodeDetails Where(w => w.ItemNo == txtItemNo.Text.ToUpper()).Max(m => m.ControlSerial.Substring(0));


                var hd = db.BoardHeaders.FirstOrDefault(f => f.RowKey == BoardHeaderKey);

                runLength = hd.RunLength;


                var fomatResult = runFormat.FirstOrDefault().Format.Replace("$$", "$");

                string[] barcode = fomatResult.Split('$');

                //KeepLog("\n$CONTROL", "MAX", maxControl);
                KeepLog("SQL", "fomatResult", fomatResult);
                //--C ModelCode
                //-- P PartNo
                //--L LineCode
                //-- Y Year
                //--M Month
                //-- D Day
                //--R Run
                //-- F FacCode
                //--X FIX
                //-- Z Day of Year
                //--T Time Daylight/ Nightlight
                //-- V Revision

                for (int i = 0; i < barcode.Length; i++)
                {
                    string[] ftype = barcode[i].Split('*');
                    switch (ftype[0])
                    {
                        case "C": // Model Code
                            sn += ftype[1];
                            code += ftype[1];

                            runformatArchive += ftype[1];
                            break;
                        case "P": //Part Code

                            if (hd.PartSubType == 1) //LEFT PAD == 1
                            {
                                sn += ftype[1];
                                code += (ftype[1]).Substring(0, hd.CodeLength);

                                runformatArchive += (ftype[1]).Substring(0, hd.CodeLength);
                            }
                            else //RIGHT PAD == 0
                            {

                                sn += ftype[1];
                                code += (ftype[1]).Substring((ftype[1]).Length - hd.CodeLength, hd.CodeLength);

                                runformatArchive += (ftype[1]).Substring((ftype[1]).Length - hd.CodeLength, hd.CodeLength);
                            }
                            break;
                        case "L": //Line Code
                            sn += ftype[1];
                            code += ftype[1];

                            runformatArchive += ftype[1];
                            break;
                        case "Y": //Year
                            sn += ftype[1];
                            var year = db.FormatDetails.Where(w => w.FormatSystem.Format == ftype[2] && w.FormatType == "Y" && w.Data == ftype[1]);
                            if (year.Count() > 0)
                            {
                                code += year.First().Result;

                                runformatArchive += year.First().Result;
                            }
                            else
                            {
                                code += ftype[1];

                                runformatArchive += ftype[1];
                            }



                            break;
                        case "M": //Month
                            //sn += ftype[1];
                            if (ftype[3] == "P") //Sample
                            {

                                sn += "00";
                                if (ftype[4] != "")
                                {
                                    code += ftype[4];

                                    runformatArchive += ftype[4];
                                }
                                else
                                {
                                    code += "P";

                                    runformatArchive += "P";
                                }
                            }
                            else
                            {
                                sn += ftype[1];
                                var month = db.FormatDetails.Where(w => w.FormatSystem.Format == ftype[2] && w.FormatType == "M" && w.Data == ftype[1]);
                                if (month.Count() > 0)
                                {
                                    code += month.First().Result;

                                    runformatArchive += month.First().Result;
                                }
                                else
                                {
                                    code += ftype[1];

                                    runformatArchive += ftype[1];
                                }

                            }

                            break;
                        case "D": //Day
                            sn += ftype[1];
                            var day = db.FormatDetails.Where(w => w.FormatSystem.Format == ftype[2] && w.FormatType == "D" && w.Data == ftype[1]);
                            if (day.Count() > 0)
                            {
                                code += day.First().Result;

                                runformatArchive += day.First().Result;
                            }
                            else
                            {
                                code += ftype[1];

                                runformatArchive += ftype[1];
                            }

                            break;
                        case "R": //Running
                            //sn += ftype[1];
                            //lastRun = Convert.ToInt32(maxControl.ToString().Substring(8));
                            //sn += (lastRun + 1).ToString();

                            runformatArchiveA = ftype[1];
                            runformatArchiveB = ftype[2];

                            //var run = db.FormatDetails.FirstOrDefault(w => w.FormatSystem.Format == ftype[1] && w.FormatType == "R" && Convert.ToInt32(w.Data) == (lastRun + 1));

                            //if (run != null)
                            //{
                            //    code += run.Result;
                            //}
                            //else
                            //{
                            //    if (Convert.ToInt32(ftype[2]) > 0)
                            //    {

                            //        code += (lastRun + 1).ToString(new string('0', Convert.ToInt32(ftype[2])));
                            //    }
                            //    else
                            //    {
                            //        code += (lastRun + 1).ToString("00000");
                            //    }
                            //}
                            //runno = lastRun + 1;


                            break;
                        case "F": //Factory Code
                            sn += ftype[1];
                            code += ftype[1];

                            runformatArchive += ftype[1];
                            break;
                        case "X": //Fixed
                            sn += ftype[1];
                            code += ftype[1];

                            runformatArchive += ftype[1];
                            break;
                        case "Z":
                            //sn += ftype[1];
                            //code += ftype[1];
                            dn = ftype[1];
                            runformatArchive += ftype[1];
                            break;
                        case "T": //Line Code
                            sn += ftype[1];
                            code += ftype[1];

                            runformatArchive += ftype[1];
                            break;
                        case "V": //Line Code
                            sn += ftype[1];
                            code += ftype[1];

                            runformatArchive += ftype[1];
                            break;
                        case "S": //Sequence
                            IsSequence = true;
                            break;
                        default:
                            break;
                    }
                }

                try
                {
                    var rn = new RunNo()
                    {
                        Format = runformatArchive,
                        ModifyDate = DateTime.Now,
                        RunNo1 = 1,
                        IsSample = IsSampleItem,
                        SeqChar = IsSequence ? "A" : null
                    };

                    db.RunNos.InsertOnSubmit(rn);
                    db.SubmitChanges();

                    lastRun = 1;




                    var run = db.FormatDetails.FirstOrDefault(w => w.FormatSystem.Format == runformatArchiveA && w.FormatType == "R" && Convert.ToInt32(w.Data) == (lastRun));

                    if (run != null)
                    {
                        code += run.Result + (IsSequence ? rn.SeqChar : "") + dn;
                    }
                    else
                    {
                        if (Convert.ToInt32(runformatArchiveB) > 0)
                        {

                            code += (lastRun).ToString(new string('0', Convert.ToInt32(runformatArchiveB))) + (IsSequence ? rn.SeqChar : "") + dn;
                        }
                        else
                        {
                            code += (lastRun).ToString("00000") + (IsSequence ? rn.SeqChar : "") + dn;
                        }
                    }


                }
                catch (Exception)
                {
                    try
                    {
                        db = new DB();
                        var rn = db.RunNos.First(f => f.Format == runformatArchive && f.IsSample == IsSampleItem);

                        string _tmpRunLength = rn.RunNo1.ToString("0000000000");
                        string _tmpCompare = ("9999999999").Substring(0, runLength);

                        if (_tmpRunLength.IndexOf(_tmpCompare) > 0)
                        {
                            rn.RunNo1 = 1;
                            lastRun = 1;
                        }
                        else
                        {
                            rn.RunNo1 = rn.RunNo1 + 1;
                            lastRun = rn.RunNo1;
                        }

                        rn.ModifyDate = DateTime.Now;
                        db.SubmitChanges();

                        var dbx = new DB();
                        var rnNew = dbx.RunNos.First(f => f.Format == runformatArchive && f.IsSample == IsSampleItem);

                        var run = db.FormatDetails.FirstOrDefault(w => w.FormatSystem.Format == runformatArchiveA && w.FormatType == "R" && Convert.ToInt32(w.Data) == (rn.RunNo1));

                        if (run != null)
                        {
                            code += run.Result + (IsSequence ? rnNew.SeqChar : "") + dn;
                        }
                        else
                        {
                            if (Convert.ToInt32(runformatArchiveB) > 0)
                            {

                                code += (lastRun).ToString(new string('0', Convert.ToInt32(runformatArchiveB))) + (IsSequence ? rnNew.SeqChar : "") + dn;
                            }
                            else
                            {
                                code += (lastRun).ToString("00000") + (IsSequence ? rnNew.SeqChar : "") + dn;
                            }
                        }

                    }
                    catch (Exception)
                    {

                        return;
                    }
                }
            }
             

            switch (bNo)
            {
                case Common.Board.Board1:
                    board1++;

                    dgvBoard1.Rows.Add(status ? imageList1.Images[1] : imageList1.Images[0], (lastRun == 0 && controlsn != "" ? controlsn : jobRunno.ToString("00000")), code);
                    //dgvBoardList.Rows.Add(status ? imageList1.Images[1] : imageList1.Images[0], sn, code);

                    AddRowToAllList(status, code, lastRun == 0 && controlsn != "" ? controlsn : (pnlFixedDate.Visible ? Convert.ToDateTime(txtFixedDate.Text).Date.ToString("yyyyMMdd") : DateTime.Now.ToString("yyyyMMdd")) + lastRun.ToString("00000"), Environment.GetEnvironmentVariable("COMPUTERNAME"), "1", this.txtEmployee1.Text.ToString(), jobRunno.ToString("00000"));

                   


                    //dgvBoard1.Rows[dgvBoard1.Rows.Count - 1].Cells[0].Selected = true;
                    dgvBoard1.Rows[dgvBoard1.Rows.Count - 1].Cells[1].Style.BackColor = Color.FromArgb(255, 255, 192);
                    dgvBoard1.Rows[dgvBoard1.Rows.Count - 1].Cells[1].Style.ForeColor = Color.Red;
                    txtTotalBoard1.Text =  board1.ToString("#,##0");

                    dgvBoard1.Sort(gdcSerial1, ListSortDirection.Descending);
                    if (dgvBoard1.Rows.Count >= 15)
                    {
                        dgvBoard1.Rows.RemoveAt(dgvBoard1.Rows.Count - 1);
                    }

                    break;

                case Common.Board.Board2:
                    board2++;

                    dgvBoard2.Rows.Add(status ? imageList1.Images[1] : imageList1.Images[0], (lastRun == 0 && controlsn != "" ? controlsn : jobRunno.ToString("00000")), code);
                    //dgvBoardList.Rows.Add(status ? imageList1.Images[1] : imageList1.Images[0], sn, code);
                    AddRowToAllList(status, code, lastRun == 0 && controlsn != "" ? controlsn : (pnlFixedDate.Visible ? Convert.ToDateTime(txtFixedDate.Text).Date.ToString("yyyyMMdd") : DateTime.Now.ToString("yyyyMMdd")) + lastRun.ToString("00000"), Environment.GetEnvironmentVariable("COMPUTERNAME"), "2", this.txtEmployee2.Text.ToString(), jobRunno.ToString("00000"));

                    dgvBoardList.Refresh();


                    //dgvBoard1.Rows[dgvBoard1.Rows.Count - 1].Cells[0].Selected = true;
                    dgvBoard2.Rows[dgvBoard2.Rows.Count - 1].Cells[1].Style.BackColor = Color.FromArgb(255, 255, 192);
                    dgvBoard2.Rows[dgvBoard2.Rows.Count - 1].Cells[1].Style.ForeColor = Color.Red;
                    txtTotalBoard2.Text =  board2.ToString("#,##0");

                    //dgvBoard2.Sort(gdcSerial1, ListSortDirection.Descending);
                    //if (dgvBoard2.Rows.Count >= 10)
                    //{
                    //    dgvBoard2.Rows.RemoveAt(dgvBoard2.Rows.Count - 1);
                    //}

                    break;
                default:
                    break;
            }




            if (IsNew)
            {
                lastBarcode = code;

                lbBarcode1.Text = code;
               


                var bdetail = new BarcodeDetail
                {
                    RowKey = Guid.NewGuid(),
                    BoardDetailKey = new Guid(txtItemNo.Tag.ToString()),
                    ItemNo = txtItemNo.Text.ToUpper(),
                    //ControlSerial = (Convert.ToInt32(maxControl) + 1).ToString(),
                    ControlSerial = (IsSampleItem ? (pnlFixedDate.Visible ? Convert.ToDateTime(txtFixedDate.Text).Date.ToString("yyyy00dd") : DateTime.Now.ToString("yyyy00dd")) : (pnlFixedDate.Visible ? Convert.ToDateTime(txtFixedDate.Text).Date.ToString("yyyyMMdd") : DateTime.Now.ToString("yyyyMMdd"))) + lastRun.ToString("00000"),
                    BarcodeData = code,
                    EmpID = txtEmployee1.Tag.ToString(),
                    BoardID = (bNo == Common.Board.Board1 ? 1 : 2).ToString(),
                    CreateDate = DateTime.Now,
                    ModifyDate = DateTime.Now,
                    ComName_Ass = Environment.GetEnvironmentVariable("COMPUTERNAME"),
                    //EmpID_Ass = "0",
                    //BoardID_Ass = (bNo == Common.Board.Board1 ? 1 : 2).ToString(),
                    //Date_Ass = DateTime.Now,
                    IsPrint = true,
                    IsVerify = false,
                    IsCancel = true,
                    Remark = null


                };


                try
                {

                    db.BarcodeDetails.InsertOnSubmit(bdetail);
                    db.SubmitChanges();


                    //NewBarcode(txtItemNo.Text, txtEmp1.Text, txtBoard1.Text)
                    // SendData(Convert.ToChar(2) + "GA" + Convert.ToChar((UInt32)3) + Convert.ToChar(13));
                    SendData(string.Format("{0}{1}{2}{3}", Convert.ToChar(2), BarCodeProfile, Convert.ToChar(3), Convert.ToChar(13)));

                    //GetData();
                    //if(BarCodeDefindField!=null && BarCodeDefindField.Length > 1)
                    //{

                   
                    SendData(string.Format("{0}{1}{2}{3}{4}{5}", Convert.ToChar(2), "UFIELD01", Convert.ToChar(10), code.ToUpper(), Convert.ToChar(3), Convert.ToChar(13)));
                    //SendData(string.Format("{0}{1}{2}{3}{4}{5}", Convert.ToChar(2), "UFIELD02", Convert.ToChar(10), lbState.Text, Convert.ToChar(3), Convert.ToChar(13)));
                    //}

                    if (BarCodeDefindField != null && BarCodeDefindField.Length > 1)
                    {


                        SendData(string.Format("{0}{1}{2}{3}{4}{5}", Convert.ToChar(2), "UFIELD02", Convert.ToChar(10), BarCodeDefindField.ToUpper(), Convert.ToChar(3), Convert.ToChar(13)));
                        //SendData(string.Format("{0}{1}{2}{3}{4}{5}", Convert.ToChar(2), "UFIELD02", Convert.ToChar(10), lbState.Text, Convert.ToChar(3), Convert.ToChar(13)));
                    }

                    try
                    {

                        if(!IsValidVideoJETOnline)
                        {
                            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                            string sendCommand = "HET:DATA:NEW:" + this.txtJobNo.Text.ToUpper() + ":FALSE:" + code + ":" + (lastRun == 0 && controlsn != "" ? controlsn : (pnlFixedDate.Visible ? Convert.ToDateTime(txtFixedDate.Text).Date.ToString("yyyyMMdd") : DateTime.Now.ToString("yyyyMMdd")) + runno.ToString("00000")) + ":" + Environment.GetEnvironmentVariable("COMPUTERNAME") + ":" + (bNo == Common.Board.Board1 ? "1" : "2") + ":" + this.txtEmployee1.Tag.ToString();

                            byte[] sendbuf = Encoding.ASCII.GetBytes(sendCommand);

                            foreach (var item in db.Computers.Where(w => w.IsValid && w.ComputerName != Environment.GetEnvironmentVariable("COMPUTERNAME") && w.ActiveJobNo == this.txtJobNo.Text.ToUpper()))
                            {
                                IPAddress broadcast = IPAddress.Parse(item.IPAddress);
                                IPEndPoint ep = new IPEndPoint(broadcast, 11000);
                                s.SendTo(sendbuf, ep);
                            }

                        }
                        
                        Console.WriteLine("Message sent to the broadcast address");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Udp:" + ex.Message);
                    }
                    


                    this.lbPrintingBarcode.ForeColor = Color.Empty;
                    this.lbPrintingBarcode.BackColor = Color.Empty;




                }
                catch (Exception ex)
                {

                    switch (bNo)
                    {
                        case Common.Board.Board1:
                            dgvBoard1.Rows.RemoveAt(dgvBoard1.Rows.Count - 1);
                            board1--;
                            txtTotalBoard1.Text = board1.ToString("#,##0");
                            break;

                        case Common.Board.Board2:
                            dgvBoard2.Rows.RemoveAt(dgvBoard2.Rows.Count - 1);
                            board2--;
                            txtTotalBoard2.Text = board2.ToString("#,##0");
                            break;
                        default:
                            break;
                    }
                    this.lbPrintingBarcode.ForeColor = Color.White;
                    this.lbPrintingBarcode.BackColor = Color.Red;
                    this.lbPrintingBarcode.Text = "DUPLICATE: " + code;
                }
            }

        }

        //void AddRowToAllList(bool status,  string code = "", string controlsn = "", string comname = "", string boardNo = "", string employeeNo = "")
        //{
        //    dgvBoardList.Rows.Add(status ? imageList1.Images[1] : imageList1.Images[0], code, controlsn, comname, boardNo, employeeNo);
        //    dgvBoardList.Sort(gdcControlSerialAll, ListSortDirection.Descending);

        //    dgvBoardList.Rows[0].Cells[0].Selected = true;

        //    lbJobCount.Text = "Total: " + JobCurrentQty.ToString("#,##0") + " of " + JobMaximumQty.ToString("#,##0");
        //}

        // Assemby
        void AddRowToAllList(bool status, string code = "", string controlsn = "", string comname = "", string boardNo = "", string employeeNo = "", string listNo = "", bool sendBroadCast = true)
        {
            //dgvBoardList.Rows.Add(status ? imageList1.Images[1] : imageList1.Images[0], code, employeeNo, boardNo, comname, listNo);
            //dgvBoardList.Sort(gdcAllListIndex, ListSortDirection.Descending);

            //dgvBoardList.Rows[0].Cells[0].Selected = true;

            //lbJobCount.Text = "Total: " + JobVerifyQty.ToString("#,##0") + " of " + JobMaximumQty.ToString("#,##0");

            if (listNo == "")
            {
                listNo = dgvBoardList.Rows.Count.ToString("00000000");
            }

            int row = dgvBoardList.Rows.Add(status ? imageList1.Images[1] : imageList1.Images[0], code, employeeNo, boardNo, comname, listNo);

           
            if (status)
            {
                dgvBoardList.Rows[row].Cells[1].Style.BackColor = Color.Green;
                dgvBoardList.Rows[row].Cells[1].Style.ForeColor = Color.White;
            }

            dgvBoardList.Sort(gdcAllListIndex, ListSortDirection.Descending);

            dgvBoardList.Rows[0].Cells[0].Selected = true;

             
            if (sendBroadCast)
            {
                UpdateQtyBroadCast();
            }
        }
        private void frmPrintLabel_Resize(object sender, EventArgs e)
        {

        }

        private bool PrintBarCode(string reportfile, string barcode, bool test)
        {
            if (!System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports\\" + reportfile)))
            {
                //MessageBox.Show(, "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

                var message = new MessageDialog("Important Note",
                    "No barcode file name specified\n" + System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports\\" + reportfile),
                    "ไม่พบไฟล์รายงาน สำหรับบาร์โค้ดนี้\n" + System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports\\" + reportfile),
                    MessageDialog.IconType.Error);
                message.ShowDialog(this);

                return false;
            }
            var report = new StiReport();
            report.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports\\" + reportfile));
            report.Compile();
            if (report.IsCompiled)
            {

                (report.CompiledReport.Dictionary.Databases[0] as Stimulsoft.Report.Dictionary.StiSqlDatabase).ConnectionString = HET.Properties.Settings.Default.HETDBProduction;

                report.CompiledReport.DataSources["DsBarcodeDaily"].Parameters["@BarcodeData"].ParameterValue = barcode;


                report.Print(test);
            }
           
            return true;
        }

        void SetTimeoutBarcode(int interval)
        {
            //tmReset = new System.Windows.Forms.Timer();
            imgPrintBarcode.Visible = true;
            tmResetBarcode.Interval = interval;
            tmResetBarcode.Enabled = true;
            tmResetBarcode.Tick += TmResetBarcode_Tick;
        }
        private void TmResetBarcode_Tick(object sender, EventArgs e)
        {
            imageStatusInput.Image = Properties.Resources.offline_state;
            imageStatusInputAss.Image = Properties.Resources.offline_state;
            imgSend1.Image = Properties.Resources.offline_state;
            imgSend2.Image = Properties.Resources.offline_state;

            tmResetBarcode.Stop();
            tmResetBarcode.Enabled = false;
            imgPrintBarcode.Visible = false;
        }

        void SetTimeoutScanner(int interval, bool error)
        {
            //tmReset = new System.Windows.Forms.Timer();




            tmResetScanner.Interval = interval;
            tmResetScanner.Enabled = true;
            tmResetScanner.Tick += TmResetScanner_Tick;
        }


        private void TmResetScanner_Tick(object sender, EventArgs e)
        {


            tmResetScanner.Stop();
            tmResetScanner.Enabled = false;
        }


        private void txtJobNo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                this.splitContainer1.Enabled = true;
                txtJobNo.Text = txtJobNo.Text.ToUpper();
                //TxtBarcodeData.Text = ""
                string[] ScanArr = txtJobNo.Text.Split('-');

                if (txtEmployee1.Tag != null)
                {
                    ClearUser(txtEmployee1.Tag.ToString());
                }

                txtEmployee1.Tag = null;
                txtEmployee1.Text = "";
                txtEmployee1.Enabled = true;
                txtTotalBoard1.Text = "0";


                if (txtEmployee2.Tag != null)
                {
                    ClearUser(txtEmployee2.Tag.ToString());
                }

                txtEmployee2.Tag = null;
                txtEmployee2.Text = "";
                txtEmployee2.Enabled = false;
                txtTotalBoard2.Text = "0";

                if (ScanArr.Length < 1)
                {
                    //MessageBox.Show("Job format missing", "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    var message = new MessageDialog("Important Note", "Job format missing", "รูปแบบของหมายเลขงานไม่ถูกต้อง", MessageDialog.IconType.Error);
                    message.ShowDialog(this);

                }
                else if (ScanArr.Length == 1 || ScanArr[1] == "0")
                {
                    txtJobNo.Text = ScanArr[0] + "-0000";
                    //MessageBox.Show("Job suffix isn't number!", "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    BrowseJob(ScanArr[0], 0);

                }
                else
                {

                    int suffix = 0;
                    try
                    {
                        suffix = Convert.ToInt32(ScanArr[1]);
                    }
                    catch (Exception)
                    {

                        //MessageBox.Show("Wrong suffix. Isn't number!", "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                        var message = new MessageDialog("Important Note", "Wrong suffix. Isn't number!", "หมายเลขซับฟิกซ์ของงานไม่ถูกต้อง", MessageDialog.IconType.Error);
                        message.ShowDialog(this);
                    }

                    txtJobNo.Text = ScanArr[0] + "-" + suffix.ToString("0000");
                    BrowseJob(ScanArr[0], suffix);
                    //BrowseBarcodeDaily( Common.BarcodeType.LabelPrinter, ScanArr[0], suffix);

                    if (tmTrigger.Enabled || stateStart)
                    {
                        tmTrigger.Enabled = false;
                        stateStart = false;
                        btnStart.BackColor = Color.Empty;
                        btnStart.Text = "Start";
                        btnStart.Image = Properties.Resources.ApproveRequest_32x32;
                        try
                        {
                            if (clientSocket.Connected)
                                clientSocket.Close();
                        }
                        catch (Exception)
                        {

                        }
                    }
                   

                }

                txtInputBarcode.Text = "";
                txtInputBarcode.Focus();
            }

            else if (e.KeyCode == Keys.F3 && e.Alt)
            {

            }
        }

        void ClearUser(string employee)
        {

            var db = new DB();
            var lockM = db.UserAccounts.FirstOrDefault(f => f.EmployeeCode == employee);

            if (lockM != null)
            {
                lockM.IsMachineLock = false;
                lockM.MachineName = null;
                db.SubmitChanges();

                KeepLog("USER", "LOGOUT", lockM.LoginName);
            }
        }

        void BrowseJob(string jobCode, int suffix, bool interfaceJob = false)
        {
            var db = new DB();
            lastBarcode = "";
            board1 = 0;
            board2 = 0;
            jobRunno = 0;
            JobNotVerifyQty = 0;
            
            this.BarCodeProfile = "";
            this.BarCodeDefindField = "";
            var result = db.GetJobsInterface(jobCode, suffix, Environment.GetEnvironmentVariable("COMPUTERNAME"), BranchID);



            //var result = from j in db.QryBrowsJobsInterfaces
            //             where j.Job == jobCode && j.Suffix == suffix && j.ComName == Environment.GetEnvironmentVariable("COMPUTERNAME")
            //             select j;

            //if (pnlFixedDate.Visible)
            //{
            //    result = result.Where(w => w.WorkingDate.Value.Date == Convert.ToDateTime(this.txtFixedDate.Text).Date);
            //}

            dgvBoard1.Rows.Clear();
            dgvBoard2.Rows.Clear();
            dgvBoardList.Rows.Clear();
            lbFinish.Visible = false;
            lbBarcode1.Text = "BARCODE";
            lbBarcodeData.Text = "BARCODE";
            lbPrintingBarcode.Text = "BARCODE";
            txtTotalBoard1.Text = "0";

            imgPrintBarcode.Visible = false;

            txtFixedDate.Text = DateTime.Now.Date.ToString("yyyy-MM-dd");



            var job = result.FirstOrDefault();

            System.TimeSpan diff = job.ServerDateTime.Value.Subtract(DateTime.Now);


            if (diff.Seconds > 1 || diff.Seconds < -1)
            {
                var message = new MessageDialog("Important Note",
                       "Server and Client datetime difference",
                        "เวลาเครื่องฐานข้อมูลและลูกข่ายไม่ตรงกัน",
                        MessageDialog.IconType.Error);
                message.ShowDialog(this);
            }

            if (job != null && job.PrinterType.ToLower() == "videojet")
            {
                string Revision = job.Revision;
                string ModelCode = job.ModelCode;
                string FacCode = job.FacCode;
                IsSampleItem = job.IsSample.Value;
                string SampleMonthCode = job.SampleMonthCode;
                IsLikeItem = job.IsLike;
                string LineCode = job.LineCode;
                string PartNo = job.PartNo;
                txtIPAddress.Text = job.IPAddress;
                JobMaximumQty = job.JobQty.Value;
                this.txtCustomer.Text = job.CustomerID;
                this.txtPrintProfile.Text = job.PrintProfile;
                txtPartNo.Text = PartNo;

                lbJobNotVerifyCount.Visible = IsValidVideoJETOnline;
                lbJobVerifyCount.Visible = !IsValidVideoJETOnline;

                if (job.PrinterType.ToUpper() == "VIDEOJET")
                {
                    pbPrint.Image = Properties.Resources.VideoJet;
                    pbScanner.Image = Properties.Resources.SR_100;
                }
                else
                {
                    pbPrint.Image = Properties.Resources.Print;
                    pbScanner.Image = Properties.Resources.Scanner;
                }


                if (IsSampleItem)
                {
                    txtJobType.Text = SampleMonthCode == null || SampleMonthCode == "" ? "SAMPLE" : "SAMPLE(" + SampleMonthCode + ")";
                }
                else
                {
                    txtJobType.Text = "MASS";

                }


                string imgBarcodePart = @"Image\BarcodeImage\" + (txtJobType.Text.ToUpper() == "SAMPLE" ? "Sample" : "Mass") + @"\" + db.BoardHeaders.FirstOrDefault(f => f.ItemNo == job.ItemNo).BarcodeImagePath;

                txtItemNo.Text = job.ItemNo;

                txtQty.Text = job.JobQty.Value.ToString("#,##0");



                int barcodeLength = 0;
                string barcodeType = "";

                BoardHeaderKey = ControlBoard(txtItemNo.Text, IsSampleItem, ref barcodeType, ref barcodeLength);


                if (BoardHeaderKey != Guid.Empty)
                {
                    txtItemNo.Tag = job.RowKey;
                    UpdateQtyBroadCast();

                    txtInputBarcode.Focus();
                    //this.txtComName.Text = job.ComName;
                    //this.txtJobKey.Text = job.RowKey.ToString();
                    //txtItemNo.Tag = db.GetVerifyBoardDailyBarcode(
                    //    BoardHeaderKey,
                    //    Environment.MachineName,
                    //    BranchID,
                    //    jobCode,
                    //    suffix,
                    //    Convert.ToInt32(txtQty.Text.Replace(",", "")),
                    //    txtItemNo.Text,
                    //    txtPartNo.Text,
                    //    Revision,
                    //    ModelCode,
                    //    FacCode,
                    //    IsSampleItem,
                    //    SampleMonthCode,
                    //    IsLikeItem,
                    //    LineCode,
                    //    pnlFixedDate.Visible ? Convert.ToDateTime(txtFixedDate.Text).Date : DateTime.Now
                    //    ).FirstOrDefault().Column1;

                    txtStatus.Text = "FOUND";
                    txtJobNo.Enabled = false;

                    IsActive = true;




                    var boardHeader = db.BoardHeaders.First(f => f.ItemNo == txtItemNo.Text.ToUpper());

                    this.BarCodeProfile = boardHeader.PrintProfile;
                    this.BarCodeDefindField = boardHeader.DefindField;
                    if (System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imgBarcodePart)))
                    {
                        imageBarcode.Image = Image.FromFile(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imgBarcodePart));
                    }
                    else
                    {
                        imageBarcode.Image = null;
                    }

                    BrowseBarcodeDaily(Common.BarcodeType.VideoJet, job.JobNo, job.JobSuffix.Value,sendBroadCast: false);

                    var cpJob = db.Computers.FirstOrDefault(f => f.ComputerName == Environment.GetEnvironmentVariable("COMPUTERNAME"));
                    if (cpJob != null)
                    {
                        cpJob.ActiveJobNo = this.txtJobNo.Text.ToUpper();
                        db.SubmitChanges();
                    }



                    tmState.Enabled = true;
                    return;
                }
                else
                {
                    txtStatus.Text = "NOT ACTIVE!";
                    txtJobNo.Enabled = true;
                    IsActive = false;

                    if (System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imgBarcodePart)))
                    {
                        imageBarcode.Image = Image.FromFile(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imgBarcodePart));
                    }
                    else
                    {
                        imageBarcode.Image = null;
                    }


                }
            }
            else
            {
                txtPartNo.Text = "";
                txtJobType.Text = "";
                txtItemNo.Text = "";
                txtQty.Text = "";
                IsActive = false;
                txtStatus.Text = "NOT FOUND!";
                txtJobNo.Enabled = true;
                imageBarcode.Image = null;
            }

            var cp = db.Computers.FirstOrDefault(f => f.ComputerName == Environment.GetEnvironmentVariable("COMPUTERNAME"));
            if (cp != null)
            {
                cp.ActiveJobNo = null;
                db.SubmitChanges();
            }


        }

        Guid ControlBoard(string itemNo, bool IsSample, ref string barcodeType, ref int barcodeLength)
        {
            var db = new DB();

            var board = from b in db.BoardHeaders
                        where
                            b.IsActive.Value && b.ItemNo == itemNo

                        select new
                        {
                            b.RowKey,
                            b.BarcodeLength,
                            b.PrinterType
                        };

            if (board.Count() > 0)
            {
                var res = board.First();


                barcodeType = res.PrinterType;
                barcodeLength = res.BarcodeLength.Value;



                return res.RowKey;
            }
            else
            {
                return Guid.Empty;
            }
        }

        void BrowseBarcodeDaily(Common.BarcodeType btype, string jobCode, int suffix, bool sendBroadCast = true)
        {
            var db = new DB();


            //if (btype == Common.BarcodeType.LabelPrinter)
            //{
            //    var res = from qry in db.QryBarcodeDetails

            //              where
            //              qry.JobNo == jobCode &&
            //              qry.JobSuffix == suffix

            //              //qry.ModifyDateRaw.Value.Date == Convert.ToDateTime(this.txtFixedDate.Text).Date
            //              // qry.DetailKey == new Guid(txtItemNo.Tag.ToString())

            //              orderby qry.CreateDate.Value descending
            //              select new
            //              {
            //                  qry.ControlSerial,
            //                  qry.BarcodeData,
            //                  qry.IsVerify,
            //                  qry.ComName,
            //                  qry.BoardId,
            //                  qry.EmpId,
            //                  qry.IsCancel,
            //                  qry.ModifyDateRaw
            //              };

            //    int verify = IsValidVideoJETOnline ? res.Count(w =>  !w.IsCancel) : res.Count(w => w.IsVerify && !w.IsCancel);
            //    int total = res.Count();



            //    if (IsValidVideoJETOnline)
            //    {
            //        foreach (var item in res.Where(w => w.ModifyDateRaw.Value.Date == Convert.ToDateTime(this.txtFixedDate.Text).Date).OrderByDescending(c => c.ControlSerial).Take(100))
            //        {
                    
            //            AddRowToAllList(!item.IsCancel, item.BarcodeData, item.ControlSerial.ToString(), item.ComName, item.BoardId, item.EmpId, sendBroadCast: sendBroadCast);
            //        }
            //        dgvBoardList.Refresh();
            //    }


            //    JobMaximumQty = Convert.ToInt32(txtQty.Text.Replace(",", ""));
            //    JobVerifyQty = verify;


            //    //this.lbJobVerifyCount.Text = "Verified: " + JobVerifyQty.ToString("#,##0");
            //    //this.lbJobNotVerifyCount.Text = "Not Verified: " + (JobMaximumQty - JobVerifyQty).ToString("#,##0");
            //    //this.lbJobCount.Text = "Total: " + JobMaximumQty.ToString("#,##0");


            //    //var lastControlSet = db.QryGetLastControlSerials.Where(w => w.ItemNo == this.txtItemNo.Text.ToUpper() && w.IsSample.Value == IsSampleItem).Max(m => m.ControlSerial.Value);
            //    //if (lastControlSet != null)
            //    //{
            //    //    LastControlSerial = lastControlSet;
            //    //}



            //    if (JobVerifyQty >= JobMaximumQty)
            //    {
            //        lbFinish.Visible = true;
            //        txtInputBarcode.Enabled = false;
            //    }
            //    else
            //    {
            //        lbFinish.Visible = false;
            //        txtInputBarcode.Enabled = true;
            //    }

            //    lbJobVerifyCount.Text = "Verified: " + res.Count(c => c.IsVerify).ToString("#,##0");
            //    //if (IsValidVideoJETOnline)
            //    //{
            //    //    lbJobNotVerifyCount.Text = "Not Verified: " + res.Count(c => !c.IsVerify && !c.IsCancel).ToString("#,##0");
            //    //}
            //    //else
            //    //{
            //    //    lbJobNotVerifyCount.Text = "Not Verified: " + res.Count(c => !c.IsVerify && !c.IsCancel).ToString("#,##0");
            //    //}
               
            //    lbJobCount.Text = "Total: " + JobVerifyQty.ToString("#,##0") + " of " + JobMaximumQty.ToString("#,##0");

            //}
            //else 
            
            if (btype == Common.BarcodeType.VideoJet && IsValidVideoJETOnline)
            {
                var res = from qry in db.QryBarcodeDetails

                          where
                         qry.JobNo == jobCode &&
                          qry.JobSuffix == suffix //&& (IsValidVideoJETOnline ? true : !qry.IsCancel)

                          orderby qry.CreateDate ascending
                          select new
                          {
                              qry.ControlSerial,
                              qry.BarcodeData,
                              qry.IsVerify,
                              ComName = qry.ComName_Ass ?? qry.ComName,
                              BoardId = qry.BoardId_Ass ?? qry.BoardId,
                              EmpId = qry.EmpId_Ass ?? qry.EmpId,
                              qry.IsCancel
                          };
                foreach (var item in res)
                {
                    AddRowToAllList( IsValidVideoJETOnline ?!item.IsCancel : item.IsVerify, item.BarcodeData, item.ControlSerial.ToString(), item.ComName, item.BoardId, item.EmpId, sendBroadCast: sendBroadCast);
                }
                dgvBoardList.Refresh();

                int verify = IsValidVideoJETOnline ? res.Count(w => !w.IsCancel) : res.Count(w => w.IsVerify && !w.IsCancel);
                int total = res.Count();

                JobMaximumQty = Convert.ToInt32(txtQty.Text.Replace(",", ""));
                JobVerifyQty = verify;
                lbJobVerifyCount.Text = "Verified: " + res.Count(c => c.IsVerify).ToString("#,##0");
                //if (IsValidVideoJETOnline)
                //{
                //    lbJobNotVerifyCount.Text = "Not Verified: " + res.Count(c => !c.IsVerify && !c.IsCancel).ToString("#,##0");
                //}
                //else
                //{
                //    lbJobNotVerifyCount.Text = "Not Verified: " + res.Count(c => !c.IsVerify && !c.IsCancel).ToString("#,##0");
                //}

                lbJobCount.Text = "Total: " + JobVerifyQty.ToString("#,##0") + " of " + JobMaximumQty.ToString("#,##0");

                if (JobVerifyQty >= JobMaximumQty)
                {
                    lbFinish.Visible = true;
                    txtInputBarcode.Enabled = false;
                    splitContainer1.Panel1.Enabled = false;
                    splitContainer1.Panel2.Enabled = false;
                }
            }
            else if (btype == Common.BarcodeType.VideoJet && !IsValidVideoJETOnline)
            {
                var res = from qry in db.QryBarcodeDetails

                          where
                         qry.JobNo == jobCode &&
                          qry.JobSuffix == suffix && //&& (IsValidVideoJETOnline ? true : !qry.IsCancel)
                          qry.IsVerify
                          orderby qry.CreateDate ascending
                          select new
                          {
                              qry.ControlSerial,
                              qry.BarcodeData,
                              qry.IsVerify,
                              ComName = qry.ComName_Ass ?? qry.ComName,
                              BoardId = qry.BoardId_Ass ?? qry.BoardId,
                              EmpId = qry.EmpId_Ass ?? qry.EmpId,
                              qry.IsCancel
                          };
                foreach (var item in res)
                {
                    AddRowToAllList(IsValidVideoJETOnline ? !item.IsCancel : item.IsVerify, item.BarcodeData, item.ControlSerial.ToString(), item.ComName, item.BoardId, item.EmpId, sendBroadCast: sendBroadCast);
                }

                int verify = IsValidVideoJETOnline ? res.Count(w => !w.IsCancel) : res.Count(w => w.IsVerify && !w.IsCancel);
                int total = res.Count();

                JobMaximumQty = Convert.ToInt32(txtQty.Text.Replace(",", ""));
                JobVerifyQty = verify;
                lbJobVerifyCount.Text = "Verified: " + res.Count(c => c.IsVerify).ToString("#,##0");

                if (JobVerifyQty >= JobMaximumQty)
                {
                    lbFinish.Visible = true;
                    txtInputBarcode.Enabled = false;
                    splitContainer1.Panel1.Enabled = false;
                    splitContainer1.Panel2.Enabled = false;
                }
            }
        }

        private void btnUnlockJobNo_Click(object sender, EventArgs e)
        {

            AssemblyCurrentBoard = 0;
            JobMaximumQty = 0;
            JobVerifyQty = 0;
            JobNotVerifyQty = 0;

            this.lbJobVerifyCount.Text = "Verified : 0";
            if (txtJobNo.Enabled) return;


            this.splitContainer1.Enabled = true;

            txtJobNo.Enabled = !txtJobNo.Enabled;
            if (txtJobNo.Enabled)
            {
                txtJobNo.Focus();
                txtJobNo.SelectAll();
                tmState.Enabled = false;
            }
            else
            {
                tmState.Enabled = true;
            }
        }

        private void txtEmployee1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;

                var db = new DB();

                var acc = from u in db.UserAccounts
                          from b in u.UserBranches
                          where u.EmployeeCode.ToUpper() == txtEmployee1.Text.ToUpper() && b.Branch.BranchID == BranchID && (!u.IsMachineLock || (u.IsMachineLock && u.MachineName == Environment.GetEnvironmentVariable("COMPUTERNAME")))
                          select u;

                if (acc.Count() > 0)
                {
                    var user = acc.First();

                    //board1 = 0;
                    //txtTotalBoard1.Text = board1.ToString("#,##0");

                    if (!txtEmployee2.Enabled && (txtEmployee2.Tag != null && txtEmployee2.Tag.ToString() == user.EmployeeCode))
                    {
                        //MessageBox.Show("Employee not be the same of other board", "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

                        var message = new MessageDialog("Important Note", "Employee not be the same of other board", "ไม่สามารถใช้รหัสพนักงานเดียวกันได้", MessageDialog.IconType.Error);
                        message.ShowDialog(this);
                        return;
                    }

                    var lockM = db.UserAccounts.First(f => f.UserKey == user.UserKey);
                    lockM.IsMachineLock = true;
                    lockM.MachineName = Environment.GetEnvironmentVariable("COMPUTERNAME");
                    // (!u.IsMachineLock || (u.IsMachineLock && u.MachineName == Environment.GetEnvironmentVariable("COMPUTERNAME")))
                    db.SubmitChanges();
                    KeepLog("USER", "LOGIN", lockM.LoginName);
                    txtEmployee1.PasswordChar = '\0';
                    txtEmployee1.Text = user.NickName;
                    txtEmployee1.Tag = user.EmployeeCode;
                    txtEmployee1.Enabled = false;
                    txtInputBarcode.Enabled = true;

                    //board1 = db.BarcodeDetails.Count(
                    //       c =>
                    //           c.BoardDetailKey == new Guid(txtItemNo.Tag.ToString()) &&
                    //          // (IsValidVideoJETOnline ? c.ComName_Ass == Environment.GetEnvironmentVariable("COMPUTERNAME") : c.EmpID_Ass == Environment.GetEnvironmentVariable("COMPUTERNAME")) &&
                    //          c.ComName_Ass == Environment.GetEnvironmentVariable("COMPUTERNAME") &&
                    //           (IsValidVideoJETOnline ? c.EmpID == txtEmployee1.Tag.ToString() : c.EmpID_Ass == txtEmployee1.Tag.ToString())
                    //   );


                    txtTotalBoard1.Text = board1.ToString("#,##0");

                    txtInputBarcode.Text = "";
                    txtInputBarcode.Focus();
                }
                else
                {
                    txtEmployee1.Text = "";
                }
            }
            else
            {
                txtEmployee1.PasswordChar = '*';

            }
        }

        private void txtEmployee2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                var db = new DB();

                var acc = from u in db.UserAccounts
                          from b in u.UserBranches
                          where u.EmployeeCode.ToUpper() == txtEmployee2.Text.ToUpper() && b.Branch.BranchID == BranchID && (!u.IsMachineLock || (u.IsMachineLock && u.MachineName == Environment.GetEnvironmentVariable("COMPUTERNAME")))
                          select u;

                if (acc.Count() > 0)
                {
                    var user = acc.First();

                    //board2 = 0;
                    //txtTotalBoard2.Text = board2.ToString("#,##0");

                    if (!txtEmployee1.Enabled && (txtEmployee1.Tag != null && txtEmployee1.Tag.ToString() == user.EmployeeCode))
                    {
                        //MessageBox.Show("Employee not be the same of other board", "Important Note", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);

                        var message = new MessageDialog("Important Note", "Employee not be the same of other board", "ไม่สามารถใช้รหัสพนักงานเดียวกันได้", MessageDialog.IconType.Error);
                        message.ShowDialog(this);
                        return;
                    }

                    var lockM = db.UserAccounts.First(f => f.UserKey == user.UserKey);
                    lockM.IsMachineLock = true;
                    lockM.MachineName = Environment.GetEnvironmentVariable("COMPUTERNAME");
                    db.SubmitChanges();

                    KeepLog("USER", "LOGIN", lockM.LoginName);

                    txtEmployee2.PasswordChar = '\0';
                    txtEmployee2.Text = user.NickName;
                    txtEmployee2.Tag = user.EmployeeCode;
                    txtEmployee2.Enabled = false;
                    txtInputBarcode.Enabled = true;

                    //board2 = db.BarcodeDetails.Count(
                    //       c =>
                    //           c.BoardDetailKey == new Guid(txtItemNo.Tag.ToString()) &&
                    //           //(IsValidVideoJETOnline ? c.ComName_Ass == Environment.GetEnvironmentVariable("COMPUTERNAME") : c.EmpID_Ass == Environment.GetEnvironmentVariable("COMPUTERNAME")) &&
                    //           c.ComName_Ass == Environment.GetEnvironmentVariable("COMPUTERNAME") &&
                    //           (IsValidVideoJETOnline ? c.EmpID == txtEmployee2.Tag.ToString() : c.EmpID_Ass == txtEmployee2.Tag.ToString())
                    //   );


                    txtTotalBoard2.Text = board2.ToString("#,##0");
                    txtInputBarcode.Text = "";
                    txtInputBarcode.Focus();
                }
                else
                {
                    txtEmployee2.Text = "";
                }

            }
            else
            {
                txtEmployee2.PasswordChar = '*';
            }

        }

        private void btnEmployee1_Click(object sender, EventArgs e)
        {
            //if (txtEmployee1.Enabled) return;

            txtEmployee1.Enabled = !txtEmployee1.Enabled;
            if (txtEmployee1.Enabled)
            {
                board1 = 0;
                txtTotalBoard1.Text = "0";
                try
                {
                    ClearUser(txtEmployee1.Tag.ToString());
                }
                catch (Exception)
                {


                }
                dgvBoard1.Rows.Clear();
                txtEmployee1.Text = "";
                txtEmployee1.Tag = null;
                txtEmployee1.Focus();
            }

        }

        private void btnEmployee2_Click(object sender, EventArgs e)
        {

            txtEmployee2.Enabled = !txtEmployee2.Enabled;
            if (txtEmployee2.Enabled)
            {
                board2 = 0;
                txtTotalBoard2.Text = "0";
                try
                {
                    ClearUser(txtEmployee2.Tag.ToString());
                }
                catch (Exception)
                {


                }
                dgvBoard2.Rows.Clear();
                txtEmployee2.Text = "";
                txtEmployee2.Tag = null;
                txtEmployee2.Focus();
            }

        }


        private void btnBoardCom1_Click(object sender, EventArgs e)
        {

            int tryCount = 0;
            if (this.SerialPort1 != null)
            {
                tryCount = 0;
                while (SerialPort1.IsOpen || tryCount == 10)
                {
                    Application.DoEvents();
                    SerialPort1.Close();
                    tryCount++;
                }
                SerialPort1.Dispose();
            }

            if (this.SerialPort2 != null)
            {
                tryCount = 0;
                while (SerialPort2.IsOpen || tryCount == 10)
                {
                    Application.DoEvents();
                    SerialPort2.Close();
                    tryCount++;
                }
                SerialPort2.Dispose();
            }

            var com = new Configurations.ComPort();

            com.ShowDialog(this);

            BindSerial();

        }

        private void btnBoardCom2_Click(object sender, EventArgs e)
        {
            btnBoardCom1_Click(null, null);
        }

        private void txtInputBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            // When connected and state is start and running
            if (e.KeyCode == Keys.Enter) // && IsValidVideoJETOnline
            {
       var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nTEST 1 LOC 2191", txtInputBarcode.Text.ToUpper(), MessageDialog.IconType.Error);
                        message.ShowDialog(this);

                string[] ScanArr = txtJobNo.Text.Split('-');

                lbBarcode1.Text = txtInputBarcode.Text.ToUpper();
                var db = new DB();

                //var barcode = from d in db.BoardDetails
                //              from b in d.BarcodeDetails
                //              where 
                //                b.BarcodeData == txtInputBarcode.Text.ToUpper() &&
                //                b.ItemNo.ToUpper() == txtItemNo.Text.ToUpper() &&
                                
                //                d.JobNo == ScanArr[0] &&
                //                d.JobSuffix == Convert.ToInt32(ScanArr[1]) //&&


                //                //Modify Date 2022-12-12
                //               // d.RowKey == new Guid(txtItemNo.Tag.ToString())

                //              select new
                //              {
                //                  b.RowKey,
                //                  b.IsVerify,
                //                  boardKey= d.RowKey,
                //                  b.IsPrint,
                //                  b.IsCancel,
                //                  d.ComName,
                //                  b.BoardID,
                //                  b.BarcodeData,
                //                  b.ControlSerial,
                //                  BoardDetailKey =d.RowKey
                //              };

                var barcode = from d in db.BoardDetails
                              from b in d.BarcodeDetails
                              where
                                b.BarcodeData == txtInputBarcode.Text.ToUpper() &&
                                b.ItemNo.ToUpper() == txtItemNo.Text.ToUpper() &&

                                d.JobNo == ScanArr[0] &&
                                d.JobSuffix == Convert.ToInt32(ScanArr[1]) //&&


                              //Modify Date 2022-12-12
                              // d.RowKey == new Guid(txtItemNo.Tag.ToString())

                              select new
                              {
                                  b.RowKey,
                                  b.IsVerify,
                                  boardKey = d.RowKey,
                                  b.IsPrint,
                                  b.IsCancel,
                                  d.ComName,
                                  b.BoardID,
                                  b.BarcodeData,
                                  b.ControlSerial,
                                  BoardDetailKey = d.RowKey
                              };

                //Offline
                if (barcode.Count(c => !c.IsVerify && c.IsPrint && !c.IsCancel) > 0)
                { 
                     var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nTEST 2 LOC 2256", txtInputBarcode.Text.ToUpper(), MessageDialog.IconType.Error);
                        message.ShowDialog(this);
                    var fx = barcode.First();
                    var v = db.BarcodeDetails.First(f => f.RowKey == fx.RowKey && f.BoardDetailKey == fx.BoardDetailKey);

                    if (IsValidVideoJETOnline)
                    {
                        var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nBarcode exists in this job or duplicate", txtInputBarcode.Text.ToUpper() + "\nบาร์โค้ดนี้รับเข้าระบบแล้ว หรือมีบาร์โค้ดซ้ำ", MessageDialog.IconType.Error);
                        message.ShowDialog(this);

                    }
                    else
                    {
                         var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nTEST 3 LOC 2269", txtInputBarcode.Text.ToUpper(), MessageDialog.IconType.Error);
                        message.ShowDialog(this);

                        if (!IsValidVideoJETOnline && AssemblyCurrentBoard == 0) {
                            SetTimeoutScanner(1000, true);
                            var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nWrong computer port", txtInputBarcode.Text.ToUpper() + "\nการเชื่อมต่อคอมพอร์ทไม่ถูกต้อง", MessageDialog.IconType.Error);
                            message.ShowDialog(this);
                            txtInputBarcode.Text = "";
                            return;
                        }
                        else if (!IsValidVideoJETOnline && AssemblyCurrentBoard != 0)
                        {

                            //begin Change board detail key on assey
                            v.BoardDetailKey = new Guid(txtItemNo.Tag.ToString());
                            //end Change board detail key on assey


                            v.IsVerify = true;
                            v.VerifyDate = DateTime.Now;
                            v.ComName_Ass = Environment.GetEnvironmentVariable("COMPUTERNAME");
                            v.EmpID_Ass = AssemblyCurrentBoard == 1 ? txtEmployee1.Tag.ToString() : txtEmployee2.Tag.ToString();
                            v.Date_Ass = DateTime.Now;
                            v.BoardID_Ass = AssemblyCurrentBoard.ToString();
                            db.SubmitChanges();



                            AddRowToList(false, true, AssemblyCurrentBoard == 1 ? Common.Board.Board1 : Common.Board.Board2, fx.BarcodeData, fx.ControlSerial);

                            SetTimeoutBarcode(500);
                            try
                            {
                                // send open arm holder
                                SerialPort2.Write(AssemblyCurrentBoard.ToString());

                                if(AssemblyCurrentBoard==1)
                                {
                                    imgSend1.Image = Properties.Resources.online_state;
                                    imgSend2.Image = Properties.Resources.offline_state;
                                }
                                if (AssemblyCurrentBoard == 2)
                                {
                                    imgSend2.Image = Properties.Resources.online_state;
                                    imgSend1.Image = Properties.Resources.offline_state;
                                }
                            }
                            catch (Exception ex)
                            {
                               // MessageBox.Show(ex.Message);
                            }

                            AssemblyCurrentBoard = 0;
                            JobVerifyQty++;

                            
                        }
                        else
                        {
                            SetTimeoutScanner(1000, true);
                            var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nBarcode is not define board id", txtInputBarcode.Text.ToUpper() + "\nบาร์โค้ดนี้ไม่ได้ตรวจสอบสายไฟ", MessageDialog.IconType.Error);
                            message.ShowDialog(this);
                            txtInputBarcode.Text = "";
                            return;
                        }
                        
                        //this.lbJobVerifyCount.Text = "Verified: " + JobVerifyQty.ToString("#,##0");
                        //this.lbJobNotVerifyCount.Text = "Not Verified: " + (JobMaximumQty - JobVerifyQty).ToString("#,##0");
                        //lbJobCount.Text = "Total: " + JobVerifyQty.ToString("#,##0") + " of " + JobMaximumQty.ToString("#,##0");
                    }
                    


                   



                    try
                    {

                        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                        string sendCommand = "HET:DATA:UPDATE:" + this.txtJobNo.Text.ToUpper() + ":TRUE:" + v.BarcodeData + ":" + v.ControlSerial;

                        byte[] sendbuf = Encoding.ASCII.GetBytes(sendCommand);

                        foreach (var item in db.Computers.Where(w => w.IsValid && w.ComputerName != Environment.GetEnvironmentVariable("COMPUTERNAME") && w.ActiveJobNo == this.txtJobNo.Text.ToUpper()))
                        {
                            IPAddress broadcast = IPAddress.Parse(item.IPAddress);
                            IPEndPoint ep = new IPEndPoint(broadcast, 11000);
                            s.SendTo(sendbuf, ep);
                        }

                        Console.WriteLine("Message sent to the broadcast address");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Udp:" + ex.Message);
                    }

                    if (AssemblyCurrentBoard==1)
                    {
                        foreach (DataGridViewRow item in dgvBoard1.Rows)
                        {
                            if (item.Cells[2].Value.ToString() == v.BarcodeData)
                            {
                                item.Cells[0].Value = imageList1.Images[1];
                                item.Cells[1].Style.BackColor = Color.Green;
                                item.Cells[1].Style.ForeColor = Color.White;
                                Application.DoEvents();
                                break;
                            }
                        }


                    }

                    if (AssemblyCurrentBoard == 2)
                    {
                        foreach (DataGridViewRow item in dgvBoard2.Rows)
                        {
                            if (item.Cells[2].Value.ToString() == v.BarcodeData)
                            {
                                item.Cells[0].Value = imageList1.Images[1];
                                item.Cells[1].Style.BackColor = Color.Green;
                                item.Cells[1].Style.ForeColor = Color.White;
                                Application.DoEvents();
                                break;
                            }
                        }


                    }
                    foreach (DataGridViewRow item in dgvBoardList.Rows)
                    {
                        if (item.Cells[1].Value.ToString() == v.BarcodeData)
                        {
                            item.Cells[0].Value = imageList1.Images[1];
                            item.Cells[1].Style.BackColor = Color.Green;
                            item.Cells[1].Style.ForeColor = Color.White;
                            Application.DoEvents();
                            break;
                        }
                    }

                    if (JobVerifyQty >= JobMaximumQty)
                    {

                        lbFinish.Visible = true;
                        txtInputBarcode.Enabled = false;
                        splitContainer1.Panel1.Enabled = false;
                        splitContainer1.Panel2.Enabled = false;

                        var message = new MessageDialog("Finished",
                                 "Job " + this.txtJobNo.Text + " has been finished\n",
                                "ใบงาน " + this.txtJobNo.Text + " เสร็จสิ้นแล้ว", MessageDialog.IconType.Error);
                        message.ShowDialog(this);
                        return;
                    }

                }
                //Default state Onlin=
                else if (barcode.Count(c => !c.IsVerify && c.IsPrint && c.IsCancel) > 0)
                {
                    var fx = barcode.First();
                    var v = db.BarcodeDetails.First(f => f.RowKey == fx.RowKey && f.BoardDetailKey == fx.BoardDetailKey);




                    lbPrintingBarcode.Text = txtInputBarcode.Text;
 var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nTEST 5 LOC 2440", txtInputBarcode.Text.ToUpper(), MessageDialog.IconType.Error);
                        message.ShowDialog(this);
                    UpdateList(txtInputBarcode.Text, Common.Board.Board1);
                        if (IsValidVideoJETOnline)
                        {

                            v.IsCancel = false;
                            v.VerifyDate = DateTime.Now;
                            v.ComName_Ass = Environment.GetEnvironmentVariable("COMPUTERNAME");
                            v.Date_Ass = DateTime.Now;
                            v.BoardID_Ass = AssemblyCurrentBoard.ToString();
                            db.SubmitChanges();
                         
                        }
                        else
                        {
                            SetTimeoutScanner(5000, true);
                            var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nBarcode is not verified from VideoJET", txtInputBarcode.Text.ToUpper() + "\nบาร์โค้ดนี้ไม่ผ่านการตรวจสอบ จาก VideoJET", MessageDialog.IconType.Error);
                            message.ShowDialog(this);
                            txtInputBarcode.Text = "";
                            return;
                        }
                        JobVerifyQty++;
                    //this.lbJobVerifyCount.Text = "Verified: " + JobVerifyQty.ToString("#,##0");
                    //this.lbJobNotVerifyCount.Text = "Not Verified: " + (JobMaximumQty - JobVerifyQty).ToString("#,##0");
                    //lbJobCount.Text = "Total: " + JobVerifyQty.ToString("#,##0") + " of " + JobMaximumQty.ToString("#,##0");

                    
                    UpdateQtyBroadCast();

                    try
                    {

                        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                        string sendCommand = "HET:DATA:UPDATE:" + this.txtJobNo.Text.ToUpper() + ":TRUE:" + v.BarcodeData + ":" + v.ControlSerial;

                        byte[] sendbuf = Encoding.ASCII.GetBytes(sendCommand);

                        foreach (var item in db.Computers.Where(w => w.IsValid && w.ComputerName != Environment.GetEnvironmentVariable("COMPUTERNAME") && w.ActiveJobNo == this.txtJobNo.Text.ToUpper()))
                        {
                            IPAddress broadcast = IPAddress.Parse(item.IPAddress);
                            IPEndPoint ep = new IPEndPoint(broadcast, 11000);
                            s.SendTo(sendbuf, ep);
                        }

                        Console.WriteLine("Message sent to the broadcast address");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Udp:" + ex.Message);
                    }

                    if (AssemblyCurrentBoard == 1)
                    {
                        foreach (DataGridViewRow item in dgvBoard1.Rows)
                        {
                            if (item.Cells[2].Value.ToString() == v.BarcodeData)
                            {
                                item.Cells[0].Value = imageList1.Images[1];
                                item.Cells[1].Style.BackColor = Color.Green;
                                item.Cells[1].Style.ForeColor = Color.White;
                                Application.DoEvents();
                                break;
                            }
                        }


                    }

                    if (AssemblyCurrentBoard == 2)
                    {
                        foreach (DataGridViewRow item in dgvBoard2.Rows)
                        {
                            if (item.Cells[2].Value.ToString() == v.BarcodeData)
                            {
                                item.Cells[0].Value = imageList1.Images[1];
                                item.Cells[1].Style.BackColor = Color.Green;
                                item.Cells[1].Style.ForeColor = Color.White;
                                Application.DoEvents();
                                break;
                            }
                        }


                    }
                    foreach (DataGridViewRow item in dgvBoardList.Rows)
                    {
                        if (item.Cells[1].Value.ToString() == v.BarcodeData)
                        {
                            item.Cells[0].Value = imageList1.Images[1];
                            item.Cells[1].Style.BackColor = Color.Green;
                            item.Cells[1].Style.ForeColor = Color.White;
                            Application.DoEvents();
                            break;
                        }
                    }

                    if (JobVerifyQty >= JobMaximumQty)
                    {

                        lbFinish.Visible = true;
                        txtInputBarcode.Enabled = false;
                        splitContainer1.Panel1.Enabled = false;
                        splitContainer1.Panel2.Enabled = false;

                        var message = new MessageDialog("Finished",
                                 "Job " + this.txtJobNo.Text + " has been finished\n",
                                "ใบงาน " + this.txtJobNo.Text + " เสร็จสิ้นแล้ว", MessageDialog.IconType.Error);
                        message.ShowDialog(this);
                        return;
                    }

                }
                else if (barcode.Count(c => c.IsVerify) > 0)
                {
                    SetTimeoutScanner(5000, true);
                    var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nBarcode is already verified in this job", txtInputBarcode.Text.ToUpper() + "\nบาร์โค้ดนี้ได้ถูกตรวจสอบไปก่อนหน้านี้แล้ว", MessageDialog.IconType.Error);
                    message.ShowDialog(this);
                }
                else
                {
                    //MessageBox.Show("Barcode is not in this job", "Invalid Barcode", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SetTimeoutScanner(5000, true);
                    var message = new MessageDialog("Important Note", txtInputBarcode.Text.ToUpper() + "\nBarcode is not in this job", txtInputBarcode.Text.ToUpper() + "\nบาร์โค้ดนี้ไม่ได้อยู่ในใบงานนี้", MessageDialog.IconType.Error);
                    message.ShowDialog(this);
                }


                txtInputBarcode.Text = "";
            }

            

            //// When disconnected 
            //if (e.KeyCode == Keys.Enter && !IsValidVideoJETOnline)
            //{
            //    lbBarcode1.Text = txtInputBarcode.Text;

            //    lbPrintingBarcode.Text = txtInputBarcode.Text;
            //    txtInputBarcode.Text = "";
            //}

           
        }

        private void btnTest1_Click(object sender, EventArgs e)
        {
            if (txtEmployee1.Tag == null)
            {
                //MessageBox.Show("Please login employee 1", "Employee not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                var message = new MessageDialog("Important Note", "Please login employee 1", "กรุณาเข้าด้วยรหัสพนักงานก่อน", MessageDialog.IconType.Error);
                message.ShowDialog(this);
                return;
            }
            AddRowToList(true, false, Common.Board.Board1, test: true);
            txtInputBarcode.Text = "";
            txtInputBarcode.Focus();
        }



        private void frmPrintLabel_KeyDown(object sender, KeyEventArgs e)
        {
           
           
            if (e.KeyCode == Keys.F12 && e.Control)
            {
                if (txtJobNo.Enabled)
                {
                    //MessageBox.Show("Please brows active job", "Job not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    var message = new MessageDialog("Important Note", "Please brows active job", "ต้องทำการเรียกใช้งานใบงานก่อน", MessageDialog.IconType.Error);
                    message.ShowDialog(this);
                    return;
                }
                if (!btnTest1.Visible)
                {
                    var flogin = new LoginFormRight("Test Mode");
                    if (flogin.ShowDialog(this) == DialogResult.OK)
                    {
                        if (flogin.Role >= 1)
                        {
                            btnTest1.Visible = !btnTest1.Visible;
                            lbTestMode.Visible = !lbTestMode.Visible;
                        }
                    }
                }
                else
                {
                    btnTest1.Visible = !btnTest1.Visible;
                    lbTestMode.Visible = !lbTestMode.Visible;
                }
            }
            if (e.KeyCode == Keys.F11 && e.Control)
            {
                bool enable = this.txtJobNo.Enabled;
                tmState.Enabled = enable;
                var flogin = new LoginFormRight("Config Barcode");
                if (flogin.ShowDialog(this) == DialogResult.OK)
                {
                    if (flogin.Role >= 1)
                    {
                        var f = new Configurations.ConfigBoard(flogin.EmployeeCode, flogin.Role);
                        f.ShowDialog(this);
                    }
                }
                tmState.Enabled = true;
            }
            if (e.KeyCode == Keys.F10 && e.Control)
            {
                if (txtJobNo.Enabled)
                {
                    // MessageBox.Show("Please brows job", "Job not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    var message = new MessageDialog("Important Note", "Please brows active job", "ต้องทำการเรียกใช้งานใบงานก่อน", MessageDialog.IconType.Error);
                    message.ShowDialog(this);
                    return;
                }

                if (!txtQty.Enabled)
                {
                    var flogin = new LoginFormRight("Change Quantity");
                    if (flogin.ShowDialog(this) == DialogResult.OK)
                    {
                        if (flogin.Role >= 1)
                        {
                            txtQty.Enabled = true;
                            txtQty.Focus();
                        }
                    }
                }
                else
                {
                    try
                    {
                        int qty = Convert.ToInt32(txtQty.Text);
                        txtQty.Enabled = false;
                        JobMaximumQty = qty;
                        try
                        {
                            var db = new DB();
                            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                            string sendCommand = "HET:DATA:QTY:" + this.txtJobNo.Text.ToUpper() + ":" + qty.ToString();

                            byte[] sendbuf = Encoding.ASCII.GetBytes(sendCommand);

                            foreach (var item in db.Computers.Where(w => w.IsValid && w.ComputerName != Environment.GetEnvironmentVariable("COMPUTERNAME") && w.ActiveJobNo == this.txtJobNo.Text.ToUpper()))
                            {
                                IPAddress broadcast = IPAddress.Parse(item.IPAddress);
                                IPEndPoint ep = new IPEndPoint(broadcast, 11000);
                                s.SendTo(sendbuf, ep);
                            }

                            Console.WriteLine("Message sent to the broadcast address");

                            KeepLog("UDP-OUT", "DATA", sendCommand);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Udp:" + ex.Message);
                        }

                        txtQty.Text = Convert.ToInt32(txtQty.Text).ToString("#,##0");
                        txtInputBarcode.Focus();
                    }
                    catch (Exception)
                    {
                        txtQty.Text = "Wrong Qty";
                        txtQty.SelectAll();
                    }

                }

            }
            if (e.KeyCode == Keys.F9 && e.Control)
            {
                if (txtJobNo.Enabled)
                {
                    //MessageBox.Show("Please brows active job", "Job not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    var message = new MessageDialog("Important Note", "Please brows active job", "ต้องทำการเรียกใช้งานใบงานก่อน", MessageDialog.IconType.Error);
                    message.ShowDialog(this);
                    return;
                }
                if (!pnlFixedDate.Visible)
                {
                    var flogin = new LoginFormRight("Fixed Date");
                    if (flogin.ShowDialog(this) == DialogResult.OK)
                    {
                        if (flogin.Role >= 1)
                        {
                            pnlFixedDate.Visible = !pnlFixedDate.Visible;
                        }
                    }
                }
                else
                {
                    pnlFixedDate.Visible = !pnlFixedDate.Visible;
                }
            }

            if (e.KeyCode == Keys.F8 && e.Control)
            {
                var flogin = new LoginFormRight("User Accounts");
                if (flogin.ShowDialog(this) == DialogResult.OK)
                {
                    if (flogin.Role >= 1)
                    {
                        var ulist = new HET.Configurations.UserAccounts(flogin.Role);
                        ulist.ShowDialog(this);
                    }
                }
            }
            if (e.KeyCode == Keys.F4 && e.Control)
            {
                try
                {
                    bool isOpen = false;
                    if (SerialPort1!=null)
                    {
                         isOpen = SerialPort1.IsOpen;

                        if (SerialPort1.IsOpen)
                        {
                            SerialPort1.Close();
                        }
                    }

                    if (SerialPort2 != null)
                    {

                        if (SerialPort2.IsOpen)
                        {
                            SerialPort2.Close();
                        }
                    }


                    var check = new FormVideoJETTest(SerialPort1);
                    check.ShowDialog(this);
                    if (SerialPort1 != null)
                    {
                        if (isOpen)
                        {
                            SerialPort1.Open();
                        }
                    }
                    if (SerialPort2 != null)
                    {
                         
                            SerialPort2.Open();
                        
                    }

                }
                catch (Exception)
                {

                   
                }
               
            }
            if (e.KeyCode == Keys.F3 && !e.Control && !e.Alt)
            {
                var check = new CheckBarcode(null);
                check.ShowDialog(this);
            }
            if (e.KeyCode == Keys.F2 && e.Control)
            {
                var flogin = new LoginFormRight("Change Password");

                if (flogin.ShowDialog(this) == DialogResult.OK)
                {

                    var frm = new Configurations.AddUser(flogin.EmployeeCode, flogin.Role);
                    frm.ShowDialog(this);

                }
            }
            if (e.KeyCode == Keys.F3 && e.Control)
            {
                var check = new BarcodeTracking(this.txtJobNo.Text.ToUpper());
                check.ShowDialog(this);
            }
            if (e.KeyCode == Keys.F1 && e.Control)
            {
                splitContainer4.Panel2Collapsed = !splitContainer4.Panel2Collapsed;
            }

        }

        private void dgvBoard1_SelectionChanged(object sender, EventArgs e)
        {
            txtInputBarcode.Text = "";
            txtInputBarcode.Focus();
        }

        private void dgvBoard2_SelectionChanged(object sender, EventArgs e)
        {
            txtInputBarcode.Text = "";
            txtInputBarcode.Focus();
        }

        private void dgvBoardList_SelectionChanged(object sender, EventArgs e)
        {
            txtInputBarcode.Text = "";
            txtInputBarcode.Focus();
        }

        private void pbScanner_Click(object sender, EventArgs e)
        {
            txtInputBarcode.Text = "";
            txtInputBarcode.Focus();
        }

        private void tmState_Tick(object sender, EventArgs e)
        {
            if (txtEmployee1.Focused || txtEmployee2.Focused || txtJobNo.Focused || txtTotalBoard1.Focused || txtQty.Focused || dtpFixedDated.Focused)
            {

            }
            else

            {
                txtInputBarcode.Focus();
            }



            //if (LastControlSerail > 0 && !txtJobNo.Enabled && !lbFinish.Visible)
            //{

            //    var db = new DB();

            //    var lastControlSet = db.QryGetLastControlSerails.Where(m => m.ItemNo == this.txtItemNo.Text.ToUpper() && Convert.ToInt64(m.ControlSerial) > LastControlSerail);



            //    foreach (var item in lastControlSet)
            //    {
            //        AddRowToAllList(item.IsVerify, item.SerialData, item.BarcodeData, item.ControlSerial, item.ComName, item.BoardID, item.EmpID);

            //        LastControlSerail = Convert.ToInt64(item.ControlSerial);
            //    }

            //    string[] ScanArr = txtJobNo.Text.Split('-');

            //    var res = from qry in db.QryBarcodeDetails

            //              where
            //              qry.JobNo == ScanArr[0] &&
            //              qry.JobSuffix == Convert.ToInt32(ScanArr[1]) //&&
            //                                                           // qry.DetailKey == new Guid(txtItemNo.Tag.ToString())

            //              orderby Convert.ToInt64(qry.ControlSerial) descending
            //              select new
            //              {
            //                  qry.ControlSerial,
            //                  qry.SerialData,
            //                  qry.BarcodeData,
            //                  qry.IsVerify,
            //                  qry.ComName,
            //                  qry.BoardId,
            //                  qry.EmpId,
            //                  qry.IsCancel
            //              };


            //    if (res.Count() >= JobCount)
            //    {
            //        lbFinish.Visible = true;
            //        splitContainer1.Panel1.Enabled = false;
            //        splitContainer1.Panel2.Enabled = false;


            //    }
            //    else
            //    {
            //        lbFinish.Visible = false;
            //        splitContainer1.Panel1.Enabled = true;
            //        splitContainer1.Panel2.Enabled = true;
            //    }
            //    lbJobVerifyCount.Text = res.Count(c => c.IsVerify).ToString("#,##0");
            //    lbJobCount.Text = res.Count().ToString("#,##0") + " of " + JobCount.ToString("#,##0");
            //}

        }

        bool test = false;


        private void timer1_Tick(object sender, EventArgs e)
        {
            //try
            //{
            //    if (test)
            //    {
            //        serial.Write("1");
            //        imgSend1.Image = Properties.Resources.online_state;
            //        SetTimeout(1000);
            //    }
            //    else
            //    {
            //        serial.Write("2");
            //    }
            //    test = !test;


            //}
            //catch (Exception)
            //{

            //}

        }





        private void dtpFixedDated_ValueChanged(object sender, EventArgs e)
        {
            //var db = new DB();
            //txtItemNo.Tag = db.GetVerifyBoardDailyBarcode(
            //            BoardHeaderKey,
            //            Environment.MachineName,
            //            1,
            //            jobCode,
            //            suffix,
            //            Convert.ToInt32(txtQty.Text.Replace(",", "")),
            //            txtItemNo.Text,
            //            txtPartNo.Text,
            //            Revision,
            //            ModelCode,
            //            FacCode,
            //            IsSampleItem,
            //            SampleMonthCode,
            //            IsLikeItem,
            //            LineCode,
            //            pnlFixedDate.Visible ? Convert.ToDateTime(txtFixedDate.Text).Date : DateTime.Now
            //            ).FirstOrDefault().Column1;

            txtFixedDate.Text = dtpFixedDated.Value.ToString("yyyy-MM-dd");
            txtFixedDate.Focus();

            if (pnlFixedDate.Visible)
            {
                txtJobNo_KeyDown(null, new KeyEventArgs(Keys.Return));
            }
        }

        private void txtQty_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                try
                {
                    JobMaximumQty = Convert.ToInt32(txtQty.Text.Replace(",", ""));

                    txtQty.Text = JobMaximumQty.ToString("#,##0");
                    txtQty.Enabled = false;

                    var db = new DB();

                    string[] ScanArr = txtJobNo.Text.Split('-');

                    var bEnable = db.BoardHeaders.First(f => f.ItemNo == this.txtItemNo.Text.ToUpper());


                    var res = from qry in db.QryBarcodeDetails

                              where
                              qry.JobNo == ScanArr[0] &&
                              qry.JobSuffix == Convert.ToInt32(ScanArr[1]) //&&
                                                                           // qry.DetailKey == new Guid(txtItemNo.Tag.ToString())

                              orderby Convert.ToInt64(qry.ControlSerial) descending
                              select new
                              {
                                  qry.ControlSerial,
                                  qry.BarcodeData,
                                  qry.IsVerify,
                                  qry.ComName,
                                  qry.BoardId,
                                  qry.EmpId,
                                  qry.IsCancel
                              };
                    JobVerifyQty = res.Count(w => w.IsVerify && !w.IsCancel);

                    UpdateQtyBroadCast();

                    if (res.Count(j => j.IsVerify && !j.IsCancel) >= JobMaximumQty)
                    {
                        lbFinish.Visible = true;
                        txtInputBarcode.Enabled = false;
                    }
                    else
                    {
                        lbFinish.Visible = false;
                        txtInputBarcode.Enabled = true;
                    }

                    if (JobVerifyQty < JobMaximumQty)
                    {
                        lbFinish.Visible = false;
                        txtInputBarcode.Enabled = true;
                        splitContainer1.Panel1.Enabled = true;
                        splitContainer1.Panel2.Enabled = true;
                    }

                }
                catch (Exception)
                {


                }
            }
        }

        [System.Diagnostics.DebuggerStepThrough()]
        private void PrintLabel_FormClosing(object sender, FormClosingEventArgs e)
        {
            var db = new HET.DB();
            string computername = Environment.GetEnvironmentVariable("COMPUTERNAME");
            String strHostName = string.Empty;
            strHostName = Dns.GetHostName();
            Console.WriteLine("Local Machine's Host Name: " + strHostName);
            // Then using host name, get the IP address list..
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);
            IPAddress[] addr = ipEntry.AddressList;
            var check = from c in db.Computers
                        where
                            c.ComputerName.ToUpper() == computername.ToUpper()

                        select c;
            if (check.Count() > 0)
            {
                var cp = check.First();
                cp.IPAddress = addr.ToList().First(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
                cp.IsValid = false;
                cp.ActiveJobNo = null;
                db.SubmitChanges();
            }

            try
            {
                var lockM = db.UserAccounts.First(f => f.EmployeeCode == txtEmployee1.Tag.ToString());
                lockM.IsMachineLock = false;
                lockM.MachineName = null;
                db.SubmitChanges();
            }
            catch (Exception)
            {


            }




        }

        int KeepCounter = 0;
        bool stateStart = false;
        bool isSend = false;
        private void btnStart_Click(object sender, EventArgs e)
        {
            tmTrigger.Tag = 0;
            if (this.txtEmployee1.Tag == null) return;
            if (!stateStart)
            {


                if (!ConnectVideoJET())
                {
                    stateStart = false;
                    tmTrigger.Enabled = stateStart;
                    btnStart.BackColor = Color.Empty;
                    btnStart.Text = "Start";
                    btnStart.Image = Properties.Resources.ApproveRequest_32x32;
                }
                else
                {
                    stateStart = true;
                    tmTrigger.Interval = 2000;
                    btnStart.Text = "Stop";
                    tmTrigger.Enabled = stateStart;
                    btnStart.Image = Properties.Resources.Close_32x32;
                }
            }
            else
            {
                stateStart = false;
                tmTrigger.Enabled = stateStart;
                btnStart.BackColor = Color.Empty;
                btnStart.Text = "Start";
                btnStart.Image = Properties.Resources.ApproveRequest_32x32;
                try
                {
                    if(clientSocket.Connected)
                    clientSocket.Close();
                }
                catch (Exception)
                {
 
                }
            }
        }

        private void tmTrigger_Tick(object sender, EventArgs e)
        {
            //  lbState.Text = DateTime.Now.ToString("yyMMddhhMMss");

            if (clientSocket.Connected)
            {

                KeepCounter = VideoJetCounter();

                if (KeepCounter >= Convert.ToInt32(tmTrigger.Tag))
                {
                    isSend = false;

                }
                GetData();

                if (!isSend)
                {


                    // GetData();


                    isSend = true;
                    tmTrigger.Tag = Convert.ToInt32(tmTrigger.Tag) + 1;

                    AddRowToList(true, false, Common.Board.Board1);
                }
            }
            else
            {

            }



        }
        private void btnBoardVDO1_Click(object sender, EventArgs e)
        {
            var test = new FormVideoJETTest(SerialPort1);
            test.ShowDialog(this);
        }

        bool ConnectVideoJET()
        {
            try
            {
                if (btnStart.Tag == null || Convert.ToBoolean(btnStart.Tag) == false)
                {
                    clientSocket = new TcpClient();
                    clientSocket.SendTimeout = 5;
                    clientSocket.ReceiveTimeout = 5;
                    btnStart.Text = "Connecting...";
                    btnStart.Image = null;
                    int loopCount = 0;
                    while (!clientSocket.Connected || loopCount == 10)
                    {
                        try
                        {
                            Application.DoEvents();
                            clientSocket.Connect(txtIPAddress.Text, 3100);

                            serverStream = clientSocket.GetStream();
                        }
                        catch (Exception)
                        {


                        }

                        Thread.Sleep(100);
                        loopCount++;
                    }


                    // listBox1.Items.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " Loop Count=> " + loopCount.ToString());

                    if (loopCount >= 10)
                    {
                        return false;
                    }

                    SendData(Convert.ToChar(2) + "RA" + Convert.ToChar(3)); //'Clear counter
                    SendData(Convert.ToChar(2) + "GA" + Convert.ToChar(3)); //'Get counter

                    // serverStream = clientSocket.GetStream();
                    btnStart.Tag = true;


                    btnStart.Text = "Stop";
                    btnStart.Image = Properties.Resources.Close_32x32;
                    // listBox1.Items.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " => " + "Connected");

                    // GetData();

                    return true;
                }
                else
                {

                    btnStart.Tag = false;
                    clientSocket.Close();
                    return false;
                    // listBox1.Items.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " => " + "Disconnected");
                }
            }
            catch (Exception ex)
            {
                //btbConnect.Text = "Connect";
                //lbError.Text = "Error: " + ex.Message;
                // MessageBox.Show(ex.Message);

                return false;
            }
        }

        int VideoJetCounter()
        {
            if (clientSocket.Connected)
            {
                string data = Convert.ToChar(2) + "GA" + Convert.ToChar(3);

                SendData(data);

                serverStream = clientSocket.GetStream();


                int buffSize = clientSocket.ReceiveBufferSize;
                byte[] inStream = new byte[buffSize];



                serverStream.Read(inStream, 0, buffSize);

                string resullt = Encoding.ASCII.GetString(inStream);
                resullt = resullt.Replace(Convert.ToChar(2), '\0');
                resullt = resullt.Remove(Convert.ToChar(3), '\0');

                int StrIndex = resullt.IndexOf("00");

                if (StrIndex < 0)
                {
                    return -1;
                }
                else
                {
                    resullt = resullt.Substring(StrIndex, 8);
                    return Convert.ToInt32(resullt);
                }
            }
            else
            {
                return -1;
            }
        }

        string GetData()
        {
            if (clientSocket.Connected && serverStream.CanRead)
            {
                try
                {
                    serverStream = clientSocket.GetStream();
                    int buffSize = clientSocket.ReceiveBufferSize;
                    byte[] inStream = new byte[buffSize];
                    serverStream.Read(inStream, 0, buffSize);

                    string data = "" + Encoding.ASCII.GetString(inStream);


                    
                    return data;
                }
                catch (Exception)
                {


                }




            }
            return "";

        }


        void SendData(string VedioJetTxt)
        {

            if (serverStream.CanWrite)
            {
                serverStream = clientSocket.GetStream();

                byte[] outStream = Encoding.ASCII.GetBytes(VedioJetTxt);

                serverStream.Write(outStream, 0, outStream.Length);
                serverStream.Flush();
                Thread.Sleep(10);
            }
            else
            {
                //  lbError.Text = "Not connected";
            }
        }

        private void dgvBoardList_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
        void KeepLog(string head, string section, string log)
        {
            string islogEnable = System.Configuration.ConfigurationSettings.AppSettings["logs"];

            if (islogEnable != null && islogEnable == "true")
            {
                try
                {
                    if (!System.IO.Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Logs"))
                    {
                        System.IO.Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\Logs");
                    }

                    using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\" + DateTime.Now.ToString("yyyy-MM-dd") + "_" + Environment.GetEnvironmentVariable("COMPUTERNAME") + "_" + "_" + this.txtJobNo.Text + ".log", true))
                    {
                        file.WriteLine(head + " : " + DateTime.Now.ToString("HH:mm:ss") + " => " + section + " => " + log);
                        file.Close();
                    }
                }
                catch (Exception)
                {


                }
            }




        }

        void UpdateQtyBroadCast()
        {
            try
            {
                var db = new DB();
                string[] ScanArr = txtJobNo.Text.Split('-');

                var res = from qry in db.QryBarcodeDetails
                          where
                                    qry.JobNo == ScanArr[0] &&
                                    qry.JobSuffix == Convert.ToInt32(ScanArr[1])


                          //qry.ModifyDateRaw.Value.Date == Convert.ToDateTime(this.txtFixedDate.Text).Date
                          // qry.DetailKey == new Guid(txtItemNo.Tag.ToString())

                          orderby qry.ControlSerial.Value descending
                          select new
                          {
                              qry.ControlSerial,
                              qry.BarcodeData,
                              qry.IsVerify,
                              qry.ComName,
                              qry.BoardId,
                              qry.EmpId,


                              qry.IsCancel,
                              qry.ModifyDateRaw
                          };

                int total = res.Count();
                int verify = res.Count(c => c.IsVerify);
                int unverify = res.Count(c => !c.IsVerify);
                int cancel = res.Count(c => c.IsCancel);


                this.lbJobVerifyCount.Text = "Verified: " + verify.ToString("#,##0");
                this.lbJobNotVerifyCount.Text = (IsValidVideoJETOnline ? "Cancel :": "Not Verified: ") + (IsValidVideoJETOnline ? cancel.ToString("#,##0") : unverify.ToString("#,##0"));
                this.lbJobCount.Text = "Total: " + (IsValidVideoJETOnline ? JobVerifyQty.ToString("#,##0"): verify.ToString("#,##0")) + " of " + JobMaximumQty.ToString("#,##0");

                Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);


                string sendCommand = "HET:DATA:QTY:" + txtJobNo.Text + ":" + Convert.ToInt32(this.txtQty.Text).ToString() + ":" + total.ToString() + ":" + verify.ToString() + ":" + unverify.ToString();

                byte[] sendbuf = Encoding.ASCII.GetBytes(sendCommand);
                //w.IsValid && w.ComputerName != Environment.GetEnvironmentVariable("COMPUTERNAME") && 
                foreach (var item in db.Computers.Where(w => w.ActiveJobNo == this.txtJobNo.Text.ToUpper()))
                {
                    IPAddress broadcast = IPAddress.Parse(item.IPAddress);
                    IPEndPoint ep = new IPEndPoint(broadcast, 11000);
                    s.SendTo(sendbuf, ep);
                }

                Console.WriteLine("Message sent to the broadcast address");

                KeepLog("UDP-OUT", "DATA", sendCommand);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Udp:" + ex.Message);
            }

        }
    }


}
