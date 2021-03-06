﻿// RegisterAssembly: DefinitionsCore.dll
// RegisterAssembly: BasicHardware.dll
// RegisterAssembly: PluginManager.dll
// RegisterAssembly: HardwareClient.dll
// RegisterSystemAssembly: System.dll
// RegisterAssembly: SpectrumLibrary.dll
// RegisterSystemAssembly: System.Xml.dll
// RegisterAssembly: Util.dll
/*
Copyright 2011 Jake Ross
 Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at 
	http://www.apache.org/licenses/LICENSE-2.0 
Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License. 
*/
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

using Thermo.Imhotep.BasicHardware;
using Thermo.Imhotep.Definitions.Core;
using Thermo.Imhotep.SpectrumLibrary;
using Thermo.Imhotep.Util;

class RemoteControl
{
	private static int m_port = 1069;
	public static TcpListener listener;
	public static Socket udp_sock;
	public static bool use_udp=true;
	public static string scan_data;
	
	public static object m_lock=new object();
	
	public const int ON = 1;
	public const int OFF = 0;

	public const int OPEN = 1;
	public const int CLOSE = 0;
	
	public static IRMSBaseCore Instrument;
    public static IRMSBaseMeasurementInfo m_restoreMeasurementInfo;
    
    public static double last_y_symmetry=-11.36;
	
	public static void Main ()
	{
		Instrument= ArgusMC;
		GetTuningSettings();
		PrepareEnvironment();
		
        //setup data recording
		InitializeDataRecord();
		
		if (use_udp)
		{
			UDPServeForever();
		}
		else
		{
		 	TCPServeForever();
		}
		
	}
    //====================================================================================================================================
	// 
	//	Commands are case sensitive and in CamelCase
	//	Commands:
	//		GetTuningSettingsList #return a comma separated string
	//		SetTuningSettings
	//		GetData #return a comma separated string of 5 or 6 values. Sorted high to low mass H2,H1,AX,L1,L2[,CDD]
	//		SetIntegrationTime <seconds>	
	// 		BlankBeam <true or false> if true set y-symmetry to -50 else return to previous value
    
    //===========Cup/SubCup Configurations==============================
    // 		GetCupConfigurationList
	//		GetSubCupConfigurationList
	//		GetActiveCupConfiguration
	//		GetActiveSubCupConfiguration
	// 		GetSubCupParameters returns list of Deflection voltages and the Ion Counter supply voltage
	//		SetSubCupConfiguration <sub cup name>
	
	//===========Ion Counter============================================
	//      ActivateIonCounter
	//      DeactivateIonCounter
	
	//===========Ion Pump Valve=========================================
    //      Open  #open the Ion pump to the mass spec
	//		Close #closes the Ion pump to the mass spec
	//		GetValveState #returns True for open false for close
	
	//===========Magnet=================================================
	//		GetMagnetDAC
	//		SetMagnetDAC <value> #0-10V
	
	//===========Source=================================================
	//		GetHighVoltage or GetHV
	//		SetHighVoltage or SetHV <kV>
	//      GetTrapVoltage
	//      SetTrapVoltage <value>
    //      GetElectronEnergy
    //      SetElectronEnergy <value>
    //      GetYSymmetry
    //      SetYSymmetry <value>
    //      GetZSymmetry
    //      SetZSymmetry <value>
    //      GetZFocus
    //      SetZFocus <value>
    //      GetIonRepeller
    //      SetIonRepeller <value>
    //      GetExtractionLens
    //      SetExtractionLens <value>
    
    //==========Detectors===============================================
	//      GetDeflection <name>
	//      SetDeflection <name>,<value>
	//      GetIonCounterVoltage
	//      SetIonCounterVoltage <value>
	//==================================================================
	//		Error Responses:
	//			Error: Invalid Command   - the command is poorly formated or does not exist. 
	//			Error: could not set <hardware> to <value> 
	//====================================================================================================================================
	
	private static string ParseAndExecuteCommand (string cmd)
	{
		
		string result = "Error: Invalid Command";

		Logger.Log(LogLevel.UserInfo, String.Format("Recieved: {0}", cmd));
		string[] args = cmd.Split (' ');
		double r;
		switch (args[0]) {

		
		case "GetTuningSettingsList":
			result = GetTuningSettings();
			break;
			
		case "SetTuningSettings":
			if(SetTuningSettings(args[1]))
			{
				result="OK";
			}
			else
			{
				result=String.Format("Error: could not set tuning settings {0}",args[1]);
			}
			break;
			
		case "GetData":
			result=scan_data;
			break;
			
		case "SetIntegrationTime":
			result=SetIntegrationTime(Convert.ToDouble(args[1]));
			break;
			
		case "BlankBeam":
			double killValue=last_y_symmetry;
			if (args[1]=="true")
			{
				//remember the non blanking Y-Symmetry value
				Instrument.GetParameter("Y-Symmetry Set", out last_y_symmetry);
				killValue=-  50;
			}
			result=SetParameter("Y-Symmetry Set",killValue);
			break;
			
//============================================================================================
//   Cup / SubCup Configurations
//============================================================================================					
        case "GetCupConfigurationList":
			List<string> cup_names = GetCupConfigurations ();
			result = string.Join ("\r", cup_names.ToArray ());
			break;
		
		case "GetSubCupConfigurationList":
			string config_name = args[1];
			List<string> sub_names = GetSubCupConfigurations (config_name);
			result = string.Join ("\r", sub_names.ToArray ());
			break;
			
		case "GetActiveCupConfiguration":
			result=Instrument.CupConfigurationDataList.GetActiveCupConfiguration().Name;
			break;
			
		case "GetActiveSubCupConfiguration":
			result=Instrument.CupConfigurationDataList.GetActiveSubCupConfiguration().Name;
			break;
			
		case "GetSubCupParameters":
			result=GetSubCupParameters();
			break;
			
		case "SetSubCupConfiguration":
			if(ActivateCupConfiguration ("Argon", cmd.Remove(0,23)))
			{
				result="OK";
			}
			else
			{
				result=String.Format("Error: could not set sub cup to {0}", args[1]);
			}
			break;	
			
//============================================================================================
//   Ion Counter
//============================================================================================					
		case "ActivateIonCounter":
			result="OK";
			SetIonCounterState(true);
			break;
		case "DeactivateIonCounter":
			result="OK";
			SetIonCounterState(false);
			break;
			
//============================================================================================
//   Ion Pump Valve
//============================================================================================					
        case "Open":
			//hardcode name for now
			result=SetParameter("Valve Ion Pump Set",OPEN);
			break;
			
		case "Close":
			Logger.Log(LogLevel.Debug, String.Format("Executing {0}", cmd));
			result=SetParameter("Valve Ion Pump Set",CLOSE);
			break;
			
		case "GetValveState":
			Logger.Log(LogLevel.Debug, String.Format("Executing {0}", cmd));
			result=GetValveState("Valve Ion Pump Set");
			Logger.Log(LogLevel.Debug, String.Format("Valve state {0}", result));
			break;

//============================================================================================
//   Magnet
//============================================================================================					
		case "GetMagnetDAC":
			//double r;
			if (Instrument.GetParameter("Field Set", out r))
			{
				result=r.ToString(CultureInfo.InvariantCulture);
			}
			break;
			
		case "SetMagnetDAC":

			//double dac= Convert.ToDouble(args[1])/ Math.Pow(10,args[1].Length-2);
			//Logger.Log(LogLevel.UserInfo, String.Format("DAC {0}",dac));
			//result=SetParameter("Field Set",dac);
			double dac= Convert.ToDouble(args[1]);
			result=SetParameter("Field Set",dac);
			break;
			
//============================================================================================
//    Source Parameters
//============================================================================================			
		case "GetHighVoltage":
			if(Instrument.GetParameter("Acceleration Reference Set",out r))
			{
				result=(r*1000).ToString(CultureInfo.InvariantCulture);
			}
			break;
			
		case "SetHighVoltage":
			result=SetParameter("Acceleration Reference Set", Convert.ToDouble(args[1])/1000.0);
			break;
		case "GetHV":
			if(Instrument.GetParameter("Acceleration Reference Set",out r))
			{
				result=(r).ToString(CultureInfo.InvariantCulture);
			}
			break;
			
		case "SetHV":
			result=SetParameter("Acceleration Reference Set", Convert.ToDouble(args[1])/1000.0);
			break;
		case "GetTrapVoltage":
			//double r;
			if(Instrument.GetParameter("Trap Voltage Readback",out r))
			{
				result=r.ToString(CultureInfo.InvariantCulture);
			}
			break;
			
		case "SetTrapVoltage":
			result=SetParameter("Trap Voltage Set",Convert.ToDouble(args[1]));
			break;
		
		case "GetElectronEnergy":
			//double r;
			if(Instrument.GetParameter("Electron Energy Readback",out r))
			{
				result=r.ToString(CultureInfo.InvariantCulture);
			}
			break;
			
		case "SetElectronEnergy":
			result=SetParameter("Electron Energy Set",Convert.ToDouble(args[1]));
			break;
			
		case "GetIonRepeller":
			//double r;
			if(Instrument.GetParameter("Ion Repeller Set",out r))
			{
				result=r.ToString(CultureInfo.InvariantCulture);
			}
			break;
			
		case "SetIonRepeller":
			result=SetParameter("Ion Repeller Set",Convert.ToDouble(args[1]));
			break;
		
		case "GetYSymmetry":
			//double r;
			if(Instrument.GetParameter("Y-Symmetry Set",out r))
			{
				result=r.ToString(CultureInfo.InvariantCulture);
			}
			break;
			
		case "SetYSymmetry":
			result=SetParameter("Y-Symmetry Set",Convert.ToDouble(args[1]));
			break;
		
		case "GetZSymmetry":
			//double r;
			if(Instrument.GetParameter("Z-Symmetry Set",out r))
			{
				result=r.ToString(CultureInfo.InvariantCulture);
			}
			break;
			
		case "SetZSymmetry":
			result=SetParameter("Z-Symmetry Set",Convert.ToDouble(args[1]));
			break;
			
		case "GetZFocus":
			//double r;
			if(Instrument.GetParameter("Z-Focus Set",out r))
			{
				result=r.ToString(CultureInfo.InvariantCulture);
			}
			break;
		
		case "SetZFocus":
			result=SetParameter("Z-Focus Set",Convert.ToDouble(args[1]));
			break;
		
		case "GetExtractionLens":
			//double r;
			if(Instrument.GetParameter("Extraction Lens Set",out r))
			{
				result=r.ToString(CultureInfo.InvariantCulture);
			}
			break;
			
		case "SetExtractionLens":
			result=SetParameter("Extraction Lens Set",Convert.ToDouble(args[1]));
			break;
		
//============================================================================================
//    Detectors
//============================================================================================			
        case "GetDeflection":
            //double r;
            if(Instrument.GetParameter(String.Format("Deflection {0} Set",args[1]), out r))
            {
                result=r.ToString(CultureInfo.InvariantCulture);
            }
            break;
            
        case "SetDeflection":
            string[] pargs=args[1].Split(',');
            result=SetParameter(String.Format("Deflection {0} Set",pargs[0]),Convert.ToDouble(pargs[1]));
		    break;
		    
		case "GetIonCounterVoltage":
			//double r;
            if(Instrument.GetParameter("CDD Supply Set",out r))
            {
                result=r.ToString(CultureInfo.InvariantCulture);
            }
            break;
            
        case "SetIonCounterVoltage":
        	result=SetParameter("CDD Supply Set", Convert.ToDouble(args[1]));
            break;
		    	    
		}

		
		return result;
	}
	
	private static bool PrepareEnvironment()
      {
        m_restoreMeasurementInfo=Instrument.MeasurementInfo;
        return Instrument.ScanTransitionController.InitializeScriptScan(m_restoreMeasurementInfo);
      }
      
	public static void InitializeDataRecord()
	{
		// attach a handler to the ScanDataAvailable Event
		ArgusMC.ScanDataAvailable+=ScanDataAvailable;
	}
	
	public static void Dispose()
	{
		Logger.Log (LogLevel.UserInfo, "Stop Server");
	
		// deattach the handler from the ScanDataAvailable Event
		ArgusMC.ScanDataAvailable-=ScanDataAvailable;
		
		//shutdown the server
		if (use_udp)
		{
			udp_sock.Close();
		}
		else
		{
			listener.Stop();
		}

	}
	//====================================================================================================================================
	//Qtegra Methods
	//====================================================================================================================================
	public static void SetIonCounterState(bool state)
	{
		IRMSBaseCupConfigurationData activeCupData = Instrument.CupConfigurationDataList.GetActiveCupConfiguration();
		foreach(IRMSBaseCollectorItem col in activeCupData.CollectorItemList)
		{
			if ((col.CollectorType == IRMSBaseCollectorType.CounterCup) && (col.Mass.HasValue == true))
			{
			    col.Active = state;
			}
		}

	}
	
	public static string GetValveState(string hwname)
	{
		string result="Error";
		double rawValue;
		if(Instrument.GetParameter(hwname,out rawValue))
		{
			if (rawValue==OPEN)
			{
				result="True";
			}
			else
			{
				result="False";
			}
		}
		return result;
		
	}
	
	public static string SetParameter(string hwname, int val)
	{
		string result="OK";
		if (!Instrument.SetParameter(hwname, val))
		{
			result=String.Format("Error: could not set {0} to (int) {1}", hwname, val);
		}
		return result;
	}
	public static string SetParameter(string hwname, double val)
	{	string result="OK";
	
	
		if (!Instrument.SetParameter(hwname, val))
		{
			result=String.Format("Error: could not set {0} to (double) {1}", hwname, val);
		}
		return result;
	}
	
	public static string SetIntegrationTime(double t)
	{
		//t in ms
		string result="OK";
	//	IRMSBaseMeasurementInfo nMI= new IRMSBaseMeasurementInfo(Instrument.MeasurementInfo);
	//	nMI.IntegrationTime = t*1000;
	//	if (!Instrument.ScanTransitionController.StartMonitoring (nMI))
	//	{
	//		Logger.Log(LogLevel.UserError, "Could not start the modified monitor");
	//		result=String.Format("Error: could not set integration time to {0}",t);
	//	}
		
		return result;
	}
	public static void ScanDataAvailable(object sender, EventArgs<Spectrum> e)
	{ 
	
		lock(m_lock)
		{
			
			List<string> data = new List<string>();
			Spectrum spec = e.Value.Clone() as Spectrum;
		
			int cnt=1;
			foreach (Series series in spec)
			{
				foreach (SpectrumData point in series)
				{
					data.Add(String.Format("{0}",point.Analog.ToString(CultureInfo.InvariantCulture)));
					cnt++;
				}
			}
			
			data.Reverse();
			if (cnt==7)
			{
				List<string> ndata = new List<string>();
				
				ndata.Add(data[1]);
				ndata.Add(data[2]);
				ndata.Add(data[3]);
				ndata.Add(data[4]);
				ndata.Add(data[5]);
				ndata.Add(data[0]);
				scan_data=string.Join(",",ndata.ToArray());
			}
			else
			{
				scan_data=string.Join(",",data.ToArray());
			}
		}
	}
	
	private static bool RunMonitorScan (double? mass)
	{
		IRMSBaseCupConfigurationData cupData = Instrument.CupConfigurationDataList.GetActiveCupConfiguration();
		IRMSBaseMeasurementInfo newMeasurementInfo = 
			new IRMSBaseMeasurementInfo(
				Instrument.MeasurementInfo.ScanType,
				Instrument.MeasurementInfo.IntegrationTime,
				Instrument.MeasurementInfo.SettlingTime,
				(mass.HasValue) ? mass.Value : cupData.CollectorItemList.GetMasterCollectorItem().Mass.Value,
				cupData.CollectorItemList,
				cupData.MassCalibration
				);
		Instrument.ScanTransitionController.StartMonitoring(newMeasurementInfo);
		return true;
	}
	
	private static string GetTuningSettings()
	{
		TuneSettingsManager tsm = new TuneSettingsManager(Instrument.Id);
		List<string> result = new List<string>();
		tsm.EntryType= typeof(TuneSettings);
		result.AddRange(tsm.GetEntries());
		return string.Join(",",result.ToArray());	
	}
	
	private static bool SetTuningSettings(string name)
	{
		
		TuneSettingsManager tsm = new TuneSettingsManager(Instrument.Id);
		TuneSettings tuneSettings = tuneSettings = tsm.ReadEntry(name) as TuneSettings; ;
		//TuneSettings tuneSettings =tsm.ReadEntry(name);
		TuneParameterBlock tuneBlock = null;
		if (tuneSettings == null)
		{
			Logger.Log(LogLevel.UserError, String.Format("Could not load tune setting \'{0}\'.", name));
			return false;
		}
		else
		{
			tuneBlock = tuneSettings.Object as TuneParameterBlock;
			if (tuneBlock == null)
			{
				Logger.Log(LogLevel.UserError, string.Format("Tune setting \'{0}\' could not convert to tuneblock.", name));
				return false;
			}
			else
			{
				Instrument.TuneParameters.Parameters = tuneBlock;
				Logger.Log(LogLevel.UserInfo, string.Format("Tune setting : \'{0}\' successfully load!", name));
			}
		}
		return true;
	}
	
	private static string GetSubCupParameters()
	{	
		List<string> parameters = new List<string>(new string[]{
												"Deflection H2 Set",
												"Deflection H1 Set",
												"Deflection AX Set",
												"Deflection L1 Set",
												"Deflection L2 Set",
												"Deflection CDD Set",
												"CDD Supply Set",
													});
		double value=0.0;
		List<string> data = new List<string>();
		foreach (var item in parameters)
		{
			if(Instrument.GetParameter(item, out value))
			{
				data.Add(String.Format("{0},{1}", item, value));
			}
		}
		return String.Join(";", data.ToArray());
	}
	//-------------------------------------------------------------------------------------------------------------------------------------------------------------
	// <summary>
	// Get all cup configurations names from the system.
	// </summary>
	// <returns>A list of cup configuration names.</returns>
	//-------------------------------------------------------------------------------------------------------------------------------------------------------------
	private static List<string> GetCupConfigurations ()
	{
		List<string> result = new List<string>();
		foreach (var item in Instrument.CupConfigurationDataList) {
			result.Add (item.Name);
		}
		return result;
	}
	//-------------------------------------------------------------------------------------------------------------------------------------------------------------
	// <summary>
	// Get all sub cup configurations names from a cup cupconfiguration.
	// </summary>
	// <param name="cupConfigurationName"></param>
	// <returns>A list of sub cup configuration names.</returns>
	//-------------------------------------------------------------------------------------------------------------------------------------------------------------
	private static List<string> GetSubCupConfigurations (string cupConfigurationName)
	{
		IRMSBaseCupConfigurationData cupData = Instrument.CupConfigurationDataList.FindCupConfigurationByName (cupConfigurationName);
		if (cupData == null) {
			Logger.Log (LogLevel.UserError, String.Format ("Could not find cup configuration \'{0}\'.", cupConfigurationName));
			return null;
		}
		
		List<string> result = new List<string>();
		foreach (var item in cupData.SubCupConfigurationList) {
			result.Add (item.Name);
		}
		

		return result;
	}
	
	//-------------------------------------------------------------------------------------------------------------------------------------------------------------
	// <summary>
	// Activate a cup configuration and sub cup configuration.
	// </summary>
	// <param name="cupConfigurationName">The cup configuration name.</param>
	// <param name="subCupConfigurationName">The sub cup configuration name.</param>
	// <param name="mass">A nullable mass value. If has a value then use the mass for the monitor scan; otherwise use the master cup mass for the monitor scan.</param>
	// <returns><c>True</c> if successfull; otherwise <c>false</c>.</returns>
	//-------------------------------------------------------------------------------------------------------------------------------------------------------------
	private static bool ActivateCupConfiguration (string cupConfigurationName, string subCupConfigurationName)
	{
		
		//Console.WriteLine (cupConfigurationName);
		//Console.WriteLine (subCupConfigurationName);
		IRMSBaseCupConfigurationData cupData = Instrument.CupConfigurationDataList.FindCupConfigurationByName (cupConfigurationName);
		if (cupData == null) {
			Logger.Log (LogLevel.UserError, String.Format ("Could not find cup configuration \'{0}\'.", cupConfigurationName));
			return false;
		}
		IRMSBaseSubCupConfigurationData subCupData = cupData.SubCupConfigurationList.FindSubCupConfigurationByName (subCupConfigurationName);
		if (subCupData == null) {
			Logger.Log (LogLevel.UserError, String.Format ("Could not find sub cup configuration \'{0}\' in cup configuration.", subCupConfigurationName, cupConfigurationName));
			return false;
		}
		Instrument.CupConfigurationDataList.SetActiveItem (cupData.Identifier, subCupData.Identifier, Instrument.CupSettingDataList, null);
		Instrument.SetHardwareParameters (cupData, subCupData);
		bool success = Instrument.RequestCupConfigurationChange (Instrument.CupConfigurationDataList);
		if (!success) {
			Logger.Log (LogLevel.UserError, "Could not request a cup configuration change.");
			return false;
		}
		return true;
	}
	//====================================================================================================================================
	//Server Methods
	//====================================================================================================================================
	private static void TCPServeForever ()
	{
		Logger.Log (LogLevel.UserInfo, "Starting TCP Server.");
		listener = new TcpListener (IPAddress.Any, m_port);
		listener.Start ();
		
		
		Thread listenThread = new Thread (new ThreadStart (TCPListen));
		listenThread.Start ();
		
	}
	private static void UDPServeForever()
	{
		Logger.Log (LogLevel.UserInfo, "Starting UDP Server.");
		Thread listenThread = new Thread (new ThreadStart (UDPListen));
		listenThread.Start ();
		
	}
	private static void UDPListen()
	{
		Logger.Log (LogLevel.UserInfo, "UDP Listening.");
		int recv;
		byte[] data= new byte[1024];
		
		IPEndPoint ipep = new IPEndPoint(IPAddress.Any, m_port);
		udp_sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		udp_sock.Bind(ipep);
		

    		IPEndPoint sender = new IPEndPoint(IPAddress.Any, m_port);
	    	EndPoint remote = (EndPoint)sender;
		
            while(true)
            {
            
            	/*
                if(!udp_sock.Connected)
                {
                    break;
                }*/
                data=new byte[1024];
				//Logger.Log(LogLevel.UserInfo, "waiting for client");
                recv=udp_sock.ReceiveFrom(data, ref remote);
	        	//Logger.Log(LogLevel.UserInfo, String.Format(" recv {0}", recv.ToString()));
	        if (recv==0)
                {
                    break;
                }
		//cant assume one read will get all the data. Read until hit an EOL
                string rdata = Encoding.ASCII.GetString(data,0, recv);
                //Logger.Log(LogLevel.UserInfo, rdata);
		/*while (!rdata.Substring(rdata.Length-1,1).Equals("\n"))
		{
			data=new byte[1024];
			recv=udp_sock.ReceiveFrom(data, ref remote);

	        
	        	if (recv==0)
                	{
                   	 break;
               		}
		
                	rdata =rdata+ Encoding.ASCII.GetString(data,0, recv);
		}   
		*/

                string result = ParseAndExecuteCommand(rdata.Trim());
                
                Logger.Log(LogLevel.UserInfo, String.Format("Sending back {0}", result));
                
                udp_sock.SendTo(Encoding.ASCII.GetBytes(result), remote);
                
                        
            }
            
    
		udp_sock.Close();
	}
	private static void TCPListen ()
	{
		
		Logger.Log (LogLevel.UserInfo, "TCP Listening");
		
		while (true) {
			TCPHandle(listener.AcceptTcpClient ());
			
			//TcpClient client = listener.AcceptTcpClient ();
			//Thread clientThread = new Thread (new ParameterizedThreadStart (TCPHandle));
			//clientThread.Start (client);
		}
	}
	
	private static void TCPHandle (TcpClient tcpClient)
	{
		//TcpClient tcpClient = (TcpClient)client;
		
		NetworkStream _stream = tcpClient.GetStream ();
		//string response = Read (_stream);
		
		string result = ParseAndExecuteCommand (TCPRead(_stream).Trim());
		
		TCPWrite(_stream, result);
		
		tcpClient.Close();
	}
	//====================================================================================================================================
	//Network Methods
	//====================================================================================================================================
	
	private static void TCPWrite (NetworkStream stream, string cmd)
	{
		if (stream.CanWrite) {
			Byte[] sendBytes = Encoding.UTF8.GetBytes (cmd);
			stream.Write (sendBytes, 0, sendBytes.Length);
		}
	}
	
	private static string TCPRead (NetworkStream stream)
	{
		int BufferSize = 1024;
		byte[] data = new byte[BufferSize];
		try {
			StringBuilder myCompleteMessage = new StringBuilder ();
			int numberOfBytesRead = 0;
			
			// Incoming message may be larger than the buffer size.
			do {
				numberOfBytesRead = stream.Read (data, 0, data.Length);
				
				myCompleteMessage.AppendFormat ("{0}", Encoding.ASCII.GetString (data, 0, numberOfBytesRead));
			} while (stream.DataAvailable);
			
			//string rd = string.Format ("Read Data: {0} the received text is '{1}'", numberOfBytesRead, myCompleteMessage);
			
			return myCompleteMessage.ToString ();
		} catch (Exception e) {
			string error = string.Format ("Could not read from the NetworkStream. {0}", e.ToString ());
			Logger.Log(LogLevel.Warning, error);
		}
		return string.Empty;
	}
}


