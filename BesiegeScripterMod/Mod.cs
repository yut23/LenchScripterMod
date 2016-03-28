﻿using System;
using System.IO;
using System.Collections.Generic;
using spaar.ModLoader;
using UnityEngine;
using NLua;
using NLua.Exceptions;

namespace BesiegeScripterMod
{
    public class ScripterLoader : Mod
    {
        public override string Name { get; } = "Lench Scripter Mod";
        public override string DisplayName { get; } = "Lench Scripter Mod";
        public override string Author { get; } = "Lench";
        public override Version Version { get; } = new Version(0, 2, 0, 0);
        public override string VersionExtra { get; } = "";
        public override string BesiegeVersion { get; } = "v0.27";
        public override bool CanBeUnloaded { get; } = true;
        public override bool Preload { get; } = false;

        private Scripter scripter;

        public override void OnLoad()
        {
            UnityEngine.Object.DontDestroyOnLoad(Scripter.Instance);
            this.scripter = Scripter.Instance;

            Game.OnSimulationToggle += this.scripter.OnSimulationToggle;
        }
        public override void OnUnload()
        {
            Game.OnSimulationToggle -= this.scripter.OnSimulationToggle;
            this.scripter.isSimulating = false;
            this.scripter.ScriptStop();
        }
    }

    public class Scripter : SingleInstance<Scripter>
    {
        public override string Name { get; } = "LenchScripter";
        public bool isSimulating;

        private Lua lua;
        private string luaFile;
        private GenericBlock hoveredBlock;

        // Map: Block type -> type count
        private Dictionary<string, int> typeCount;

        // Map: Building Block -> ID
        private Dictionary<GenericBlock, string> buildingBlocks;

        // Map: ID -> Simulation Block
        private Dictionary<string, Transform> simulationBlocks;

        // Stopwatch for measuring simulation time.
        System.Diagnostics.Stopwatch stopwatch;

        /// <summary>
        /// Rebuilds ID dictionary.
        /// </summary>
        private void AddBlockID(Transform block)
        {
            InitializeBuildingBlockIDs();
        }

        /// <summary>
        /// Populates dictionary with references to building blocks.
        /// Used for dumping block IDs while building.
        /// </summary>
        public void InitializeBuildingBlockIDs()
        {
            if (typeCount != null) typeCount.Clear();
            else typeCount = new Dictionary<string, int>();
            if (buildingBlocks != null) buildingBlocks.Clear();
            else
            {
                Game.OnBlockPlaced += AddBlockID;
                Game.OnBlockRemoved += InitializeBuildingBlockIDs;
                buildingBlocks = new Dictionary<GenericBlock, string>();
            }
            Transform buildingMachine = GameObject.Find("Building Machine").transform;
            foreach (Transform b in buildingMachine)
            {
                GenericBlock block = b.GetComponent<GenericBlock>();
                string name = block.GetComponent<MyBlockInfo>().blockName.ToUpper();
                typeCount[name] = typeCount.ContainsKey(name) ? typeCount[name] + 1 : 1;
                buildingBlocks[block] = name + " " + typeCount[name];
            }
        }

        /// <summary>
        /// Populates dictionary with references to simulation blocks.
        /// Used for accessing blocks with GetBlock(blockId) while simulating.
        /// Called at first call of GetBlock(blockId);
        /// </summary>
        public void InitializeSimulationBlockIDs()
        {
            /* Populates dictionary with references to simulation blocks.
               Used for accessing blocks with GetBlock(id) function while simulating. */
            simulationBlocks = new Dictionary<string, Transform>();
            Transform simulationMachine = GameObject.Find("Simulation Machine").transform;
            foreach (Transform b in simulationMachine)
            {
                string name = b.GetComponent<MyBlockInfo>().blockName.ToUpper();

                int c = 0;
                string id;
                do
                {
                    c++;
                    id = name + " " + c;
                } while (simulationBlocks.ContainsKey(id));

                simulationBlocks[id] = b;
            }
        }

        /// <summary>
        /// Called to start script.
        /// </summary>
        public void ScriptStart()
        {
            simulationBlocks = null;
            this.lua = new Lua();

            // Populate function table
            this.lua.NewTable("besiege");
            this.lua.RegisterFunction("besiege.log", this, typeof(Scripter).GetMethod("Log"));
            this.lua.RegisterFunction("besiege.getTime", this, typeof(Scripter).GetMethod("GetTime"));

            this.lua.RegisterFunction("besiege.setToggleMode", this, typeof(Scripter).GetMethod("SetToggleMode"));
            this.lua.RegisterFunction("besiege.setSliderValue", this, typeof(Scripter).GetMethod("SetSliderValue"));

            this.lua.RegisterFunction("besiege.getToggleMode", this, typeof(Scripter).GetMethod("GetToggleMode"));
            this.lua.RegisterFunction("besiege.getSliderValue", this, typeof(Scripter).GetMethod("GetSliderValue"));
            this.lua.RegisterFunction("besiege.getSliderMin", this, typeof(Scripter).GetMethod("GetSliderMin"));
            this.lua.RegisterFunction("besiege.getSliderMax", this, typeof(Scripter).GetMethod("GetSliderMax"));

            this.lua.RegisterFunction("besiege.getPosition", this, typeof(Scripter).GetMethod("GetPosition"));
            this.lua.RegisterFunction("besiege.getPositionX", this, typeof(Scripter).GetMethod("GetPositionX"));
            this.lua.RegisterFunction("besiege.getPositionY", this, typeof(Scripter).GetMethod("GetPositionY"));
            this.lua.RegisterFunction("besiege.getPositionZ", this, typeof(Scripter).GetMethod("GetPositionZ"));

            this.lua.RegisterFunction("besiege.getVelocity", this, typeof(Scripter).GetMethod("GetVelocity"));
            this.lua.RegisterFunction("besiege.getVelocityX", this, typeof(Scripter).GetMethod("GetVelocityX"));
            this.lua.RegisterFunction("besiege.getVelocityY", this, typeof(Scripter).GetMethod("GetVelocityY"));
            this.lua.RegisterFunction("besiege.getVelocityZ", this, typeof(Scripter).GetMethod("GetVelocityZ"));

            this.lua.RegisterFunction("besiege.getAngular", this, typeof(Scripter).GetMethod("GetAngular"));
            this.lua.RegisterFunction("besiege.getAngularX", this, typeof(Scripter).GetMethod("GetAngularX"));
            this.lua.RegisterFunction("besiege.getAngularY", this, typeof(Scripter).GetMethod("GetAngularY"));
            this.lua.RegisterFunction("besiege.getAngularZ", this, typeof(Scripter).GetMethod("GetAngularZ"));

            this.lua.RegisterFunction("besiege.getHeading", this, typeof(Scripter).GetMethod("GetHeading"));
            this.lua.RegisterFunction("besiege.getPitch", this, typeof(Scripter).GetMethod("GetPitch"));
            this.lua.RegisterFunction("besiege.getRoll", this, typeof(Scripter).GetMethod("GetRoll"));
            this.lua.RegisterFunction("besiege.getYaw", this, typeof(Scripter).GetMethod("GetYaw"));

            this.lua.RegisterFunction("besiege.getRaycastHit", this, typeof(Scripter).GetMethod("GetRaycastHit"));


            // Populate keycode table
            this.lua.NewTable("besiege.keyCodes");
            foreach (KeyCode value in Enum.GetValues(typeof(KeyCode)))
            {
                string str = value.ToString();
                object[] objArray = new object[] { "besiege.keyCodes[\"", str, "\"] = ", (int)value };
                lua.DoString(string.Concat(objArray), "chunk");
            }

            // Find script file
            string luaFile = string.Concat(Application.dataPath, "/Scripts/", MyTextField.lastNameUsed, ".lua");
            if (File.Exists(luaFile))
            {
                UnityEngine.Debug.Log("Script file: " + luaFile);
                this.luaFile = luaFile;
            }
            else
            {
                Debug.Log("Script file does not exist: " + luaFile);
                this.ScriptStop();
            }

            // Start simulation stopwatch.
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

        }

        /// <summary>
        /// Called to stop script.
        /// </summary>
        public void ScriptStop()
        {
            this.lua.Close();
            this.lua.Dispose();
            this.lua = null;
            stopwatch.Stop();
            Debug.Log("Script stopped");
        }

        /// <summary>
        /// Finds hovered block in buildingBlocks dictionary and dumps its ID string.
        /// </summary>
        private void DumpHoveredBlock()
        {
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                if (Game.AddPiece.HoveredBlock == null)
                {
                    this.hoveredBlock = null;
                    return;
                }

                if (this.hoveredBlock != null && Game.AddPiece.HoveredBlock == this.hoveredBlock)
                    return;

                this.hoveredBlock = Game.AddPiece.HoveredBlock;

                if (buildingBlocks == null)
                    InitializeBuildingBlockIDs();

                Debug.Log(buildingBlocks[hoveredBlock]);
            }
        }

        private void Update()
        {
            if (!this.isSimulating)
            {
                this.DumpHoveredBlock();
                return;
            }

            if (!AddPiece.isSimulating) return;
            if (this.lua == null) return;

            if (this.luaFile != null)
            {   // load the script on the first update
                this.lua.DoFile(this.luaFile);
                this.luaFile = null;
            }

            if ((string)this.lua.DoString("return type(besiege.onUpdate)", "chunk")[0] == "function")
                this.lua.DoString("besiege:onUpdate()", "chunk");

            if (Input.anyKey)
            {
                if ((string)this.lua.DoString("return type(besiege.onKeyHeld)", "chunk")[0] == "function")
                {
                    foreach (KeyCode value in Enum.GetValues(typeof(KeyCode)))
                    {
                        if (!Input.GetKey(value)) continue;
                        this.lua.DoString(string.Concat("besiege:onKeyHeld(", (int)value, ")"), "chunk");
                    }
                }
                if ((string)this.lua.DoString("return type(besiege.onKeyDown)", "chunk")[0] == "function")
                {
                    foreach (KeyCode value in Enum.GetValues(typeof(KeyCode)))
                    {
                        if (!Input.GetKeyDown(value)) continue;
                        this.lua.DoString(string.Concat("besiege:onKeyDown(", (int)value, ")"), "chunk");
                    }
                }
            }
        }

        public void OnSimulationToggle(bool isSimulating)
        {
            this.isSimulating = isSimulating;
            if (isSimulating)
                this.ScriptStart();
            else if (this.lua != null)
                this.ScriptStop();
        }

        /// <summary>
        /// Finds blockId string in dictionary of simulation blocks.
        /// On first call of the simulation, it also initializes the dictionary.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier.</param>
        /// <returns>Returns reference to blocks Transform object.</returns>
        private Transform GetBlock(string blockId)
        {
            /* Returns block reference.
               Initializes block dictionary on the first call. */
            if (simulationBlocks == null) {
                InitializeSimulationBlockIDs();
            }
            return simulationBlocks[blockId.ToUpper()];
        }


        /***************** Lua methods *****************/


        public void Log(string msg)
        {
            Debug.Log(msg);
        }

        public long GetTime()
        {
            return stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Used to toggle various block properties.
        /// Does nothing if the property is not found.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier.</param>
        /// <param name="toggleName">Case insensitive string specifying the property to be set.
        /// Usually identical to in-game label.</param>
        /// <param name="value">Boolean value to be set.</param>
        public void SetToggleMode(string blockId, string toggleName, bool value)
        {
            BlockBehaviour b = GetBlock(blockId).GetComponent<BlockBehaviour>();
            foreach (MToggle m in b.Toggles)
            {
                if(m.DisplayName.ToUpper() == toggleName.ToUpper())
                {
                    m.IsActive = value;
                    return;
                } 
            }    
        }

        /// <summary>
        /// Used to set slider value of various block properties.
        /// Does nothing if the property is not found.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier.</param>
        /// <param name="toggleName">Case insensitive string specifying the property to be set.
        /// Usually identical to in-game label.</param>
        /// <param name="value">Float value to be set.</param>
        public void SetSliderValue(string blockId, string sliderName, float value)
        {
            BlockBehaviour b = GetBlock(blockId).GetComponent<BlockBehaviour>();
            foreach (MSlider m in b.Sliders)
            {
                if (m.DisplayName.ToUpper() == sliderName.ToUpper())
                {
                    m.Value = value;
                    return;
                }
            }
        }

        /// <summary>
        /// Used to get the toggle value of various block properties.
        /// Throws an exception if the property is not found.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier.</param>
        /// <param name="toggleName">Case insensitive string specifying the property to be set.
        /// Usually identical to in-game label.</param>
        /// <returns>Returns the toggle value of a specified property.</returns>
        public bool GetToggleMode(string blockId, string toggleName)
        {
            BlockBehaviour b = GetBlock(blockId).GetComponent<BlockBehaviour>();
            foreach (MToggle m in b.Toggles)
            {
                if (m.DisplayName.ToUpper() == toggleName.ToUpper())
                {
                    return m.IsActive;
                }
            }
            throw new LuaException("Toggle " + toggleName + " not found.");
        }

        /// <summary>
        /// Used to get the slider value of various block properties.
        /// Throws an exception if the property is not found.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier.</param>
        /// <param name="toggleName">Case insensitive string specifying the property to be set.
        /// Usually identical to in-game label.</param>
        /// <returns>Returns the float value of a specified property.</returns>
        public float GetSliderValue(string blockId, string sliderName)
        {
            BlockBehaviour b = GetBlock(blockId).GetComponent<BlockBehaviour>();
            foreach (MSlider m in b.Sliders)
            {
                if (m.DisplayName.ToUpper() == sliderName.ToUpper())
                {
                    return m.Value;
                }
            }
            throw new LuaException("Slider " + sliderName + " not found.");
        }

        /// <summary>
        /// Used to get the minimum slider value of various block properties.
        /// Throws an exception if the property is not found.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier.</param>
        /// <param name="toggleName">Case insensitive string specifying the property to be set.
        /// Usually identical to in-game label.</param>
        /// <returns>Returns the float value of a specified property.</returns>
        public float GetSliderMin(string blockId, string sliderName)
        {
            BlockBehaviour b = GetBlock(blockId).GetComponent<BlockBehaviour>();
            foreach (MSlider m in b.Sliders)
            {
                if (m.DisplayName.ToUpper() == sliderName.ToUpper())
                {
                    return m.Min;
                }
            }
            throw new LuaException("Slider " + sliderName + " not found.");
        }

        /// <summary>
        /// Used to get the maximum slider value of various block properties.
        /// Throws an exception if the property is not found.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier.</param>
        /// <param name="toggleName">Case insensitive string specifying the property to be set.
        /// Usually identical to in-game label.</param>
        /// <returns>Returns the float value of a specified property.</returns>
        public float GetSliderMax(string blockId, string sliderName)
        {
            BlockBehaviour b = GetBlock(blockId).GetComponent<BlockBehaviour>();
            foreach (MSlider m in b.Sliders)
            {
                if (m.DisplayName.ToUpper() == sliderName.ToUpper())
                {
                    return m.Max;
                }
            }
            throw new LuaException("Slider " + sliderName + " not found.");
        }

        /// Position functions return coordinates of the specified block.

        public float[] GetPosition(string blockId = "STARTING BLOCK 1")
        {
            float[] pos = { 0, 0, 0 };
            pos[0] = this.GetBlock(blockId).transform.position.x;
            pos[1] = this.GetBlock(blockId).transform.position.y;
            pos[2] = this.GetBlock(blockId).transform.position.z;
            return pos;
        }

        public float GetPositionX(string blockId = "STARTING BLOCK 1")
        {
            return this.GetBlock(blockId).transform.position.x;
        }

        public float GetPositionY(string blockId = "STARTING BLOCK 1")
        {
            return this.GetBlock(blockId).transform.position.y;
        }

        public float GetPositionZ(string blockId = "STARTING BLOCK 1")
        {
            return this.GetBlock(blockId).transform.position.z;
        }

        /// Velocity functions return the velocity vector of the pecified block.

        public float[] GetVelocity(string blockId = "STARTING BLOCK 1")
        {
            float[] vel = { 0, 0, 0 };
            vel[0] = this.GetBlock(blockId).GetComponent<Rigidbody>().velocity.x;
            vel[1] = this.GetBlock(blockId).GetComponent<Rigidbody>().velocity.y;
            vel[2] = this.GetBlock(blockId).GetComponent<Rigidbody>().velocity.z;
            return vel;
        }

        public float GetVelocityX(string blockId = "STARTING BLOCK 1")
        {
            return this.GetBlock(blockId).GetComponent<Rigidbody>().velocity.x;
        }

        public float GetVelocityY(string blockId = "STARTING BLOCK 1")
        {
            return this.GetBlock(blockId).GetComponent<Rigidbody>().velocity.z;
        }

        public float GetVelocityZ(string blockId = "STARTING BLOCK 1")
        {
            return this.GetBlock(blockId).GetComponent<Rigidbody>().velocity.z;
        }

        /// Angular velocity functions return the angular velocity vector of the pecified block.

        public float[] GetAngular(string blockId = "STARTING BLOCK 1")
        {
            float[] ang = { 0, 0, 0 };
            ang[0] = this.GetBlock(blockId).GetComponent<Rigidbody>().angularVelocity.x;
            ang[1] = this.GetBlock(blockId).GetComponent<Rigidbody>().angularVelocity.y;
            ang[2] = this.GetBlock(blockId).GetComponent<Rigidbody>().angularVelocity.z;
            return ang;
        }

        public float GetAngularX(string blockId = "STARTING BLOCK 1")
        {
            return this.GetBlock(blockId).GetComponent<Rigidbody>().angularVelocity.x;
        }

        public float GetAngularY(string blockId = "STARTING BLOCK 1")
        {
            return this.GetBlock(blockId).GetComponent<Rigidbody>().angularVelocity.z;
        }

        public float GetAngularZ(string blockId = "STARTING BLOCK 1")
        {
            return this.GetBlock(blockId).GetComponent<Rigidbody>().angularVelocity.z;
        }

        /// Angle functions are swapped in a way to fit starting blocks initial position.
        /// This means that at the start of the simulation, starting blocks angles will be 0, 0, 0.

        /// <summary>
        /// Calculates the heading of the specified block in degrees.
        /// Works the same as GetYaw.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier. Default is starting block.</param>
        /// <returns>Float value ranging from 0 to 360.</returns>
        public float GetHeading(string blockId = "STARTING BLOCK 1")
        {
            Quaternion q = this.GetBlock(blockId).transform.rotation;
            float jaw = Mathf.Atan2(2 * q.y * q.w - 2 * q.x * q.z, 1 - 2 * q.y * q.y - 2 * q.z * q.z) * Mathf.Rad2Deg;
            return jaw < 0 ? jaw + 360 : jaw;
        }

        /// <summary>
        /// Returns directed yaw angle of the specified block in degrees.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier. Default is starting block.</param>
        /// <returns>Float value ranging from -180 to 180.</returns>
        public float GetYaw(string blockId = "STARTING BLOCK 1")
        {
            Quaternion q = this.GetBlock(blockId).transform.rotation;
            return Mathf.Atan2(2 * q.y * q.w - 2 * q.x * q.z, 1 - 2 * q.y * q.y - 2 * q.z * q.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Returns directed pitch angle of the specified block in degrees.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier. Default is starting block.</param>
        /// <returns>Float value ranging from -180 to 180.</returns>
        public float GetPitch(string blockId = "STARTING BLOCK 1")
        {
            Quaternion q = this.GetBlock(blockId).transform.rotation;
            return - Mathf.Atan2(2 * q.x * q.w - 2 * q.y * q.z, 1 - 2 * q.x * q.x - 2 * q.z * q.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Returns directed roll angle of the specified block in degrees.
        /// </summary>
        /// <param name="blockId">Blocks unique identifier. Default is starting block.</param>
        /// <returns>Float value ranging from -180 to 180.</returns>
        public float GetRoll(string blockId = "STARTING BLOCK 1")
        {
            Quaternion q = this.GetBlock(blockId).transform.rotation;
            return - Mathf.Asin(2 * q.x * q.y + 2 * q.z * q.w) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Uses raycast to find out where mouse cursor is pointing.
        /// </summary>
        /// <returns>Returns an x, y, z positional vector of the hit.</returns>
        public float[] GetRaycastHit()
        {
            float[] point = { 0, 0, 0 };
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit))
            {
                point[0] = hit.point.x;
                point[1] = hit.point.y;
                point[2] = hit.point.z;
            }  
            return point;
        }
    }
}
