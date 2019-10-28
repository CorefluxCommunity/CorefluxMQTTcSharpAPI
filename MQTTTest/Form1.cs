using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Coreflux.API.cSharp.Networking.MQTT;
using System.Threading;


namespace MQTTTest
{
    public partial class Form1 : Form
    {
        bool connectstatus;
        public Form1()
        {
            InitializeComponent();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            Connect();

        }

        private void timer1_Tick(object sender, EventArgs e)
        {


        }

        private void Connect()
        {
            connectstatus = false;
            MQTTController.ClientName = "TESTAMOS";
            MQTTController.Start("127.0.0.1");
            timer1.Enabled = true;
        }

        private void btnTest1_Click(object sender, EventArgs e)
        {
            try
            {
                Connect();
                MQTTController.PersistentConnection = false;
                MQTTController.SetData(@"Machine1/MainCode/VarC", "Test");
                Thread.Sleep(100);
                var test = MQTTController.GetData(@"Machine1/MainCode/VarC");
                if (test.Equals("Test"))
                {
                    label2.Text = "OK";
                }
                else
                {
                    label2.Text = "FAIL";
                }

            }
            catch (Exception ex)
            {

                EXWriter(ex.Message);
            }
        }
        private void btnTest2_Click(object sender, EventArgs e)
        {
            try
            {
                Connect();
                MQTTController.PersistentConnection = false;
                for (int i = 0; i < 100; i++)
                {
                  
                    MQTTController.SetData(@"Machine1/MainCode/VarC", "Test");
                    Thread.Sleep(100);
                    var test = MQTTController.GetData(@"Machine1/MainCode/VarC");
                    if (test.Equals("Test"))
                    {
                        label3.Text = "OK " + i.ToString();
                        Application.DoEvents();
                    }
                    else
                    {
                        label3.Text = "FAIL";
                    }
                }



            }
            catch (Exception ex)
            {

                EXWriter(ex.Message);
            }
        }


        private void btnTest3_Click(object sender, EventArgs e)
        {
            try
            {
                MQTTController.PersistentConnection = true;
                Connect();
              
                MQTTController.SetData(@"Machine1/MainCode/VarC", "Test",2,true);
                Thread.Sleep(100);
                MQTTController.Stop();
                Thread.Sleep(100);
                Connect();
                Thread.Sleep(100);
                var test = MQTTController.GetData(@"Machine1/MainCode/VarC",2);
                if (test.Equals("Test"))
                {
                    label12.Text = "OK ";
                }
                else
                {
                    label12.Text = "FAIL";
                }
            }
            catch (Exception ex)
            {

                EXWriter(ex.Message);
            }
        }


        public void EXWriter(string exception)
        {
            lblEX.Text = "Exception: " + exception;
        }
    }

}
