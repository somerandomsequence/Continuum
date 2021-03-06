﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using System.Net;
using System.IO;

namespace ContinuumNS
{
    [Serializable()]
    public class MERRACollection
    {
        public MERRA[] merraData = new MERRA[0];
        public int numMERRA_Nodes = 1; // OE Methodology 2017.1 uses closest MERRA2 node (future methodology to incorporate interpolated data)
        public DateTime startDate = new DateTime(1989, 1, 1, 0, 0, 0);
        public DateTime endDate = new DateTime(2018, 12, 31, 23, 0, 0);
        public string MERRAfolder = "";
        public string earthdataUser = "";
        public string earthdataPwd = "";

        public struct minMaxReq
        {
            public double minReqLat;
            public double maxReqLat;
            public double minReqLong;
            public double maxReqLong;
        }

        public int numMERRA_Data
        {
            get
            {
                if (merraData == null)
                    return 0;
                else
                    return merraData.Length;
            }            
        }

        public void ClearMERRA()
        {
            merraData = new MERRA[0];
        }

        public void Set_Num_MERRA_Nodes(Continuum thisInst)
        {            
            if (merraData == null)
                numMERRA_Nodes = Convert.ToInt16(thisInst.cboNumMERRA_Nodes.SelectedItem.ToString());
            else if (numMERRA_Data != 0)
            {
                if (((thisInst.cboNumMERRA_Nodes.SelectedIndex == 0 && merraData[0].numMERRA_Nodes != 1) ||
                    (thisInst.cboNumMERRA_Nodes.SelectedIndex == 1 && merraData[0].numMERRA_Nodes != 4) ||
                    (thisInst.cboNumMERRA_Nodes.SelectedIndex == 2 && merraData[0].numMERRA_Nodes != 16)) && thisInst.okToUpdate == true && merraData[0].GotWindTS(thisInst.UTM_conversions))

                {                    
                    numMERRA_Nodes = Convert.ToInt16(thisInst.cboNumMERRA_Nodes.SelectedItem.ToString());
                    merraData = null;

                  /*  for (int i = 0; i < numMERRA_Data; i++)
                    {
                        Array.Resize(ref merraData[i].MERRA_Nodes, numMERRA_Nodes);
                        merraData[i].numMERRA_Nodes = numMERRA_Nodes;
                        merraData[i].ClearAll(true);                        
                    }                    
                      */                    

                }
                else
                    numMERRA_Nodes = Convert.ToInt16(thisInst.cboNumMERRA_Nodes.SelectedItem.ToString());
            }
            else
                numMERRA_Nodes = Convert.ToInt16(thisInst.cboNumMERRA_Nodes.SelectedItem.ToString());

            thisInst.ChangesMade();
        }        
        
        /// <summary> Returns array of lat/long coordinates of MERRA2 coordinates for specified lat/long </summary>        
        public UTM_conversion.Lat_Long[] GetRequiredMERRACoords(double latitude, double longitude)
        {
            UTM_conversion.Lat_Long[] theseReqCoords = new UTM_conversion.Lat_Long[numMERRA_Nodes];

            double minReqLat = Math.Round(latitude / 0.5, 0) * 0.5;
            double  minReqLong = Math.Round(longitude / 0.625) * 0.625;
            double maxReqLat = minReqLat;
            double maxReqLong = minReqLong;

            if (numMERRA_Nodes == 1)
            {                
                theseReqCoords[0].latitude = minReqLat;
                theseReqCoords[0].longitude = minReqLong;
            }
            else if (numMERRA_Nodes == 4)
            {                
                if (latitude > minReqLat)
                    maxReqLat = minReqLat + 0.5;
                else
                {
                    minReqLat = minReqLat - 0.5;
                    maxReqLat = minReqLat + 0.5;
                }
                
                if (longitude > minReqLong)
                    maxReqLong = minReqLong + 0.625;
                else
                {
                    minReqLong = minReqLong - 0.625;
                    maxReqLong = minReqLong + 0.625;
                }

                theseReqCoords[0].latitude = minReqLat;
                theseReqCoords[0].longitude = minReqLong;

                theseReqCoords[1].latitude = minReqLat;
                theseReqCoords[1].longitude = maxReqLong;

                theseReqCoords[2].latitude = maxReqLat;
                theseReqCoords[2].longitude = minReqLong;

                theseReqCoords[3].latitude = maxReqLat;
                theseReqCoords[3].longitude = maxReqLong;
            }
            else if (numMERRA_Nodes == 16)
            {
                if (latitude > minReqLat)
                {
                    minReqLat = minReqLat - 0.5;
                    maxReqLat = minReqLat + 3 * 0.5;
                }
                else
                {
                    minReqLat = minReqLat - 1;
                    maxReqLat = minReqLat + 3 * 0.5;
                }

                if (longitude > minReqLong)
                {
                    minReqLong = minReqLong - 0.625;
                    maxReqLong = minReqLong + 3 * 0.625;
                }
                else
                {
                    minReqLong = minReqLong - 2 * 0.625;
                    maxReqLong = minReqLong + 3 * 0.625;
                }

                int latInd = 0;
                int longInd = 0;

                for (int i = 0; i < numMERRA_Nodes; i++)
                {
                    theseReqCoords[i].latitude = minReqLat + 0.5 * latInd;
                    theseReqCoords[i].longitude = minReqLong + 0.625 * longInd;

                    latInd++;
                    if (latInd >= 4)
                    {
                        latInd = 0;
                        longInd++;
                    }
                }

            }

            return theseReqCoords;
        }

        public UTM_conversion.Lat_Long[] GetRequiredNewMERRANodeCoords(double latitude, double longitude, Continuum thisInst)
        {
            // Finds the min/max lat/long of MERRA nodes needed for specified latitude and longitude. Loops through existing MERRA data to see
            // if additional MERRA data is needed
            UTM_conversion.Lat_Long[] newRequiredMERRANodes = new UTM_conversion.Lat_Long[0];
            int numNewReqNodes = 0;

            UTM_conversion.Lat_Long[] theseRequiredNodes = GetRequiredMERRACoords(latitude, longitude);
            UTM_conversion.Lat_Long[] existingNodes = new UTM_conversion.Lat_Long[0];
            int numExistingNodes = 0;

            // Loop through required nodes and see what additional nodes are needed
            NodeCollection nodeList = new NodeCollection();
            string connString = nodeList.GetDB_ConnectionString(thisInst.savedParams.savedFileName);

            using (var context = new Continuum_EDMContainer(connString))
            {
                var theseNodes = from N in context.MERRA_Node_table select N;

                foreach (var N in theseNodes)
                {
                    numExistingNodes++;
                    Array.Resize(ref existingNodes, numExistingNodes);
                    existingNodes[numExistingNodes - 1].latitude = N.latitude;
                    existingNodes[numExistingNodes - 1].longitude = N.longitude;
                }
            }

            for (int i = 0; i < numMERRA_Nodes; i++)
            {
                bool gotIt = false;
                for (int j = 0; j < numExistingNodes; j++)
                {
                    if (existingNodes[j].latitude == theseRequiredNodes[i].latitude && existingNodes[j].longitude == theseRequiredNodes[i].longitude)
                    {
                        gotIt = true;
                        break;
                    }
                }

                if (gotIt == false)
                {
                    numNewReqNodes++;
                    Array.Resize(ref newRequiredMERRANodes, numNewReqNodes);
                    newRequiredMERRANodes[numNewReqNodes - 1].latitude = theseRequiredNodes[i].latitude;
                    newRequiredMERRANodes[numNewReqNodes - 1].longitude = theseRequiredNodes[i].longitude;
                }

            }                                       

            return newRequiredMERRANodes;
        }

        public bool areSameMERRAData(MERRA merra1, MERRA merra2)
        {
            // Compares the two MERRA objects and returns true if they are identical
            bool areSame = true;

            if (merra1.MERRA_Nodes == null && merra2.MERRA_Nodes == null)
                return areSame;
            else if ((merra1.MERRA_Nodes == null && merra2.MERRA_Nodes != null) || (merra1.MERRA_Nodes != null && merra2.MERRA_Nodes == null))
                areSame = false;
            else if (merra1.MERRA_Nodes.Length != merra2.MERRA_Nodes.Length)
                areSame = false;

            return areSame;
        }        
        
        public MERRA GetMERRA(double latitude, double longitude)
        {
            // Returns MERRA object with specified latitude and longitude
            MERRA thisMERRA = new MERRA();

            for (int i = 0; i < numMERRA_Data; i++)
                if (merraData[i].interpData.Coords.latitude == Math.Round(latitude, 3) && merraData[i].interpData.Coords.longitude == Math.Round(longitude, 3))
                    thisMERRA = merraData[i];

            return thisMERRA;
        }

        public void AddMERRA_GetDataFromTextFiles(double thisLat, double thisLong, int offset, Continuum thisInst, Met thisMet, bool isTest)
        {
            // Adds new MERRA object to list. Figures out if additional MERRA nodes need to be uploaded from textfiles.
            // Runs MCP at Met site (if thisMet not null) if have all MERRA node data. Calls BW worker to uploaded additional data if needed.

            // Create new MERRA object and assign lat, long, and node lat/long            
            MERRA thisMERRA = new MERRA();
            thisMERRA.Set_Interp_LatLon_Dates_Offset(thisLat, thisLong, offset, thisInst);
            thisMERRA.numMERRA_Nodes = numMERRA_Nodes;
            thisMERRA.MERRA_Nodes = new MERRA.MERRA_Node_Data[numMERRA_Nodes];

            if (thisMet.name == null)
                thisMERRA.isUserDefined = true;

            if (MERRAfolder == "")
            {
                try
                {
                    MessageBox.Show("Please select folder containing MERRA2 data .ascii files.");
                    if (thisInst.fbd_MERRAData.ShowDialog() == DialogResult.OK)
                        MERRAfolder = thisInst.fbd_MERRAData.SelectedPath;
                    else
                        return;

                    SetMERRA2LatLong(thisInst);
                }
                catch
                {
                    MessageBox.Show("Folder path not valid.", "", MessageBoxButtons.OK);
                    return;
                }
            }

            // Figure out if MERRA textfile has the necessary lat/long range and get MERRA node coordinates
            bool gotCoords = thisMERRA.Find_MERRA_Coords(MERRAfolder); 

            if (gotCoords == false)
                return;                                                        

      //      thisMERRA.Get_Export_Params(thisInst);
                                   
            DialogResult doMCP = DialogResult.No;

            if (thisMet.name != null && isTest == false && (thisInst.metList.ThisCount == 1 || thisInst.metList.isMCPd == false))
                doMCP = MessageBox.Show("Do you want to conduct MCP at selected met?", "Continuum 3.0", MessageBoxButtons.YesNo);
            else if (thisMet.name != "" && isTest == false && thisInst.metList.ThisCount > 1 && thisInst.metList.isMCPd == true)
                doMCP = DialogResult.Yes;
            else if (isTest == true)
                doMCP = DialogResult.No;

            if (doMCP == DialogResult.Yes)
            {                
                thisInst.metList.isMCPd = true;
                thisInst.modelList.ClearAllExceptImported();
                thisInst.turbineList.ClearAllWSEsts();
                thisInst.turbineList.ClearAllGrossEsts();
                thisInst.turbineList.ClearAllNetEsts();
                thisInst.mapList.ClearAllMaps();
                thisInst.metPairList.ClearAll();
            }            
            
            // Figure out what MERRA nodes need to be downloaded
            UTM_conversion.Lat_Long[] requiredMERRANode = GetRequiredNewMERRANodeCoords(thisLat, thisLong, thisInst);

            if (requiredMERRANode.Length != 0)
            {              

                MERRA.MERRA_Pull[] nodesToPull = new MERRA.MERRA_Pull[requiredMERRANode.Length];

                for (int i = 0; i < requiredMERRANode.Length; i++)
                {
                    nodesToPull[i].Coords.latitude = requiredMERRANode[i].latitude;
                    nodesToPull[i].Coords.longitude = requiredMERRANode[i].longitude;
                    nodesToPull[i].UTM = thisInst.UTM_conversions.LLtoUTM(nodesToPull[i].Coords.latitude, nodesToPull[i].Coords.longitude);
                }

                // Check to see that MERRA data files have required lat/long and assign XInd and YInd
                bool gotIndices = thisMERRA.GetMERRAPullXYIndices(ref nodesToPull, MERRAfolder);

                if (gotIndices == false)
                    return;

                BackgroundWork.Vars_for_MERRA Vars_for_MERRA = new BackgroundWork.Vars_for_MERRA();
                Vars_for_MERRA.thisInst = thisInst;
                Vars_for_MERRA.thisMERRA = thisMERRA;
                Vars_for_MERRA.MCP_type = thisInst.Get_MCP_Method();
                Vars_for_MERRA.thisMet = thisMet;
                Vars_for_MERRA.nodesToPull = nodesToPull;

                thisInst.BW_worker = new BackgroundWork();
                thisInst.BW_worker.Call_BW_MERRA2_Import(Vars_for_MERRA);
            }
            else
            {
                // Have all necessary MERRA nodes and user wants to do MCP so.... Run MCP!

                // Add MERRA object to list
                Array.Resize(ref merraData, numMERRA_Data + 1);
                merraData[numMERRA_Data - 1] = thisMERRA;
                thisMERRA.GetMERRADataFromDB(thisInst);
                thisMERRA.GetInterpData(thisInst.UTM_conversions);

                if (doMCP == DialogResult.Yes)
                {
                    thisMet.WSWD_Dists = new Met.WSWD_Dist[0];
                    thisInst.metList.RunMCP(ref thisMet, thisMERRA, thisInst, thisInst.Get_MCP_Method());
                    thisMet.CalcAllLT_WSWD_Dists(thisInst, thisMet.mcp.LT_WS_Ests); // Calculates LT wind speed / wind direction distributions for using all day and using each season and each time of day (Day vs. Night)
                    thisInst.updateThe.AllTABs(thisInst);
                }
                else
                {
                    thisInst.updateThe.MERRA_Dropdowns(thisInst);
                    thisInst.updateThe.MERRA_TAB(thisInst);
                }
                    
            }
        }
        
        public void deleteMERRA(double latitude, double longitude, Continuum thisInst)
        {
            // Deletes MERRA data from list
            int newCount = numMERRA_Data - 1;

            if (newCount > 0)
            {
                MERRA[] tempList = new MERRA[newCount]; // Create list of met towers that you//re keeping(so size one less than before)
                int tempIndex = 0;

                for (int i = 0; i < numMERRA_Data; i++)
                {
                    if (merraData[i].interpData.Coords.latitude != Math.Round(latitude, 3) && merraData[i].interpData.Coords.longitude != Math.Round(longitude, 3))
                    {
                        tempList[tempIndex] = merraData[i];
                        tempIndex++;
                    }
                }
                merraData = tempList;
            }
            else
            {
                merraData = new MERRA[0];
            }
        }

        public void DeleteAllMERRADataFromDB(Continuum thisInst)
        {
            // clears all MERRA data from DB
            NodeCollection nodeList = new NodeCollection();
            string connString = nodeList.GetDB_ConnectionString(thisInst.savedParams.savedFileName);

            try
            {
                using (var ctx = new Continuum_EDMContainer(connString))
                {
                    ctx.Database.ExecuteSqlCommand("DELETE FROM MERRA_Node_table");
                    ctx.SaveChanges();                    
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }

        public void DeleteMERRANodeDataFromDB(double latitude, double longitude, Continuum thisInst)
        { 
            // Deletes MERRA Node data from DB
            NodeCollection nodeList = new NodeCollection();
            string connString = nodeList.GetDB_ConnectionString(thisInst.savedParams.savedFileName);
            try
            {
                using (var context = new Continuum_EDMContainer(connString))
                {
                    var merra_db = from N in context.MERRA_Node_table where N.latitude == latitude & N.longitude == longitude select N;

                    foreach (var N in merra_db)
                        context.MERRA_Node_table.Remove(N);                                     

                    context.SaveChanges();                    
                }
            }
            catch (Exception ex)
            {                
                MessageBox.Show(ex.InnerException.ToString());
                return;
            }

        }

        public int GetLTRefLength(Continuum thisInst)
        {
            // returns number of hours in long-term ref data
            int thisLength = 0;

            TimeSpan timeSpan = endDate - startDate;
            thisLength = timeSpan.Days * 24 + timeSpan.Hours + 1;

            return thisLength;
        }

        public void ClearMERRA_Data(Continuum thisInst)
        {
            for (int i = 0; i < numMERRA_Data; i++)
            {
                merraData[i].Clear_MERRA2_Node_Data();
                merraData[i].interpData.TS_Data = new MERRA.Wind_TS_with_Prod[0];
            }
        }
        
        public void ApplyPC_ToAllMERRA(Continuum thisInst, TurbineCollection.PowerCurve powerCurve)
        {
            // Calculates MERRA2 energy production using power curve at each MERRA2 interpolated node
            
            for (int i = 0; i < numMERRA_Data; i++)
            {
                merraData[i].powerCurve = powerCurve;
                merraData[i].ApplyPC(ref merraData[i].interpData.TS_Data);
            }
        }

        public void ClearMERRA_ProdStats()
        {
            // Clears monthly and annual energy production estimates. Called after a power curve is deleted
            
            for (int i = 0; i < numMERRA_Data; i++)
            {
                merraData[i].Reset_MonthProdStats();
                merraData[i].Reset_AnnualProdStats();
                merraData[i].powerCurve.Clear();
            }
        }

        public bool GotMERRA(double latitude, double longitude)
        {
            // Returns true if have MERRA with specified latitude and longitude in list
            bool gotIt = false;

            for (int i = 0; i < numMERRA_Data; i++)
                if (merraData[i].interpData.Coords.latitude == Math.Round(latitude, 3) && merraData[i].interpData.Coords.longitude == Math.Round(longitude, 3))
                    gotIt = true;

            return gotIt;
        }                

        public async Task NASA_LogInAsync(Continuum thisInst)
        {
                                  
            if (earthdataUser == "" || earthdataPwd == "")
            {
                try
                {
                    earthdataUser = Microsoft.VisualBasic.Interaction.InputBox("Enter your Earthdata username. If you don't have one, go to https://urs.earthdata.nasa.gov/ to create an account.", "Continuum 3").ToString();
                }
                catch
                {
                    MessageBox.Show("Invalid Earthdata username");
                    return;
                }

                if (earthdataUser == "")
                    return;
                
                try
                {
                    earthdataPwd = Microsoft.VisualBasic.Interaction.InputBox("Enter your Earthdata password. If you don't have one, go to https://urs.earthdata.nasa.gov/ to create an account.", "Continuum 3").ToString();
                }
                catch
                {
                    MessageBox.Show("Invalid Earthdata password");
                    return;
                }

                if (earthdataPwd == "")
                    return;
            }

            BackgroundWork.Vars_for_BW Vars_for_MERRA = new BackgroundWork.Vars_for_BW();
            Vars_for_MERRA.thisInst = thisInst;
            
            thisInst.BW_worker = new BackgroundWork();
            thisInst.BW_worker.Call_BW_MERRA2_Download(Vars_for_MERRA);

        }

        public int GetLatitudeIndex(double thisLat)
        {            
            int latInd = (int)(2 * thisLat + 180);

            return latInd;
        }

        public int GetLongitudeIndex(double thisLong)
        {
            int longInd = (int)(1.6 * thisLong + 288);

            return longInd;
        }

        public string GetMERRA2URL(DateTime thisDay, double minLat, double maxLat, double minLong, double maxLong)
        {
            int dateNum = 0;

            string thisYear = thisDay.Year.ToString();
            string thisMonth = thisDay.Month.ToString();

            if (thisDay.Month < 10)
                thisMonth = "0" + thisMonth;

            string thisDayStr = thisDay.Day.ToString();

            if (thisDay.Day < 10)
                thisDayStr = "0" + thisDayStr;

            if (thisDay.Year <= 1991)
                dateNum = 100;
            else if (thisDay.Year <= 2000)
                dateNum = 200;
            else if (thisDay.Year <= 2010)
                dateNum = 300;
            else
                dateNum = 400;

            int minLatInd = GetLatitudeIndex(minLat);
            int maxLatInd = GetLatitudeIndex(maxLat);
            int minLongInd = GetLongitudeIndex(minLong);
            int maxLongInd = GetLongitudeIndex(maxLong);

            string URL = "https://goldsmr4.gesdisc.eosdis.nasa.gov/opendap/MERRA2/M2T1NXSLV.5.12.4/";
            URL += thisYear + "/" + thisMonth + "/";
            URL += "MERRA2_" + dateNum.ToString() + ".tavg1_2d_slv_Nx." + thisYear + thisMonth + thisDayStr + ".nc4.ascii?";
            URL += "T10M[0:23][" + minLatInd + ":" + maxLatInd + "][" + minLongInd + ":" + maxLongInd + "],";
            URL += "U50M[0:23][" + minLatInd + ":" + maxLatInd + "][" + minLongInd + ":" + maxLongInd + "],";
            URL += "V50M[0:23][" + minLatInd + ":" + maxLatInd + "][" + minLongInd + ":" + maxLongInd + "],";
            URL += "SLP[0:23][" + minLatInd + ":" + maxLatInd + "][" + minLongInd + ":" + maxLongInd + "],";
            URL += "PS[0:23][" + minLatInd + ":" + maxLatInd + "][" + minLongInd + ":" + maxLongInd + "],";
            URL += "lat[" + minLatInd + ":" + maxLatInd + "]," + "time[0:23]," + "lon[" + minLongInd + ":" + maxLongInd + "]";

            return URL;

        }

        public void ChangeMERRA2Folder(Continuum thisInst)
        {
            MessageBox.Show("Please select folder containing MERRA2 .ascii data files.");
            if (thisInst.fbd_MERRAData.ShowDialog() == DialogResult.OK)
                MERRAfolder = thisInst.fbd_MERRAData.SelectedPath;
            else
                return;

            thisInst.txt_MERRA2_folder.Text = MERRAfolder.ToString();

            SetMERRA2LatLong(thisInst);
            
        }

        public void SetMERRA2LatLong (Continuum thisInst)
        {
            if (MERRAfolder == null || MERRAfolder == "")
                return;

            // If there are files, check one and get min/max lat/long
            string[] MERRAfiles = Directory.GetFiles(MERRAfolder, "*.ascii");
            string line;

            if (MERRAfiles == null)
            {
                thisInst.txtMinLat.Text = "";
                thisInst.txtMaxLat.Text = "";
                thisInst.txtMinLong.Text = "";
                thisInst.txtMaxLong.Text = "";

                thisInst.txtMinLat.Enabled = true;
                thisInst.txtMaxLat.Enabled = true;
                thisInst.txtMinLong.Enabled = true;
                thisInst.txtMaxLong.Enabled = true;

                thisInst.btnDownloadMERRA2.BackColor = System.Drawing.Color.LightCoral;
                return;
            }


            if (MERRAfiles.Length == 0)
            {
                thisInst.txtMinLat.Text = "";
                thisInst.txtMaxLat.Text = "";
                thisInst.txtMinLong.Text = "";
                thisInst.txtMaxLong.Text = "";

                thisInst.txtMinLat.Enabled = true;
                thisInst.txtMaxLat.Enabled = true;
                thisInst.txtMinLong.Enabled = true;
                thisInst.txtMaxLong.Enabled = true;

                thisInst.btnDownloadMERRA2.BackColor = System.Drawing.Color.LightCoral;
                return;
            }

            StreamReader file = new StreamReader(MERRAfiles[0]);

            char[] delims = { ',' };
            int numLats = 0;
            int numLongs = 0;
            double[] lats = new double[numLats];
            double[] longs = new double[numLongs];

            while ((line = file.ReadLine()) != null)
            {
                string[] substrings = line.Split(delims);

                if (substrings[0] == "lat") // read in all latitudes
                {
                    numLats = substrings.Length - 1;
                    Array.Resize(ref lats, numLats);

                    for (int i = 0; i < numLats; i++)
                        lats[i] = Convert.ToDouble(substrings[i + 1]);
                }

                if (substrings[0] == "lon") // read in all longitudes
                {
                    numLongs = substrings.Length - 1;
                    Array.Resize(ref longs, numLongs);

                    for (int i = 0; i < numLongs; i++)
                        longs[i] = Convert.ToDouble(substrings[i + 1]);
                }
            }

            thisInst.txtMinLat.Text = lats[0].ToString();
            thisInst.txtMaxLat.Text = lats[numLats - 1].ToString();
            thisInst.txtMinLong.Text = longs[0].ToString();
            thisInst.txtMaxLong.Text = longs[numLongs - 1].ToString();

            thisInst.txtMinLat.Enabled = false;
            thisInst.txtMaxLat.Enabled = false;
            thisInst.txtMinLong.Enabled = false;
            thisInst.txtMaxLong.Enabled = false;

            thisInst.btnDownloadMERRA2.BackColor = System.Drawing.Color.MediumSeaGreen;

            file.Close();
        }

        public string CreateMERRA2filename(DateTime thisDate)
        {
            string MERRA2name = "";
            int dateNum = 0;
            if (thisDate.Year <= 1991)
                dateNum = 100;
            else if (thisDate.Year <= 2000)
                dateNum = 200;
            else if (thisDate.Year <= 2010)
                dateNum = 300;
            else
                dateNum = 400;

            string thisMonth = thisDate.Month.ToString();
            if (thisDate.Month < 10)
                thisMonth = "0" + thisMonth;

            string thisDay = thisDate.Day.ToString();
            if (thisDate.Day < 10)
                thisDay = "0" + thisDay;

            MERRA2name = "MERRA2_" + dateNum.ToString() + ".tavg1_2d_slv_Nx." + thisDate.Year + thisMonth + thisDay + ".nc4.ascii";

            return MERRA2name;
        }

        public bool MERRA2FileExists(DateTime thisDate)
        {
            bool fileExists = false;
            string fileName = CreateMERRA2filename(thisDate);

            try
            {
                string[] thisFile = Directory.GetFiles(MERRAfolder, fileName);
                if (thisFile.Length == 1)
                    fileExists = true;
                else
                    fileExists = false;
            }
            catch
            {
                return fileExists;
            }

            return fileExists;
        }

        public void SaveMERRA2DataFile(HttpWebResponse response, DateTime thisDate)
        {
            // Now access the data

            long length = response.ContentLength;
            string type = response.ContentType;
            Stream stream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream);
            string MERRA2filename = CreateMERRA2filename(thisDate);
            StreamWriter writer = new StreamWriter(MERRAfolder + "\\" + MERRA2filename);

            // Save to file
            while (reader.EndOfStream == false)
            {
                string thisLine = reader.ReadLine();
                writer.WriteLine(thisLine);
            }
                                    
            writer.Close();
            stream.Close();
            reader.Close();
        }
        
    }
}
