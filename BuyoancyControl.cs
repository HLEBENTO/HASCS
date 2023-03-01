using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;


namespace BuyoancyControl
{
    partial class Program : MyGridProgram
    {


		// HELLBENT's Automatic Submarine Control Script [For Water Mod]
		// Version: 1.0 (21.01.2023 04:31)
		// A special thanks to Andon, because I did not have to write it entirely from scratch.
		// Andon's script (obsolete)(https://steamcommunity.com/sharedfiles/filedetails/?id=2444715869)
		// You can find the guide on YouTube and the manual in Steam.
		// YouTube: https://youtu.be/pe3mDV7VbaU
		// Steam: (Coming Soon)
		// And link to script itself: https://steamcommunity.com/sharedfiles/filedetails/?id=2921178770

		// === DON'T MODIFY SCRIPT. USE CUSTOMDATA OF THE PB!!! ===

		// === DON'T MODIFY SCRIPT. USE CUSTOMDATA OF THE PB!!! ===

		// === DON'T MODIFY SCRIPT. USE CUSTOMDATA OF THE PB!!! ===



		//Start copying the code into the program block after this line:

		// Block Tags
		string PUMP_TAG = "[PUMP]";
		string DRAIN_TAG = "[DRAIN]";
		string RC_TAG = "[SUBMARINE]";
		string LCD_TAG = "[Buoyancy Control LCD]";
		string BULK_TAG = "[BULKHEAD]";

		// Ice margin and minimum amount.
		int MARGIN = 100;
		int MIN_ICE = 500;

		// Adjustment to the calculated buoyancy. Will be added to the final calculation. Almost useless.
		int BUOYANCY_ADJUST = 0;

		//Tanks Buoyancy.
		int SLT_Force = 86293; // force of empty small tank (large grid)
		int LLT_Force = 1164959; // force of empty large tank (large grid)
		int SST_Force = 1973; // force of empty small tank (small grid)
		int MST_Force = 26628; // force of empty oxygen tank (small grid)
		int LST_Force = 123277; // force of empty large tank (small grid)

		int Ship_Buoyancy = 0;
		int Required_Depth = -1000;
		double Speed_multiplier = 5;

		//ini.
		MyIni ini = new MyIni();

		//Variables.
		List<IMyTerminalBlock> cargo;
		List<IMyGasTank> tanks;
		List<IMyCollector> collectors;
		List<IMyCollector> pumps;
		List<IMyShipConnector> connectors;
		List<IMyShipConnector> drains;
		List<IMyShipController> controllers;
		List<IMyDoor> Bulkheads;
		List<IMyDoor> Level_1;
		List<IMyDoor> Level_2;
		List<IMyDoor> Level_3;
		List<IMyDoor> Level_4;
		List<IMyDoor> Level_5;
		List<IMyDoor> Level_M1;
		List<IMyDoor> Level_M2;
		List<IMyDoor> Level_M3;
		List<IMyDoor> Level_M4;
		List<IMyDoor> Level_M5;

		IMyTextSurface screen;
		IMyTextSurface display;
		IMyRemoteControl RC;
		string screenText;
		float gravity_c;
		float gravity;
		string dive_status = "Maintaining";
		double Sealevel_Calibrate = 0.666;

		//Check for blocks
		bool RC_Check = false;
		bool DS_Check = false;
		
		// Set up.
		public Program()
		{
			Runtime.UpdateFrequency = UpdateFrequency.Update10;

			//ini parsing.
			if (!ini.TryParse(Me.CustomData))
			{
				Me.CustomData = "";
				throw new Exception("Initialisation failed. CustomData has been cleared. Recompile the script.");
			}
			PUMP_TAG = ini.Get("Settings", "Collectors Name").ToString(PUMP_TAG);
			DRAIN_TAG = ini.Get("Settings", "Connectors Name").ToString(DRAIN_TAG);
			RC_TAG = ini.Get("Settings", "Remote Control Name").ToString(RC_TAG);
			LCD_TAG = ini.Get("Settings", "LCD Name").ToString(LCD_TAG);
			BULK_TAG = ini.Get("Settings", "Bulkheads Name").ToString(BULK_TAG);
			MARGIN = ini.Get("Settings", "Maximum Ballast Deviation").ToInt32(MARGIN);
			MIN_ICE = ini.Get("Settings", "Minimum Ice Stored").ToInt32(MIN_ICE);
			Ship_Buoyancy = ini.Get("Settings", "Ship Buoyancy").ToInt32(Ship_Buoyancy);
			dive_status = ini.Get("Settings", "Dive Status").ToString(dive_status);
			Speed_multiplier = ini.Get("Settings", "Speed Multiplier").ToDouble(Speed_multiplier);
			Required_Depth = ini.Get("Settings", "Required Depth").ToInt32(Required_Depth);
			Sealevel_Calibrate = ini.Get("Settings", "Depth Calibration").ToDouble(Sealevel_Calibrate);

			//Check for LCD.
			try
            {
				display = (IMyTextSurface)GridTerminalSystem.GetBlockWithName(LCD_TAG);
				if (display != null) { DS_Check = true; display.ContentType = ContentType.TEXT_AND_IMAGE; }
            }
            catch { DS_Check = false; }

			//Set up PB's LCD
			screen = Me.GetSurface(0);
			screen.ContentType = ContentType.TEXT_AND_IMAGE;
			screen.FontSize = 0.812f;

			//Check for Remote Control
			try 
			{
				RC = (IMyRemoteControl)GridTerminalSystem.GetBlockWithName(RC_TAG);
				if(RC != null)
					RC_Check = true;
			}
			catch
            {
				RC_Check = false;
			}
		}

		// We'll need this to write to our screen.
		public void WriteText(string x)
		{
			Echo(x);
			screenText += x + "\n";
		}

		//Function for depth level calibration (use "calibrate" argument when submarine on surface)
		public void CalibrateDepth()
        {
			if (RC != null)
			{
				RC.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out Sealevel_Calibrate);
				ini.Set("Settings", "Depth Calibration", Sealevel_Calibrate);
				Me.CustomData = ini.ToString();
			}
		}

		// Now for the real work.
		public void Main(string argument, UpdateType updateSource)
		{
			
			// These variables get continually updated.
			cargo = new List<IMyTerminalBlock>();
			tanks = new List<IMyGasTank>();
			collectors = new List<IMyCollector>();
			pumps = new List<IMyCollector>();
			connectors = new List<IMyShipConnector>();
			drains = new List<IMyShipConnector>();
			controllers = new List<IMyShipController>();
			Bulkheads = new List<IMyDoor>();
			Level_1 = new List<IMyDoor>();
			Level_2 = new List<IMyDoor>();
			Level_3 = new List<IMyDoor>();
			Level_4 = new List<IMyDoor>();
			Level_5 = new List<IMyDoor>();
			Level_M1 = new List<IMyDoor>();
			Level_M2 = new List<IMyDoor>();
			Level_M3 = new List<IMyDoor>();
			Level_M4 = new List<IMyDoor>();
			Level_M5 = new List<IMyDoor>();
			// These new variables are for our use here.
			MyInventoryItem item;
			string pump_status;
			string drain_status;
			bool precision_P;
			bool precision_D;
			double ballast;
			float ship_mass;
			
			float current_gas_force = 0;
			float cargo_mass = 0;
			float dry_cargo_mass = 0;
			float ice = 0;
			int max_vol = 0;
			int cur_vol = 0;
			double Pre_Depth = 0;
			float Depth = 0;

			// Clearing the text box for output.
			screenText = "";
			
			// Depth?
			if (RC != null)
				RC.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out Pre_Depth);
			Depth = (float)(Sealevel_Calibrate - Pre_Depth);
			// Gravity and Mass.
			GridTerminalSystem.GetBlocksOfType(controllers);
			gravity_c = (float)(controllers[0].GetNaturalGravity().Length() / 9.81);
			gravity = (float)(controllers[0].GetNaturalGravity().Length());
			ship_mass = controllers[0].CalculateShipMass().BaseMass;
			// Multiply it by the local gravity.
			ship_mass *= gravity_c;
			

			// Get our cargo information.
			GridTerminalSystem.GetBlocksOfType(cargo);
			for (int i = 0; i < cargo.Count; i++)
			{
				if (cargo[i].HasInventory)
				{
					cargo_mass += cargo[i].GetInventory(0).CurrentMass.ToIntSafe();
					max_vol += cargo[i].GetInventory(0).MaxVolume.ToIntSafe();
					cur_vol += cargo[i].GetInventory(0).CurrentVolume.ToIntSafe();
					
					for (int j = 0; j < cargo[i].GetInventory(0).ItemCount; j++)
					{
						item = cargo[i].GetInventory(0).GetItemAt(j).Value;

						if (item.ToString().Contains("MyObjectBuilder_Ore/Ice"))
						{
							ice += (int)item.Amount;
						}
					}
				}
			}
			dry_cargo_mass = cargo_mass - ice;



			
			// And our tank information.
			GridTerminalSystem.GetBlocksOfType(tanks);
			for (int i = 0; i < tanks.Count; i++)
			{
				float fill = 0;
				int Tank_Force = 0;
				float empty = 0;
				fill = (float)tanks[i].FilledRatio;
				if(tanks[i].Capacity >= 100000)
                {
					if (tanks[i].Capacity >= 500000)
					{
						if (tanks[i].Capacity >= 15000000) Tank_Force = LLT_Force;
						else Tank_Force = LST_Force;
					}
					else Tank_Force = SLT_Force;
                }
				else
                {
					if (tanks[i].Capacity <= 15000) Tank_Force = SST_Force;
					else Tank_Force = MST_Force;
                }
				empty = 1 - fill;
				current_gas_force += Tank_Force * empty ;
			}

			// Figure out our neutral buoyancy.
			double neutral_force;
			double temp_force;
			
			temp_force = ((Ship_Buoyancy + current_gas_force) - ((ship_mass + dry_cargo_mass) * gravity));
			neutral_force = temp_force + BUOYANCY_ADJUST;

			ballast = (neutral_force / gravity);
			
			//Bulkheads
			int Levels_Count = 0;
			int Minus_Levels_Count = 0;
			int Bulkheads_Count = 0;
			GridTerminalSystem.GetBlocksOfType(Bulkheads);
			for (int i = 0; i < Bulkheads.Count; i++)
			{
				if (Bulkheads[i].CustomName.Contains(BULK_TAG))
				{
					if (Bulkheads[i].CustomName.Contains("Level 1"))
					{ Level_1.Add(Bulkheads[i]); if (Levels_Count < 1) Levels_Count = 1; }
					else if (Bulkheads[i].CustomName.Contains("Level 2"))
					{ Level_2.Add(Bulkheads[i]); if (Levels_Count < 2) Levels_Count = 2; }
					else if (Bulkheads[i].CustomName.Contains("Level 3"))
					{ Level_3.Add(Bulkheads[i]); if (Levels_Count < 3) Levels_Count = 3; }
					else if (Bulkheads[i].CustomName.Contains("Level 4"))
					{ Level_4.Add(Bulkheads[i]); if (Levels_Count < 4) Levels_Count = 4; }
					else if (Bulkheads[i].CustomName.Contains("Level 5"))
					{ Level_5.Add(Bulkheads[i]); if (Levels_Count < 5) Levels_Count = 5; }

					else if (Bulkheads[i].CustomName.Contains("Level -1"))
					{ Level_M1.Add(Bulkheads[i]); if (Minus_Levels_Count < 1) Minus_Levels_Count = 1; }
					else if (Bulkheads[i].CustomName.Contains("Level -2"))
					{ Level_M2.Add(Bulkheads[i]); if (Minus_Levels_Count < 2) Minus_Levels_Count = 2; }
					else if (Bulkheads[i].CustomName.Contains("Level -3"))
					{ Level_M3.Add(Bulkheads[i]); if (Minus_Levels_Count < 3) Minus_Levels_Count = 3; }
					else if (Bulkheads[i].CustomName.Contains("Level -4"))
					{ Level_M4.Add(Bulkheads[i]); if (Minus_Levels_Count < 4) Minus_Levels_Count = 4; }
					else if (Bulkheads[i].CustomName.Contains("Level -5"))
					{ Level_M5.Add(Bulkheads[i]); if (Minus_Levels_Count < 5) Minus_Levels_Count = 5; }
					Bulkheads_Count++;
				}
			}

			// Multipliers.
			float DIVE_MULTIPLIER = 1.0f;
			double Reversed_multiplier = 1 / Speed_multiplier;
			Math.Round(Reversed_multiplier, 2);

			// Argument parsing.
			int n;
			bool isNumeric = int.TryParse(argument, out n);
			if (isNumeric == true)
			{
				Required_Depth = n;
				dive_status = "Maintaining";

				ini.Set("Settings", "Required Depth", Required_Depth);
				ini.Set("Settings", "Dive Status", dive_status);
				Me.CustomData = ini.ToString();
			}
			// Other Arguments.
			else if (argument.ToLower() == "calibrate")
            {
				CalibrateDepth();
            }
			else if (argument.ToLower() == "ed")
			{
				dive_status = "Emergency Diving";
				ini.Set("Settings", "Dive Status", dive_status);
				Me.CustomData = ini.ToString();
			}
			else if (argument.ToLower() == "es")
			{
				dive_status = "Emergency Surfacing";
				ini.Set("Settings", "Dive Status", dive_status);
				Me.CustomData = ini.ToString();
			}
			else if (argument.ToLower() == "maintain")
			{
				dive_status = "Maintaining";
				ini.Set("Settings", "Dive Status", dive_status);
				Me.CustomData = ini.ToString();
			}

			// Maintaining "thing"
			if (dive_status == "Maintaining" && Required_Depth != -1000 && RC != null && Sealevel_Calibrate != 0.666)
			{
				WriteText("Maintaining: " + Required_Depth + " m");
				
				// Are we higher or lower?
				if (Required_Depth > Depth) //If higher.
                {
					//Bulkheads managing.
					if (Minus_Levels_Count > 0) for (int i = 0; i < Level_M1.Count; i++) Level_M1[i].OpenDoor();
					if (Minus_Levels_Count > 1) for (int i = 0; i < Level_M2.Count; i++) Level_M2[i].OpenDoor();
					if (Minus_Levels_Count > 2) for (int i = 0; i < Level_M3.Count; i++) Level_M3[i].OpenDoor();
					if (Minus_Levels_Count > 3) for (int i = 0; i < Level_M4.Count; i++) Level_M4[i].OpenDoor();
					if (Minus_Levels_Count > 4) for (int i = 0; i < Level_M5.Count; i++) Level_M5[i].OpenDoor();
					
					if (Required_Depth - Depth >= 20 * Reversed_multiplier)
					{
						if (Levels_Count >= 1)
							for (int i = 0; i < Level_1.Count; i++) Level_1[i].OpenDoor();
						if (Required_Depth - Depth >= 50 * Reversed_multiplier)
						{
							if (Levels_Count >= 2)
								for (int i = 0; i < Level_2.Count; i++) Level_2[i].OpenDoor();
							if (Required_Depth - Depth >= 100 * Reversed_multiplier)
							{
								if (Levels_Count >= 3)
									for (int i = 0; i < Level_3.Count; i++) Level_3[i].OpenDoor();
								if (Required_Depth - Depth >= 150 * Reversed_multiplier)
								{
									if(Levels_Count >= 4)
										for (int i = 0; i < Level_4.Count; i++) Level_4[i].OpenDoor();
									if (Required_Depth - Depth >= 200 * Reversed_multiplier)
									{
										if (Levels_Count >= 5)
											for (int i = 0; i < Level_5.Count; i++) Level_5[i].OpenDoor();
									}
									else if (Levels_Count >= 5)
										for (int i = 0; i < Level_5.Count; i++) Level_5[i].CloseDoor();
								}
								else if (Levels_Count >= 4)
									for (int i = 0; i < Level_4.Count; i++) Level_4[i].CloseDoor();
							}
							else if (Levels_Count >= 3)
								for (int i = 0; i < Level_3.Count; i++) Level_3[i].CloseDoor();
						}
						else if (Levels_Count >= 2)
							for (int i = 0; i < Level_2.Count; i++) Level_2[i].CloseDoor();
					}
					else
					{
						if (Levels_Count >= 1)
							for (int i = 0; i < Level_1.Count; i++) Level_1[i].CloseDoor();
						// Ice multiplying.
						if (Required_Depth - Depth >= 10 * Reversed_multiplier)
						{
							ballast *= 1.05; DIVE_MULTIPLIER = 0.95f;
						}
						else if (Required_Depth - Depth >= ((5 * Reversed_multiplier) + 1.5))
						{ ballast *= 1.025; DIVE_MULTIPLIER = 0.975f; }
					}

				}
				// If lower.
                else
                {
					//Bulkheads managing.
					if (Levels_Count > 0) for (int i = 0; i < Level_1.Count; i++) Level_1[i].CloseDoor();
					if (Levels_Count > 1) for (int i = 0; i < Level_2.Count; i++) Level_2[i].CloseDoor();
					if (Levels_Count > 2) for (int i = 0; i < Level_3.Count; i++) Level_3[i].CloseDoor();
					if (Levels_Count > 3) for (int i = 0; i < Level_4.Count; i++) Level_4[i].CloseDoor();
					if (Levels_Count > 4) for (int i = 0; i < Level_5.Count; i++) Level_5[i].CloseDoor();

					if (Depth - Required_Depth >= 20 * Reversed_multiplier)
					{
						if (Minus_Levels_Count >= 1)
							for (int i = 0; i < Level_M1.Count; i++) Level_M1[i].CloseDoor();
						if (Depth - Required_Depth >= 50 * Reversed_multiplier)
						{
							if (Minus_Levels_Count >= 2)
								for (int i = 0; i < Level_M2.Count; i++) Level_M2[i].CloseDoor();
							if (Depth - Required_Depth >= 100 * Reversed_multiplier)
							{
								if (Minus_Levels_Count >= 3)
									for (int i = 0; i < Level_M3.Count; i++) Level_M3[i].CloseDoor();
								if (Depth - Required_Depth >= 150 * Reversed_multiplier)
								{
									if (Minus_Levels_Count >= 4)
										for (int i = 0; i < Level_M4.Count; i++) Level_M4[i].CloseDoor();
									if (Depth - Required_Depth >= 200 * Reversed_multiplier)
									{
										if (Minus_Levels_Count >= 5)
											for (int i = 0; i < Level_M5.Count; i++) Level_M5[i].CloseDoor();
									}
									else if (Minus_Levels_Count >= 5)
										for (int i = 0; i < Level_M5.Count; i++) Level_M5[i].OpenDoor();
								}
								else if (Minus_Levels_Count >= 4)
									for (int i = 0; i < Level_M4.Count; i++) Level_M4[i].OpenDoor();
							}
							else if (Minus_Levels_Count >= 3)
								for (int i = 0; i < Level_M3.Count; i++) Level_M3[i].OpenDoor();
						}
						else if (Minus_Levels_Count >= 2)
							for (int i = 0; i < Level_M2.Count; i++) Level_M2[i].OpenDoor();
					}
					else
					{
						if (Minus_Levels_Count >= 1)
							for (int i = 0; i < Level_M1.Count; i++) Level_M1[i].OpenDoor();
						// Ice multiplying.
						if (Depth - Required_Depth >= 10 * Reversed_multiplier)
						{
							ballast *= 0.95; DIVE_MULTIPLIER = 1.05f;
						}
						else if (Depth - Required_Depth >= ((5 * Reversed_multiplier) + 1.5))
						{ ballast *= 0.975; DIVE_MULTIPLIER = 1.025f; }
					}
				}
			}

			//Emergency Statuses

			string Emergency = "None";
			if (dive_status == "Emergency Diving")
			{
				Emergency = "On";
				ballast = max_vol * 2826;
			}
			else if (dive_status == "Emergency Surfacing")
			{
				Emergency = "Off";
				ballast = dry_cargo_mass + MIN_ICE;
			}
			if (Emergency != "None")
			{
				if (Levels_Count > 0) for (int i = 0; i < Level_1.Count; i++) Level_1[i].ApplyAction("Open_" + Emergency);
				if (Levels_Count > 1) for (int i = 0; i < Level_2.Count; i++) Level_2[i].ApplyAction("Open_" + Emergency);
				if (Levels_Count > 2) for (int i = 0; i < Level_3.Count; i++) Level_3[i].ApplyAction("Open_" + Emergency);
				if (Levels_Count > 3) for (int i = 0; i < Level_4.Count; i++) Level_4[i].ApplyAction("Open_" + Emergency);
				if (Levels_Count > 4) for (int i = 0; i < Level_5.Count; i++) Level_5[i].ApplyAction("Open_" + Emergency);
				if (Minus_Levels_Count > 0) for (int i = 0; i < Level_M1.Count; i++) Level_M1[i].ApplyAction("Open_" + Emergency);
				if (Minus_Levels_Count > 1) for (int i = 0; i < Level_M2.Count; i++) Level_M2[i].ApplyAction("Open_" + Emergency);
				if (Minus_Levels_Count > 2) for (int i = 0; i < Level_M3.Count; i++) Level_M3[i].ApplyAction("Open_" + Emergency);
				if (Minus_Levels_Count > 3) for (int i = 0; i < Level_M4.Count; i++) Level_M4[i].ApplyAction("Open_" + Emergency);
				if (Minus_Levels_Count > 4) for (int i = 0; i < Level_M5.Count; i++) Level_M5[i].ApplyAction("Open_" + Emergency);
			}

			// Now we get our collectors and connectors (and ejectors)
			GridTerminalSystem.GetBlocksOfType(collectors);
			GridTerminalSystem.GetBlocksOfType(connectors);

			//And check them for our tags.
			for (int i = 0; i < collectors.Count; i++)
			{
				if (collectors[i].CustomName.Contains(PUMP_TAG))
					pumps.Add(collectors[i]);
			}
			for (int i = 0; i < connectors.Count; i++)
			{
				if (connectors[i].CustomName.Contains(DRAIN_TAG))
					drains.Add(connectors[i]);
			}

			// Now check if we want to turn our pumps and/or drains on.
			pump_status = "Off";
			drain_status = "Off";
			precision_P = false;
			precision_D = false;
			if ((cargo_mass < ballast) && (cur_vol < max_vol))
			{
				if (cargo_mass < ballast - MARGIN) pump_status = "On";
				else { pump_status = "Off"; precision_P = true; }
			}
			else if (cargo_mass > ballast + MARGIN)
			{
				if(cargo_mass > ballast + 14900 * drains.Count) drain_status = "On";
				else { drain_status = "Off"; precision_D = true; }
			}

			// Set our pumps and drains.
			for (int i = 0; i < pumps.Count; i++)
			{
				pumps[i].ApplyAction("OnOff_" + pump_status);
			}

			//Only one will be activated.
			if (precision_P == true) pumps.First().ApplyAction("OnOff_On");

			for (int i = 0; i < drains.Count; i++)
			{
				//Only one will be activated.
				if (precision_D == true && i == 0)
					drains[i].ApplyAction("OnOff_On");
				else drains[i].ApplyAction("OnOff_" + drain_status);
				// And make sure they are set to collect all and throw out.
				drains[i].ThrowOut = true;
				drains[i].CollectAll = true;
			}

			// And write some text.
			WriteText("Depth: " + Math.Round(Depth, 2) + " m");
			WriteText("Buyoancy Status: " + dive_status);
			WriteText("Current Cargo Mass: " + dry_cargo_mass + " kg");
			WriteText("Required Ballast Force: " + Math.Round((ship_mass+cargo_mass)*gravity) + " N / " + (int)(Ship_Buoyancy*DIVE_MULTIPLIER) + " N");
			WriteText("Ballast: " + (Int32)ice + " kg / " + Math.Round(ballast - dry_cargo_mass) + " kg");

			WriteText("\nPump Status: " + pump_status);
			WriteText("Drain Status: " + drain_status);
			WriteText("Pump Count: " + pumps.Count);
			WriteText("Drain Count: " + drains.Count);
			WriteText("Bulkheads Count: " + Bulkheads_Count);

			// Exceptions
			if (RC_Check == false)
				WriteText("Remote Control with [SUBMARINE] tag not found. Depth will not work.");
			if (DS_Check == false)
				WriteText("LCD with [Buoyancy Control LCD] tag not found. Optional.");
			// Write it to the screen.
			screen.WriteText(screenText);
			if (DS_Check == true) display.WriteText(screenText);
		}

		//Saving data
		public void Save()
		{
			if (!ini.TryParse(Me.CustomData))
			{
				Me.CustomData = "";
				throw new Exception("Initialisation failed. CustomData has been cleared. Recompile the script.");
			}
			PUMP_TAG = ini.Get("Settings", "Collectors Name").ToString(PUMP_TAG);
			DRAIN_TAG = ini.Get("Settings", "Connectors Name").ToString(DRAIN_TAG);
			RC_TAG = ini.Get("Settings", "Remote Control Name").ToString(RC_TAG);
			LCD_TAG = ini.Get("Settings", "LCD Name").ToString(LCD_TAG);
			BULK_TAG = ini.Get("Settings", "Bulkheads Name").ToString(BULK_TAG);
			MARGIN = ini.Get("Settings", "Maximum Ballast Deviation").ToInt32(MARGIN);
			MIN_ICE = ini.Get("Settings", "Minimum Ice Stored").ToInt32(MIN_ICE);
			Ship_Buoyancy = ini.Get("Settings", "Ship Buoyancy").ToInt32(Ship_Buoyancy);
			dive_status = ini.Get("Settings", "Dive Status").ToString(dive_status);
			Speed_multiplier = ini.Get("Settings", "Speed Multiplier").ToDouble(Speed_multiplier);
			Required_Depth = ini.Get("Settings", "Required Depth").ToInt32(Required_Depth);
			Sealevel_Calibrate = ini.Get("Settings", "Depth Calibration").ToDouble(Sealevel_Calibrate);

			ini.Set("Settings", "Collectors Name", PUMP_TAG);
			ini.Set("Settings", "Connectors Name", DRAIN_TAG);
			ini.Set("Settings", "Remote Control Name", RC_TAG);
			ini.Set("Settings", "LCD Name", LCD_TAG);
			ini.Set("Settings", "Bulkheads Name", BULK_TAG);
			ini.Set("Settings", "Maximum Ballast Deviation", MARGIN);
			ini.Set("Settings", "Minimum Ice Stored", MIN_ICE);
			ini.Set("Settings", "Ship Buoyancy", Ship_Buoyancy);
			ini.Set("Settings", "Dive Status", dive_status);
			ini.Set("Settings", "Speed Multiplier", Speed_multiplier);
			ini.Set("Settings", "Required Depth", Required_Depth);
			ini.Set("Settings", "Depth Calibration", Sealevel_Calibrate);

			Me.CustomData = ini.ToString();
			//YEEEEES WE ARE F*CKIN' DOOOOONE
		}

		//End copying the code into the program block before this line.
	}
}
