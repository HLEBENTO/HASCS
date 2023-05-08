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


namespace BuoyancyControl
{
    partial class Program : MyGridProgram
    {


		// HELLBENT's Automatic Submarine Control Script [For Water Mod]
		// Version: 1.1 (05.05.2023 23:25)
		//
		// Ver 1.0 - Main script (04.01.2023); Ver 1.1 - Minor updates, Code refactor, Bug fixes, Interface upgraded (05.05.2023);
		//
		// You can find the guide on YouTube and the manual in Steam.
		// YouTube: https://youtu.be/pe3mDV7VbaU
		// Steam: (Coming Soon)
		// And link to script itself: https://steamcommunity.com/sharedfiles/filedetails/?id=2921178770

		// === DON'T MODIFY SCRIPT. USE CUSTOMDATA OF THE PB!!! ===

		// === DON'T MODIFY SCRIPT. USE CUSTOMDATA OF THE PB!!! ===

		// === DON'T MODIFY SCRIPT. USE CUSTOMDATA OF THE PB!!! ===


		//=============================================================
		//Start copying the code into the program block after this line:
		//=============================================================

		#region Global_Variables

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

		int Ship_Buoyancy = 300000000;
		double Speed_multiplier = 5;

		const double updatesPerSecond = 10;
		const double timeLimit = 1 / updatesPerSecond;
		double timeElapsed = 0;

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
		List<IMyDoor> Match_Bulkheads;
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

		IMyRemoteControl RC;
		IMyTextSurface screen;
		string screenText;
		string pump_status;
		string drain_status;
		float gravity;
		string dive_status = "Maintaining";
		float Depth = 0;
		int Required_Depth = -1000;
		double ballast;
		float ship_mass;
		float cargo_mass = 0;
		float dry_cargo_mass = 0;
		float ice = 0;
		int max_vol = 0;
		float current_gas_force = 0;
		int cur_vol = 0;
		double Sealevel_Calibrate = 0.666;
		float Screen_Buoyancy_Multiplier = 1.0f;
		int Levels_Count = 0;
		int Minus_Levels_Count = 0;
		bool ShouldWork = true;

		#endregion
		#region Program() (Init)

		// Initialization
		public Program()
		{
			Runtime.UpdateFrequency = UpdateFrequency.Update1;

			ParseIni();

			//Set up PB's LCD
			screen = Me.GetSurface(0);
			screen.ContentType = ContentType.TEXT_AND_IMAGE;
			screen.FontSize = 0.700f;

			try
			{
				controllers = new List<IMyShipController>();
				GridTerminalSystem.GetBlocksOfType(controllers);
				RC = (IMyRemoteControl)controllers.First(x => x is IMyRemoteControl && x.CustomName.Contains(RC_TAG));
			}
			catch { };
		}

		#endregion
		#region Ini And Save()

		public void ParseIni()
		{
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
			ShouldWork = ini.Get("Settings", "Is The Script Running").ToBoolean(ShouldWork);

		}

		//Saving data
		public void Save()
		{
			ParseIni();

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
			ini.Set("Settings", "Is The Script Running", ShouldWork);

			Me.CustomData = ini.ToString();
		}

		#endregion
		#region Display
		public void ShowStatus(bool isWorking)
		{
			screenText = "";
			// And write some text.
			if (dive_status == "Maintaining" && Required_Depth != -1000 && RC != null && Sealevel_Calibrate != 0.666 && isWorking)
			{
				WriteText("Maintaining: " + (Required_Depth) + " m");
			}
			else WriteText("Maintaining: Neutral");

			if (RC != null) WriteText("Depth: " + Math.Round(Depth, 2) + " m");
			else WriteText("Depth: Disabled");

			if (dive_status == "Maintaining" && Required_Depth != -1000 && isWorking)
			{
				if (Required_Depth - Depth > 2.5) WriteText("Buoyancy Status: Diving");
				else if (Depth - Required_Depth > 2.5) WriteText("Buoyancy Status: Surfacing");
				else WriteText("Buoyancy Status: " + dive_status);
			}
			else WriteText("Buoyancy Status: " + dive_status);

			if (isWorking) WriteText("\n" + DoorStatusString()); else WriteText("\nDiving < --- --- --- --- --- |OFF| --- --- --- --- --- > Surfacing");

			if (isWorking && RC != null)
			{
				WriteText("\nCurrent Cargo Mass: " + dry_cargo_mass + " kg");
				WriteText("Required Ballast Force: " + Math.Round((ship_mass + cargo_mass) * gravity) + " N / " + (int)(Ship_Buoyancy * Screen_Buoyancy_Multiplier) + " N");
				WriteText("Ballast: " + (Int32)ice + " kg / " + Math.Round(ballast - dry_cargo_mass) + " kg");
				WriteText("Pump Status: " + pump_status + " || Drain Status: " + drain_status + "\n");
			}
			else WriteText("\nCurrent Cargo Mass: Disabled \nRequired Ballast Force: Still Disabled \nBallast: And This Disabled Too! \nPump Status: What are you want from disabled script?\n");

			// Possible errors
			if (isWorking && Match_Bulkheads.Count != 0)
			{
				foreach (var bulk in Match_Bulkheads)
				{
					if (!bulk.IsFunctional) WriteText("Attention:  " + bulk.CustomName + " is damaged!");
				}
			}

			if (RC == null) WriteText("Error:  Remote Control with " + RC_TAG + " tag not found. \nScript will not work.");

			var screens = new List<IMyTerminalBlock>();
			GridTerminalSystem.SearchBlocksOfName(LCD_TAG, screens, block => block is IMyTextPanel);

			if (screens.Count == 0)
			{
				WriteText("Attention:  " + LCD_TAG + " not found. Optional.");
				screen.WriteText(screenText);
				return;
			}
			else screen.WriteText(screenText);

			foreach (IMyTextPanel thisScreen in screens)
			{
				thisScreen.ContentType = ContentType.TEXT_AND_IMAGE;
				thisScreen.WriteText(screenText);
			}

		}

		public string DoorStatusString()
		{
			string DoorsStatus = "Diving < ";
			if (Levels_Count > 4) { if (Level_5.First().Status.ToString() == "Open") DoorsStatus += "[<] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			if (Levels_Count > 3) { if (Level_4.First().Status.ToString() == "Open") DoorsStatus += "[<] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			if (Levels_Count > 2) { if (Level_3.First().Status.ToString() == "Open") DoorsStatus += "[<] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			if (Levels_Count > 1) { if (Level_2.First().Status.ToString() == "Open") DoorsStatus += "[<] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			if (Levels_Count > 0) { if (Level_1.First().Status.ToString() == "Open") DoorsStatus += "[<] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			DoorsStatus += "|=| ";
			if (Minus_Levels_Count > 0) { if (Level_M1.First().Status.ToString() == "Closed") DoorsStatus += "[>] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			if (Minus_Levels_Count > 1) { if (Level_M2.First().Status.ToString() == "Closed") DoorsStatus += "[>] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			if (Minus_Levels_Count > 2) { if (Level_M3.First().Status.ToString() == "Closed") DoorsStatus += "[>] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			if (Minus_Levels_Count > 3) { if (Level_M4.First().Status.ToString() == "Closed") DoorsStatus += "[>] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			if (Minus_Levels_Count > 4) { if (Level_M5.First().Status.ToString() == "Closed") DoorsStatus += "[>] "; else DoorsStatus += "--- "; } else DoorsStatus += "--- ";
			DoorsStatus += "> Surfacing";
			return DoorsStatus;
		}
		// We'll need this to write to our screen.
		public void WriteText(string x)
		{
			Echo(x);
			screenText += x + "\n";
		}

		#endregion
		#region Depth Metods

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

		public void GetDepth()
		{
			// Depth?
			if (RC != null)
			{
				double Pre_Depth = 0;
				RC.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out Pre_Depth);
				Depth = (float)(Sealevel_Calibrate - Pre_Depth);
			}
		}

		#endregion
		#region Ballast Methods
		public void GetShipMass()
		{
			ship_mass = 0;
			if (RC != null)
			{
				// Gravity and Mass.
				gravity = (float)(RC.GetNaturalGravity().Length());
				ship_mass = RC.CalculateShipMass().BaseMass;
				// Multiply it by the local gravity coefficient.
				ship_mass *= (float)(gravity / 9.81);
			}
		}

		public void GetTanksBuoynantForce()
		{
			// And our tank information.
			current_gas_force = 0;
			tanks = new List<IMyGasTank>();
			GridTerminalSystem.GetBlocksOfType(tanks);
			for (int i = 0; i < tanks.Count; i++)
			{
				int Tank_Force = 0;

				if (tanks[i].BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/" || tanks[i].BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/LargeHydrogenTankSmall")
				{ Tank_Force = SLT_Force; }
				if (tanks[i].BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/SmallHydrogenTank")
				{ Tank_Force = LST_Force; }
				if (tanks[i].BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/LargeHydrogenTankIndustrial" || tanks[i].BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/LargeHydrogenTank")
				{ Tank_Force = LLT_Force; }
				if (tanks[i].BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/OxygenTankSmall" || tanks[i].BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/SmallHydrogenTankMedium")
				{ Tank_Force = MST_Force; }
				if (tanks[i].BlockDefinition.ToString() == "MyObjectBuilder_OxygenTank/SmallHydrogenTankSmall")
				{ Tank_Force = SST_Force; }

				float fill = (float)tanks[i].FilledRatio;
				float empty = 1 - fill;
				current_gas_force += Tank_Force * empty;
			}
		}

		public void GetCargoMass()
		{
			// Get our cargo information.
			cargo = new List<IMyTerminalBlock>();
			GridTerminalSystem.GetBlocksOfType(cargo);
			MyInventoryItem item;
			ice = 0;
			cargo_mass = 0;
			max_vol = 0;
			cur_vol = 0;
			dry_cargo_mass = 0;
			foreach (var Block in cargo)
			{
				if (Block.HasInventory)
				{
					int inventory_count = Block.InventoryCount;
					for (int current_inventory = 0; current_inventory < inventory_count; current_inventory++)
					{
						cargo_mass += Block.GetInventory(current_inventory).CurrentMass.ToIntSafe();
						max_vol += Block.GetInventory(current_inventory).MaxVolume.ToIntSafe();
						cur_vol += Block.GetInventory(current_inventory).CurrentVolume.ToIntSafe();

						for (int j = 0; j < Block.GetInventory(current_inventory).ItemCount; j++)
						{
							item = Block.GetInventory(current_inventory).GetItemAt(j).Value;

							if (item.ToString().Contains("MyObjectBuilder_Ore/Ice"))
							{
								ice += (int)item.Amount;
							}
						}
					}
				}
			}
			dry_cargo_mass = cargo_mass - ice;
		}

		public void CalculateNeededBallast()
		{

			// Figure out our neutral buoyancy.
			double neutral_force = ((Ship_Buoyancy + current_gas_force) - ((ship_mass + dry_cargo_mass) * gravity)) + BUOYANCY_ADJUST;
			ballast = (neutral_force / gravity);
		}
		#endregion
		#region Bulkheads Methods

		public void GetBulkheads()
		{
			//Bulkheads
			Levels_Count = 0;
			Minus_Levels_Count = 0;
			Bulkheads = new List<IMyDoor>();
			Match_Bulkheads = new List<IMyDoor>();
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
			GridTerminalSystem.GetBlocksOfType(Bulkheads);
			foreach (var bulk in Bulkheads)
			{
				if (bulk.CustomName.Contains(BULK_TAG))
				{
					Match_Bulkheads.Add(bulk);

					if (bulk.CustomName.Contains("Level 1")) { Level_1.Add(bulk); if (Levels_Count < 1) Levels_Count = 1; }
					else if (bulk.CustomName.Contains("Level 2")) { Level_2.Add(bulk); if (Levels_Count < 2) Levels_Count = 2; }
					else if (bulk.CustomName.Contains("Level 3")) { Level_3.Add(bulk); if (Levels_Count < 3) Levels_Count = 3; }
					else if (bulk.CustomName.Contains("Level 4")) { Level_4.Add(bulk); if (Levels_Count < 4) Levels_Count = 4; }
					else if (bulk.CustomName.Contains("Level 5")) { Level_5.Add(bulk); if (Levels_Count < 5) Levels_Count = 5; }

					else if (bulk.CustomName.Contains("Level -1")) { Level_M1.Add(bulk); if (Minus_Levels_Count < 1) Minus_Levels_Count = 1; }
					else if (bulk.CustomName.Contains("Level -2")) { Level_M2.Add(bulk); if (Minus_Levels_Count < 2) Minus_Levels_Count = 2; }
					else if (bulk.CustomName.Contains("Level -3")) { Level_M3.Add(bulk); if (Minus_Levels_Count < 3) Minus_Levels_Count = 3; }
					else if (bulk.CustomName.Contains("Level -4")) { Level_M4.Add(bulk); if (Minus_Levels_Count < 4) Minus_Levels_Count = 4; }
					else if (bulk.CustomName.Contains("Level -5")) { Level_M5.Add(bulk); if (Minus_Levels_Count < 5) Minus_Levels_Count = 5; }
				}
			}
		}

		public void BulkheadsSwitch()
		{

			// Multipliers.

			double Reversed_multiplier = 1 / Speed_multiplier;
			if (Reversed_multiplier < 0.1) Reversed_multiplier = 0.1;
			Math.Round(Reversed_multiplier, 2);

			// Maintaining "thing"
			if (dive_status == "Maintaining" && Required_Depth != -1000 && RC != null && Sealevel_Calibrate != 0.666)
			{

				// Are we higher or lower?
				if (Required_Depth > Depth) //If higher.
				{
					//Bulkheads managing.
					if (Minus_Levels_Count > 0) foreach (var bulk in Level_M1) bulk.OpenDoor();
					if (Minus_Levels_Count > 1) foreach (var bulk in Level_M2) bulk.OpenDoor();
					if (Minus_Levels_Count > 2) foreach (var bulk in Level_M3) bulk.OpenDoor();
					if (Minus_Levels_Count > 3) foreach (var bulk in Level_M4) bulk.OpenDoor();
					if (Minus_Levels_Count > 4) foreach (var bulk in Level_M5) bulk.OpenDoor();

					if (Required_Depth - Depth >= 20 * Reversed_multiplier && Levels_Count >= 1)
					{
						foreach (var bulk in Level_1) bulk.OpenDoor();

						if (Required_Depth - Depth >= 50 * Reversed_multiplier && Levels_Count >= 2)
						{
							foreach (var bulk in Level_2) bulk.OpenDoor();

							if (Required_Depth - Depth >= 100 * Reversed_multiplier && Levels_Count >= 3)
							{
								foreach (var bulk in Level_3) bulk.OpenDoor();

								if (Required_Depth - Depth >= 150 * Reversed_multiplier && Levels_Count >= 4)
								{
									foreach (var bulk in Level_4) bulk.OpenDoor();

									if (Required_Depth - Depth >= 200 * Reversed_multiplier && Levels_Count >= 5)
									{
										foreach (var bulk in Level_5) bulk.OpenDoor();
									}
									else if (Levels_Count >= 5) foreach (var bulk in Level_5) bulk.CloseDoor();
								}
								else if (Levels_Count >= 4) foreach (var bulk in Level_4) bulk.CloseDoor();
							}
							else if (Levels_Count >= 3) foreach (var bulk in Level_3) bulk.CloseDoor();
						}
						else if (Levels_Count >= 2) foreach (var bulk in Level_2) bulk.CloseDoor();
					}
					else
					{
						if (Levels_Count >= 1) foreach (var bulk in Level_1) bulk.CloseDoor();
						// Ice multiplying.
						if (Required_Depth - Depth >= 10 * Reversed_multiplier)
						{
							ballast *= 1.02; Screen_Buoyancy_Multiplier = 0.98f;
						}
						else if (Required_Depth - Depth >= 5 * Reversed_multiplier)
						{ ballast *= 1.01; Screen_Buoyancy_Multiplier = 0.99f; }
					}

				}
				// If lower.
				else
				{
					//Bulkheads managing.
					if (Levels_Count > 0) foreach (var bulk in Level_1) bulk.CloseDoor();
					if (Levels_Count > 1) foreach (var bulk in Level_2) bulk.CloseDoor();
					if (Levels_Count > 2) foreach (var bulk in Level_3) bulk.CloseDoor();
					if (Levels_Count > 3) foreach (var bulk in Level_4) bulk.CloseDoor();
					if (Levels_Count > 4) foreach (var bulk in Level_5) bulk.CloseDoor();

					if (Depth - Required_Depth >= 20 * Reversed_multiplier && Minus_Levels_Count >= 1)
					{
						foreach (var bulk in Level_M1) bulk.CloseDoor();

						if (Depth - Required_Depth >= 50 * Reversed_multiplier && Minus_Levels_Count >= 2)
						{
							foreach (var bulk in Level_M2) bulk.CloseDoor();

							if (Depth - Required_Depth >= 100 * Reversed_multiplier && Minus_Levels_Count >= 3)
							{
								foreach (var bulk in Level_M3) bulk.CloseDoor();

								if (Depth - Required_Depth >= 150 * Reversed_multiplier && Minus_Levels_Count >= 4)
								{
									foreach (var bulk in Level_M4) bulk.CloseDoor();

									if (Depth - Required_Depth >= 200 * Reversed_multiplier && Minus_Levels_Count >= 5)
									{
										foreach (var bulk in Level_M5) bulk.CloseDoor();
									}
									else if (Minus_Levels_Count >= 5) foreach (var bulk in Level_M5) bulk.OpenDoor();
								}
								else if (Minus_Levels_Count >= 4) foreach (var bulk in Level_M4) bulk.OpenDoor();
							}
							else if (Minus_Levels_Count >= 3) foreach (var bulk in Level_M3) bulk.OpenDoor();
						}
						else if (Minus_Levels_Count >= 2) foreach (var bulk in Level_M2) bulk.OpenDoor();
					}
					else
					{
						if (Minus_Levels_Count >= 1) foreach (var bulk in Level_M1) bulk.OpenDoor();
						// Ice multiplying.
						if (Depth - Required_Depth >= 10 * Reversed_multiplier)
						{
							ballast *= 0.98; Screen_Buoyancy_Multiplier = 1.02f;
						}
						else if (Depth - Required_Depth >= 5 * Reversed_multiplier)
						{ ballast *= 0.99; Screen_Buoyancy_Multiplier = 1.01f; }
					}
				}
			}
		}

		#endregion
		#region Pumps And Drains
		public void GetIcePumpsAndDrains()
		{

			// Now we get our collectors and connectors (and ejectors)
			collectors = new List<IMyCollector>();
			pumps = new List<IMyCollector>();
			connectors = new List<IMyShipConnector>();
			drains = new List<IMyShipConnector>();
			GridTerminalSystem.GetBlocksOfType(collectors);
			GridTerminalSystem.GetBlocksOfType(connectors);

			//And check them for our tags.
			foreach (var collector in collectors)
			{
				if (collector.CustomName.Contains(PUMP_TAG))
					pumps.Add(collector);
			}
			foreach (var connector in connectors)
			{
				if (connector.CustomName.Contains(DRAIN_TAG))
					drains.Add(connector);
			}
		}

		public void PumpsAndDrainsSwitch()
		{
			// Now check if we want to turn our pumps and/or drains on.
			pump_status = "Off";
			drain_status = "Off";
			bool precision_P = false;
			bool precision_D = false;

			//Need to gain?
			if ((cargo_mass < ballast) && (cur_vol < max_vol))
			{
				if (cargo_mass < ballast - MARGIN) pump_status = "On";
				else { pump_status = "Off"; precision_P = true; }
			}

			//Or need to throw out?
			else if (cargo_mass > ballast + MARGIN)
			{
				if (cargo_mass > ballast + 14900 * drains.Count) drain_status = "On";
				else { drain_status = "Off"; precision_D = true; }
			}

			// Set pumps.
			foreach (var pump in pumps)
			{
				pump.ApplyAction("OnOff_" + pump_status);
			}
			if (precision_P == true) pumps.First().ApplyAction("OnOff_On"); //Only one will be activated.

			//Set drains.
			foreach (var drain in drains)
			{
				drain.ApplyAction("OnOff_" + drain_status);
				// And make sure they are set to collect all and throw out.
				drain.ThrowOut = true;
				drain.CollectAll = true;
			}
			if (precision_D == true) drains.First().ApplyAction("OnOff_On"); //Only one will be activated.
		}

		#endregion
		#region Emergency Status

		//Emergency Statuses
		public bool CheckEmergency()
		{
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
				if (Match_Bulkheads.Count != 0)
				{
					foreach (var bulk in Match_Bulkheads) bulk.ApplyAction("Open_" + Emergency);
				}
				return true;
			}
			return false;
		}

		#endregion
		#region Main() Method

		public void Main(string argument, UpdateType updateSource)
		{
			timeElapsed += Runtime.TimeSinceLastRun.TotalSeconds;

			// Argument parsing for depth requirements.
			int n = 0;
			bool isNumeric = int.TryParse(argument, out n);
			if (isNumeric == true)
			{
				Required_Depth = n;
				dive_status = "Maintaining";

				ini.Set("Settings", "Required Depth", Required_Depth);
				ini.Set("Settings", "Dive Status", dive_status);
				Me.CustomData = ini.ToString();
			}
			// Other string arguments
			else
			{
				switch (argument.ToLower())
				{
					//Should script do it's job?
					case "toggle":
						if (!ShouldWork)
						{
							ShouldWork = true;
							dive_status = "Maintaining";
							ini.Set("Settings", "Is The Script Running", ShouldWork);
							Me.CustomData = ini.ToString();
						}
						else
						{
							ShouldWork = false;
							dive_status = "!! Disabled !!";
							ini.Set("Settings", "Is The Script Running", ShouldWork);
							Me.CustomData = ini.ToString();
						}
						break;

					case "on":
						ShouldWork = true;
						dive_status = "Maintaining";
						ini.Set("Settings", "Is The Script Running", ShouldWork);
						Me.CustomData = ini.ToString();
						break;

					case "off":
						ShouldWork = false;
						dive_status = "!! Disabled !!";
						ini.Set("Settings", "Is The Script Running", ShouldWork);
						Me.CustomData = ini.ToString();
						break;

					// Other Arguments.
					case "calibrate":
						CalibrateDepth();
						break;

					case "ed":
						dive_status = "Emergency Diving";
						ini.Set("Settings", "Dive Status", dive_status);
						Me.CustomData = ini.ToString();
						break;

					case "es":
						dive_status = "Emergency Surfacing";
						ini.Set("Settings", "Dive Status", dive_status);
						Me.CustomData = ini.ToString();
						break;

					case "maintain":
						dive_status = "Maintaining";

						Required_Depth = -1000;
						ini.Set("Settings", "Required Depth", Required_Depth);
						ini.Set("Settings", "Dive Status", dive_status);
						Me.CustomData = ini.ToString();
						break;

					default:
						break;
				}
			}

			//Should the script run in current iteration?
			if (timeElapsed >= timeLimit)
			{
				GetDepth();

				if (ShouldWork)
				{
					timeElapsed = 0;

					GetShipMass();
					GetCargoMass();
					GetTanksBuoynantForce();

					CalculateNeededBallast();

					GetBulkheads();

					if (CheckEmergency() == false)
					{
						BulkheadsSwitch();
					}

					GetIcePumpsAndDrains();
					PumpsAndDrainsSwitch();

					ShowStatus(ShouldWork);
				}
				else ShowStatus(ShouldWork);
			}

		}
		#endregion

		//============================================================
		//End copying the code into the program block before this line.
		//============================================================
	}
}
